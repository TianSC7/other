namespace WheelMenu.Logic.Actions.Executors;

/// <summary>
/// 日期时间执行器 - 解析格式字符串并插入日期时间
/// 支持 {0:格式字符串} 格式
/// </summary>
public class DateTimeExecutor : IActionExecutor
{
    public void Execute(string actionType, string actionValue, string? label = null, string? iconPath = null)
    {
        if (string.IsNullOrEmpty(actionValue))
            return;

        string result;

        try
        {
            // 格式字符串如 {0:yyyy-MM-dd HH:mm:ss}
            // 提取格式部分
            string format = actionValue;
            if (format.StartsWith("{0:") && format.Contains("}"))
            {
                int start = 3;
                int end = format.IndexOf('}');
                format = format[start..end];
            }

            result = string.Format("{" + format + "}", DateTime.Now);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DateTimeExecutor] 格式化失败: {ex.Message}");
            // 回退到默认格式
            result = DateTime.Now.ToString("yyyy-MM-dd");
        }

        // 使用 PasteExecutor 发送日期时间
        PasteExecutor.SetClipboardText(result);

        // 延迟发送 Ctrl+V
        Task.Delay(50).ContinueWith(_ =>
        {
            SendCtrlV();
        });
    }

    private static void SendCtrlV()
    {
        var inputs = new WheelMenu.Logic.Win32.NativeMethods.INPUT[]
        {
            new WheelMenu.Logic.Win32.NativeMethods.INPUT
            {
                type = WheelMenu.Logic.Win32.NativeMethods.INPUT_KEYBOARD,
                U = new WheelMenu.Logic.Win32.NativeMethods.INPUTUNION
                {
                    ki = new WheelMenu.Logic.Win32.NativeMethods.KEYBDINPUT { wVk = 0x11 }
                }
            },
            new WheelMenu.Logic.Win32.NativeMethods.INPUT
            {
                type = WheelMenu.Logic.Win32.NativeMethods.INPUT_KEYBOARD,
                U = new WheelMenu.Logic.Win32.NativeMethods.INPUTUNION
                {
                    ki = new WheelMenu.Logic.Win32.NativeMethods.KEYBDINPUT { wVk = 0x56 }
                }
            },
            new WheelMenu.Logic.Win32.NativeMethods.INPUT
            {
                type = WheelMenu.Logic.Win32.NativeMethods.INPUT_KEYBOARD,
                U = new WheelMenu.Logic.Win32.NativeMethods.INPUTUNION
                {
                    ki = new WheelMenu.Logic.Win32.NativeMethods.KEYBDINPUT { wVk = 0x56, dwFlags = WheelMenu.Logic.Win32.NativeMethods.KEYEVENTF_KEYUP }
                }
            },
            new WheelMenu.Logic.Win32.NativeMethods.INPUT
            {
                type = WheelMenu.Logic.Win32.NativeMethods.INPUT_KEYBOARD,
                U = new WheelMenu.Logic.Win32.NativeMethods.INPUTUNION
                {
                    ki = new WheelMenu.Logic.Win32.NativeMethods.KEYBDINPUT { wVk = 0x11, dwFlags = WheelMenu.Logic.Win32.NativeMethods.KEYEVENTF_KEYUP }
                }
            }
        };

        int size = System.Runtime.InteropServices.Marshal.SizeOf<WheelMenu.Logic.Win32.NativeMethods.INPUT>();
        WheelMenu.Logic.Win32.NativeMethods.SendInput((uint)inputs.Length, inputs, size);
    }
}
