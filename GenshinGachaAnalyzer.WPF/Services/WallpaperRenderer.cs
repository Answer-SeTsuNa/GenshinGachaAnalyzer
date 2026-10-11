using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GenshinGachaAnalyzer.WPF.Services;

/// <summary>后台生成已模糊的冻结位图。页面动画不再逐帧执行全窗口 BlurEffect。</summary>
internal static class WallpaperRenderer
{
    public static BitmapSource Load(string path)
    {
        using var stream = File.OpenRead(path);
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 1600; image.StreamSource = stream; image.EndInit(); image.Freeze();
        return image;
    }

    public static BitmapSource Blur(BitmapSource source, double radius, CancellationToken cancellation)
    {
        if (radius < .5) return source;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        int width = source.PixelWidth, height = source.PixelHeight, stride = width * 4;
        var pixels = new byte[stride * height]; converted.CopyPixels(pixels, stride, 0);
        var scratch = new byte[pixels.Length];
        // 三次线性复杂度的盒式滤波近似高斯，保留预乘 Alpha，边缘采用重复像素。
        // 半径在后台处理；每行检查取消，快速拖动滑块不会堆积过期工作。
        int extent = Math.Clamp((int)Math.Round(radius / Math.Sqrt(3)), 1, 40);
        for (int pass = 0; pass < 3; pass++)
        {
            Filter(pixels, scratch, width, height, extent, false, cancellation);
            Filter(scratch, pixels, width, height, extent, true, cancellation);
        }
        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        result.Freeze(); return result;
    }

    private static void Filter(byte[] input, byte[] output, int width, int height, int radius, bool vertical, CancellationToken cancellation)
    {
        int lines = vertical ? width : height, length = vertical ? height : width;
        int step = vertical ? width * 4 : 4, denominator = 2 * radius + 1;
        for (int line = 0; line < lines; line++)
        {
            cancellation.ThrowIfCancellationRequested();
            int start = vertical ? line * 4 : line * width * 4;
            for (int channel = 0; channel < 4; channel++)
            {
                int sum = 0;
                for (int offset = -radius; offset <= radius; offset++)
                    sum += input[start + Math.Clamp(offset, 0, length - 1) * step + channel];
                for (int position = 0; position < length; position++)
                {
                    output[start + position * step + channel] = (byte)((sum + denominator / 2) / denominator);
                    int remove = Math.Clamp(position - radius, 0, length - 1);
                    int add = Math.Clamp(position + radius + 1, 0, length - 1);
                    sum += input[start + add * step + channel] - input[start + remove * step + channel];
                }
            }
        }
    }
}
