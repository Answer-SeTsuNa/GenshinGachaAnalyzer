using System.Windows;
namespace GenshinGachaAnalyzer.WPF.Views;
public partial class ImportPreviewWindow : Window
{
    public ImportPreviewWindow() => InitializeComponent();
    private void ConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
