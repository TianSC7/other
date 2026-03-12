using System.Windows;
using System.Windows.Input;
using WheelMenu.Renderer;
using WheelMenu.Windows;

namespace WheelMenu.Windows;

public partial class TestWindow : Window
{
    private readonly WheelWindow _wheel;
    private readonly WheelAnimator _animator;
    private bool _outerMode = false;
    private bool _hideLabel = true;
    private bool _outer16Mode = false;

    public TestWindow()
    {
        InitializeComponent();
        _wheel = new WheelWindow();
        _wheel.Show();
        _animator = new WheelAnimator(_wheel.WheelPreviewCanvas, _wheel);
        KeyDown += OnKeyDown;
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsWindow = new WheelMenu.Windows.SettingsWindow();
            settingsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"打开设置窗口失败：{ex.Message}\n\n详细信息：\n{ex.StackTrace}", 
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var canvas = _wheel.WheelPreviewCanvas;

        switch (e.Key)
        {
            case Key.Space:
                var center = new System.Windows.Point(
                    SystemParameters.PrimaryScreenWidth / 2,
                    SystemParameters.PrimaryScreenHeight / 2);
                _animator.Open(center);
                break;

            case Key.Escape:
                _animator.Close();
                break;

            case Key.D1: case Key.D2: case Key.D3: case Key.D4:
            case Key.D5: case Key.D6: case Key.D7: case Key.D8:
                int idx = e.Key - Key.D1;
                // DiskUIControl: 顺时针编号：内圈(环1)扇区 1,2,3,4,5,6,7,8 ; 外圈(环2)扇区 9,10,11,...,16
                string ring = _outerMode ? "环2" : "环1";
                int sector;
                if (_outerMode)
                {
                    // 外圈: 索引0->9, 1->10, ..., 7->16 (顺时针)
                    int[] outerSectors = { 9, 10, 11, 12, 13, 14, 15, 16 };
                    sector = outerSectors[idx];
                }
                else
                {
                    // 内圈: 索引0->1, 1->2, ..., 7->8 (顺时针)
                    int[] innerSectors = { 1, 2, 3, 4, 5, 6, 7, 8 };
                    sector = innerSectors[idx];
                }
                canvas.SetHighlight(ring, sector);
                break;

            case Key.Q:
                _outerMode = !_outerMode;
                Title = _outerMode ? "外圈模式（Q切换）" : "内圈模式（Q切换）";
                break;

            case Key.F1:
                _hideLabel = !_hideLabel;
                canvas.SetDisplayOptions(_hideLabel, _outer16Mode);
                break;

            case Key.F2:
                _outer16Mode = !_outer16Mode;
                canvas.SetDisplayOptions(_hideLabel, _outer16Mode);
                break;

            case Key.F3:
                _animator.Open(new System.Windows.Point(
                    SystemParameters.PrimaryScreenWidth - 10,
                    10));
                break;

            case Key.F4:
                _animator.Open(new System.Windows.Point(
                    10,
                    SystemParameters.PrimaryScreenHeight - 10));
                break;
        }
    }
}
