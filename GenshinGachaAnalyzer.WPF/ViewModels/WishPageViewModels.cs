using System.Collections.ObjectModel;
using System.Windows.Input;
using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.Core.Services;
using GenshinGachaAnalyzer.WPF.Infrastructure;

namespace GenshinGachaAnalyzer.WPF.ViewModels;

public sealed record SectionTab(string Name, bool IsSelected)
{
    public string Glyph => Name switch { "总览" => "◈", "历史" => "▤", "角色" => "♙", "武器" => "⚔", "复刻日历" => "◷", "我的" => "♡", "平均" => "≈", "数量" => "#", "区间" => "↔", "抽卡足迹" => "✦", _ => "◷" };
}
public sealed record FilterOption(string Value, string Name) { public override string ToString() => Name; }
public class ItemDisplayViewModel(WishItem item) : ObservableObject
{
    public WishItem Item { get; } = item;
    public string Id => Item.Id;
    public string Name => Item.Name;
    public string Kind => Item.Kind;
    public string Rank => Item.Rank;
    public string RankText => Rank is "3" or "4" or "5" ? Rank + " 星" : "星级未知";
    public string Initial => Name.Length > 0 ? Name[..1] : "?";
    private System.Windows.Media.ImageSource? _icon;
    private bool _requested;
    public System.Windows.Media.ImageSource? Icon { get { if (!_requested) { _requested = true; _ = LoadIconAsync(); } return _icon; } }
    public string? IconPath => _icon is null ? null : Id;
    private async Task LoadIconAsync()
    {
        var image = await ItemIconCache.GetAsync(Id);
        Set(ref _icon, image, nameof(Icon)); Raise(nameof(IconPath));
    }
    public string QualityColor => Rank switch { "5" => "#A77636", "4" => "#8064A8", "3" => "#5C7C9B", _ => "#788087" };
    public System.Windows.Media.Brush QualityBackground => QualityBrushes.Get(Rank);
}
public sealed class GoldDisplayViewModel(FiveStarEntry entry, WishCatalogService catalog, int hardPity, bool cycle = false) : ItemDisplayViewModel(catalog.Resolve(entry.Record))
{
    public FiveStarEntry Entry { get; } = entry;
    private bool _highlighted;
    public bool IsHighlighted { get => _highlighted; set => Set(ref _highlighted, value); }
    public int? DisplayPulls => cycle ? Entry.UpCyclePulls : Entry.Pulls;
    public string PullText => cycle ? Entry.UpCyclePulls is { } n ? $"{n} 抽" : Entry.ObservedUpCyclePulls is { } observed ? $"{observed} 抽" : "周期不完整" : Entry.PullText;
    public string CompactPullText => cycle ? Entry.UpCyclePulls?.ToString() ?? (Entry.ObservedUpCyclePulls is { } observed ? $"{observed}" : "?") : Entry.Pulls?.ToString() ?? (Entry.ObservedPulls is { } n ? $"{n}" : "?");
    public double PullValue => DisplayPulls ?? (cycle ? Entry.ObservedUpCyclePulls ?? 0 : Entry.ObservedPulls ?? 0);
    public int Maximum => cycle ? hardPity * 2 : hardPity;
    public System.Windows.Media.Brush PullBrush => QualityBrushes.PullColor(PullValue, Maximum);
    public string TimeText => Entry.Record.Time.ToString("yyyy-MM-dd HH:mm");
    public string UpStatus => catalog.DescribeStatus(Entry.Record);
    public string Tooltip => $"{Name}\n{PullText} · {TimeText}\n{UpStatus}\n来源：{Entry.Record.Source}" + (DisplayPulls is null ? "\n星级缺失或抽数异常，此区间不参与均值与分布。" : "");
}
public sealed class RecordRowViewModel(GachaRecord record, WishCatalogService catalog) : ItemDisplayViewModel(catalog.Resolve(record))
{
    public GachaRecord Record { get; } = record;
    public string TimeText => Record.Time.ToString("yyyy-MM-dd HH:mm:ss");
    public string PoolName => PoolCatalog.NameOf(Record.GachaType);
    public string Source => Record.Source;
    public string RecordId => Record.Id;
}
public sealed class CollectionItemViewModel(ItemSummary summary) : ItemDisplayViewModel(summary.Item)
{
    public ItemSummary Summary { get; } = summary;
    public int Count => Summary.Count;
    public string CountText => $"×{Count:N0}";
    public string LastTimeText => Summary.LastTime.ToString("yyyy-MM-dd");
    public string Tooltip => $"{Name} · {RankText}\n记录内获取 {Count:N0} 次\n首次 {Summary.FirstTime:yyyy-MM-dd}\n最近 {LastTimeText}\n不代表游戏内当前持有数量。";
}
public sealed record CollectionSection(string Title, string Subtitle, IReadOnlyList<CollectionItemViewModel> Items);

