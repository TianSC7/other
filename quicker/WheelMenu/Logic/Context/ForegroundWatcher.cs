namespace WheelMenu.Logic.Context;

using System.Diagnostics;
using WheelMenu.Logic.Win32;

/// <summary>
/// 前台窗口变化监听器
/// 使用 WinEvent Hook 监听前台窗口切换事件，获取当前活动窗口进程名
/// </summary>
public class ForegroundWatcher : IDisposable
{
    private IntPtr _hookHandle = IntPtr.Zero;
    private NativeMethods.WinEventProc? _proc;

    /// <summary>前台窗口进程名变化时触发</summary>
    public event EventHandler<string?>? ForegroundProcessChanged;

    /// <summary>当前前台窗口进程名（小写，含 .exe 后缀）</summary>
    public string? CurrentProcessName { get; private set; }

    /// <summary>启动监听</summary>
    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return;

        _proc = OnWinEvent;
        _hookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _proc,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);

        if (_hookHandle == IntPtr.Zero)
        {
            int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"[ForegroundWatcher] 启动失败，错误码: {err}");
        }

        // 初始化当前进程名
        UpdateCurrentProcessName();
    }

    /// <summary>停止监听</summary>
    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint thread, uint time)
    {
        UpdateCurrentProcessName();
    }

    private void UpdateCurrentProcessName()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return;

        try
        {
            var proc = Process.GetProcessById((int)pid);
            string newName = proc.ProcessName.ToLowerInvariant() + ".exe";

            if (CurrentProcessName != newName)
            {
                CurrentProcessName = newName;
                ForegroundProcessChanged?.Invoke(this, CurrentProcessName);
            }
        }
        catch
        {
            // 进程可能已退出
            CurrentProcessName = null;
        }
    }

    public void Dispose() => Stop();
}
