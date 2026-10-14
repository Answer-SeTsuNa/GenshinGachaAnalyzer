using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace GenshinGachaAnalyzer.WPF.Controls;

/// <summary>卡池原图后台解码，最多缓存 12 张缩略图；切换选中期次不会等待图片 I/O。</summary>
public sealed class BannerImage : Grid
{
    public static readonly DependencyProperty FileNameProperty = DependencyProperty.Register(nameof(FileName), typeof(string), typeof(BannerImage), new PropertyMetadata(null, Changed));
    public string? FileName { get => (string?)GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }
    private static readonly Dictionary<string, BitmapSource> Cache = new();
    private static readonly Queue<string> Order = new();
    private static readonly SemaphoreSlim Slots = new(2);
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _placeholder = new() { Text = "✦  正在展开祈愿画卷…", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 15 };
    private int _request;
    public BannerImage()
    {
        ClipToBounds = true; _placeholder.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        Children.Add(_placeholder); Children.Add(_image); MinHeight = 150;
    }
    private static void Changed(DependencyObject obj, DependencyPropertyChangedEventArgs args) => _ = ((BannerImage)obj).LoadAsync((string?)args.NewValue);
    private async Task LoadAsync(string? file)
    {
        int request = ++_request;
        // 热缓存不再先清空图片、排后台任务和重播淡入，避免期次切换闪白。
        lock (Cache)
        {
            if (file is not null && Cache.TryGetValue(file, out var saved))
            {
                _image.BeginAnimation(OpacityProperty, null); _image.Opacity = 1;
                _image.Source = saved; _placeholder.Visibility = Visibility.Collapsed; return;
            }
        }
        _image.Source = null; _placeholder.Visibility = Visibility.Visible;
        _placeholder.Text = "✦  正在展开祈愿画卷…";
        if (file is null || file != Path.GetFileName(file)) { _placeholder.Text = "该期卡池图片未收录"; return; }
        var bitmap = await Task.Run(async () =>
        {
            lock (Cache) { if (Cache.TryGetValue(file, out var saved)) return saved; }
            await Slots.WaitAsync();
            try
            {
                lock (Cache) { if (Cache.TryGetValue(file, out var saved)) return saved; }
                string path = Path.Combine(AppContext.BaseDirectory, "Resources", "Banners", file);
                if (!File.Exists(path)) return null;
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 1280; image.UriSource = new Uri(path); image.EndInit(); image.Freeze();
                lock (Cache) { if (!Cache.ContainsKey(file)) { Cache[file] = image; Order.Enqueue(file); while (Order.Count > 12) Cache.Remove(Order.Dequeue()); } }
                return (BitmapSource)image;
            }
            catch (Exception error) when (error is IOException or NotSupportedException or FormatException) { return null; }
            finally { Slots.Release(); }
        });
        if (request != _request) return;
        if (bitmap is null) { _placeholder.Text = "该期卡池图片不可用"; return; }
        _image.Source = bitmap; _placeholder.Visibility = Visibility.Collapsed;
        _image.BeginAnimation(OpacityProperty, null); _image.Opacity = 1;
        if (RenderOptions.ProcessRenderMode != System.Windows.Interop.RenderMode.SoftwareOnly)
            _image.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)) { FillBehavior = FillBehavior.Stop });
    }
}

