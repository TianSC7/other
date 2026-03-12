# Phase 2 — 设置界面  分步开发 SOP（.NET 8.0 / WPF）

> 所属总流程：`Quicker_轮盘_开发项目流程.md` → Phase 2
> UI 详细描述：`Quicker_轮盘_设置界面详细描述.md`
> 配置结构参考：`配置文件设计文档.md`
> **技术栈：.NET 8.0 + WPF + CommunityToolkit.Mvvm + System.Text.Json**
> 本阶段原则：**只管配置读写，不驱动圆盘**。Phase 3 接入后配置变更才会影响运行时行为。

---

## 阶段目标与验收总标准

| 目标 | 验收判断 |
|------|---------|
| 全局参数页所有控件可操作 | 每项修改后状态正确保存 |
| 动作管理页圆盘示意图渲染正确 | 格子颜色状态 4 种均可区分 |
| 场景 CRUD 完整 | 添加/删除/切换场景均正常 |
| 动作绑定弹窗 7 种类型均可配置 | 每种类型输入框正确显示 |
| 配置文件读写无损 | 保存后重启数据完全恢复 |
| 未保存变更提示正常 | 有修改未保存时切换/关闭提示 |

**全部通过后进入 Phase 3。**

---

## Step 2.0  项目依赖与结构

### 2.0.1  NuGet 包

```xml
<!-- WheelMenu.csproj 新增 -->
<ItemGroup>
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
  <!-- JSON 序列化，.NET 8 内置 System.Text.Json，无需额外引用 -->
</ItemGroup>
```

### 2.0.2  目录结构（Settings/ 下）

```
Settings/
  Models/
    WheelConfig.cs          ← 配置根模型（对应 JSON 根节点）
    WheelSettings.cs        ← 全局参数模型
    SceneConfig.cs          ← 单个场景模型
    SectorActionConfig.cs   ← 单个格子动作配置
  ViewModels/
    SettingsWindowViewModel.cs
    WheelSettingsPageViewModel.cs
    ActionManagerPageViewModel.cs
    SceneListViewModel.cs
    SectorEditViewModel.cs
  Views/
    SettingsWindow.xaml/.cs
    Pages/
      WheelSettingsPage.xaml/.cs    ← 全局参数页
      ActionManagerPage.xaml/.cs    ← 动作管理页
  Controls/
    WheelPreviewCanvas.cs   ← 设置界面圆盘示意图（静态，可点击）
    KeyRecorderBox.xaml/.cs ← 按键录入控件
    SectorEditDialog.xaml/.cs ← 格子编辑弹窗
  Services/
    ConfigService.cs        ← 配置文件读写服务（单例）
```

---

## Step 2.1  配置模型定义

### WheelSettings.cs

```csharp
namespace WheelMenu.Settings.Models;

public class WheelSettings
{
    public string TriggerKey          { get; set; } = "middle";
    public double Size                { get; set; } = 120.0;
    public int    TimeoutMs           { get; set; } = 0;
    public bool   OuterRing16Mode     { get; set; } = false;
    public bool   HideLabelWhenIcon   { get; set; } = true;
    public bool   ConstrainToScreen   { get; set; } = true;
    public bool   AutoMoveCursor      { get; set; } = false;
    public string RepeatTriggerKey    { get; set; } = "F1";
}
```

### SectorActionConfig.cs

```csharp
namespace WheelMenu.Settings.Models;

public enum ActionType
{
    None, Hotkey, SimulateInput, Paste,
    Open, RunAction, SendText, DateTime
}

public class SectorActionConfig
{
    public ActionType Type    { get; set; } = ActionType.None;
    public string     Value   { get; set; } = string.Empty;
    public string     Label   { get; set; } = string.Empty;   // 空=自动生成
    public string?    IconPath { get; set; } = null;
    // RunAction 专用：目标动作唯一 ID（不是名称）
    public string?    ActionRefId { get; set; } = null;
    // 动作参数传递（可选）
    public string?    ActionParam { get; set; } = null;
}
```

### SceneConfig.cs

```csharp
namespace WheelMenu.Settings.Models;

public class SceneConfig
{
    public string  Name    { get; set; } = string.Empty;
    /// <summary>null = 全局场景</summary>
    public string? Process { get; set; } = null;

    /// <summary>
    /// Key = 方向字符串（N/NE/E/SE/S/SW/W/NW）
    /// Value = null 表示继承全局场景
    /// </summary>
    public Dictionary<string, SectorActionConfig?> InnerRing    { get; set; } = new();
    public Dictionary<string, SectorActionConfig?> OuterRing    { get; set; } = new();
    public Dictionary<string, SectorActionConfig?> ExtendedRing { get; set; } = new();
}
```

