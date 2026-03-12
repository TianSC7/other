namespace WheelMenu.Renderer;

using System.Windows;
using System.Windows.Media;
using WheelMenu.Config;

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
    /// 
    /// 扇区编号规则（新几何结构）：
    /// - 环1(R100): 编号 1-8
    /// - 环2(R200): 编号 1-8 (8模式) 或 1-16 (16模式)
    /// - 环3(R300): 编号 9-16 (8模式) 或 9-32 (16模式)
    /// - 旋转22.5度后获取扇区
    /// </summary>
    public static (string Ring, int Sector) HitTest(
        double cx, double cy,
        double mouseX, double mouseY,
        int totalSectors = 8)
    {
        return HitTestWithRotation(cx, cy, mouseX, mouseY, totalSectors, 0.0);
    }

    /// <summary>
    /// 带旋转角度的点击测试方法
    /// </summary>
    public static (string Ring, int Sector) HitTestWithRotation(
        double cx, double cy,
        double mouseX, double mouseY,
        int totalSectors = 8,
        double rotationAngle = 0.0)
    {
        double dx   = mouseX - cx;
        double dy   = mouseY - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        // 轮盘角度：atan2(dx, -dy)，0°=正上方，顺时针
        double angleDeg = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        if (angleDeg < 0) angleDeg += 360.0;
        
        // 应用旋转角度，调整角度以匹配旋转后的扇区
        double adjustedAngle = (angleDeg - rotationAngle + 360.0) % 360.0;
        double step   = 360.0 / totalSectors;
        int    sector = (int)((adjustedAngle + step / 2.0) / step) % totalSectors;

        string ring;
        if (dist <= WheelConstants.DeadZoneRadius)
            ring = "dead";
        else if (dist <= WheelConstants.InnerRingRadius)
            ring = "inner";
        else if (dist <= WheelConstants.OuterRingRadius)
            ring = "outer";
        else
            ring = "extended";

        return (ring, ring == "dead" ? -1 : sector);
    }
    
    /// <summary>
    /// 由鼠标坐标判断圈层和扇区索引（带扩展圈可见性）。
    /// </summary>
    public static (string Ring, int Sector) HitTestWithExtended(
        double cx, double cy,
        double mouseX, double mouseY,
        int totalSectors = 8,
        bool showExtended = false)
    {
        return HitTestWithExtendedAndRotation(cx, cy, mouseX, mouseY, totalSectors, showExtended, 0.0);
    }

    /// <summary>
    /// 带旋转角度和扩展圈的点击测试方法
    /// </summary>
    public static (string Ring, int Sector) HitTestWithExtendedAndRotation(
        double cx, double cy,
        double mouseX, double mouseY,
        int totalSectors = 8,
        bool showExtended = false,
        double rotationAngle = 0.0)
    {
        double dx   = mouseX - cx;
        double dy   = mouseY - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        // 轮盘角度：atan2(dx, -dy)，0°=正上方，顺时针
        double angleDeg = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        if (angleDeg < 0) angleDeg += 360.0;
        
        // 应用旋转角度，调整角度以匹配旋转后的扇区
        double adjustedAngle = (angleDeg - rotationAngle + 360.0) % 360.0;
        double step   = 360.0 / totalSectors;
        int    sector = (int)((adjustedAngle + step / 2.0) / step) % totalSectors;

        string ring;
        if (dist <= WheelConstants.DeadZoneRadius)
            ring = "dead";
        else if (dist <= WheelConstants.InnerRingRadius)
            ring = "inner";
        else if (dist <= WheelConstants.OuterRingRadius)
            ring = "outer";
        else if (showExtended && dist <= WheelConstants.ExtendedRingRadius)
            ring = "extended";
        else
            ring = "outer"; // 超出范围默认算外圈

        return (ring, ring == "dead" ? -1 : sector);
    }

    /// <summary>
    /// 主界面轮盘点击测试（使用 DiskUI 一致的几何结构）
    /// 起始角度 -90度 + 旋转22.5度 = -67.5度
    /// 用户反馈：需要减去11.25度才能正确匹配高亮
    /// </summary>
    public static (string Ring, int Sector) HitTestMainWheel(
        double cx, double cy,
        double mouseX, double mouseY,
        int totalSectors = 8,
        bool showExtended = false)
    {
        double dx   = mouseX - cx;
        double dy   = mouseY - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        // 轮盘角度：atan2(dx, -dy)，0°=正上方，顺时针
        double angleDeg = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        if (angleDeg < 0) angleDeg += 360.0;
        
        string ring;
        // 起始角度 + 旋转角度 = -90 + 22.5 = -67.5度
        double rotationAngle = WheelConstants.SectorStartAngle + WheelConstants.SectorRotationAngle;
        
        if (dist <= WheelConstants.DeadZoneRadius)
            ring = "dead";
        else if (dist <= WheelConstants.InnerRingRadius)
        {
            ring = "inner";
            // 环1使用起始角度 + 旋转角度
            rotationAngle = WheelConstants.SectorStartAngle + WheelConstants.SectorRotationAngle;
        }
        else if (dist <= WheelConstants.OuterRingRadius)
        {
            ring = "outer";
            // 环2与环1对齐（与 DiskUI 一致）
            rotationAngle = WheelConstants.SectorStartAngle + WheelConstants.SectorRotationAngle;
        }
        else if (showExtended && dist <= WheelConstants.ExtendedRingRadius)
        {
            ring = "extended";
            // 扩展圈与环1对齐
            rotationAngle = WheelConstants.SectorStartAngle + WheelConstants.SectorRotationAngle;
        }
        else
        {
            ring = "outer"; // 超出范围默认算外圈
            rotationAngle = WheelConstants.SectorStartAngle + WheelConstants.SectorRotationAngle;
        }
        
        // 应用旋转角度，调整角度以匹配旋转后的扇区
        // 用户反馈：需要减去11.25度（半个扇区）才能正确匹配高亮
        double adjustedAngle = (angleDeg - rotationAngle - 11.25 + 360.0) % 360.0;
        double step   = 360.0 / totalSectors;
        int    sector = (int)((adjustedAngle + step / 2.0) / step) % totalSectors;

        return (ring, ring == "dead" ? -1 : sector);
    }

    /// <summary>
    /// 根据圈层和扇区索引计算扇区编号（1-48）。
    /// 扇区编号规则：
    /// - 环1: 1-8
    /// - 环2: 9-16 (8模式) 或 9-32 (16模式)
    /// - 环3: 33-48 (固定)
    /// </summary>
    public static int GetSectorNumber(string ring, int sectorIndex, bool is16Mode = false)
    {
        if (sectorIndex < 0) return -1;
        
        return ring switch
        {
            "inner" => sectorIndex + 1,           // 环1: 1-8
            "outer" => is16Mode ? sectorIndex + 9 : sectorIndex + 9,   // 环2: 9-16 或 9-32
            "extended" => sectorIndex + 33,       // 环3: 33-48 (固定)
            _ => -1
        };
    }

    /// <summary>
    /// 屏幕边缘适配：计算圆盘应显示的圆心坐标。
    /// </summary>
    /// <param name="mouseX">鼠标X坐标</param>
    /// <param name="mouseY">鼠标Y坐标</param>
    /// <param name="screenWorkArea">屏幕工作区域</param>
    /// <param name="mode">边缘适配模式</param>
    /// <param name="showExtended">是否显示扩展圈(R300)</param>
    /// <returns>计算后的圆心坐标</returns>
    public static Point CalculateWheelCenter(
        double mouseX, double mouseY,
        Rect screenWorkArea,
        EdgeConstrainMode mode,
        bool showExtended = false)
    {
        // 模式A（不限定）：圆心就在鼠标位置，部分扇区可能超出屏幕
        if (mode == EdgeConstrainMode.None)
            return new Point(mouseX, mouseY);

        // 模式B和C（限定屏幕）：自动将圆心向屏幕内偏移，确保整个轮盘都在屏幕内
        double r = showExtended ? WheelConstants.ExtendedRingRadius : WheelConstants.OuterRingRadius;
        double cx = Math.Clamp(mouseX, screenWorkArea.Left + r, screenWorkArea.Right - r);
        double cy = Math.Clamp(mouseY, screenWorkArea.Top + r, screenWorkArea.Bottom - r);
        return new Point(cx, cy);
    }

    /// <summary>
    /// 屏幕边缘适配：计算圆盘应显示的圆心坐标（兼容旧版本）。
    /// </summary>
    public static Point CalculateWheelCenter(
        double mouseX, double mouseY,
        Rect screenWorkArea,
        bool constrainToScreen)
    {
        return CalculateWheelCenter(mouseX, mouseY, screenWorkArea,
            constrainToScreen ? EdgeConstrainMode.Constrain : EdgeConstrainMode.None);
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
