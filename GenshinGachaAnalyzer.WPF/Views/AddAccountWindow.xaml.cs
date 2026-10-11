using System.Windows;
using GenshinGachaAnalyzer.WPF.Services;
namespace GenshinGachaAnalyzer.WPF.Views;
public partial class AddAccountWindow : Window
{
    public DataSourceChoice Choice { get; private set; }
    public AddAccountWindow() => InitializeComponent();
    private void GameClick(object sender, RoutedEventArgs e) { Choice = DataSourceChoice.Game; DialogResult = true; }
    private void ImportClick(object sender, RoutedEventArgs e) { Choice = DataSourceChoice.Uigf; DialogResult = true; }
    private void UrlClick(object sender, RoutedEventArgs e) { Choice = DataSourceChoice.Url; DialogResult = true; }
}
