using System.Windows.Controls;
using GenshinGachaAnalyzer.WPF.ViewModels;
namespace GenshinGachaAnalyzer.WPF.Views;
public partial class DetailView : UserControl
{
    public DetailView() => InitializeComponent();
    private void TimelineScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.OriginalSource is ScrollViewer viewer && viewer.ScrollableHeight > 0 &&
            viewer.VerticalOffset >= viewer.ScrollableHeight - 3 && DataContext is DetailViewModel { HasMore: true } model)
            model.LoadMore();
    }
}
