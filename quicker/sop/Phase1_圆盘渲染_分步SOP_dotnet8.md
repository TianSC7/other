# Phase 1 — 圆盘界面渲染  分步开发 SOP（.NET 8.0 / WPF）

> 所属总流程：`Quicker_轮盘_开发项目流程.md` → Phase 1
> 尺寸参考：`Quicker_轮盘菜单_界面尺寸详细描述.md`
> **技术栈：.NET 8.0 + WPF（Windows Presentation Foundation）**
> 本阶段原则：**只管画，不接逻辑**。所有动作数据用硬编码假数据填充，Phase 3 再替换为真实数据。

---

## 阶段目标与验收总标准

| 目标 | 验收判断 |
|------|---------|
| 圆盘在屏幕正确位置弹出 | 视觉上出现完整圆盘，无错位 |
| 三圈结构比例正确 | 内圈:外圈 = 60:80 逻辑单位，圆心死区 20 |
| 8 方向格子清晰可辨 | 分隔线均匀，扇形面积相等 |
| 图标与文字正确显示 | 假数据填充时布局不溢出 |
| 高亮切换正常 | 指定格子背景色可切换 |
| 动画流畅 | 弹出/关闭无卡顿，无闪烁 |
| 屏幕边缘不超出 | 四角触发时圆盘完整显示 |

**全部通过后进入 Phase 2。**

---

## Step 1.0  项目结构与技术选型

### 1.0.1  技术方案说明

WPF 天然支持透明窗口、矢量绘图、DPI 感知和动画系统，是 .NET 8.0 下实现轮盘界面的最优选择：

| 需求 | WPF 方案 |
|------|---------|
| 透明无边框窗口 | `WindowStyle=None` + `AllowsTransparency=True` + `Background=Transparent` |
| 鼠标事件穿透 | `WS_EX_TRANSPARENT` + `WS_EX_LAYERED`（Win32 扩展样式，P/Invoke）|
| 扇形绘制 | `PathGeometry` + `ArcSegment` + `LineSegment` |
| 高性能绘制 | `DrawingVisual` 或 `OnRender(DrawingContext dc)` 覆写 |
| 动画 | `DoubleAnimation` + `Storyboard`，或 `CompositionTarget.Rendering` 手动帧 |
| DPI 适配 | `VisualTreeHelper.GetDpi()` + `dpiX/dpiY` 换算 |
| 始终置顶 | `Topmost=True` |

### 1.0.2  项目结构

```
WheelMenu.sln
└── WheelMenu/                         ← 主项目（WPF, .NET 8.0-windows）
    ├── WheelMenu.csproj
    ├── App.xaml / App.xaml.cs
    │
    ├── Renderer/                      ← Phase 1：渲染层（本阶段全部在这里）
    │   ├── WheelConstants.cs          ← 所有尺寸、颜色、动画常量
    │   ├── WheelGeometry.cs           ← 扇形路径计算工具类（静态）
    │   ├── SectorState.cs             ← 枚举：格子状态
    │   ├── WheelSectorData.cs         ← 格子数据模型（Phase 1 用假数据）
    │   ├── WheelCanvas.cs             ← 核心绘制控件（继承 FrameworkElement）
    │   ├── WheelAnimator.cs           ← 弹出/关闭动画控制器
    │   └── FakeData.cs                ← Phase 1 假数据（Phase 3 时删除/替换）
    │
    ├── Settings/                      ← Phase 2：设置界面（本阶段不涉及）
    ├── Logic/                         ← Phase 3：功能逻辑（本阶段不涉及）
    │
    ├── Windows/
    │   └── WheelWindow.xaml/.cs       ← 轮盘宿主窗口（透明全屏窗口）
    │
    └── TestWindow.xaml/.cs            ← Phase 1 测试用窗口（键盘驱动高亮）
```

### 1.0.3  csproj 配置

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- 高 DPI 感知，必须设置，否则坐标计算错误 -->
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
</Project>
```

`app.manifest` 必须声明 DPI 感知模式：

```xml
<application xmlns="urn:schemas-microsoft-com:asm.v3">
  <windowsSettings>
    <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">
      PerMonitorV2
    </dpiAwareness>
  </windowsSettings>
</application>
```

---

## Step 1.1  常量定义（WheelConstants.cs）

所有尺寸使用 **WPF 逻辑像素（device-independent pixels, DIPs）**，与 DPI 无关。

```csharp
// WheelConstants.cs
namespace WheelMenu.Renderer;

public static class WheelConstants
{
    // ===== 圆盘尺寸（WPF 逻辑像素，DPI 无关）=====
    public const double DeadZoneRadius  = 20.0;   // 圆心死区半径
    public const double InnerRingRadius = 80.0;   // 内圈外半径
    public const double OuterRingRadius = 160.0;  // 外圈外半径
    public const double WheelDiameter   = 320.0;  // 整体直径

    // ===== 格子数量 =====
    public const int InnerSectors    = 8;
    public const int OuterSectors8   = 8;
    public const int OuterSectors16  = 16;
    public const int ExtendedSectors = 8;

