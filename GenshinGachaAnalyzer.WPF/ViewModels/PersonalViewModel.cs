using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.Core.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace GenshinGachaAnalyzer.WPF.ViewModels;
public sealed record PersonalMetric(string Icon, string Label, string Value, string Note);
public sealed class PersonalViewModel : Infrastructure.ObservableObject
{
    public MainViewModel Shell { get; }
    public IReadOnlyList<PersonalMetric> Metrics { get; }
    public IReadOnlyList<CollectionItemViewModel> Favorites { get; }
    public IReadOnlyList<PersonalMetric> PoolCounts { get; }
    public ISeries[] Series { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }
    public string RangeText { get; }
    private readonly IReadOnlyList<GachaRecord> _records;
    private bool _excludeStandard = true;
    public bool ExcludeStandard { get => _excludeStandard; set { if (Set(ref _excludeStandard, value)) RefreshMonths(); } }
    public bool HasMonths { get; private set; }
    public string MonthsNote => ExcludeStandard ? "仅统计角色、武器与集录祈愿。" : "包含常驻祈愿；按档案中的月份汇总。";
    public System.Windows.Input.ICommand ShowFavoriteCommand { get; }
    public void ApplyTheme()
    {
        var style = Shell.SelectedStyle;
        ((ColumnSeries<int>)Series[0]).Fill = new SolidColorPaint(SKColor.Parse(Services.ThemeService.IsDark ? style.NightAccent : style.Accent));
    }
    public PersonalViewModel(MainViewModel shell, IReadOnlyList<GachaRecord> records)
    {
        Shell = shell; _records = records; var report = new PersonalAnalysisService(shell.Catalog).Analyze(records, DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).DateTime);
        ShowFavoriteCommand = new Infrastructure.RelayCommand(p => { if (p is CollectionItemViewModel item) _ = ShowFavoriteAsync(item); });
        string Number(int? n, string suffix = "") => n is { } value ? value.ToString("N0") + suffix : "暂无数据";
        string Average(double? n) => n is { } value ? value.ToString("F1") + " 抽" : "暂无数据";
        Metrics = [new("✦", "档案总抽数", Number(report.Total), "包含全部本地卡池记录"), new("★", "五星相遇", Number(report.Gold), "按物品元数据识别"),
            new("♙", "UP 角色平均抽数", Average(report.CharacterUpAverage), "从档案起点累计，包含中间的非 UP"),
            new("⚔", "UP 武器平均抽数", Average(report.WeaponUpAverage), "从档案起点累计；不代表定轨"),
            new("↯", "已知小保底最多连歪", Number(report.ConsecutiveLosses, " 次"), "角色池；大保底 UP 不打断连歪计数"),
            new("◷", "记录内最长空档", Number(report.LongestGap, " 天"), "两次活动祈愿日之间的空白天数，不含常驻"),
            new("☾", "距最后活动祈愿", Number(report.CurrentGap, " 天"), "以本地最后一次活动祈愿为准"),
            new("▦", "留下足迹的日子", Number(report.ActiveDays, " 天"), "活动卡池的不同抽卡日期")];
        RangeText = report.First is { } first ? $"{first:yyyy-MM-dd} — {report.Last:yyyy-MM-dd} · 仅根据当前 UID 的已保存记录" : "开始记录您的星轨";
        Favorites = shell.Catalog.Summarize(records.Where(r => shell.Catalog.Resolve(r).Rank == "5")).Take(12).Select(s => new CollectionItemViewModel(s)).ToArray();
        PoolCounts = PoolCatalog.MainPools.Select(p => new PersonalMetric(p.Glyph, p.Name, records.Count(r => r.PoolType == p.Type).ToString("N0") + " 抽", "已保存记录")).ToArray();
        HasMonths = report.Months.Count > 0;
        Series = [new ColumnSeries<int> { Values = report.Months.Select(m => m.Count).ToArray(), Name = "活动祈愿抽数", Fill = new SolidColorPaint(SKColor.Parse("#598DBD")), MaxBarWidth = 32 }];
        XAxes = [new Axis { Labels = report.Months.Select(m => m.Month).ToArray(), LabelsRotation = 30, TextSize = 12, MinStep = 1, LabelsPaint = new SolidColorPaint(SKColor.Parse("#8992A6")) }];
        int max = report.Months.Count == 0 ? 0 : report.Months.Max(m => m.Count);
        YAxes = [new Axis { MinLimit = 0, MinStep = Math.Max(1, Math.Ceiling(max / 5d)), ForceStepToMin = true, TextSize = 12, LabelsPaint = new SolidColorPaint(SKColor.Parse("#8992A6")) }];
        RefreshMonths(); ApplyTheme();
    }
    private void RefreshMonths()
    {
        var months = _records.Where(r => !ExcludeStandard || r.PoolType is "301" or "302" or "500")
            .GroupBy(r => r.Time.ToString("yyyy-MM")).OrderBy(g => g.Key).ToArray();
        HasMonths = months.Length > 0;
        ((ColumnSeries<int>)Series[0]).Values = months.Select(g => g.Count()).ToArray();
        Series[0].Name = ExcludeStandard ? "活动祈愿抽数" : "祈愿抽数";
        XAxes[0].Labels = months.Select(g => g.Key).ToArray();
        int max = months.Length == 0 ? 0 : months.Max(g => g.Count());
        YAxes[0].MinStep = Math.Max(1, Math.Ceiling(max / 5d));
        Raise(nameof(HasMonths)); Raise(nameof(MonthsNote));
    }
    private async Task ShowFavoriteAsync(CollectionItemViewModel item)
    {
        try
        {
            Shell.ShowSection(item.Kind == "角色" ? "角色" : "武器"); await Shell.NavigationReady;
            if (Shell.CurrentPage is CollectionViewModel collection)
            {
                collection.Search = item.Name;
                var selected = collection.Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.Id == item.Id);
                if (selected is not null) collection.SelectItemCommand.Execute(selected);
            }
        }
        catch (Exception error) { Shell.ReportError(error); }
    }
}
public sealed record LoadingViewModel(string Name)
{
    public string Message => Name == "本地档案" ? "正在加载本地档案…" : "✦ 正在整理星轨…";
}
