# Phase 3 — 功能逻辑层  分步开发 SOP（.NET 8.0 / WPF）

> 所属总流程：`Quicker_轮盘_开发项目流程.md` → Phase 3
> 依赖：Phase 1 渲染层接口已锁定 · Phase 2 ConfigService 已可用
> **技术栈：.NET 8.0 + WPF + P/Invoke（Win32 低级鼠标钩子 + SendInput + ShellExecute）**
> 本阶段原则：**实现所有运行时行为，通过 Phase 1/2 的公开接口驱动渲染和读写配置，不修改已有代码。**

---

## 阶段目标与验收总标准

| 目标 | 验收判断 |
|------|---------|
| 鼠标钩子正确安装/卸载 | 按触发键可呼出圆盘，进程退出时钩子被清理 |
| 状态机完整 | 5 种状态转换均按预期工作 |
| 方向判断准确 | 8 方向高亮切换正确，死区不高亮 |
| 7 种动作类型全部可执行 | 每种类型单独测试通过 |
| 场景上下文切换正常 | 切换前台应用后轮盘内容变化 |
| F1 重复触发正常 | 按 F1 重复执行不关闭圆盘 |
| 超时取消正常 | 设置超时后不操作自动关闭 |

**全部通过后进入 Phase 4。**

---

## Step 3.0  项目结构

```
Logic/
  MouseHook/
    LowLevelMouseHook.cs    ← Win32 低级鼠标钩子封装
    MouseHookEventArgs.cs   ← 钩子事件参数
  StateMachine/
    WheelStateMachine.cs    ← 触发状态机（核心协调器）
    WheelState.cs           ← 状态枚举
  Actions/
    ActionExecutor.cs       ← 动作执行引擎（分派器）
    Executors/
      HotkeyExecutor.cs
      SimulateInputExecutor.cs
      PasteExecutor.cs
      OpenExecutor.cs
      RunActionExecutor.cs
      SendTextExecutor.cs
      DateTimeExecutor.cs
  Context/
    ForegroundWatcher.cs    ← 前台窗口变化监听
    SceneResolver.cs        ← 当前有效场景解析
  Win32/
    NativeMethods.cs        ← 所有 P/Invoke 声明（集中管理）
```

---

## Step 3.1  Win32 P/Invoke 声明（NativeMethods.cs）

所有 Win32 API 集中在此文件，其他类仅引用此处声明。

```csharp
// NativeMethods.cs
namespace WheelMenu.Logic.Win32;

using System.Runtime.InteropServices;

internal static class NativeMethods
{
    // ===== 鼠标钩子 =====
    public const int WH_MOUSE_LL = 14;
    public const int WM_MOUSEMOVE   = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP   = 0x0208;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int WM_XBUTTONUP   = 0x020C;
    public const int WM_MOUSEWHEEL  = 0x020A;

    public const int XBUTTON1 = 0x0001;   // X1（后退）
    public const int XBUTTON2 = 0x0002;   // X2（前进）

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT    pt;
        public uint     mouseData;
        public uint     flags;
        public uint     time;
        public IntPtr   dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    // ===== SendInput（模拟键鼠）=====
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP       = 0x0002;
    public const uint KEYEVENTF_UNICODE     = 0x0004;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint   type;
        public INPUTUNION U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int    dx; public int dy;
        public uint   mouseData; public uint dwFlags;
        public uint   time; public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // ===== 剪贴板 =====
    [DllImport("user32.dll")] public static extern bool OpenClipboard(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool CloseClipboard();
    [DllImport("user32.dll")] public static extern bool EmptyClipboard();
    [DllImport("user32.dll")] public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    public const uint CF_UNICODETEXT = 13;

    // ===== 前台窗口 =====
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // ===== 鼠标移动 =====
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    // ===== WinEvent Hook（前台窗口变化）=====
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT   = 0x0000;

    public delegate void WinEventProc(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    // ===== ShellExecute =====
    [DllImport("shell32.dll")]
    public static extern IntPtr ShellExecute(IntPtr hwnd, string lpOperation,
        string lpFile, string? lpParameters, string? lpDirectory, int nShowCmd);
    public const int SW_SHOW = 5;
}
```

