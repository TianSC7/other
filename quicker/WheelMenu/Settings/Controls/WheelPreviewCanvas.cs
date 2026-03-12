using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WheelMenu.Renderer;
using WheelMenu.Settings.Models;

namespace WheelMenu.Settings.Controls;

public class WheelPreviewCanvas : FrameworkElement
{
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
    public SceneConfig? GlobalScene { get => (SceneConfig?)GetValue(GlobalSceneProperty); set => SetValue(GlobalSceneProperty, value); }
    public bool IsGlobal { get => (bool)GetValue(IsGlobalProperty); set => SetValue(IsGlobalProperty, value); }
    public bool OuterRing16 { get => (bool)GetValue(OuterRing16Property); set => SetValue(OuterRing16Property, value); }

    private string _selectedRing = string.Empty;
    private int _selectedSector = -1;
    public string ActiveRing { get; set; } = "inner";

    public event EventHandler<(string Ring, int Sector, SectorActionConfig? Action)>? SectorClicked;
    public event EventHandler<(string Ring, int Sector, SectorActionConfig? Action)>? SectorRightClicked;

    private System.Windows.Point Center => new(ActualWidth / 2, ActualHeight / 2);

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 1 || ActualHeight < 1) return;
        var cx = Center.X;
        var cy = Center.Y;

        double scale = Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter;

        double deadR = WheelConstants.DeadZoneRadius * scale;
        double innerR = WheelConstants.InnerRingRadius * scale;
        double outerR = WheelConstants.OuterRingRadius * scale;

        DrawBackgroundCircle(dc, cx, cy, outerR);
        DrawRingSectors(dc, cx, cy, deadR, innerR, outerR, scale);
        DrawDividers(dc, cx, cy, deadR, outerR, scale);
        DrawSectorLabels(dc, cx, cy, deadR, innerR, outerR, scale);
        DrawSelectedBorder(dc, cx, cy, deadR, innerR, outerR, scale);
    }

    private void DrawBackgroundCircle(DrawingContext dc, double cx, double cy, double r)
    {
        dc.DrawEllipse(
            new SolidColorBrush(System.Windows.Media.Color.FromArgb(240, 255, 255, 255)),
            new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220)), 1),
            new System.Windows.Point(cx, cy), r, r);
    }

    private void DrawRingSectors(DrawingContext dc, double cx, double cy,
        double deadR, double innerR, double outerR, double scale)
    {
        int sectors = OuterRing16 ? 16 : 8;
        string ring = ActiveRing;
        double rIn = ring == "inner" ? deadR : innerR;
        double rOut = ring == "inner" ? innerR : outerR;
        int count = ring == "outer" && OuterRing16 ? 16 : 8;
        double step = 360.0 / count;

        var directions8 = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        for (int i = 0; i < count; i++)
        {
            // 修复：应用旋转角度，确保渲染与点击检测一致
            double startAngle = GetSectorStartAngle(i, count);
            double endAngle = startAngle + step;
            var geo = WheelGeometry.CreateSectorRing(
                cx, cy, rIn, rOut, startAngle, endAngle);

            string dir = ring == "outer" && OuterRing16
                ? $"outer_{i}"
                : directions8[i % 8];

            var color = GetSectorFillColor(ring, dir, i);
            bool sel = _selectedRing == ring && _selectedSector == i;
            dc.DrawGeometry(new SolidColorBrush(
                sel ? System.Windows.Media.Color.FromArgb(80, 25, 118, 210) : color), null, geo);

            // 调试：在扇区中心显示编号，方便调试点击反馈不一致的问题
            // 计算扇区中心角度（转换为WPF角度：从12点钟方向开始，顺时针）
            double centerAngle = startAngle + step / 2.0;
            double wpfAngle = centerAngle - 90.0; // 转换为WPF角度

            // 计算中心位置（使用扇区环的中间半径）
            double textRadius = (rIn + rOut) / 2.0;
            double centerX = cx + textRadius * Math.Cos(wpfAngle * Math.PI / 180.0);
            double centerY = cy + textRadius * Math.Sin(wpfAngle * Math.PI / 180.0);

            // 绘制编号
            var formattedText = new FormattedText(i.ToString(),
                System.Globalization.CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                14 * scale,
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            formattedText.TextAlignment = TextAlignment.Center;
            dc.DrawText(formattedText,
                new System.Windows.Point(centerX - formattedText.Width / 2,
                          centerY - formattedText.Height / 2));
        }
    }

    private System.Windows.Media.Color GetSectorFillColor(string ring, string dir, int sectorIdx)
    {
        var directions8 = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        string d = sectorIdx < 8 ? directions8[sectorIdx] : directions8[sectorIdx % 8];

        var globalRing = ring == "inner" ? GlobalScene?.InnerRing
                       : ring == "outer" ? GlobalScene?.OuterRing
                       : GlobalScene?.ExtendedRing;
        bool globalHas = globalRing?.ContainsKey(d) == true
                      && globalRing[d] != null
                      && globalRing[d]!.Type != ActionType.None;

        if (!IsGlobal)
        {
            var appRing = ring == "inner" ? CurrentScene?.InnerRing
                        : ring == "outer" ? CurrentScene?.OuterRing
                        : CurrentScene?.ExtendedRing;
            bool appHas = appRing?.ContainsKey(d) == true && appRing[d] != null;
            if (appHas) return System.Windows.Media.Color.FromRgb(227, 242, 253);
        }

        if (!globalHas) return System.Windows.Media.Color.FromArgb(0, 255, 255, 255);
        return ring == "inner"
            ? System.Windows.Media.Color.FromRgb(255, 253, 231)
            : System.Windows.Media.Color.FromRgb(255, 249, 196);
    }

    private void DrawDividers(DrawingContext dc, double cx, double cy, double deadR, double outerR, double scale)
    {
        // 设置界面轮盘不旋转
        double rotation = 0.0; // 不旋转
        double innerR = WheelConstants.InnerRingRadius * scale;
        
        // ===== 环1分割线：从deadR到innerR =====
        var penInner = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0)), 1);
        penInner.Freeze();
        for (int i = 0; i < 8; i++)
        {
            double angle = (i * 45.0 + rotation) % 360.0;
            double wpfAngle = angle - 90.0; // 转换为WPF角度
            var p1 = WheelGeometry.PolarToPoint(cx, cy, deadR, wpfAngle);
            var p2 = WheelGeometry.PolarToPoint(cx, cy, innerR, wpfAngle);
            dc.DrawLine(penInner, p1, p2);
        }
        
        // ===== 环2分割线 =====
        if (OuterRing16)
        {
            // 16模式：16条分割线
            var pen16 = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0)), 1);
            pen16.Freeze();
            for (int i = 0; i < 16; i++)
            {
                double angle = (i * 22.5) % 360.0;
                double wpfAngle = angle - 90.0;
                var p1 = WheelGeometry.PolarToPoint(cx, cy, innerR, wpfAngle);
                var p2 = WheelGeometry.PolarToPoint(cx, cy, outerR, wpfAngle);
                dc.DrawLine(pen16, p1, p2);
            }
        }
        else
        {
            // 8模式：8条分割线
            var penOuter = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0)), 1);
            penOuter.Freeze();
            for (int i = 0; i < 8; i++)
            {
                double angle = (i * 45.0 + rotation) % 360.0;
                double wpfAngle = angle - 90.0;
                var p1 = WheelGeometry.PolarToPoint(cx, cy, innerR, wpfAngle);
                var p2 = WheelGeometry.PolarToPoint(cx, cy, outerR, wpfAngle);
                dc.DrawLine(penOuter, p1, p2);
            }
        }
        
        // ===== 圆形边框 =====
        var penRing = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 0, 0, 0)), 1);
        dc.DrawEllipse(null, penRing, new System.Windows.Point(cx, cy), deadR, deadR);
        dc.DrawEllipse(null, penRing, new System.Windows.Point(cx, cy), innerR, innerR);
        dc.DrawEllipse(null, penRing, new System.Windows.Point(cx, cy), outerR, outerR);
    }

    private void DrawSectorLabels(DrawingContext dc, double cx, double cy,
        double deadR, double innerR, double outerR, double scale)
    {
        // 使用序号显示，不再使用N/NE等方向名
        // 环1: 1-8, 环2: 9-16(8模式)或9-32(16模式), 环3: 33-48
        double step = 360.0 / 8;
        double rotation = 0.0; // 设置界面不旋转

        // 绘制环1标签 (1-8)
        for (int i = 0; i < 8; i++)
        {
            string label = (i + 1).ToString(); // 序号1-8
            double angle = (i * step + rotation) % 360.0;
            var pt = WheelGeometry.SectorCenterPoint(cx, cy, deadR, innerR, angle);

            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"),
                10.0 * scale,
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            { MaxTextWidth = 30 * scale, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };

            dc.DrawText(ft, new System.Windows.Point(pt.X - ft.Width / 2, pt.Y - ft.Height / 2));
        }

        // 绘制环2标签 (9-16 或 9-32)
        int ring2Count = OuterRing16 ? 16 : 8;
        double step2 = 360.0 / ring2Count;
        double angleOffset = 0.0; // 设置界面不旋转
        
        for (int i = 0; i < ring2Count; i++)
        {
            string label = (i + 9).ToString(); // 序号9-24(16模式)或9-16(8模式)
            double angle = (i * step2 + angleOffset) % 360.0;
            var pt = WheelGeometry.SectorCenterPoint(cx, cy, innerR, outerR, angle);

            var ft = new FormattedText(label,
                System.Globalization.CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"),
                9.0 * scale,
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            { MaxTextWidth = 30 * scale, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };

            dc.DrawText(ft, new System.Windows.Point(pt.X - ft.Width / 2, pt.Y - ft.Height / 2));
        }
    }

    /// <summary>
    /// 计算扇区的起始角度（统一应用旋转角度）
    /// </summary>
    /// <param name="sectorIndex">扇区索引</param>
    /// <param name="totalSectors">总扇区数</param>
    /// <returns>起始角度（度）</returns>
    private double GetSectorStartAngle(int sectorIndex, int totalSectors)
    {
        double step = 360.0 / totalSectors;
        double rotation = 0.0; // 设置界面不旋转
        return sectorIndex * step - step / 2.0 + rotation;
    }

    private void DrawSelectedBorder(DrawingContext dc, double cx, double cy,
        double deadR, double innerR, double outerR, double scale)
    {
        if (_selectedSector < 0) return;
        // 修复：支持16模式并应用旋转角度
        int count = _selectedRing == "outer" && OuterRing16 ? 16 : 8;
        double startAngle = GetSectorStartAngle(_selectedSector, count);
        double endAngle = startAngle + 360.0 / count;
        double rIn = _selectedRing == "inner" ? deadR : innerR;
        double rOut = _selectedRing == "inner" ? innerR : outerR;
        var geo = WheelGeometry.CreateSectorRing(cx, cy, rIn, rOut, startAngle, endAngle);
        var pen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 118, 210)), 2.0);
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

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        HitSector(pos, out var ring, out var sector);
        if (sector >= 0)
        {
            _selectedRing = ring;
            _selectedSector = sector;
            InvalidateVisual();
            // 修复：计算正确的方向字符串
            string dir;
            if (ring == "outer" && OuterRing16)
            {
                dir = $"outer_{sector}";
            }
            else
            {
                var directions8 = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                dir = directions8[sector % 8];
            }
            SectorClicked?.Invoke(this, (ring, sector, GetResolvedAction(ring, dir)));
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        HitSector(pos, out var ring, out var sector);
        if (sector >= 0)
            SectorRightClicked?.Invoke(this, (ring, sector, null));
    }

    private void HitSector(System.Windows.Point pos, out string ring, out int sector)
    {
        double scale = Math.Min(ActualWidth, ActualHeight) / WheelConstants.WheelDiameter;
        var (r, s) = WheelGeometry.HitTest(
            Center.X, Center.Y, pos.X, pos.Y,
            OuterRing16 && ActiveRing == "outer" ? 16 : 8);
        ring = r == ActiveRing ? r : string.Empty;
        sector = ring == ActiveRing ? s : -1;
    }
}