    // ===== 图标/字体（逻辑像素）=====
    public const double IconSizeInner  = 24.0;   // 内圈图标
    public const double IconSizeCenter = 20.0;   // 圆心提示图标
    public const double IconSizeLarge  = 28.0;   // 仅图标模式（无文字）
    public const double FontSizeLabel  = 11.0;   // 格子文字
    public const double FontSizeCenter = 11.0;   // 圆心文字
    public const double IconTextGap    = 3.0;    // 图标与文字间距

    // ===== 颜色（浅色主题）=====
    // 使用 Color.FromArgb(a, r, g, b)，a=0~255
    public static readonly Color ColorBackground    = Color.FromArgb(224, 255, 255, 255);
    public static readonly Color ColorSectorEmpty   = Color.FromArgb(  0, 255, 255, 255);
    public static readonly Color ColorSectorNormal  = Color.FromArgb( 30, 255, 255, 255);
    public static readonly Color ColorSectorHovered = Color.FromArgb( 50,  30, 120, 255);
    public static readonly Color ColorDivider       = Color.FromArgb( 31,   0,   0,   0);
    public static readonly Color ColorText          = Color.FromArgb(255,  51,  51,  51);
    public static readonly Color ColorTextEmpty     = Color.FromArgb(255, 180, 180, 180);

    // ===== 动画 =====
    public static readonly TimeSpan AnimOpenDuration  = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan AnimCloseDuration = TimeSpan.FromMilliseconds(80);
    public const double AnimOpenScaleFrom  = 0.3;
    public const double AnimCloseScaleTo   = 0.5;

    // ===== 分隔线 =====
    public const double DividerThicknessPrimary   = 1.0;   // 主分隔线（8方向）
    public const double DividerThicknessSecondary = 0.5;   // 次分隔线（16格模式新增）
}
```

> ⚠️ **WPF 坐标系说明**：WPF 使用逻辑像素（1 逻辑像素 = 1/96 英寸），在 DPI 100%（96dpi）下逻辑像素 = 物理像素；DPI 125% 时，1 逻辑像素 = 1.25 物理像素。所有绘制坐标使用逻辑像素，WPF 自动处理 DPI 缩放，**无需手动乘以 DPI 系数**。

---

## Step 1.2  扇形几何工具（WheelGeometry.cs）

```csharp
// WheelGeometry.cs
namespace WheelMenu.Renderer;

using System.Windows;
using System.Windows.Media;

public static class WheelGeometry
{
    /// <summary>
    /// 生成一个扇环（环形扇形）的 PathGeometry，用于 WPF 绘制。
    /// 角度约定：0° = 正上方，顺时针为正（与屏幕坐标系一致，Y轴向下）。
    /// </summary>
    /// <param name="cx">圆心 X</param>
    /// <param name="cy">圆心 Y</param>
    /// <param name="rInner">内半径</param>
    /// <param name="rOuter">外半径</param>
    /// <param name="startAngleDeg">起始角度（轮盘坐标，0°=正上方，顺时针）</param>
    /// <param name="endAngleDeg">终止角度</param>
    public static PathGeometry CreateSectorRing(
        double cx, double cy,
        double rInner, double rOuter,
        double startAngleDeg, double endAngleDeg)
    {
        // 轮盘角度 → WPF 标准角度（WPF：0°=正右方，顺时针）
        // 轮盘 0°（上）= WPF -90°
        double wpfStart = startAngleDeg - 90.0;
        double wpfEnd   = endAngleDeg   - 90.0;
        bool   isLarge  = (endAngleDeg - startAngleDeg) > 180.0;

        var outerStart = PolarToPoint(cx, cy, rOuter, wpfStart);
        var outerEnd   = PolarToPoint(cx, cy, rOuter, wpfEnd);
        var innerStart = PolarToPoint(cx, cy, rInner, wpfStart);
        var innerEnd   = PolarToPoint(cx, cy, rInner, wpfEnd);

        var figure = new PathFigure { StartPoint = outerStart, IsClosed = true, IsFilled = true };
        // 外弧（顺时针）
        figure.Segments.Add(new ArcSegment(outerEnd,
            new Size(rOuter, rOuter), 0, isLarge,
            SweepDirection.Clockwise, true));
        // 连接到内弧末端
        figure.Segments.Add(new LineSegment(innerEnd, true));
        // 内弧（逆时针，回到起点方向）
        figure.Segments.Add(new ArcSegment(innerStart,
            new Size(rInner, rInner), 0, isLarge,
            SweepDirection.Counterclockwise, true));
        // 自动闭合回 outerStart

        return new PathGeometry(new[] { figure });
    }

