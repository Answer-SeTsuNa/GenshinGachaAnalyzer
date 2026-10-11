using System.Windows;
using GenshinGachaAnalyzer.WPF.ViewModels;
namespace GenshinGachaAnalyzer.WPF.Views;
public partial class ClearAccountWindow : Window
{
    public ClearAccountWindow() => InitializeComponent();
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClearAccountViewModel { CanConfirm: true }) DialogResult = true;
    }
}
