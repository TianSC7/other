# Phase 2 — 动作管理页  详细开发 SOP（.NET 8.0 / WPF）

> 所属文档：`Phase2_设置界面_分步SOP_dotnet8.md` 的补充细化
> UI 原型：`Quicker_轮盘_设置界面详细描述.md` → 第四章
> 本文档覆盖：`ActionManagerPage.xaml/.cs` + `ActionManagerPageViewModel.cs` + 所有子组件的**完整可运行代码**

---

## 一、页面结构总览

```
ActionManagerPage（Grid 两列）
├── 左列（220px 固定）
│   └── 场景列表
│       ├── ListBox（场景行）
│       │   ├── 全局场景（不可删除）
│       │   └── 软件场景（可右键操作）
│       └── 添加场景按钮
│
└── 右列（* 自动填充）
    ├── 圆盘示意图区（WheelPreviewCanvas，340px）
    ├── 圈层 Tab（内圈 / 外圈 / 扩展圈）
    └── 格子详情面板（选中格子后显示）
        ├── 位置说明文字
        ├── 动作类型下拉
        ├── 动态输入区（7种类型各自内容）
        ├── 图标设置行
        ├── 动作名称输入框
        └── 保存/清除按钮
```

---

## 二、ActionManagerPageViewModel.cs（完整实现）