public sealed class HistoryGroupViewModel(WishHistoryGroup group, WishCatalogService catalog)
{
    public WishHistoryGroup Group { get; } = group;
    public string Name => Group.Name;
    public string Pool => Group.Pool;
    public string Period => Group.Period;
    public string DateText => $"{Group.From:yyyy-MM-dd} — {Group.To:yyyy-MM-dd}";
    public string CountText => Group.Records.Count == 0 ? "无抽卡信息" : $"{Group.Records.Count:N0} 抽";
    public bool HasRecords => Group.Records.Count > 0;
    public string SummaryText => Group.Records.Count == 0 ? "无抽卡信息 · 已收录卡池排期与 UP 名单" : $"{Group.Records.Count:N0} 抽 · 五星 {Group.Records.Count(r => catalog.Resolve(r).Rank == "5")} · 四星 {Group.Records.Count(r => catalog.Resolve(r).Rank == "4")} · 三星 {Group.Records.Count(r => catalog.Resolve(r).Rank == "3")}";
    public string? BannerFile => Group.Event?.BannerFile;
    public string GoldCountText => $"当期五星 UP {Group.Records.Count(r => Group.Event?.UpGold.Contains(catalog.Resolve(r).Id) == true)}";
    public string PurpleCountText => $"四星 {Group.Records.Count(r => catalog.Resolve(r).Rank == "4")}";
    public string StandardCountText => $"非 UP 五星 {Group.Records.Count(r => catalog.IsCurrentUp(r) == false)}";
    public IReadOnlyList<HistoryUpItemViewModel> GoldItems => BannerItems.Where(i => i.Rank == "5").ToArray();
    public IReadOnlyList<HistoryUpItemViewModel> PurpleItems => BannerItems.Where(i => i.Rank == "4").ToArray();
    public IReadOnlyList<HistoryUpItemViewModel> BannerItems => Group.Event is null ? [] : Group.Event.UpGold.Concat(Group.Event.UpPurple).Select(id => new HistoryUpItemViewModel(catalog.Resolve(id), Group.Records.Count(r => catalog.Resolve(r).Id == id))).ToArray();
    public string UpLabel => Group.Event is null ? "未匹配到排期，仅展示原始记录" : Group.Event.Type == "500" ? "当期集录候选 · 不代表命定选择" : "当期 UP · 卡池信息，不代表命定选择";
    public IReadOnlyList<ItemDisplayViewModel> UpItems => Group.Event is null ? [] : Group.Event.UpGold.Concat(Group.Event.UpPurple).Select(id => new ItemDisplayViewModel(catalog.Resolve(id))).ToArray();
    public IReadOnlyList<CollectionSection> Sections => new[] { "5", "4", "3", "未知" }.Select(rank =>
        new CollectionSection(rank == "未知" ? "星级未知" : rank + " 星记录", "记录中的获取数量", catalog.Summarize(Group.Records.Where(r => catalog.Resolve(r).Rank == rank)).Select(s => new CollectionItemViewModel(s)).ToArray())).Where(s => s.Items.Count > 0).ToArray();
}
public sealed class HistoryUpItemViewModel(WishItem item, int count) : ItemDisplayViewModel(item)
{
    public int Count => count;
    public string CountText => $"×{Count}";
}

