using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

/// <summary>每个物品只解码一次缩略图；冻结后可跨线程共享，避免计时页反复解码原图。</summary>
public static class ItemIconCache
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<ImageSource?>>> Images = new();
    private static readonly SemaphoreSlim DecodeSlots = new(2);
    public static Task<ImageSource?> GetAsync(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 20 || !id.All(char.IsAsciiDigit)) return Task.FromResult<ImageSource?>(null);
        return Images.GetOrAdd(id, key => new Lazy<Task<ImageSource?>>(() => Task.Run(async () =>
        {
            await DecodeSlots.WaitAsync(); try { return Load(key); } finally { DecodeSlots.Release(); }
        }))).Value;
    }
    private static ImageSource? Load(string id)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Resources", "Items", id + ".png");
        if (!File.Exists(path)) return null;
        try
        {
            var bitmap = new BitmapImage(); bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 160;
            using var stream = File.OpenRead(path);
            bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }
        catch (Exception error) when (error is IOException or NotSupportedException or FormatException) { return null; }
    }
}

