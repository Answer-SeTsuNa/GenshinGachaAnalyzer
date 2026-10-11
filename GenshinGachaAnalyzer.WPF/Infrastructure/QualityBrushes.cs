using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

/// <summary>使用参考项目原始稀有度底图；冻结后跨页面共享，避免重复解码。</summary>
public static class QualityBrushes
{
    private static readonly Dictionary<string, Brush> Cache = new();
    public static Brush Get(string rank)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(rank, out var saved)) return saved;
            string quality = rank switch { "5" => "ORANGE", "4" => "PURPLE", _ => "BLUE" };
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(Path.Combine(AppContext.BaseDirectory, "Resources", "Quality", $"UI_QUALITY_{quality}.png")); image.EndInit(); image.Freeze();
            var brush = new ImageBrush(image) { Stretch = Stretch.Fill }; brush.Freeze(); Cache[rank] = brush; return brush;
        }
    }
    public static Brush PullColor(double value, int maximum)
    {
        double t = Math.Clamp((value - 1) / Math.Max(1, maximum - 1), 0, 1);
        byte Mix(byte a, byte b) => (byte)Math.Round(a + (b - a) * t);
        var brush = new SolidColorBrush(Color.FromRgb(Mix(0x48, 0xFF), Mix(0xFF, 0x49), Mix(0x7A, 0x49))); brush.Freeze(); return brush;
    }
}
