using System.Windows;
using GenshinGachaAnalyzer.WPF.Services;
using GenshinGachaAnalyzer.WPF.ViewModels;
namespace GenshinGachaAnalyzer.WPF.Views;
public partial class ExportWindow : Window
{
    public ExportWindow() => InitializeComponent();
    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ExportSelectionViewModel model && model.Accounts.Any(a => a.IsSelected)) DialogResult = true;
        else ValidationText.Text = "请至少选择一个账号。";
    }
    private void UigfClick(object sender, RoutedEventArgs e)
    {
        try { new AppDialogs().OpenLink("https://uigf.org/"); }
        catch { ValidationText.Text = "无法打开浏览器，请稍后重试。"; }
    }
}
