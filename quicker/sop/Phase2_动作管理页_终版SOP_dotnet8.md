# 设置界面 — 动作管理页  终版 SOP（.NET 8.0 / WPF）

> 以本文档为最终版本，之前所有动作管理页 SOP 全部作废。
> 核心变化：
> ① 取消"内圈/外圈/扩展圈"Tab 选项
> ② 左侧标题从"内圈·北(N)"改为"动作页面"
> ③ 动作页为 4×4 网格（16格/页），右键格子选动作类型，二次弹窗配置
> ④ 轮盘圆盘联动外圈16格开关（实时切换8/16扇区）
> ⑤ 动作页格子可拖拽到轮盘扇区完成绑定

---

## 一、页面整体布局

```
┌──────────────────────────────────────────────────────────────────────────┐
│  场景与动作管理                                                    [─][□][×]│
├───────────────┬──────────────────────────────────────────────────────────┤
│  场景列表      │  右侧主区域                                               │
│  [170px固定]  │                                                          │
│               │  ┌─ 左半区：动作页面 ──────────────────────────────────┐ │
│  全局 global  │  │  标题："动作页面"                                    │ │
│  通用 common  │  │  [+ 添加动作页] 按钮                                 │ │
│  任务栏       │  │                                                      │ │
│  桌面         │  │  ┌─ 动作页 Tab（横向，可拖拽排序）──────────────┐   │ │
│  AutoCAD App  │  │  │ [页面1▼] [页面2▼] [页面3▼] [+]              │   │ │
│               │  │  └──────────────────────────────────────────────┘   │ │
│               │  │                                                      │ │
│  [+][✎][✕][↑]│  │  ┌─ 4×4 动作网格（当前动作页）──────────────────┐  │ │
│               │  │  │  ┌───┬───┬───┬───┐                            │  │ │
│               │  │  │  │ 1 │ 2 │ 3 │ 4 │  ← 每格：图标+名称         │  │ │
│               │  │  │  ├───┼───┼───┼───┤     右键：选动作类型        │  │ │
│               │  │  │  │ 5 │ 6 │ 7 │ 8 │     拖出：绑定到扇区        │  │ │
│               │  │  │  ├───┼───┼───┼───┤                            │  │ │
│               │  │  │  │ 9 │10 │11 │12 │                            │  │ │
│               │  │  │  ├───┼───┼───┼───┤                            │  │ │
│               │  │  │  │13 │14 │15 │16 │                            │  │ │
│               │  │  │  └───┴───┴───┴───┘                            │  │ │
│               │  │  └────────────────────────────────────────────────┘  │ │
│               │  └──────────────────────────────────────────────────────┘ │
│               │                                                          │
│               │  ┌─ 右半区：轮盘圆盘 ─────────────────────────────────┐ │
│               │  │  □ 在此软件下禁用轮盘菜单                           │ │
│               │  │                                                     │ │
│               │  │  [轮盘圆盘预览，支持拖拽目标]                        │ │
│               │  │  每个扇区显示：序号 + 已绑定动作名                   │ │
│               │  │  扇区可选中（点击高亮），接受拖拽                    │ │
│               │  │                                                     │ │
│               │  │  [复制轮盘设置] [清空轮盘] [轮盘设置]               │ │
│               │  └─────────────────────────────────────────────────────┘ │
└───────────────┴──────────────────────────────────────────────────────────┘
```

---

## 二、数据模型

### ActionCell.cs（4×4网格中的单格）

```csharp
// Settings/Models/ActionCell.cs
namespace WheelMenu.Settings.Models;

public class ActionCell
{
    /// <summary>格子在4×4网格中的位置（0-based，0~15）</summary>
    public int CellIndex { get; set; }

    public ActionType Type     { get; set; } = ActionType.None;
    public string     Name     { get; set; } = string.Empty;
    public string     Value    { get; set; } = string.Empty;
    public string?    IconPath { get; set; }

    // RunAction 专用
    public string? ActionRefId { get; set; }
    public string? ActionParam { get; set; }

    public bool IsEmpty => Type == ActionType.None;
}
```

### ActionPage.cs

```csharp
// Settings/Models/ActionPage.cs
namespace WheelMenu.Settings.Models;

public class ActionPage
{
    public string Id   { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "新动作页";

    /// <summary>固定16格（4×4），索引0~15对应位置从左到右、从上到下</summary>
    public ActionCell[] Cells { get; set; } =
        Enumerable.Range(0, 16)
                  .Select(i => new ActionCell { CellIndex = i })
                  .ToArray();
}
```

### WheelSectorBinding.cs（扇区绑定）

```csharp
// Settings/Models/WheelSectorBinding.cs
namespace WheelMenu.Settings.Models;

/// <summary>
/// 轮盘某个扇区的绑定信息。
/// Key格式："{ring}_{sectorNumber}"，如 "ring1_1"、"ring2_9"
/// </summary>
public class WheelSectorBinding
{
    public int        SectorNumber { get; set; }   // 1-based 全局序号
    public string     Ring         { get; set; } = string.Empty;
    public string     Direction    { get; set; } = string.Empty;

    /// <summary>绑定来源：某动作页的某格</summary>
    public string? SourcePageId   { get; set; }
    public int?    SourceCellIndex { get; set; }   // 0~15

    /// <summary>绑定后缓存的显示信息（名称/图标），避免每次查找</summary>
    public string  DisplayName { get; set; } = string.Empty;
    public string? IconPath    { get; set; }

    public bool IsEmpty => string.IsNullOrEmpty(SourcePageId);
}
```

### SceneConfig.cs（精简版）

```csharp
// Settings/Models/SceneConfig.cs
namespace WheelMenu.Settings.Models;

public class SceneConfig
{
    public string  Name       { get; set; } = string.Empty;
    public string? Process    { get; set; }
    public bool    DisableWheel { get; set; } = false;

    /// <summary>该场景下所有动作页</summary>
    public List<ActionPage> ActionPages { get; set; } = new();

    /// <summary>
    /// 扇区绑定表。Key = "ring1_1"..."ring3_24"
    /// </summary>
    public Dictionary<string, WheelSectorBinding> Bindings { get; set; } = new();
}
```

---

## 三、ViewModel 层

