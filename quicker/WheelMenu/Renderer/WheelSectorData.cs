namespace WheelMenu.Renderer;

using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>单个扇区的显示数据（Phase 1 用假数据，Phase 3 替换为真实配置）</summary>
public class WheelSectorData
{
    public bool       HasAction { get; set; } = false;
    public string     Label     { get; set; } = string.Empty;
    public ImageSource? Icon    { get; set; } = null;

    /// <summary>根据设置项计算实际显示模式</summary>
    public DisplayMode GetDisplayMode(bool hideLabelWhenIcon) =>
        (HasAction, Icon != null, hideLabelWhenIcon) switch
        {
            (false, _, _)          => DisplayMode.LabelOnly,   // 空格，显示 "+"
            (true, true, true)     => DisplayMode.IconOnly,
            (true, true, false)    => DisplayMode.IconAndLabel,
            (true, false, _)       => DisplayMode.LabelOnly
        };
}
