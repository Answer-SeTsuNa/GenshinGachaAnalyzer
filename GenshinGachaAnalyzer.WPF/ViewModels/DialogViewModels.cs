using System.Collections.ObjectModel;
using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.WPF.Infrastructure;

namespace GenshinGachaAnalyzer.WPF.ViewModels;

public sealed record PreviewRow(string Uid, string Pool, int Count);
public sealed class ImportPreviewViewModel(PreparedImport import)
{
    public string Version => import.Version;
    public string Summary => $"{import.Accounts.Count} 个账号 · {import.Records.Count:N0} 条记录";
    public IReadOnlyList<PreviewRow> Rows => import.Counts.Select(c => new PreviewRow(c.Uid, c.Pool, c.Count)).ToArray();
    public IReadOnlyList<string> Warnings => import.Warnings;
}
public sealed class ExportAccountViewModel(Account account) : ObservableObject
{
    private bool _isSelected = true;
    public string Uid => account.Uid;
    public string Display => Uid;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
}
public sealed class ExportSelectionViewModel(IEnumerable<Account> accounts)
{
    public IReadOnlyList<string> Versions => GenshinGachaAnalyzer.Core.Services.UigfService.SupportedVersions;
    public string SelectedVersion { get; set; } = "v4.1";
    public ObservableCollection<ExportAccountViewModel> Accounts { get; } = new(accounts.Select(a => new ExportAccountViewModel(a)));
}
public sealed class ClearAccountViewModel(string uid, int count) : ObservableObject
{
    private string _confirmation = "";
    public string Uid => uid;
    public string Summary => $"将移除账号 {uid} 及其 {count:N0} 条祈愿记录。其他账号不受影响。";
    public string Confirmation { get => _confirmation; set { Set(ref _confirmation, value); Raise(nameof(CanConfirm)); } }
    public bool CanConfirm => Confirmation.Trim() == uid;
}
public sealed class WriteResultViewModel(WriteReport report)
{
    public string Summary => $"新增 {report.Added:N0} 条 · 重复跳过 {report.Duplicates:N0} 条 · 冲突 {report.Conflicts:N0} 个";
    public bool HasConflicts => report.Conflicts > 0;
    public IReadOnlyList<string> ConflictIds => report.ConflictIds;
    public IReadOnlyList<RecordConflict> Details => report.Details;
    public RecordConflict? SelectedConflict { get; set; }
    public string? LogPath => report.LogPath;
    public string? LogWarning => report.LogWarning;
    public bool HasLog => report.LogPath is not null;
    public System.Windows.Input.ICommand OpenLogCommand { get; } = new RelayCommand(_ =>
    {
        if (report.LogPath is { } path) OpenSafely(path);
    });
    public System.Windows.Input.ICommand OpenLogFolderCommand { get; } = new RelayCommand(_ =>
    {
        if (report.LogPath is { } path) OpenSafely(Path.GetDirectoryName(path)!);
    });
    private static void OpenSafely(string path)
    {
        try { new Services.AppDialogs().OpenPath(path); }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or Core.Helpers.UserFacingException or UnauthorizedAccessException)
        { new Services.AppDialogs().Explain("无法打开日志", "日志文件或查看程序不可用。可从设置页打开数据目录，查看 logs 文件夹。"); }
    }
}