```csharp
// Settings/ViewModels/ActionManagerPageViewModel.cs
namespace WheelMenu.Settings.ViewModels;

using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheelMenu.Settings.Models;
using WheelMenu.Settings.Services;

public partial class ActionManagerPageViewModel : ObservableObject
{
    private readonly WheelConfig _config;

    // ===== 场景列表 =====
    public ObservableCollection<SceneItemViewModel> Scenes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAppScene))]
    [NotifyPropertyChangedFor(nameof(CurrentSceneForPreview))]
    private SceneItemViewModel? _selectedScene;

    partial void OnSelectedSceneChanged(SceneItemViewModel? value)
    {
        // 切换场景时，清除格子选中状态
        SelectedRing   = string.Empty;
        SelectedSector = -1;
        RefreshSectorDetail();
        OnPropertyChanged(nameof(CurrentSceneForPreview));
    }

    public bool IsAppScene => SelectedScene?.IsGlobal == false;

    /// <summary>传给 WheelPreviewCanvas 的当前场景（可能是全局或软件场景）</summary>
    public SceneConfig? CurrentSceneForPreview => SelectedScene?.Scene;

    /// <summary>全局场景始终传给 WheelPreviewCanvas 作为兜底</summary>
    public SceneConfig? GlobalSceneForPreview  => _config.Scenes.GetValueOrDefault("global");

    // ===== 圈层 Tab =====
    // 0=内圈 1=外圈 2=扩展圈
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveRingName))]
    [NotifyPropertyChangedFor(nameof(OuterTabLabel))]
    private int _activeTabIndex = 0;

    public string ActiveRingName => _activeTabIndex switch
    {
        0 => "inner",
        1 => "outer",
        2 => "extended",
        _ => "inner"
    };

    public string OuterTabLabel => _config.Settings.OuterRing16Mode
        ? "外圈 (16格)" : "外圈 (8格)";

    // ===== 选中格子 =====
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSectorLabel))]
    [NotifyPropertyChangedFor(nameof(HasSelectedSector))]
    private string _selectedRing = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSectorLabel))]
    [NotifyPropertyChangedFor(nameof(HasSelectedSector))]
    private int _selectedSector = -1;

    public bool HasSelectedSector => SelectedSector >= 0 && !string.IsNullOrEmpty(SelectedRing);

    public string SelectedSectorLabel
    {
        get
        {
            if (!HasSelectedSector) return string.Empty;
            string ringLabel = SelectedRing switch
            {
                "inner"    => "内圈",
                "outer"    => "外圈",
                "extended" => "扩展圈",
                _          => SelectedRing
            };
            string dirLabel = GetDirectionLabel(SelectedSector, SelectedRing);
            return $"{ringLabel} · {dirLabel}";
        }
    }

    // ===== 格子详情 VM（内联编辑，不弹窗）=====
    public SectorDetailViewModel SectorDetail { get; } = new();

    // ===== 复制粘贴缓冲区 =====
    private SectorActionConfig? _clipboard;

    // ===== 构造 =====
    public ActionManagerPageViewModel(WheelConfig config)
    {
        _config = config;
        RefreshScenes();
        SectorDetail.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SectorDetail));
    }

    // ===== 场景管理 =====

    private void RefreshScenes()
    {
        Scenes.Clear();
        Scenes.Add(new SceneItemViewModel("global", _config.Scenes["global"], isGlobal: true));
        foreach (var (key, scene) in _config.Scenes)
        {
            if (key == "global") continue;
            Scenes.Add(new SceneItemViewModel(key, scene, isGlobal: false));
        }
        SelectedScene = Scenes.FirstOrDefault();
    }

    [RelayCommand]
    private void AddScene()
    {
        var dlg = new Views.AddSceneDialog { Owner = GetOwnerWindow() };
        if (dlg.ShowDialog() != true) return;

        string key = dlg.ProcessName.ToLowerInvariant().Trim();
        if (string.IsNullOrEmpty(key)) return;
        if (_config.Scenes.ContainsKey(key))
        {
            System.Windows.MessageBox.Show($"场景 "{key}" 已存在。",
                "提示", System.Windows.MessageBoxButton.OK);
            return;
        }

        _config.Scenes[key] = new SceneConfig
            { Name = dlg.DisplayName, Process = key };
        RefreshScenes();
        SelectedScene = Scenes.FirstOrDefault(s => s.Key == key);
        OnPropertyChanged(string.Empty);  // 通知父 VM dirty
    }

    [RelayCommand]
    private void DeleteScene(SceneItemViewModel? item)
    {
        if (item == null || item.IsGlobal) return;
        var r = System.Windows.MessageBox.Show(
            $"确定删除场景 "{item.Scene.Name}"？此操作不可撤销。",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (r != System.Windows.MessageBoxResult.Yes) return;

        _config.Scenes.Remove(item.Key);
        RefreshScenes();
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void RenameScene(SceneItemViewModel? item)
    {
        if (item == null || item.IsGlobal) return;
        var dlg = new Views.InputDialog("重命名场景", "显示名称：", item.Scene.Name)
            { Owner = GetOwnerWindow() };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Result)) return;
        item.Scene.Name = dlg.Result;
        RefreshScenes();
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void CopySceneTo(SceneItemViewModel? source)
    {
        if (source == null) return;
        // 弹出目标场景选择
        var targets = Scenes.Where(s => s.Key != source.Key).ToList();
        if (!targets.Any()) return;

        var dlg = new Views.ScenePickerDialog(targets) { Owner = GetOwnerWindow() };
        if (dlg.ShowDialog() != true || dlg.SelectedScene == null) return;

        var targetScene = dlg.SelectedScene.Scene;
        var json = System.Text.Json.JsonSerializer.Serialize(source.Scene);
        var copy = System.Text.Json.JsonSerializer.Deserialize<SceneConfig>(json)!;
        // 保留目标名称和进程名，只复制格子配置
        targetScene.InnerRing    = copy.InnerRing;
        targetScene.OuterRing    = copy.OuterRing;
        targetScene.ExtendedRing = copy.ExtendedRing;
        OnPropertyChanged(string.Empty);
    }

    // ===== 格子选中 =====

    /// <summary>由 WheelPreviewCanvas 的 SectorClicked 事件调用</summary>
    public void OnSectorClicked(string ring, int sectorIndex)
    {
        SelectedRing   = ring;
        SelectedSector = sectorIndex;
        RefreshSectorDetail();
    }

    private void RefreshSectorDetail()
    {
        if (!HasSelectedSector || SelectedScene == null)
        {
            SectorDetail.Clear();
            return;
        }

        string dir    = GetDirection(SelectedSector, SelectedRing);
        var    scene  = SelectedScene.Scene;
        var    ringDict = GetRingDict(scene, SelectedRing);
        var    action = ringDict.GetValueOrDefault(dir);

        // 软件场景中未覆盖的格子：显示全局值（只读提示）
        bool isInherited = false;
        if (!SelectedScene.IsGlobal && !ringDict.ContainsKey(dir))
        {
            var globalRing = GetRingDict(_config.Scenes["global"], SelectedRing);
            action      = globalRing.GetValueOrDefault(dir);
            isInherited = true;
        }

        SectorDetail.LoadFrom(action, SelectedSectorLabel, isInherited);
    }

    // ===== 格子操作命令 =====

    [RelayCommand]
    private void SaveSectorAction()
    {
        if (!HasSelectedSector || SelectedScene == null) return;

        string dir     = GetDirection(SelectedSector, SelectedRing);
        var    scene   = SelectedScene.Scene;
        var    ring    = GetRingDict(scene, SelectedRing);
        ring[dir]      = SectorDetail.ToConfig();

        // 刷新示意图
        OnPropertyChanged(nameof(CurrentSceneForPreview));
        OnPropertyChanged(string.Empty);   // dirty
    }

    [RelayCommand]
    private void ClearSectorAction()
    {
        if (!HasSelectedSector || SelectedScene == null) return;

        string dir   = GetDirection(SelectedSector, SelectedRing);
        var    scene = SelectedScene.Scene;
        var    ring  = GetRingDict(scene, SelectedRing);

        if (SelectedScene.IsGlobal)
            ring.Remove(dir);          // 全局：直接移除
        else
            ring[dir] = null;          // 软件场景：设 null = 显式清空（不回退全局）

        SectorDetail.Clear();
        OnPropertyChanged(nameof(CurrentSceneForPreview));
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void OpenSectorEditDialog()
    {
        if (!HasSelectedSector || SelectedScene == null) return;

        string dir    = GetDirection(SelectedSector, SelectedRing);
        var    scene  = SelectedScene.Scene;
        var    ring   = GetRingDict(scene, SelectedRing);
        var    action = ring.GetValueOrDefault(dir);

        var dlg = new Controls.SectorEditDialog(action, SelectedSectorLabel)
            { Owner = GetOwnerWindow() };
        if (dlg.ShowDialog() != true) return;

        ring[dir] = dlg.ResultConfig;
        RefreshSectorDetail();
        OnPropertyChanged(nameof(CurrentSceneForPreview));
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private void CopySectorAction()
    {
        if (!HasSelectedSector || SelectedScene == null) return;
        string dir  = GetDirection(SelectedSector, SelectedRing);
        var    ring = GetRingDict(SelectedScene.Scene, SelectedRing);
        var    act  = ring.GetValueOrDefault(dir);
        if (act == null) return;
        // 深拷贝
        var json = System.Text.Json.JsonSerializer.Serialize(act);
        _clipboard = System.Text.Json.JsonSerializer.Deserialize<SectorActionConfig>(json);
    }

    [RelayCommand(CanExecute = nameof(CanPasteSectorAction))]
    private void PasteSectorAction()
    {
        if (_clipboard == null || !HasSelectedSector || SelectedScene == null) return;
        string dir  = GetDirection(SelectedSector, SelectedRing);
        var    ring = GetRingDict(SelectedScene.Scene, SelectedRing);
        var    json = System.Text.Json.JsonSerializer.Serialize(_clipboard);
        ring[dir]   = System.Text.Json.JsonSerializer.Deserialize<SectorActionConfig>(json);
        RefreshSectorDetail();
        OnPropertyChanged(nameof(CurrentSceneForPreview));
        OnPropertyChanged(string.Empty);
    }

    private bool CanPasteSectorAction() => _clipboard != null;

    [RelayCommand]
    private void TestExecuteSectorAction()
    {
        if (!HasSelectedSector || SelectedScene == null) return;
        string dir    = GetDirection(SelectedSector, SelectedRing);
        var    ring   = GetRingDict(SelectedScene.Scene, SelectedRing);
        var    action = ring.GetValueOrDefault(dir);
        if (action == null || action.Type == ActionType.None)
        {
            System.Windows.MessageBox.Show("此格子未绑定动作。", "测试执行");
            return;
        }
        // 直接调用 ActionExecutor（需引用 Logic 层，此处可用弱引用/事件解耦）
        TestExecuteRequested?.Invoke(action);
    }

    /// <summary>由 App.xaml.cs 订阅，转发给 ActionExecutor 执行</summary>
    public event Action<SectorActionConfig>? TestExecuteRequested;

    // ===== 辅助方法 =====

    private static readonly string[] Dirs8 =
        { "N","NE","E","SE","S","SW","W","NW" };
    private static readonly string[] Dirs16 =
        { "N","NNE","NE","ENE","E","ESE","SE","SSE",
          "S","SSW","SW","WSW","W","WNW","NW","NNW" };

    private string GetDirection(int sectorIndex, string ring)
    {
        bool use16 = ring == "outer" && _config.Settings.OuterRing16Mode;
        var  dirs  = use16 ? Dirs16 : Dirs8;
        return dirs[sectorIndex % dirs.Length];
    }

    private string GetDirectionLabel(int sectorIndex, string ring)
    {
        var dir = GetDirection(sectorIndex, ring);
        return dir switch
        {
            "N"   => "北方向 (N)",
            "NE"  => "右上方向 (NE)",
            "E"   => "右方向 (E)",
            "SE"  => "右下方向 (SE)",
            "S"   => "南方向 (S)",
            "SW"  => "左下方向 (SW)",
            "W"   => "左方向 (W)",
            "NW"  => "左上方向 (NW)",
            _     => dir
        };
    }

    private static Dictionary<string, SectorActionConfig?> GetRingDict(
        SceneConfig scene, string ring) => ring switch
    {
        "inner"    => scene.InnerRing,
        "outer"    => scene.OuterRing,
        "extended" => scene.ExtendedRing,
        _          => scene.InnerRing
    };

    private static System.Windows.Window? GetOwnerWindow() =>
        System.Windows.Application.Current.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive)
        ?? System.Windows.Application.Current.MainWindow;
}
```