### ActionCellViewModel.cs

```csharp
// Settings/ViewModels/ActionCellViewModel.cs
namespace WheelMenu.Settings.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using WheelMenu.Settings.Models;

public partial class ActionCellViewModel : ObservableObject
{
    public ActionCell Model { get; }

    [ObservableProperty] private string  _displayName = string.Empty;
    [ObservableProperty] private string? _iconPath;
    [ObservableProperty] private bool    _isEmpty = true;

    // 拖拽状态
    [ObservableProperty] private bool _isDragging = false;

    public ActionCellViewModel(ActionCell model)
    {
        Model = model;
        Refresh();
    }

    public void Refresh()
    {
        IsEmpty     = Model.IsEmpty;
        DisplayName = Model.IsEmpty ? string.Empty : Model.Name;
        IconPath    = Model.IconPath;
    }
}
```

### ActionPageViewModel.cs

```csharp
// Settings/ViewModels/ActionPageViewModel.cs
namespace WheelMenu.Settings.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WheelMenu.Settings.Models;

public partial class ActionPageViewModel : ObservableObject
{
    public ActionPage Model { get; }

    [ObservableProperty] private string _name;

    public ObservableCollection<ActionCellViewModel> Cells { get; } = new();

    public ActionPageViewModel(ActionPage model)
    {
        Model = model;
        _name = model.Name;

        foreach (var cell in model.Cells)
            Cells.Add(new ActionCellViewModel(cell));
    }

    partial void OnNameChanged(string value) => Model.Name = value;
}
```

### WheelSectorViewModel.cs（轮盘扇区）

```csharp
// Settings/ViewModels/WheelSectorViewModel.cs
namespace WheelMenu.Settings.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using WheelMenu.Settings.Models;

public partial class WheelSectorViewModel : ObservableObject
{
    public int    SectorNumber { get; }   // 1-based
    public string Ring        { get; }
    public string Direction   { get; }

    [ObservableProperty] private bool    _isSelected   = false;
    [ObservableProperty] private bool    _isDragTarget = false;
    [ObservableProperty] private bool    _isBound      = false;
    [ObservableProperty] private string  _boundName    = string.Empty;
    [ObservableProperty] private string? _boundIconPath;

    public WheelSectorViewModel(int sectorNumber, string ring, string direction)
    {
        SectorNumber = sectorNumber;
        Ring         = ring;
        Direction    = direction;
    }

    public void ApplyBinding(WheelSectorBinding? binding)
    {
        if (binding == null || binding.IsEmpty)
        {
            IsBound       = false;
            BoundName     = string.Empty;
            BoundIconPath = null;
        }
        else
        {
            IsBound       = true;
            BoundName     = binding.DisplayName;
            BoundIconPath = binding.IconPath;
        }
    }
}
```

### ActionManagerPageViewModel.cs（终版核心）

