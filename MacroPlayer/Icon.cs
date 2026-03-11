using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Svg;

namespace MacroPlayer;

/// <summary>
/// 图标生成器，从SVG文件加载图标
/// </summary>
public static class IconGenerator
{
    /// <summary>
    /// 从SVG文件创建托盘图标
    /// </summary>
    /// <returns>图标对象</returns>
    public static Icon CreateTrayIcon()
    {
        try
        {
            // 加载SVG文件
            var svgPath = Path.Combine(AppContext.BaseDirectory, "icon_tray_32.svg");
            if (!File.Exists(svgPath))
            {
                // 如果SVG文件不存在，使用程序化生成
                return CreateFallbackIcon();
            }

            var svgDocument = SvgDocument.Open(svgPath);
            
            // 渲染为位图
            using var bitmap = svgDocument.Draw(32, 32);
            
            // 转换为图标
            IntPtr hIcon = bitmap.GetHicon();
            return Icon.FromHandle(hIcon);
        }
        catch
        {
            // 如果加载失败，使用程序化生成
            return CreateFallbackIcon();
        }
    }

    /// <summary>
    /// 从SVG文件创建主窗体图标
    /// </summary>
    /// <returns>图标对象</returns>
    public static Icon CreateMainIcon()
    {
        try
        {
            // 加载SVG文件
            var svgPath = Path.Combine(AppContext.BaseDirectory, "icon_main_256.svg");
            if (!File.Exists(svgPath))
            {
                // 如果SVG文件不存在，使用托盘图标
                return CreateTrayIcon();
            }

            var svgDocument = SvgDocument.Open(svgPath);
            
            // 渲染为位图
            using var bitmap = svgDocument.Draw(256, 256);
            
            // 缩小到32x32用于图标
            using var smallBitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(smallBitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(bitmap, 0, 0, 32, 32);
            
            // 转换为图标
            IntPtr hIcon = smallBitmap.GetHicon();
            return Icon.FromHandle(hIcon);
        }
        catch
        {
            // 如果加载失败，使用托盘图标
            return CreateTrayIcon();
        }
    }

    /// <summary>
    /// 从SVG文件创建图像（用于窗体标题栏）
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns>图像对象</returns>
    public static Image? CreateImage(int width, int height)
    {
        try
        {
            // 加载SVG文件
            var svgPath = Path.Combine(AppContext.BaseDirectory, "icon_main_256.svg");
            if (!File.Exists(svgPath))
            {
                return null;
            }

            var svgDocument = SvgDocument.Open(svgPath);
            
            // 渲染为位图
            return svgDocument.Draw(width, height);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 创建备用图标（程序化生成）
    /// </summary>
    /// <returns>图标对象</returns>
    private static Icon CreateFallbackIcon()
    {
        // 创建一个32x32的位图
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        
        // 清除背景为透明
        graphics.Clear(Color.Transparent);
        
        // 绘制红橙色渐变背景
        var bgRect = new Rectangle(0, 0, 32, 32);
        using var bgBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
            bgRect,
            Color.FromArgb(255, 107, 107),
            Color.FromArgb(192, 57, 43),
            System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal);
        graphics.FillEllipse(bgBrush, 2, 2, 28, 28);
        
        // 绘制黑胶唱片主体
        using var discBrush = new SolidBrush(Color.FromArgb(26, 26, 26));
        graphics.FillEllipse(discBrush, 5, 5, 22, 22);
        
        // 绘制唱片纹路
        using var groovePen = new Pen(Color.FromArgb(46, 46, 46), 1);
        graphics.DrawEllipse(groovePen, 7, 7, 18, 18);
        graphics.DrawEllipse(groovePen, 9, 9, 14, 14);
        
        // 绘制中心标签
        using var labelBrush = new SolidBrush(Color.FromArgb(214, 48, 49));
        graphics.FillEllipse(labelBrush, 11, 11, 10, 10);
        
        // 绘制中心孔
        using var holeBrush = new SolidBrush(Color.FromArgb(17, 17, 17));
        graphics.FillEllipse(holeBrush, 14, 14, 4, 4);
        
        // 转换为图标
        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    /// <summary>
    /// 保存图标到文件
    /// </summary>
    /// <param name="icon">图标对象</param>
    /// <param name="filePath">保存路径</param>
    public static void SaveIconToFile(Icon icon, string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Create);
        icon.Save(fileStream);
    }
}
