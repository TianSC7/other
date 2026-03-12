namespace WheelMenu.Logic.Actions.Executors;

using WheelMenu.Logic.Win32;

/// <summary>
/// 快捷键执行器 - 模拟键盘快捷键组合
/// </summary>
public class HotkeyExecutor : IActionExecutor
{
    // 虚拟键码映射表
    private static readonly Dictionary<string, ushort> KeyNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // 修饰键
        ["ctrl"] = 0x11, ["control"] = 0x11,
        ["alt"] = 0x12, ["menu"] = 0x12,
        ["shift"] = 0x10,
        ["win"] = 0x5B, ["windows"] = 0x5B,

        // 特殊键
        ["enter"] = 0x0D, ["return"] = 0x0D,
        ["esc"] = 0x1B, ["escape"] = 0x1B,
        ["tab"] = 0x09,
        ["space"] = 0x20,
        ["back"] = 0x08, ["backspace"] = 0x08,
        ["del"] = 0x2E, ["delete"] = 0x2E,
        ["ins"] = 0x2D, ["insert"] = 0x2D,
        ["home"] = 0x24,
        ["end"] = 0x23,
        ["pgup"] = 0x21, ["pageup"] = 0x21,
        ["pgdn"] = 0x22, ["pagedown"] = 0x22,
        ["up"] = 0x26,
        ["down"] = 0x28,
        ["left"] = 0x25,
        ["right"] = 0x27,
        ["capslock"] = 0x14,
        ["numlock"] = 0x90,
        ["scrolllock"] = 0x91,
        ["printscreen"] = 0x2C,
        ["pause"] = 0x13,
        ["apps"] = 0x5D,

        // 功能键
        ["f1"] = 0x70, ["f2"] = 0x71, ["f3"] = 0x72, ["f4"] = 0x73,
        ["f5"] = 0x74, ["f6"] = 0x75, ["f7"] = 0x76, ["f8"] = 0x77,
        ["f9"] = 0x78, ["f10"] = 0x79, ["f11"] = 0x7A, ["f12"] = 0x7B,
    };

    // 需要 EXTENDEDKEY 标志的键
    private static readonly HashSet<ushort> ExtendedKeys = new()
    {
        0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, // PageUp/Down, Home, End, 方向键
        0x2D, 0x2E,   // Insert, Delete
        0x6F,         // 小键盘 /
        0xA3, 0xA5,   // 右 Ctrl, 右 Alt
    };

    public void Execute(string actionType, string actionValue, string? label = null, string? iconPath = null)
    {
        if (string.IsNullOrEmpty(actionValue))
            return;

        var vkCodes = ParseKeyString(actionValue);
        if (vkCodes == null || vkCodes.Length == 0)
        {
            System.Diagnostics.Debug.WriteLine($"[HotkeyExecutor] 无法解析快捷键: {actionValue}");
            return;
        }

        SendKeyCombo(vkCodes);
    }

    /// <summary>
    /// 解析快捷键字符串（如 "Ctrl+C"）为虚拟键码数组
    /// </summary>
    private static ushort[]? ParseKeyString(string keyString)
    {
        var parts = keyString.Split('+', StringSplitOptions.TrimEntries);
        var result = new List<ushort>();

        foreach (var part in parts)
        {
            if (KeyNameMap.TryGetValue(part, out ushort vk))
            {
                result.Add(vk);
            }
            else if (part.Length == 1)
            {
                // 单字符：用 VkKeyScan 转换
                short scanResult = VkKeyScan(part[0]);
                if (scanResult == -1) return null;
                ushort mainVk = (ushort)(scanResult & 0xFF);
                byte shiftState = (byte)((scanResult >> 8) & 0xFF);

                // 若需要 Shift，自动添加
                if ((shiftState & 0x01) != 0 && !result.Contains(0x10))
                    result.Insert(0, 0x10);
                result.Add(mainVk);
            }
            else
            {
                return null;
            }
        }

        return result.Count > 0 ? result.ToArray() : null;
    }

    /// <summary>
    /// 发送快捷键组合
    /// </summary>
    private static void SendKeyCombo(ushort[] vkCodes)
    {
        if (vkCodes.Length == 0) return;

        var inputs = new List<NativeMethods.INPUT>();

        // 按下所有键
        foreach (var vk in vkCodes)
            inputs.Add(MakeKeyDown(vk));

        // 松开所有键（逆序）
        for (int i = vkCodes.Length - 1; i >= 0; i--)
            inputs.Add(MakeKeyUp(vkCodes[i]));

        SendInputs(inputs.ToArray());
    }

    private static NativeMethods.INPUT MakeKeyDown(ushort vk)
    {
        uint flags = 0;
        if (ExtendedKeys.Contains(vk)) flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new() { ki = new() { wVk = vk, dwFlags = flags } }
        };
    }

    private static NativeMethods.INPUT MakeKeyUp(ushort vk)
    {
        uint flags = NativeMethods.KEYEVENTF_KEYUP;
        if (ExtendedKeys.Contains(vk)) flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new() { ki = new() { wVk = vk, dwFlags = flags } }
        };
    }

    private static void SendInputs(NativeMethods.INPUT[] inputs)
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>();
        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, size);
        if (sent != inputs.Length)
        {
            int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"[HotkeyExecutor] SendInput 仅发送 {sent}/{inputs.Length}，错误码 {err}");
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);
}
