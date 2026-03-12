namespace WheelMenu.Renderer;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

/// <summary>
/// 参考 sop/disk_sectors (1).md 文档创建的圆盘控件
/// 严格使用文档中的扇区角度数据
/// </summary>
public class DiskUIControl : FrameworkElement
{
    // ===== 几何参数（严格遵循 disk_sectors (1).md）=====
    const double Rc = 20;    // 圆1半径
    const double R1 = 100;   // 环1外径
    const double R2 = 200;   // 环2外径
    const double R3 = 300;   // 环3外径
    const double ROT = 22.5; // 整盘旋转角度
    const double S8 = 45;    // 8格模式角度跨度
    const double S16 = 22.5; // 16格模式角度跨度

    // ===== 旋转后的实际起始角度（来自 disk_sectors (1).md）=====
    // 环1: 扇区1在12点位置(270°)，逆时针编号
    // 旋转后角度：0°=3点，270°=12点
    // 顺序：1→12点, 2→12~1点, 3→3点, 4→4~5点, 5→6点, 6→7~8点, 7→9点, 8→10~11点
    
    // 画布中心
    private Point _center = new Point(170, 170); // 默认中心

    // 颜色（浅色主题 - 与 WheelCanvas 一致）
    // 背景色
    static readonly Color C_Background = Color.FromArgb(224, 255, 255, 255);
    
    // 扇区颜色
    static readonly Color C_SectorEmpty = Color.FromArgb(0, 255, 255, 255);
    static readonly Color C_SectorNormal = Color.FromArgb(30, 255, 255, 255);
    static readonly Color C_SectorHovered = Color.FromArgb(50, 30, 120, 255);
    
    // 分割线颜色
    static readonly Color C_Divider = Color.FromArgb(31, 0, 0, 0);
    
    // 边框颜色
    static readonly Color C_Border = Color.FromArgb(31, 0, 0, 0);
    
    // 文字颜色
    static readonly Color C_Label = Color.FromArgb(255, 51, 51, 51);
    static readonly Color C_LabelEmpty = Color.FromArgb(255, 180, 180, 180);
    
    // 选中高亮
    static readonly Color C_Sel = Color.FromArgb(50, 30, 120, 255);

    // 显示选项
    private bool _showCircle1 = true;
    private bool _showRing1 = true;
    private bool _showRing2 = true;
    private bool _showRing3 = false;
    private bool _outer16 = false;

    // 扇区数据
    private WheelSectorData[] _innerData = Array.Empty<WheelSectorData>();
    private WheelSectorData[] _outerData = Array.Empty<WheelSectorData>();

    // 选中状态
    private Path? _selectedPath;
    private Brush? _selectedOrigFill;
    private Brush? _selectedOrigStroke;
    
    // 高亮状态（用于 SetHighlight）
    private string? _highlightRing;
    private int _highlightSector = -1;

    // 事件
    public event EventHandler<SectorClickEventArgs>? SectorClicked;

    // 重写鼠标按下事件进行点击检测
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        
        Point pos = e.GetPosition(this);
        double cx = _center.X;
        double cy = _center.Y;
        
        // 计算鼠标相对于中心的坐标
        double dx = pos.X - cx;
        double dy = pos.Y - cy;
        
        // 计算距离和角度
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        if (angle < 0) angle += 360;
        