---

## Step 3.2  低级鼠标钩子封装（LowLevelMouseHook.cs）

```csharp
// LowLevelMouseHook.cs
namespace WheelMenu.Logic.MouseHook;

using System.Diagnostics;
using System.Runtime.InteropServices;
using WheelMenu.Logic.Win32;

public class LowLevelMouseHook : IDisposable
{
    private IntPtr             _hookHandle = IntPtr.Zero;
    private NativeMethods.HookProc? _proc;   // 必须持有引用，防止 GC 回收

    public event EventHandler<MouseHookEventArgs>? MouseEvent;

    public void Install()
    {
        if (_hookHandle != IntPtr.Zero) return;
        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule  = curProcess.MainModule!;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _proc,
            NativeMethods.GetModuleHandle(curModule.ModuleName),
            0);
        if (_hookHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                $"安装鼠标钩子失败，错误码：{Marshal.GetLastWin32Error()}");
    }

    public void Uninstall()
    {
        if (_hookHandle == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            var args = new MouseHookEventArgs((int)wParam, info);

            MouseEvent?.Invoke(this, args);

            // 若事件被标记为已消费，不传给系统
            if (args.Handled)
                return new IntPtr(1);
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}

public class MouseHookEventArgs(int message, NativeMethods.MSLLHOOKSTRUCT info)
{
    public int     Message   { get; } = message;
    public int     X         { get; } = info.pt.X;
    public int     Y         { get; } = info.pt.Y;
    public uint    MouseData { get; } = info.mouseData;
    public bool    Handled   { get; set; } = false;

    // 便捷属性
    public bool IsMiddleDown => Message == NativeMethods.WM_MBUTTONDOWN;
    public bool IsMiddleUp   => Message == NativeMethods.WM_MBUTTONUP;
    public bool IsX1Down     => Message == NativeMethods.WM_XBUTTONDOWN
                             && (MouseData >> 16) == NativeMethods.XBUTTON1;
    public bool IsX1Up       => Message == NativeMethods.WM_XBUTTONUP
                             && (MouseData >> 16) == NativeMethods.XBUTTON1;
    public bool IsX2Down     => Message == NativeMethods.WM_XBUTTONDOWN
                             && (MouseData >> 16) == NativeMethods.XBUTTON2;
    public bool IsX2Up       => Message == NativeMethods.WM_XBUTTONUP
                             && (MouseData >> 16) == NativeMethods.XBUTTON2;
    public bool IsMove       => Message == NativeMethods.WM_MOUSEMOVE;
}
```

---

## Step 3.3  触发状态机（WheelStateMachine.cs）

状态机是 Phase 3 的核心，协调钩子、渲染层、动作执行器之间的关系。

