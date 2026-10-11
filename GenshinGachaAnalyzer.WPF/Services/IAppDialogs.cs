using GenshinGachaAnalyzer.Core.Models;

namespace GenshinGachaAnalyzer.WPF.Services;

public enum DataSourceChoice { Cancel, Game, Uigf, Url }
public interface IAppDialogs
{
    string ExportVersion => "v4.1";
    string? ChooseImportFile();
    string? ChooseExportFile();
    string? ChooseGameFolder();
    string? ChooseWallpaperFile() => null;
    void ShowDiagnosticLog() { }
    Core.Services.GachaCredential? ChooseGachaUrl() => null;
    DataSourceChoice ChooseDataSource();
    bool ConfirmImport(PreparedImport import);
    IReadOnlyList<string>? SelectExportAccounts(IReadOnlyList<Account> accounts);
    bool ConfirmClearAccount(string uid, int count);
    void ShowWriteResult(WriteReport result);
    void Explain(string title, string message);
    void OpenPath(string path);
    void OpenLink(string url);
}