### WheelConfig.cs（根节点）

```csharp
namespace WheelMenu.Settings.Models;

public class WheelConfig
{
    public int            Version  { get; set; } = 1;
    public WheelSettings  Settings { get; set; } = new();
    /// <summary>Key = "global" 或进程名小写（如 "notepad.exe"）</summary>
    public Dictionary<string, SceneConfig> Scenes { get; set; } = new()
    {
        ["global"] = new SceneConfig { Name = "全局", Process = null }
    };
}
```

---

## Step 2.2  配置读写服务（ConfigService.cs）

```csharp
namespace WheelMenu.Settings.Services;

using System.IO;
using System.Text.Json;
using WheelMenu.Settings.Models;

public class ConfigService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WheelMenu", "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented       = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters          = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    // 单例
    public static ConfigService Instance { get; } = new();
    private ConfigService() { }

    private WheelConfig? _cache;

    /// <summary>读取配置，若文件不存在则返回默认值</summary>
    public WheelConfig Load()
    {
        if (_cache != null) return _cache;
        try
        {
            if (!File.Exists(ConfigPath))
            {
                _cache = new WheelConfig();
                return _cache;
            }
            var json = File.ReadAllText(ConfigPath);
            _cache = JsonSerializer.Deserialize<WheelConfig>(json, JsonOpts)
                     ?? new WheelConfig();
            // 版本迁移（见 配置文件设计文档.md）
            MigrateIfNeeded(_cache);
            return _cache;
        }
        catch (Exception ex)
        {
            // 配置损坏：备份原文件，使用默认值
            BackupCorruptedConfig(ex);
            _cache = new WheelConfig();
            return _cache;
        }
    }

    /// <summary>保存配置到磁盘，同时更新内存缓存</summary>
    public void Save(WheelConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(config, JsonOpts);
        // 原子写入：先写临时文件，再替换
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, ConfigPath, overwrite: true);
        _cache = config;
        ConfigSaved?.Invoke(config);
    }

    /// <summary>Phase 3 订阅此事件以感知配置变更</summary>
    public event Action<WheelConfig>? ConfigSaved;

    /// <summary>获取指定进程的有效场景（软件场景优先，回退全局）</summary>
    public SceneConfig GetEffectiveScene(WheelConfig config, string? processName)
    {
        if (!string.IsNullOrEmpty(processName))
        {
            var key = processName.ToLowerInvariant();
            if (config.Scenes.TryGetValue(key, out var appScene))
                return appScene;
        }
        return config.Scenes["global"];
    }

    /// <summary>解析某个位置的最终动作（软件场景覆盖全局）</summary>
    public SectorActionConfig? ResolveSectorAction(
        WheelConfig config, string? processName,
        string ring, string direction)
    {
        var globalScene = config.Scenes["global"];
        var ringData    = GetRingDict(globalScene, ring);
        SectorActionConfig? result = ringData.GetValueOrDefault(direction);

        if (!string.IsNullOrEmpty(processName))
        {
            var key = processName.ToLowerInvariant();
            if (config.Scenes.TryGetValue(key, out var appScene))
            {
                var appRing = GetRingDict(appScene, ring);
                if (appRing.TryGetValue(direction, out var appAction))
                    result = appAction; // null 也覆盖（表示显式清空）
            }
        }
        return result;
    }

    private static Dictionary<string, SectorActionConfig?> GetRingDict(
        SceneConfig scene, string ring) => ring switch
    {
        "inner"    => scene.InnerRing,
        "outer"    => scene.OuterRing,
        "extended" => scene.ExtendedRing,
        _          => throw new ArgumentException($"Unknown ring: {ring}")
    };

    private static void MigrateIfNeeded(WheelConfig config)
    {
        // 版本迁移逻辑见 配置文件设计文档.md
        // 当前版本 = 1，无需迁移
    }

    private static void BackupCorruptedConfig(Exception ex)
    {
        try
        {
            var backup = ConfigPath + $".bak.{DateTime.Now:yyyyMMddHHmmss}";
            if (File.Exists(ConfigPath))
                File.Copy(ConfigPath, backup);
        }
        catch { /* 备份失败不影响主流程 */ }
    }
}
```

---

## Step 2.3  ViewModel 层

### SettingsWindowViewModel.cs

