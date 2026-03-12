namespace WheelMenu.Config;

using System.Text.Json.Serialization;

/// <summary>屏幕边缘适配模式</summary>
public enum EdgeConstrainMode
{
    /// <summary>不限定：圆心就在鼠标位置，部分扇区可能超出屏幕</summary>
    None,
    /// <summary>限定屏幕：自动将圆心向屏幕内偏移，确保整个轮盘都在屏幕内</summary>
    Constrain,
    /// <summary>限定+移指针：在限定屏幕基础上，同时把鼠标指针移动到新圆心位置</summary>
    ConstrainWithCursor
}

/// <summary>触发设置</summary>
public class TriggerSettings
{
    public string TriggerKey { get; set; } = "middle"; // middle, x1, x2, side_scroll
    public int Size { get; set; } = 120;
    public int TimeoutMs { get; set; } = 0;
    public bool OuterRing16Mode { get; set; } = false;
    public bool HideLabelWhenIcon { get; set; } = true;
    
    // ===== 圆盘尺寸自定义 =====
    /// <summary>圆1(死区)半径</summary>
    public int DeadZoneRadius { get; set; } = 20;
    /// <summary>环1半径</summary>
    public int Ring1Radius { get; set; } = 100;
    /// <summary>环2半径</summary>
    public int Ring2Radius { get; set; } = 200;
    /// <summary>环3半径</summary>
    public int Ring3Radius { get; set; } = 300;
    
    /// <summary>屏幕边缘适配模式</summary>
    public EdgeConstrainMode EdgeConstrainMode { get; set; } = EdgeConstrainMode.Constrain;
    
    // 保留旧属性以兼容现有配置（JSON反序列化时使用）
    [JsonIgnore]
    public bool ConstrainToScreen
    {
        get => EdgeConstrainMode != EdgeConstrainMode.None;
        set => EdgeConstrainMode = value ? EdgeConstrainMode.Constrain : EdgeConstrainMode.None;
    }
    
    [JsonIgnore]
    public bool AutoMoveCursor
    {
        get => EdgeConstrainMode == EdgeConstrainMode.ConstrainWithCursor;
        set
        {
            if (value)
                EdgeConstrainMode = EdgeConstrainMode.ConstrainWithCursor;
            else if (EdgeConstrainMode == EdgeConstrainMode.ConstrainWithCursor)
                EdgeConstrainMode = EdgeConstrainMode.Constrain;
        }
    }
    
    public string RepeatTriggerKey { get; set; } = "F1";
}

/// <summary>单个扇区配置</summary>
public class SlotConfig
{
    public string ActionType { get; set; } = string.Empty; // hotkey, simulate_input, paste, open, run_action, send_text, datetime, none
    public string ActionValue { get; set; } = string.Empty;
    public string? Label { get; set; } = null;
    public string? IconPath { get; set; } = null;
}

/// <summary>场景配置</summary>
public class SceneConfig
{
    public string Name { get; set; } = string.Empty;
    public string? ProcessName { get; set; } = null; // null = 全局场景
    public SlotConfig[] InnerRing { get; set; } = new SlotConfig[8];
    public SlotConfig[] OuterRing { get; set; } = new SlotConfig[8];
    public SlotConfig[] ExtendedRing { get; set; } = new SlotConfig[8];
}

/// <summary>应用配置根节点</summary>
public class AppConfig
{
    /// <summary>配置版本号，用于迁移</summary>
    public string ConfigVersion { get; set; } = "1.0";
    public TriggerSettings TriggerSettings { get; set; } = new TriggerSettings();
    public SceneConfig GlobalScene { get; set; } = new SceneConfig
    {
        Name = "全局",
        InnerRing = new SlotConfig[8],
        OuterRing = new SlotConfig[8],
        ExtendedRing = new SlotConfig[8]
    };
    public Dictionary<string, SceneConfig> Scenes { get; set; } = new Dictionary<string, SceneConfig>();
}
