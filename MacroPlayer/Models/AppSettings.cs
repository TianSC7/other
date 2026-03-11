namespace MacroPlayer.Models;

/// <summary>
/// 应用程序配置
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 宏列表
    /// </summary>
    public List<MacroEntry> Macros { get; set; } = new();

    /// <summary>
    /// 按键按下持续时间（毫秒）
    /// </summary>
    public int KeyDownDuration { get; set; } = 20;

    /// <summary>
    /// 是否启动时最小化到托盘
    /// </summary>
    public bool StartMinimized { get; set; } = false;
}