```csharp
namespace WheelMenu.Settings.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheelMenu.Settings.Models;
using WheelMenu.Settings.Services;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly ConfigService _configService = ConfigService.Instance;
    private WheelConfig _workingCopy;   // 编辑中的副本，未保存前不影响原始配置

    [ObservableProperty] private bool _isDirty = false;   // 有未保存修改

    public WheelSettingsPageViewModel  WheelSettingsVm  { get; }
    public ActionManagerPageViewModel  ActionManagerVm  { get; }

    public SettingsWindowViewModel()
    {
        var original = _configService.Load();
        // 深拷贝：用 JSON 序列化实现
        _workingCopy = DeepCopy(original);

        WheelSettingsVm = new WheelSettingsPageViewModel(_workingCopy.Settings);
        ActionManagerVm = new ActionManagerPageViewModel(_workingCopy);

        // 任何子 VM 变更时标记 dirty
        WheelSettingsVm.PropertyChanged += (_, _) => IsDirty = true;
        ActionManagerVm.PropertyChanged += (_, _) => IsDirty = true;
    }

    [RelayCommand]
    private void Save()
    {
        _configService.Save(_workingCopy);
        IsDirty = false;
    }

    [RelayCommand]
    private void Cancel()
    {
        // 丢弃副本，重新从磁盘加载
        _workingCopy = DeepCopy(_configService.Load());
        IsDirty = false;
    }

    /// <summary>窗口关闭前检查</summary>
    public bool CanClose()
    {
        if (!IsDirty) return true;
        var result = System.Windows.MessageBox.Show(
            "有未保存的修改，是否保存？",
            "提示",
            System.Windows.MessageBoxButton.YesNoCancel);
        return result switch
        {
            System.Windows.MessageBoxResult.Yes    => (Save(), true).Item2,
            System.Windows.MessageBoxResult.No     => true,
            _                                      => false   // Cancel，不关闭
        };
    }

    private static WheelConfig DeepCopy(WheelConfig src)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(src);
        return System.Text.Json.JsonSerializer.Deserialize<WheelConfig>(json)!;
    }
}
```

### WheelSettingsPageViewModel.cs

```csharp
namespace WheelMenu.Settings.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using WheelMenu.Settings.Models;

public partial class WheelSettingsPageViewModel : ObservableObject
{
    private readonly WheelSettings _model;

    public WheelSettingsPageViewModel(WheelSettings model)
    {
        _model = model;
    }

    public string TriggerKey
    {
        get => _model.TriggerKey;
        set { _model.TriggerKey = value; OnPropertyChanged(); }
    }

    public double Size
    {
        get => _model.Size;
        set { _model.Size = Math.Clamp(value, 50, 300); OnPropertyChanged(); }
    }

    public int TimeoutMs
    {
        get => _model.TimeoutMs;
        set { _model.TimeoutMs = Math.Max(0, value); OnPropertyChanged(); }
    }

    public bool OuterRing16Mode
    {
        get => _model.OuterRing16Mode;
        set { _model.OuterRing16Mode = value; OnPropertyChanged(); }
    }

    public bool HideLabelWhenIcon
    {
        get => _model.HideLabelWhenIcon;
        set { _model.HideLabelWhenIcon = value; OnPropertyChanged(); }
    }

    public bool ConstrainToScreen
    {
        get => _model.ConstrainToScreen;
        set
        {
            _model.ConstrainToScreen = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AutoMoveCursorEnabled));
            // 关闭限定时，同步关闭自动移指针
            if (!value) _model.AutoMoveCursor = false;
            OnPropertyChanged(nameof(AutoMoveCursor));
        }
    }

    public bool AutoMoveCursor
    {
        get => _model.AutoMoveCursor;
        set { _model.AutoMoveCursor = value; OnPropertyChanged(); }
    }

    /// <summary>自动移指针开关的 IsEnabled 绑定</summary>
    public bool AutoMoveCursorEnabled => _model.ConstrainToScreen;

    public string RepeatTriggerKey
    {
        get => _model.RepeatTriggerKey;
        set { _model.RepeatTriggerKey = value; OnPropertyChanged(); }
    }
}
```

---

## Step 2.4  圆盘示意图控件（WheelPreviewCanvas.cs）

设置界面中的可点击圆盘预览，继承 `FrameworkElement`，绘制逻辑与 Phase 1 的 `WheelCanvas` 类似，但增加了**颜色状态区分**和**点击交互**。