```csharp
// WheelStateMachine.cs
namespace WheelMenu.Logic.StateMachine;

using System.Windows;
using System.Windows.Threading;
using WheelMenu.Logic.MouseHook;
using WheelMenu.Logic.Win32;
using WheelMenu.Renderer;
using WheelMenu.Settings.Models;
using WheelMenu.Settings.Services;
using WheelMenu.Windows;

public enum WheelState { Idle, WheelShown, SectorHighlighted }

public class WheelStateMachine : IDisposable
{
    // ===== 依赖注入 =====
    private readonly LowLevelMouseHook _hook;
    private readonly WheelAnimator     _animator;
    private readonly WheelCanvas       _canvas;
    private readonly WheelWindow       _window;
    private readonly ActionExecutor    _executor;
    private readonly SceneResolver     _sceneResolver;
    private readonly ConfigService     _configService;

    // ===== 运行时状态 =====
    private WheelState _state        = WheelState.Idle;
    private Point      _wheelCenter  = new(0, 0);
    private string     _hoverRing    = string.Empty;
    private int        _hoverSector  = -1;
    private bool       _triggerKeyDown = false;

    // 触发键按下时的鼠标坐标（用于判断是否"有移动"）
    private Point _keyDownPos;
    private const double MoveThreshold = 5.0;   // 移动超过 5 逻辑像素才算"有移动"

    // 超时取消定时器
    private readonly DispatcherTimer _timeoutTimer = new();

    // F1 重复触发键监听（键盘钩子，Phase 3 阶段用全局键盘钩子或 RegisterHotKey）
    // 此处简化：在 WheelWindow 的 KeyDown 事件中处理

    public WheelStateMachine(
        LowLevelMouseHook hook, WheelAnimator animator,
        WheelCanvas canvas, WheelWindow window,
        ActionExecutor executor, SceneResolver sceneResolver,
        ConfigService configService)
    {
        _hook          = hook;
        _animator      = animator;
        _canvas        = canvas;
        _window        = window;
        _executor      = executor;
        _sceneResolver = sceneResolver;
        _configService = configService;

        _hook.MouseEvent     += OnMouseEvent;
        _timeoutTimer.Tick   += OnTimeout;
        _configService.ConfigSaved += OnConfigSaved;
    }

    // ===== 配置变更热加载 =====
    private void OnConfigSaved(WheelConfig config)
    {
        // 设置保存后重新加载，下次触发时生效
        // 若轮盘正在显示，当前交互结束后再更新
        if (_state == WheelState.Idle)
            ReloadConfig(config);
    }

    private void ReloadConfig(WheelConfig config)
    {
        var settings = config.Settings;
        _timeoutTimer.Interval = settings.TimeoutMs > 0
            ? TimeSpan.FromMilliseconds(settings.TimeoutMs)
            : Timeout.InfiniteTimeSpan;
        _canvas.SetDisplayOptions(settings.HideLabelWhenIcon, settings.OuterRing16Mode);
        // 更新当前场景数据到渲染层
        var sceneData = _sceneResolver.GetCurrentSectorData(config, "inner");
        _canvas.InnerData = sceneData;
        // outer data ...
    }

    // ===== 鼠标事件处理 =====
    private void OnMouseEvent(object? sender, MouseHookEventArgs e)
    {
        var config  = _configService.Load();
        var setting = config.Settings;

        bool isTriggerDown = IsTriggerKeyDown(e, setting.TriggerKey);
        bool isTriggerUp   = IsTriggerKeyUp(e, setting.TriggerKey);

        switch (_state)
        {
            case WheelState.Idle:
                if (isTriggerDown)
                {
                    _triggerKeyDown = true;
                    _keyDownPos     = new Point(e.X, e.Y);
                    e.Handled       = true;   // 先消费，防止触发键副作用
                }
                else if (_triggerKeyDown && e.IsMove)
                {
                    double dist = Distance(new Point(e.X, e.Y), _keyDownPos);
                    if (dist >= MoveThreshold)
                        TransitionToShown(e.X, e.Y, config);
                }
                else if (_triggerKeyDown && isTriggerUp)
                {
                    // 按下后未移动，视为普通单击，恢复事件（无法恢复已消费的，只能不处理）
                    _triggerKeyDown = false;
                    e.Handled       = false;
                }
                break;

            case WheelState.WheelShown:
            case WheelState.SectorHighlighted:
                e.Handled = true;   // 轮盘显示期间全部消费

                if (e.IsMove)
                    UpdateHighlight(e.X, e.Y);

                if (isTriggerUp)
                    TriggerAction(config);
                break;
        }
    }

    private void TransitionToShown(int mouseX, int mouseY, WheelConfig config)
    {
        _state = WheelState.WheelShown;

        // 计算圆盘显示位置
        var rawPos    = new Point(mouseX, mouseY);
        var (center, moveTo) = ScreenHelper.CalculateCenter(
            rawPos,
            config.Settings.ConstrainToScreen,
            config.Settings.AutoMoveCursor);
        _wheelCenter = center;

        // 若需要移动鼠标指针
        if (moveTo.HasValue)
            NativeMethods.SetCursorPos((int)moveTo.Value.X, (int)moveTo.Value.Y);

        // 加载当前场景数据到渲染层
        LoadSceneToRenderer(config);

        // 弹出动画（UI 线程）
        Application.Current.Dispatcher.Invoke(() =>
            _animator.Open(_wheelCenter));

        // 启动超时定时器
        if (config.Settings.TimeoutMs > 0)
        {
            _timeoutTimer.Interval = TimeSpan.FromMilliseconds(config.Settings.TimeoutMs);
            _timeoutTimer.Start();
        }
    }

    private void UpdateHighlight(int mouseX, int mouseY)
    {
        var config   = _configService.Load();
        int sectors  = config.Settings.OuterRing16Mode ? 16 : 8;
        var (ring, sector) = WheelGeometry.HitTest(
            _wheelCenter.X, _wheelCenter.Y,
            mouseX, mouseY, sectors);

        if (ring == "dead")
        {
            Application.Current.Dispatcher.Invoke(() => _canvas.ClearHighlight());
            _state = WheelState.WheelShown;
        }
        else
        {
            Application.Current.Dispatcher.Invoke(() => _canvas.SetHighlight(ring, sector));
            _hoverRing   = ring;
            _hoverSector = sector;
            _state       = WheelState.SectorHighlighted;
        }
    }

    private void TriggerAction(WheelConfig config)
    {
        _timeoutTimer.Stop();
        _triggerKeyDown = false;
        _state          = WheelState.Idle;

        // 关闭动画
        Application.Current.Dispatcher.Invoke(() => _animator.Close());

        if (_hoverRing == string.Empty || _hoverSector < 0) return;

        // 解析并执行动作（轻微延迟，等待动画开始后再执行，避免焦点问题）
        var ring   = _hoverRing;
        var sector = _hoverSector;
        _hoverRing   = string.Empty;
        _hoverSector = -1;

        Task.Delay(50).ContinueWith(_ =>
        {
            var direction = SectorIndexToDirection(sector,
                ring == "outer" && config.Settings.OuterRing16Mode ? 16 : 8);
            var action = _configService.ResolveSectorAction(
                config, _sceneResolver.CurrentProcessName, ring, direction);
            if (action != null && action.Type != ActionType.None)
                _executor.Execute(action);
        });
    }

    /// <summary>F1 重复触发（由 WheelWindow.KeyDown 调用）</summary>
    public void RepeatTrigger(WheelConfig config)
    {
        if (_state != WheelState.SectorHighlighted) return;
        // 执行但不关闭圆盘
        var direction = SectorIndexToDirection(_hoverSector, 8);
        var action    = _configService.ResolveSectorAction(
            config, _sceneResolver.CurrentProcessName, _hoverRing, direction);
        if (action != null && action.Type != ActionType.None)
            _executor.Execute(action);
    }

    private void OnTimeout(object? sender, EventArgs e)
    {
        _timeoutTimer.Stop();
        _state          = WheelState.Idle;
        _triggerKeyDown = false;
        _hoverRing      = string.Empty;
        _hoverSector    = -1;
        Application.Current.Dispatcher.Invoke(() => _animator.Close());
    }

    // ===== 辅助方法 =====

    private static bool IsTriggerKeyDown(MouseHookEventArgs e, string key) => key switch
    {
        "middle" => e.IsMiddleDown,
        "x1"     => e.IsX1Down,
        "x2"     => e.IsX2Down,
        _        => false
    };

    private static bool IsTriggerKeyUp(MouseHookEventArgs e, string key) => key switch
    {
        "middle" => e.IsMiddleUp,
        "x1"     => e.IsX1Up,
        "x2"     => e.IsX2Up,
        _        => false
    };

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static string SectorIndexToDirection(int idx, int total)
    {
        var dirs8  = new[] { "N","NE","E","SE","S","SW","W","NW" };
        var dirs16 = new[] { "N","NNE","NE","ENE","E","ESE","SE","SSE",
                             "S","SSW","SW","WSW","W","WNW","NW","NNW" };
        return total == 16 ? dirs16[idx % 16] : dirs8[idx % 8];
    }

    private void LoadSceneToRenderer(WheelConfig config)
    {
        // 将配置数据转换为渲染层数据模型
        var innerData = _sceneResolver.BuildSectorDataArray(config, "inner", 8);
        var outerData = _sceneResolver.BuildSectorDataArray(config,
            "outer", config.Settings.OuterRing16Mode ? 16 : 8);
        Application.Current.Dispatcher.Invoke(() =>
        {
            _canvas.InnerData = innerData;
            _canvas.OuterData = outerData;
        });
    }

    public void Dispose()
    {
        _timeoutTimer.Stop();
        _hook.MouseEvent -= OnMouseEvent;
    }
}
```

