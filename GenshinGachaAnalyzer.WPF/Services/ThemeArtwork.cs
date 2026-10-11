using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GenshinGachaAnalyzer.WPF.Services;

/// <summary>木偶使用眼内齿轮纹饰，其余使用 HoYoWiki 元素原图；全部离线绘制。</summary>
public static class ThemeArtwork
{
    public const double CanvasSize = 320;
    private static readonly Dictionary<string, BitmapSource> Icons = [];
    public static string ElementFor(string key) => key switch
    {
        "Ayaka" => "cryo", "Klee" => "pyro", "Furina" => "hydro",
        "Varesa" => "electro", "Zhongli" => "geo",
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "未知角色主题")
    };
    public static BitmapSource IconFor(string key)
    {
        string element = ElementFor(key);
        if (Icons.TryGetValue(element, out var cached)) return cached;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri($"pack://application:,,,/Resources/Elements/{element}.png", UriKind.Absolute);
        image.EndInit(); image.Freeze(); Icons.Add(element, image); return image;
    }
    /// <summary>预览保留官方原色原形；只缩放，不描摹或重新设计符号。</summary>
    public static DrawingGroup Create(string key, Color accent)
    {
        if (key == "Gear") return CreatePupilGear(accent);
        var drawing = new DrawingGroup();
        drawing.Children.Add(new ImageDrawing(IconFor(key), new Rect(0, 0, CanvasSize, CanvasSize)));
        RenderOptions.SetBitmapScalingMode(drawing, BitmapScalingMode.HighQuality);
        drawing.Freeze(); return drawing;
    }
    /// <summary>背景用原图透明通道作遮罩适配主题色，形状与孔洞完全来自原图。</summary>
    public static DrawingGroup CreateWatermark(string key, Color accent)
    {
        if (key == "Gear") return CreatePupilGear(accent);
        // 只在切主题时着色 84×84 原图，渲染时直接画冻结的位图，避免实时 OpacityMask 离屏合成。
        var source = new FormatConvertedBitmap(IconFor(key), PixelFormats.Bgra32, null, 0);
        int width = source.PixelWidth, height = source.PixelHeight, stride = width * 4;
        byte[] pixels = new byte[stride * height]; source.CopyPixels(pixels, stride, 0);
        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            int alpha = pixels[offset + 3];
            pixels[offset] = (byte)(accent.B * alpha / 255);
            pixels[offset + 1] = (byte)(accent.G * alpha / 255);
            pixels[offset + 2] = (byte)(accent.R * alpha / 255);
        }
        var tinted = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        tinted.Freeze();
        var drawing = new DrawingGroup();
        drawing.Children.Add(new ImageDrawing(tinted, new Rect(0, 0, CanvasSize, CanvasSize)));
        RenderOptions.SetBitmapScalingMode(drawing, BitmapScalingMode.HighQuality);
        drawing.Freeze(); return drawing;
    }

    /// <summary>
    /// 依据官网桑多涅原画中围绕瞳孔的齿轮环简化：圆形中心孔、短方齿、细外环。
    /// 这是主题用矢量纹饰，不是官方图标，也不宣称精确复刻原画的齿数。
    /// </summary>
    private static DrawingGroup CreatePupilGear(Color accent)
    {
        var brush = new SolidColorBrush(accent); brush.Freeze();
        var gear = new StreamGeometry { FillRule = FillRule.EvenOdd };
        using (var context = gear.Open())
        {
            // 统一圆心和半径；8 个短齿，方齿的边缘由角度区间生成，禁止非等比缩放。
            Point Polar(double radius, double degrees)
            {
                double radians = degrees * Math.PI / 180;
                return new Point(160 + radius * Math.Cos(radians), 160 + radius * Math.Sin(radians));
            }
            context.BeginFigure(Polar(112, -90 - 22.5), true, true);
            for (int tooth = 0; tooth < 8; tooth++)
            {
                double center = -90 + tooth * 45;
                context.LineTo(Polar(112, center - 22.5), true, false);
                context.LineTo(Polar(112, center - 10), true, false);
                context.LineTo(Polar(133, center - 8.5), true, false);
                context.LineTo(Polar(133, center + 8.5), true, false);
                context.LineTo(Polar(112, center + 10), true, false);
                context.ArcTo(Polar(112, center + 22.5), new Size(112, 112), 0, false, SweepDirection.Clockwise, true, false);
            }
            context.BeginFigure(new Point(160, 72), true, true);
            context.ArcTo(new Point(160, 248), new Size(88, 88), 0, false, SweepDirection.Clockwise, true, false);
            context.ArcTo(new Point(160, 72), new Size(88, 88), 0, false, SweepDirection.Clockwise, true, false);
        }
        gear.Freeze();
        var drawing = new DrawingGroup();
        // 细外环对应虹膜边缘，中心保留通透的圆孔，避免普通机械齿轮的辐条造型。
        var rimPen = new Pen(brush, 3); rimPen.Freeze();
        drawing.Children.Add(new GeometryDrawing(null, rimPen, new EllipseGeometry(new Point(160, 160), 149, 149)));
        drawing.Children.Add(new GeometryDrawing(brush, null, gear));
        drawing.Freeze(); return drawing;
    }
}
