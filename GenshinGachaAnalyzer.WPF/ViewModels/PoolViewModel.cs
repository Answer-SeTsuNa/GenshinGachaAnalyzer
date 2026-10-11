using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.Core.Services;
using GenshinGachaAnalyzer.WPF.Infrastructure;
using System.Windows.Input;

namespace GenshinGachaAnalyzer.WPF.ViewModels;

public sealed class PoolViewModel : ObservableObject
{
    public PoolAnalysis Analysis { get; }
    private bool _expanded = true, _gridLayout;
    private string _metric = "平均";
    public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    public bool IsGridLayout { get => _gridLayout; set => Set(ref _gridLayout, value); }
    public IReadOnlyList<SectionTab> MetricTabs => new[] { "平均", "数量", "区间" }.Select(t => new SectionTab(t, t == _metric)).ToArray();
    public bool ShowRightMetric => _metric != "平均" || Analysis.UpAverage.HasValue;
    public string LeftMetric { get; private set; } = "";
    public string RightMetric { get; private set; } = "";
    public string LeftLabel { get; private set; } = "";
    public string RightLabel { get; private set; } = "";
    private readonly WishCatalogService _catalog;
    private bool _upCycle;
    public bool SupportsUpCycle => Type == "301";
    public bool IsUpCycle { get => _upCycle; set { if (Set(ref _upCycle, value)) RebuildGoldItems(); } }
    public IReadOnlyList<GoldDisplayViewModel> GoldItems { get; private set; } = [];
    public GoldDisplayViewModel? AnchorTarget { get; private set; }
    public ICommand AnchorCommand { get; }
    private void RebuildGoldItems()
    {
        // 只有已覆盖排期且明确非 UP 才合并；无法判定的记录继续保留。
        GoldItems = Analysis.Golds.Where(g => !IsUpCycle || _catalog.IsCurrentUp(g.Record) != false)
            .Select(g => new GoldDisplayViewModel(g, _catalog, HardPity, IsUpCycle && _catalog.IsCurrentUp(g.Record) == true)).ToArray();
        GridGoldItems.Clear(); LoadMoreGrid(); Raise(nameof(GoldItems));
    }
    public System.Collections.ObjectModel.ObservableCollection<GoldDisplayViewModel> GridGoldItems { get; } = [];
    public void LoadMoreGrid()
    {
        // 网格采用分批追加，避免一次生成数百张物品卡；列表保持原生虚拟化。
        foreach (var item in GoldItems.Skip(GridGoldItems.Count).Take(40)) GridGoldItems.Add(item);
    }
    public string DateRange { get; }
    public string FourStarText { get; }
    public double FourStarPity { get; }
    public int FourStarCount { get; }
    public string GoldCaption => $"五星记录 · {Analysis.Golds.Count:N0} 次";
    public ICommand MetricCommand { get; }
    public PoolViewModel(PoolAnalysis analysis, IReadOnlyList<GachaRecord>? allRecords = null, WishCatalogService? catalog = null)
    {
        Analysis = analysis; catalog ??= new(); _catalog = catalog;
        var records = (allRecords ?? []).Where(r => r.PoolType == Type).Select(catalog.ForAnalysis).ToArray();
        RebuildGoldItems();
        AnchorCommand = new RelayCommand(p =>
        {
            var ordered = Analysis.Golds.Where(g => g.Pulls.HasValue).OrderBy(g => g.Pulls).ToArray();
            var entry = p?.ToString() == "min" ? ordered.FirstOrDefault() : ordered.LastOrDefault();
            if (entry is null) return;
            if (IsUpCycle && _catalog.IsCurrentUp(entry.Record) == false) IsUpCycle = false;
            AnchorTarget = GoldItems.FirstOrDefault(g => g.Entry.Record.Id == entry.Record.Id); Raise(nameof(AnchorTarget));
        });
        DateRange = records.Length == 0 ? "暂无记录" : $"{records.Min(r => r.Time):yyyy-MM-dd} — {records.Max(r => r.Time):yyyy-MM-dd}";
        FourStarCount = records.Count(r => r.RankType == "4");
        int? fourStar = GachaCounters.FourStarProgress(records);
        FourStarPity = fourStar ?? 0;
        FourStarText = fourStar is { } n ? $"{n} / 10" : "暂无数据";
        MetricCommand = new RelayCommand(p => { _metric = p is SectionTab tab ? tab.Name : "平均"; UpdateMetrics(); }); UpdateMetrics();
    }
    private void UpdateMetrics()
    {
        var intervals = Analysis.Golds.Where(g => g.Pulls.HasValue).Select(g => g.Pulls!.Value).ToArray();
        (LeftMetric, RightMetric, LeftLabel, RightLabel) = _metric switch
        {
            "数量" => (GoldText, FourStarCount.ToString("N0"), "五星数量", "已知四星数量"),
            "区间" => (intervals.Length > 0 ? intervals.Min().ToString() : "暂无数据", intervals.Length > 0 ? intervals.Max().ToString() : "暂无数据", "最少出金抽数", "最多出金抽数"),
            _ => (AverageText, UpAverageText, "平均出金抽数", "UP 平均抽数")
        };
        Raise(nameof(ShowRightMetric)); Raise(nameof(MetricTabs)); Raise(nameof(LeftMetric)); Raise(nameof(RightMetric)); Raise(nameof(LeftLabel)); Raise(nameof(RightLabel));
    }
    public string Name => Analysis.Pool.Name;
    public string Type => Analysis.Pool.Type;
    public string Glyph => Analysis.Pool.Glyph;
    public string Accent => Analysis.Pool.Accent;
    public int HardPity => Analysis.Pool.HardPity;
    public int PityValue => Analysis.Pity ?? 0;
    public string PityText => Analysis.Pity is { } n ? $"{(Analysis.PityIsLowerBound ? "至少 " : "")}{n} / {HardPity} 抽" : "暂无可推导数据";
    public string Guarantee => Analysis.Guarantee;
    public string TotalText => Analysis.Total.ToString("N0");
    public string GoldText => Analysis.FiveStarCount is > 0 and var n ? n.ToString("N0") : "暂无数据";
    public string AverageText => Analysis.Average is { } n ? n.ToString("F1") : "暂无数据";
    public string LuckText => Analysis.LuckPercentile is { } n ? $"{n:F1}%" : "暂无数据";
    public bool HasLuck => Analysis.LuckPercentile.HasValue;
    public IReadOnlyList<FiveStarEntry> Recent => Analysis.Golds.Take(3).ToArray();
    public bool HasGold => Analysis.Golds.Count > 0;
    public string Note => Analysis.Note;
    public string IntegrityNote => Analysis.PityIsLowerBound && Analysis.Total > 0 ? "未见前次五星，当前抽数仅为下限。" :
        "按已保存记录计数；无法验证历史是否缺失。";
    public string UpAverageText => Analysis.UpAverage is { } n ? n.ToString("F1") : "";
    public string MostUnluckyText => Analysis.Golds.Any(g => g.Pulls.HasValue) ? Analysis.Golds.Max(g => g.Pulls)?.ToString() + " 抽" : "暂无数据";
    public string LuckiestText => Analysis.Golds.Any(g => g.Pulls.HasValue) ? Analysis.Golds.Where(g => g.Pulls.HasValue).Min(g => g.Pulls)?.ToString() + " 抽" : "暂无数据";
    public string GoldRateText => Analysis.Total > 0 && Analysis.FiveStarCount is { } n ? (100d * n / Analysis.Total).ToString("F2") + "%" : "暂无数据";
    public double GoldRate => Analysis.Total > 0 && Analysis.FiveStarCount is { } n ? 100d * n / Analysis.Total : 0;
}