```csharp
// Settings/ViewModels/ActionManagerPageViewModel.cs
namespace WheelMenu.Settings.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheelMenu.Settings.Models;

public partial class ActionManagerPageViewModel : ObservableObject
{
    private readonly WheelConfig _config;

    // ══ 场景列表 ══
    public ObservableCollection<SceneItemViewModel> Scenes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionPages))]
    [NotifyPropertyChangedFor(nameof(DisableWheelForScene))]
    private SceneItemViewModel? _selectedScene;

    partial void OnSelectedSceneChanged(SceneItemViewModel? value)
    {
        RefreshActionPages();
        RefreshWheelSectors();
    }

    // ══ 动作页 ══
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
        SelectedPage?.Cells ?? new();

    // ══ 当前选中格子（右键/编辑用）══
    [ObservableProperty] private ActionCellViewModel? _selectedCell;

    // ══ 轮盘扇区 ══
    // 所有扇区的 VM（包含所有圈层），Key = SectorNumber
    public Dictionary<int, WheelSectorViewModel> AllSectors { get; } = new();
    public ObservableCollection<WheelSectorViewModel> Ring1Sectors { get; } = new();
    public ObservableCollection<WheelSectorViewModel> Ring2Sectors { get; } = new();
    public ObservableCollection<WheelSectorViewModel> Ring3Sectors { get; } = new();

    // 当前选中扇区（高亮）
    [ObservableProperty] private WheelSectorViewModel? _selectedSector;

    // ══ 外圈16格开关（从全局设置读取）══
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

    // ══ 禁用开关 ══
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

    // ══ 构造 ══
    public ActionManagerPageViewModel(WheelConfig config)
    {
        _config = config;
        InitWheelSectors();
        RefreshScenes();
    }

    // ══════════════════════════════════════
    // 初始化轮盘扇区结构
    // ══════════════════════════════════════
    private void InitWheelSectors()
    {
        AllSectors.Clear();
        Ring1Sectors.Clear();
        Ring2Sectors.Clear();
        Ring3Sectors.Clear();

        var dirs8 = new[] { "N","NE","E","SE","S","SW","W","NW" };

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
        int  count = is16 ? 16 : 8;
        var  dirs8  = new[] { "N","NE","E","SE","S","SW","W","NW" };
        var  dirs16 = new[]
        {
            "N","NNE","NE","ENE","E","ESE","SE","SSE",
            "S","SSW","SW","WSW","W","WNW","NW","NNW"
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
        var dirs8 = new[] { "N","NE","E","SE","S","SW","W","NW" };

        for (int i = 0; i < 8; i++)
        {
            int sectorNum = ring3Start + i;
            var vm = new WheelSectorViewModel(sectorNum, "ring3", dirs8[i]);
            Ring3Sectors.Add(vm);
            AllSectors[sectorNum] = vm;
        }
    }

    // ══════════════════════════════════════
    // 场景管理
    // ══════════════════════════════════════
    private void RefreshScenes()
    {
        Scenes.Clear();
        var priority = new[] { "global", "common", "taskbar", "desktop" };
        foreach (var key in priority)
            if (_config.Scenes.TryGetValue(key, out var sc))
                Scenes.Add(new SceneItemViewModel(key, sc));

        foreach (var (k, v) in _config.Scenes)
            if (!priority.Contains(k))
                Scenes.Add(new SceneItemViewModel(k, v));

        SelectedScene = Scenes.FirstOrDefault();
    }

    [RelayCommand]
    private void AddScene()
    {
        var dlg = new Views.AddSceneDialog { Owner = GetOwner() };
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
        if (item == null || IsSystemScene(item.Key)) return;
        if (System.Windows.MessageBox.Show($"确定删除场景"{item.Scene.Name}"？",
            "确认", System.Windows.MessageBoxButton.YesNo)
            != System.Windows.MessageBoxResult.Yes) return;
        _config.Scenes.Remove(item.Key);
        RefreshScenes();
        MarkDirty();
    }

    [RelayCommand]
    private void EditScene()
    {
        if (SelectedScene == null || IsSystemScene(SelectedScene.Key)) return;
        var dlg = new Views.InputDialog("重命名场景", "名称：",
            SelectedScene.Scene.Name) { Owner = GetOwner() };
        if (dlg.ShowDialog() != true) return;
        SelectedScene.Scene.Name = dlg.Result!;
        RefreshScenes();
        MarkDirty();
    }

    private static bool IsSystemScene(string key) =>
        key is "global" or "common" or "taskbar" or "desktop";

    // ══════════════════════════════════════
    // 动作页管理
    // ══════════════════════════════════════
    private void RefreshActionPages()
    {
        ActionPages.Clear();
        if (_selectedScene == null) return;
        foreach (var page in _selectedScene.Scene.ActionPages)
            ActionPages.Add(new ActionPageViewModel(page));
        SelectedPage = ActionPages.FirstOrDefault();
    }

    [RelayCommand]
    private void AddActionPage()
    {
        if (_selectedScene == null) return;
        var dlg = new Views.InputDialog("添加动作页", "动作页名称：", "新动作页")
            { Owner = GetOwner() };
        if (dlg.ShowDialog() != true ||
            string.IsNullOrWhiteSpace(dlg.Result)) return;

        var page = new ActionPage { Name = dlg.Result };
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
            $"确定删除动作页"{page.Name}"？关联的扇区绑定也将解除。",
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
        var page  = pages[fromIndex];
        pages.RemoveAt(fromIndex);
        pages.Insert(toIndex, page);
        var vm = ActionPages[fromIndex];
        ActionPages.RemoveAt(fromIndex);
        ActionPages.Insert(toIndex, vm);
        MarkDirty();
    }

    // ══════════════════════════════════════
    // 格子编辑（右键流程）
    // ══════════════════════════════════════

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
        System.Windows.Window? dlg = type switch
        {
            ActionType.Hotkey        => new Views.Dialogs.HotkeyDialog(cell.Model),
            ActionType.SimulateInput => new Views.Dialogs.SimulateInputDialog(cell.Model),
            ActionType.Paste         => new Views.Dialogs.PasteDialog(cell.Model),
            ActionType.Open          => new Views.Dialogs.OpenDialog(cell.Model),
            ActionType.RunAction     => new Views.Dialogs.RunActionDialog(cell.Model),
            ActionType.SendText      => new Views.Dialogs.SendTextDialog(cell.Model),
            ActionType.DateTime      => new Views.Dialogs.DateTimeDialog(cell.Model),
            ActionType.None          => null,
            _                        => null
        };

        if (dlg == null)
        {
            // None 类型直接清空
            cell.Model.Type  = ActionType.None;
            cell.Model.Name  = string.Empty;
            cell.Model.Value = string.Empty;
            cell.Refresh();
            return true;
        }

        dlg.Owner = GetOwner();
        return dlg.ShowDialog() == true;
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
        cell.Model.Type      = ActionType.None;
        cell.Model.Name      = string.Empty;
        cell.Model.Value     = string.Empty;
        cell.Model.IconPath  = null;
        cell.Refresh();
        SyncBoundSectors(cell);
        MarkDirty();
    }

    // ══════════════════════════════════════
    // 拖拽：格子 → 扇区绑定
    // ══════════════════════════════════════

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
            SectorNumber   = sectorNumber,
            Ring           = GetRingBySectorNumber(sectorNumber),
            Direction      = GetDirectionBySectorNumber(sectorNumber),
            SourcePageId   = GetPageIdByCell(cell),
            SourceCellIndex = cell.Model.CellIndex,
            DisplayName    = cell.Model.Name,
            IconPath       = cell.Model.IconPath
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
        string keyA  = $"sector_{fromSectorNum}";
        string keyB  = $"sector_{toSectorNum}";

        bindings.TryGetValue(keyA, out var bindA);
        bindings.TryGetValue(keyB, out var bindB);

        if (bindA != null)
        {
            bindA.SectorNumber = toSectorNum;
            bindA.Ring         = GetRingBySectorNumber(toSectorNum);
            bindA.Direction    = GetDirectionBySectorNumber(toSectorNum);
            bindings[keyB]     = bindA;
        }
        else bindings.Remove(keyB);

        if (bindB != null)
        {
            bindB.SectorNumber = fromSectorNum;
            bindB.Ring         = GetRingBySectorNumber(fromSectorNum);
            bindB.Direction    = GetDirectionBySectorNumber(fromSectorNum);
            bindings[keyA]     = bindB;
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

    // ══════════════════════════════════════
    // 轮盘操作
    // ══════════════════════════════════════
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

    // ══════════════════════════════════════
    // 内部工具
    // ══════════════════════════════════════
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
            binding.IconPath    = cell.Model.IconPath;

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
        var dirs8 = new[] { "N","NE","E","SE","S","SW","W","NW" };
        var dirs16 = new[]
        {
            "N","NNE","NE","ENE","E","ESE","SE","SSE",
            "S","SSW","SW","WSW","W","WNW","NW","NNW"
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
```

---

## 四、ActionManagerPage.xaml（完整布局）

