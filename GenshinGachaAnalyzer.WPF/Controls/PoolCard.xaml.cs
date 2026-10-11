using System.Windows.Controls;
using System.ComponentModel;
using System.Windows;
using GenshinGachaAnalyzer.WPF.ViewModels;
namespace GenshinGachaAnalyzer.WPF.Controls;
public partial class PoolCard : UserControl
{
    private PoolViewModel? _subscribed;
    public PoolCard()
    {
        InitializeComponent(); Loaded += (_, _) => Subscribe(); Unloaded += (_, _) => Unsubscribe();
        DataContextChanged += (_, _) => Subscribe();
    }
    private void Unsubscribe() { if (_subscribed is not null) _subscribed.PropertyChanged -= PoolChanged; _subscribed = null; }
    private void Subscribe() { Unsubscribe(); if (DataContext is PoolViewModel pool) { _subscribed = pool; pool.PropertyChanged += PoolChanged; } }
    private async void PoolChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PoolViewModel.AnchorTarget) || sender is not PoolViewModel pool || pool.AnchorTarget is not { } target) return;
        if (pool.IsGridLayout)
        {
            while (!pool.GridGoldItems.Contains(target) && pool.GridGoldItems.Count < pool.GoldItems.Count) pool.LoadMoreGrid();
            await Dispatcher.InvokeAsync(() => { GoldGrid.UpdateLayout(); (GoldGrid.ItemContainerGenerator.ContainerFromItem(target) as FrameworkElement)?.BringIntoView(); });
        }
        else GoldList.ScrollIntoView(target);
        target.IsHighlighted = true; await Task.Delay(1400); target.IsHighlighted = false;
    }
    private void GridScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer { IsVisible: true } viewer && DataContext is ViewModels.PoolViewModel pool &&
            e.VerticalChange > 0 && viewer.VerticalOffset + viewer.ViewportHeight >= viewer.ExtentHeight - 60)
            pool.LoadMoreGrid();
    }
}