---

## 三、SectorDetailViewModel.cs（内联格子编辑）

> 内联编辑面板，显示在动作管理页右下角，不弹窗。双击格子时改为弹窗（`SectorEditDialog`）。

```csharp
// Settings/ViewModels/SectorDetailViewModel.cs
namespace WheelMenu.Settings.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheelMenu.Settings.Models;

public partial class SectorDetailViewModel : ObservableObject
{
    [ObservableProperty] private string     _positionLabel  = string.Empty;
    [ObservableProperty] private bool       _isInherited    = false;   // 灰色提示"继承自全局"
    [ObservableProperty] private ActionType _selectedType   = ActionType.None;
    [ObservableProperty] private string     _hotkeyValue    = string.Empty;
    [ObservableProperty] private string     _textValue      = string.Empty;
    [ObservableProperty] private string     _openValue      = string.Empty;
    [ObservableProperty] private string     _actionRefId    = string.Empty;
    [ObservableProperty] private string     _actionParam    = string.Empty;
    [ObservableProperty] private string     _dateTimeFormat = "{0:yyyy-MM-dd}";
    [ObservableProperty] private string     _customLabel    = string.Empty;
    [ObservableProperty] private System.Windows.Media.ImageSource? _iconPreview;

    // 类型可见性控制
    public bool IsNoneType      => SelectedType == ActionType.None;
    public bool IsHotkeyType    => SelectedType == ActionType.Hotkey;
    public bool IsTextType      => SelectedType is ActionType.SimulateInput
                                or ActionType.Paste or ActionType.SendText;
    public bool IsOpenType      => SelectedType == ActionType.Open;
    public bool IsRunActionType => SelectedType == ActionType.RunAction;
    public bool IsDateTimeType  => SelectedType == ActionType.DateTime;
    public string DateTimePreview => FormatDateTime(DateTimeFormat);

    partial void OnSelectedTypeChanged(ActionType value)
    {
        OnPropertyChanged(nameof(IsNoneType));
        OnPropertyChanged(nameof(IsHotkeyType));
        OnPropertyChanged(nameof(IsTextType));
        OnPropertyChanged(nameof(IsOpenType));
        OnPropertyChanged(nameof(IsRunActionType));
        OnPropertyChanged(nameof(IsDateTimeType));
    }

    partial void OnDateTimeFormatChanged(string value) =>
        OnPropertyChanged(nameof(DateTimePreview));

    public void LoadFrom(SectorActionConfig? action, string posLabel, bool inherited)
    {
        PositionLabel = posLabel;
        IsInherited   = inherited;

        if (action == null || action.Type == ActionType.None)
        {
            Clear();
            PositionLabel = posLabel;
            return;
        }

        SelectedType   = action.Type;
        CustomLabel    = action.Label ?? string.Empty;
        ActionRefId    = action.ActionRefId ?? string.Empty;
        ActionParam    = action.ActionParam ?? string.Empty;
        IconPreview    = LoadIcon(action.IconPath);

        HotkeyValue    = string.Empty;
        TextValue      = string.Empty;
        OpenValue      = string.Empty;
        DateTimeFormat = "{0:yyyy-MM-dd}";

        switch (action.Type)
        {
            case ActionType.Hotkey:        HotkeyValue    = action.Value; break;
            case ActionType.SimulateInput:
            case ActionType.Paste:
            case ActionType.SendText:      TextValue      = action.Value; break;
            case ActionType.Open:          OpenValue      = action.Value; break;
            case ActionType.DateTime:      DateTimeFormat = action.Value; break;
        }
    }

    public void Clear()
    {
        SelectedType   = ActionType.None;
        HotkeyValue    = string.Empty;
        TextValue      = string.Empty;
        OpenValue      = string.Empty;
        ActionRefId    = string.Empty;
        ActionParam    = string.Empty;
        DateTimeFormat = "{0:yyyy-MM-dd}";
        CustomLabel    = string.Empty;
        IconPreview    = null;
        IsInherited    = false;
        PositionLabel  = string.Empty;
    }

    public SectorActionConfig ToConfig() => new()
    {
        Type        = SelectedType,
        Value       = SelectedType switch
        {
            ActionType.Hotkey        => HotkeyValue,
            ActionType.SimulateInput => TextValue,
            ActionType.Paste         => TextValue,
            ActionType.Open          => OpenValue,
            ActionType.SendText      => TextValue,
            ActionType.DateTime      => DateTimeFormat,
            _                        => string.Empty
        },
        Label       = CustomLabel,
        ActionRefId = string.IsNullOrEmpty(ActionRefId) ? null : ActionRefId,
        ActionParam = string.IsNullOrEmpty(ActionParam) ? null : ActionParam
    };

    [RelayCommand]
    private void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
            { Filter = "所有文件|*.*|程序|*.exe|快捷方式|*.lnk" };
        if (dlg.ShowDialog() == true) OpenValue = dlg.FileName;
    }

    [RelayCommand]
    private void InsertDateFormat(string? fmt)
    {
        if (!string.IsNullOrEmpty(fmt)) DateTimeFormat = fmt;
    }

    [RelayCommand]
    private void ChangeIcon()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
            { Filter = "图片文件|*.png;*.jpg;*.ico;*.bmp" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage(
                new Uri(dlg.FileName));
            bmp.Freeze();
            IconPreview = bmp;
        }
        catch { System.Windows.MessageBox.Show("无法加载图片文件。", "错误"); }
    }

    [RelayCommand]
    private void ClearIcon() => IconPreview = null;

    private static string FormatDateTime(string fmt)
    {
        try { return string.Format(fmt, DateTime.Now); }
        catch { return "格式错误"; }
    }

    private static System.Windows.Media.ImageSource? LoadIcon(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
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

## 四、SceneItemViewModel.cs

```csharp
// Settings/ViewModels/SceneItemViewModel.cs
namespace WheelMenu.Settings.ViewModels;

