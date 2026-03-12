namespace WheelMenu.Renderer;

using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>Phase 1 专用，Phase 3 接入真实数据后整体删除</summary>
public static class FakeData
{
    // 假图标：用纯色矩形 DrawingImage 代替真实图片，无需图片文件
    private static ImageSource MakeSolidIcon(Color color, double size = 24)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            dc.DrawRectangle(new SolidColorBrush(color), null,
                new System.Windows.Rect(0, 0, size, size));
        var bmp = new RenderTargetBitmap(
            (int)size, (int)size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        return bmp;
    }

    public static WheelSectorData[] InnerRing { get; } =
    [
        new() { HasAction=true,  Label="复制",              Icon=null },                              // N
        new() { HasAction=true,  Label="粘贴",              Icon=MakeSolidIcon(Color.FromRgb( 70,130,180)) }, // NE
        new() { HasAction=true,  Label="撤销",              Icon=MakeSolidIcon(Color.FromRgb(255,165,  0)) }, // E
        new() { HasAction=true,  Label="很长的动作名称测试溢出", Icon=null },                         // SE
        new() { HasAction=true,  Label="保存",              Icon=MakeSolidIcon(Color.FromRgb( 34,139, 34)) }, // S
        new() { HasAction=true,  Label="关闭",              Icon=null },                              // SW
        new() { HasAction=false, Label=string.Empty,        Icon=null },                              // W（空）
        new() { HasAction=true,  Label="截图",              Icon=MakeSolidIcon(Color.FromRgb(147,112,219)) }, // NW
    ];

    public static WheelSectorData[] OuterRing { get; } =
    [
        new() { HasAction=true,  Label="浏览器", Icon=MakeSolidIcon(Color.FromRgb(255, 99, 71)) }, // N
        new() { HasAction=true,  Label="文件夹", Icon=MakeSolidIcon(Color.FromRgb(255,215,  0)) }, // NE
        new() { HasAction=false, Label=string.Empty, Icon=null },
        new() { HasAction=false, Label=string.Empty, Icon=null },
        new() { HasAction=true,  Label="设置",   Icon=MakeSolidIcon(Color.FromRgb(128,128,128)) }, // S
        new() { HasAction=false, Label=string.Empty, Icon=null },
        new() { HasAction=false, Label=string.Empty, Icon=null },
        new() { HasAction=false, Label=string.Empty, Icon=null },
    ];
}
