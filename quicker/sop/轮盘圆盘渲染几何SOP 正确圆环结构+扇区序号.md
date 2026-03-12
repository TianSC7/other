# 轮盘圆盘渲染几何 SOP（.NET 8.0 / WPF）
## 正确结构：单圆 + 圆环分层 + 透底分割线 + 扇区序号

> **本文档纠正之前所有 SOP 中错误的几何实现方式。**
> 旧方式：画三个独立圆形叠加 → 错误，圆环边界不透底
> 正确方式：画一个大圆 → 用放射分割线切割 → 圆环边界只是**逻辑半径**，不画实线圆

---

## 一、几何结构定义

### 1.1 半径分层

```
圆心 (cx, cy)
│
├─ R0  = 0        圆心点
├─ R50 = 50px     圆心死区外边界（触发区分界）
├─ R110 = 110px   圆环1（内圈）外边界
├─ R220 = 220px   圆环2（外圈）外边界
└─ R400 = 400px   圆环3（扩展圈）外边界 / 整个大圆半径
```

### 1.2 区域划分

```
R0  ~ R50   → 圆心死区（不响应，显示圆心内容）
R50 ~ R110  → 圆环1（内圈），8个扇区，序号 1-8
R110 ~ R220 → 圆环2（外圈），8或16个扇区，序号 9-16（8格）或 9-24（16格）
R220 ~ R400 → 圆环3（扩展圈），8个扇区，序号 17-24（8格时）
R400+       → 扩展圈逻辑区，不渲染
```

### 1.3 扇区序号分配（8格模式）

```
         1(N)
    8(NW)   2(NE)
  7(W)         3(E)
    6(SW)   4(SE)
         5(S)

内圈扇区编号：1~8（顺时针，从正上方开始）
外圈扇区编号：9~16（同方向，顺时针）
扩展圈扇区编号：17~24（同方向，顺时针）

完整序号对照表（8格模式）：
┌──────┬──────┬──────┬──────────┐
│ 方向 │ 内圈 │ 外圈 │ 扩展圈   │
├──────┼──────┼──────┼──────────┤
│  N   │  1   │  9   │  17      │
│  NE  │  2   │  10  │  18      │
│  E   │  3   │  11  │  19      │
│  SE  │  4   │  12  │  20      │
│  S   │  5   │  13  │  21      │
│  SW  │  6   │  14  │  22      │
│  W   │  7   │  15  │  23      │
│  NW  │  8   │  16  │  24      │
└──────┴──────┴──────┴──────────┘

16格外圈模式时，外圈编号 9~24，扩展圈编号 25~32。
```

### 1.4 关键几何原则

```
✅ 正确：
  画一个半径 R400 的实心大圆（背景）
  画 8 条从圆心出发到 R400 的放射线（分割线，透底可见）
  圆环边界（R50/R110/R220）只是逻辑判断用，不画实线圆

❌ 错误（旧方式）：
  画三个独立的圆形叠加
  每个圆自带边界线
  → 圆环之间会有多余的圆形边界线，不透底
```

---

## 二、WheelConstants.cs（修正后）