public sealed class HistoryViewModel : ObservableObject
{
    public MainViewModel Shell { get; }
    private readonly WishCatalogService _catalog;
    private readonly IReadOnlyList<WishHistoryGroup> _groups;
    private IReadOnlyList<GachaRecord> _filteredRecords = [];
    private HistoryGroupViewModel? _selectedGroup;
    private string _search = "";
    private FilterOption _pool = new("", "全部卡池"), _era = new("", "全部大版本");
    private bool _showSummary = true;
    private bool _timelineMode = true;
    public bool TimelineMode { get => _timelineMode; set { if (Set(ref _timelineMode, value)) { Apply(); Raise(nameof(DisplayModes)); } } }
    public IReadOnlyList<SectionTab> DisplayModes => [new("抽卡足迹", !TimelineMode), new("祈愿时间线", TimelineMode)];
    public ICommand DisplayModeCommand { get; }
    public string Search { get => _search; set { if (Set(ref _search, value)) Apply(); } }
    public FilterOption SelectedPool { get => _pool; set { if (value is not null && Set(ref _pool, value)) Apply(); } }
    public IReadOnlyList<FilterOption> Eras { get; } = [new("", "全部大版本"), new("7", "至冬"), new("6", "挪德卡莱"), new("5", "纳塔"), new("4", "枫丹"), new("3", "须弥"), new("2", "稻妻"), new("1", "蒙德 & 璃月")];
    public FilterOption SelectedEra { get => _era; set { if (value is not null && Set(ref _era, value)) Apply(); } }
    private int _pageIndex = 1;
    public int PageIndex { get => _pageIndex; private set => Set(ref _pageIndex, value); }
    public const int PageSize = 50;
    public int TotalPages => Math.Max(1, (_filteredRecords.Count + PageSize - 1) / PageSize);
    public string PageText => $"第 {PageIndex} / {TotalPages} 页 · 共 {_filteredRecords.Count:N0} 条 · 每页 {PageSize} 条";
    public ICommand PreviousPageCommand { get; }
    public ICommand NextPageCommand { get; }
    public bool ShowSummary { get => _showSummary; set => Set(ref _showSummary, value); }
    public ICommand ModeCommand { get; }
    public IReadOnlyList<FilterOption> Pools { get; } = new[] { new FilterOption("", "全部活动卡池") }.Concat(PoolCatalog.MainPools.Where(p => p.Type != "200").Select(p => new FilterOption(p.Type, p.Name))).ToArray();
    public IReadOnlyList<HistoryGroupViewModel> Groups { get; private set; } = [];
    public HistoryGroupViewModel? SelectedGroup { get => _selectedGroup; set { if (Set(ref _selectedGroup, value)) LoadGroup(); } }
    public ObservableCollection<RecordRowViewModel> Records { get; } = [];
    public bool HasMore => Records.Count < _filteredRecords.Count;
    public string ResultText => $"{Groups.Count} 个分组 · {Groups.Sum(g => g.Group.Records.Count):N0} 条记录";
    public bool HasGroups => Groups.Count > 0;
    public ICommand LoadMoreCommand { get; }
    public HistoryViewModel(MainViewModel shell, IReadOnlyList<GachaRecord> records, WishCatalogService catalog)
    {
        Shell = shell; _catalog = catalog; _groups = catalog.BuildHistory(records);
        _pool = Pools[0];
        PreviousPageCommand = new RelayCommand(_ => { PageIndex--; LoadPage(); }, () => PageIndex > 1);
        NextPageCommand = new RelayCommand(_ => { PageIndex++; LoadPage(); }, () => PageIndex < TotalPages);
        LoadMoreCommand = NextPageCommand; Apply();
        ModeCommand = new RelayCommand(p => ShowSummary = p?.ToString() == "汇总");
        DisplayModeCommand = new RelayCommand(p => TimelineMode = (p is SectionTab tab ? tab.Name : p?.ToString()) == "祈愿时间线");
    }
    private void Apply()
    {
        string? key = SelectedGroup?.Group.Key;
        bool Match(string text) => text.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase);
        bool MatchName(WishItem item) => item.Name != $"物品 {item.Id}" && Match(item.Name);
        Groups = _groups.Where(g => SelectedPool.Value == "" || (g.Event is not null ? PoolCatalog.Normalize(g.Event.Type) == SelectedPool.Value : g.Records.Any(r => r.PoolType == SelectedPool.Value)))
            .Where(g => SelectedEra.Value == "" || (g.Event?.Version.Split('.')[0] ?? "unknown") == SelectedEra.Value)
            .Where(g => TimelineMode || g.Records.Count > 0)
            .Where(g => string.IsNullOrWhiteSpace(Search) || Match(g.Name) || Match(g.Period) ||
                (g.Event?.UpGold.Concat(g.Event.UpPurple).Any(id => MatchName(_catalog.Resolve(id))) ?? false) ||
                Match(g.From.ToString("yyyy-MM-dd")) || Match(g.To.ToString("yyyy-MM-dd")) ||
                g.Records.Any(r => MatchName(_catalog.Resolve(r)) || Match(r.Time.ToString("yyyy-MM-dd"))))
            
