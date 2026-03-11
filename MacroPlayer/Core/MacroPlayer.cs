using System.Runtime.InteropServices;
using System.Collections.Generic;
using MacroPlayer.Models;

namespace MacroPlayer.Core;

/// <summary>
/// 宏播放器
/// </summary>
public static class MacroPlayer
{
    /// <summary>
    /// 播放宏
    /// </summary>
    /// <param name="entry">宏条目</param>
    /// <param name="keyDownDuration">按键按下持续时间（毫秒）</param>
    public static async Task PlayAsync(MacroEntry entry, int keyDownDuration)
    {
        await InputSimulator.PlayAsync(entry, keyDownDuration);
    }
}