```csharp
// Renderer/WheelConstants.cs
namespace WheelMenu.Renderer;

public static class WheelConstants
{
    // ── 半径定义（WPF 逻辑像素，DPI 无关）──
    public const double R_DEAD      =  50.0;   // 圆心死区外边界
    public const double R_RING1     = 110.0;   // 圆环1（内圈）外边界
    public const double R_RING2     = 220.0;   // 圆环2（外圈）外边界
    public const double R_RING3     = 400.0;   // 圆环3（扩展圈）外边界 = 大圆半径

    // ── 整体尺寸 ──
    public const double WheelRadius   = R_RING3;           // 大圆半径
    public const double WheelDiameter = R_RING3 * 2;       // 大圆直径 = 800px

    // ── 扇区数量 ──
    public const int SectorsInner    = 8;    // 内圈固定8格
    public const int SectorsOuter8  = 8;    // 外圈8格模式
    public const int SectorsOuter16 = 16;   // 外圈16格模式
    public const int SectorsExt     = 8;    // 扩展圈固定8格

    // ── 方向名称（顺时针从正上方开始）──
    public static readonly string[] Directions8 =
        { "N","NE","E","SE","S","SW","W","NW" };
    public static readonly string[] Directions16 =
        { "N","NNE","NE","ENE","E","ESE","SE","SSE",
          "S","SSW","SW","WSW","W","WNW","NW","NNW" };

    // ── 扇区序号（1-based）──
    // GetSectorIndex(ring, directionIndex) → 1-based 全局序号
    public static int GetSectorNumber(string ring, int dirIndex, bool outer16 = false)
    {
        return ring switch
        {
            "ring1" => dirIndex + 1,                              //  1~8
            "ring2" => (outer16 ? 8 : 8) + dirIndex + 1,         //  9~16 或 9~24
            "ring3" => (outer16 ? 16 : 16) + dirIndex + 1,       // 17~24 或 25~32
            _       => 0
        };
    }

    // ── 颜色定义 ──
    public static class Colors
    {
        // 大圆背景
        public static readonly System.Windows.Media.Color WheelBackground =
            System.Windows.Media.Color.FromArgb(220, 40, 40, 40);   // 深灰半透明

        // 圆环背景（各圈微妙区分）
        public static readonly System.Windows.Media.Color Ring1Fill =
            System.Windows.Media.Color.FromArgb(200, 55, 55, 55);
        public static readonly System.Windows.Media.Color Ring2Fill =
            System.Windows.Media.Color.FromArgb(180, 48, 48, 48);
        public static readonly System.Windows.Media.Color Ring3Fill =
            System.Windows.Media.Color.FromArgb(150, 38, 38, 38);

        // 悬停/选中
        public static readonly System.Windows.Media.Color Hover =
            System.Windows.Media.Color.FromArgb(160, 80, 120, 200);
        public static readonly System.Windows.Media.Color Selected =
            System.Windows.Media.Color.FromArgb(220, 25, 118, 210);

        // 分割线
        public static readonly System.Windows.Media.Color Divider =
            System.Windows.Media.Color.FromArgb(60, 255, 255, 255);

        // 扇区序号文字
        public static readonly System.Windows.Media.Color SectorNumber =
            System.Windows.Media.Color.FromArgb(80, 255, 255, 255);  // 极淡，水印效果

        // 动作名称
        public static readonly System.Windows.Media.Color LabelText =
            System.Windows.Media.Color.FromRgb(240, 240, 240);

        // 死区背景
        public static readonly System.Windows.Media.Color DeadZone =
            System.Windows.Media.Color.FromArgb(230, 30, 30, 30);
    }

    // ── 字体 ──
    public const double FontSizeLabel  = 11.0;   // 动作名
    public const double FontSizeNumber = 9.0;    // 扇区序号（水印）
    public const string FontFamily     = "Microsoft YaHei UI";

    // ── 分割线宽度 ──
    public const double DividerThickness = 1.5;
}
```

---

## 三、WheelGeometry.cs（修正后）

