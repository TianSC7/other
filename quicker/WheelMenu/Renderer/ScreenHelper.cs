namespace WheelMenu.Renderer;

using System.Windows;
using System.Windows.Forms;
using WheelMenu.Config;

public static class ScreenHelper
{
    /// <summary>
    /// 获取主显示器工作区域（WPF 逻辑像素）。
    /// </summary>
    public static Rect GetPrimaryScreenWorkArea()
    {
        var primaryScreen = Screen.PrimaryScreen;
        if (primaryScreen == null)
        {
            // 如果无法获取主屏幕，返回默认值
            return new Rect(0, 0, 1920, 1080);
        }
        var wa = primaryScreen.WorkingArea;
        // 转换为WPF逻辑像素
        double dpiX = 96.0, dpiY = 96.0;
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow != null)
        {
            var src = PresentationSource.FromVisual(mainWindow);
            if (src?.CompositionTarget != null)
            {
                dpiX = 96.0 * src.CompositionTarget.TransformToDevice.M11;
                dpiY = 96.0 * src.CompositionTarget.TransformToDevice.M22;
            }
        }
        return new Rect(
            wa.Left / dpiX * 96,
            wa.Top / dpiY * 96,
            wa.Width / dpiX * 96,
            wa.Height / dpiY * 96);
    }

    /// <summary>
    /// 获取包含指定屏幕坐标的显示器工作区域（WPF 逻辑像素）。
    /// </summary>
    public static Rect GetWorkAreaContaining(Point screenPt)
    {
        // 转为物理像素（WPF 逻辑坐标需要换算）
        var screen = Screen.FromPoint(
            new System.Drawing.Point((int)screenPt.X, (int)screenPt.Y));
        var wa = screen.WorkingArea;
        // Screen.WorkingArea 是物理像素，需换算为逻辑像素
        // 通过 PresentationSource 获取 DPI
        double dpiX = 96.0, dpiY = 96.0;
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow != null)
        {
            var src = PresentationSource.FromVisual(mainWindow);
            if (src?.CompositionTarget != null)
            {
                dpiX = 96.0 * src.CompositionTarget.TransformToDevice.M11;
                dpiY = 96.0 * src.CompositionTarget.TransformToDevice.M22;
            }
        }
        return new Rect(
            wa.Left   / dpiX * 96,
            wa.Top    / dpiY * 96,
            wa.Width  / dpiX * 96,
            wa.Height / dpiY * 96);
    }

    /// <summary>
    /// 计算圆盘实际显示圆心（考虑屏幕边缘限制）。
    /// </summary>
    /// <param name="mousePos">鼠标位置</param>
    /// <param name="mode">边缘适配模式</param>
    /// <returns>（轮盘圆心，需要移动到的鼠标位置）</returns>
    public static (Point WheelCenter, Point? MoveCursorTo) CalculateCenter(
        Point mousePos,
        EdgeConstrainMode mode)
    {
        // 模式A（不限定）：圆心就在鼠标位置，不移动鼠标
        if (mode == EdgeConstrainMode.None)
            return (mousePos, null);

        // 模式B和C（限定屏幕）：计算适配后的圆心
        var wa = GetWorkAreaContaining(mousePos);
        var c = WheelGeometry.CalculateWheelCenter(
            mousePos.X, mousePos.Y, wa, mode);

        // 模式C（限定+移指针）：返回需要移动到的鼠标位置
        Point? moveTo = mode == EdgeConstrainMode.ConstrainWithCursor ? c : null;
        return (c, moveTo);
    }

    /// <summary>
    /// 计算圆盘实际显示圆心（考虑屏幕边缘限制，兼容旧版本）。
    /// </summary>
    public static (Point WheelCenter, Point? MoveCursorTo) CalculateCenter(
        Point mousePos,
        bool constrainToScreen,
        bool autoMoveCursor)
    {
        EdgeConstrainMode mode = EdgeConstrainMode.None;
        if (constrainToScreen)
            mode = autoMoveCursor ? EdgeConstrainMode.ConstrainWithCursor : EdgeConstrainMode.Constrain;
        
        return CalculateCenter(mousePos, mode);
    }
}
