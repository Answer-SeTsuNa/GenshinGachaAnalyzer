using System.Windows;
using GenshinGachaAnalyzer.Core.Services;
namespace GenshinGachaAnalyzer.WPF.Views;
public partial class UrlWindow : Window
{
    public GachaCredential? Credential { get; private set; }
    public UrlWindow() { InitializeComponent(); Loaded += (_, _) => UrlText.Focus(); Closed += (_, _) => UrlText.Clear(); }
    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        Credential = GachaLogService.TryParseCredential(UrlText.Text.Trim());
        if (Credential is null) { ErrorText.Text = "链接无效：需要含 authkey 和国服 region 的官方 HTTPS 祈愿链接，不支持国际服与云原神。"; return; }
        DialogResult = true;
    }
}