```csharp
// Renderer/WheelGeometry.cs
namespace WheelMenu.Renderer;

using System.Windows;
using System.Windows.Media;

public static class WheelGeometry
{
    // ══════════════════════════════════════
    // 核心：创建单个扇形（圆环切片）
    // 使用 PathGeometry + ArcSegment，不用圆叠加
    // ══════════════════════════════════════

    /// <summary>
    /// 创建一个圆环扇形路径（从 rInner 到 rOuter，从 startAngle 到 endAngle）。
    /// 角度约定：0° = 正上方，顺时针为正。WPF 坐标系中 Y 轴向下。
    /// </summary>
    public static PathGeometry CreateSectorPath(
        double cx, double cy,
        double rInner, double rOuter,
        double startAngleDeg, double endAngleDeg)
    {
        // 转换为 WPF 角度（WPF：0°=正右，顺时针）
        // 我们的约定：0°=正上方，顺时针
        // 转换：wpfAngle = ourAngle - 90°
        double startRad = (startAngleDeg - 90.0) * Math.PI / 180.0;
        double endRad   = (endAngleDeg   - 90.0) * Math.PI / 180.0;

        // 四个角点
        Point outerStart = new(cx + rOuter * Math.Cos(startRad),
                               cy + rOuter * Math.Sin(startRad));
        Point outerEnd   = new(cx + rOuter * Math.Cos(endRad),
                               cy + rOuter * Math.Sin(endRad));
        Point innerEnd   = new(cx + rInner * Math.Cos(endRad),
                               cy + rInner * Math.Sin(endRad));
        Point innerStart = new(cx + rInner * Math.Cos(startRad),
                               cy + rInner * Math.Sin(startRad));

        double sweepAngle = endAngleDeg - startAngleDeg;
        bool   isLarge    = sweepAngle > 180.0;

        var figure = new PathFigure
        {
            StartPoint = outerStart,
            IsClosed   = true
        };

        // 外弧（从 outerStart 到 outerEnd，顺时针）
        figure.Segments.Add(new ArcSegment(
            outerEnd,
            new Size(rOuter, rOuter),
            0, isLarge,
            SweepDirection.Clockwise,
            true));

        // 内边连线（从 outerEnd 到 innerEnd）
        figure.Segments.Add(new LineSegment(innerEnd, true));

        // 内弧（从 innerEnd 到 innerStart，逆时针）
        figure.Segments.Add(new ArcSegment(
            innerStart,
            new Size(rInner, rInner),
            0, isLarge,
            SweepDirection.Counterclockwise,
            true));

        // 关闭（innerStart 到 outerStart 自动）

        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        geo.Freeze();
        return geo;
    }

    /// <summary>
    /// 创建最内层：圆心死区圆形路径（纯圆，r=0 到 rDead）
    /// </summary>
    public static EllipseGeometry CreateDeadZoneCircle(
        double cx, double cy, double rDead)
    {
        var geo = new EllipseGeometry(new Point(cx, cy), rDead, rDead);
        geo.Freeze();
        return geo;
    }

    /// <summary>
    /// 创建大圆背景路径（完整圆，r=0 到 rOuter）
    /// 用于绘制整个轮盘的底色
    /// </summary>
    public static EllipseGeometry CreateFullCircle(
        double cx, double cy, double r)
    {
        var geo = new EllipseGeometry(new Point(cx, cy), r, r);
        geo.Freeze();
        return geo;
    }

    // ══════════════════════════════════════
    // 分割线（放射线，从 rInner 到 rOuter，透底）
    // ══════════════════════════════════════

    /// <summary>
    /// 生成一条放射分割线的两个端点。
    /// angleDeg：0°=正上方，顺时针。
    /// </summary>
    public static (Point From, Point To) GetDividerLine(
        double cx, double cy,
        double rFrom, double rTo,
        double angleDeg)
    {
        double rad = (angleDeg - 90.0) * Math.PI / 180.0;
        return (
            new Point(cx + rFrom * Math.Cos(rad), cy + rFrom * Math.Sin(rad)),
            new Point(cx + rTo   * Math.Cos(rad), cy + rTo   * Math.Sin(rad))
        );
    }

    // ══════════════════════════════════════
    // 扇区中心点（用于放置标签/序号）
    // ══════════════════════════════════════

    /// <summary>
    /// 计算扇区的视觉中心点（圆环中点 × 角度中点）
    /// </summary>
    public static Point GetSectorCenter(
        double cx, double cy,
        double rInner, double rOuter,
        double startAngleDeg, double endAngleDeg)
    {
        double midAngle = (startAngleDeg + endAngleDeg) / 2.0;
        double midR     = (rInner + rOuter) / 2.0;
        double rad      = (midAngle - 90.0) * Math.PI / 180.0;
        return new Point(
            cx + midR * Math.Cos(rad),
            cy + midR * Math.Sin(rad));
    }

    // ══════════════════════════════════════
    // Hit Test（判断鼠标在哪个扇区）
    // ══════════════════════════════════════

    /// <summary>
    /// 输入：鼠标屏幕坐标（物理像素，需先转为逻辑像素）
    /// 输出：(ring, sectorIndex)
    ///   ring = "dead" / "ring1" / "ring2" / "ring3" / "extended"
    ///   sectorIndex = 0-based（0=N，1=NE，...）
    ///   sectorNumber = 1-based 全局序号
    /// </summary>
    public static (string Ring, int SectorIndex, int SectorNumber) HitTest(
        double cx, double cy,
        double mouseX, double mouseY,
        bool outer16Mode = false)
    {
        double dx   = mouseX - cx;
        double dy   = mouseY - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        // 判断圈层
        string ring;
        if      (dist <= WheelConstants.R_DEAD)  ring = "dead";
        else if (dist <= WheelConstants.R_RING1) ring = "ring1";
        else if (dist <= WheelConstants.R_RING2) ring = "ring2";
        else if (dist <= WheelConstants.R_RING3) ring = "ring3";
        else                                     ring = "extended";

        if (ring == "dead") return ("dead", -1, 0);

        // 判断扇区
        // atan2(dx, -dy)：dx=水平，-dy 将 Y 轴翻转（正上方为0）
        double angle = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        if (angle < 0) angle += 360.0;

        int sectors = (ring == "ring2" && outer16Mode) ? 16 : 8;
        double step = 360.0 / sectors;
        int sectorIndex = (int)Math.Floor((angle + step / 2.0) / step) % sectors;

        int sectorNumber = WheelConstants.GetSectorNumber(ring, sectorIndex, outer16Mode);
        return (ring, sectorIndex, sectorNumber);
    }

    // ══════════════════════════════════════
    // 工具
    // ══════════════════════════════════════
    public static Point PolarToPoint(double cx, double cy, double r, double angleDeg)
    {
        double rad = (angleDeg - 90.0) * Math.PI / 180.0;
        return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }
}
```

