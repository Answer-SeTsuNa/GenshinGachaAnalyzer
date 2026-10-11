using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GenshinGachaAnalyzer.WPF.Controls;

/// <summary>缓存真实页面视觉树；等布局就绪再播放入场，避免动画在图片布局等待中耗尽。</summary>
public sealed class TransitionPresenter : Grid
{
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(TransitionPresenter), new PropertyMetadata(null, Changed));
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    private readonly Dictionary<object, ContentPresenter> _views = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<object> _order = new();
    private int _revision;
    private ContentPresenter? _loading;
    public TransitionPresenter() { ClipToBounds = true; }
    private static void Changed(DependencyObject obj, DependencyPropertyChangedEventArgs args) => ((TransitionPresenter)obj).Show(args.NewValue);
    private void Show(object? page)
    {
        int revision = ++_revision;
        foreach (UIElement child in Children) child.Visibility = Visibility.Collapsed;
        if (page is null) return;
        ContentPresenter view;
        if (page is ViewModels.LoadingViewModel or ViewModels.WelcomeViewModel)
        {
            // 临时加载帧不占据页面缓存，否则首次构建时会把真正的页面挤出并迫使重建。
            if (_loading is null) { _loading = CreatePresenter(); Children.Add(_loading); }
            view = _loading; view.Content = page;
        }
        else if (!_views.TryGetValue(page, out view!))
        {
            view = CreatePresenter(); view.Content = page;
            _views[page] = view; _order.Enqueue(page); Children.Add(view);
            while (_order.Count > 9) { var old = _order.Dequeue(); Children.Remove(_views[old]); _views.Remove(old); }
        }
        view.Visibility = Visibility.Visible; view.BeginAnimation(OpacityProperty, null); view.Opacity = 0;
        var offset = (TranslateTransform)view.RenderTransform; offset.BeginAnimation(TranslateTransform.YProperty, null); offset.Y = 18;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (revision != _revision) return;
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = easing });
            view.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)));
        }));
    }
    private static ContentPresenter CreatePresenter() => new() { HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch, RenderTransform = new TranslateTransform() };
}