    /// <summary>极坐标转 WPF Point（WPF 角度：0°=右，顺时针）</summary>
    public static Point PolarToPoint(double cx, double cy, double r, double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180.0;
        return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    /// <summary>
    /// 扇形视觉中心点（用于放置图标和文字）。
    /// 中心点 = 内外半径中点，沿扇区中心角方向。
    /// </summary>
    public static Point SectorCenterPoint(
        double cx, double cy,
        double rInner, double rOuter,
        double centerAngleDeg)
    {
        double rMid   = (rInner + rOuter) / 2.0;
        double wpfAng = centerAngleDeg - 90.0;   // 轮盘角 → WPF角
        return PolarToPoint(cx, cy, rMid, wpfAng);
    }

    /// <summary>
    /// 由鼠标坐标判断圈层和扇区索引。
    /// 返回 (ring, sectorIndex)：
    ///   ring = "dead" | "inner" | "outer" | "extended"
    ///   sectorIndex = 0~7（8格）或 0~15（16格），dead 时为 -1
    /// </summary>
    public static (string Ring, int Sector) HitTest(
        double cx, double cy,
        double mouseX, double mouseY,
        int totalSectors = 8)
    {
        double dx   = mouseX - cx;
        double dy   = mouseY - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        // 轮盘角度：atan2(dx, -dy)，0°=正上方，顺时针
        double angleDeg = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        if (angleDeg < 0) angleDeg += 360.0;

        double step   = 360.0 / totalSectors;
        int    sector = (int)((angleDeg + step / 2.0) / step) % totalSectors;

        string ring = dist switch
        {
            <= WheelConstants.DeadZoneRadius  => "dead",
            <= WheelConstants.InnerRingRadius => "inner",
            <= WheelConstants.OuterRingRadius => "outer",
            _                                 => "extended"
        };

        return (ring, ring == "dead" ? -1 : sector);
    }

    /// <summary>
    /// 屏幕边缘适配：计算圆盘应显示的圆心坐标。
    /// </summary>
    public static Point CalculateWheelCenter(
        double mouseX, double mouseY,
        Rect screenWorkArea,
        bool constrainToScreen)
    {
        if (!constrainToScreen)
            return new Point(mouseX, mouseY);

        double r  = WheelConstants.OuterRingRadius;
        double cx = Math.Clamp(mouseX, screenWorkArea.Left + r, screenWorkArea.Right  - r);
        double cy = Math.Clamp(mouseY, screenWorkArea.Top  + r, screenWorkArea.Bottom - r);
        return new Point(cx, cy);
    }

    /// <summary>方向名（N/NE/E...）→ 扇区中心角度（轮盘坐标，0°=正上，顺时针）</summary>
    public static double DirectionToCenterAngle(string direction, int totalSectors = 8)
    {
        var map8 = new Dictionary<string, int>
        {
            {"N",0},{"NE",1},{"E",2},{"SE",3},
            {"S",4},{"SW",5},{"W",6},{"NW",7}
        };
        double step = 360.0 / totalSectors;
        return map8[direction] * step;
    }
}
```

---

## Step 1.3  格子数据模型与假数据

### SectorState.cs

```csharp
// SectorState.cs
namespace WheelMenu.Renderer;

public enum SectorState
{
    Empty,    // 无动作绑定，显示 "+"
    Normal,   // 有动作，未高亮
    Hovered   // 鼠标悬停高亮
}

public enum DisplayMode
{
    IconAndLabel,  // 有图标 + 有文字（默认）
    IconOnly,      // 有图标时隐藏文字
    LabelOnly      // 无图标，只显文字
}
```

### WheelSectorData.cs

```csharp
// WheelSectorData.cs
namespace WheelMenu.Renderer;

using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>单个扇区的显示数据（Phase 1 用假数据，Phase 3 替换为真实配置）</summary>
public class WheelSectorData
{
    public bool       HasAction { get; set; } = false;
    public string     Label     { get; set; } = string.Empty;
    public ImageSource? Icon    { get; set; } = null;

    /// <summary>根据设置项计算实际显示模式</summary>
    public DisplayMode GetDisplayMode(bool hideLabelWhenIcon) =>
        (HasAction, Icon != null, hideLabelWhenIcon) switch
        {
            (false, _, _)          => DisplayMode.LabelOnly,   // 空格，显示 "+"
            (true, true, true)     => DisplayMode.IconOnly,
            (true, true, false)    => DisplayMode.IconAndLabel,
            (true, false, _)       => DisplayMode.LabelOnly,
            _                      => DisplayMode.LabelOnly
        };
}
```

### FakeData.cs

```csharp
// FakeData.cs — Phase 1 专用，Phase 3 接入真实数据后整体删除
namespace WheelMenu.Renderer;

using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class FakeData
{
    // 假图标：用纯色矩形 DrawingImage 代替真实图片，无需图片文件
    private static ImageSource MakeSolidIcon(Color color, double size = 24)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            dc.DrawRectangle(new SolidColorBrush(color), null,
                new System.Windows.Rect(0, 0, size, size));
        var bmp = new RenderTargetBitmap(
            (int)size, (int)size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        return bmp;
    }

    public static WheelSectorData[] InnerRing { get; } =
    [
        new() { HasAction=true,  Label="复制",              Icon=null },                              // N
        new() { HasAction=true,  Label="粘贴",              Icon=MakeSolidIcon(Color.FromRgb( 70,130,180)) }, // NE
        new() { HasAction=true,  Label="撤销",              Icon=MakeSolidIcon(Color.FromRgb(255,165,  0)) }, // E
        new() { HasAction=true,  Label="很长的动作名称测试溢出", Icon=null },                         // SE
        new() { HasAction=true,  Label="保存",              Icon=MakeSolidIcon(Color.FromRgb( 34,139, 34)) }, // S
        new() { HasAction=true,  Label="关闭",              Icon=null },                              // SW
        new() { HasAction=false, Label=string.Empty,        Icon=null },                              // W（空）
        new() { HasAction=true,  Label="截图",              Icon=MakeSolidIcon(Color.FromRgb(147,112,219)) }, // NW
    ];

    public static WheelSectorData[] OuterRing { get; } =
    [
        new() { HasAction=true,  Label="浏览器", Icon=MakeSolidIcon(Color.FromRgb(255, 99, 71)) }, // N
        new() { HasAction=true,  Label="文件夹", Icon=MakeSolidIcon(Color.FromRgb(255,215,  0)) }, // NE
        new() { HasAction=false, Label=string.Empty, Icon=null },
        new() { HasAction=false, Label=string.Empty, Icon=null },
        new() { HasAction=true,  Label="设置",   Icon=MakeSolidIcon(Color.FromRgb(128,128,128)) }, // S
        new() { HasAction=false, Label=string.Empty, Icon=null },
        new() { HasAction=false, Label=string.Empty, Icon=null },
        new() { HasAction=false, Label=string.Empty, Icon=null },
    ];
}
```

---

## Step 1.4  核心绘制控件（WheelCanvas.cs）

继承 `FrameworkElement` 并覆写 `OnRender`，使用 `DrawingContext` 绘制所有内容。

```csharp
// WheelCanvas.cs
namespace WheelMenu.Renderer;

using System.Windows;
using System.Windows.Media;

public class WheelCanvas : FrameworkElement
{
    // ===== 状态 =====
    private Point  _center          = new(0, 0);
    private string _highlightRing   = string.Empty;  // "inner"|"outer"|"extended"|""
    private int    _highlightSector = -1;
    private bool   _outerRing16Mode = false;
    private bool   _hideLabelWhenIcon = true;