---

## 四、WheelCanvas.cs（完整重写，正确几何）

```csharp
// Renderer/WheelCanvas.cs
namespace WheelMenu.Renderer;

using System.Globalization;
using System.Windows;
using System.Windows.Media;

public class WheelCanvas : FrameworkElement
{
    // ══════════════════════════════════════
    // 公开接口（Phase 3 调用）
    // ══════════════════════════════════════
    private Point  _center          = new(0, 0);
    private string _highlightRing   = string.Empty;
    private int    _highlightSector = -1;
    private bool   _outer16Mode     = false;
    private bool   _hideLabelWhenIcon = true;

    public WheelSectorData[] Ring1Data { get; set; } =
        Enumerable.Repeat(WheelSectorData.Empty, 8).ToArray();
    public WheelSectorData[] Ring2Data { get; set; } =
        Enumerable.Repeat(WheelSectorData.Empty, 8).ToArray();
    public WheelSectorData[] Ring3Data { get; set; } =
        Enumerable.Repeat(WheelSectorData.Empty, 8).ToArray();

    public void SetCenter(Point screenCenter)
    {
        _center = screenCenter;
        // 控件在全屏 Canvas 中定位，使圆心对齐到 screenCenter
        double d = WheelConstants.WheelDiameter;
        Width  = d;
        Height = d;
        System.Windows.Controls.Canvas.SetLeft(this, screenCenter.X - d / 2);
        System.Windows.Controls.Canvas.SetTop( this, screenCenter.Y - d / 2);
        InvalidateVisual();
    }

    /// <summary>
    /// ring = "ring1" / "ring2" / "ring3"
    /// sectorIndex = 0-based
    /// </summary>
    public void SetHighlight(string ring, int sectorIndex)
    {
        _highlightRing   = ring;
        _highlightSector = sectorIndex;
        InvalidateVisual();
    }

    public void ClearHighlight()
    {
        _highlightRing   = string.Empty;
        _highlightSector = -1;
        InvalidateVisual();
    }

    public void SetDisplayOptions(bool hideLabelWhenIcon, bool outer16Mode)
    {
        _hideLabelWhenIcon = hideLabelWhenIcon;
        _outer16Mode       = outer16Mode;
        InvalidateVisual();
    }

    // ══════════════════════════════════════
    // 渲染（核心）
    // ══════════════════════════════════════
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 1 || ActualHeight < 1) return;

        // 控件内部坐标：圆心始终在控件中央
        double cx = ActualWidth  / 2.0;
        double cy = ActualHeight / 2.0;

        // 缩放比（控件实际尺寸 vs 设计尺寸）
        double scale = Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter;

        double rDead  = WheelConstants.R_DEAD  * scale;
        double rRing1 = WheelConstants.R_RING1 * scale;
        double rRing2 = WheelConstants.R_RING2 * scale;
        double rRing3 = WheelConstants.R_RING3 * scale;

        // ── Step 1：绘制大圆背景（单一大圆，不叠圆）──
        DrawLargeCircleBackground(dc, cx, cy, rRing3);

        // ── Step 2：绘制各圆环扇区（从外到内，内层覆盖外层）──
        DrawRingSectors(dc, cx, cy, rRing2, rRing3, "ring3", 8,      scale);
        DrawRingSectors(dc, cx, cy, rRing1, rRing2, "ring2",
            _outer16Mode ? 16 : 8, scale);
        DrawRingSectors(dc, cx, cy, rDead,  rRing1, "ring1", 8,      scale);

        // ── Step 3：绘制放射分割线（透底，从圆心到 rRing3）──
        // 注意：分割线从 r=0 画到 rRing3，覆盖所有圆环
        // 这样所有圆环共享同一套分割线，线是透底的（不被圆环盖住）
        DrawDividerLines(dc, cx, cy, 0, rRing3, 8, scale);

        // ── Step 4：绘制圆心死区（盖在分割线上方）──
        DrawDeadZone(dc, cx, cy, rDead);

        // ── Step 5：绘制扇区序号水印 ──
        DrawSectorNumbers(dc, cx, cy, rDead, rRing1, rRing2, rRing3, scale);

        // ── Step 6：绘制动作标签/图标 ──
        DrawSectorLabels(dc, cx, cy, rDead, rRing1, rRing2, rRing3, scale);

        // ── Step 7：绘制圆心内容（高亮时显示动作名）──
        DrawCenterContent(dc, cx, cy, rDead, scale);
    }

    // ── 大圆背景 ──
    private static void DrawLargeCircleBackground(
        DrawingContext dc, double cx, double cy, double rRing3)
    {
        var brush = new RadialGradientBrush(
            WheelConstants.Colors.WheelBackground,
            Color.FromArgb(180, 30, 30, 30))
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center         = new Point(0.5, 0.5),
            RadiusX        = 0.5,
            RadiusY        = 0.5
        };
        dc.DrawEllipse(brush, null, new Point(cx, cy), rRing3, rRing3);
    }

    // ── 圆环扇区（单个圆环的所有扇区）──
    private void DrawRingSectors(
        DrawingContext dc, double cx, double cy,
        double rInner, double rOuter,
        string ring, int sectorCount, double scale)
    {
        double step = 360.0 / sectorCount;

        for (int i = 0; i < sectorCount; i++)
        {
            double startAngle = i * step - step / 2.0;
            double endAngle   = startAngle + step;

            bool isHighlight = _highlightRing == ring && _highlightSector == i;

            // 确定填充色（高亮 vs 普通）
            Color fillColor;
            if (isHighlight)
            {
                fillColor = WheelConstants.Colors.Hover;
            }
            else
            {
                // 各圈层微妙不同的底色（可选，也可统一用透明让大圆背景透出）
                fillColor = ring switch
                {
                    "ring1" => WheelConstants.Colors.Ring1Fill,
                    "ring2" => WheelConstants.Colors.Ring2Fill,
                    "ring3" => WheelConstants.Colors.Ring3Fill,
                    _       => WheelConstants.Colors.Ring1Fill
                };
                // 有动作的格子：用稍亮的颜色区分
                var data = GetSectorData(ring, i);
                if (data.HasAction)
                    fillColor = Color.FromArgb(
                        fillColor.A,
                        (byte)Math.Min(fillColor.R + 20, 255),
                        (byte)Math.Min(fillColor.G + 20, 255),
                        (byte)Math.Min(fillColor.B + 20, 255));
            }

            var geo = WheelGeometry.CreateSectorPath(
                cx, cy, rInner, rOuter, startAngle, endAngle);
            dc.DrawGeometry(new SolidColorBrush(fillColor), null, geo);
        }
    }

    // ── 放射分割线（透底关键）──
    private static void DrawDividerLines(
        DrawingContext dc, double cx, double cy,
        double rFrom, double rTo,
        int lineCount, double scale)
    {
        var pen = new Pen(
            new SolidColorBrush(WheelConstants.Colors.Divider),
            WheelConstants.DividerThickness * scale);
        pen.Freeze();

        double step = 360.0 / lineCount;
        for (int i = 0; i < lineCount; i++)
        {
            double angle = i * step;
            var (from, to) = WheelGeometry.GetDividerLine(cx, cy, rFrom, rTo, angle);
            dc.DrawLine(pen, from, to);
        }
    }

    // ── 圆心死区 ──
    private static void DrawDeadZone(
        DrawingContext dc, double cx, double cy, double rDead)
    {
        var brush = new SolidColorBrush(WheelConstants.Colors.DeadZone);
        dc.DrawEllipse(brush, null, new Point(cx, cy), rDead, rDead);
    }

    // ── 扇区序号水印 ──
    private void DrawSectorNumbers(
        DrawingContext dc,
        double cx, double cy,
        double rDead, double rRing1, double rRing2, double rRing3,
        double scale)
    {
        var numBrush = new SolidColorBrush(WheelConstants.Colors.SectorNumber);
        double fontSize = WheelConstants.FontSizeNumber * scale;
        var typeface    = new Typeface(WheelConstants.FontFamily);
        double dpi      = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Ring1：序号 1~8
        DrawNumbersInRing(dc, cx, cy, rDead, rRing1, "ring1", 8,
            1, fontSize, typeface, numBrush, dpi, scale);

        // Ring2：序号 9~16（8格）或 9~24（16格）
        int ring2Count  = _outer16Mode ? 16 : 8;
        int ring2Offset = 9;
        DrawNumbersInRing(dc, cx, cy, rRing1, rRing2, "ring2", ring2Count,
            ring2Offset, fontSize, typeface, numBrush, dpi, scale);

        // Ring3：序号 17~24（8格）或 25~32（16格）
        int ring3Offset = _outer16Mode ? 25 : 17;
        DrawNumbersInRing(dc, cx, cy, rRing2, rRing3, "ring3", 8,
            ring3Offset, fontSize, typeface, numBrush, dpi, scale);
    }

    private static void DrawNumbersInRing(
        DrawingContext dc,
        double cx, double cy,
        double rInner, double rOuter,
        string ring, int count, int startNumber,
        double fontSize, Typeface typeface,
        Brush brush, double dpi, double scale)
    {
        double step = 360.0 / count;
        for (int i = 0; i < count; i++)
        {
            double midAngle = i * step;
            var    center   = WheelGeometry.GetSectorCenter(
                cx, cy, rInner, rOuter,
                midAngle - step / 2.0, midAngle + step / 2.0);

            string numText = (startNumber + i).ToString();
            var ft = new FormattedText(numText, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, fontSize, brush, dpi)
            {
                TextAlignment = TextAlignment.Center
            };

            // 序号显示在扇区的外侧偏上位置（不遮挡动作名称）
            dc.DrawText(ft, new Point(
                center.X - ft.Width  / 2,
                center.Y - ft.Height / 2 - 6 * scale));
        }
    }

    // ── 动作标签/图标 ──
    private void DrawSectorLabels(
        DrawingContext dc,
        double cx, double cy,
        double rDead, double rRing1, double rRing2, double rRing3,
        double scale)
    {
        var labelBrush = new SolidColorBrush(WheelConstants.Colors.LabelText);
        double fontSize = WheelConstants.FontSizeLabel * scale;
        var typeface    = new Typeface(WheelConstants.FontFamily);
        double dpi      = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        DrawLabelsInRing(dc, cx, cy, rDead,  rRing1, Ring1Data, 8,
            fontSize, typeface, labelBrush, dpi, scale);
        DrawLabelsInRing(dc, cx, cy, rRing1, rRing2, Ring2Data,
            _outer16Mode ? 16 : 8,
            fontSize * 0.85, typeface, labelBrush, dpi, scale);
        DrawLabelsInRing(dc, cx, cy, rRing2, rRing3, Ring3Data, 8,
            fontSize * 0.75, typeface, labelBrush, dpi, scale);
    }

    private void DrawLabelsInRing(
        DrawingContext dc,
        double cx, double cy,
        double rInner, double rOuter,
        WheelSectorData[] data, int count,
        double fontSize, Typeface typeface,
        Brush brush, double dpi, double scale)
    {
        double step    = 360.0 / count;
        double maxW    = (rOuter - rInner) * 0.8;  // 标签最大宽度

        for (int i = 0; i < count && i < data.Length; i++)
        {
            var sector = data[i];
            if (!sector.HasAction) continue;

            double midAngle = i * step;
            var    center   = WheelGeometry.GetSectorCenter(
                cx, cy, rInner, rOuter,
                midAngle - step / 2.0, midAngle + step / 2.0);

            // 图标
            if (sector.Icon != null)
            {
                double iconSize = 16 * scale;
                dc.DrawImage(sector.Icon, new Rect(
                    center.X - iconSize / 2,
                    center.Y - iconSize,
                    iconSize, iconSize));

                if (_hideLabelWhenIcon) continue;
            }

            // 文字标签
            if (!string.IsNullOrEmpty(sector.Label))
            {
                var ft = new FormattedText(sector.Label,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface, fontSize, brush, dpi)
                {
                    MaxTextWidth  = maxW,
                    MaxLineCount  = 2,
                    Trimming      = TextTrimming.CharacterEllipsis,
                    TextAlignment = TextAlignment.Center
                };

                double yOffset = sector.Icon != null ? 2 * scale : -ft.Height / 2;
                dc.DrawText(ft, new Point(
                    center.X - ft.Width / 2,
                    center.Y + yOffset));
            }
        }
    }

    // ── 圆心区域内容（高亮时显示动作名）──
    private void DrawCenterContent(
        DrawingContext dc, double cx, double cy, double rDead, double scale)
    {
        if (string.IsNullOrEmpty(_highlightRing) || _highlightSector < 0)
            return;

        var data = GetSectorData(_highlightRing, _highlightSector);
        if (!data.HasAction) return;

        var brush     = new SolidColorBrush(WheelConstants.Colors.LabelText);
        var typeface  = new Typeface(WheelConstants.FontFamily);
        double dpi    = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // 图标
        double yStart = cy - rDead * 0.6;
        if (data.Icon != null)
        {
            double iconSize = 20 * scale;
            dc.DrawImage(data.Icon, new Rect(
                cx - iconSize / 2, yStart, iconSize, iconSize));
            yStart += iconSize + 2 * scale;
        }

        // 名称
        if (!string.IsNullOrEmpty(data.Label))
        {
            var ft = new FormattedText(data.Label,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface, 11 * scale, brush, dpi)
            {
                MaxTextWidth  = rDead * 1.6,
                MaxLineCount  = 2,
                Trimming      = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center
            };
            dc.DrawText(ft, new Point(cx - ft.Width / 2, yStart));
        }
    }

    // ── 辅助：取扇区数据 ──
    private WheelSectorData GetSectorData(string ring, int index)
    {
        var arr = ring switch
        {
            "ring1" => Ring1Data,
            "ring2" => Ring2Data,
            "ring3" => Ring3Data,
            _       => Ring1Data
        };
        return index >= 0 && index < arr.Length ? arr[index] : WheelSectorData.Empty;
    }
}
```

