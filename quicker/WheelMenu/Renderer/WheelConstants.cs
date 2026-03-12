namespace WheelMenu.Renderer;

using System.Windows.Media;

public static class WheelConstants
{
    // ===== 圆盘尺寸（WPF 逻辑像素，DPI 无关）=====
    // 默认值：根据新几何结构
    // 圆1 R20(死区) -> 环1 R100 -> 环2 R200 -> 环3 R300
    // 支持从配置中读取自定义值
    private static double _deadZoneRadius = 20.0;
    private static double _ring1Radius = 100.0;
    private static double _ring2Radius = 200.0;
    private static double _ring3Radius = 300.0;

    public static double DeadZoneRadius
    {
        get => _deadZoneRadius;
        set { _deadZoneRadius = value; UpdateDerivedValues(); }
    }
    public static double Ring1Radius
    {
        get => _ring1Radius;
        set { _ring1Radius = value; UpdateDerivedValues(); }
    }
    public static double Ring2Radius
    {
        get => _ring2Radius;
        set { _ring2Radius = value; UpdateDerivedValues(); }
    }
    public static double Ring3Radius
    {
        get => _ring3Radius;
        set { _ring3Radius = value; UpdateDerivedValues(); }
    }

    // 动态计算的派生值
    public static double WheelRadius { get; private set; } = 300.0;
    public static double WheelDiameter { get; private set; } = 600.0;

    // 兼容旧名称（指向新的属性）
    public static double InnerRingRadius => Ring1Radius;
    public static double OuterRingRadius => Ring2Radius;
    public static double ExtendedRingRadius => Ring3Radius;

    /// <summary>
    /// 从TriggerSettings更新圆盘尺寸
    /// </summary>
    public static void UpdateFromSettings(Config.TriggerSettings? settings)
    {
        if (settings == null) return;
        _deadZoneRadius = settings.DeadZoneRadius;
        _ring1Radius = settings.Ring1Radius;
        _ring2Radius = settings.Ring2Radius;
        _ring3Radius = settings.Ring3Radius;
        UpdateDerivedValues();
    }

    private static void UpdateDerivedValues()
    {
        WheelRadius = _ring3Radius;
        WheelDiameter = _ring3Radius * 2;
    }

    /// <summary>
    /// 重置为默认值
    /// </summary>
    public static void ResetToDefaults()
    {
        _deadZoneRadius = 20.0;
        _ring1Radius = 100.0;
        _ring2Radius = 200.0;
        _ring3Radius = 300.0;
        UpdateDerivedValues();
    }

    // ===== 格子数量 =====
    public const int InnerSectors    = 8;     // 环1: 8扇区 (1-8)
    public const int OuterSectors8   = 8;     // 环2: 8扇区 (1-8)  
    public const int OuterSectors16  = 16;   // 环2: 16扇区 (1-16) - 16模式
    public const int ExtendedSectors = 16;   // 环3: 16扇区 (9-24) 或32扇区 (9-40)
    public const int ExtendedSectors32 = 32; // 环3: 32扇区 (9-40)

    // ===== 旋转角度 =====
    // 起始角度：-90度（12点钟方向为起点，与 DiskUI 示例一致）
    public const double SectorStartAngle = -90.0;
    // 旋转角度：22.5度（整盘旋转角度）
    public const double SectorRotationAngle = 22.5;

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