```csharp
namespace WheelMenu.Settings.Controls;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WheelMenu.Renderer;          // 复用 WheelGeometry、WheelConstants
using WheelMenu.Settings.Models;

/// <summary>
/// 设置界面圆盘示意图（静态，仅用于配置，不弹出）
/// 颜色含义：
///   White       = 全局场景未设置
///   #FFFDE7     = 全局已设置（内圈）
///   #FFF9C4     = 全局已设置（外圈）
///   #E3F2FD     = 当前软件场景覆盖了全局
///   #E8F5E9     = 选中状态（蓝色边框）
/// </summary>
public class WheelPreviewCanvas : FrameworkElement
{
    // ===== 依赖属性 =====

    public static readonly DependencyProperty CurrentSceneProperty =
        DependencyProperty.Register(nameof(CurrentScene), typeof(SceneConfig),
            typeof(WheelPreviewCanvas),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlobalSceneProperty =
        DependencyProperty.Register(nameof(GlobalScene), typeof(SceneConfig),
            typeof(WheelPreviewCanvas),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsGlobalProperty =
        DependencyProperty.Register(nameof(IsGlobal), typeof(bool),
            typeof(WheelPreviewCanvas),
            new FrameworkPropertyMetadata(true,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OuterRing16Property =
        DependencyProperty.Register(nameof(OuterRing16), typeof(bool),
            typeof(WheelPreviewCanvas),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public SceneConfig? CurrentScene { get => (SceneConfig?)GetValue(CurrentSceneProperty); set => SetValue(CurrentSceneProperty, value); }
    public SceneConfig? GlobalScene  { get => (SceneConfig?)GetValue(GlobalSceneProperty);  set => SetValue(GlobalSceneProperty,  value); }
    public bool         IsGlobal     { get => (bool)GetValue(IsGlobalProperty);              set => SetValue(IsGlobalProperty,     value); }
    public bool         OuterRing16  { get => (bool)GetValue(OuterRing16Property);            set => SetValue(OuterRing16Property,   value); }

    // 当前选中扇区
    private string _selectedRing    = string.Empty;
    private int    _selectedSector  = -1;

    // 当前显示圈层
    public string ActiveRing { get; set; } = "inner";   // "inner"|"outer"|"extended"

    // 点击事件
    public event EventHandler<(string Ring, int Sector, SectorActionConfig? Action)>? SectorClicked;
    public event EventHandler<(string Ring, int Sector, SectorActionConfig? Action)>? SectorRightClicked;

    private Point Center => new(ActualWidth / 2, ActualHeight / 2);

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 1 || ActualHeight < 1) return;
        var cx = Center.X;
        var cy = Center.Y;

        // 缩放比（示意图直径 340px，常量基准 320px）
        double scale = Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter;

        double deadR  = WheelConstants.DeadZoneRadius  * scale;
        double innerR = WheelConstants.InnerRingRadius * scale;
        double outerR = WheelConstants.OuterRingRadius * scale;

        DrawBackgroundCircle(dc, cx, cy, outerR);
        DrawRingSectors(dc, cx, cy, deadR, innerR, outerR, scale);
        DrawDividers(dc, cx, cy, deadR, outerR);
        DrawSectorLabels(dc, cx, cy, deadR, innerR, outerR, scale);
        DrawSelectedBorder(dc, cx, cy, deadR, innerR, outerR, scale);
    }

    private void DrawBackgroundCircle(DrawingContext dc, double cx, double cy, double r)
    {
        dc.DrawEllipse(
            new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
            new Pen(new SolidColorBrush(Color.FromRgb(220, 220, 220)), 1),
            new Point(cx, cy), r, r);
    }

    private void DrawRingSectors(DrawingContext dc, double cx, double cy,
        double deadR, double innerR, double outerR, double scale)
    {
        int sectors = OuterRing16 ? 16 : 8;
        string ring = ActiveRing;
        double rIn  = ring == "inner" ? deadR  : innerR;
        double rOut = ring == "inner" ? innerR : outerR;
        int    count = ring == "outer" && OuterRing16 ? 16 : 8;
        double step  = 360.0 / count;

        var directions8 = new[] { "N","NE","E","SE","S","SW","W","NW" };

        for (int i = 0; i < count; i++)
        {
            double startAngle = i * step - step / 2.0;
            double endAngle   = startAngle + step;
            var    geo        = WheelGeometry.CreateSectorRing(
                cx, cy, rIn, rOut, startAngle, endAngle);

            string dir    = ring == "outer" && OuterRing16
                ? $"outer_{i}"  // 16格方向名
                : directions8[i % 8];

            var    color  = GetSectorFillColor(ring, dir, i);
            bool   sel    = _selectedRing == ring && _selectedSector == i;
            dc.DrawGeometry(new SolidColorBrush(
                sel ? Color.FromArgb(80, 25, 118, 210) : color), null, geo);
        }
    }

    private Color GetSectorFillColor(string ring, string dir, int sectorIdx)
    {
        var directions8 = new[] { "N","NE","E","SE","S","SW","W","NW" };
        string d = sectorIdx < 8 ? directions8[sectorIdx] : directions8[sectorIdx % 8];

        // 检查全局场景是否有动作
        var globalRing = ring == "inner" ? GlobalScene?.InnerRing
                       : ring == "outer" ? GlobalScene?.OuterRing
                       : GlobalScene?.ExtendedRing;
        bool globalHas = globalRing?.ContainsKey(d) == true
                      && globalRing[d] != null
                      && globalRing[d]!.Type != Models.ActionType.None;

        // 检查当前场景是否覆盖
        if (!IsGlobal)
        {
            var appRing = ring == "inner" ? CurrentScene?.InnerRing
                        : ring == "outer" ? CurrentScene?.OuterRing
                        : CurrentScene?.ExtendedRing;
            bool appHas = appRing?.ContainsKey(d) == true && appRing[d] != null;
            if (appHas) return Color.FromRgb(227, 242, 253);   // 蓝色：软件场景覆盖
        }

        if (!globalHas) return Color.FromArgb(0, 255, 255, 255);   // 未设置：透明
        return ring == "inner"
            ? Color.FromRgb(255, 253, 231)  // 淡黄浅：内圈已设置
            : Color.FromRgb(255, 249, 196); // 淡黄深：外圈已设置
    }

    private void DrawDividers(DrawingContext dc, double cx, double cy, double deadR, double outerR)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), 1);
        pen.Freeze();
        for (int i = 0; i < 8; i++)
        {
            double wpfAngle = i * 45.0 - 90.0;
            var    p1 = WheelGeometry.PolarToPoint(cx, cy, deadR,  wpfAngle);
            var    p2 = WheelGeometry.PolarToPoint(cx, cy, outerR, wpfAngle);
            dc.DrawLine(pen, p1, p2);
        }
        // 圆环边界线
        var penRing = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 1);
        double innerR = WheelConstants.InnerRingRadius / WheelConstants.OuterRingRadius * outerR;
        dc.DrawEllipse(null, penRing, new Point(cx, cy), deadR, deadR);
        dc.DrawEllipse(null, penRing, new Point(cx, cy), innerR, innerR);
        dc.DrawEllipse(null, penRing, new Point(cx, cy), outerR, outerR);
    }

    private void DrawSectorLabels(DrawingContext dc, double cx, double cy,
        double deadR, double innerR, double outerR, double scale)
    {
        var directions8 = new[] { "N","NE","E","SE","S","SW","W","NW" };
        double step = 360.0 / 8;

        for (int i = 0; i < 8; i++)
        {
            string dir   = directions8[i];
            double angle = i * step;
            var    pt    = WheelGeometry.SectorCenterPoint(cx, cy, deadR, innerR, angle);

            // 取当前场景动作（含覆盖逻辑）
            SectorActionConfig? action = GetResolvedAction("inner", dir);
            string label = action?.Type != Models.ActionType.None
                ? (string.IsNullOrEmpty(action?.Label) ? action?.Value ?? "+" : action.Label)
                : "+";

            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"),
                10.0 * scale,
                new SolidColorBrush(label == "+" ? Color.FromRgb(180,180,180) : Color.FromRgb(60,60,60)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            { MaxTextWidth = 44 * scale, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };

            dc.DrawText(ft, new Point(pt.X - ft.Width / 2, pt.Y - ft.Height / 2));
        }
    }

    private void DrawSelectedBorder(DrawingContext dc, double cx, double cy,
        double deadR, double innerR, double outerR, double scale)
    {
        if (_selectedSector < 0) return;
        double step = 360.0 / 8;
        double startAngle = _selectedSector * step - step / 2.0;
        double endAngle   = startAngle + step;
        double rIn  = _selectedRing == "inner" ? deadR  : innerR;
        double rOut = _selectedRing == "inner" ? innerR : outerR;
        var    geo  = WheelGeometry.CreateSectorRing(cx, cy, rIn, rOut, startAngle, endAngle);
        var    pen  = new Pen(new SolidColorBrush(Color.FromRgb(25, 118, 210)), 2.0);
        dc.DrawGeometry(null, pen, geo);
    }

    private SectorActionConfig? GetResolvedAction(string ring, string dir)
    {
        var ringDict = ring == "inner" ? CurrentScene?.InnerRing : CurrentScene?.OuterRing;
        if (!IsGlobal && ringDict?.TryGetValue(dir, out var appAct) == true)
            return appAct;
        var globalRing = ring == "inner" ? GlobalScene?.InnerRing : GlobalScene?.OuterRing;
        return globalRing?.GetValueOrDefault(dir);
    }

    // ===== 鼠标交互 =====

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        HitSector(pos, out var ring, out var sector);
        if (sector >= 0)
        {
            _selectedRing   = ring;
            _selectedSector = sector;
            InvalidateVisual();
            SectorClicked?.Invoke(this, (ring, sector, GetResolvedAction(ring, sector == 0 ? "N" : "NE")));
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        HitSector(pos, out var ring, out var sector);
        if (sector >= 0)
            SectorRightClicked?.Invoke(this, (ring, sector, null));
    }

    private void HitSector(Point pos, out string ring, out int sector)
    {
        double scale = Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter;
        var (r, s) = WheelGeometry.HitTest(
            Center.X, Center.Y, pos.X, pos.Y,
            OuterRing16 && ActiveRing == "outer" ? 16 : 8);
        // 只有当前 ActiveRing 匹配才算命中
        ring   = r == ActiveRing ? r : string.Empty;
        sector = ring == ActiveRing ? s : -1;
    }
}
```