---

## Step 3.4  动作执行引擎

### ActionExecutor.cs（分派器）

```csharp
// ActionExecutor.cs
namespace WheelMenu.Logic.Actions;

using WheelMenu.Settings.Models;

public class ActionExecutor
{
    private readonly Dictionary<ActionType, IActionExecutor> _executors;

    public ActionExecutor()
    {
        _executors = new()
        {
            [ActionType.Hotkey]        = new HotkeyExecutor(),
            [ActionType.SimulateInput] = new SimulateInputExecutor(),
            [ActionType.Paste]         = new PasteExecutor(),
            [ActionType.Open]          = new OpenExecutor(),
            [ActionType.RunAction]     = new RunActionExecutor(),
            [ActionType.SendText]      = new SendTextExecutor(),
            [ActionType.DateTime]      = new DateTimeExecutor(),
        };
    }

    public void Execute(SectorActionConfig action)
    {
        if (!_executors.TryGetValue(action.Type, out var executor))
            return;
        try { executor.Execute(action); }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"动作执行失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }
}

public interface IActionExecutor
{
    void Execute(SectorActionConfig action);
}
```

### HotkeyExecutor.cs

```csharp
// Executors/HotkeyExecutor.cs
namespace WheelMenu.Logic.Actions.Executors;

using System.Windows.Input;
using WheelMenu.Logic.Win32;
using WheelMenu.Settings.Models;

public class HotkeyExecutor : IActionExecutor
{
    public void Execute(SectorActionConfig action)
    {
        // 解析 "Ctrl+C" → 按键序列
        var parts = action.Value.Split('+', StringSplitOptions.TrimEntries);
        var vks   = new List<ushort>();

        foreach (var part in parts)
        {
            ushort vk = part.ToUpperInvariant() switch
            {
                "CTRL"  or "CONTROL" => 0x11,
                "ALT"                => 0x12,
                "SHIFT"              => 0x10,
                "WIN"                => 0x5B,
                "ENTER"              => 0x0D,
                "TAB"                => 0x09,
                "ESC"   or "ESCAPE"  => 0x1B,
                "SPACE"              => 0x20,
                "DEL"   or "DELETE"  => 0x2E,
                "BACK"  or "BACKSPACE"=> 0x08,
                "F1"                 => 0x70,
                "F2"                 => 0x71,
                // ... F3~F12 类推
                _ when part.Length == 1 => (ushort)VkKeyScan(part[0]),
                _ => 0
            };
            if (vk != 0) vks.Add(vk);
        }

        if (vks.Count == 0) return;

        // 按下所有键
        var inputs = new NativeMethods.INPUT[vks.Count * 2];
        for (int i = 0; i < vks.Count; i++)
            inputs[i] = MakeKeyInput(vks[i], false);
        // 松开所有键（逆序）
        for (int i = 0; i < vks.Count; i++)
            inputs[vks.Count + i] = MakeKeyInput(vks[vks.Count - 1 - i], true);

        NativeMethods.SendInput((uint)inputs.Length, inputs,
            System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT MakeKeyInput(ushort vk, bool keyUp) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U    = new() { ki = new()
        {
            wVk     = vk,
            dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0
        }}
    };

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);
}
```