using System.Windows.Media;
using System.Windows.Media.Imaging;
using WheelMenu.Settings.Models;

public class SceneItemViewModel
{
    public string      Key      { get; }
    public SceneConfig Scene    { get; }
    public bool        IsGlobal { get; }

    public string DisplayName =>
        IsGlobal ? "🌐 全局" : Scene.Name;

    /// <summary>软件场景的 EXE 图标（尝试从系统提取）</summary>
    public ImageSource? ProcessIcon { get; }

    public SceneItemViewModel(string key, SceneConfig scene, bool isGlobal)
    {
        Key      = key;
        Scene    = scene;
        IsGlobal = isGlobal;

        if (!isGlobal && !string.IsNullOrEmpty(scene.Process))
            ProcessIcon = TryExtractIcon(scene.Process);
    }

    private static ImageSource? TryExtractIcon(string processName)
    {
        try
        {
            // 尝试在 PATH 和常见目录中查找 EXE
            string? exePath = FindExePath(processName);
            if (exePath == null) return null;

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            if (icon == null) return null;

            var bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16));
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    private static string? FindExePath(string exeName)
    {
        // 1. 当前运行进程中查找
        var proc = System.Diagnostics.Process.GetProcessesByName(
            System.IO.Path.GetFileNameWithoutExtension(exeName)).FirstOrDefault();
        if (proc?.MainModule?.FileName is string path) return path;

        // 2. 从 PATH 环境变量查找
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            string full = System.IO.Path.Combine(dir, exeName);
            if (System.IO.File.Exists(full)) return full;
        }
        return null;
    }
}
```

---

## 五、ActionManagerPage.xaml（完整布局）

```xml
<!-- Settings/Views/Pages/ActionManagerPage.xaml -->
<Page x:Class="WheelMenu.Settings.Views.Pages.ActionManagerPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:vm="clr-namespace:WheelMenu.Settings.ViewModels"
      xmlns:ctrl="clr-namespace:WheelMenu.Settings.Controls"
      xmlns:conv="clr-namespace:WheelMenu.Settings.Converters"
      Title="动作管理">

  <Page.Resources>
    <conv:BoolToVisibilityConverter x:Key="BoolToVis"/>
    <conv:InverseBoolToVisibilityConverter x:Key="InvBoolToVis"/>
    <conv:ActionTypeToDisplayConverter x:Key="ActionTypeDisplay"/>
  </Page.Resources>

  <!-- 两列主布局 -->
  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="220"/>
      <ColumnDefinition Width="Auto"/>  <!-- 分隔线 -->
      <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- ===== 左列：场景列表 ===== -->
    <Grid Grid.Column="0" Background="#F5F5F5">
      <Grid.RowDefinitions>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
      </Grid.RowDefinitions>

      <!-- 场景列表 -->
      <ListBox Grid.Row="0"
               ItemsSource="{Binding Scenes}"
               SelectedItem="{Binding SelectedScene}"
               BorderThickness="0"
               Background="Transparent"
               ScrollViewer.HorizontalScrollBarVisibility="Disabled">
        <ListBox.ItemTemplate>
          <DataTemplate DataType="{x:Type vm:SceneItemViewModel}">
            <Grid Height="32" Margin="4,2">
              <Grid.ContextMenu>
                <ContextMenu>
                  <MenuItem Header="重命名"
                            Command="{Binding DataContext.RenameSceneCommand,
                                RelativeSource={RelativeSource AncestorType=Page}}"
                            CommandParameter="{Binding}"/>
                  <MenuItem Header="复制配置到..."
                            Command="{Binding DataContext.CopySceneToCommand,
                                RelativeSource={RelativeSource AncestorType=Page}}"
                            CommandParameter="{Binding}"/>
                  <Separator/>
                  <MenuItem Header="删除"
                            Command="{Binding DataContext.DeleteSceneCommand,
                                RelativeSource={RelativeSource AncestorType=Page}}"
                            CommandParameter="{Binding}"
                            IsEnabled="{Binding IsGlobal,
                                Converter={StaticResource InvBoolToVis}}"/>
                </ContextMenu>
              </Grid.ContextMenu>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="20"/>
                <ColumnDefinition Width="*"/>
              </Grid.ColumnDefinitions>
              <!-- 进程图标（或emoji） -->
              <Image Grid.Column="0" Width="16" Height="16"
                     Source="{Binding ProcessIcon}"
                     Visibility="{Binding ProcessIcon,
                         Converter={StaticResource BoolToVis}}"/>
              <TextBlock Grid.Column="0" Text="🌐"
                         Visibility="{Binding IsGlobal,
                             Converter={StaticResource BoolToVis}}"
                         FontSize="13" VerticalAlignment="Center"/>
              <TextBlock Grid.Column="1" Text="{Binding DisplayName}"
                         VerticalAlignment="Center" Margin="4,0,0,0"
                         TextTrimming="CharacterEllipsis"/>
            </Grid>
          </DataTemplate>
        </ListBox.ItemTemplate>
        <ListBox.ItemContainerStyle>
          <Style TargetType="ListBoxItem">
            <Setter Property="Padding" Value="0"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
          </Style>
        </ListBox.ItemContainerStyle>
      </ListBox>

      <!-- 添加场景按钮 -->
      <Button Grid.Row="1" Content="＋ 添加场景"
              Command="{Binding AddSceneCommand}"
              Margin="8" Height="32"
              Background="White"
              BorderBrush="#BDBDBD"/>
    </Grid>

    <!-- 分隔线 -->
    <GridSplitter Grid.Column="1" Width="1"
                  Background="#E0E0E0"
                  HorizontalAlignment="Center"
                  ResizeBehavior="PreviousAndNext"/>

    <!-- ===== 右列：编辑区 ===== -->
    <Grid Grid.Column="2">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>   <!-- 圈层 Tab -->
        <RowDefinition Height="360"/>    <!-- 圆盘示意图 -->
        <RowDefinition Height="*"/>      <!-- 格子详情 -->
      </Grid.RowDefinitions>

      <!-- 圈层 Tab -->
      <TabControl Grid.Row="0"
                  SelectedIndex="{Binding ActiveTabIndex}"
                  Margin="12,8,12,0">
        <TabItem Header="内圈 (8格)"/>
        <TabItem Header="{Binding OuterTabLabel}"/>
        <TabItem Header="扩展圈 (8格)"/>
      </TabControl>

      <!-- 圆盘示意图 -->
      <ctrl:WheelPreviewCanvas
          Grid.Row="1"
          Width="340" Height="340"
          HorizontalAlignment="Center"
          VerticalAlignment="Center"
          CurrentScene="{Binding CurrentSceneForPreview}"
          GlobalScene="{Binding GlobalSceneForPreview}"
          IsGlobal="{Binding SelectedScene.IsGlobal}"
          OuterRing16="{Binding ActiveRingName,
              Converter={StaticResource OuterRing16Converter}}"
          ActiveRing="{Binding ActiveRingName}"
          SectorClicked="OnSectorClicked"
          SectorRightClicked="OnSectorRightClicked"
          SectorDoubleClicked="OnSectorDoubleClicked"/>

      <!-- 格子详情面板 -->
      <ScrollViewer Grid.Row="2" Margin="12,0,12,12"
                    Visibility="{Binding HasSelectedSector,
                        Converter={StaticResource BoolToVis}}">
        <Border BorderBrush="#E0E0E0" BorderThickness="1"
                CornerRadius="4" Padding="12">
          <StackPanel DataContext="{Binding SectorDetail}">

            <!-- 位置说明 -->
            <TextBlock Text="{Binding PositionLabel}"
                       FontWeight="Bold" FontSize="13"
                       Margin="0,0,0,4"/>

            <!-- 继承提示（软件场景且未覆盖时）-->
            <TextBlock Text="继承自全局场景（修改后将覆盖全局）"
                       Foreground="#9E9E9E" FontSize="11"
                       Visibility="{Binding IsInherited,
                           Converter={StaticResource BoolToVis}}"
                       Margin="0,0,0,8"/>

            <!-- 动作类型 -->
            <TextBlock Text="动作类型" FontSize="11"
                       Foreground="#616161" Margin="0,0,0,2"/>
            <ComboBox ItemsSource="{x:Static vm:SectorDetailViewModel.ActionTypeOptions}"
                      SelectedItem="{Binding SelectedType}"
                      DisplayMemberPath="Display"
                      SelectedValuePath="Value"
                      Margin="0,0,0,8" Height="28"/>

            <!-- 无动作 -->
            <TextBlock Text="此格子不执行任何动作。"
                       Foreground="#9E9E9E"
                       Visibility="{Binding IsNoneType,
                           Converter={StaticResource BoolToVis}}"
                       Margin="0,0,0,8"/>

            <!-- 发送快捷键 -->
            <StackPanel Visibility="{Binding IsHotkeyType,
                            Converter={StaticResource BoolToVis}}">
              <TextBlock Text="快捷键" FontSize="11"
                         Foreground="#616161" Margin="0,0,0,2"/>
              <ctrl:KeyRecorderBox KeyText="{Binding HotkeyValue, Mode=TwoWay}"
                                   Margin="0,0,0,8"/>
            </StackPanel>

            <!-- 文本类（模拟输入/粘贴/发送文本）-->
            <StackPanel Visibility="{Binding IsTextType,
                            Converter={StaticResource BoolToVis}}">
              <TextBlock FontSize="11" Foreground="#616161" Margin="0,0,0,2">
                <Run Text="{Binding SelectedType,
                         Converter={StaticResource ActionTypeDisplay}}"/>
                <Run Text=" 内容"/>
              </TextBlock>
              <TextBox Text="{Binding TextValue, UpdateSourceTrigger=PropertyChanged}"
                       AcceptsReturn="True" Height="80"
                       TextWrapping="Wrap" VerticalScrollBarVisibility="Auto"
                       Margin="0,0,0,4"/>
              <!-- SimulateInput 专属说明 -->
              <TextBlock Text="使用 {键名} 格式表示按键，如 {ctrl}{c} {enter}"
                         FontSize="10" Foreground="#9E9E9E"
                         Visibility="{Binding IsSimulateInputType,
                             Converter={StaticResource BoolToVis}}"
                         Margin="0,0,0,8"/>
              <!-- SendText FILE: 说明 -->
              <TextBlock Text="支持 FILE:路径 从文件读取内容"
                         FontSize="10" Foreground="#9E9E9E"
                         Visibility="{Binding IsSendTextType,
                             Converter={StaticResource BoolToVis}}"
                         Margin="0,0,0,8"/>
            </StackPanel>

            <!-- 打开文件/URL -->
            <StackPanel Visibility="{Binding IsOpenType,
                            Converter={StaticResource BoolToVis}}">
              <TextBlock Text="路径或网址" FontSize="11"
                         Foreground="#616161" Margin="0,0,0,2"/>
              <DockPanel Margin="0,0,0,4">
                <Button DockPanel.Dock="Right" Content="浏览..."
                        Command="{Binding BrowseCommand}"
                        Width="60" Margin="4,0,0,0"/>
                <TextBox Text="{Binding OpenValue, UpdateSourceTrigger=PropertyChanged}"/>
              </DockPanel>
              <TextBlock Text="支持环境变量，如 %APPDATA%\MyFolder"
                         FontSize="10" Foreground="#9E9E9E" Margin="0,0,0,8"/>
            </StackPanel>

            <!-- 运行动作引用 -->
            <StackPanel Visibility="{Binding IsRunActionType,
                            Converter={StaticResource BoolToVis}}">
              <TextBlock Text="目标动作 ID" FontSize="11"
                         Foreground="#616161" Margin="0,0,0,2"/>
              <TextBox Text="{Binding ActionRefId, UpdateSourceTrigger=PropertyChanged}"
                       Margin="0,0,0,4"/>
              <TextBlock Text="⚠️ 删除原始动作后此格子将失联，需重新绑定"
                         Foreground="#F57C00" FontSize="10" Margin="0,0,0,4"/>
              <TextBlock Text="传递参数（可选）" FontSize="11"
                         Foreground="#616161" Margin="0,4,0,2"/>
              <TextBox Text="{Binding ActionParam, UpdateSourceTrigger=PropertyChanged}"
                       Margin="0,0,0,8"/>
            </StackPanel>

            <!-- 日期时间 -->
            <StackPanel Visibility="{Binding IsDateTimeType,
                            Converter={StaticResource BoolToVis}}">
              <TextBlock Text="格式字符串" FontSize="11"
                         Foreground="#616161" Margin="0,0,0,2"/>
              <TextBox Text="{Binding DateTimeFormat,
                           UpdateSourceTrigger=PropertyChanged}"
                       Margin="0,0,0,4"/>
              <TextBlock Text="{Binding DateTimePreview}"
                         Foreground="#757575" FontSize="11"
                         Margin="0,0,0,4"/>
              <WrapPanel Margin="0,0,0,8">
                <Button Content="yyyy-MM-dd"
                        Command="{Binding InsertDateFormatCommand}"
                        CommandParameter="{}{0:yyyy-MM-dd}"
                        Margin="0,0,4,4" Padding="6,2"/>
                <Button Content="HH:mm:ss"
                        Command="{Binding InsertDateFormatCommand}"
                        CommandParameter="{}{0:HH:mm:ss}"
                        Margin="0,0,4,4" Padding="6,2"/>
                <Button Content="yyyy年MM月dd日"
                        Command="{Binding InsertDateFormatCommand}"
                        CommandParameter="{}{0:yyyy年MM月dd日}"
                        Margin="0,0,4,4" Padding="6,2"/>
              </WrapPanel>
            </StackPanel>

            <!-- 图标设置 -->
            <TextBlock Text="图标" FontSize="11"
                       Foreground="#616161" Margin="0,0,0,2"/>
            <StackPanel Orientation="Horizontal" Margin="0,0,0,8">
              <Border Width="28" Height="28"
                      Background="#F5F5F5"
                      BorderBrush="#BDBDBD" BorderThickness="1">
                <Image Source="{Binding IconPreview}" Stretch="Uniform"/>
              </Border>
              <Button Content="更换" Width="50" Margin="6,0,4,0"
                      Command="{Binding ChangeIconCommand}"/>
              <Button Content="清除" Width="50"
                      Command="{Binding ClearIconCommand}"/>
            </StackPanel>

            <!-- 动作名称 -->
            <TextBlock Text="显示名称（空=自动生成）" FontSize="11"
                       Foreground="#616161" Margin="0,0,0,2"/>
            <TextBox Text="{Binding CustomLabel, UpdateSourceTrigger=PropertyChanged}"
                     Margin="0,0,0,12"/>

            <!-- 操作按钮 -->
            <StackPanel Orientation="Horizontal">
              <Button Content="保存到此格子" Width="100"
                      Command="{Binding DataContext.SaveSectorActionCommand,
                          RelativeSource={RelativeSource AncestorType=Page}}"
                      Background="#1976D2" Foreground="White"
                      BorderThickness="0"/>
              <Button Content="清除此格子" Width="80" Margin="8,0,0,0"
                      Command="{Binding DataContext.ClearSectorActionCommand,
                          RelativeSource={RelativeSource AncestorType=Page}}"
                      Foreground="#F44336"/>
            </StackPanel>

          </StackPanel>
        </Border>
      </ScrollViewer>

      <!-- 未选中格子时的提示 -->
      <TextBlock Grid.Row="2"
                 Text="点击圆盘上的格子进行编辑"
                 HorizontalAlignment="Center"
                 VerticalAlignment="Center"
                 Foreground="#9E9E9E"
                 Visibility="{Binding HasSelectedSector,
                     Converter={StaticResource InvBoolToVis}}"/>
    </Grid>
  </Grid>