```xml
<!-- Settings/Views/Pages/ActionManagerPage.xaml -->
<Page x:Class="WheelMenu.Settings.Views.Pages.ActionManagerPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:vm="clr-namespace:WheelMenu.Settings.ViewModels"
      xmlns:ctrl="clr-namespace:WheelMenu.Settings.Controls"
      xmlns:conv="clr-namespace:WheelMenu.Settings.Converters">

  <Page.Resources>
    <conv:BoolToVisibilityConverter    x:Key="BoolToVis"/>
    <conv:InverseBoolToVisConverter    x:Key="InvBoolToVis"/>
    <conv:NullToVisibilityConverter    x:Key="NullToVis"/>
  </Page.Resources>

  <!-- 最外层：两列（场景列表 + 主区域）-->
  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="170"/>
      <ColumnDefinition Width="1"/>
      <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- ══════════ 场景列表（左列）══════════ -->
    <Grid Grid.Column="0" Background="#F5F5F5">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
      </Grid.RowDefinitions>

      <TextBlock Grid.Row="0" Text="场景"
                 FontSize="11" Foreground="#757575"
                 Margin="8,6,0,2"/>

      <ListBox Grid.Row="1"
               x:Name="SceneList"
               ItemsSource="{Binding Scenes}"
               SelectedItem="{Binding SelectedScene}"
               BorderThickness="0" Background="Transparent"
               ScrollViewer.HorizontalScrollBarVisibility="Disabled">
        <ListBox.ItemTemplate>
          <DataTemplate DataType="{x:Type vm:SceneItemViewModel}">
            <StackPanel Height="44" Margin="4,2" Orientation="Vertical"
                        VerticalAlignment="Center">
              <TextBlock Text="{Binding DisplayName}"
                         FontSize="13" FontWeight="SemiBold"
                         TextTrimming="CharacterEllipsis"/>
              <TextBlock Text="{Binding ProcessKey}"
                         FontSize="10" Foreground="#9E9E9E"
                         TextTrimming="CharacterEllipsis"/>
            </StackPanel>
          </DataTemplate>
        </ListBox.ItemTemplate>
        <ListBox.ItemContainerStyle>
          <Style TargetType="ListBoxItem">
            <Setter Property="Padding" Value="4,0"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
          </Style>
        </ListBox.ItemContainerStyle>
      </ListBox>

      <!-- 底部工具栏 -->
      <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="4">
        <Button Width="26" Height="26" Content="+"
                Command="{Binding AddSceneCommand}" ToolTip="添加场景"/>
        <Button Width="26" Height="26" Content="✎" Margin="2,0"
                Command="{Binding EditSceneCommand}" ToolTip="重命名"/>
        <Button Width="26" Height="26" Content="✕" Foreground="#F44336"
                Command="{Binding DeleteSceneCommand}"
                CommandParameter="{Binding SelectedScene}"
                ToolTip="删除场景"/>
      </StackPanel>
    </Grid>

    <Rectangle Grid.Column="1" Fill="#E0E0E0"/>

    <!-- ══════════ 主区域（右列）══════════ -->
    <Grid Grid.Column="2">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>    <!-- 左半：动作页 -->
        <ColumnDefinition Width="1"/>
        <ColumnDefinition Width="360"/>  <!-- 右半：轮盘圆盘 -->
      </Grid.ColumnDefinitions>

      <!-- ──────── 左半：动作页面区 ──────── -->
      <Grid Grid.Column="0">
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto"/>  <!-- 标题 + 添加按钮 -->
          <RowDefinition Height="Auto"/>  <!-- 动作页 Tab -->
          <RowDefinition Height="*"/>     <!-- 4×4 网格 -->
        </Grid.RowDefinitions>

        <!-- 标题行 -->
        <StackPanel Grid.Row="0" Orientation="Horizontal"
                    Margin="12,10,12,6" VerticalAlignment="Center">
          <TextBlock Text="动作页面"
                     FontSize="15" FontWeight="SemiBold"
                     VerticalAlignment="Center"/>
          <Button Content="＋ 添加动作页"
                  Command="{Binding AddActionPageCommand}"
                  Margin="12,0,0,0" Padding="10,3"
                  Height="28" VerticalAlignment="Center"/>
        </StackPanel>

        <!-- 动作页 Tab（横向滚动，每个Tab可右键删除/改名）-->
        <ScrollViewer Grid.Row="1"
                      HorizontalScrollBarVisibility="Auto"
                      VerticalScrollBarVisibility="Hidden"
                      Margin="8,0,8,4">
          <ctrl:ActionPageTabBar
              x:Name="PageTabBar"
              ItemsSource="{Binding ActionPages}"
              SelectedItem="{Binding SelectedPage, Mode=TwoWay}"
              TabReordered="OnTabReordered"
              TabDeleteRequested="OnTabDeleteRequested"/>
        </ScrollViewer>

        <!-- 4×4 动作网格 -->
        <ctrl:ActionCellGrid
            Grid.Row="2"
            x:Name="CellGrid"
            Margin="12,4,12,12"
            ItemsSource="{Binding CurrentPageCells}"
            CellRightClicked="OnCellRightClicked"
            CellDoubleClicked="OnCellDoubleClicked"
            DragStarted="OnCellDragStarted"/>
      </Grid>

      <Rectangle Grid.Column="1" Fill="#E0E0E0"/>

      <!-- ──────── 右半：轮盘圆盘 ──────── -->
      <Grid Grid.Column="2" Margin="12">
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto"/>  <!-- 禁用开关 -->
          <RowDefinition Height="*"/>     <!-- 圆盘 -->
          <RowDefinition Height="Auto"/>  <!-- 操作按钮 -->
        </Grid.RowDefinitions>

        <!-- 禁用开关 -->
        <CheckBox Grid.Row="0"
                  Content="在此软件下禁用轮盘菜单"
                  IsChecked="{Binding DisableWheelForScene}"
                  Margin="0,0,0,8"/>

        <!-- 圆盘预览（拖拽目标）-->
        <ctrl:WheelSectorCanvas
            Grid.Row="1"
            x:Name="WheelCanvas"
            Ring1Sectors="{Binding Ring1Sectors}"
            Ring2Sectors="{Binding Ring2Sectors}"
            Ring3Sectors="{Binding Ring3Sectors}"
            SelectedSector="{Binding SelectedSector, Mode=TwoWay}"
            Outer16Mode="{Binding Outer16Mode}"
            AllowDrop="True"
            SectorDrop="OnSectorDrop"
            SectorSwap="OnSectorSwap"
            SectorRightClicked="OnSectorRightClicked"
            HorizontalAlignment="Center"
            VerticalAlignment="Center"
            Width="320" Height="320"/>

        <!-- 操作按钮 -->
        <StackPanel Grid.Row="2" Margin="0,8,0,0">
          <Button Content="复制当前轮盘设置"
                  Command="{Binding CopyWheelBindingsCommand}"
                  Height="30" Margin="0,0,0,4"/>
          <Button Content="清空轮盘"
                  Command="{Binding ClearWheelBindingsCommand}"
                  Height="30" Foreground="#F44336" Margin="0,0,0,4"/>
          <Button Content="轮盘设置"
                  Command="{Binding OpenWheelSettingsCommand}"
                  Height="30"/>
        </StackPanel>
      </Grid>
    </Grid>
  </Grid>
</Page>
```

