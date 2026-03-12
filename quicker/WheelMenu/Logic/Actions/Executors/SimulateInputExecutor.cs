namespace WheelMenu.Logic.Actions.Executors;

using WheelMenu.Logic.Win32;

/// <summary>
/// 模拟输入执行器 - 解析 {键名} 格式并模拟按键序列
/// 普通文字用 Unicode 方式输入（不受输入法影响）
/// </summary>
public class SimulateInputExecutor : IActionExecutor
{
    public void Execute(string actionType, string actionValue, string? label = null, string? iconPath = null)
    {
        if (string.IsNullOrEmpty(actionValue))
            return;

        var tokens = Tokenize(actionValue);
        foreach (var token in tokens)
        {
            if (token.IsKey)
            {
                // {键名} → SendInput VK 方式
                SendKeyString(token.Value);
            }
            else if (!string.IsNullOrEmpty(token.Value))
            {
                // 普通文字 → Unicode 方式
                SendUnicodeText(token.Value);
            }
        }
    }

    /// <summary>
    /// 发送快捷键字符串
    /// </summary>
    private static void SendKeyString(string keyString)
    {
        var executor = new HotkeyExecutor();
        // 这里简化处理，实际上 HotkeyExecutor 需要修改为可以接受单个键字符串
        var vkCodes = ParseKeyString(keyString);
        if (vkCodes != null && vkCodes.Length > 0)
            SendKeyCombo(vkCodes);
    }

    private static ushort[]? ParseKeyString(string keyString)
    {
        var keyMap = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            ["ctrl"] = 0x11, ["alt"] = 0x12, ["shift"] = 0x10, ["win"] = 0x5B,
            ["enter"] = 0x0D, ["esc"] = 0x1B, ["tab"] = 0x09, ["space"] = 0x20,
            ["back"] = 0x08, ["del"] = 0x2E, ["ins"] = 0x2D,
            ["home"] = 0x24, ["end"] = 0x23, ["pgup"] = 0x21, ["pgdn"] = 0x22,
            ["up"] = 0x26, ["down"] = 0x28, ["left"] = 0x25, ["right"] = 0x27,
            ["f1"] = 0x70, ["f2"] = 0x71, ["f3"] = 0x72, ["f4"] = 0x73,
            ["f5"] = 0x74, ["f6"] = 0x75, ["f7"] = 0x76, ["f8"] = 0x77,
            ["f9"] = 0x78, ["f10"] = 0x79, ["f11"] = 0x7A, ["f12"] = 0x7B,
        };

        if (keyMap.TryGetValue(keyString, out ushort vk))
            return new[] { vk };

        if (keyString.Length == 1)
        {
            short result = VkKeyScan(keyString[0]);
            if (result != -1)
                return new[] { (ushort)(result & 0xFF) };
        }

        return null;
    }

    private static void SendKeyCombo(ushort[] vkCodes)
    {
        var inputs = new List<NativeMethods.INPUT>();
        foreach (var vk in vkCodes)
            inputs.Add(MakeKeyDown(vk));
        for (int i = vkCodes.Length - 1; i >= 0; i--)
            inputs.Add(MakeKeyUp(vkCodes[i]));

        int size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>();
        NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), size);
    }

    private static NativeMethods.INPUT MakeKeyDown(ushort vk) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new() { ki = new() { wVk = vk } }
    };

    private static NativeMethods.INPUT MakeKeyUp(ushort vk) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new() { ki = new() { wVk = vk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
    };

    /// <summary>
    /// 使用 Unicode 扫描码发送文本
    /// </summary>
    private static void SendUnicodeText(string text)
    {
        var inputs = new List<NativeMethods.INPUT>();
        foreach (char c in text)
        {
            inputs.Add(new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new() { ki = new() { wScan = c, dwFlags = NativeMethods.KEYEVENTF_UNICODE } }
            });
            inputs.Add(new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new() { ki = new() { wScan = c, dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP } }
            });
        }

        if (inputs.Count > 0)
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>();
            NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), size);
        }
    }

    /// <summary>
    /// 解析 {键名} 格式
    /// </summary>
    private static List<(bool IsKey, string Value)> Tokenize(string input)
    {
        var result = new List<(bool, string)>();
        int i = 0;

        while (i < input.Length)
        {
            if (input[i] == '{')
            {
                int end = input.IndexOf('}', i);
                if (end > i)
                {
                    result.Add((true, input.Substring(i + 1, end - i - 1)));
                    i = end + 1;
                }
                else
                {
                    i++;
                }
            }
            else
            {
                int next = input.IndexOf('{', i);
                string text = next < 0 ? input[i..] : input[i..next];
                if (!string.IsNullOrEmpty(text))
                    result.Add((false, text));
                i = next < 0 ? input.Length : next;
            }
        }

        return result;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);
}