    // ===== 数据 =====
    public WheelSectorData[] InnerData { get; set; } = FakeData.InnerRing;
    public WheelSectorData[] OuterData { get; set; } = FakeData.OuterRing;

    // ===== 公开接口（Phase 3 将调用这些）=====

    public void SetCenter(Point center)
    {
        _center = center;
        InvalidateVisual();
    }

    public void SetHighlight(string ring, int sectorIndex)
    {
        _highlightRing   = ring;
        _highlightSector = sectorIndex;
        InvalidateVisual();   // 触发重绘
    }

    public void ClearHighlight()
    {
        _highlightRing   = string.Empty;
        _highlightSector = -1;
        InvalidateVisual();
    }

    public void SetDisplayOptions(bool hideLabelWhenIcon, bool outerRing16)
    {
        _hideLabelWhenIcon = hideLabelWhenIcon;
        _outerRing16Mode   = outerRing16;
        InvalidateVisual();
    }

    // ===== 核心绘制 =====

    protected override void OnRender(DrawingContext dc)
    {
        double cx = _center.X;
        double cy = _center.Y;

        DrawBackground(dc, cx, cy);
        DrawOuterRing(dc, cx, cy);
        DrawInnerRing(dc, cx, cy);
        DrawDividers(dc, cx, cy);
        DrawDeadZone(dc, cx, cy);
        DrawLabels(dc, cx, cy);
        DrawCenterHint(dc, cx, cy);
    }

    // ----- 1. 圆盘整体背景 -----
    private void DrawBackground(DrawingContext dc, double cx, double cy)
    {
        var brush = new SolidColorBrush(WheelConstants.ColorBackground);
        // 可选：添加投影效果
        dc.DrawEllipse(brush, null, new Point(cx, cy),
            WheelConstants.OuterRingRadius,
            WheelConstants.OuterRingRadius);
    }

    // ----- 2. 外圈扇形 -----
    private void DrawOuterRing(DrawingContext dc, double cx, double cy)
    {
        int sectors = _outerRing16Mode
            ? WheelConstants.OuterSectors16
            : WheelConstants.OuterSectors8;
        double step = 360.0 / sectors;

        for (int i = 0; i < sectors; i++)
        {
            double startAngle = i * step - step / 2.0;
            double endAngle   = startAngle + step;
            var    geo        = WheelGeometry.CreateSectorRing(
                cx, cy,
                WheelConstants.InnerRingRadius,
                WheelConstants.OuterRingRadius,
                startAngle, endAngle);

            bool isHovered = _highlightRing == "outer" && _highlightSector == i;
            var  data      = GetOuterData(i, sectors);
            var  color     = GetSectorColor(data?.HasAction ?? false, isHovered);

            dc.DrawGeometry(new SolidColorBrush(color), null, geo);
        }
    }

    // ----- 3. 内圈扇形 -----
    private void DrawInnerRing(DrawingContext dc, double cx, double cy)
    {
        double step = 360.0 / WheelConstants.InnerSectors;

        for (int i = 0; i < WheelConstants.InnerSectors; i++)
        {
            double startAngle = i * step - step / 2.0;
            double endAngle   = startAngle + step;
            var    geo        = WheelGeometry.CreateSectorRing(
                cx, cy,
                WheelConstants.DeadZoneRadius,
                WheelConstants.InnerRingRadius,
                startAngle, endAngle);

            bool isHovered = _highlightRing == "inner" && _highlightSector == i;
            var  data      = GetInnerData(i);
            var  color     = GetSectorColor(data?.HasAction ?? false, isHovered);

            dc.DrawGeometry(new SolidColorBrush(color), null, geo);
        }
    }

