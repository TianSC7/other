namespace WheelMenu.Logic.Win32;

using System.Runtime.InteropServices;

public static class NativeMethods
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

    // ===== 键盘钩子（用于F1重复触发）=====
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP   = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP   = 0x0105;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