---

## 五、ActionManagerPage.xaml.cs（Code-Behind）

```csharp
namespace WheelMenu.Settings.Views.Pages;

using System.Windows;
using System.Windows.Controls;
using WheelMenu.Settings.Models;
using WheelMenu.Settings.ViewModels;

public partial class ActionManagerPage : Page
{
    private ActionManagerPageViewModel Vm =>
        (ActionManagerPageViewModel)DataContext;

    // 拖拽中的格子
    private ActionCellViewModel? _draggingCell;

    public ActionManagerPage() => InitializeComponent();

    // ══ 动作页 Tab 事件 ══

    private void OnTabReordered(int from, int to) =>
        Vm.MoveActionPage(from, to);

    private void OnTabDeleteRequested(ActionPageViewModel page) =>
        Vm.DeleteActionPageCommand.Execute(page);

    // ══ 格子右键菜单 ══

    private void OnCellRightClicked(object sender, ActionCellViewModel cell)
    {
        var menu = new ContextMenu { Tag = cell };

        // 动作类型选项
        var typeItem = new MenuItem { Header = "设置动作类型" };
        foreach (ActionType type in Enum.GetValues<ActionType>())
        {
            var sub = new MenuItem
            {
                Header    = GetActionTypeLabel(type),
                IsChecked = cell.Model.Type == type
            };
            var capturedType = type;
            sub.Click += (_, _) => Vm.OnCellTypeSelected(cell, capturedType);
            typeItem.Items.Add(sub);
        }
        menu.Items.Add(typeItem);

        if (!cell.IsEmpty)
        {
            menu.Items.Add(new Separator());
            var editItem = new MenuItem { Header = "编辑动作..." };
            editItem.Click += (_, _) => Vm.OnCellDoubleClicked(cell);
            menu.Items.Add(editItem);

            var clearItem = new MenuItem
                { Header = "清除动作", Foreground = System.Windows.Media.Brushes.Red };
            clearItem.Click += (_, _) => Vm.OnClearCell(cell);
            menu.Items.Add(clearItem);
        }

        menu.IsOpen = true;
    }

    private void OnCellDoubleClicked(object sender, ActionCellViewModel cell) =>
        Vm.OnCellDoubleClicked(cell);

    // ══ 拖拽：格子 → 扇区 ══

    private void OnCellDragStarted(object sender, ActionCellViewModel cell)
    {
        if (cell.IsEmpty) return;
        _draggingCell = cell;
        // DragDrop 在 ActionCellGrid 内部启动，此处记录来源
    }

    // ══ 扇区事件 ══

    private void OnSectorDrop(object sender, int sectorNumber)
    {
        if (_draggingCell == null) return;
        Vm.BindCellToSector(_draggingCell, sectorNumber);
        _draggingCell = null;
    }

    private void OnSectorSwap(object sender, (int From, int To) e) =>
        Vm.SwapSectors(e.From, e.To);

    private void OnSectorRightClicked(object sender,
        WheelSectorViewModel sector)
    {
        var menu = new ContextMenu();

        if (sector.IsBound)
        {
            var unbind = new MenuItem { Header = $"解除绑定（{sector.BoundName}）" };
            unbind.Click += (_, _) => Vm.UnbindSector(sector.SectorNumber);
            menu.Items.Add(unbind);
        }
        else
        {
            menu.Items.Add(new MenuItem
                { Header = $"扇区 {sector.SectorNumber}（{sector.Direction}）空",
                  IsEnabled = false });
        }

        menu.IsOpen = true;
    }

    // ══ 工具 ══
    private static string GetActionTypeLabel(ActionType t) => t switch
    {
        ActionType.None          => "（清空）",
        ActionType.Hotkey        => "发送快捷键",
        ActionType.SimulateInput => "模拟输入序列",
        ActionType.Paste         => "粘贴内容",
        ActionType.Open          => "打开文件/网址",
        ActionType.RunAction     => "运行动作",
        ActionType.SendText      => "发送文本",
        ActionType.DateTime      => "插入日期时间",
        _                        => t.ToString()
    };
}
```

---

## 六、ActionCellGrid 控件（4×4网格）

