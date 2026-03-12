# Win32 SendInput 与键盘模拟  技术实现指南（.NET 8.0）

> 本文档解决 Phase 3 动作执行层中**最容易出错的技术点**：
> SendInput 的正确用法、输入法兼容、焦点问题、权限限制、各类边界情况。

---

## 一、三种键盘输入 API 对比

| API | 方式 | 推荐场景 | 注意事项 |
|-----|------|---------|---------|
| `SendInput` | 注入到系统输入队列 | **所有场景首选** | 必须在目标窗口有焦点时发送 |
| `keybd_event` | 旧版，已废弃 | 不使用 | 不支持 Unicode，忽略 |
| `PostMessage(WM_KEYDOWN)` | 直接发送到特定窗口消息队列 | 目标窗口无焦点时 | 部分应用忽略此类消息（Chrome等） |

**结论：本项目统一使用 `SendInput`，配合 50ms 焦点恢复延迟。**

---

## 二、SendInput 完整封装

### 2.1  虚拟键码参考表

```csharp
// 常用虚拟键码（VK）
// 完整列表：https://docs.microsoft.com/windows/win32/inputdev/virtual-key-codes
public static class VirtualKeys
{
    public const ushort VK_LBUTTON  = 0x01;
    public const ushort VK_BACK     = 0x08;  // Backspace
    public const ushort VK_TAB      = 0x09;
    public const ushort VK_RETURN   = 0x0D;  // Enter
    public const ushort VK_SHIFT    = 0x10;
    public const ushort VK_CONTROL  = 0x11;
    public const ushort VK_MENU     = 0x12;  // Alt
    public const ushort VK_PAUSE    = 0x13;
    public const ushort VK_CAPITAL  = 0x14;  // CapsLock
    public const ushort VK_ESCAPE   = 0x1B;
    public const ushort VK_SPACE    = 0x20;
    public const ushort VK_PRIOR    = 0x21;  // PageUp
    public const ushort VK_NEXT     = 0x22;  // PageDown
    public const ushort VK_END      = 0x23;
    public const ushort VK_HOME     = 0x24;
    public const ushort VK_LEFT     = 0x25;
    public const ushort VK_UP       = 0x26;
    public const ushort VK_RIGHT    = 0x27;
    public const ushort VK_DOWN     = 0x28;
    public const ushort VK_INSERT   = 0x2D;
    public const ushort VK_DELETE   = 0x2E;

    // 数字行 0~9
    public const ushort VK_0 = 0x30;   // VK_1 ~ VK_9 = 0x31 ~ 0x39

    // 字母 A~Z
    public const ushort VK_A = 0x41;   // VK_B ~ VK_Z = 0x42 ~ 0x5A

    // 小键盘
    public const ushort VK_NUMPAD0   = 0x60;  // ~ VK_NUMPAD9 = 0x69
    public const ushort VK_MULTIPLY  = 0x6A;  // *
    public const ushort VK_ADD       = 0x6B;  // +
    public const ushort VK_SUBTRACT  = 0x6D;  // -
    public const ushort VK_DECIMAL   = 0x6E;  // .
    public const ushort VK_DIVIDE    = 0x6F;  // /

    // 功能键
    public const ushort VK_F1        = 0x70;  // ~ VK_F12 = 0x7B

    // 修饰键（左右分别）
    public const ushort VK_LSHIFT    = 0xA0;
    public const ushort VK_RSHIFT    = 0xA1;
    public const ushort VK_LCONTROL  = 0xA2;
    public const ushort VK_RCONTROL  = 0xA3;
    public const ushort VK_LMENU    = 0xA4;  // Left Alt
    public const ushort VK_RMENU    = 0xA5;  // Right Alt

    // 特殊
    public const ushort VK_LWIN     = 0x5B;
    public const ushort VK_RWIN     = 0x5C;

    // 扩展键标志（某些键需要 KEYEVENTF_EXTENDEDKEY）
    // 包括：右 Ctrl、右 Alt、Insert、Delete、Home、End、PageUp、PageDown、方向键、小键盘 /、小键盘 Enter
}
```

### 2.2  InputHelper 封装类

