using System.Collections.Generic;
using System.Linq;
using MacroPlayer.Core;
using MacroPlayer.Models;
using MacroPlayer.UI;

namespace MacroPlayer;

/// <summary>
/// 应用程序上下文，管理托盘图标和应用程序生命周期
/// </summary>
public class AppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly HotkeyManager _hotkeyManager;
    private MainForm? _mainForm;
    private CancellationTokenSource _cts = new();
    private bool _isPlaying = false;
    private DateTime _lastHotkeyTime = DateTime.MinValue;
    private const int HOTKEY_COOLDOWN_MS = 200; // 冷却时间，防止快速连续触发
    private bool _isPaused = false; // 是否暂停

    /// <summary>
    /// 获取应用程序基目录
    /// </summary>
    public static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

    public AppContext()
    {
        Logger.Clear(); // 清空旧日志
        Logger.Info("应用程序启动");
        
        // 1. 加载配置
        ConfigManager.Load();
        Logger.Info($"配置加载完成，共 {ConfigManager.Current.Macros.Count} 个宏");

        // 2. 初始化托盘图标
        var customIcon = IconGenerator.CreateTrayIcon();
        _tray = new NotifyIcon
        {
            Icon = customIcon,
            Text = "键盘宏播放器",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _tray.DoubleClick += (_, _) => ShowMainForm();
        Logger.Info("托盘图标初始化完成");

        // 3. 注册热键
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.OnHotkeyTriggered += OnHotkeyTriggered;
        RegisterAllHotkeys();

        // 4. 按配置决定是否显示窗口
        if (!ConfigManager.Current.StartMinimized)
            ShowMainForm();
        
        Logger.Info("应用程序初始化完成");
    }

    /// <summary>
    /// 构建托盘右键菜单
    /// </summary>
    /// <returns>上下文菜单</returns>
    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开编辑器", null, (_, _) => ShowMainForm());
        menu.Items.Add("重载配置", null, (_, _) => ReloadConfig());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_isPaused ? "启动热键" : "暂停热键", null, (_, _) => TogglePause());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());
        return menu;
    }

    /// <summary>
    /// 切换暂停/启动状态
    /// </summary>
    private void TogglePause()
    {
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            // 暂停：注销所有热键
            _hotkeyManager.UnregisterAll();
            _tray.Text = "键盘宏播放器 (已暂停)";
            _tray.ShowBalloonTip(2000, "宏播放器", "热键已暂停", ToolTipIcon.Info);
            Logger.Info("热键已暂停");
        }
        else
        {
            // 启动：重新注册所有热键
            RegisterAllHotkeys();
            _tray.Text = "键盘宏播放器";
            _tray.ShowBalloonTip(2000, "宏播放器", "热键已启动", ToolTipIcon.Info);
            Logger.Info("热键已启动");
        }
        // 重建菜单以更新显示
        _tray.ContextMenuStrip = BuildTrayMenu();
    }

    /// <summary>
    /// 显示主窗体
    /// </summary>
    private void ShowMainForm()
    {
        if (_mainForm == null || _mainForm.IsDisposed)
            _mainForm = new MainForm();
        _mainForm.Show();
        _mainForm.BringToFront();
    }

    /// <summary>
    /// 重载配置
    /// </summary>
    private void ReloadConfig()
    {
        ConfigManager.Load();
        _hotkeyManager.UnregisterAll();  // 先注销所有旧热键
        RegisterAllHotkeys();
        if (_mainForm != null)
        {
            _mainForm.LoadGrid();
        }
        Logger.Info("配置已重载");
    }

    /// <summary>
    /// 注册所有热键
    /// </summary>
    private void RegisterAllHotkeys()
    {
        Logger.Info("开始注册所有热键");
        int registeredCount = 0;
        var failedHotkeys = new List<(string hotkey, string name)>();
        
        foreach (var macro in ConfigManager.Current.Macros)
        {
            if (macro.Enabled && !string.IsNullOrEmpty(macro.Hotkey))
            {
                if (_hotkeyManager.Register(macro.Hotkey))
                    registeredCount++;
                else
                    failedHotkeys.Add((macro.Hotkey, macro.Name));
            }
            else
            {
                Logger.Info($"跳过宏: {macro.Name} (启用: {macro.Enabled}, 热键: {macro.Hotkey})");
            }
        }
        
        Logger.Info($"热键注册完成，成功注册 {registeredCount} 个");
        
        // 显示热键冲突提示
        if (failedHotkeys.Count > 0)
        {
            var message = $"以下热键注册失败，可能已被其他程序占用：\n\n";
            message += string.Join("\n", failedHotkeys.Select(f => $"• {f.hotkey} ({f.name})"));
            MessageBox.Show(message, "热键冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// 热键触发事件处理
    /// </summary>
    /// <param name="hotkey">触发的热键</param>
    private void OnHotkeyTriggered(string hotkey)
    {
        Logger.Info($"热键事件处理: {hotkey}");
        
        // 检查是否暂停
        if (_isPaused)
        {
            Logger.Info("热键已暂停，忽略本次触发");
            return;
        }
        
        var macro = ConfigManager.Current.Macros
            .FirstOrDefault(m => m.Hotkey.Equals(hotkey, StringComparison.OrdinalIgnoreCase)
                                  && m.Enabled);
        if (macro == null)
        {
            Logger.Warning($"未找到对应的热键宏: {hotkey}");
            return;
        }

        Logger.Info($"找到宏: {macro.Name}, 开始播放");
        
        // 检查冷却时间
        var timeSinceLastHotkey = (DateTime.Now - _lastHotkeyTime).TotalMilliseconds;
        if (timeSinceLastHotkey < HOTKEY_COOLDOWN_MS)
        {
            Logger.Info($"热键冷却中，跳过本次触发");
            return;
        }

        _lastHotkeyTime = DateTime.Now;
        
        // 取消上一次未完成的播放，开始新的
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        
        Task.Run(async () =>
        {
            _isPlaying = true;
            try
            {
                await Core.MacroPlayer.PlayAsync(macro, ConfigManager.Current.KeyDownDuration);
            }
            finally
            {
                _isPlaying = false;
            }
        });
    }

    /// <summary>
    /// 退出应用程序
    /// </summary>
    private void ExitApplication()
    {
        Logger.Info("应用程序退出");
        _isPlaying = false;
        _cts.Cancel();
        _hotkeyManager.Dispose();
        _tray.Visible = false;
        Application.Exit();
    }
}