</Page>
```

---

## 六、ActionManagerPage.xaml.cs（Code-Behind）

```csharp
// Settings/Views/Pages/ActionManagerPage.xaml.cs
namespace WheelMenu.Settings.Views.Pages;

using System.Windows;
using System.Windows.Controls;
using WheelMenu.Settings.Controls;
using WheelMenu.Settings.ViewModels;

public partial class ActionManagerPage : Page
{
    private ActionManagerPageViewModel Vm =>
        (ActionManagerPageViewModel)DataContext;

    public ActionManagerPage()
    {
        InitializeComponent();
    }

    // ===== 圆盘格子事件转发到 VM =====

    private void OnSectorClicked(object sender,
        (string Ring, int Sector, Settings.Models.SectorActionConfig? Action) e)
    {
        Vm.OnSectorClicked(e.Ring, e.Sector);
    }

    private void OnSectorDoubleClicked(object sender,
        (string Ring, int Sector, Settings.Models.SectorActionConfig? Action) e)
    {
        Vm.OnSectorClicked(e.Ring, e.Sector);
        Vm.OpenSectorEditDialogCommand.Execute(null);
    }

    private void OnSectorRightClicked(object sender,
        (string Ring, int Sector, Settings.Models.SectorActionConfig? Action) e)
    {
        Vm.OnSectorClicked(e.Ring, e.Sector);
        ShowSectorContextMenu();
    }

