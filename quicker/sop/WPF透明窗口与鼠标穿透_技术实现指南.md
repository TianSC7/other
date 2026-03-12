# WPF 透明窗口与鼠标穿透  技术实现指南（.NET 8.0）

> 本文档解决 Phase 1 中**最容易踩坑的技术点**：无边框透明窗口 + 鼠标事件穿透 + 不抢焦点 + 多显示器 DPI。
> 所有代码均为可直接使用的完整实现，不是伪代码。

---

## 一、核心需求与约束

```
需求：
  ✓ 窗口覆盖全屏，背景完全透明（圆盘外的区域对用户不可见）
  ✓ 圆盘隐藏时：鼠标事件完全穿透到下层窗口（桌面/其他应用）
  ✓ 圆盘显示时：轮盘区域正常接收鼠标事件
  ✓ 触发轮盘时不抢走当前应用的键盘焦点（防止触发后粘贴失效）
  ✓ 始终置顶（覆盖在所有普通窗口之上）
  ✓ 在任务栏不显示窗口图标
  ✓ DPI 变化时（拖拽到不同 DPI 显示器）正确响应

约束：
  ✗ WPF 的 AllowsTransparency=True 有性能限制，必须正确使用
  ✗ WS_EX_TRANSPARENT 与 AllowsTransparency 同时使用时有坑
  ✗ 窗口不能 Focusable=False（否则鼠标事件不会送达），需要特殊处理
```

---

## 二、WheelWindow.xaml 完整实现

```xml
<!-- WheelWindow.xaml -->
<Window
    x:Class="WheelMenu.Windows.WheelWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:r="clr-namespace:WheelMenu.Renderer"

    WindowStyle="None"
    AllowsTransparency="True"
    Background="Transparent"

    Topmost="True"
    ShowInTaskbar="False"

    WindowState="Maximized"
    ResizeMode="NoResize"

    Focusable="False"
    IsHitTestVisible="True"

    Visibility="Hidden"
    ShowActivated="False">

    <!-- ShowActivated="False" 防止窗口 Show() 时抢焦点 -->

    <Canvas x:Name="RootCanvas" Background="Transparent">
        <r:WheelCanvas
            x:Name="WheelCanvas"
            RenderTransformOrigin="0.5,0.5">
            <r:WheelCanvas.RenderTransform>
                <ScaleTransform ScaleX="1" ScaleY="1"/>
            </r:WheelCanvas.RenderTransform>
        </r:WheelCanvas>
    </Canvas>
</Window>
```

---

## 三、WheelWindow.xaml.cs 完整实现

