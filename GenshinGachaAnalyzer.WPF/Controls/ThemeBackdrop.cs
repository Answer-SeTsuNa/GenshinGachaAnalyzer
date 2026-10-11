using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GenshinGachaAnalyzer.Core.Services;
using GenshinGachaAnalyzer.WPF.Services;

namespace GenshinGachaAnalyzer.WPF.Controls;

/// <summary>独立背景层：仅对壁纸模糊，不模糊文字或整个页面；壁纸离线读取并限制解码尺寸。</summary>
public sealed class ThemeBackdrop : Grid
{
    private readonly Image _wallpaper = new() { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
    private readonly Ornament _ornament = new();
    private string? _loadedPath;
    private double _loadedBlur = -1;
    private BitmapSource? _original;
    private CancellationTokenSource? _processing;
    private int _revision;
    internal Task Ready { get; private set; } = Task.CompletedTask;
    public ThemeBackdrop()
    {
        IsHitTestVisible = false; ClipToBounds = true;
        Children.Add(_wallpaper); Children.Add(_ornament);
        Loaded += (_, _) => { ThemeService.Changed += Refresh; Refresh(); };
        Unloaded += (_, _) => ThemeService.Changed -= Refresh;
    }
    private void Refresh()
    {
        var settings = ThemeService.Appearance;
        _ornament.InvalidateVisual();
        _wallpaper.Opacity = settings.WallpaperOpacity;
        // 主题色、透明度和页面切换复用同一张背景，不重新解码或施加实时效果。
        if (_loadedPath == settings.WallpaperPath && Math.Abs(_loadedBlur - settings.WallpaperBlur) < .01) return;
        bool samePath = _loadedPath == settings.WallpaperPath;
        _loadedPath = settings.WallpaperPath; _loadedBlur = settings.WallpaperBlur;
        _processing?.Cancel(); _processing?.Dispose(); _processing = new();
        var token = _processing.Token;
        int revision = ++_revision;
        if (!samePath) _original = null;
        if (settings.WallpaperPath is not { } path) { _wallpaper.Source = null; Ready = Task.CompletedTask; return; }
        var original = _original;
        double radius = settings.WallpaperBlur;
        Ready = ProcessAsync(path, original, radius, revision, token);
    }
    private async Task ProcessAsync(string path, BitmapSource? original, double radius, int revision, CancellationToken token)
    {
        try
        {
            await Task.Delay(120, token); // 合并连续滑块变化，继续显示上一张可用壁纸。
            var processed = await Task.Run(() =>
            {
                var source = original ?? WallpaperRenderer.Load(path);
                return (source, blurred: WallpaperRenderer.Blur(source, radius, token));
            }, token);
            if (revision == _revision) { _original = processed.source; _wallpaper.Source = processed.blurred; }
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (revision == _revision) _wallpaper.Source = null;
            DiagnosticLog.Error("主题壁纸读取失败，使用本地纹饰背景", error);
        }
    }
    private sealed class Ornament : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var profile = ThemeProfile.All.First(p => p.Key == ThemeService.StyleKey || ThemeService.StyleKey == "Puppet" && p.Key == "Gear");
            var settings = ThemeService.Appearance;
            var background = (Color)ColorConverter.ConvertFromString(ThemeService.IsDark ? profile.DarkBackground : profile.LightBackground);
            var accent = (Color)ColorConverter.ConvertFromString(ThemeService.IsDark ? profile.NightAccent : profile.Accent);
            var mask = new SolidColorBrush(background) { Opacity = settings.SemiTransparent ? settings.OverlayOpacity : 1 };
            var bounds = new Rect(RenderSize); dc.DrawRectangle(mask, null, bounds);
            var motif = (Drawing)Application.Current.Resources["ThemeWatermark"];
            // 原图始终等比显示。背景只放一大一小两枚低对比元素，避免复杂装饰抢占数据。
            void DrawElement(double x, double y, double size, double opacity)
            {
                dc.PushOpacity(opacity); dc.PushTransform(new TranslateTransform(x, y));
                double scale = size / ThemeArtwork.CanvasSize;
                dc.PushTransform(new ScaleTransform(scale, scale)); dc.DrawDrawing(motif);
                dc.Pop(); dc.Pop(); dc.Pop();
            }
            DrawElement(RenderSize.Width - 290, 12, 260, ThemeService.IsDark ? .13 : .085);
            DrawElement(12, RenderSize.Height - 135, 110, ThemeService.IsDark ? .07 : .045);
            var line = new Pen(new SolidColorBrush(Color.FromArgb(25, accent.R, accent.G, accent.B)), 1);
            dc.DrawLine(line, new(Math.Max(24, RenderSize.Width - 350), 22), new(RenderSize.Width - 24, 22));
            dc.DrawLine(line, new(RenderSize.Width - 24, 22), new(RenderSize.Width - 24, 145));
            dc.DrawLine(line, new(24, Math.Max(0, RenderSize.Height - 24)), new(200, Math.Max(0, RenderSize.Height - 24)));
        }
    }
}