---

## Step 2.5  按键录入控件（KeyRecorderBox）

```csharp
// KeyRecorderBox.xaml.cs
namespace WheelMenu.Settings.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

/// <summary>
/// 点击后进入录制模式，捕获下一次按键组合并显示。
/// 用于"触发键"和"重复触发键"设置。
/// </summary>
public partial class KeyRecorderBox : UserControl
{
    public static readonly DependencyProperty KeyTextProperty =
        DependencyProperty.Register(nameof(KeyText), typeof(string),
            typeof(KeyRecorderBox),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string KeyText
    {
        get => (string)GetValue(KeyTextProperty);
        set => SetValue(KeyTextProperty, value);
    }

    private bool _recording = false;

    public KeyRecorderBox() => InitializeComponent();

    private void OnBoxClick(object s, MouseButtonEventArgs e)
    {
        _recording     = true;
        DisplayText    = "请按下目标键...";
        BorderBrush    = System.Windows.Media.Brushes.DodgerBlue;
        Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!_recording) return;
        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            _recording  = false;
            DisplayText = KeyText;
            BorderBrush = System.Windows.Media.Brushes.Gray;
            return;
        }

        // 构建组合键字符串
        var mods = new List<string>();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Alt)     != 0) mods.Add("Alt");
        if ((Keyboard.Modifiers & ModifierKeys.Shift)   != 0) mods.Add("Shift");
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) mods.Add("Win");

        // 只记录非修饰键
        var mainKey = e.Key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin
            ? null : e.Key.ToString();

        if (mainKey == null) return;   // 只按了修饰键，继续等待

        mods.Add(mainKey);
        KeyText     = string.Join("+", mods);
        DisplayText = KeyText;
        BorderBrush = System.Windows.Media.Brushes.Gray;
        _recording  = false;
    }

    // DisplayText 和 BorderBrush 绑定到 XAML 控件（略）
    private string DisplayText { set { /* 更新 TextBlock.Text */ } }
    private System.Windows.Media.Brush BorderBrush { set { /* 更新边框颜色 */ } }
}
```

