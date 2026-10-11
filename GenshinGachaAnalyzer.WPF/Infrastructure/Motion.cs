using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

public static class Motion
{
    public static readonly DependencyProperty InteractiveProperty = DependencyProperty.RegisterAttached("Interactive", typeof(bool), typeof(Motion), new PropertyMetadata(false, Attach));
    public static bool GetInteractive(DependencyObject obj) => (bool)obj.GetValue(InteractiveProperty);
    public static void SetInteractive(DependencyObject obj, bool value) => obj.SetValue(InteractiveProperty, value);
    private static void Attach(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is not Control control || !(bool)args.NewValue) return;
        var transform = new ScaleTransform(1, 1); control.RenderTransform = transform; control.RenderTransformOrigin = new(.5, .5);
        void Animate(double target, int milliseconds)
        {
            var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation); transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
        control.MouseEnter += (_, _) => Animate(1.018, 130);
        control.MouseLeave += (_, _) => Animate(1, 170);
        control.PreviewMouseLeftButtonDown += (_, _) => Animate(.975, 80);
        control.PreviewMouseLeftButtonUp += (_, _) => Animate(control.IsMouseOver ? 1.018 : 1, 160);
    }
}
