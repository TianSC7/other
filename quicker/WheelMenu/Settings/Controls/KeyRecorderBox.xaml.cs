using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WheelMenu.Settings.Controls;

public partial class KeyRecorderBox : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty KeyTextProperty =
        DependencyProperty.Register(nameof(KeyText), typeof(string),
            typeof(KeyRecorderBox),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string KeyText
    {
        get => (string)GetValue(KeyTextProperty);
        set => SetValue(KeyTextProperty, value);
    }

    private bool _recording = false;

    public KeyRecorderBox()
    {
        InitializeComponent();
        MouseLeftButtonDown += OnBoxClick;
        PreviewKeyDown += HandlePreviewKeyDown;
    }

    private void OnBoxClick(object sender, MouseButtonEventArgs e)
    {
        _recording = true;
        DisplayTextBlock.Text = "请按下目标键...";
        BorderBox.BorderBrush = new SolidColorBrush(Colors.DodgerBlue);
        Focus();
    }

    private void HandlePreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_recording) return;
        e.Handled = true;

        if (e.Key == System.Windows.Input.Key.Escape)
        {
            _recording = false;
            DisplayTextBlock.Text = KeyText;
            BorderBox.BorderBrush = new SolidColorBrush(Colors.Gray);
            return;
        }

        var mods = new List<string>();
        if ((Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0) mods.Add("Ctrl");
        if ((Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0) mods.Add("Alt");
        if ((Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0) mods.Add("Shift");
        if ((Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Windows) != 0) mods.Add("Win");

        var mainKey = e.Key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl
            or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt
            or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
            or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin
            ? null : e.Key.ToString();

        if (mainKey == null) return;

        mods.Add(mainKey);
        KeyText = string.Join("+", mods);
        DisplayTextBlock.Text = KeyText;
        BorderBox.BorderBrush = new SolidColorBrush(Colors.Gray);
        _recording = false;
    }
}
