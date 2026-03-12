namespace WheelMenu;

using System.Windows;
using WheelMenu.Services;

public partial class App
{
    private TrayIconService? _trayIcon;
    private Windows.TestWindow? _testWindow;
    private Windows.SettingsWindow? _settingsWindow;

    public App()
    {
        // 应用启动时初始化托盘
        _trayIcon = new TrayIconService(() => System.Windows.Application.Current.Shutdown());
        _trayIcon.ShowTestWindow = () =>
        {
            if (_testWindow == null)
            {
                _testWindow = new Windows.TestWindow();
                _testWindow.Closed += (s, args) => _testWindow = null;
                _testWindow.Show();
            }
            else
            {
                _testWindow.Activate();
            }
        };
        _trayIcon.ShowSettingsWindow = () =>
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new Windows.SettingsWindow();
                _settingsWindow.Closed += (s, args) => _settingsWindow = null;
                _settingsWindow.Show();
            }
            else
            {
                _settingsWindow.Activate();
            }
        };
    }
}
