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
    private bool   _showExtendedRing = false;  // 环3(R300)默认不可见

    // ===== 数据 =====
    public WheelSectorData[] InnerData { get; set; } = FakeData.InnerRing;
    public WheelSectorData[] OuterData { get; set; } = FakeData.OuterRing;

    // ===== 公开接口（Phase 3 将调用这些）=====

    public void SetCenter(Point center)
    {
        _center = center;
        InvalidateVisual();
    }

    public Point GetCenter() => _center;

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

    public void SetExtendedRingVisible(bool visible)
    {
        _showExtendedRing = visible;
        InvalidateVisual();
    }

    public bool GetExtendedRingVisible() => _showExtendedRing;

    // ===== 核心绘制 =====

    protected override void OnRender(DrawingContext dc)
    {
        double cx = _center.X;
        double cy = _center.Y;

        DrawBackground(dc, cx, cy);
        
        // 环3(R300) - 默认不可见
        if (_showExtendedRing)
        {
            DrawExtendedRing(dc, cx, cy);
        }
        
        DrawOuterRing(dc, cx, cy);  // 环2(R200)
        DrawInnerRing(dc, cx, cy);  // 环1(R100)
        DrawDividers(dc, cx, cy);
        DrawDeadZone(dc, cx, cy);   // 圆1(R20)死区 - 无分割线
        DrawLabels(dc, cx, cy);
        DrawCenterHint(dc, cx, cy);
    }

    // ----- 0. 扩展圈圆环（环3 R300）-----
    private void DrawExtendedRing(DrawingContext dc, double cx, double cy)
    {
        // 创建完整的圆环几何
        var ringGeo = CreateRingGeometry(cx, cy, WheelConstants.OuterRingRadius, WheelConstants.ExtendedRingRadius);
        
        // 绘制圆环背景
        dc.DrawGeometry(new SolidColorBrush(WheelConstants.ColorSectorEmpty), null, ringGeo);
        
        // 如果有高亮的扇区，绘制高亮扇形
        // 与 DiskUI 示例一致：扩展圈分割线角度与内圈对齐
        if (_highlightRing == "extended" && _highlightSector >= 0)
        {
            int sectors = _outerRing16Mode ? WheelConstants.ExtendedSectors : WheelConstants.ExtendedSectors / 2;
            double step = 360.0 / sectors;
            double startAngle = WheelConstants.SectorStartAngle + WheelConstants.SectorRotationAngle; // -67.5度
            double centerAngle = (startAngle + _highlightSector * step + step / 2.0) % 360.0; // 扇区中心角度
            double sectorStart = centerAngle - step / 2.0;
            double sectorEnd = centerAngle + step / 2.0;
            var sectorGeo = WheelGeometry.CreateSectorRing(
                cx, cy,
                WheelConstants.OuterRingRadius,
                WheelConstants.ExtendedRingRadius,
                sectorStart, sectorEnd);
            dc.DrawGeometry(new SolidColorBrush(WheelConstants.ColorSectorHovered), null, sectorGeo);
        }
    }

    // ----- 1. 圆盘整体背景（使用圆环：大圆减小圆）-----
    private void DrawBackground(DrawingContext dc, double cx, double cy)
    {
        var brush = new SolidColorBrush(WheelConstants.ColorBackground);
        // 圆环 = 大圆R200 减去 小圆R20
        var ringGeo = CreateRingGeometry(cx, cy, WheelConstants.DeadZoneRadius, WheelConstants.OuterRingRadius);
        dc.DrawGeometry(brush, null, ringGeo);
    }

    // ----- 2. 外圈圆环（完整圆环，避免分隔线透底）-----
    private void DrawOuterRing(DrawingContext dc, double cx, double cy)
    {
        // 创建完整的圆环几何
        var ringGeo = CreateRingGeometry(cx, cy, WheelConstants.InnerRingRadius, WheelConstants.OuterRingRadius);
        
        // 绘制圆环背景
        dc.DrawGeometry(new SolidColorBrush(WheelConstants.ColorSectorEmpty), null, ringGeo);
        
        // 如果有高亮的扇区，绘制高亮扇形
        // 与 DiskUI 示例一致：外圈分割线角度与内圈对齐
        if (_highlightRing == "outer" && _highlightSector >= 0)
        {
            int sectors = _outerRing16Mode ? WheelConstants.OuterSectors16 : WheelConstants.OuterSectors8;
            double step = 360.0 / sectors;
            double startAngle = WheelConstants.SectorStartAngle + WheelConstants.SectorRotationAngle; // -67.5度
            double centerAngle = (startAngle + _highlightSector * step + step / 2.0) % 360.0; // 扇区中心角度
            double sectorStart = centerAngle - step / 2.0;
            double sectorEnd = centerAngle + step / 2.0;
            var sectorGeo = WheelGeometry.CreateSectorRing(
                cx, cy,
                WheelConstants.InnerRingRadius,
                WheelConstants.OuterRingRadius,
                sectorStart, sectorEnd);
            dc.DrawGeometry(new SolidColorBrush(WheelConstants.ColorSectorHovered), null, sectorGeo);
        }
    }

    // ----- 3. 内圈圆环（完整圆环，避免分隔线透底）-----
    private void DrawInnerRing(DrawingContext dc, double cx, double cy)
    {
        // 创建完整的圆环几何
        var ringGeo = CreateRingGeometry(cx, cy, WheelConstants.DeadZoneRadius, WheelConstants.InnerRingRadius);
        
        // 绘制圆环背景
        dc.DrawGeometry(new SolidColorBrush(WheelConstants.ColorSectorEmpty), null, ringGeo);
        
        // 如果有高亮的扇区，绘制高亮扇形
        // 与 DiskUI 示例一致：起始角度 -90 + 旋转22.5
        if (_highlightRing == "inner" && _highlightSector >= 0)
        {
            double step = 360.0 / WheelConstants.InnerSectors;
            double startAngle = WheelConstants.SectorStartAngle + WheelConstants.SectorRotationAngle; // -67.5度
            double centerAngle = (startAngle + _highlightSector * step + step / 2.0) % 360.0; // 扇区中心角度
            double sectorStart = centerAngle - step / 2.0;
            double sectorEnd = centerAngle + step / 2.0;
            var sectorGeo = WheelGeometry.CreateSectorRing(
                cx, cy,
                WheelConstants.DeadZoneRadius,
                WheelConstants.InnerRingRadius,
                sectorStart, sectorEnd);
            dc.DrawGeometry(new SolidColorBrush(WheelConstants.ColorSectorHovered), null, sectorGeo);
        }
    }

    // ----- 创建完整圆环几何（使用 CombinedGeometry）-----
    private Geometry CreateRingGeometry(double cx, double cy, double rInner, double rOuter)
    {
        // 外圆
        var outerEllipse = new EllipseGeometry(new Point(cx, cy), rOuter, rOuter);
        // 内圆
        var innerEllipse = new EllipseGeometry(new Point(cx, cy), rInner, rInner);
        // 组合：外圆减去内圆 = 圆环
        var ringGeo = new CombinedGeometry(GeometryCombineMode.Exclude, outerEllipse, innerEllipse);
        ringGeo.Freeze();
        return ringGeo;
    }

    // ----- 4. 放射状分隔线 -----
    // 正确几何结构（与 DiskUI 示例一致）：
    // - 圆1(R20): 死区，无分割线
    // - 环1(R100): 始终8个扇区，分割线不随16模式变化
    // - 环2(R200): 8模式8个扇区，16模式16个扇区
    // - 环3(R300): 不可见
    // - 起始角度：-90度（12点钟方向），旋转22.5度
    private void DrawDividers(DrawingContext dc, double cx, double cy)
    {
        // 起始角度 -90度 + 旋转角度 22.5度 = -67.5度（第一分割线位置）
        double startAngle = WheelConstants.SectorStartAngle + WheelConstants.SectorRotationAngle; // -90 + 22.5 = -67.5
        
        // 环2是最大可见半径
        double maxRadius = WheelConstants.OuterRingRadius; // R200

        // ===== 环1始终8条分隔线（从R20到R100）=====
        var penInner = new Pen(new SolidColorBrush(WheelConstants.ColorDivider),
            WheelConstants.DividerThicknessPrimary);
        penInner.Freeze();
        
        for (int i = 0; i < 8; i++)
        {
            // 分割线角度：起始角度 + i * 45°
            double angle = (startAngle + i * 45.0) % 360.0;
            // 环1分割线：从R20到R100
            var innerRing1 = WheelGeometry.PolarToPoint(cx, cy, WheelConstants.DeadZoneRadius, angle);
            var outerRing1 = WheelGeometry.PolarToPoint(cx, cy, WheelConstants.InnerRingRadius, angle);
            dc.DrawLine(penInner, innerRing1, outerRing1);
        }

        // ===== 环2分隔线 =====
        // 环2分割线角度与环1对齐（与 DiskUI 示例一致）
        
        if (_outerRing16Mode)
        {
            // 16格模式：环2有16条分割线
            var pen16 = new Pen(new SolidColorBrush(WheelConstants.ColorDivider),
                WheelConstants.DividerThicknessPrimary);
            pen16.Freeze();

            for (int i = 0; i < 16; i++)
            {
                // 16模式分割线角度：起始角度 + i * 22.5°
                double dividerAngle = (startAngle + i * 22.5) % 360.0;
                  
                // 分割线：从R100到R200（环2）
                var innerMain = WheelGeometry.PolarToPoint(cx, cy, WheelConstants.InnerRingRadius, dividerAngle);
                var outerMain = WheelGeometry.PolarToPoint(cx, cy, maxRadius, dividerAngle);
                dc.DrawLine(pen16, innerMain, outerMain);
            }
        }
        else
        {
            // 8格模式：环2有8条分隔线（与环1对齐）
            var penOuter = new Pen(new SolidColorBrush(WheelConstants.ColorDivider),
                WheelConstants.DividerThicknessPrimary);
            penOuter.Freeze();

            for (int i = 0; i < 8; i++)
            {
                // 分割线角度：起始角度 + i * 45°（与环1对齐）
                double angle = (startAngle + i * 45.0) % 360.0;
                // 环2分割线：从R100到R200
                var inner = WheelGeometry.PolarToPoint(cx, cy, WheelConstants.InnerRingRadius, angle);
                var outer = WheelGeometry.PolarToPoint(cx, cy, maxRadius, angle);
                dc.DrawLine(penOuter, inner, outer);
            }
        }

        // 圆形边框（每环一个边界）
        var penCircle = new Pen(new SolidColorBrush(WheelConstants.ColorDivider), 1.0);
        
        // 圆1(R20)死区 - 仅显示边框，无分割线
        dc.DrawEllipse(null, penCircle, new Point(cx, cy),
            WheelConstants.DeadZoneRadius, WheelConstants.DeadZoneRadius);
            
        // 环1(R100)内边界
        dc.DrawEllipse(null, penCircle, new Point(cx, cy),
            WheelConstants.InnerRingRadius, WheelConstants.InnerRingRadius);
            
        // 环2(R200)外边界
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
        // 起始角度 -90度 + 旋转角度 22.5度 = -67.5度
        // 转换为正数：292.5度 (360 - 67.5)
        double startAngle = ((WheelConstants.SectorStartAngle + WheelConstants.SectorRotationAngle) % 360.0 + 360.0) % 360.0;
        
        int innerSectors = WheelConstants.InnerSectors;
        double innerStep = 360.0 / innerSectors;

        // 绘制内圈编号 (1-8)
        for (int i = 0; i < innerSectors; i++)
        {
            // 扇区中心角度 = 起始角度 + i * step + 扇区半宽
            // 扇区半宽 = 45/2 = 22.5度
            double centerAngle = (startAngle + i * innerStep + innerStep / 2.0) % 360.0;
            var    center      = WheelGeometry.SectorCenterPoint(
                cx, cy,
                WheelConstants.DeadZoneRadius,
                WheelConstants.InnerRingRadius,
                centerAngle);
            
            // 绘制编号
            var numberText = MakeFormattedText((i + 1).ToString(), 12.0, 20.0,
                new SolidColorBrush(Color.FromRgb(100, 100, 100)));
            dc.DrawText(numberText, new Point(center.X - numberText.Width / 2, center.Y - numberText.Height / 2));
            
            // 绘制动作内容
            var data = GetInnerData(i);
            if (data != null && data.HasAction)
            {
                DrawSectorContent(dc, center, data,
                    WheelConstants.IconSizeInner,
                    WheelConstants.FontSizeLabel,
                    maxTextWidth: 48.0);
            }
            else if (data != null && !data.HasAction)
            {
                DrawPlusSign(dc, center);
            }
        }

        // 绘制外圈编号 (9-16 或 9-32)
        int outerSectors = _outerRing16Mode ? WheelConstants.OuterSectors16 : WheelConstants.OuterSectors8;
        double outerStep = 360.0 / outerSectors;
        
        for (int i = 0; i < outerSectors; i++)
        {
            // 外圈扇区中心角度：起始角度 + i * step + 扇区半宽
            double centerAngle = (startAngle + i * outerStep + outerStep / 2.0) % 360.0;
            
            var    center      = WheelGeometry.SectorCenterPoint(
                cx, cy,
                WheelConstants.InnerRingRadius,
                WheelConstants.OuterRingRadius,
                centerAngle);
            
            // 绘制编号
            var numberText = MakeFormattedText((i + 9).ToString(), 10.0, 20.0,
                new SolidColorBrush(Color.FromRgb(100, 100, 100)));
            dc.DrawText(numberText, new Point(center.X - numberText.Width / 2, center.Y - numberText.Height / 2));
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
