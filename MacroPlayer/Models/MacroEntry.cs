namespace MacroPlayer.Models;

/// <summary>
/// 宏条目
/// </summary>
public class MacroEntry
{
    /// <summary>
    /// 宏名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 热键（如 "F4"）
    /// </summary>
    public string Hotkey { get; set; } = "";

    /// <summary>
    /// 按键序列（如 "DDQQ" 或 "D(30)Q(50)"）
    /// </summary>
    public string Sequence { get; set; } = "";

    /// <summary>
    /// 按键间隔（毫秒）
    /// </summary>
    public int DelayMs { get; set; } = 10;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;
}
