using WheelMenu.Logic.Win32;

namespace WheelMenu.Logic.MouseHook;

public class MouseHookEventArgs(int message, NativeMethods.MSLLHOOKSTRUCT info)
{
    public int Message { get; } = message;
    public int X { get; } = info.pt.X;
    public int Y { get; } = info.pt.Y;
    public uint MouseData { get; } = info.mouseData;
    public bool Handled { get; set; } = false;

    public bool IsMiddleDown => Message == NativeMethods.WM_MBUTTONDOWN;
    public bool IsMiddleUp => Message == NativeMethods.WM_MBUTTONUP;
    public bool IsX1Down => Message == NativeMethods.WM_XBUTTONDOWN
                             && (MouseData >> 16) == NativeMethods.XBUTTON1;
    public bool IsX1Up => Message == NativeMethods.WM_XBUTTONUP
                           && (MouseData >> 16) == NativeMethods.XBUTTON1;
    public bool IsX2Down => Message == NativeMethods.WM_XBUTTONDOWN
                             && (MouseData >> 16) == NativeMethods.XBUTTON2;
    public bool IsX2Up => Message == NativeMethods.WM_XBUTTONUP
                           && (MouseData >> 16) == NativeMethods.XBUTTON2;
    public bool IsMove => Message == NativeMethods.WM_MOUSEMOVE;
}
