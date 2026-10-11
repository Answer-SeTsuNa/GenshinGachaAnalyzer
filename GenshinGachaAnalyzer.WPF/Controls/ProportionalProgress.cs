using System.Windows;
using System.Windows.Media;

namespace GenshinGachaAnalyzer.WPF.Controls;

/// <summary>可见文字区域的线性背景进度：没有最小填充宽度，也不被头像遮住。</summary>
public sealed class ProportionalProgress : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(ProportionalProgress), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ProportionalProgress), new FrameworkPropertyMetadata(90d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(ProportionalProgress), new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public double FillWidth => Maximum > 0 && double.IsFinite(Value) ? ActualWidth * Math.Clamp(Value / Maximum, 0, 1) : 0;
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (FillWidth > 0) dc.DrawRectangle(Fill, null, new Rect(0, 0, FillWidth, ActualHeight));
    }
}