### PasteExecutor.cs

```csharp
// Executors/PasteExecutor.cs
namespace WheelMenu.Logic.Actions.Executors;

using System.Runtime.InteropServices;
using WheelMenu.Logic.Win32;
using WheelMenu.Settings.Models;

public class PasteExecutor : IActionExecutor
{
    public void Execute(SectorActionConfig action)
    {
        SetClipboardText(action.Value);
        // 发送 Ctrl+V
        new HotkeyExecutor().Execute(new SectorActionConfig
            { Type = ActionType.Hotkey, Value = "Ctrl+V" });
    }

    public static void SetClipboardText(string text)
    {
        // 使用 WPF Clipboard（UI线程上调用）
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
            System.Windows.Clipboard.SetText(text));
    }
}
```

### OpenExecutor.cs

```csharp
// Executors/OpenExecutor.cs
namespace WheelMenu.Logic.Actions.Executors;

using WheelMenu.Logic.Win32;
using WheelMenu.Settings.Models;

public class OpenExecutor : IActionExecutor
{
    public void Execute(SectorActionConfig action)
    {
        // 展开环境变量
        string path = Environment.ExpandEnvironmentVariables(action.Value);
        NativeMethods.ShellExecute(IntPtr.Zero, "open", path,
            null, null, NativeMethods.SW_SHOW);
    }
}
```