            .Select(g => new HistoryGroupViewModel(g, _catalog)).ToArray();
        SelectedGroup = Groups.FirstOrDefault(g => g.Group.Key == key) ?? Groups.FirstOrDefault();
        Raise(nameof(Groups)); Raise(nameof(ResultText)); Raise(nameof(HasGroups));
    }
    private void LoadGroup() { _filteredRecords = SelectedGroup?.Group.Records ?? []; PageIndex = 1; LoadPage(); }
    private void LoadPage()
    {
        Records.Clear();
        foreach (var r in _filteredRecords.Skip((PageIndex - 1) * PageSize).Take(PageSize)) Records.Add(new(r, _catalog));
        Raise(nameof(PageText)); Raise(nameof(TotalPages));
        Raise(nameof(HasMore)); CommandManager.InvalidateRequerySuggested();
    }
}

public sealed class CollectionViewModel : ObservableObject
{
    public MainViewModel Shell { get; }
    private readonly IReadOnlyList<ItemSummary> _items;
    private readonly WishCatalogService _catalog;
    private string _search = "";
    private FilterOption _sort;
    private CollectionItemViewModel? _selectedItem;
    public string Kind { get; }
    public string Search { get => _search; set { if (Set(ref _search, value)) Apply(); } }
    public FilterOption SelectedSort { get => _sort; set { if (value is not null && Set(ref _sort, value)) Apply(); } }
    public IReadOnlyList<FilterOption> Sorts { get; } = [new("count", "获取数量"), new("recent", "最近获取"), new("name", "名称")];
    public IReadOnlyList<CollectionSection> Sections { get; private set; } = [];
    public string SummaryText => $"{_items.Select(i => i.Item.Id).Distinct().Count()} 种{Kind} · {_items.Sum(i => i.Count):N0} 次获取 · 只统计所选 UID 的祈愿记录";
    public bool HasItems => Sections.Any(s => s.Items.Count > 0);
    public CollectionItemViewModel? SelectedItem { get => _selectedItem; private set { Set(ref _selectedItem, value); Raise(nameof(HasSelection)); Raise(nameof(SelectedRecords)); } }
    public bool HasSelection => SelectedItem is not null;
    public IReadOnlyList<RecordRowViewModel> SelectedRecords => SelectedItem?.Summary.Records.Take(100).Select(r => new RecordRowViewModel(r, _catalog)).ToArray() ?? [];
    public string SelectionNote => SelectedItem?.Count > 100 ? "展示最近 100 次，可在历史页按物品名查看全部记录。" : "记录内获取次数，不代表命座、精炼或当前持有数量。";
    public ICommand SelectItemCommand { get; }
    public ICommand CloseSelectionCommand { get; }
    public CollectionViewModel(MainViewModel shell, IReadOnlyList<GachaRecord> records, WishCatalogService catalog, string kind)
    {
        Shell = shell; Kind = kind; _catalog = catalog; _items = catalog.Summarize(records, kind); _sort = Sorts[0];
        SelectItemCommand = new RelayCommand(p => { if (p is CollectionItemViewModel item) { SelectedItem = item; Raise(nameof(SelectionNote)); } }); Apply();
        CloseSelectionCommand = new RelayCommand(_ => SelectedItem = null);
    }
    private void Apply()
    {
        var filtered = _items.Where(i => string.IsNullOrWhiteSpace(Search) || i.Item.Name != $"物品 {i.Item.Id}" && i.Item.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase));
        var sorted = SelectedSort.Value switch { "recent" => filtered.OrderByDescending(i => i.LastTime), "name" => filtered.OrderBy(i => i.Item.Name), _ => filtered.OrderByDescending(i => i.Count).ThenByDescending(i => i.LastTime) };
        Sections = new[] { "5", "4", "3", "未知" }.Select(rank => new CollectionSection(rank == "未知" ? "星级未知" : rank + " 星" + Kind,
            "点击查看获取记录", sorted.Where(i => i.Item.Rank == rank).Select(i => new CollectionItemViewModel(i)).ToArray())).Where(s => s.Items.Count > 0).ToArray();
        SelectedItem = null; Raise(nameof(Sections)); Raise(nameof(HasItems));
    }
}