```csharp
// WheelWindow.xaml.cs
namespace WheelMenu.Windows;

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WheelMenu.Renderer;

public partial class WheelWindow : Window
{
    // ===== Win32 常量 =====
    private const int GWL_EXSTYLE       = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED     = 0x00080000;
    private const int WS_EX_NOACTIVATE  = 0x08000000;
    private const int WS_EX_TOOLWINDOW  = 0x00000080;   // 不在 Alt+Tab 中显示

    private const int WM_NCHITTEST      = 0x0084;
    private const int HTTRANSPARENT     = -1;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int  GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int  SetWindowLong(IntPtr hwnd, int index, int newStyle);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;

    private IntPtr _hwnd;
    private bool   _passThrough = true;  // 初始状态：穿透

    // 圆盘显示期间的 WheelCanvas 公开引用
    public WheelCanvas WheelCanvas => (WheelCanvas)((System.Windows.Controls.Canvas)Content).Children[0];

    // F1 重复触发事件（由 Logic 层订阅）
    public event Action? RepeatTriggerRequested;

    public WheelWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded            += OnLoaded;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        // 安装 WndProc Hook，用于拦截 WM_NCHITTEST
        var source = HwndSource.FromHwnd(_hwnd);
        source.AddHook(WndProc);

        // 设置初始窗口扩展样式
        ApplyWindowStyle();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // 确保始终置顶（Topmost=True 有时在某些系统上失效）
        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    }

    // ===== 核心：动态切换鼠标穿透 =====

    /// <summary>
    /// 控制鼠标事件是否穿透此窗口。
    /// true  = 穿透（圆盘隐藏时），鼠标事件到达下层窗口
    /// false = 不穿透（圆盘显示时），正常接收鼠标事件
    /// </summary>
    public void EnableMousePassThrough(bool passThrough)
    {
        if (_passThrough == passThrough) return;
        _passThrough = passThrough;
        ApplyWindowStyle();
    }

    private void ApplyWindowStyle()
    {
        if (_hwnd == IntPtr.Zero) return;

        int style = GetWindowLong(_hwnd, GWL_EXSTYLE);

        // 始终保留的样式
        style |= WS_EX_LAYERED;      // AllowsTransparency 需要
        style |= WS_EX_NOACTIVATE;   // 不抢焦点（关键！）
        style |= WS_EX_TOOLWINDOW;   // 不在 Alt+Tab 显示

        if (_passThrough)
            style |= WS_EX_TRANSPARENT;    // 穿透：加上
        else
            style &= ~WS_EX_TRANSPARENT;   // 不穿透：去掉

        SetWindowLong(_hwnd, GWL_EXSTYLE, style);
    }

    // ===== WndProc：处理特殊消息 =====
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST when _passThrough:
                // 穿透模式：报告整个窗口为透明，系统将事件转发给下层窗口
                // 这是比 WS_EX_TRANSPARENT 更精细的控制方式
                handled = true;
                return new IntPtr(HTTRANSPARENT);
        }
        return IntPtr.Zero;
    }

    // ===== 公开接口 =====

    public void ShowWheel(Point screenCenter)
    {
        // 设置圆盘圆心坐标
        WheelCanvas.SetCenter(screenCenter);

        // 先取消穿透，再显示（顺序很重要）
        EnableMousePassThrough(false);
        Visibility = Visibility.Visible;
    }

    public void HideWheel()
    {
        EnableMousePassThrough(true);
        Visibility = Visibility.Hidden;
    }

    // ===== 键盘事件：F1 重复触发 =====
    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        // 注意：WS_EX_NOACTIVATE 使窗口不接收键盘焦点
        // F1 重复触发需要通过全局键盘钩子或 RegisterHotKey 实现
        // 此处仅作为回退处理（若窗口意外获得焦点时）
        base.OnPreviewKeyDown(e);
    }
}
```

---

## 四、透明窗口的三个关键坑

### 坑 1：`WS_EX_TRANSPARENT` 与 `AllowsTransparency` 同时使用的问题

```
现象：设置了 WS_EX_TRANSPARENT 后，WPF 的鼠标事件（MouseMove/MouseDown）
      完全无法触发，连 IsHitTestVisible=True 也没用。

原因：WS_EX_TRANSPARENT 在 Win32 层面直接屏蔽所有鼠标消息，
      WPF 的 HitTest 机制根本收不到原始 WM_MOUSEMOVE 等消息。

解决：圆盘显示时必须移除 WS_EX_TRANSPARENT（本文档的做法）。
      不要尝试用 WS_EX_TRANSPARENT + IsHitTestVisible 组合——无效。
```

### 坑 2：`AllowsTransparency=True` 不设置时的后果

```
现象：圆盘背景变成黑色，透明区域显示为黑色。

原因：WPF 默认窗口使用桌面合成，不支持逐像素透明。
      必须同时满足：
        WindowStyle = None
        AllowsTransparency = True
        Background = Transparent（或具体颜色）

坑：AllowsTransparency=True 启用后，窗口无法使用硬件加速的某些特性
    （如 WindowChrome 的 Aero Glass）。轮盘窗口全屏覆盖，不影响使用。
```

### 坑 3：`ShowActivated="False"` 与 `WS_EX_NOACTIVATE` 的区别

```
ShowActivated="False"：
  → 仅在首次 Show() 时不激活
  → 后续 Show()/Visibility 切换 仍可能激活窗口

WS_EX_NOACTIVATE：
  → 永久性：点击此窗口时不会激活它，也不改变前台窗口
  → 这是轮盘需要的：触发后当前编辑器/浏览器保持焦点

两者都需要设置！
```

---

## 五、多显示器与 DPI 适配

### 5.1  为什么需要 `PerMonitorV2`

```
问题场景：
  主显示器 DPI 100%，副显示器 DPI 150%。
  不声明 DPI 感知时：WPF 在所有显示器上按主显示器 DPI 渲染，
  在副显示器上圆盘会被系统拉伸，字体模糊。

解决：app.manifest 声明 PerMonitorV2：
  → WPF 在每个显示器上独立缩放
  → 拖到高 DPI 显示器时触发 DpiChanged 事件，窗口自动重布局
```