    // ----- 4. 放射状分隔线 -----
    private void DrawDividers(DrawingContext dc, double cx, double cy)
    {
        var pen = new Pen(new SolidColorBrush(WheelConstants.ColorDivider),
            WheelConstants.DividerThicknessPrimary);
        pen.Freeze();

        // 8条主分隔线（穿越内外圈）
        for (int i = 0; i < 8; i++)
        {
            double angle = i * 45.0 - 90.0; // 转 WPF 角
            var    inner = WheelGeometry.PolarToPoint(cx, cy, WheelConstants.DeadZoneRadius, angle);
            var    outer = WheelGeometry.PolarToPoint(cx, cy, WheelConstants.OuterRingRadius, angle);
            dc.DrawLine(pen, inner, outer);
        }

        // 16格模式：新增 8 条次分隔线（仅外圈）
        if (_outerRing16Mode)
        {
            var penSecondary = new Pen(
                new SolidColorBrush(WheelConstants.ColorDivider),
                WheelConstants.DividerThicknessSecondary);
            penSecondary.Freeze();

            for (int i = 0; i < 8; i++)
            {
                double angle = i * 45.0 + 22.5 - 90.0; // 主线中间位置
                var    inner = WheelGeometry.PolarToPoint(cx, cy, WheelConstants.InnerRingRadius, angle);
                var    outer = WheelGeometry.PolarToPoint(cx, cy, WheelConstants.OuterRingRadius, angle);
                dc.DrawLine(penSecondary, inner, outer);
            }
        }

        // 圆心圆形边框
        var penCircle = new Pen(new SolidColorBrush(WheelConstants.ColorDivider), 1.0);
        dc.DrawEllipse(null, penCircle, new Point(cx, cy),
            WheelConstants.DeadZoneRadius, WheelConstants.DeadZoneRadius);
        dc.DrawEllipse(null, penCircle, new Point(cx, cy),
            WheelConstants.InnerRingRadius, WheelConstants.InnerRingRadius);
        dc.DrawEllipse(null, penCircle, new Point(cx, cy),
            WheelConstants.OuterRingRadius, WheelConstants.OuterRingRadius);
    }

    // ----- 5. 圆心死区背景 -----
    private void DrawDeadZone(DrawingContext dc, double cx, double cy)
    {
        var brush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
        dc.DrawEllipse(brush, null, new Point(cx, cy),
            WheelConstants.DeadZoneRadius, WheelConstants.DeadZoneRadius);
    }

    // ----- 6. 格子内图标和文字 -----
    private void DrawLabels(DrawingContext dc, double cx, double cy)
    {
        int innerSectors = WheelConstants.InnerSectors;
        double innerStep = 360.0 / innerSectors;

        for (int i = 0; i < innerSectors; i++)
        {
            var data = GetInnerData(i);
            if (data == null) continue;
            double centerAngle = i * innerStep;
            var    center      = WheelGeometry.SectorCenterPoint(
                cx, cy,
                WheelConstants.DeadZoneRadius,
                WheelConstants.InnerRingRadius,
                centerAngle);
            DrawSectorContent(dc, center, data,
                WheelConstants.IconSizeInner,
                WheelConstants.FontSizeLabel,
                maxTextWidth: 48.0);
        }

        // 外圈（无图标/文字，只在圆心提示区显示）
        // 空格子显示 "+"
        for (int i = 0; i < innerSectors; i++)
        {
            var data = GetInnerData(i);
            if (data != null && !data.HasAction)
            {
                double centerAngle = i * innerStep;
                var    center      = WheelGeometry.SectorCenterPoint(
                    cx, cy,
                    WheelConstants.DeadZoneRadius,
                    WheelConstants.InnerRingRadius,
                    centerAngle);
                DrawPlusSign(dc, center);
            }
        }
    }

    // ----- 7. 圆心提示区（悬停外圈时显示）-----
    private void DrawCenterHint(DrawingContext dc, double cx, double cy)
    {
        if (_highlightRing != "outer" || _highlightSector < 0) return;

        int sectors = _outerRing16Mode ? WheelConstants.OuterSectors16 : WheelConstants.OuterSectors8;
        var data    = GetOuterData(_highlightSector, sectors);
        if (data == null || !data.HasAction) return;

        var center = new Point(cx, cy);
        DrawSectorContent(dc, center, data,
            WheelConstants.IconSizeCenter,
            WheelConstants.FontSizeCenter,
            maxTextWidth: 36.0);
    }

    // ===== 辅助方法 =====