```csharp
// Logic/Actions/InputHelper.cs
namespace WheelMenu.Logic.Actions;

using System.Runtime.InteropServices;
using WheelMenu.Logic.Win32;

/// <summary>
/// SendInput 的高级封装，提供按键组合和 Unicode 文本输入。
/// 所有方法均为静态，线程安全（SendInput 本身是线程安全的）。
/// </summary>
public static class InputHelper
{
    private static readonly int InputSize =
        Marshal.SizeOf<NativeMethods.INPUT>();

    // ===== 按键组合（如 Ctrl+C）=====

    /// <summary>
    /// 发送按键组合。
    /// 先按下所有修饰键，再按下主键，再逆序松开所有键。
    /// </summary>
    public static void SendKeyCombo(params ushort[] vkCodes)
    {
        if (vkCodes.Length == 0) return;

        var inputs = new NativeMethods.INPUT[vkCodes.Length * 2];

        // 按下（顺序）
        for (int i = 0; i < vkCodes.Length; i++)
            inputs[i] = MakeKeyDown(vkCodes[i]);

        // 松开（逆序）
        for (int i = 0; i < vkCodes.Length; i++)
            inputs[vkCodes.Length + i] = MakeKeyUp(vkCodes[vkCodes.Length - 1 - i]);

        SendInputs(inputs);
    }

    /// <summary>
    /// 解析字符串格式的按键组合并发送。
    /// 支持格式："Ctrl+C"、"Alt+F4"、"Ctrl+Shift+S"、"F5"
    /// </summary>
    public static bool TrySendKeyString(string keyString)
    {
        var vks = ParseKeyString(keyString);
        if (vks == null) return false;
        SendKeyCombo(vks);
        return true;
    }

    // ===== Unicode 文本输入 =====

    /// <summary>
    /// 使用 Unicode 扫描码输入文本，不受输入法状态影响。
    /// 适用于：直接粘贴、插入固定文本。
    /// 注意：部分老旧应用可能不支持 Unicode 输入事件。
    /// </summary>
    public static void SendUnicodeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var inputs = new List<NativeMethods.INPUT>(text.Length * 2);
        foreach (char c in text)
        {
            // 处理代理对（Emoji 等 BMP 以外字符）
            if (char.IsHighSurrogate(c) || char.IsLowSurrogate(c))
            {
                // Unicode 代理对：逐个发送 wScan
                inputs.Add(MakeUnicodeKeyDown(c));
                inputs.Add(MakeUnicodeKeyUp(c));
            }
            else
            {
                inputs.Add(MakeUnicodeKeyDown(c));
                inputs.Add(MakeUnicodeKeyUp(c));
            }
        }
        SendInputs(inputs.ToArray());
    }

    // ===== 键名字符串解析 =====

    private static readonly Dictionary<string, ushort> KeyNameMap =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // 修饰键
        ["ctrl"]      = 0x11, ["control"] = 0x11,
        ["alt"]       = 0x12, ["menu"]    = 0x12,
        ["shift"]     = 0x10,
        ["win"]       = 0x5B, ["windows"] = 0x5B,

        // 特殊键
        ["enter"]     = 0x0D, ["return"]  = 0x0D,
        ["esc"]       = 0x1B, ["escape"]  = 0x1B,
        ["tab"]       = 0x09,
        ["space"]     = 0x20,
        ["back"]      = 0x08, ["backspace"]= 0x08,
        ["del"]       = 0x2E, ["delete"]  = 0x2E,
        ["ins"]       = 0x2D, ["insert"]  = 0x2D,
        ["home"]      = 0x24,
        ["end"]       = 0x23,
        ["pgup"]      = 0x21, ["pageup"]  = 0x21,
        ["pgdn"]      = 0x22, ["pagedown"]= 0x22,
        ["up"]        = 0x26,
        ["down"]      = 0x28,
        ["left"]      = 0x25,
        ["right"]     = 0x27,
        ["capslock"]  = 0x14,
        ["numlock"]   = 0x90,
        ["scrolllock"]= 0x91,
        ["printscreen"]=0x2C,
        ["pause"]     = 0x13,
        ["apps"]      = 0x5D,  // 右键菜单键

        // 功能键
        ["f1"]  = 0x70, ["f2"]  = 0x71, ["f3"]  = 0x72, ["f4"]  = 0x73,
        ["f5"]  = 0x74, ["f6"]  = 0x75, ["f7"]  = 0x76, ["f8"]  = 0x77,
        ["f9"]  = 0x78, ["f10"] = 0x79, ["f11"] = 0x7A, ["f12"] = 0x7B,
    };

    /// <summary>
    /// 解析 "Ctrl+Shift+S" 格式，返回 VK 数组（顺序 = 先修饰键后主键）。
    /// 返回 null 表示解析失败。
    /// </summary>
    public static ushort[]? ParseKeyString(string keyString)
    {
        var parts  = keyString.Split('+', StringSplitOptions.TrimEntries);
        var result = new List<ushort>();

        foreach (var part in parts)
        {
            if (KeyNameMap.TryGetValue(part, out ushort vk))
            {
                result.Add(vk);
            }
            else if (part.Length == 1)
            {
                // 单字符：用 VkKeyScan 转换（考虑当前键盘布局）
                short scanResult = VkKeyScan(part[0]);
                if (scanResult == -1) return null;  // 无法映射
                ushort mainVk  = (ushort)(scanResult & 0xFF);
                byte   shiftState = (byte)((scanResult >> 8) & 0xFF);

                // 若需要 Shift 才能输入此字符（如 @ 需要 Shift+2），自动添加 Shift
                if ((shiftState & 0x01) != 0 && !result.Contains(0x10))
                    result.Insert(0, 0x10);  // 插入 Shift 到修饰键位置
                result.Add(mainVk);
            }
            else
            {
                return null;  // 未知键名
            }
        }

        return result.Count > 0 ? result.ToArray() : null;
    }

    // ===== 需要 EXTENDEDKEY 的键 =====

    // 这些键发送时需要加 KEYEVENTF_EXTENDEDKEY 标志，否则行为异常
    private static readonly HashSet<ushort> ExtendedKeys = new()
    {
        0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, // PageUp/Dn, End, Home, 方向键
        0x2D, 0x2E,   // Insert, Delete
        0x6F,         // 小键盘 /
        0xA3, 0xA5,   // 右 Ctrl, 右 Alt
    };

    // ===== 底层构造方法 =====

    private static NativeMethods.INPUT MakeKeyDown(ushort vk)
    {
        uint flags = 0;
        if (ExtendedKeys.Contains(vk)) flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U    = new() { ki = new() { wVk = vk, dwFlags = flags } }
        };
    }

    private static NativeMethods.INPUT MakeKeyUp(ushort vk)
    {
        uint flags = NativeMethods.KEYEVENTF_KEYUP;
        if (ExtendedKeys.Contains(vk)) flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U    = new() { ki = new() { wVk = vk, dwFlags = flags } }
        };
    }

    private static NativeMethods.INPUT MakeUnicodeKeyDown(char c) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U    = new() { ki = new()
        {
            wScan   = c,
            dwFlags = NativeMethods.KEYEVENTF_UNICODE
        }}
    };

    private static NativeMethods.INPUT MakeUnicodeKeyUp(char c) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U    = new() { ki = new()
        {
            wScan   = c,
            dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP
        }}
    };

    private static void SendInputs(NativeMethods.INPUT[] inputs)
    {
        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, InputSize);
        if (sent != inputs.Length)
        {
            int err = Marshal.GetLastWin32Error();
            // 错误码 5 = ACCESS_DENIED（管理员进程拦截）
            System.Diagnostics.Debug.WriteLine(
                $"[InputHelper] SendInput 仅发送 {sent}/{inputs.Length}，错误码 {err}");
        }
    }

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);
}
```