### 5.2  获取当前鼠标所在显示器工作区

```csharp
// Helpers/ScreenHelper.cs
namespace WheelMenu.Helpers;

using System.Windows;
using System.Windows.Forms;   // Screen 类

public static class ScreenHelper
{
    /// <summary>
    /// 获取包含指定屏幕坐标（物理像素）的显示器工作区（WPF 逻辑像素）。
    /// 鼠标钩子返回的坐标是物理像素，需转换为 WPF 逻辑像素。
    /// </summary>
    public static Rect GetWorkAreaLogical(int physicalX, int physicalY)
    {
        var screen = Screen.FromPoint(new System.Drawing.Point(physicalX, physicalY));
        var wa     = screen.WorkingArea;

        // 获取 DPI 换算比（通过 PresentationSource）
        double dpiX = 96.0, dpiY = 96.0;
        var src = PresentationSource.FromVisual(Application.Current.MainWindow);
        if (src?.CompositionTarget != null)
        {
            dpiX = 96.0 * src.CompositionTarget.TransformToDevice.M11;
            dpiY = 96.0 * src.CompositionTarget.TransformToDevice.M22;
        }

        return new Rect(
            wa.Left   * 96.0 / dpiX,
            wa.Top    * 96.0 / dpiY,
            wa.Width  * 96.0 / dpiX,
            wa.Height * 96.0 / dpiY);
    }

    /// <summary>
    /// 将鼠标钩子返回的物理像素坐标转换为 WPF 逻辑像素坐标。
    /// </summary>
    public static Point PhysicalToLogical(int physicalX, int physicalY)
    {
        var src = PresentationSource.FromVisual(Application.Current.MainWindow);
        if (src?.CompositionTarget == null)
            return new Point(physicalX, physicalY);

        double dpiX = 96.0 * src.CompositionTarget.TransformToDevice.M11;
        double dpiY = 96.0 * src.CompositionTarget.TransformToDevice.M22;
        return new Point(physicalX * 96.0 / dpiX, physicalY * 96.0 / dpiY);
    }

    /// <summary>
    /// 计算圆盘显示圆心（逻辑像素），考虑屏幕边缘限制。
    /// </summary>
    public static (Point WheelCenter, Point? MoveCursorTo) CalculateCenter(
        int mousePhysicalX, int mousePhysicalY,
        bool constrainToScreen, bool autoMoveCursor)
    {
        var logicalPos = PhysicalToLogical(mousePhysicalX, mousePhysicalY);

        if (!constrainToScreen)
            return (logicalPos, null);

        var wa = GetWorkAreaLogical(mousePhysicalX, mousePhysicalY);
        double r  = WheelMenu.Renderer.WheelConstants.OuterRingRadius;
        double cx = Math.Clamp(logicalPos.X, wa.Left + r, wa.Right  - r);
        double cy = Math.Clamp(logicalPos.Y, wa.Top  + r, wa.Bottom - r);
        var    center = new Point(cx, cy);

        Point? moveTo = autoMoveCursor ? center : null;
        return (center, moveTo);
    }
}
```

### 5.3  鼠标钩子坐标是物理像素

```
⚠️ 重要：
  Win32 低级鼠标钩子（MSLLHOOKSTRUCT.pt）返回的坐标是【物理像素】。
  WPF 的所有坐标（Point、Rect、Canvas 位置）是【逻辑像素】。

  DPI 100%：物理像素 = 逻辑像素，无差异
  DPI 150%：物理 1500px = 逻辑 1000px

  必须在 StateMachine 中调用 ScreenHelper.PhysicalToLogical() 转换后
  再传给 WheelCanvas.SetCenter() 和 WheelGeometry.HitTest()。
```

---

## 六、WheelCanvas 尺寸与位置设置

`WheelCanvas` 位于全屏 `Canvas` 内，需要在每次弹出时正确设置位置：

