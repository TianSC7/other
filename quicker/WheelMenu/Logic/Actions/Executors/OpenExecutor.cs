namespace WheelMenu.Logic.Actions.Executors;

using WheelMenu.Logic.Win32;

/// <summary>
/// 打开执行器 - 使用 ShellExecute 打开文件/文件夹/网址/命令
/// </summary>
public class OpenExecutor : IActionExecutor
{
    public void Execute(string actionType, string actionValue, string? label = null, string? iconPath = null)
    {
        if (string.IsNullOrEmpty(actionValue))
            return;

        try
        {
            // 展开环境变量
            string path = Environment.ExpandEnvironmentVariables(actionValue);

            // 使用 ShellExecute 打开
            NativeMethods.ShellExecute(
                IntPtr.Zero,  // hwnd
                "open",       // operation
                path,         // file
                null,         // parameters
                null,         // directory
                NativeMethods.SW_SHOW); // showCmd
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpenExecutor] 打开失败: {ex.Message}");
            System.Windows.MessageBox.Show($"无法打开：{actionValue}\n{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }
}