---

## Step 2.6  格子编辑弹窗（SectorEditDialog）

弹窗根据 `ActionType` 动态显示不同的输入区域，使用 `StackPanel` 的 `Visibility` 绑定控制。

### XAML 骨架

```xml
<!-- SectorEditDialog.xaml -->
<Window Title="设置动作" Width="480" Height="400"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
  <Grid Margin="16">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>   <!-- 位置说明 -->
      <RowDefinition Height="Auto"/>   <!-- 动作类型下拉 -->
      <RowDefinition Height="*"/>      <!-- 动态输入区 -->
      <RowDefinition Height="Auto"/>   <!-- 图标设置 -->
      <RowDefinition Height="Auto"/>   <!-- 动作名称 -->
      <RowDefinition Height="Auto"/>   <!-- 按钮区 -->
    </Grid.RowDefinitions>

    <!-- 位置说明 -->
    <TextBlock Grid.Row="0" Text="{Binding PositionLabel}"
               FontWeight="Bold" Margin="0,0,0,8"/>

    <!-- 动作类型 -->
    <ComboBox Grid.Row="1"
              ItemsSource="{Binding ActionTypeOptions}"
              SelectedItem="{Binding SelectedActionType}"
              DisplayMemberPath="DisplayName"
              Margin="0,0,0,12"/>

    <!-- 动态输入区（根据 SelectedActionType 显示/隐藏）-->
    <ScrollViewer Grid.Row="2">
      <StackPanel>
        <!-- 发送快捷键 -->
        <controls:KeyRecorderBox
            Visibility="{Binding IsHotkeyType, Converter={...}}"
            KeyText="{Binding HotkeyValue, Mode=TwoWay}"/>

        <!-- 模拟输入/粘贴/发送文本 -->
        <TextBox Visibility="{Binding IsTextType, Converter={...}}"
                 Text="{Binding TextValue}" AcceptsReturn="True"
                 Height="80" TextWrapping="Wrap"/>

        <!-- 打开文件/URL -->
        <DockPanel Visibility="{Binding IsOpenType, Converter={...}}">
          <Button DockPanel.Dock="Right" Content="浏览..."
                  Command="{Binding BrowseCommand}"/>
          <TextBox Text="{Binding OpenValue}"/>
        </DockPanel>

        <!-- 运行动作引用 -->
        <StackPanel Visibility="{Binding IsRunActionType, Converter={...}}">
          <Button Content="{Binding SelectedActionDisplay}"
                  Command="{Binding SelectActionCommand}"/>
          <TextBlock Text="⚠️ 删除原始动作后此处失联"
                     Foreground="OrangeRed" FontSize="11"/>
          <TextBox Text="{Binding ActionParam}"
                   watermark:Placeholder.Text="传递参数（可选）"/>
        </StackPanel>

        <!-- 日期时间格式 -->
        <StackPanel Visibility="{Binding IsDateTimeType, Converter={...}}">
          <TextBox Text="{Binding DateTimeFormat, UpdateSourceTrigger=PropertyChanged}"/>
          <TextBlock Text="{Binding DateTimePreview}"
                     Foreground="Gray" FontSize="11"/>
          <WrapPanel>
            <Button Content="yyyy-MM-dd"
                    Command="{Binding InsertFormatCommand}"
                    CommandParameter="{{0:yyyy-MM-dd}}"/>
            <Button Content="HH:mm:ss"
                    Command="{Binding InsertFormatCommand}"
                    CommandParameter="{{0:HH:mm:ss}}"/>
          </WrapPanel>
        </StackPanel>
      </StackPanel>
    </ScrollViewer>

    <!-- 图标设置 -->
    <Grid Grid.Row="3" Margin="0,8,0,0">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>
      <Border Width="28" Height="28" Grid.Column="0"
              Background="#F0F0F0" BorderBrush="Gray" BorderThickness="1">
        <Image Source="{Binding IconPreview}"/>
      </Border>
      <Button Content="更换图标" Grid.Column="1" Margin="8,0,0,0"
              Command="{Binding ChangeIconCommand}"/>
      <Button Content="清除"    Grid.Column="2" Margin="4,0,0,0"
              Command="{Binding ClearIconCommand}"/>
    </Grid>

    <!-- 动作名称 -->
    <TextBox Grid.Row="4" Margin="0,8,0,0"
             Text="{Binding CustomLabel}"
             controls:Placeholder.Text="动作名称（空=自动生成）"/>

    <!-- 按钮 -->
    <StackPanel Grid.Row="5" Orientation="Horizontal"
                HorizontalAlignment="Right" Margin="0,12,0,0">
      <Button Content="确定" Width="80" IsDefault="True"
              Command="{Binding OkCommand}"/>
      <Button Content="取消" Width="80" Margin="8,0,0,0"
              IsCancel="True" Command="{Binding CancelCommand}"/>
    </StackPanel>
  </Grid>
</Window>
```