```csharp
// WheelCanvas.cs 中的 SetCenter
public void SetCenter(Point center)
{
    _center = center;

    // 将 WheelCanvas 控件本身定位到圆盘区域
    // WheelCanvas 大小 = 圆盘直径（全部绘制区域）
    double d = WheelConstants.WheelDiameter;
    Width    = d;
    Height   = d;

    // 在父 Canvas 中定位，使圆心对齐到 center
    System.Windows.Controls.Canvas.SetLeft(this, center.X - d / 2);
    System.Windows.Controls.Canvas.SetTop( this, center.Y - d / 2);

    // 绘制时使用控件内的本地坐标（圆心 = 控件中心）
    // _center 在 OnRender 中设为 (Width/2, Height/2)
    InvalidateVisual();
}

protected override void OnRender(DrawingContext dc)
{
    // 使用控件本地坐标，圆心始终在控件中央
    double cx = ActualWidth  / 2;
    double cy = ActualHeight / 2;
    // ... 所有绘制均以 (cx, cy) 为圆心
}
```

---

## 七、焦点管理（确保 Ctrl+V 等动作生效）

轮盘的核心价值在于"触发动作不影响当前焦点"，以下是完整的焦点保护措施：

```
措施 1：WS_EX_NOACTIVATE（必须）
  → 点击轮盘窗口时不激活，当前应用保持焦点

措施 2：ShowActivated = "False"（XAML 属性）
  → 首次 Show 时不激活

措施 3：动作执行延迟 50ms（Phase 3 代码中的 Task.Delay(50)）
  → 等待轮盘关闭动画开始后再执行 SendInput
  → 确保目标窗口已重新成为前台窗口

措施 4：不在轮盘窗口上调用 Focus()
  → WheelWindow 的所有代码中不应出现 Focus() 调用
```

**验证焦点未被抢走的方法**：
1. 在文本框中输入文字
2. 触发轮盘，执行"粘贴"动作
3. 文字应被粘贴到原文本框中，而不是其他地方

---

## 八、全屏窗口与 WindowState

```csharp
// 不使用 WindowState = Maximized 的方案（更精确）
// 因为 Maximized 会被任务栏大小影响，轮盘需要覆盖完整物理屏幕

// 在 OnLoaded 中手动设置窗口大小为所有屏幕的虚拟桌面区域
private void OnLoaded(object? sender, RoutedEventArgs e)
{
    // 虚拟屏幕总区域（所有显示器的并集）
    Left   = SystemParameters.VirtualScreenLeft;
    Top    = SystemParameters.VirtualScreenTop;
    Width  = SystemParameters.VirtualScreenWidth;
    Height = SystemParameters.VirtualScreenHeight;
}
```

> 使用 `SystemParameters.VirtualScreen*` 而非 `PrimaryScreen*`，确保多显示器环境下轮盘可在任意显示器弹出。

---

## 九、常见报错与排查

| 报错 / 现象 | 原因 | 解决 |
|------------|------|------|
| 圆盘背景为黑色 | 缺少 `AllowsTransparency=True` 或 `Background=Transparent` | 检查 XAML 三项属性 |
| 圆盘显示后鼠标事件不触发 | `WS_EX_TRANSPARENT` 未移除 | `EnableMousePassThrough(false)` 在 `ShowWheel` 中调用 |
| 触发轮盘后文本框失焦 | 缺少 `WS_EX_NOACTIVATE` | 检查 `ApplyWindowStyle()` 中是否包含此样式 |
| 动作执行后粘贴到错误位置 | 执行太快，焦点未恢复 | 确认 `Task.Delay(50)` 存在于触发后延迟执行 |
| 高 DPI 下圆盘位置偏移 | 钩子坐标未从物理像素转换为逻辑像素 | 使用 `ScreenHelper.PhysicalToLogical()` |
| 高 DPI 下字体模糊 | `app.manifest` 未声明 `PerMonitorV2` | 检查 manifest 文件内容 |
| 程序退出后系统鼠标异常卡顿 | 钩子未卸载 | 确认 `App.OnExit` 中调用了 `hook.Uninstall()` |
| `SetWindowLong` 调用后无效果 | `_hwnd` 为 `IntPtr.Zero`（窗口未初始化）| 在 `SourceInitialized` 事件之后调用，不要在构造函数中调用 |
| 多显示器时圆盘超出屏幕 | `ScreenHelper` 使用了主显示器工作区 | 确认使用 `Screen.FromPoint(physicalX, physicalY)` 获取当前显示器 |
