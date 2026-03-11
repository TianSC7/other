using System.Runtime.InteropServices;
using System.Collections.Generic;
using MacroPlayer.Models;

namespace MacroPlayer.Core;

/// <summary>
/// 键盘模拟器，使用keybd_event API模拟按键
/// </summary>
public static class InputSimulator
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_KEYDOWN = 0x0000;

    // 虚拟键码映射表
    private static readonly Dictionary<string, byte> VkMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // 字母
        ["A"] = 0x41, ["B"] = 0x42, ["C"] = 0x43, ["D"] = 0x44, ["E"] = 0x45,
        ["F"] = 0x46, ["G"] = 0x47, ["H"] = 0x48, ["I"] = 0x49, ["J"] = 0x4A,
        ["K"] = 0x4B, ["L"] = 0x4C, ["M"] = 0x4D, ["N"] = 0x4E, ["O"] = 0x4F,
        ["P"] = 0x50, ["Q"] = 0x51, ["R"] = 0x52, ["S"] = 0x53, ["T"] = 0x54,
        ["U"] = 0x55, ["V"] = 0x56, ["W"] = 0x57, ["X"] = 0x58, ["Y"] = 0x59, ["Z"] = 0x5A,
        // 数字
        ["0"] = 0x30, ["1"] = 0x31, ["2"] = 0x32, ["3"] = 0x33, ["4"] = 0x34,
        ["5"] = 0x35, ["6"] = 0x36, ["7"] = 0x37, ["8"] = 0x38, ["9"] = 0x39,
        // F键
        ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73, ["F5"] = 0x74,
        ["F6"] = 0x75, ["F7"] = 0x76, ["F8"] = 0x77, ["F9"] = 0x78, ["F10"] = 0x79,
        ["F11"] = 0x7A, ["F12"] = 0x7B,
        // 特殊键
        ["ENTER"] = 0x0D, ["RETURN"] = 0x0D,
        ["SPACE"] = 0x20,
        ["TAB"] = 0x09,
        ["ESC"] = 0x1B, ["ESCAPE"] = 0x1B,
        ["BACKSPACE"] = 0x08, ["BS"] = 0x08,
        ["DELETE"] = 0x2E, ["DEL"] = 0x2E,
        ["INSERT"] = 0x2D, ["INS"] = 0x2D,
        ["HOME"] = 0x24,
        ["END"] = 0x23,
        ["PAGEUP"] = 0x21, ["PGUP"] = 0x21,
        ["PAGEDOWN"] = 0x22, ["PGDN"] = 0x22,
        ["LEFT"] = 0x25,
        ["UP"] = 0x26,
        ["RIGHT"] = 0x27,
        ["DOWN"] = 0x28,
        // 修饰键
        ["LSHIFT"] = 0xA0,
        ["RSHIFT"] = 0xA1,
        ["SHIFT"] = 0x10,
        ["LCTRL"] = 0xA2,
        ["RCTRL"] = 0xA3,
        ["CTRL"] = 0x11,
        ["CONTROL"] = 0x11,
        ["LALT"] = 0xA4,
        ["RALT"] = 0xA5,
        ["ALT"] = 0x12
    };

    /// <summary>
    /// 播放宏
    /// </summary>
    /// <param name="entry">宏条目</param>
    /// <param name="keyDownDuration">按键按下持续时间（毫秒）</param>
    public static async Task PlayAsync(MacroEntry entry, int keyDownDuration)
    {
        var actions = MacroParser.Parse(entry.Sequence, entry.DelayMs);
        foreach (var action in actions)
        {
            if (TryGetVirtualKey(action.Key, out var vk))
            {
                // 按下
                keybd_event(vk, 0, KEYEVENTF_KEYDOWN, IntPtr.Zero);
                await Task.Delay(keyDownDuration);
                keybd_event(vk, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            }
            else
            {
                Logger.Warning($"未知按键：{action.Key}");
            }

            // 处理延迟
            if (action.Delay > 0)
            {
                await Task.Delay(action.Delay);
            }
        }
    }

    /// <summary>
    /// 获取虚拟键码
    /// </summary>
    /// <param name="key">按键名称</param>
    /// <param name="vk">虚拟键码</param>
    /// <returns>是否成功获取</returns>
    private static bool TryGetVirtualKey(string key, out byte vk)
    {
        return VkMap.TryGetValue(key, out vk);
    }
}