### SectorEditViewModel.cs（核心逻辑）

```csharp
namespace WheelMenu.Settings.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheelMenu.Settings.Models;

public partial class SectorEditViewModel : ObservableObject
{
    [ObservableProperty] private ActionType _selectedActionType = ActionType.None;
    [ObservableProperty] private string     _hotkeyValue   = string.Empty;
    [ObservableProperty] private string     _textValue     = string.Empty;
    [ObservableProperty] private string     _openValue     = string.Empty;
    [ObservableProperty] private string     _actionRefId   = string.Empty;
    [ObservableProperty] private string     _actionParam   = string.Empty;
    [ObservableProperty] private string     _dateTimeFormat = "{0:yyyy-MM-dd}";
    [ObservableProperty] private string     _customLabel   = string.Empty;

    public string DateTimePreview =>
        string.Format(DateTimeFormat.Replace("{0:", "{0:"), DateTime.Now);

    // 显示控制属性
    public bool IsHotkeyType    => SelectedActionType == ActionType.Hotkey;
    public bool IsTextType      => SelectedActionType is ActionType.SimulateInput
                                or ActionType.Paste or ActionType.SendText;
    public bool IsOpenType      => SelectedActionType == ActionType.Open;
    public bool IsRunActionType => SelectedActionType == ActionType.RunAction;
    public bool IsDateTimeType  => SelectedActionType == ActionType.DateTime;

    partial void OnSelectedActionTypeChanged(ActionType value)
    {
        OnPropertyChanged(nameof(IsHotkeyType));
        OnPropertyChanged(nameof(IsTextType));
        OnPropertyChanged(nameof(IsOpenType));
        OnPropertyChanged(nameof(IsRunActionType));
        OnPropertyChanged(nameof(IsDateTimeType));
    }

    partial void OnDateTimeFormatChanged(string value) =>
        OnPropertyChanged(nameof(DateTimePreview));

    [RelayCommand]
    private void Browse()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog();
        if (dlg.ShowDialog() == true)
            OpenValue = dlg.FileName;
    }

    /// <summary>将 ViewModel 状态转换为 SectorActionConfig</summary>
    public SectorActionConfig ToConfig() => new()
    {
        Type        = SelectedActionType,
        Value       = SelectedActionType switch
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
        ActionRefId = ActionRefId,
        ActionParam = ActionParam
    };

    /// <summary>从现有配置填充 ViewModel</summary>
    public void LoadFrom(SectorActionConfig? config)
    {
        if (config == null) return;
        SelectedActionType = config.Type;
        CustomLabel        = config.Label;
        ActionRefId        = config.ActionRefId ?? string.Empty;
        ActionParam        = config.ActionParam ?? string.Empty;
        switch (config.Type)
        {
            case ActionType.Hotkey:
                HotkeyValue = config.Value; break;
            case ActionType.SimulateInput:
            case ActionType.Paste:
            case ActionType.SendText:
                TextValue = config.Value; break;
            case ActionType.Open:
                OpenValue = config.Value; break;
            case ActionType.DateTime:
                DateTimeFormat = config.Value; break;
        }
    }
}
```

