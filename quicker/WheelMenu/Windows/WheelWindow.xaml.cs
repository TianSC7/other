namespace WheelMenu.Windows;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using WheelMenu.Config;
using WheelMenu.Logic.Actions;
using WheelMenu.Logic.Context;
using WheelMenu.Logic.Win32;
using WheelMenu.Renderer;

public partial class WheelWindow : Window
{
    // ===== Win32 P/Invoke：设置鼠标穿透 =====
    private const int GWL_EXSTYLE      = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED    = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private IntPtr _hwnd;

    // ===== 鼠标钩子 =====
    private IntPtr _mouseHook = IntPtr.Zero;
    private NativeMethods.HookProc? _mouseHookProc;

    // ===== 键盘钩子（用于F1重复触发）=====
    private IntPtr _keyboardHook = IntPtr.Zero;
    private NativeMethods.HookProc? _keyboardHookProc;

    // ===== 触发状态 =====
    private bool _isTriggerPressed = false;
    private Point _triggerPressPos = new Point(0, 0);
    private bool _hasMovedSincePress = false;
    private bool _isWheelVisible = false;
    private string _currentHighlightRing = string.Empty;
    private int _currentHighlightSector = -1;

    // ===== 超时检测 =====
    private DispatcherTimer? _timeoutTimer;
    private Stopwatch _pressStopwatch = new Stopwatch();

    // ===== 配置 =====
    private AppConfig _config = ConfigService.LoadConfig();

    // ===== 模块四：场景上下文 =====
    private ForegroundWatcher? _foregroundWatcher;
    private SceneResolver? _sceneResolver;
    private string? _lastSceneProcess;

    // ===== 虚拟键V1二段触发（可选）=====
    private bool _v1TriggerActive = false;
    private DateTime _v1TriggerTime = DateTime.MinValue;

    public WheelWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        // 初始状态：全屏透明穿透（等待显示时再取消穿透）
        EnableMousePassThrough(true);
        
        // 从配置加载圆盘尺寸
        var triggerSettings = _config.TriggerSettings;
        if (triggerSettings != null)
        {
            Renderer.WheelConstants.UpdateFromSettings(triggerSettings);
        }
        
        // 初始化钩子
        InitializeHooks();
        
        // 初始化超时定时器
        InitializeTimeoutTimer();
        