---

## 三、剪贴板操作（推荐替代直接输入）

对于中文、特殊符号等复杂文本，**剪贴板 + Ctrl+V 比 SendInput Unicode 更可靠**。

```csharp
// Logic/Actions/ClipboardHelper.cs
namespace WheelMenu.Logic.Actions;

public static class ClipboardHelper
{
    /// <summary>
    /// 将文本写入剪贴板，并发送 Ctrl+V 粘贴。
    /// 必须在 UI 线程调用 SetText，SendInput 可在任意线程。
    /// </summary>
    public static void PasteText(string text)
    {
        // 1. 保存原始剪贴板内容（可选，用于恢复）
        string? original = null;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            try { original = System.Windows.Clipboard.GetText(); } catch { }
            System.Windows.Clipboard.SetText(text);
        });

        // 2. 发送 Ctrl+V
        InputHelper.SendKeyCombo(0x11, 0x56);  // VK_CONTROL + V

        // 3. 延迟恢复原始剪贴板（可选，约 200ms 后）
        if (original != null)
        {
            Task.Delay(200).ContinueWith(_ =>
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try { System.Windows.Clipboard.SetText(original); }
                    catch { /* 恢复失败不影响功能 */ }
                }));
        }
    }

    /// <summary>
    /// 仅设置剪贴板，不发送粘贴（用于"粘贴内容"类型动作）。
    /// </summary>
    public static void SetClipboard(string text)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
            System.Windows.Clipboard.SetText(text));
        InputHelper.SendKeyCombo(0x11, 0x56);
    }
}
```

