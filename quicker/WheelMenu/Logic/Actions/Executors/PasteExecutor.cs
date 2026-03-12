namespace WheelMenu.Logic.Actions.Executors;

using WheelMenu.Logic.Win32;

/// <summary>
/// 粘贴执行器 - 将文本写入剪贴板并发送 Ctrl+V 粘贴
/// </summary>
public class PasteExecutor : IActionExecutor
{
    public void Execute(string actionType, string actionValue, string? label = null, string? iconPath = null)
    {
        if (string.IsNullOrEmpty(actionValue))
            return;

        // 设置剪贴板文本
        SetClipboardText(actionValue);

        // 发送 Ctrl+V
        Task.Delay(50).ContinueWith(_ =>
        {
            SendCtrlV();
        });
    }

    /// <summary>
    /// 设置剪贴板文本（必须在 UI 线程调用）
    /// </summary>
    public static void SetClipboardText(string text)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PasteExecutor] 设置剪贴板失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 发送 Ctrl+V 粘贴
    /// </summary>
    private static void SendCtrlV()
    {
        var inputs = new NativeMethods.INPUT[]
        {
            // Ctrl down
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new() { ki = new() { wVk = 0x11 } } // VK_CONTROL
            },
            // V down
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new() { ki = new() { wVk = 0x56 } } // VK_V
            },
            // V up
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new() { ki = new() { wVk = 0x56, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
            },
            // Ctrl up
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new() { ki = new() { wVk = 0x11, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
            }
        };

        int size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>();
        NativeMethods.SendInput((uint)inputs.Length, inputs, size);
    }
}
