using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

/// <summary>每个物品只解码一次缩略图；冻结后可跨线程共享，避免计时页反复解码原图。</summary>
public static class ItemIconCache
{
    private static readonly ConcurrentDictionary<string, Task<ImageSource?>> Images = new();
    private static readonly SemaphoreSlim DecodeSlots = new(4);
    public static Task<ImageSource?> GetAsync(string id) => Images.GetOrAdd(id, key => Task.Run(async () =>
    {
        await DecodeSlots.WaitAsync(); try { return Load(key); } finally { DecodeSlots.Release(); }
    }));
    private static ImageSource? Load(string id)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Resources", "Items", id + ".png");
        if (!File.Exists(path)) return null;
        try
        {
            var bitmap = new BitmapImage(); bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 160;
            bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }
        catch (Exception error) when (error is IOException or NotSupportedException or FormatException) { return null; }
    }
}