### SimulateInputExecutor.cs

```csharp
// Executors/SimulateInputExecutor.cs
namespace WheelMenu.Logic.Actions.Executors;

using WheelMenu.Logic.Win32;
using WheelMenu.Settings.Models;

/// <summary>
/// 解析 {键名} 格式并模拟按键序列。
/// 普通文字用 Unicode 方式输入（不受输入法影响）。
/// 注意：此方式受当前输入法状态影响，建议复杂文本使用 PasteExecutor。
/// </summary>
public class SimulateInputExecutor : IActionExecutor
{
    public void Execute(SectorActionConfig action)
    {
        var tokens = Tokenize(action.Value);
        foreach (var token in tokens)
        {
            if (token.IsKey)
                new HotkeyExecutor().Execute(new SectorActionConfig
                    { Type = ActionType.Hotkey, Value = token.Value });
            else
                SendUnicodeText(token.Value);
        }
    }

    private static void SendUnicodeText(string text)
    {
        var inputs = new List<NativeMethods.INPUT>();
        foreach (char c in text)
        {
            inputs.Add(new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U    = new() { ki = new()
                {
                    wScan   = c,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE
                }}
            });
            inputs.Add(new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U    = new() { ki = new()
                {
                    wScan   = c,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP
                }}
            });
        }
        NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(),
            System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static List<(bool IsKey, string Value)> Tokenize(string input)
    {
        // 解析 "{ctrl}{c}文字{enter}" 格式
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
                else i++;
            }
            else
            {
                int next = input.IndexOf('{', i);
                string text = next < 0 ? input[i..] : input[i..next];
                result.Add((false, text));
                i = next < 0 ? input.Length : next;
            }
        }
        return result;
    }
}
```

### DateTimeExecutor.cs

```csharp
// Executors/DateTimeExecutor.cs
namespace WheelMenu.Logic.Actions.Executors;

using WheelMenu.Settings.Models;

public class DateTimeExecutor : IActionExecutor
{
    public void Execute(SectorActionConfig action)
    {
        // 格式字符串如 {0:yyyy-MM-dd HH:mm:ss}
        string result;
        try { result = string.Format(action.Value, DateTime.Now); }
        catch { result = DateTime.Now.ToString("yyyy-MM-dd"); }

        new PasteExecutor().Execute(new SectorActionConfig
            { Type = ActionType.Paste, Value = result });
    }
}
```

### SendTextExecutor.cs

```csharp
// Executors/SendTextExecutor.cs
namespace WheelMenu.Logic.Actions.Executors;

using WheelMenu.Settings.Models;

public class SendTextExecutor : IActionExecutor
{
    public void Execute(SectorActionConfig action)
    {
        string text = action.Value;
        // 支持 FILE: 前缀
        if (text.StartsWith("FILE:", StringComparison.OrdinalIgnoreCase))
        {
            string path = Environment.ExpandEnvironmentVariables(text[5..].Trim());
            if (File.Exists(path))
                text = File.ReadAllText(path);
            else
            {
                System.Windows.MessageBox.Show($"文件不存在：{path}", "错误");
                return;
            }
        }
        new PasteExecutor().Execute(new SectorActionConfig
            { Type = ActionType.Paste, Value = text });
    }
}
```