```csharp
// Settings/Controls/ActionCellGrid.cs
namespace WheelMenu.Settings.Controls;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WheelMenu.Settings.ViewModels;

/// <summary>
/// 4×4 动作格子网格控件。
/// 每格：图标（若有）+ 动作名称，空格显示灰色"+"。
/// 支持：右键菜单、双击编辑、拖拽到圆盘扇区。
/// </summary>
public class ActionCellGrid : Control
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource),
            typeof(ObservableCollection<ActionCellViewModel>),
            typeof(ActionCellGrid),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (d, _) => ((ActionCellGrid)d).RebuildCells()));

    public ObservableCollection<ActionCellViewModel>? ItemsSource
    {
        get => (ObservableCollection<ActionCellViewModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public event EventHandler<ActionCellViewModel>? CellRightClicked;
    public event EventHandler<ActionCellViewModel>? CellDoubleClicked;
    public event EventHandler<ActionCellViewModel>? DragStarted;

    private readonly UniformGrid _grid;
    private Point _dragStart;

    public ActionCellGrid()
    {
        _grid = new UniformGrid { Rows = 4, Columns = 4 };
        AddVisualChild(_grid);
        AddLogicalChild(_grid);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _grid;

    protected override Size MeasureOverride(Size availableSize)
    {
        _grid.Measure(availableSize);
        return _grid.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _grid.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private void RebuildCells()
    {
        _grid.Children.Clear();
        if (ItemsSource == null) return;

        foreach (var vm in ItemsSource)
        {
            var cell = BuildCellButton(vm);
            _grid.Children.Add(cell);
        }
    }

    private Button BuildCellButton(ActionCellViewModel vm)
    {
        // 格子外观
        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };

        if (!vm.IsEmpty && vm.IconPath != null)
        {
            content.Children.Add(new Image
            {
                Width  = 20, Height = 20,
                Source = LoadImage(vm.IconPath),
                Margin = new Thickness(0, 0, 0, 2)
            });
        }

        var label = new TextBlock
        {
            Text            = vm.IsEmpty ? "+" : vm.DisplayName,
            Foreground      = vm.IsEmpty
                ? System.Windows.Media.Brushes.LightGray
                : System.Windows.Media.Brushes.Black,
            FontSize        = 10,
            TextAlignment   = TextAlignment.Center,
            TextWrapping    = TextWrapping.Wrap,
            MaxWidth        = 72,
            TextTrimming    = TextTrimming.CharacterEllipsis
        };
        content.Children.Add(label);

        var btn = new Button
        {
            Content     = content,
            Width       = 80, Height = 80,
            Margin      = new Thickness(2),
            Background  = vm.IsEmpty
                ? new SolidColorBrush(Color.FromRgb(250, 250, 250))
                : new SolidColorBrush(Color.FromRgb(232, 240, 254)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            BorderThickness = new Thickness(1),
            Tag         = vm
        };

        // 右键
        btn.MouseRightButtonDown += (s, e) =>
        {
            e.Handled = true;
            CellRightClicked?.Invoke(this, vm);
        };

        // 双击
        btn.MouseDoubleClick += (s, e) =>
        {
            e.Handled = true;
            CellDoubleClicked?.Invoke(this, vm);
        };

        // 拖拽源
        btn.PreviewMouseLeftButtonDown += (s, e) =>
            _dragStart = e.GetPosition(this);

        btn.PreviewMouseMove += (s, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (vm.IsEmpty) return;
            var pos  = e.GetPosition(this);
            var diff = pos - _dragStart;
            if (Math.Abs(diff.X) < 8 && Math.Abs(diff.Y) < 8) return;

            DragStarted?.Invoke(this, vm);
            var data = new DataObject("ActionCellViewModel", vm);
            DragDrop.DoDragDrop(btn, data, DragDropEffects.Move | DragDropEffects.Copy);
        };

        return btn;
    }

    private static System.Windows.Media.ImageSource? LoadImage(string path)
    {
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage(new Uri(path));
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
```

---

## 七、WheelSectorCanvas 控件（圆盘+扇区序号+拖拽目标）