---

## 四、常见问题与解决方案

### 4.1  SendInput 发送的按键目标窗口是哪个？

```
答：SendInput 将按键注入到【当前获得键盘焦点的窗口】。
    不是指定目标窗口，而是系统当前焦点窗口。

这就是为什么：
  1. 轮盘窗口必须设置 WS_EX_NOACTIVATE，确保触发时原窗口保持焦点
  2. 动作执行需要 Task.Delay(50ms)，等待轮盘开始关闭动画后原窗口焦点恢复
  3. 若用户在触发和执行之间点击了其他窗口，动作会发送到新窗口（这是正常行为）
```

### 4.2  SendInput 发送 Ctrl+V 但没有粘贴

```
排查顺序：
  1. 检查剪贴板是否设置成功（Clipboard.GetText() 验证）
  2. 检查目标窗口是否有焦点（Alt+Tab 确认前台窗口）
  3. 检查 Task.Delay 是否足够（增大到 100ms 测试）
  4. 确认目标应用支持标准 Ctrl+V（部分应用如游戏可能拦截）
  5. 检查是否以管理员权限运行目标应用（见 4.3）
```

### 4.3  目标窗口是管理员权限进程时 SendInput 无效

```
问题：程序以标准用户权限运行，目标窗口（如 Process Explorer）以管理员权限运行。
     此时 SendInput 返回 0，错误码 5（ACCESS_DENIED）。

原因：Windows UAC 隔离，标准权限进程无法向管理员权限进程发送输入。

解决方案（按推荐度排序）：
  A. 以管理员权限运行本程序（在 app.manifest 中设置 requestedExecutionLevel=requireAdministrator）
     缺点：每次启动需要 UAC 确认弹窗
  B. 对管理员窗口改用 PostMessage(WM_KEYDOWN) 方式
     缺点：部分应用忽略此类消息
  C. 在 README 中说明此限制，建议用户管理员运行
     推荐：Phase 1 版本先记录限制，不做特殊处理
```

### 4.4  中文输入时乱码

```
问题：使用 SendInput VK 方式发送中文字符，结果是乱码。

原因：VK 码是针对英文键盘布局的，中文字符没有对应 VK 码。
     VkKeyScan('中') 返回 -1（无法映射）。

解决：
  A. 对于固定文本（"粘贴内容"/"发送文本"类型）：
     → 使用 ClipboardHelper.PasteText()，通过剪贴板传递，100% 准确
  B. 对于模拟输入（SimulateInput 类型）中的中文字符：
     → 普通中文文字直接用 ClipboardHelper.PasteText() 发送
     → {键名} 格式的按键仍用 SendInput
  C. 绝对不要用 KEYEVENTF_UNICODE 发送中文到输入法模式下的窗口：
     → 部分输入法会拦截 Unicode 事件，表现为输入法弹出候选框而非直接输入
```

### 4.5  Ctrl+V 粘贴到了错误位置

```
场景：用户触发轮盘后移动了鼠标并点击了其他地方，焦点切换了。
这是正常行为，不是 bug。

但若粘贴始终到错误位置：
  1. 检查 WheelWindow 是否在执行动作时意外获取了焦点
     → 加日志：Application.Current.MainWindow.IsActive
  2. 检查延迟时间是否不足（动画未结束就执行了 SendInput）
     → 将 Task.Delay(50) 增加到 Task.Delay(100) 测试
```

### 4.6  某些应用不响应 SendInput（如 DirectX 游戏）

```
原因：全屏 DirectX 应用通常通过 DirectInput 或 RawInput 接收输入，
     不走标准消息队列，SendInput 无效。

这是系统限制，不在本项目处理范围内。
在 README 中说明：游戏全屏模式下轮盘可弹出但动作可能无效。
```

---

## 五、SimulateInput 的 {键名} 序列完整解析实现

