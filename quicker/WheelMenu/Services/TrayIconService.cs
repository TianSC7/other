namespace WheelMenu.Services;

using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

public class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly Action _onExit;

    public TrayIconService(Action onExit)
    {
        _onExit = onExit;

        // 创建托盘图标
        _notifyIcon = new NotifyIcon
        {
            Text = "轮盘菜单",
            Icon = GetDefaultIcon(),
            Visible = true
        };

        // 创建右键菜单
        _contextMenu = new ContextMenuStrip();
        var showTestItem = new ToolStripMenuItem("打开测试窗口");
        showTestItem.Click += (s, e) => ShowTestWindow?.Invoke();
        _contextMenu.Items.Add(showTestItem);

        var showSettingsItem = new ToolStripMenuItem("打开设置");
        showSettingsItem.Click += (s, e) => ShowSettingsWindow?.Invoke();
        _contextMenu.Items.Add(showSettingsItem);

        var separator = new ToolStripSeparator();
        _contextMenu.Items.Add(separator);

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (s, e) => _onExit?.Invoke();
        _contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = _contextMenu;
    }

    public Action? ShowTestWindow { get; set; }
    public Action? ShowSettingsWindow { get; set; }

    private Icon GetDefaultIcon()
    {
        // 创建简单的托盘图标（蓝色圆形）
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.FillEllipse(new SolidBrush(Color.FromArgb(30, 120, 255)), 0, 0, 16, 16);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Show()
    {
        _notifyIcon.Visible = true;
    }

    public void Hide()
    {
        _notifyIcon.Visible = false;
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();
    }
}
