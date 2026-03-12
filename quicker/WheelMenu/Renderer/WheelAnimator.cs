namespace WheelMenu.Renderer;

using System.Windows;
using System.Windows.Media.Animation;
using WheelMenu.Windows;

public class WheelAnimator
{
    private readonly DiskUIControl _canvas;
    private readonly WheelWindow _window;   // 需要 window 引用以控制 ScaleTransform

    // DiskUIControl 的 RenderTransform 必须预先设置为 ScaleTransform + 居中 TransformOrigin
    // 在 WheelWindow.xaml 中: RenderTransformOrigin="0.5,0.5"

    public WheelAnimator(DiskUIControl canvas, WheelWindow window)
    {
        _canvas = canvas;
        _window = window;
    }

    public void Open(Point screenCenter, Action? onCompleted = null)
    {
        _window.ShowWheel(screenCenter);

        var scaleX  = new DoubleAnimation(
            WheelConstants.AnimOpenScaleFrom, 1.0,
            WheelConstants.AnimOpenDuration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var scaleY  = scaleX.Clone();
        var opacity = new DoubleAnimation(0.0, 1.0, WheelConstants.AnimOpenDuration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

        if (onCompleted != null)
            opacity.Completed += (_, _) => onCompleted();

        var sb = new Storyboard();
        // _canvas 需要有 ScaleTransform 作为 RenderTransform
        Storyboard.SetTarget(scaleX,  _canvas);
        Storyboard.SetTarget(scaleY,  _canvas);
        Storyboard.SetTarget(opacity, _canvas);
        Storyboard.SetTargetProperty(scaleX,
            new PropertyPath("RenderTransform.ScaleX"));
        Storyboard.SetTargetProperty(scaleY,
            new PropertyPath("RenderTransform.ScaleY"));
        Storyboard.SetTargetProperty(opacity,
            new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(scaleX);
        sb.Children.Add(scaleY);
        sb.Children.Add(opacity);
        sb.Begin();
    }

    public void Close(Action? onCompleted = null)
    {
        var scaleX  = new DoubleAnimation(
            1.0, WheelConstants.AnimCloseScaleTo,
            WheelConstants.AnimCloseDuration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        var scaleY  = scaleX.Clone();
        var opacity = new DoubleAnimation(1.0, 0.0, WheelConstants.AnimCloseDuration)
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };

        opacity.Completed += (_, _) =>
        {
            _window.HideWheel();
            onCompleted?.Invoke();
        };

        var sb = new Storyboard();
        Storyboard.SetTarget(scaleX,  _canvas);
        Storyboard.SetTarget(scaleY,  _canvas);
        Storyboard.SetTarget(opacity, _canvas);
        Storyboard.SetTargetProperty(scaleX,
            new PropertyPath("RenderTransform.ScaleX"));
        Storyboard.SetTargetProperty(scaleY,
            new PropertyPath("RenderTransform.ScaleY"));
        Storyboard.SetTargetProperty(opacity,
            new PropertyPath(UIElement.OpacityProperty));
        sb.Children.Add(scaleX);
        sb.Children.Add(scaleY);
        sb.Children.Add(opacity);
        sb.Begin();
    }
}