```csharp
// Executors/SimulateInputExecutor.cs（完整版）
public void Execute(SectorActionConfig action)
{
    var tokens = Tokenize(action.Value);

    foreach (var (isKey, value) in tokens)
    {
        if (isKey)
        {
            // {键名} → SendInput VK 方式
            if (!InputHelper.TrySendKeyString(value))
                System.Diagnostics.Debug.WriteLine($"[SimulateInput] 未知键名: {value}");
        }
        else if (!string.IsNullOrEmpty(value))
        {
            // 普通文字 → Unicode 方式（不受输入法影响）
            // 但中文等字符建议改用剪贴板，此处两种方式均可，视需求选择
            InputHelper.SendUnicodeText(value);
        }
    }
}

// 完整 Tokenize（处理转义 {{ 和 }}）
private static List<(bool IsKey, string Value)> Tokenize(string input)
{
    var result = new List<(bool, string)>();
    int i      = 0;
    var text   = new System.Text.StringBuilder();

    while (i < input.Length)
    {
        if (input[i] == '{')
        {
            // 处理转义 {{
            if (i + 1 < input.Length && input[i + 1] == '{')
            {
                text.Append('{');
                i += 2;
                continue;
            }

            // 先将已积累的普通文字入队
            if (text.Length > 0)
            {
                result.Add((false, text.ToString()));
                text.Clear();
            }

            int end = input.IndexOf('}', i + 1);
            if (end > i)
            {
                result.Add((true, input.Substring(i + 1, end - i - 1)));
                i = end + 1;
            }
            else
            {
                // 未闭合的 { 视为普通字符
                text.Append(input[i]);
                i++;
            }
        }
        else if (input[i] == '}' && i + 1 < input.Length && input[i + 1] == '}')
        {
            // 转义 }}
            text.Append('}');
            i += 2;
        }
        else
        {
            text.Append(input[i]);
            i++;
        }
    }

    if (text.Length > 0)
        result.Add((false, text.ToString()));

    return result;
}
```

---

## 六、动作执行时序图

```
[Phase 3 状态机：触发键松开]
    │
    ├─ 1. 记录 ring + sector（轮盘显示时）
    │
    ├─ 2. WheelAnimator.Close() 开始动画（80ms）
    │
    ├─ 3. Task.Delay(50ms)  ← 等待轮盘开始消失，原窗口焦点恢复
    │         ↓
    ├─ 4. ConfigService.ResolveSectorAction()  ← 解析要执行的动作
    │         ↓
    ├─ 5. ActionExecutor.Execute(action)  ← 分派到对应执行器
    │         ↓
    │   ┌─────────────────────────────────────────────┐
    │   │ Hotkey → InputHelper.TrySendKeyString()     │
    │   │ Paste  → ClipboardHelper.PasteText()        │
    │   │ Open   → ShellExecute()                     │
    │   │ ...                                         │
    │   └─────────────────────────────────────────────┘
    │
    └─ 6. 动画结束（约 30ms 后）→ WheelWindow.HideWheel()

时序：
  t=0ms    触发键松开
  t=0ms    关闭动画开始（80ms总时长）
  t=50ms   SendInput 执行（原窗口已恢复焦点）
  t=80ms   动画结束，窗口隐藏
```

---

## 七、调试技巧

### 7.1  确认 SendInput 是否成功发送

```csharp
// 在 InputHelper.SendInputs 中添加诊断日志
uint sent = NativeMethods.SendInput(...);
if (sent == 0)
{
    int err = Marshal.GetLastWin32Error();
    System.Diagnostics.Debug.WriteLine(
        $"SendInput 完全失败，Win32 错误码: {err}");
    // 常见错误码：
    //   5  = ACCESS_DENIED（目标是管理员进程）
    //   87 = INVALID_PARAMETER（INPUT 结构体大小错误）
}
```

### 7.2  验证 VkKeyScan 映射

```csharp
// 调试不同字符在当前键盘布局下的 VK 码
for (char c = 'A'; c <= 'Z'; c++)
{
    short result = VkKeyScan(c);
    ushort vk    = (ushort)(result & 0xFF);
    byte   mods  = (byte)((result >> 8) & 0xFF);
    Debug.WriteLine($"'{c}' → VK=0x{vk:X2}, NeedShift={mods & 1}");
}
```

### 7.3  用 Spy++ 或 WinSpy 验证消息

使用 Visual Studio 内置的 **Spy++**（`工具菜单 → Spy++`）监控目标窗口收到的 WM_KEYDOWN 消息，确认 SendInput 是否真正到达。