### RunActionExecutor.cs

```csharp
// Executors/RunActionExecutor.cs
namespace WheelMenu.Logic.Actions.Executors;

using WheelMenu.Settings.Models;

/// <summary>
/// 运行对其他已配置动作的引用。
/// ActionRefId 指向动作唯一 ID；若找不到则提示用户重新绑定。
/// </summary>
public class RunActionExecutor : IActionExecutor
{
    public void Execute(SectorActionConfig action)
    {
        if (string.IsNullOrEmpty(action.ActionRefId))
        {
            System.Windows.MessageBox.Show("动作引用为空，请重新绑定动作。", "⚠️ 引用失联");
            return;
        }

        // 在配置中查找目标动作
        var config     = Services.ConfigService.Instance.Load();
        var targetAction = FindActionById(config, action.ActionRefId);

        if (targetAction == null)
        {
            System.Windows.MessageBox.Show(
                $"找不到引用的动作（ID: {action.ActionRefId}），原始动作可能已被删除。\n请重新绑定。",
                "⚠️ 动作引用失联",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        // 若有参数覆盖，替换 value
        var execAction = string.IsNullOrEmpty(action.ActionParam)
            ? targetAction
            : targetAction with { Value = action.ActionParam };

        new ActionExecutor().Execute(execAction);
    }

    private static SectorActionConfig? FindActionById(WheelConfig config, string id)
    {
        foreach (var scene in config.Scenes.Values)
        {
            foreach (var ring in new[] { scene.InnerRing, scene.OuterRing, scene.ExtendedRing })
            {
                foreach (var act in ring.Values)
                {
                    if (act?.ActionRefId == id || act?.Label == id)
                        return act;
                }
            }
        }
        return null;
    }
}
```

---

## Step 3.5  前台窗口变化监听（ForegroundWatcher.cs）

```csharp
// ForegroundWatcher.cs
namespace WheelMenu.Logic.Context;

using System.Diagnostics;
using WheelMenu.Logic.Win32;

public class ForegroundWatcher : IDisposable
{
    private IntPtr                    _hookHandle = IntPtr.Zero;
    private NativeMethods.WinEventProc? _proc;

    public event EventHandler<string?>? ForegroundProcessChanged;

    /// <summary>当前前台窗口进程名（小写，不含路径）</summary>
    public string? CurrentProcessName { get; private set; }

    public void Start()
    {
        _proc       = OnWinEvent;
        _hookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _proc, 0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint thread, uint time)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            var proc = Process.GetProcessById((int)pid);
            CurrentProcessName = proc.ProcessName.ToLowerInvariant() + ".exe";
        }
        catch
        {
            CurrentProcessName = null;
        }
        ForegroundProcessChanged?.Invoke(this, CurrentProcessName);
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    public void Dispose() => Stop();
}
```

### SceneResolver.cs

```csharp
// SceneResolver.cs
namespace WheelMenu.Logic.Context;

using WheelMenu.Renderer;
using WheelMenu.Settings.Models;
using WheelMenu.Settings.Services;

public class SceneResolver
{
    private readonly ForegroundWatcher _watcher;
    private readonly ConfigService     _config;

    public string? CurrentProcessName => _watcher.CurrentProcessName;

    public SceneResolver(ForegroundWatcher watcher, ConfigService config)
    {
        _watcher = watcher;
        _config  = config;
    }

    /// <summary>构建指定圈的渲染数据数组（已合并场景覆盖）</summary>
    public WheelSectorData[] BuildSectorDataArray(
        WheelConfig config, string ring, int count)
    {
        var dirs8  = new[] { "N","NE","E","SE","S","SW","W","NW" };
        var dirs16 = new[] { "N","NNE","NE","ENE","E","ESE","SE","SSE",
                             "S","SSW","SW","WSW","W","WNW","NW","NNW" };
        var dirs   = count == 16 ? dirs16 : dirs8;
        var result = new WheelSectorData[count];

        for (int i = 0; i < count; i++)
        {
            var action = _config.ResolveSectorAction(
                config, CurrentProcessName, ring, dirs[i]);
            result[i] = action != null && action.Type != ActionType.None
                ? new WheelSectorData
                {
                    HasAction = true,
                    Label     = string.IsNullOrEmpty(action.Label)
                                ? action.Value : action.Label,
                    Icon      = LoadIcon(action.IconPath)
                }
                : new WheelSectorData { HasAction = false };
        }
        return result;
    }

    private static System.Windows.Media.ImageSource? LoadIcon(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try
        {
            var img = new System.Windows.Media.Imaging.BitmapImage(new Uri(path));
            img.Freeze();
            return img;
        }
        catch { return null; }
    }
}
```