---

## 五、WheelSectorData.cs（修正后）

```csharp
// Renderer/WheelSectorData.cs
namespace WheelMenu.Renderer;

using System.Windows.Media;

public class WheelSectorData
{
    public bool         HasAction { get; set; } = false;
    public string       Label     { get; set; } = string.Empty;
    public ImageSource? Icon      { get; set; } = null;

    /// <summary>1-based 全局扇区序号（用于赋值功能）</summary>
    public int SectorNumber { get; set; } = 0;

    public static WheelSectorData Empty => new() { HasAction = false };
}
```

---

## 六、绘制顺序总览（为什么这样排列）

```
绘制层次（从底到顶）：

Layer 0：大圆背景
  └─ DrawEllipse(r=R400) 一次绘制整个轮盘底色
     ✅ 不画三个圆，只画一个

Layer 1：Ring3 扇区（最外圈，先画在底部）
  └─ 8 个 CreateSectorPath(R220, R400, ...) 扇形

Layer 2：Ring2 扇区
  └─ 8/16 个 CreateSectorPath(R110, R220, ...) 扇形

Layer 3：Ring1 扇区（内圈，盖在外圈上方）
  └─ 8 个 CreateSectorPath(R50, R110, ...) 扇形

Layer 4：放射分割线（从 r=0 到 R400）
  ✅ 画在所有扇区上方 → 分割线穿透所有圆环，视觉上"透底"
  ✅ 不在每个圆环单独画线，而是统一画8条长线覆盖所有层

Layer 5：死区圆（r=R50）
  └─ 盖住分割线在圆心区的部分，保持圆心区域干净

Layer 6：扇区序号水印
  └─ 极淡的数字，位于每个扇区右上角或中上位置

Layer 7：动作标签 / 图标
  └─ 正常亮度的文字+图标

Layer 8：圆心内容（高亮时的动作名/图标）
  └─ 盖在最上层
```

