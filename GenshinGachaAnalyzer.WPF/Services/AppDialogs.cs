using System.Diagnostics;
using System.Windows;
using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.WPF.ViewModels;
using GenshinGachaAnalyzer.WPF.Views;
using Microsoft.Win32;

namespace GenshinGachaAnalyzer.WPF.Services;

public sealed class AppDialogs : IAppDialogs
{
    public string ExportVersion { get; private set; } = "v4.1";
    private static Window? Owner => Application.Current.MainWindow;
    private DeveloperLogWindow? _logWindow;
    public string? ChooseWallpaperFile()
    {
        var picker = new OpenFileDialog { Filter = "壁纸图片|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true, Title = "选择本地壁纸（不会上传）" };
        return picker.ShowDialog(Owner) == true ? picker.FileName : null;
    }
    public void ShowDiagnosticLog()
    {
        if (_logWindow is not null) { _logWindow.Activate(); _ = _logWindow.RefreshAsync(); return; }
        _logWindow = new DeveloperLogWindow { Owner = Owner };
        _logWindow.Closed += (_, _) => _logWindow = null;
        _logWindow.Show();
    }
    public string? ChooseImportFile()
    {
        var dialog = new OpenFileDialog { Filter = "UIGF JSON 文件 (*.json)|*.json", CheckFileExists = true, Multiselect = false, Title = "选择 UIGF v4.2–v2.2 标准文件" };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }
    public string? ChooseExportFile()
    {
        var dialog = new SaveFileDialog { Filter = "UIGF JSON 文件 (*.json)|*.json", DefaultExt = ".json", AddExtension = true, OverwritePrompt = true, FileName = $"UIGF-{ExportVersion}-{DateTime.Now:yyyyMMdd-HHmmss}.json", Title = $"导出 UIGF {ExportVersion}" };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }
    public string? ChooseGameFolder()
    {
        var dialog = new OpenFolderDialog { Title = "选择含 YuanShen.exe 的国服游戏目录", Multiselect = false };
        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }
    public DataSourceChoice ChooseDataSource()
    {
        var dialog = new AddAccountWindow { Owner = Owner };
        dialog.ShowDialog(); return dialog.Choice;
    }
    public Core.Services.GachaCredential? ChooseGachaUrl()
    {
        var dialog = new UrlWindow { Owner = Owner };
        return dialog.ShowDialog() == true ? dialog.Credential : null;
    }
    public bool ConfirmImport(PreparedImport import) =>
        new ImportPreviewWindow { Owner = Owner, DataContext = new ImportPreviewViewModel(import) }.ShowDialog() == true;
    public async Task<Core.Services.GachaCredential?> ChooseMiyousheCredentialAsync()
    {
        using var client = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        var dialog = new MiyousheLoginWindow(new Core.Services.MiyousheLoginService(client)) { Owner = Owner };
        dialog.ShowDialog(); await dialog.OperationReady;
        return dialog.Credential;
    }
    public bool ConfirmClearAccount(string uid, int count) =>
        new ClearAccountWindow { Owner = Owner, DataContext = new ClearAccountViewModel(uid, count) }.ShowDialog() == true;
    public IReadOnlyList<string>? SelectExportAccounts(IReadOnlyList<Account> accounts)
    {
        var model = new ExportSelectionViewModel(accounts);
        if (new ExportWindow { Owner = Owner, DataContext = model }.ShowDialog() != true) return null;
        ExportVersion = model.SelectedVersion;
        return model.Accounts.Where(a => a.IsSelected).Select(a => a.Uid).ToArray();
    }
    public void ShowWriteResult(WriteReport result) => new WriteResultWindow { Owner = Owner, DataContext = new WriteResultViewModel(result) }.ShowDialog();
    public void Explain(string title, string message) => MessageBox.Show(Owner!, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    public void OpenPath(string path)
    {
        if (File.Exists(path) && Path.GetExtension(path).Equals(".log", StringComparison.OrdinalIgnoreCase))
        {
            var start = new ProcessStartInfo("notepad.exe") { UseShellExecute = true };
            start.ArgumentList.Add(path); Process.Start(start);
        }
        else if (Directory.Exists(path) || File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else throw new Core.Helpers.UserFacingException("文件或目录不存在，请先完成初始化或检查安装目录。");
    }
    public void OpenLink(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new ArgumentException("仅允许 HTTPS 链接。");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
    public void OpenStoreUpdates() => Process.Start(new ProcessStartInfo("ms-windows-store://downloadsandupdates") { UseShellExecute = true });
}
