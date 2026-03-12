using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheelMenu.Settings.Models;
using WheelMenu.Windows;

namespace WheelMenu.Settings.ViewModels;

public partial class ActionManagerPageViewModel : ObservableObject
{
    private readonly WheelConfig _config;

    // ===== 场景列表 =====
    public ObservableCollection<SceneItemViewModel> Scenes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionPages))]
    [NotifyPropertyChangedFor(nameof(DisableWheelForScene))]
    private SceneItemViewModel? _selectedScene;

    partial void OnSelectedSceneChanged(SceneItemViewModel? value)
    {
        SelectedCell = null;
        RefreshActionPages();
        RefreshWheelSectors();
    }

    // ===== 动作页 =====
    public ObservableCollection<ActionPageViewModel> ActionPages { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPageCells))]
    private ActionPageViewModel? _selectedPage;

    partial void OnSelectedPageChanged(ActionPageViewModel? value)
    {
        // 切换动作页时清除选中格子
        SelectedCell = null;
    }

    public ObservableCollection<ActionCellViewModel> CurrentPageCells =>
        SelectedPage?.Cells ?? new ObservableCollection<ActionCellViewModel>();

    // ===== 当前选中格子（右键/编辑用）=====
    [ObservableProperty]
    private ActionCellViewModel? _selectedCell;

    // ===== 轮盘扇区 =====
    // 所有扇区的 VM（包含所有圈层），Key = SectorNumber
    public Dictionary<int, WheelSectorViewModel> AllSectors { get; } = new();
    public ObservableCollection<WheelSectorViewModel> Ring1Sectors { get; } = new();
    public ObservableCollection<WheelSectorViewModel> Ring2Sectors { get; } = new();
    public ObservableCollection<WheelSectorViewModel> Ring3Sectors { get; } = new();

    // 当前选中扇区（高亮）
    [ObservableProperty]
    private WheelSectorViewModel? _selectedSector;

    // ===== 外圈16格开关（从全局设置读取）=====
    public bool Outer16Mode
    {
        get => _config.Settings.OuterRing16Mode;
        set
        {
            if (_config.Settings.OuterRing16Mode == value) return;
            _config.Settings.OuterRing16Mode = value;
            OnPropertyChanged();
            // 重建外圈扇区（8→16或16→8）
            RebuildRing2Sectors();
            MarkDirty();
        }
    }

    // ===== 禁用开关 =====
    public bool DisableWheelForScene
    {
        get => _selectedScene?.Scene.DisableWheel ?? false;
        set
        {
            if (_selectedScene == null) return;
            _selectedScene.Scene.DisableWheel = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    // ===== 构造 =====
    public ActionManagerPageViewModel(WheelConfig config)
    {
        _config = config;
        InitWheelSectors();
        RefreshScenes();
    }

    // ===== 初始化轮盘扇区结构 =====
    private void InitWheelSectors()
    {
        AllSectors.Clear();
        Ring1Sectors.Clear();
        Ring2Sectors.Clear();
        Ring3Sectors.Clear();

        var dirs8 = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        // Ring1：序号 1~8
        for (int i = 0; i < 8; i++)
        {
            var vm = new WheelSectorViewModel(i + 1, "ring1", dirs8[i]);
            Ring1Sectors.Add(vm);
            AllSectors[i + 1] = vm;
        }

        RebuildRing2Sectors();

        // Ring3：序号固定偏移（依赖Ring2数量）
        RebuildRing3Sectors();
    }

    private void RebuildRing2Sectors()
    {
        // 清除旧 Ring2 扇区（从 AllSectors 也移除）
        foreach (var vm in Ring2Sectors)
            AllSectors.Remove(vm.SectorNumber);
        Ring2Sectors.Clear();

        bool is16 = _config.Settings.OuterRing16Mode;
        int count = is16 ? 16 : 8;
        var dirs8 = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        var dirs16 = new[]
        {
            "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
            "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"
        };
        var dirs = is16 ? dirs16 : dirs8;

        for (int i = 0; i < count; i++)
        {
            int sectorNum = 9 + i;
            var vm = new WheelSectorViewModel(sectorNum, "ring2", dirs[i]);
            Ring2Sectors.Add(vm);
            AllSectors[sectorNum] = vm;
        }

        // Ring3 序号紧跟 Ring2
        RebuildRing3Sectors();

        // 重新应用当前场景绑定
        if (_selectedScene != null)
            RefreshWheelSectors();
    }

    private void RebuildRing3Sectors()
    {
        foreach (var vm in Ring3Sectors)
            AllSectors.Remove(vm.SectorNumber);
        Ring3Sectors.Clear();

        int ring2Count = _config.Settings.OuterRing16Mode ? 16 : 8;
        int ring3Start = 9 + ring2Count;  // 8格=17，16格=25
        var dirs8 = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        for (int i = 0; i < 8; i++)
        {
            int sectorNum = ring3Start + i;
            var vm = new WheelSectorViewModel(sectorNum, "ring3", dirs8[i]);
            Ring3Sectors.Add(vm);
            AllSectors[sectorNum] = vm;
        }
    }

    // ===== 场景管理 =====
    private void RefreshScenes()
    {
        Scenes.Clear();
        var priority = new[] { "global", "common", "taskbar", "desktop" };
        foreach (var key in priority)
            if (_config.Scenes.TryGetValue(key, out var sc))
                Scenes.Add(new SceneItemViewModel(key, sc, key == "global"));

        foreach (var (k, v) in _config.Scenes)
            if (!priority.Contains(k))
                Scenes.Add(new SceneItemViewModel(k, v, false));

        SelectedScene = Scenes.FirstOrDefault();
    }

    [RelayCommand]
    private void AddScene()
    {
        var dlg = new AddSceneDialog { Owner = GetOwner() };
        if (dlg.ShowDialog() != true) return;
        string key = dlg.ProcessName.ToLowerInvariant().Trim();
        if (string.IsNullOrEmpty(key) || _config.Scenes.ContainsKey(key)) return;
        _config.Scenes[key] = new SceneConfig
            { Name = dlg.DisplayName, Process = key };
        RefreshScenes();
        SelectedScene = Scenes.FirstOrDefault(s => s.Key == key);
        MarkDirty();
    }

    [RelayCommand]
    private void DeleteScene(SceneItemViewModel? item)
    {
        if (item == null || item.IsGlobal) return;
        if (System.Windows.MessageBox.Show($"确定删除场景\"{item.Scene.Name}\"？",
            "确认", System.Windows.MessageBoxButton.YesNo)
            != System.Windows.MessageBoxResult.Yes) return;
        _config.Scenes.Remove(item.Key);
        RefreshScenes();
        MarkDirty();
    }

    // [RelayCommand]
    // private void EditScene()
    // {
    //     if (SelectedScene == null || SelectedScene.IsGlobal) return;
    //     // TODO: 创建InputDialog
    //     // var dlg = new Views.InputDialog("重命名场景", "名称：",
    //     //     SelectedScene.Scene.Name) { Owner = GetOwner() };
    //     // if (dlg.ShowDialog() != true) return;
    //     // SelectedScene.Scene.Name = dlg.Result!;
    //     RefreshScenes();
    //     MarkDirty();
    // }

    private static bool IsSystemScene(string key) =>
        key is "global" or "common" or "taskbar" or "desktop";

    // ===== 动作页管理 =====
    private void RefreshActionPages()
    {
        ActionPages.Clear();
        if (_selectedScene == null) return;
        foreach (var page in _selectedScene.Scene.ActionPages)
            ActionPages.Add(new ActionPageViewModel(page));
        SelectedPage = ActionPages.FirstOrDefault();
    }

    // [RelayCommand]
    // private void AddActionPage()
    // {
    //     if (_selectedScene == null) return;
    //     // TODO: 创建InputDialog
    //     var dlg = new Views.InputDialog("添加动作页", "动作页名称：", "新动作页")
    //         { Owner = GetOwner() };
    //     if (dlg.ShowDialog() != true ||
    //         string.IsNullOrWhiteSpace(dlg.Result)) return;
    //
    //     var page = new ActionPage { Name = dlg.Result };
    //     _selectedScene.Scene.ActionPages.Add(page);
    //     var vm = new ActionPageViewModel(page);
    //     ActionPages.Add(vm);
    //     SelectedPage = vm;
    //     MarkDirty();
    // }

    /// <summary>添加动作页（简单版本，使用默认名称）</summary>
    [RelayCommand]
    private void AddActionPage()
    {
        if (_selectedScene == null) return;
        var pageName = $"动作页 {_selectedScene.Scene.ActionPages.Count + 1}";
        var page = new ActionPage(pageName);
        _selectedScene.Scene.ActionPages.Add(page);
        var vm = new ActionPageViewModel(page);
        ActionPages.Add(vm);
        SelectedPage = vm;
        MarkDirty();
    }

    [RelayCommand]
    private void DeleteActionPage(ActionPageViewModel? page)
    {
        if (page == null || _selectedScene == null) return;
        if (System.Windows.MessageBox.Show(
            $"确定删除动作页\"{page.Name}\"？关联的扇区绑定也将解除。",
            "确认", System.Windows.MessageBoxButton.YesNo)
            != System.Windows.MessageBoxResult.Yes) return;

        // 解除所有指向此动作页的扇区绑定
        var toRemove = _selectedScene.Scene.Bindings
            .Where(kv => kv.Value.SourcePageId == page.Model.Id)
            .Select(kv => kv.Key).ToList();
        foreach (var k in toRemove)
            _selectedScene.Scene.Bindings.Remove(k);

        _selectedScene.Scene.ActionPages.Remove(page.Model);
        ActionPages.Remove(page);
        SelectedPage = ActionPages.FirstOrDefault();
        RefreshWheelSectors();
        MarkDirty();
    }

    // 动作页 Tab 排序（拖拽）
    public void MoveActionPage(int fromIndex, int toIndex)
    {
        if (_selectedScene == null) return;
        if (fromIndex == toIndex) return;
        var pages = _selectedScene.Scene.ActionPages;
        var page = pages[fromIndex];
        pages.RemoveAt(fromIndex);
        pages.Insert(toIndex, page);
        var vm = ActionPages[fromIndex];
        ActionPages.RemoveAt(fromIndex);
        ActionPages.Insert(toIndex, vm);
        MarkDirty();
    }

    // ===== 格子编辑（右键流程）=====

    /// <summary>
    /// 右键格子后第一步：选动作类型菜单。
    /// 由 Code-Behind 呼出 ContextMenu，用户选择后调用此方法。
    /// </summary>
    public void OnCellTypeSelected(ActionCellViewModel cell, ActionType type)
    {
        SelectedCell = cell;
        // 打开对应的配置弹窗
        bool saved = OpenActionConfigDialog(cell, type);
        if (saved)
        {
            cell.Model.Type = type;
            cell.Refresh();
            // 若该格子已绑定到某扇区，同步更新扇区显示
            SyncBoundSectors(cell);
            MarkDirty();
        }
    }

    /// <summary>根据动作类型打开对应配置弹窗，返回是否保存成功</summary>
    private bool OpenActionConfigDialog(ActionCellViewModel cell, ActionType type)
    {
        // TODO: 实现配置弹窗
        // System.Windows.Window? dlg = type switch
        // {
        //     ActionType.Hotkey => new Views.Dialogs.HotkeyDialog(cell.Model),
        //     ...
        // };
        // if (dlg == null) { ... }
        // dlg.Owner = GetOwner();
        // return dlg.ShowDialog() == true;
        
        // 临时：直接保存
        if (type == ActionType.None)
        {
            cell.Model.Type = ActionType.None;
            cell.Model.Name = string.Empty;
            cell.Model.Value = string.Empty;
            cell.Refresh();
            return true;
        }
        
        // 模拟保存
        cell.Model.Type = type;
        cell.Model.Name = $"动作_{type}";
        cell.Refresh();
        return true;
    }

    /// <summary>双击格子 → 直接重新编辑（保持原类型，重新打开配置弹窗）</summary>
    public void OnCellDoubleClicked(ActionCellViewModel cell)
    {
        if (cell.Model.IsEmpty) return;
        bool saved = OpenActionConfigDialog(cell, cell.Model.Type);
        if (saved)
        {
            cell.Refresh();
            SyncBoundSectors(cell);
            MarkDirty();
        }
    }

    /// <summary>右键→清除动作</summary>
    public void OnClearCell(ActionCellViewModel cell)
    {
        cell.Model.Type = ActionType.None;
        cell.Model.Name = string.Empty;
        cell.Model.Value = string.Empty;
        cell.Model.IconPath = null;
        cell.Refresh();
        SyncBoundSectors(cell);
        MarkDirty();
    }

    // ===== 拖拽：格子 → 扇区绑定 =====

    /// <summary>
    /// 动作页格子被拖拽到轮盘扇区时调用。
    /// sectorNumber：1-based 全局扇区序号
    /// </summary>
    public void BindCellToSector(ActionCellViewModel cell, int sectorNumber)
    {
        if (_selectedScene == null) return;
        if (cell.Model.IsEmpty)
        {
            System.Windows.MessageBox.Show("该格子没有设置动作，无法绑定。",
                "提示", System.Windows.MessageBoxButton.OK);
            return;
        }

        string key = $"sector_{sectorNumber}";
        _selectedScene.Scene.Bindings[key] = new WheelSectorBinding
        {
            SectorNumber = sectorNumber,
            Ring = GetRingBySectorNumber(sectorNumber),
            Direction = GetDirectionBySectorNumber(sectorNumber),
            SourcePageId = GetPageIdByCell(cell),
            SourceCellIndex = cell.Model.CellIndex,
            DisplayName = cell.Model.Name,
            IconPath = cell.Model.IconPath
        };

        // 更新扇区 VM
        if (AllSectors.TryGetValue(sectorNumber, out var sectorVm))
            sectorVm.ApplyBinding(_selectedScene.Scene.Bindings[key]);

        MarkDirty();
    }

    /// <summary>扇区之间互换绑定（扇区拖到另一个扇区）</summary>
    public void SwapSectors(int fromSectorNum, int toSectorNum)
    {
        if (_selectedScene == null) return;
        var bindings = _selectedScene.Scene.Bindings;
        string keyA = $"sector_{fromSectorNum}";
        string keyB = $"sector_{toSectorNum}";

        bindings.TryGetValue(keyA, out var bindA);
        bindings.TryGetValue(keyB, out var bindB);

        if (bindA != null)
        {
            bindA.SectorNumber = toSectorNum;
            bindA.Ring = GetRingBySectorNumber(toSectorNum);
            bindA.Direction = GetDirectionBySectorNumber(toSectorNum);
            bindings[keyB] = bindA;
        }
        else bindings.Remove(keyB);

        if (bindB != null)
        {
            bindB.SectorNumber = fromSectorNum;
            bindB.Ring = GetRingBySectorNumber(fromSectorNum);
            bindB.Direction = GetDirectionBySectorNumber(fromSectorNum);
            bindings[keyA] = bindB;
        }
        else bindings.Remove(keyA);

        // 刷新两个扇区 VM
        if (AllSectors.TryGetValue(fromSectorNum, out var vmA))
            vmA.ApplyBinding(bindings.GetValueOrDefault(keyA));
        if (AllSectors.TryGetValue(toSectorNum, out var vmB))
            vmB.ApplyBinding(bindings.GetValueOrDefault(keyB));

        MarkDirty();
    }

    /// <summary>右键扇区 → 解除绑定</summary>
    public void UnbindSector(int sectorNumber)
    {
        if (_selectedScene == null) return;
        string key = $"sector_{sectorNumber}";
        _selectedScene.Scene.Bindings.Remove(key);
        if (AllSectors.TryGetValue(sectorNumber, out var vm))
            vm.ApplyBinding(null);
        MarkDirty();
    }

    // ===== 轮盘操作 =====
    [RelayCommand]
    private void CopyWheelBindings()
    {
        if (_selectedScene == null) return;
        var json = System.Text.Json.JsonSerializer.Serialize(
            _selectedScene.Scene.Bindings);
        System.Windows.Clipboard.SetText(json);
    }

    [RelayCommand]
    private void ClearWheelBindings()
    {
        if (_selectedScene == null) return;
        if (System.Windows.MessageBox.Show("确定清空所有扇区绑定？",
            "确认清空", System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning)
            != System.Windows.MessageBoxResult.Yes) return;

        _selectedScene.Scene.Bindings.Clear();
        RefreshWheelSectors();
        MarkDirty();
    }

    [RelayCommand]
    private void OpenWheelSettings() =>
        NavigateToWheelSettings?.Invoke();

    public event Action? NavigateToWheelSettings;

    // ===== 内部工具 =====
    private void RefreshWheelSectors()
    {
        if (_selectedScene == null)
        {
            foreach (var vm in AllSectors.Values) vm.ApplyBinding(null);
            return;
        }
        foreach (var (num, vm) in AllSectors)
        {
            string key = $"sector_{num}";
            vm.ApplyBinding(
                _selectedScene.Scene.Bindings.GetValueOrDefault(key));
        }
    }

    /// <summary>某格子内容变更后，同步所有绑定了该格子的扇区显示</summary>
    private void SyncBoundSectors(ActionCellViewModel cell)
    {
        if (_selectedScene == null) return;
        string? pageId = GetPageIdByCell(cell);
        if (pageId == null) return;

        foreach (var (key, binding) in _selectedScene.Scene.Bindings)
        {
            if (binding.SourcePageId != pageId ||
                binding.SourceCellIndex != cell.Model.CellIndex) continue;

            binding.DisplayName = cell.Model.Name;
            binding.IconPath = cell.Model.IconPath;

            string numStr = key.Replace("sector_", "");
            if (int.TryParse(numStr, out int num) &&
                AllSectors.TryGetValue(num, out var vm))
                vm.ApplyBinding(binding);
        }
    }

    private string? GetPageIdByCell(ActionCellViewModel cell)
    {
        if (_selectedScene == null) return null;
        return _selectedScene.Scene.ActionPages
            .FirstOrDefault(p => p.Cells.Contains(cell.Model))?.Id;
    }

    private string GetRingBySectorNumber(int n)
    {
        if (n <= 8) return "ring1";
        int ring2Count = _config.Settings.OuterRing16Mode ? 16 : 8;
        return n <= 8 + ring2Count ? "ring2" : "ring3";
    }

    private string GetDirectionBySectorNumber(int n)
    {
        var dirs8 = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        var dirs16 = new[]
        {
            "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
            "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"
        };
        if (n <= 8) return dirs8[n - 1];
        int ring2Count = _config.Settings.OuterRing16Mode ? 16 : 8;
        if (n <= 8 + ring2Count)
        {
            var dirs = _config.Settings.OuterRing16Mode ? dirs16 : dirs8;
            return dirs[n - 9];
        }
        return dirs8[n - (8 + ring2Count) - 1];
    }

    private void MarkDirty() => OnPropertyChanged(string.Empty);

    private static System.Windows.Window? GetOwner() =>
        System.Windows.Application.Current.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive);
}