---

## Step 3.6  App.xaml.cs 组装（依赖注入）

```csharp
// App.xaml.cs
namespace WheelMenu;

using System.Windows;
using WheelMenu.Logic.Actions;
using WheelMenu.Logic.Context;
using WheelMenu.Logic.MouseHook;
using WheelMenu.Logic.StateMachine;
using WheelMenu.Renderer;
using WheelMenu.Settings.Services;
using WheelMenu.Windows;

public partial class App : Application
{
    private LowLevelMouseHook? _hook;
    private WheelStateMachine? _stateMachine;
    private ForegroundWatcher? _fgWatcher;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 创建轮盘窗口（常驻，始终置顶隐藏）
        var wheelWindow = new WheelWindow();
        wheelWindow.Show();

        var canvas    = wheelWindow.WheelCanvas;
        var config    = ConfigService.Instance;
        _fgWatcher    = new ForegroundWatcher();
        var resolver  = new SceneResolver(_fgWatcher, config);
        var executor  = new ActionExecutor();
        var animator  = new WheelAnimator(canvas, wheelWindow);
        _hook         = new LowLevelMouseHook();

        _stateMachine = new WheelStateMachine(
            _hook, animator, canvas, wheelWindow,
            executor, resolver, config);

        _fgWatcher.Start();
        _hook.Install();

        // 绑定 F1 重复触发（在 WheelWindow 的 PreviewKeyDown 中）
        wheelWindow.RepeatTriggerRequested += () =>
            _stateMachine.RepeatTrigger(config.Load());

        // 显示系统托盘或主窗口（可选）
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hook?.Uninstall();
        _fgWatcher?.Stop();
        _stateMachine?.Dispose();
        base.OnExit(e);
    }
}
```

---

## Step 3.7  Phase 3 验收清单

- [ ] 安装鼠标钩子后，程序不崩溃，系统其他应用鼠标正常
- [ ] 按住中键（未移动）→ 松开，视为普通单击，圆盘不弹出
- [ ] 按住中键并移动 ≥ 5px → 圆盘弹出，中键事件被消费
- [ ] 圆盘弹出后移动鼠标，8方向格子高亮切换正确
- [ ] 移入圆心死区（r≤20），高亮清除
- [ ] 松开触发键 → 执行高亮格子的动作 → 圆盘关闭
- [ ] **发送快捷键**：Ctrl+C 正确复制选中内容
- [ ] **粘贴内容**：固定文本正确粘贴到目标输入框
- [ ] **模拟输入**：`{ctrl}{a}` 正确全选
- [ ] **打开文件/URL**：`%USERPROFILE%\Desktop` 正确打开桌面
- [ ] **发送文本**：`FILE:C:\test.txt` 读取并粘贴文件内容
- [ ] **插入日期时间**：`{0:yyyy-MM-dd}` 正确插入当前日期
- [ ] **运行动作引用**：目标动作存在时正常执行
- [ ] **运行动作引用**：目标动作不存在时弹出失联提示，不崩溃
- [ ] 切换到 notepad.exe → 轮盘内容变为 notepad 场景配置
- [ ] 切回桌面 → 轮盘恢复全局场景
- [ ] 超时设置为 2000ms → 不操作 2 秒后圆盘自动关闭
- [ ] F1 触发重复执行 → 轮盘保持不关闭
- [ ] 程序退出时钩子被正确卸载（用 Process Hacker 验证无残留钩子）
