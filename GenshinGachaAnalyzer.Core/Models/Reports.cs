namespace GenshinGachaAnalyzer.Core.Models;

public sealed record ClearAccountReport(string Uid, int Deleted, string BackupPath);

public sealed record WriteReport(int Added, int Duplicates, IReadOnlyList<string> ConflictIds)
{
    public int Conflicts => ConflictIds.Count;
    public IReadOnlyList<RecordConflict> Details { get; init; } = [];
    public string? LogPath { get; init; }
    public string? LogWarning { get; init; }
}
public sealed record FieldDifference(string Field, string? Existing, string? Incoming);
public sealed record RecordConflict(string Id, string Reason, GachaRecord Existing, GachaRecord Incoming, IReadOnlyList<FieldDifference> Differences)
{
    public string Fields => string.Join("、", Differences.Select(d => d.Field));
    public string DetailText => string.Join(Environment.NewLine, Differences.Select(d => $"{d.Field}\n  已有：{d.Existing ?? "（未提供）"}\n  本次：{d.Incoming ?? "（未提供）"}"));
}
public sealed record ImportProgress(int Processed, int Total, string Stage);
public sealed record PoolCount(string Uid, string Pool, int Count);
public sealed record PreparedImport(string Version, IReadOnlyList<AccountSeed> Accounts,
    IReadOnlyList<GachaRecord> Records, IReadOnlyList<PoolCount> Counts, IReadOnlyList<string> Warnings);
public sealed record FetchProgress(string Pool, int Page, int Fetched, int Saved);
public sealed record FetchResult(int Fetched, int Added, int Duplicates, IReadOnlyList<string> ConflictIds, string? Uid)
{
    public IReadOnlyList<RecordConflict> Details { get; init; } = [];
    public string? LogPath { get; init; }
    public string? LogWarning { get; init; }
}
public sealed record FiveStarEntry(GachaRecord Record, int? Pulls, string UpStatus, double? Percentile)
{
    // 档案从第一条累计，与胡桃统计口径一致；未知星级或异常区间仍不推导。
    public int? ObservedPulls { get; init; }
    public int? UpCyclePulls { get; init; }
    public int? ObservedUpCyclePulls { get; init; }
    public string Name => string.IsNullOrWhiteSpace(Record.Name) ? $"物品 {Record.ItemId}" : Record.Name;
    public string PullText => Pulls is { } n ? $"{n} 抽" : "区间不完整";
    public string TimeText => Record.Time.ToString("yyyy-MM-dd HH:mm:ss");
    public string SourceText => Record.Source;
    public string PercentileText => Percentile is { } n ? $"{n:F1}% · 基于您的历史记录" : "暂无数据";
}
public sealed record HistogramBin(int From, int To, IReadOnlyList<FiveStarEntry> Entries)
{
    public string Label => $"{From}–{To}";
    public int Count => Entries.Count;
}
public sealed record PoolAnalysis(PoolDefinition Pool, int Total, int? FiveStarCount, int? Pity,
    bool PityIsLowerBound, double? Average, string Guarantee, string Note,
    IReadOnlyList<FiveStarEntry> Golds, IReadOnlyList<HistogramBin> Histogram, double? LuckPercentile)
{
    public double? UpAverage { get; init; }
    public IReadOnlyList<int> UpCycles { get; init; } = [];
}