---

## Step 2.7  场景列表 ViewModel（SceneListViewModel.cs）

```csharp
namespace WheelMenu.Settings.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheelMenu.Settings.Models;

public partial class SceneListViewModel : ObservableObject
{
    private readonly WheelConfig _config;

    public ObservableCollection<SceneItemViewModel> Scenes { get; } = new();

    [ObservableProperty]
    private SceneItemViewModel? _selectedScene;

    public SceneListViewModel(WheelConfig config)
    {
        _config = config;
        RefreshScenes();
    }

    private void RefreshScenes()
    {
        Scenes.Clear();
        // 全局场景始终第一位
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
        var dlg = new Views.AddSceneDialog();
        if (dlg.ShowDialog() != true) return;
        var processName = dlg.ProcessName.ToLowerInvariant().Trim();
        if (string.IsNullOrEmpty(processName) || _config.Scenes.ContainsKey(processName))
            return;
        _config.Scenes[processName] = new SceneConfig
        {
            Name    = dlg.DisplayName,
            Process = processName
        };
        RefreshScenes();
        SelectedScene = Scenes.FirstOrDefault(s => s.Key == processName);
    }

    [RelayCommand]
    private void DeleteScene(SceneItemViewModel item)
    {
        if (item.IsGlobal) return;
        var result = System.Windows.MessageBox.Show(
            $"确定删除场景"{item.Scene.Name}"？", "确认删除",
            System.Windows.MessageBoxButton.YesNo);
        if (result != System.Windows.MessageBoxResult.Yes) return;
        _config.Scenes.Remove(item.Key);
        RefreshScenes();
    }
}

public class SceneItemViewModel(string key, SceneConfig scene, bool isGlobal)
{
    public string      Key     { get; } = key;
    public SceneConfig Scene   { get; } = scene;
    public bool        IsGlobal { get; } = isGlobal;
    public string      DisplayName => IsGlobal ? "🌐 全局" : $"📄 {Scene.Name}";
}
```

---

## Step 2.8  Phase 2 验收清单

- [ ] 项目可正常编译（`dotnet build`）
- [ ] `ConfigService.Load()` 在无配置文件时返回默认值
- [ ] `ConfigService.Save()` 原子写入，保存后文件内容正确（JSON 格式合法）
- [ ] 全局参数页：所有控件可操作，修改后 `IsDirty = true`
- [ ] 全局参数页：`AutoMoveCursor` 开关在 `ConstrainToScreen=false` 时为灰色
- [ ] 全局参数页：`Size` 滑块与数字输入框双向同步
- [ ] 圆盘示意图：格子颜色 4 种状态正确（白/淡黄浅/淡黄深/浅蓝）
- [ ] 圆盘示意图：点击格子触发 `SectorClicked` 事件，选中格子蓝色边框显示
- [ ] 圆盘示意图：圈层 Tab 切换后，示意图重绘对应圈格子
- [ ] 场景列表：全局场景不可删除
- [ ] 场景列表：添加软件场景后列表刷新，选中新场景
- [ ] 格子编辑弹窗：7 种动作类型切换后，正确的输入区域显示，其余隐藏
- [ ] 格子编辑弹窗：日期时间类型实时预览格式化结果
- [ ] 格子编辑弹窗：打开类型"浏览..."按钮可选择文件
- [ ] 有未保存修改时切换导航页弹出确认对话框
- [ ] 关闭窗口时若有未保存修改弹出确认对话框
- [ ] 保存后重启程序，配置完全恢复

**全部勾选后，Phase 2 完成，可进入 Phase 3 功能逻辑开发。**