        // 初始化场景上下文监听（模块四）
        InitializeSceneContext();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // 清理钩子
        UninstallHooks();
        _timeoutTimer?.Stop();
        // 清理场景监听
        _foregroundWatcher?.Stop();
        _foregroundWatcher?.Dispose();
    }

    // ===== 模块四：场景上下文初始化 =====
    private void InitializeSceneContext()
    {
        _foregroundWatcher = new ForegroundWatcher();
        _sceneResolver = new SceneResolver(_foregroundWatcher);
        
        // 监听前台窗口变化
        _foregroundWatcher.ForegroundProcessChanged += OnForegroundProcessChanged;
        
        // 启动监听
        _foregroundWatcher.Start();
        
        Debug.WriteLine($"[WheelWindow] 场景监听已启动，当前前台: {_foregroundWatcher.CurrentProcessName}");
    }

    private void OnForegroundProcessChanged(object? sender, string? processName)
    {
        Debug.WriteLine($"[WheelWindow] 前台进程变化: {processName}");
        _lastSceneProcess = processName;
    }

    // ===== 钩子初始化 =====

    private void InitializeHooks()
    {
        _mouseHookProc = MouseHookCallback;
        _keyboardHookProc = KeyboardHookCallback;

        IntPtr moduleHandle = NativeMethods.GetModuleHandle(null);
        
        // 安装低级鼠标钩子
        _mouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _mouseHookProc,
            moduleHandle,
            0);

        if (_mouseHook == IntPtr.Zero)
        {
            Debug.WriteLine("Failed to install mouse hook");
        }

        // 安装低级键盘钩子（用于F1重复触发）
        _keyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _keyboardHookProc,
            moduleHandle,
            0);

        if (_keyboardHook == IntPtr.Zero)
        {
            Debug.WriteLine("Failed to install keyboard hook");
        }
    }

    private void UninstallHooks()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    // ===== 鼠标钩子回调 =====

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            Point mousePos = new Point(hookStruct.pt.X, hookStruct.pt.Y);

            // 检查是否是触发键
            if (IsTriggerButton(msg, hookStruct.mouseData))
            {
                HandleTriggerButton(msg, mousePos);
                return new IntPtr(1); // 拦截触发键事件
            }

            // 检查是否是右键（用于二段触发）
            if (msg == NativeMethods.WM_RBUTTONDOWN && _v1TriggerActive)
            {
                // V1触发模式下，右键按下时弹出轮盘
                _v1TriggerActive = false;
                _isTriggerPressed = true;
                _triggerPressPos = mousePos;
                _hasMovedSincePress = true;
                ShowWheelAtPosition(mousePos);
                return new IntPtr(1); // 拦截右键事件
            }

            // 轮盘显示时，处理鼠标移动高亮
            if (_isWheelVisible && msg == NativeMethods.WM_MOUSEMOVE)
            {
                UpdateHighlight(mousePos);
            }
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    // ===== 键盘钩子回调（用于F1重复触发）=====

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isWheelVisible)
        {
            int msg = wParam.ToInt32();
            var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            // 检查是否是重复触发键（默认F1）
            if (msg == NativeMethods.WM_KEYDOWN)
            {
                string repeatKey = _config.TriggerSettings.RepeatTriggerKey;
                if (IsKeyMatch(hookStruct.vkCode, repeatKey))
                {
                    // 执行当前高亮扇区的动作
                    ExecuteCurrentAction();
                    return new IntPtr(1); // 拦截按键事件
                }
            }
        }

        return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    // ===== 触发键处理 =====

    private bool IsTriggerButton(int msg, uint mouseData)
    {
        string triggerKey = _config.TriggerSettings.TriggerKey;
        
        return triggerKey switch
        {
            "middle" => msg == NativeMethods.WM_MBUTTONDOWN || msg == NativeMethods.WM_MBUTTONUP,
            "x1" => msg == NativeMethods.WM_XBUTTONDOWN && (mouseData >> 16) == NativeMethods.XBUTTON1,
            "x2" => msg == NativeMethods.WM_XBUTTONDOWN && (mouseData >> 16) == NativeMethods.XBUTTON2,
            "side_scroll" => msg == NativeMethods.WM_MOUSEWHEEL && (short)(mouseData >> 16) != 0,
            _ => false
        };
    }

    private void HandleTriggerButton(int msg, Point mousePos)
    {
        if (msg == NativeMethods.WM_MBUTTONDOWN || msg == NativeMethods.WM_XBUTTONDOWN)
        {
            // 触发键按下
            _isTriggerPressed = true;
            _triggerPressPos = mousePos;
            _hasMovedSincePress = false;
            _pressStopwatch.Restart();
            
            // 启动超时定时器
            if (_config.TriggerSettings.TimeoutMs > 0)
            {
                _timeoutTimer!.Interval = TimeSpan.FromMilliseconds(_config.TriggerSettings.TimeoutMs);
                _timeoutTimer.Start();
            }
        }
        else if (msg == NativeMethods.WM_MBUTTONUP || msg == NativeMethods.WM_XBUTTONUP)
        {
            // 触发键松开
            _isTriggerPressed = false;
            _timeoutTimer?.Stop();

            if (_isWheelVisible)
            {
                // 轮盘显示中，执行动作并关闭
                ExecuteCurrentAction();
                HideWheel();
            }
            else if (!_hasMovedSincePress)
            {
                // 没有移动，视为普通按键，透传给系统
                PassThroughTriggerButton();
            }
        }
        else if (msg == NativeMethods.WM_MOUSEMOVE && _isTriggerPressed)
        {
            // 检测鼠标移动
            double distance = Math.Sqrt(
                Math.Pow(mousePos.X - _triggerPressPos.X, 2) +
                Math.Pow(mousePos.Y - _triggerPressPos.Y, 2));

            if (distance > 5) // 移动超过5像素视为有移动
            {
                _hasMovedSincePress = true;
                
                if (!_isWheelVisible)
                {
                    // 首次移动，弹出轮盘
                    ShowWheelAtPosition(mousePos);
                }
                else
                {
                    // 轮盘已显示，更新高亮
                    UpdateHighlight(mousePos);
                }
            }
        }
    }

    // ===== 轮盘显示/隐藏 =====

    private void ShowWheelAtPosition(Point mousePos)
    {
        // 计算轮盘圆心（考虑屏幕边缘适配）
        var (center, moveCursorTo) = ScreenHelper.CalculateCenter(
            mousePos, _config.TriggerSettings.EdgeConstrainMode);

        // 显示轮盘
        ShowWheel(center);
        _isWheelVisible = true;
        
        // 更新显示选项
        WheelPreviewCanvas.SetDisplayOptions(
            _config.TriggerSettings.HideLabelWhenIcon,
            _config.TriggerSettings.OuterRing16Mode);

        // 模式C（限定+移指针）：移动鼠标指针到新圆心位置
        if (moveCursorTo.HasValue)
        {
            MoveCursorTo(moveCursorTo.Value);
        }
    }

    /// <summary>
    /// 移动鼠标指针到指定位置
    /// </summary>
    private void MoveCursorTo(Point position)
    {
        // 将逻辑像素转换为物理像素（SetCursorPos 使用物理像素）
        double dpiX = 96.0, dpiY = 96.0;
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget != null)
        {
            dpiX = 96.0 * src.CompositionTarget.TransformToDevice.M11;
            dpiY = 96.0 * src.CompositionTarget.TransformToDevice.M22;
        }
        
        int physicalX = (int)(position.X * dpiX / 96.0);
        int physicalY = (int)(position.Y * dpiY / 96.0);
        
        NativeMethods.SetCursorPos(physicalX, physicalY);
    }

    // ===== 高亮更新 =====

    private void UpdateHighlight(Point mousePos)
    {
        if (!_isWheelVisible) return;

        // 获取当前圆心
        Point center = WheelPreviewCanvas.GetCenter();
        
        // 计算扇区
        int totalSectors = _config.TriggerSettings.OuterRing16Mode ? 16 : 8;
        var (ring, sector) = WheelGeometry.HitTestMainWheel(
            center.X, center.Y, mousePos.X, mousePos.Y, totalSectors,
            false); // 环1使用22.5度，环2使用33.75度

        // 更新高亮
        if (ring != "dead" && ring != "extended")
        {
            WheelPreviewCanvas.SetHighlight(ring, sector);
            _currentHighlightRing = ring;
            _currentHighlightSector = sector;
        }
        else
        {
            WheelPreviewCanvas.ClearHighlight();
            _currentHighlightRing = string.Empty;
            _currentHighlightSector = -1;
        }
    }

    // ===== 动作执行 =====

    private void ExecuteCurrentAction()
    {
        if (string.IsNullOrEmpty(_currentHighlightRing) || _currentHighlightSector < 0)
            return;

        // 获取当前场景配置
        var scene = _config.GlobalScene;
        WheelMenu.Config.SlotConfig? slot = null;

        if (_currentHighlightRing == "inner" && _currentHighlightSector < scene.InnerRing.Length)
        {
            slot = scene.InnerRing[_currentHighlightSector];
        }
        else if (_currentHighlightRing == "outer" && _currentHighlightSector < scene.OuterRing.Length)
        {
            slot = scene.OuterRing[_currentHighlightSector];
        }

        // 执行动作（模拟实现，后续由模块四完善）
        if (slot != null && !string.IsNullOrEmpty(slot.ActionType))
        {
            ExecuteAction(slot);
        }
    }

    private void ExecuteAction(WheelMenu.Config.SlotConfig slot)
    {
        if (string.IsNullOrEmpty(slot.ActionType) || slot.ActionType == "none")
            return;

        try
        {
            // 使用动作执行器执行动作
            var executor = new ActionExecutor();
            executor.Execute(slot.ActionType, slot.ActionValue, slot.Label, slot.IconPath);
            
            Debug.WriteLine($"执行动作: {slot.ActionType} - {slot.ActionValue}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"执行动作失败: {ex.Message}");
        }
    }

    // ===== 透传触发键 =====

    private void PassThroughTriggerButton()
    {
        // 模拟原始按键事件，透传给系统
        string triggerKey = _config.TriggerSettings.TriggerKey;
        
        // 使用SendInput模拟按键
        switch (triggerKey)
        {
            case "middle":
                SimulateMouseClick(NativeMethods.WM_MBUTTONDOWN, NativeMethods.WM_MBUTTONUP);
                break;
            case "x1":
                SimulateXButtonClick(NativeMethods.XBUTTON1);
                break;
            case "x2":
                SimulateXButtonClick(NativeMethods.XBUTTON2);
                break;
            // side_scroll 不需要透传，因为滚轮事件已经被处理
        }
    }

    private void SimulateMouseClick(int downMsg, int upMsg)
    {
        // TODO: 使用SendInput模拟鼠标点击
        // 后续实现
    }

    private void SimulateXButtonClick(int xButton)
    {
        // TODO: 使用SendInput模拟X按钮点击
        // 后续实现
    }

    // ===== 键盘匹配 =====

    private bool IsKeyMatch(uint vkCode, string keyName)
    {
        return keyName.ToUpper() switch
        {
            "F1" => vkCode == 0x70,
            "F2" => vkCode == 0x71,
            "F3" => vkCode == 0x72,
            "F4" => vkCode == 0x73,
            "F5" => vkCode == 0x74,
            "F6" => vkCode == 0x75,
            "F7" => vkCode == 0x76,
            "F8" => vkCode == 0x77,
            "F9" => vkCode == 0x78,
            "F10" => vkCode == 0x79,
            "F11" => vkCode == 0x7A,
            "F12" => vkCode == 0x7B,
            _ => false
        };
    }

    // ===== 超时检测 =====

    private void InitializeTimeoutTimer()
    {
        _timeoutTimer = new DispatcherTimer();
        _timeoutTimer.Tick += OnTimeout;
    }

    private void OnTimeout(object? sender, EventArgs e)
    {
        _timeoutTimer?.Stop();
        
        if (_isWheelVisible)
        {
            // 超时关闭轮盘，不执行动作
            HideWheel();
        }
        
        _isTriggerPressed = false;
        _hasMovedSincePress = false;
    }

    // ===== 鼠标穿透 =====

    /// <summary>
    /// true = 鼠标穿透整个窗口（圆盘隐藏时）
    /// false = 正常响应鼠标（圆盘显示时）
    /// </summary>
    public void EnableMousePassThrough(bool passThrough)
    {
        int style = GetWindowLong(_hwnd, GWL_EXSTYLE);
        if (passThrough)
            style |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE;
        else
            style &= ~WS_EX_TRANSPARENT;
        SetWindowLong(_hwnd, GWL_EXSTYLE, style);
    }

    /// <summary>
    /// 显示圆盘：设置圆心位置，取消鼠标穿透
    /// </summary>
    public void ShowWheel(Point screenCenter)
    {
        WheelPreviewCanvas.SetCenter(screenCenter);
        EnableMousePassThrough(false);
        Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 隐藏圆盘：恢复鼠标穿透
    /// </summary>
    public void HideWheel()
    {
        EnableMousePassThrough(true);
        Visibility = Visibility.Hidden;
        _isWheelVisible = false;
        WheelPreviewCanvas.ClearHighlight();
        _currentHighlightRing = string.Empty;
        _currentHighlightSector = -1;
    }
}