    private void ShowSectorContextMenu()
    {
        var menu = new ContextMenu();

        var editItem  = new MenuItem { Header = "设置动作..." };
        editItem.Click += (_, _) => Vm.OpenSectorEditDialogCommand.Execute(null);
        menu.Items.Add(editItem);

        var clearItem = new MenuItem { Header = "清除动作" };
        clearItem.Click += (_, _) => Vm.ClearSectorActionCommand.Execute(null);
        menu.Items.Add(clearItem);

        menu.Items.Add(new Separator());

        var copyItem  = new MenuItem { Header = "复制" };
        copyItem.Click += (_, _) => Vm.CopySectorActionCommand.Execute(null);
        menu.Items.Add(copyItem);

        var pasteItem = new MenuItem { Header = "粘贴",
            IsEnabled = Vm.PasteSectorActionCommand.CanExecute(null) };
        pasteItem.Click += (_, _) => Vm.PasteSectorActionCommand.Execute(null);
        menu.Items.Add(pasteItem);

        menu.Items.Add(new Separator());

        var testItem  = new MenuItem { Header = "测试执行" };
        testItem.Click += (_, _) => Vm.TestExecuteSectorActionCommand.Execute(null);
        menu.Items.Add(testItem);

        menu.IsOpen = true;
    }
}
```

---

## 七、AddSceneDialog.xaml/.cs

```xml
<!-- Settings/Views/AddSceneDialog.xaml -->
<Window x:Class="WheelMenu.Settings.Views.AddSceneDialog"
        Title="添加软件场景" Width="420" Height="360"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize">
  <Grid Margin="16">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <TextBlock Grid.Row="0" Text="进程名（含 .exe）："
               Margin="0,0,0,4"/>
    <TextBox   Grid.Row="1" x:Name="ProcessNameBox"
               Margin="0,0,0,4"
               Text="{Binding ProcessName, UpdateSourceTrigger=PropertyChanged}"/>
    <TextBlock Grid.Row="1" Text="如：notepad.exe"
               Foreground="#9E9E9E" FontSize="10"
               VerticalAlignment="Bottom" Margin="4,0,0,18"/>

    <TextBlock Grid.Row="2" Text="或从当前运行进程中选择："
               Margin="0,8,0,4"/>

    <ListBox Grid.Row="3" x:Name="ProcessList"
             ItemsSource="{Binding RunningProcesses}"
             DisplayMemberPath="Display"
             SelectionChanged="OnProcessSelected"
             BorderBrush="#BDBDBD"/>

    <StackPanel Grid.Row="4" Orientation="Horizontal"
                HorizontalAlignment="Right" Margin="0,12,0,0">
      <Button Content="确定" Width="80" IsDefault="True"
              Click="OnOkClick"
              IsEnabled="{Binding IsValid}"/>
      <Button Content="取消" Width="80" Margin="8,0,0,0"
              IsCancel="True" Click="OnCancelClick"/>
    </StackPanel>
  </Grid>