```csharp
// Settings/Controls/WheelSectorCanvas.cs
namespace WheelMenu.Settings.Controls;

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WheelMenu.Renderer;
using WheelMenu.Settings.ViewModels;

/// <summary>
/// 设置页面中的轮盘圆盘控件。
/// - 几何：单大圆背景 + 圆环扇区 + 透底放射分割线（R50/R110/R220/R400）
/// - 每个扇区显示：序号（水印）+ 已绑定动作名称
/// - 支持：点击选中、拖拽目标（接受 ActionCellViewModel）、扇区互拖
/// - Outer16Mode 变化时，外圈动态切换 8/16 个扇区
/// </summary>
public class WheelSectorCanvas : FrameworkElement
{
    // ══ 依赖属性 ══

    public static readonly DependencyProperty Ring1SectorsProperty =
        DependencyProperty.Register(nameof(Ring1Sectors),
            typeof(ObservableCollection<WheelSectorViewModel>),
            typeof(WheelSectorCanvas),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnSectorsChanged));

    public static readonly DependencyProperty Ring2SectorsProperty =
        DependencyProperty.Register(nameof(Ring2Sectors),
            typeof(ObservableCollection<WheelSectorViewModel>),
            typeof(WheelSectorCanvas),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnSectorsChanged));

    public static readonly DependencyProperty Ring3SectorsProperty =
        DependencyProperty.Register(nameof(Ring3Sectors),
            typeof(ObservableCollection<WheelSectorViewModel>),
            typeof(WheelSectorCanvas),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnSectorsChanged));

    public static readonly DependencyProperty Outer16ModeProperty =
        DependencyProperty.Register(nameof(Outer16Mode),
            typeof(bool), typeof(WheelSectorCanvas),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedSectorProperty =
        DependencyProperty.Register(nameof(SelectedSector),
            typeof(WheelSectorViewModel), typeof(WheelSectorCanvas),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public ObservableCollection<WheelSectorViewModel>? Ring1Sectors
    {
        get => (ObservableCollection<WheelSectorViewModel>?)GetValue(Ring1SectorsProperty);
        set => SetValue(Ring1SectorsProperty, value);
    }
    public ObservableCollection<WheelSectorViewModel>? Ring2Sectors
    {
        get => (ObservableCollection<WheelSectorViewModel>?)GetValue(Ring2SectorsProperty);
        set => SetValue(Ring2SectorsProperty, value);
    }
    public ObservableCollection<WheelSectorViewModel>? Ring3Sectors
    {
        get => (ObservableCollection<WheelSectorViewModel>?)GetValue(Ring3SectorsProperty);
        set => SetValue(Ring3SectorsProperty, value);
    }
    public bool Outer16Mode
    {
        get => (bool)GetValue(Outer16ModeProperty);
        set => SetValue(Outer16ModeProperty, value);
    }
    public WheelSectorViewModel? SelectedSector
    {
        get => (WheelSectorViewModel?)GetValue(SelectedSectorProperty);
        set => SetValue(SelectedSectorProperty, value);
    }

    // ══ 事件 ══
    public event EventHandler<int>?                        SectorDrop;
    public event EventHandler<(int From, int To)>?         SectorSwap;
    public event EventHandler<WheelSectorViewModel>?       SectorRightClicked;

    // ══ 内部状态 ══
    private WheelSectorViewModel? _hoverSector;
    private WheelSectorViewModel? _dragSourceSector;   // 扇区互拖来源

    private static void OnSectorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (WheelSectorCanvas)d;
        // 订阅集合变化通知
        if (e.NewValue is ObservableCollection<WheelSectorViewModel> col)
            col.CollectionChanged += (_, _) => canvas.InvalidateVisual();
        // 订阅每个VM的属性变化
        if (e.NewValue is ObservableCollection<WheelSectorViewModel> col2)
            foreach (var vm in col2)
                vm.PropertyChanged += (_, _) => canvas.InvalidateVisual();
    }

    // ══ 绘制 ══
    protected override void OnRender(DrawingContext dc)
    {
        double cx    = ActualWidth  / 2.0;
        double cy    = ActualHeight / 2.0;
        double scale = Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter;

        double rDead  = WheelConstants.R_DEAD  * scale;
        double rRing1 = WheelConstants.R_RING1 * scale;
        double rRing2 = WheelConstants.R_RING2 * scale;
        double rRing3 = WheelConstants.R_RING3 * scale;

        // 1. 大圆背景
        dc.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(30, 100, 149, 237)),
            new Pen(new SolidColorBrush(Color.FromArgb(80, 100, 149, 237)), 1.5 * scale),
            new Point(cx, cy), rRing3, rRing3);

        // 2. Ring3 扇区（从外到内画）
        DrawRingSectors(dc, cx, cy, rRing2, rRing3, Ring3Sectors, 8, scale);

        // 3. Ring2 扇区（8 或 16）
        int ring2Count = Outer16Mode ? 16 : 8;
        DrawRingSectors(dc, cx, cy, rRing1, rRing2, Ring2Sectors, ring2Count, scale);

        // 4. Ring1 扇区
        DrawRingSectors(dc, cx, cy, rDead, rRing1, Ring1Sectors, 8, scale);

        // 5. 放射分割线（透底，从0到rRing3）
        DrawDividers(dc, cx, cy, 0, rRing3, 8, scale);
        // 16格模式：外圈额外16条更细的线
        if (Outer16Mode)
            DrawDividers(dc, cx, cy, rRing1, rRing2, 16, scale, extraThin: true);

        // 6. 死区圆
        dc.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(200, 245, 245, 245)),
            new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), 1 * scale),
            new Point(cx, cy), rDead, rDead);

        // 7. 扇区序号 + 绑定名称
        DrawSectorContent(dc, cx, cy, rDead, rRing1, rRing2, rRing3, scale);
    }

    private void DrawRingSectors(
        DrawingContext dc,
        double cx, double cy,
        double rIn, double rOut,
        ObservableCollection<WheelSectorViewModel>? sectors,
        int count, double scale)
    {
        if (sectors == null) return;
        double step = 360.0 / count;

        for (int i = 0; i < count && i < sectors.Count; i++)
        {
            var    vm    = sectors[i];
            double start = i * step - step / 2.0;
            double end   = start + step;

            // 填充色
            Color fill;
            if (vm == SelectedSector)
                fill = Color.FromArgb(120, 25, 118, 210);
            else if (vm == _hoverSector || vm.IsDragTarget)
                fill = Color.FromArgb(80, 25, 118, 210);
            else if (vm.IsBound)
                fill = Color.FromArgb(40, 100, 200, 100);
            else
                fill = Color.FromArgb(15, 80, 80, 80);

            var geo = WheelGeometry.CreateSectorPath(cx, cy, rIn, rOut, start, end);
            // ✅ pen = null，不画扇区边框线，分割线统一后画
            dc.DrawGeometry(new SolidColorBrush(fill), null, geo);

            // 选中高亮边框（单独一条笔）
            if (vm == SelectedSector)
            {
                var selPen = new Pen(
                    new SolidColorBrush(Color.FromRgb(25, 118, 210)), 2 * scale);
                selPen.Freeze();
                dc.DrawGeometry(null, selPen, geo);
            }
        }
    }

    private static void DrawDividers(
        DrawingContext dc, double cx, double cy,
        double rFrom, double rTo, int count, double scale,
        bool extraThin = false)
    {
        double alpha = extraThin ? 30 : 60;
        double thick = extraThin ? 0.8 : 1.5;
        var pen = new Pen(
            new SolidColorBrush(Color.FromArgb((byte)alpha, 150, 150, 150)),
            thick * scale);
        pen.Freeze();
        double step = 360.0 / count;
        for (int i = 0; i < count; i++)
        {
            var (from, to) = WheelGeometry.GetDividerLine(
                cx, cy, rFrom, rTo, i * step);
            dc.DrawLine(pen, from, to);
        }
    }

    private void DrawSectorContent(
        DrawingContext dc,
        double cx, double cy,
        double rDead, double rRing1, double rRing2, double rRing3,
        double scale)
    {
        var typeface = new Typeface(WheelConstants.FontFamily);
        double dpi   = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Ring1
        DrawRingContent(dc, cx, cy, rDead,  rRing1, Ring1Sectors, 8,      scale, typeface, dpi);
        // Ring2
        int ring2Count = Outer16Mode ? 16 : 8;
        DrawRingContent(dc, cx, cy, rRing1, rRing2, Ring2Sectors, ring2Count, scale, typeface, dpi);
        // Ring3
        DrawRingContent(dc, cx, cy, rRing2, rRing3, Ring3Sectors, 8,      scale, typeface, dpi);
    }

    private static void DrawRingContent(
        DrawingContext dc,
        double cx, double cy,
        double rIn, double rOut,
        ObservableCollection<WheelSectorViewModel>? sectors,
        int count, double scale,
        Typeface typeface, double dpi)
    {
        if (sectors == null) return;
        double step = 360.0 / count;

        for (int i = 0; i < count && i < sectors.Count; i++)
        {
            var    vm     = sectors[i];
            double midAng = i * step;
            var    center = WheelGeometry.GetSectorCenter(
                cx, cy, rIn, rOut,
                midAng - step / 2.0, midAng + step / 2.0);

            // 序号水印（右上角偏移，极淡）
            double numSize = Math.Max(7, 8 * scale);
            var    numFt   = new FormattedText(
                vm.SectorNumber.ToString(),
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, numSize,
                new SolidColorBrush(Color.FromArgb(80, 80, 80, 80)), dpi);
            // 序号放在扇区右上角
            double numX = center.X + (rOut - rIn) * 0.28 * scale;
            double numY = center.Y - (rOut - rIn) * 0.28 * scale;
            dc.DrawText(numFt, new Point(numX - numFt.Width / 2,
                                         numY - numFt.Height / 2));

            // 绑定名称（居中）
            if (!vm.IsBound) continue;
            double labelSize = Math.Max(8, 9.5 * scale);
            double maxW      = (rOut - rIn) * 0.75;
            var    labelFt   = new FormattedText(
                vm.BoundName,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, labelSize,
                new SolidColorBrush(Color.FromRgb(20, 20, 20)), dpi)
            {
                MaxTextWidth  = maxW,
                MaxLineCount  = 2,
                Trimming      = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center
            };
            dc.DrawText(labelFt, new Point(
                center.X - labelFt.Width / 2,
                center.Y - labelFt.Height / 2));
        }
    }

    // ══ 鼠标交互 ══

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var vm = HitSectorVm(e.GetPosition(this));
        if (vm == _hoverSector) return;
        _hoverSector = vm;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hoverSector = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var vm = HitSectorVm(e.GetPosition(this));
        if (vm == null) return;
        SelectedSector = vm;

        // 开始扇区→扇区拖拽
        _dragSourceSector = vm;
        var data = new DataObject("WheelSectorViewModel", vm);
        DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        var vm = HitSectorVm(e.GetPosition(this));
        if (vm == null) return;
        SelectedSector = vm;
        SectorRightClicked?.Invoke(this, vm);
    }

    // ══ 拖拽目标 ══

    protected override void OnDragOver(DragEventArgs e)
    {
        var vm = HitSectorVm(e.GetPosition(this));
        if (vm != _hoverSector)
        {
            // 清除旧目标高亮
            if (_hoverSector != null) _hoverSector.IsDragTarget = false;
            _hoverSector = vm;
            if (_hoverSector != null) _hoverSector.IsDragTarget = true;
            InvalidateVisual();
        }
        e.Effects = vm != null ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        if (_hoverSector != null) _hoverSector.IsDragTarget = false;
        _hoverSector = null;
        InvalidateVisual();
    }

    protected override void OnDrop(DragEventArgs e)
    {
        if (_hoverSector != null) _hoverSector.IsDragTarget = false;
        var targetVm = HitSectorVm(e.GetPosition(this));
        _hoverSector = null;

        if (targetVm == null) { InvalidateVisual(); return; }

        // 情况1：动作格子拖来
        if (e.Data.GetDataPresent("ActionCellViewModel"))
        {
            SectorDrop?.Invoke(this, targetVm.SectorNumber);
        }
        // 情况2：扇区互换
        else if (e.Data.GetDataPresent("WheelSectorViewModel") &&
                 _dragSourceSector != null &&
                 _dragSourceSector != targetVm)
        {
            SectorSwap?.Invoke(this,
                (_dragSourceSector.SectorNumber, targetVm.SectorNumber));
        }

        _dragSourceSector = null;
        InvalidateVisual();
        e.Handled = true;
    }

    // ══ Hit Test ══
    private WheelSectorViewModel? HitSectorVm(Point mousePos)
    {
        double cx    = ActualWidth  / 2.0;
        double cy    = ActualHeight / 2.0;
        double scale = Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter;

        double dx   = mousePos.X - cx;
        double dy   = mousePos.Y - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        double rDead  = WheelConstants.R_DEAD  * scale;
        double rRing1 = WheelConstants.R_RING1 * scale;
        double rRing2 = WheelConstants.R_RING2 * scale;
        double rRing3 = WheelConstants.R_RING3 * scale;

        if (dist <= rDead || dist > rRing3) return null;

        ObservableCollection<WheelSectorViewModel>? sectors;
        int count;

        if (dist <= rRing1)
        {
            sectors = Ring1Sectors; count = 8;
        }
        else if (dist <= rRing2)
        {
            sectors = Ring2Sectors; count = Outer16Mode ? 16 : 8;
        }
        else
        {
            sectors = Ring3Sectors; count = 8;
        }

        if (sectors == null || sectors.Count == 0) return null;

        double angle = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        if (angle < 0) angle += 360.0;
        double step  = 360.0 / count;
        int    idx   = (int)Math.Floor((angle + step / 2.0) / step) % count;

        return idx < sectors.Count ? sectors[idx] : null;
    }
}
```