public sealed class CountdownItemViewModel(WishCountdown countdown) : ItemDisplayViewModel(countdown.Item)
{
    public WishCountdown Countdown { get; } = countdown;
    public string DaysText => Countdown.IsActive ? $"剩余 {Countdown.Days} 天" : $"{Countdown.Days} 天";
    public string Status => Countdown.IsActive ? Countdown.Histories[0].Type == "500" ? "当期集录候选" : "当期 UP" : "距最后收录排期结束";
    public string LastPeriod => Countdown.Histories[0].PeriodText;
    public string Tooltip => $"{Name}\n{Status} · {DaysText}\n排期收录 {Countdown.Histories.Count} 次，点击查看";
}
public sealed record CountdownSection(string Title, IReadOnlyList<CountdownItemViewModel> Items);
public sealed class CountdownViewModel : ObservableObject
{
    public MainViewModel Shell { get; }
    private readonly WishCatalogService _catalog;
    private string _search = "";
    private IReadOnlyList<CountdownItemViewModel> _items = [];
    private bool _loading = true;
    public bool IsLoading { get => _loading; private set => Set(ref _loading, value); }
    public Task Ready { get; }
    private CountdownItemViewModel? _selectedItem;
    public string Search { get => _search; set { if (Set(ref _search, value)) Apply(); } }
    public CountdownItemViewModel? SelectedItem { get => _selectedItem; private set { Set(ref _selectedItem, value); Raise(nameof(HasSelection)); Raise(nameof(Histories)); Raise(nameof(HistoryTitle)); } }
    public bool HasSelection => SelectedItem is not null;
    public IReadOnlyList<WishEvent> Histories => SelectedItem?.Countdown.Histories ?? [];
    public string HistoryTitle => SelectedItem is { } item ? $"{item.Name} · UP 历史共 {Histories.Count} 次" : "UP 历史";
    public IReadOnlyList<CountdownSection> Sections { get; private set; } = [];
    public string CoverageText => _catalog.CoverageText;
    public string AsOfText { get; }
    public DateOnly CalculatedDay { get; }
    public ICommand SelectItemCommand { get; }
    public ICommand CloseSelectionCommand { get; }
    public CountdownViewModel(MainViewModel shell, WishCatalogService catalog)
    {
        Shell = shell; _catalog = catalog; var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));
        CalculatedDay = DateOnly.FromDateTime(now.DateTime);
        AsOfText = now > catalog.CoveredThrough ? $"快照已过期：只计至 {catalog.CoveredThrough:yyyy-MM-dd}，之后是否复刻未知。" : $"按北京时间 {now:yyyy-MM-dd} 计算 · 历史间隔不代表未来复刻安排";
        SelectItemCommand = new RelayCommand(p => { if (p is CountdownItemViewModel item) SelectedItem = item; });
        CloseSelectionCommand = new RelayCommand(_ => SelectedItem = null);
        Ready = InitializeAsync(now);
    }
    private async Task InitializeAsync(DateTimeOffset now)
    {
        try
        {
            _items = await Task.Run(() => _catalog.BuildCountdown(now).Select(c => new CountdownItemViewModel(c)).ToArray());
            Apply();
        }
        catch (Exception error) { Shell.ReportError(error); }
        finally { IsLoading = false; }
    }
    private void Apply()
    {
        Sections = new[] { ("角色", "5"), ("角色", "4"), ("武器", "5"), ("武器", "4") }.Select(pair => new CountdownSection(pair.Item2 + " 星" + pair.Item1,
            _items.Where(i => i.Item.Kind == pair.Item1 && i.Item.Rank == pair.Item2 && (string.IsNullOrWhiteSpace(Search) || i.Item.Name != $"物品 {i.Item.Id}" && i.Item.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)))
                .ToArray())).ToArray();
        SelectedItem = null; Raise(nameof(Sections));
    }
}
