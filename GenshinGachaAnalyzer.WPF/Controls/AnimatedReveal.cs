using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace GenshinGachaAnalyzer.WPF.Controls;

/// <summary>按真实内容高度平滑收起，避免 Visibility 一帧跳变与错误的固定高度。</summary>
public sealed class AnimatedReveal : Decorator
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(AnimatedReveal), new PropertyMetadata(true, Changed));
    public static readonly DependencyProperty RevealProperty = DependencyProperty.Register(nameof(Reveal), typeof(double), typeof(AnimatedReveal), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public double Reveal { get => (double)GetValue(RevealProperty); set => SetValue(RevealProperty, value); }
    public AnimatedReveal() { ClipToBounds = true; }
    private static void Changed(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        var reveal = (AnimatedReveal)obj; double target = (bool)args.NewValue ? 1 : 0;
        if (!reveal.IsLoaded) { reveal.BeginAnimation(RevealProperty, null); reveal.Reveal = target; return; }
        reveal.BeginAnimation(RevealProperty, new DoubleAnimation(reveal.Reveal, target, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null) return default;
        Child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return new(Child.DesiredSize.Width, Child.DesiredSize.Height * Reveal);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        Opacity = Reveal; return finalSize;
    }
}