---

## 七、分割线透底的关键代码说明

```
❌ 旧的错误方式：
   foreach(圈层)
     DrawEllipse(圆形) ← 每个圆有自己的边界线
     DrawLines(该圈的分割线)
   → 各圈边界线叠加，有多余圆形轮廓

✅ 正确方式（本 SOP）：
   Step1: DrawEllipse(大圆背景, r=R400)  ← 只画一次背景
   Step2: DrawSectors(ring3)             ← 纯填色，无轮廓线
   Step3: DrawSectors(ring2)             ← 纯填色，无轮廓线
   Step4: DrawSectors(ring1)             ← 纯填色，无轮廓线
   Step5: DrawLines(8条从0到R400的放射线) ← 统一分割线，穿透所有圆环

所有 DrawGeometry 调用：pen 参数为 null！
   dc.DrawGeometry(fillBrush, null, sectorGeo)
                             ^^^^
                             不画边框！

只有分割线用 pen，其他全部用 fill + null pen。
```

---

## 八、扇区序号赋值接口

```csharp
// Phase 3 / 设置界面通过序号赋值的公开接口

// 方式1：通过序号直接设置（设置界面使用）
public void SetSectorByNumber(int sectorNumber, WheelSectorData data)
{
    // 根据序号反推 ring 和 index
    var (ring, index) = SectorNumberToRingIndex(sectorNumber, _outer16Mode);
    var arr = ring switch
    {
        "ring1" => Ring1Data,
        "ring2" => Ring2Data,
        "ring3" => Ring3Data,
        _       => null
    };
    if (arr != null && index < arr.Length)
    {
        data.SectorNumber = sectorNumber;
        arr[index]        = data;
        InvalidateVisual();
    }
}

// 方式2：批量设置（Phase 3 场景切换时使用）
public void SetAllSectors(WheelSectorData[] ring1, WheelSectorData[] ring2,
    WheelSectorData[]? ring3 = null)
{
    Ring1Data = ring1.Take(8).ToArray();
    Ring2Data = ring2.Take(_outer16Mode ? 16 : 8).ToArray();
    if (ring3 != null) Ring3Data = ring3.Take(8).ToArray();
    InvalidateVisual();
}

// 序号反推工具
public static (string Ring, int Index) SectorNumberToRingIndex(
    int sectorNumber, bool outer16 = false)
{
    if (sectorNumber <= 8)
        return ("ring1", sectorNumber - 1);

    int ring2Count = outer16 ? 16 : 8;
    if (sectorNumber <= 8 + ring2Count)
        return ("ring2", sectorNumber - 9);

    return ("ring3", sectorNumber - (8 + ring2Count) - 1);
}
```

---

## 九、验收清单

- [ ] 圆盘只有一个大圆背景（`DrawEllipse` 调用一次，r=R400）
- [ ] 不存在多个圆形边界线叠加（所有 `DrawGeometry` 的 pen 参数为 null）
- [ ] 8条放射分割线从 r=0 画到 r=R400，穿透所有圆环
- [ ] 分割线颜色半透明（`alpha≈60`），轮盘背景色可从分割线处透出
- [ ] Ring1 扇区区域：R50~R110，序号 1~8 显示为极淡水印
- [ ] Ring2 扇区区域：R110~R220，序号 9~16（8格）显示正确
- [ ] Ring3 扇区区域：R220~R400，序号 17~24（8格）显示正确
- [ ] 16格模式下：Ring2 显示 16 个扇区，Ring3 序号改为 25~32
- [ ] 圆心死区（r≤R50）：盖住分割线中心，显示干净的圆心区域
- [ ] 高亮某扇区时：仅该扇区填色改变，分割线/序号不受影响
- [ ] `SetSectorByNumber(3, data)` 正确设置 Ring1 第3格（E方向）
- [ ] `HitTest` 返回正确的 SectorNumber（1-based 全局序号）