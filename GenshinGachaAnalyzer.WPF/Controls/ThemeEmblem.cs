using System.Windows;
using System.Windows.Media;
using GenshinGachaAnalyzer.WPF.Services;

namespace GenshinGachaAnalyzer.WPF.Controls;

public sealed class ThemeEmblem : FrameworkElement
{
    public ThemeEmblem()
    {
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        Loaded += (_, _) => { ThemeService.Changed += Refresh; Refresh(); };
        Unloaded += (_, _) => ThemeService.Changed -= Refresh;
    }
    private void Refresh() => InvalidateVisual();
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Application.Current.Resources["ThemeDrawing"] is not Drawing drawing) return;
        double size = Math.Min(ActualWidth, ActualHeight);
        dc.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
        dc.PushTransform(new ScaleTransform(size / ThemeArtwork.CanvasSize, size / ThemeArtwork.CanvasSize));
        dc.DrawDrawing(drawing); dc.Pop(); dc.Pop();
    }
}
