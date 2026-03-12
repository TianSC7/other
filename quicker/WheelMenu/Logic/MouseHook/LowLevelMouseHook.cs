using System.Diagnostics;
using System.Runtime.InteropServices;
using WheelMenu.Logic.Win32;

namespace WheelMenu.Logic.MouseHook;

public class LowLevelMouseHook : IDisposable
{
    private IntPtr _hookHandle = IntPtr.Zero;
    private NativeMethods.HookProc? _proc;

    public event EventHandler<MouseHookEventArgs>? MouseEvent;

    public void Install()
    {
        if (_hookHandle != IntPtr.Zero) return;
        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
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

            if (args.Handled)
                return new IntPtr(1);
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