---

## 八、验收清单

**动作页面区：**
- [ ] 标题显示"动作页面"，旁边有"＋ 添加动作页"按钮
- [ ] 没有"内圈/外圈/扩展圈"Tab 选项
- [ ] 动作页 Tab 横向排列，可拖拽排序，右键可删除/改名
- [ ] 4×4 网格（16格），空格显示灰色"+"
- [ ] 右键格子弹出动作类型子菜单（8种类型）
- [ ] 选择类型后弹出对应配置弹窗（二次设置）
- [ ] 保存后格子显示动作名称（或图标）
- [ ] 双击已有动作格子可重新编辑
- [ ] 右键→清除动作可清空格子

**轮盘圆盘区：**
- [ ] 圆盘几何正确：单大圆背景 + R50/R110/R220/R400 圆环
- [ ] 分割线从 r=0 透底画到 r=R400，不出现多余圆形边界线
- [ ] 全局设置"外圈16格"开启时，Ring2 从8个扇区变为16个扇区，圆盘实时重绘
- [ ] 全局设置"外圈16格"关闭时，Ring2 恢复8个扇区
- [ ] 每个扇区右上角显示序号水印（极淡）
- [ ] 内圈序号 1-8，外圈(8格)序号 9-16，扩展圈序号 17-24
- [ ] 外圈16格时，外圈序号 9-24，扩展圈序号 25-32
- [ ] 点击扇区：选中高亮（蓝色边框）
- [ ] 从动作网格拖动格子到扇区：扇区高亮提示，松开后显示动作名
- [ ] 扇区之间拖拽：交换绑定内容
- [ ] 右键扇区：显示"解除绑定（动作名）"或"空"提示
- [ ] 右侧按钮："复制轮盘设置"/"清空轮盘"/"轮盘设置"均可用
- [ ] 禁用轮盘开关状态正确保存