        // 判断点击的环
        if (dist < Rc)
        {
            // 圆心死区，不处理
            return;
        }
        else if (dist < R1)
        {
            // 环1
            int sector = GetSectorFromAngle(angle, 8);
            if (sector >= 0)
            {
                // 环1扇区：2在12~1点位置(292.5°~337.5°)
                // 扇区顺序：2,3,4,5,6,7,8,1
                int[] sectorMap = { 2, 3, 4, 5, 6, 7, 8, 1 };
                sector = sectorMap[sector];
                RaiseSectorClick("环1", sector);
            }
        }
        else if (dist < R2)
        {
            // 环2
            int sectors = _outer16 ? 16 : 8;
            int sector = GetSectorFromAngle(angle, sectors);
            if (sector >= 0)
            {
                if (_outer16)
                {
                    // 16模式扇区：10-24, 9
                    int[] sectorMap = { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 9 };
                    sector = sectorMap[sector];
                }
                else
                {
                    // 8模式扇区：10-16, 9
                    int[] sectorMap = { 10, 11, 12, 13, 14, 15, 16, 9 };
                    sector = sectorMap[sector];
                }
                RaiseSectorClick("环2", sector);
            }
        }
        else if (dist < R3 && _showRing3)
        {
            // 环3
            int sector = GetSectorFromAngle(angle, 8);
            if (sector >= 0)
            {
                int[] sectorMap = { 34, 35, 36, 37, 38, 39, 40, 33 };
                sector = sectorMap[sector];
                RaiseSectorClick("环3", sector);
            }
        }
    }

    // 根据角度获取扇区索引（0-based）
    private int GetSectorFromAngle(double angle, int sectorCount)
    {
        // 分割线角度：292.5°, 337.5°, 22.5°, 67.5°, 112.5°, 157.5°, 202.5°, 247.5°
        // 从292.5°开始，每45°一个分割线
        double step = 360.0 / sectorCount;
        
        // 将角度转换到 292.5° 开始的空间
        double startAngle = 292.5;
        double relAngle = angle - startAngle;
        if (relAngle < 0) relAngle += 360;
        
        int sector = (int)(relAngle / step);
        if (sector >= 0 && sector < sectorCount)
            return sector;
        
        return -1;
    }

    // 触发扇区点击事件
    private void RaiseSectorClick(string ring, int sector)
    {
        // 清除之前的高亮
        ClearHighlight();
        
        // 触发事件
        SectorClicked?.Invoke(this, new SectorClickEventArgs(sector, ring));
    }

    /// <summary>
    /// 内环数据（环1：8个扇区）
    /// </summary>
    public WheelSectorData[] InnerData
    {
        get => _innerData;
        set { _innerData = value; InvalidateVisual(); }
    }

    /// <summary>
    /// 外环数据（环2：8或16个扇区）
    /// </summary>
    public WheelSectorData[] OuterData
    {
        get => _outerData;
        set { _outerData = value; InvalidateVisual(); }
    }

    /// <summary>
    /// 获取扩展环（环3）是否可见
    /// </summary>
    public bool GetExtendedRingVisible() => _showRing3;

    /// <summary>
    /// 设置高亮显示指定的扇区
    /// </summary>
    public void SetHighlight(string ring, int sector)
    {
        // 保存要高亮的信息
        _highlightRing = ring;
        _highlightSector = sector;
        
        // 重新渲染并高亮
        InvalidateVisual();
    }

    /// <summary>
    /// 清除高亮
    /// </summary>
    public void ClearHighlight()
    {
        _highlightRing = null;
        _highlightSector = -1;
        InvalidateVisual();
    }

    public DiskUIControl()
    {
        Loaded += (_, _) => InvalidateVisual();
    }

    public void SetCenter(Point center)
    {
        _center = center;
        InvalidateVisual();
    }

    public Point GetCenter() => _center;

    public void SetDisplayOptions(bool showRing3, bool outer16)
    {
        _showRing3 = showRing3;
        _outer16 = outer16;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double cx = _center.X;
        double cy = _center.Y;

        // ===== 绘制背景（整盘）=====
        var bgBrush = new SolidColorBrush(C_Background);
        var ringGeo = CreateRingGeometry(cx, cy, Rc, R2);
        dc.DrawGeometry(bgBrush, null, ringGeo);

        // ===== 环3（R200~R300，编号33-40）=====
        // 来自 disk_sectors (1).md:
        // 扇区34: 292.5°~337.5°, 35: 337.5°~22.5°, 36: 22.5°~67.5°
        // 扇区37: 67.5°~112.5°, 38: 112.5°~157.5°, 39: 157.5°~202.5°
        // 扇区40: 202.5°~247.5°, 33: 247.5°~292.5°
        if (_showRing3)
        {
            // 环3扇区顺序：34,35,36,37,38,39,40,33
            int[] r3Sectors = { 34, 35, 36, 37, 38, 39, 40, 33 };
            double[] r3Starts = { 292.5, 337.5, 22.5, 67.5, 112.5, 157.5, 202.5, 247.5 };
            
            for (int i = 0; i < 8; i++)
            {
                int sid = r3Sectors[i];
                // 检查是否高亮
                bool isHighlight = (_highlightRing == "环3" || _highlightRing == "extended") && _highlightSector == sid;
                Color fillColor = isHighlight ? C_Sel : C_SectorEmpty;
                var p = MakeSector(R2, R3, r3Starts[i], S8, fillColor, C_Border, 0.5);
                dc.DrawGeometry(p.Fill, null, p.Data);
            }
            dc.DrawEllipse(new SolidColorBrush(C_Border), null, new Point(cx, cy), R3, R3);
        }

        // ===== 分割线角度（8条基础线）=====
        // 来自 disk_sectors (1).md: 分割线1-8: 292.5°, 337.5°, 22.5°, 67.5°, 112.5°, 157.5°, 202.5°, 247.5°
        double[] divAngles = { 292.5, 337.5, 22.5, 67.5, 112.5, 157.5, 202.5, 247.5 };

        // ===== 环2（R100~R200，编号9-16或9-24）=====
        if (_showRing2)
        {
            if (_outer16)
            {
                // 外圈16模式：扇区10-24, 9
                int[] r2_16Sectors = { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 9 };
                double[] r2_16Starts = { 292.5, 315, 337.5, 0, 22.5, 45, 67.5, 90, 112.5, 135, 157.5, 180, 202.5, 225, 247.5, 270 };
                
                for (int i = 0; i < 16; i++)
                {
                    int sid = r2_16Sectors[i];
                    // 检查是否高亮
                    bool isHighlight = (_highlightRing == "环2" || _highlightRing == "outer") && _highlightSector == sid;
                    Color fillColor = isHighlight ? C_Sel : C_SectorEmpty;
                    var p = MakeSector(R1, R2, r2_16Starts[i], S16, fillColor, C_Border, 0.4);
                    dc.DrawGeometry(p.Fill, null, p.Data);
                    
                    // 标签
                    AddLabel(dc, cx, cy, sid.ToString(), R1, R2, r2_16Starts[i], S16);
                }
            }
            else
            {
                // 普通8格模式：扇区9-16
                int[] r2_8Sectors = { 10, 11, 12, 13, 14, 15, 16, 9 };
                double[] r2_8Starts = { 292.5, 337.5, 22.5, 67.5, 112.5, 157.5, 202.5, 247.5 };
                
                for (int i = 0; i < 8; i++)
                {
                    int sid = r2_8Sectors[i];
                    // 检查是否高亮
                    bool isHighlight = (_highlightRing == "环2" || _highlightRing == "outer") && _highlightSector == sid;
                    Color fillColor = isHighlight ? C_Sel : C_SectorEmpty;
                    var p = MakeSector(R1, R2, r2_8Starts[i], S8, fillColor, C_Border, 0.4);
                    dc.DrawGeometry(p.Fill, null, p.Data);
                    
                    // 标签
                    AddLabel(dc, cx, cy, sid.ToString(), R1, R2, r2_8Starts[i], S8);
                }
            }

            // 分割线（8条基础线）
            var penDivider = new Pen(new SolidColorBrush(C_Divider), 1.0);
            penDivider.Freeze();
            for (int i = 0; i < 8; i++)
            {
                double a = divAngles[i];
                dc.DrawLine(penDivider, Pt(cx, cy, R1, a), Pt(cx, cy, R2, a));
            }
            
            // 外圈16额外插入分割线（8条）
            if (_outer16)
            {
                var penDivider2 = new Pen(new SolidColorBrush(C_Divider), 0.5);
                penDivider2.Freeze();
                double[] insertAngles = { 315, 0, 45, 90, 135, 180, 225, 270 };
                for (int i = 0; i < 8; i++)
                {
                    double a = insertAngles[i];
                    dc.DrawLine(penDivider2, Pt(cx, cy, R1, a), Pt(cx, cy, R2, a));
                }
            }
            dc.DrawEllipse(new SolidColorBrush(C_Border), null, new Point(cx, cy), R2, R2);
        }

        // ===== 环1（R20~R100，编号1-8）=====
        // 来自 disk_sectors (1).md:
        // 扇区2: 292.5°~337.5°, 3: 337.5°~22.5°, 4: 22.5°~67.5°
        // 扇区5: 67.5°~112.5°, 6: 112.5°~157.5°, 7: 157.5°~202.5°
        // 扇区8: 202.5°~247.5°, 1: 247.5°~292.5°
        if (_showRing1)
        {
            // 环1扇区顺序：2,3,4,5,6,7,8,1
            int[] r1Sectors = { 2, 3, 4, 5, 6, 7, 8, 1 };
            double[] r1Starts = { 292.5, 337.5, 22.5, 67.5, 112.5, 157.5, 202.5, 247.5 };
            
            for (int i = 0; i < 8; i++)
            {
                int sid = r1Sectors[i];
                // 检查是否高亮
                bool isHighlight = (_highlightRing == "环1" || _highlightRing == "inner") && _highlightSector == sid;
                Color fillColor = isHighlight ? C_Sel : C_SectorEmpty;
                var p = MakeSector(Rc, R1, r1Starts[i], S8, fillColor, C_Border, 0.4);
                dc.DrawGeometry(p.Fill, null, p.Data);

                // 标签
                AddLabel(dc, cx, cy, sid.ToString(), Rc, R1, r1Starts[i], S8);
            }
            
            // 分割线（与环2共用）
            var penDividerInner = new Pen(new SolidColorBrush(C_Divider), 1.0);
            penDividerInner.Freeze();
            for (int i = 0; i < 8; i++)
            {
                double a = divAngles[i];
                dc.DrawLine(penDividerInner, Pt(cx, cy, Rc, a), Pt(cx, cy, R1, a));
            }
            dc.DrawEllipse(new SolidColorBrush(C_Border), null, new Point(cx, cy), R1, R1);
        }

        // 圆1（整圆R20 死区）
        if (_showCircle1)
        {
            dc.DrawGeometry(new SolidColorBrush(C_SectorEmpty), 
                new Pen(new SolidColorBrush(C_Border), 1.0), 
                new EllipseGeometry(new Point(cx, cy), Rc, Rc));
        }

        // 装饰外圆
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(C_Border), 1.5),
            new Point(cx, cy), R3 + 3, R3 + 3);
        
        // 圆心点
        dc.DrawEllipse(new SolidColorBrush(C_Sel), null, new Point(cx, cy), 2.5, 2.5);
    }

    // 创建圆环几何
    private Geometry CreateRingGeometry(double cx, double cy, double rInner, double rOuter)
    {
        var outerEllipse = new EllipseGeometry(new Point(cx, cy), rOuter, rOuter);
        var innerEllipse = new EllipseGeometry(new Point(cx, cy), rInner, rInner);
        var ringGeo = new CombinedGeometry(GeometryCombineMode.Exclude, outerEllipse, innerEllipse);
        ringGeo.Freeze();
        return ringGeo;
    }

    private void OnSectorClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Path p && p.Tag is SectorInfo info)
        {
            _selectedPath = p;
            _selectedOrigFill = p.Fill;
            _selectedOrigStroke = p.Stroke;
            
            // 设置高亮
            p.Fill = new SolidColorBrush(C_SectorHovered);
            p.Stroke = new SolidColorBrush(C_Sel);
            
            InvalidateVisual();
            
            // 触发事件
            SectorClicked?.Invoke(this, new SectorClickEventArgs(info.Sid, info.Ring));
        }
        e.Handled = true;
    }

    // 角度归一化到0-360范围
    private static double NormalizeAngle(double deg)
    {
        while (deg < 0) deg += 360;
        while (deg >= 360) deg -= 360;
        return deg;
    }

    // 几何工具方法
    Point Pt(double cx, double cy, double r, double deg)
    {
        double rad = deg * Math.PI / 180;
        return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    Path MakeSector(double r1, double r2, double startDeg, double span,
                    Color fill, Color stroke, double sw)
    {
        // 处理跨越0°的情况
        double endDeg = startDeg + span;
        bool crossZero = endDeg > 360;
        
        // 归一化角度
        double s = NormalizeAngle(startDeg);
        double e = crossZero ? endDeg - 360 : endDeg;
        bool largeArc = span > 180;

        var p1s = Pt(_center.X, _center.Y, r2, s);
        var p1e = Pt(_center.X, _center.Y, r2, e);
        var p2s = Pt(_center.X, _center.Y, r1, s);
        var p2e = Pt(_center.X, _center.Y, r1, e);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(p1s, true, true);
            ctx.ArcTo(p1e, new Size(r2, r2), 0, largeArc, SweepDirection.Clockwise, true, false);
            if (r1 > 0)
            {
                ctx.LineTo(p2e, true, false);
                ctx.ArcTo(p2s, new Size(r1, r1), 0, largeArc, SweepDirection.Counterclockwise, true, false);
            }
            else
            {
                ctx.LineTo(new Point(_center.X, _center.Y), true, false);
            }
        }
        geo.Freeze();

        return new Path
        {
            Data = geo,
            Fill = new SolidColorBrush(fill),
            Stroke = new SolidColorBrush(stroke),
            StrokeThickness = sw
        };
    }

    void AddLabel(DrawingContext dc, double cx, double cy, string text, double r1, double r2, 
                  double startDeg, double span)
    {
        double midDeg = startDeg + span / 2;
        if (midDeg >= 360) midDeg -= 360;
        
        double midR = (r1 + r2) / 2;
        double rad = midDeg * Math.PI / 180;
        double x = cx + midR * Math.Cos(rad);
        double y = cy + midR * Math.Sin(rad);

        var tb = new System.Windows.Media.FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface("Courier New"),
            9.5,
            new SolidColorBrush(C_Label),
            1.0);
        
        dc.DrawText(tb, new Point(x - tb.Width / 2, y - tb.Height / 2));
    }

    static Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromRgb(
            Convert.ToByte(hex[0..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    record SectorInfo(int Sid, string Ring, double StartDeg, double Span);
}

public class SectorClickEventArgs : EventArgs
{
    public int SectorId { get; }
    public string Ring { get; }

    public SectorClickEventArgs(int sectorId, string ring)
    {
        SectorId = sectorId;
        Ring = ring;
    }
}
