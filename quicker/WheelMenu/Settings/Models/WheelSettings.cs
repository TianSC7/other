namespace WheelMenu.Settings.Models;

public class WheelSettings
{
    public string TriggerKey { get; set; } = "middle";
    public double Size { get; set; } = 120.0;
    public int TimeoutMs { get; set; } = 0;
    public bool OuterRing16Mode { get; set; } = false;
    public bool HideLabelWhenIcon { get; set; } = true;
    public bool ConstrainToScreen { get; set; } = true;
    public bool AutoMoveCursor { get; set; } = false;
    public string RepeatTriggerKey { get; set; } = "F1";
    
    // ===== 圆盘尺寸自定义 =====
    /// <summary>圆1(死区)半径</summary>
    public int DeadZoneRadius { get; set; } = 20;
    /// <summary>环1半径</summary>
    public int Ring1Radius { get; set; } = 100;
    /// <summary>环2半径</summary>
    public int Ring2Radius { get; set; } = 200;
    /// <summary>环3半径</summary>
    public int Ring3Radius { get; set; } = 300;
}
