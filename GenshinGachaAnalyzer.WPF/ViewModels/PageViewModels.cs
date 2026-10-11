using System.Collections.ObjectModel;
using System.Windows.Input;
using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.WPF.Infrastructure;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace GenshinGachaAnalyzer.WPF.ViewModels;

public sealed record WelcomeViewModel(MainViewModel Shell);
public sealed record DatabaseUnavailableViewModel(MainViewModel Shell);
public sealed class DashboardViewModel : ObservableObject
{
    public MainViewModel Shell { get; }
    public IReadOnlyList<PoolViewModel> Pools { get; }
    private bool _ratioOpen;
    public bool IsRatioOpen { get => _ratioOpen; set => Set(ref _ratioOpen, value); }
    public ICommand RatioCommand { get; }
    public ICommand CloseRatioCommand { get; }
    public IReadOnlyList<GoldShareViewModel> Shares { get; }
    public string GoldSummary => $"已识别五星 {Shares.Sum(s => s.Count):N0} 次 · {Shares.Count} 种物品";
    public bool HasShares => Shares.Count > 0;
    public DashboardViewModel(MainViewModel shell, IReadOnlyList<PoolViewModel> pools)
    {
        Shell = shell; Pools = pools;
        var summaries = shell.Catalog.Summarize(pools.SelectMany(p => p.Analysis.Golds).Select(g => g.Record));
        int total = summaries.Sum(s => s.Count); Shares = summaries.Select(s => new GoldShareViewModel(s, total)).ToArray();
        RatioCommand = new RelayCommand(_ => IsRatioOpen = !IsRatioOpen);
        CloseRatioCommand = new RelayCommand(_ => IsRatioOpen = false);
    }
}
public sealed class GoldShareViewModel(GenshinGachaAnalyzer.Core.Services.ItemSummary summary, int total) : ItemDisplayViewModel(summary.Item)
{
    public int Count => summary.Count;
    public double Percent => total > 0 ? 100d * Count / total : 0;
    public string ShareText => $"{Count:N0} 次 · {Percent:F1}%";
}
public sealed record SettingsViewModel(MainViewModel Shell);
public sealed record PoolTabViewModel(PoolDefinition Definition, bool IsSelected)
{
    public string Name => Definition.Name;
}

public sealed class DetailViewModel : ObservableObject
{
    public MainViewModel Shell { get; }
    public PoolViewModel Pool { get; }
    public IReadOnlyList<PoolTabViewModel> Tabs => PoolCatalog.MainPools.Select(p => new PoolTabViewModel(p, p.Type == Pool.Type)).ToArray();
    public ObservableCollection<FiveStarEntry> Timeline { get; } = [];
    private IReadOnlyList<FiveStarEntry> _filtered;
    private string _filterText = "全部五星记录";
    public string FilterText { get => _filterText; private set => Set(ref _filterText, value); }
    public bool HasMore => Timeline.Count < _filtered.Count;
    public bool HasTimeline => Timeline.Count > 0;
    public ISeries[] Series { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand ResetFilterCommand { get; }
    public RecordTableViewModel RecordsTable { get; }
    public ICommand FilterCommand { get; }

    public DetailViewModel(MainViewModel shell, PoolViewModel pool)
    {
        Shell = shell; Pool = pool; _filtered = pool.Analysis.Golds;
        LoadMoreCommand = new RelayCommand(_ => LoadMore(), () => HasMore);
        ResetFilterCommand = new RelayCommand(_ => Reset());
        RecordsTable = new(shell.CurrentRecords.Where(r => r.PoolType == pool.Type), shell.Catalog);
        FilterCommand = new RelayCommand(p => { if (p is HistogramBin bin) SelectBin(bin.From / 10); });
        var series = new ColumnSeries<int>
        {
            Name = "出金抽数", Values = pool.Analysis.Histogram.Select(b => b.Count).ToArray(),
            Fill = new SolidColorPaint(SKColor.Parse(pool.Accent)), Stroke = null,
            MaxBarWidth = 42, Rx = 5, Ry = 5,
            XToolTipLabelFormatter = point => pool.Analysis.Histogram[(int)point.Coordinate.SecondaryValue].Label + " 抽",
            YToolTipLabelFormatter = point => $"{point.Coordinate.PrimaryValue:0} 次"
        };
        // 图表交互只负责选择区间，记录筛选仍在 ViewModel 完成。
        series.DataPointerDown += (_, points) =>
        {
            var point = points.FirstOrDefault();
            if (point is not null) SelectBin((int)point.Coordinate.SecondaryValue);
        };
        Series = [series];
        XAxes = [new Axis { Labels = pool.Analysis.Histogram.Select(b => b.Label).ToArray(), MinStep = 1,
            LabelsPaint = new SolidColorPaint(SKColor.Parse("#727982")), SeparatorsPaint = null, TextSize = 14 }];
        double largest = pool.Analysis.Histogram.Max(b => b.Count);
        double step = NiceStep(Math.Max(1, largest / 5));
        YAxes = [new Axis { MinLimit = 0, MaxLimit = Math.Max(2 * step, (Math.Ceiling(largest / step) + 1) * step), MinStep = step, ForceStepToMin = true, Labeler = n => n.ToString("0"),
            LabelsPaint = new SolidColorPaint(SKColor.Parse("#727982")), SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E3E7EB")), TextSize = 14 }];
        ApplyTheme(); LoadMore();
    }
    private static double NiceStep(double value)
    {
        double basis = Math.Pow(10, Math.Floor(Math.Log10(value))); double n = value / basis;
        return basis * (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10);
    }
    public void ApplyTheme()
    {
        var muted = Services.ThemeService.IsDark ? "#B4BDCC" : "#727982";
        XAxes[0].LabelsPaint = new SolidColorPaint(SKColor.Parse(muted));
        YAxes[0].LabelsPaint = new SolidColorPaint(SKColor.Parse(muted));
        YAxes[0].SeparatorsPaint = new SolidColorPaint(SKColor.Parse(Services.ThemeService.IsDark ? "#454A59" : "#E3E7EB"));
    }
    public void LoadMore()
    {
        foreach (var entry in _filtered.Skip(Timeline.Count).Take(40)) Timeline.Add(entry);
        Raise(nameof(HasMore)); Raise(nameof(HasTimeline)); CommandManager.InvalidateRequerySuggested();
    }
    public void SelectBin(int index)
    {
        if (index < 0 || index >= Pool.Analysis.Histogram.Count) return;
        var bin = Pool.Analysis.Histogram[index];
        _filtered = bin.Entries; FilterText = $"{bin.Label} 抽 · {bin.Count} 次出金";
        Timeline.Clear(); LoadMore();
    }
    private void Reset() { _filtered = Pool.Analysis.Golds; FilterText = "全部五星记录"; Timeline.Clear(); LoadMore(); }
}


