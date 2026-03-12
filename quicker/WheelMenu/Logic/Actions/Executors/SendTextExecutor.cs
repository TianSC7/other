namespace WheelMenu.Logic.Actions.Executors;

/// <summary>
/// 发送文本执行器 - 支持从文件读取或直接发送文本
/// </summary>
public class SendTextExecutor : IActionExecutor
{
    public void Execute(string actionType, string actionValue, string? label = null, string? iconPath = null)
    {
        if (string.IsNullOrEmpty(actionValue))
            return;

        string text = actionValue;

        // 支持 FILE: 前缀从文件读取
        if (actionValue.StartsWith("FILE:", StringComparison.OrdinalIgnoreCase))
        {
            string filePath = actionValue[5..].Trim();
            // 展开环境变量
            filePath = Environment.ExpandEnvironmentVariables(filePath);

            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    text = System.IO.File.ReadAllText(filePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SendTextExecutor] 读取文件失败: {ex.Message}");
                    ShowError($"无法读取文件：{filePath}");
                    return;
                }
            }
            else
            {
                ShowError($"文件不存在：{filePath}");
                return;
            }
        }

        // 使用 PasteExecutor 发送文本
        PasteExecutor.SetClipboardText(text);

        // 延迟发送 Ctrl+V
        Task.Delay(50).ContinueWith(_ =>
        {
            SendCtrlV();
        });
    }

    private static void SendCtrlV()
    {
        var executor = new HotkeyExecutor();
        // 这里简化处理，直接发送 Ctrl+V
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

    private static void ShowError(string message)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show(message, "错误",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        });
    }
}
