using System.Collections.ObjectModel;
using System.Windows.Input;
using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.Core.Services;
using GenshinGachaAnalyzer.WPF.Infrastructure;

namespace GenshinGachaAnalyzer.WPF.ViewModels;

/// <summary>只读原始记录表格。按当前账号、卡池快照分页，切换账号后不会复用旧快照。</summary>
public sealed class RecordTableViewModel : ObservableObject
{
    private readonly IReadOnlyList<GachaRecord> _records;
    private readonly WishCatalogService _catalog;
    private IReadOnlyList<GachaRecord> _filtered = [];
    private int _page = 1;
    private string _search = "";
    public string Search { get => _search; set { if (Set(ref _search, value)) Filter(); } }
    public int PageSize => 50;
    public int TotalPages => Math.Max(1, (_filtered.Count + PageSize - 1) / PageSize);
    public string PageText => $"第 {_page} / {TotalPages} 页 · 共 {_filtered.Count:N0} 条 · 每页 {PageSize} 条";
    public ObservableCollection<RecordRowViewModel> Rows { get; } = [];
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
    public RecordTableViewModel(IEnumerable<GachaRecord> records, WishCatalogService catalog)
    {
        _catalog = catalog;
        _records = records.OrderByDescending(r => r.Time).ThenByDescending(r => r.Id.Length).ThenByDescending(r => r.Id, StringComparer.Ordinal).ToArray();
        PreviousCommand = new RelayCommand(_ => { _page--; Load(); }, () => _page > 1);
        NextCommand = new RelayCommand(_ => { _page++; Load(); }, () => _page < TotalPages);
        Filter();
    }
    private void Filter()
    {
        var query = Search.Trim();
        _filtered = _records.Where(r => query.Length == 0 || _catalog.Resolve(r).Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || r.Time.ToString("yyyy-MM-dd").Contains(query, StringComparison.Ordinal)).ToArray();
        _page = 1; Load();
    }
    private void Load()
    {
        Rows.Clear();
        foreach (var record in _filtered.Skip((_page - 1) * PageSize).Take(PageSize)) Rows.Add(new(record, _catalog));
        Raise(nameof(PageText)); CommandManager.InvalidateRequerySuggested();
    }
}