    private void DrawSectorContent(DrawingContext dc, Point center,
        WheelSectorData data, double iconSize, double fontSize, double maxTextWidth)
    {
        var mode = data.GetDisplayMode(_hideLabelWhenIcon);
        double totalH = 0;
        if (mode == DisplayMode.IconAndLabel)
            totalH = iconSize + WheelConstants.IconTextGap + fontSize * 1.4;
        else if (mode == DisplayMode.IconOnly)
            totalH = WheelConstants.IconSizeLarge;
        else
            totalH = fontSize * 1.4;

        double startY = center.Y - totalH / 2.0;

        if (mode != DisplayMode.LabelOnly && data.Icon != null)
        {
            double actualIconSize = mode == DisplayMode.IconOnly
                ? WheelConstants.IconSizeLarge : iconSize;
            dc.DrawImage(data.Icon,
                new Rect(center.X - actualIconSize / 2, startY,
                         actualIconSize, actualIconSize));
            startY += actualIconSize + WheelConstants.IconTextGap;
        }

        if (mode != DisplayMode.IconOnly && !string.IsNullOrEmpty(data.Label))
        {
            var ft = MakeFormattedText(data.Label, fontSize, maxTextWidth);
            dc.DrawText(ft,
                new Point(center.X - ft.Width / 2, startY));
        }
    }

    private void DrawPlusSign(DrawingContext dc, Point center)
    {
        var ft = MakeFormattedText("+", 14.0, 20.0,
            new SolidColorBrush(WheelConstants.ColorTextEmpty));
        dc.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
    }

    private FormattedText MakeFormattedText(
        string text, double fontSize, double maxWidth,
        Brush? brush = null)
    {
        brush ??= new SolidColorBrush(WheelConstants.ColorText);
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Microsoft YaHei UI"),
                FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            fontSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        ft.MaxTextWidth  = maxWidth;
        ft.MaxLineCount  = 1;
        ft.Trimming      = TextTrimming.CharacterEllipsis;
        return ft;
    }

    private Color GetSectorColor(bool hasAction, bool isHovered)
    {
        if (isHovered) return WheelConstants.ColorSectorHovered;
        if (hasAction) return WheelConstants.ColorSectorNormal;
        return WheelConstants.ColorSectorEmpty;
    }

    private WheelSectorData? GetInnerData(int index) =>
        index >= 0 && index < InnerData.Length ? InnerData[index] : null;

    private WheelSectorData? GetOuterData(int index, int totalSectors)
    {
        // 16格模式时，数据数组仍然是8个，奇数索引映射到相邻格子
        int dataIndex = _outerRing16Mode ? index / 2 : index;
        return dataIndex >= 0 && dataIndex < OuterData.Length ? OuterData[dataIndex] : null;
    }
}
```

---

## Step 1.5  透明宿主窗口（WheelWindow.xaml）

```xml
<!-- WheelWindow.xaml -->
<Window x:Class="WheelMenu.Windows.WheelWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:r="clr-namespace:WheelMenu.Renderer"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        IsHitTestVisible="True"
        WindowState="Maximized">
    <Canvas x:Name="RootCanvas">
        <r:WheelCanvas x:Name="WheelCanvas"/>
    </Canvas>
</Window>
```

```csharp
// WheelWindow.xaml.cs
namespace WheelMenu.Windows;

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

public partial class WheelWindow : Window
{
    // ===== Win32 P/Invoke：设置鼠标穿透 =====
    private const int GWL_EXSTYLE      = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED    = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private IntPtr _hwnd;

    public WheelWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        // 初始状态：全屏透明穿透（等待显示时再取消穿透）
        EnableMousePassThrough(true);
    }

    /// <summary>
    /// true = 鼠标穿透整个窗口（圆盘隐藏时）
    /// false = 正常响应鼠标（圆盘显示时）
    /// </summary>
    public void EnableMousePassThrough(bool passThrough)
    {
        int style = GetWindowLong(_hwnd, GWL_EXSTYLE);
        if (passThrough)
            style |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE;
        else
            style &= ~WS_EX_TRANSPARENT;
        SetWindowLong(_hwnd, GWL_EXSTYLE, style);
    }

    /// <summary>
    /// 显示圆盘：设置圆心位置，取消鼠标穿透
    /// </summary>
    public void ShowWheel(Point screenCenter)
    {
        WheelCanvas.SetCenter(screenCenter);
        EnableMousePassThrough(false);
        Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 隐藏圆盘：恢复鼠标穿透
    /// </summary>
    public void HideWheel()
    {
        EnableMousePassThrough(true);
        Visibility = Visibility.Hidden;
    }
}
```

> ⚠️ **穿透策略说明**：
> - 圆盘**隐藏时**：`WS_EX_TRANSPARENT` 使窗口对鼠标完全透明，下层应用正常使用
> - 圆盘**显示时**：移除 `WS_EX_TRANSPARENT`，窗口正常接收鼠标事件
> - `WS_EX_NOACTIVATE` 始终保留：防止触发轮盘时当前应用失去焦点

---

## Step 1.6  弹出 / 关闭动画（WheelAnimator.cs）

使用 WPF `Storyboard` + `DoubleAnimation` 实现，无需手动帧计时器。

```csharp
// WheelAnimator.cs
namespace WheelMenu.Renderer;

using System.Windows;
using System.Windows.Media.Animation;

public class WheelAnimator
{
    private readonly WheelCanvas _canvas;
    private readonly WheelWindow _window;   // 需要 window 引用以控制 ScaleTransform

    // WheelCanvas 的 RenderTransform 必须预先设置为 ScaleTransform + 居中 TransformOrigin
    // 在 WheelWindow.xaml 中: RenderTransformOrigin="0.5,0.5"