</Window>
```

```csharp
// Settings/Views/AddSceneDialog.xaml.cs
namespace WheelMenu.Settings.Views;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

public partial class AddSceneDialog : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _processName = string.Empty;
    public string ProcessName
    {
        get => _processName;
        set
        {
            _processName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsValid)));
            // 自动生成显示名称
            if (string.IsNullOrEmpty(DisplayName))
                DisplayName = System.IO.Path.GetFileNameWithoutExtension(value);
        }
    }

    public string DisplayName { get; set; } = string.Empty;
    public bool   IsValid => !string.IsNullOrWhiteSpace(ProcessName)
                          && ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    public ObservableCollection<ProcessItem> RunningProcesses { get; } = new();

    public AddSceneDialog()
    {
        InitializeComponent();
        DataContext = this;
        RefreshProcessList();
    }

    private void RefreshProcessList()
    {
        RunningProcesses.Clear();
        var procs = Process.GetProcesses()
            .Where(p =>
            {
                try { return !string.IsNullOrEmpty(p.MainWindowTitle)
                          && p.MainModule != null; }
                catch { return false; }
            })
            .GroupBy(p => p.ProcessName.ToLowerInvariant())
            .Select(g => g.First())
            .OrderBy(p => p.ProcessName);

        foreach (var p in procs)
        {
            try
            {
                RunningProcesses.Add(new ProcessItem(
                    p.ProcessName.ToLowerInvariant() + ".exe",
                    p.MainWindowTitle));
            }
            catch { }
        }
    }

    private void OnProcessSelected(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        var item = (ProcessItem)e.AddedItems[0]!;
        ProcessName = item.ProcessName;
        DisplayName = item.WindowTitle;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (!IsValid) return;
        ProcessName = ProcessName.ToLowerInvariant().Trim();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) =>
        DialogResult = false;

    public record ProcessItem(string ProcessName, string WindowTitle)
    {
        public string Display => $"{ProcessName}  —  {WindowTitle}";
    }
}
```

---

## 八、验收清单（动作管理页专项）

- [ ] 左侧场景列表正确显示"🌐 全局"+ 所有软件场景
- [ ] 全局场景右键菜单中"删除"为灰色不可点击
- [ ] 软件场景可右键重命名，名称立即更新
- [ ] 添加场景对话框中，进程列表显示当前运行进程
- [ ] 点击进程列表行，自动填充进程名输入框
- [ ] 圆盘示意图：内圈8格/外圈8格（默认）格子均匀
- [ ] 内圈已绑定格子显示淡黄色（`#FFFDE7`），空格显示白色+灰色"+"
- [ ] 软件场景下，覆盖全局的格子显示浅蓝色（`#E3F2FD`）
- [ ] 软件场景下，未覆盖的格子显示灰色继承文字
- [ ] 点击格子后底部详情面板显示，位置说明文字正确（"内圈 · 北方向 (N)"）
- [ ] 切换圈层 Tab，圆盘示意图重绘对应圈层格子
- [ ] 开启 16 格模式后，外圈 Tab 标签变为"外圈 (16格)"，示意图格子数变为 16
- [ ] 格子详情面板：切换动作类型后，对应输入区域显示，其余隐藏
- [ ] 日期时间类型：格式字符串修改后预览实时更新
- [ ] 打开类型：点击"浏览..."可选择文件，路径填入输入框
- [ ] 点击"保存到此格子"后，圆盘示意图格子颜色/文字立即更新
- [ ] 右键格子菜单：6 项全部可点击，"粘贴"在无复制内容时为灰色
- [ ] 双击格子弹出 `SectorEditDialog` 弹窗，确定后格子更新
- [ ] 保存整体配置后，重启应用格子绑定完全恢复
- [ ] 软件场景清除某格子（设为 null）后，该格子不显示全局兜底内容