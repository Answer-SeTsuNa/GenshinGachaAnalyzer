using System.Windows;
using GenshinGachaAnalyzer.Core.Services;

namespace GenshinGachaAnalyzer.WPF.Views;
public partial class DeveloperLogWindow : Window
{
    public DeveloperLogWindow() { InitializeComponent(); Loaded += async (_, _) => await RefreshAsync(); }
    private async void RefreshClicked(object sender, RoutedEventArgs e) => await RefreshAsync();
    public async Task RefreshAsync()
    {
        try { LogPath.Text = DiagnosticLog.CurrentPath ?? "日志目录尚未配置"; LogText.Text = await Task.Run(DiagnosticLog.ReadLatest); LogText.ScrollToEnd(); }
        catch (Exception error) { LogText.Text = DiagnosticLog.Sanitize("读取日志失败：" + error.Message); }
    }
}