    public WheelAnimator(WheelCanvas canvas, WheelWindow window)
    {
        _canvas = canvas;
        _window = window;
    }

    public void Open(Point screenCenter, Action? onCompleted = null)
    {
        _window.ShowWheel(screenCenter);

        var scaleX  = new DoubleAnimation(
            WheelConstants.AnimOpenScaleFrom, 1.0,
            WheelConstants.AnimOpenDuration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var scaleY  = scaleX.Clone();
        var opacity = new DoubleAnimation(0.0, 1.0, WheelConstants.AnimOpenDuration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

        if (onCompleted != null)
            opacity.Completed += (_, _) => onCompleted();

        var sb = new Storyboard();
        // _canvas 需要有 ScaleTransform 作为 RenderTransform
        Storyboard.SetTarget(scaleX,  _canvas);
        Storyboard.SetTarget(scaleY,  _canvas);
        Storyboard.SetTarget(opacity, _canvas);
        Storyboard.SetTargetProperty(scaleX,
            new PropertyPath("RenderTransform.ScaleX"));
        Storyboard.SetTargetProperty(scaleY,
            new PropertyPath("RenderTransform.ScaleY"));
        Storyboard.SetTargetProperty(opacity,
            new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(scaleX);
        sb.Children.Add(scaleY);
        sb.Children.Add(opacity);
        sb.Begin();
    }

    public void Close(Action? onCompleted = null)
    {
        var scaleX  = new DoubleAnimation(
            1.0, WheelConstants.AnimCloseScaleTo,
            WheelConstants.AnimCloseDuration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        var scaleY  = scaleX.Clone();
        var opacity = new DoubleAnimation(1.0, 0.0, WheelConstants.AnimCloseDuration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };

        opacity.Completed += (_, _) =>
        {
            _window.HideWheel();
            onCompleted?.Invoke();
        };

        var sb = new Storyboard();
        Storyboard.SetTarget(scaleX,  _canvas);
        Storyboard.SetTarget(scaleY,  _canvas);
        Storyboard.SetTarget(opacity, _canvas);
        Storyboard.SetTargetProperty(scaleX,
            new PropertyPath("RenderTransform.ScaleX"));
        Storyboard.SetTargetProperty(scaleY,
            new PropertyPath("RenderTransform.ScaleY"));
        Storyboard.SetTargetProperty(opacity,
            new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(scaleX);
        sb.Children.Add(scaleY);
        sb.Children.Add(opacity);
        sb.Begin();
    }
}
```

`WheelCanvas` 在 XAML 中需要预设 `ScaleTransform`：

```xml
<r:WheelCanvas x:Name="WheelCanvas"
               RenderTransformOrigin="0.5,0.5">
    <r:WheelCanvas.RenderTransform>
        <ScaleTransform ScaleX="1" ScaleY="1"/>
    </r:WheelCanvas.RenderTransform>
</r:WheelCanvas>
```

---

## Step 1.7  屏幕边缘适配与多显示器

```csharp
// ScreenHelper.cs（放在 Renderer/ 或单独 Helpers/ 目录均可）
namespace WheelMenu.Renderer;

using System.Windows;
using System.Windows.Forms;   // 需要引用 System.Windows.Forms

public static class ScreenHelper
{
    /// <summary>
    /// 获取包含指定屏幕坐标的显示器工作区域（WPF 逻辑像素）。
    /// </summary>
    public static Rect GetWorkAreaContaining(Point screenPt)
    {
        // 转为物理像素（WPF 逻辑坐标需要换算）
        var screen = Screen.FromPoint(
            new System.Drawing.Point((int)screenPt.X, (int)screenPt.Y));
        var wa = screen.WorkingArea;
        // Screen.WorkingArea 是物理像素，需换算为逻辑像素
        // 通过 PresentationSource 获取 DPI
        double dpiX = 96.0, dpiY = 96.0;
        var src = PresentationSource.FromVisual(
            System.Windows.Application.Current.MainWindow);
        if (src?.CompositionTarget != null)
        {
            dpiX = 96.0 * src.CompositionTarget.TransformToDevice.M11;
            dpiY = 96.0 * src.CompositionTarget.TransformToDevice.M22;
        }
        return new Rect(
            wa.Left   / dpiX * 96,
            wa.Top    / dpiY * 96,
            wa.Width  / dpiX * 96,
            wa.Height / dpiY * 96);
    }

    /// <summary>
    /// 计算圆盘实际显示圆心（考虑屏幕边缘限制）。
    /// </summary>
    public static (Point WheelCenter, Point? MoveCursorTo) CalculateCenter(
        Point mousePos,
        bool constrainToScreen,
        bool autoMoveCursor)
    {
        if (!constrainToScreen)
            return (mousePos, null);

        var   wa = GetWorkAreaContaining(mousePos);
        var   c  = WheelGeometry.CalculateWheelCenter(
            mousePos.X, mousePos.Y, wa, constrainToScreen: true);

        Point? moveTo = autoMoveCursor ? c : null;
        return (c, moveTo);
    }
}
```

---

## Step 1.8  Phase 1 测试窗口

```csharp
// TestWindow.xaml.cs — Phase 1 独立测试，不包含任何 Phase 3 逻辑
namespace WheelMenu;

using System.Windows;
using System.Windows.Input;
using WheelMenu.Renderer;
using WheelMenu.Windows;

public partial class TestWindow : Window
{
    /*
     * 键盘操作：
     *   Space      → 在屏幕中央弹出圆盘
     *   Escape     → 关闭圆盘
     *   1~8        → 高亮内圈对应格子（1=N, 2=NE, ..., 8=NW）
     *   Q + 1~8    → 高亮外圈对应格子
     *   F1         → 切换 hideLabelWhenIcon（图标隐文字模式）
     *   F2         → 切换外圈 16 格模式
     *   F3         → 移到屏幕右上角测试边缘适配
     *   F4         → 移到屏幕左下角测试边缘适配
     */

    private readonly WheelWindow  _wheel;
    private readonly WheelAnimator _animator;
    private bool _outerMode    = false;
    private bool _hideLabel    = true;
    private bool _outer16Mode  = false;

    public TestWindow()
    {
        InitializeComponent();
        _wheel    = new WheelWindow();
        _wheel.Show();
        _animator = new WheelAnimator(_wheel.WheelCanvas, _wheel);
        KeyDown  += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var canvas = _wheel.WheelCanvas;

        switch (e.Key)
        {
            case Key.Space:
                var center = new Point(
                    SystemParameters.PrimaryScreenWidth  / 2,
                    SystemParameters.PrimaryScreenHeight / 2);
                _animator.Open(center);
                break;

            case Key.Escape:
                _animator.Close();
                break;

            case Key.D1: case Key.D2: case Key.D3: case Key.D4:
            case Key.D5: case Key.D6: case Key.D7: case Key.D8:
                int idx = e.Key - Key.D1;
                canvas.SetHighlight(_outerMode ? "outer" : "inner", idx);
                break;

            case Key.Q:
                _outerMode = !_outerMode;
                Title = _outerMode ? "外圈模式（Q切换）" : "内圈模式（Q切换）";
                break;

            case Key.F1:
                _hideLabel = !_hideLabel;
                canvas.SetDisplayOptions(_hideLabel, _outer16Mode);
                break;

            case Key.F2:
                _outer16Mode = !_outer16Mode;
                canvas.SetDisplayOptions(_hideLabel, _outer16Mode);
                break;

            case Key.F3:
                _animator.Open(new Point(
                    SystemParameters.PrimaryScreenWidth - 10,
                    10)); // 右上角边缘测试
                break;

            case Key.F4:
                _animator.Open(new Point(
                    10,
                    SystemParameters.PrimaryScreenHeight - 10)); // 左下角边缘测试
                break;
        }
    }
}
```

---

## Step 1.9  Phase 1 验收清单

- [ ] 项目可正常编译，无错误（`dotnet build`）
- [ ] TestWindow 运行后按 Space 在屏幕中央弹出圆盘
- [ ] 内圈 8 格均匀分布，分隔线精确到圆心（r=20）
- [ ] 外圈 8 格与内圈方向对齐
- [ ] F2 开启 16 格后，外圈分割为 16 个扇区，次分隔线可见
- [ ] 假数据图标和文字正确显示在各格子内（不溢出扇形边界）
- [ ] 长名称（"很长的动作名称测试溢出"）显示省略号而非溢出
- [ ] 数字键高亮内圈格子，Q 切换后高亮外圈格子，颜色变化明显
- [ ] 悬停外圈（Q 模式 + 数字键）时，圆心死区显示对应动作名称
- [ ] F1 切换后，有图标的格子隐藏文字，无图标格子不受影响
- [ ] Space 触发弹出动画流畅（约 120ms），无卡顿、无闪烁
- [ ] Escape 触发关闭动画流畅（约 80ms），关闭后窗口事件穿透
- [ ] F3/F4 触发后，圆盘完整显示在屏幕内（不超出边界）
- [ ] 圆盘外区域正常点击（鼠标事件穿透到桌面）
- [ ] DPI 125% / 150% 环境下圆盘比例正确，无模糊

**全部勾选后，Phase 1 完成，可进入 Phase 2 设置界面开发。**

---

## Phase 3 接口约定（提前锁定，不可随意修改）

Phase 1 结束后，以下公开方法签名已固定，Phase 3 直接调用：

```csharp
// WheelCanvas 公开接口
canvas.SetCenter(Point screenCenter);
canvas.SetHighlight(string ring, int sectorIndex);   // ring: "inner"|"outer"|"extended"
canvas.ClearHighlight();
canvas.SetDisplayOptions(bool hideLabelWhenIcon, bool outerRing16);
canvas.InnerData = WheelSectorData[];                // Phase 3 替换假数据
canvas.OuterData = WheelSectorData[];

// WheelAnimator 公开接口
animator.Open(Point screenCenter, Action? onCompleted = null);
animator.Close(Action? onCompleted = null);

// WheelWindow 公开接口
window.ShowWheel(Point screenCenter);
window.HideWheel();
window.EnableMousePassThrough(bool passThrough);
```

---

*下一份：`Phase3_功能逻辑_分步SOP.md`*
*技术栈全系 .NET 8.0 + WPF，P/Invoke 调用 Win32 API 实现鼠标钩子和窗口穿透。*
