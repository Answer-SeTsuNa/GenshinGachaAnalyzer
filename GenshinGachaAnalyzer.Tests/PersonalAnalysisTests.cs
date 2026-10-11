using GenshinGachaAnalyzer.Core.Services;
using Xunit;
namespace GenshinGachaAnalyzer.Tests;
public sealed class PersonalAnalysisTests
{
    private static WishCatalogService Catalog() => new(
        [new("10000022", "温迪", "角色", "5", ""), new("10000003", "琴", "角色", "5", "")],
        [new("测试排期", "5.0", 1, "301", new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)), new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.FromHours(8)), ["10000022"], [])]);
    [Fact]
    public void FirstGoldShows78AndParticipatesInStatisticsLikeHutao()
    {
        var result = new AnalysisService(new CatalogBannerProvider(Catalog()), Catalog()).Analyze(TestData.Sequence("301", 78), Core.Models.PoolCatalog.MainPools[0]);
        Assert.Equal(78, result.Golds[0].Pulls); Assert.Equal(78, result.Golds[0].ObservedPulls);
        Assert.Equal("78 抽", result.Golds[0].PullText);
        Assert.Equal(78d, result.Average); Assert.Equal(1, result.Histogram.Sum(b => b.Count));
    }
    [Fact]
    public void UpCyclesBelongToNextUpAndIncludeIntermediateLoss()
    {
        var records = TestData.Sequence("301", 3, 20, 30, 10);
        int gold = 0;
        records = records.Select(r => r.RankType == "5" ? r with { ItemId = ++gold == 2 ? "10000003" : "10000022" } : r).ToList();
        var catalog = Catalog(); var result = new AnalysisService(new CatalogBannerProvider(catalog), catalog).Analyze(records, Core.Models.PoolCatalog.MainPools[0]);
        Assert.Equal(new[] { 3, 50, 10 }, result.UpCycles);
        Assert.Equal(50, result.Golds.Single(g => g.Record.Id == "53").UpCyclePulls);
        Assert.Null(result.Golds.Single(g => g.Record.Id == "23").UpCyclePulls);
        Assert.Equal(3, result.Golds[^1].ObservedUpCyclePulls);
    }
    [Fact]
    public void UnknownUpDoesNotCreateObservedInitialCycle()
    {
        var catalog = Catalog(); var records = TestData.Sequence("301", 3, 10);
        records[2] = records[2] with { ItemId = "99999999" };
        records[^1] = records[^1] with { ItemId = "10000022" };
        var report = new AnalysisService(new CatalogBannerProvider(catalog), catalog).Analyze(records, Core.Models.PoolCatalog.MainPools[0]);
        Assert.All(report.Golds, g => { Assert.Null(g.UpCyclePulls); Assert.Null(g.ObservedUpCyclePulls); });
    }
    [Fact]
    public void ImpossibleInitialGoldDoesNotBecomeObservedUpCycle()
    {
        var catalog = Catalog(); var records = TestData.Sequence("301", 100);
        records[^1] = records[^1] with { ItemId = "10000022" };
        var report = new AnalysisService(new CatalogBannerProvider(catalog), catalog).Analyze(records, Core.Models.PoolCatalog.MainPools[0]);
        Assert.Null(report.Golds[0].ObservedPulls); Assert.Null(report.Golds[0].ObservedUpCyclePulls);
    }
    [Fact]
    public void HistoricalGapExcludesStandardAndDoesNotIncludeCurrentTail()
    {
        var records = new[] { TestData.Record() with { Time = new(2025, 1, 1) }, TestData.Record("2", "200") with { Time = new(2025, 1, 5) }, TestData.Record("3") with { Time = new(2025, 1, 11) } };
        var report = new PersonalAnalysisService(Catalog()).Analyze(records, new(2025, 1, 15));
        Assert.Equal(9, report.LongestGap); Assert.Equal(4, report.CurrentGap); Assert.Equal(2, report.ActiveDays);
        Assert.Equal(3, report.Total); Assert.Equal(2, report.Months.Single().Count);
    }
    [Fact]
    public void EmptyArchiveHasNoInventedAveragesOrGaps()
    {
        var report = new PersonalAnalysisService(Catalog()).Analyze([], new(2025, 1, 15));
        Assert.Null(report.CharacterUpAverage); Assert.Null(report.WeaponUpAverage); Assert.Null(report.LongestGap); Assert.Null(report.CurrentGap); Assert.Null(report.ConsecutiveLosses);
    }
    [Fact]
    public void GuaranteedUpDoesNotBreakObservedSmallPityLossStreak()
    {
        var ids = new[] { "10000003", "10000022", "10000003", "10000022", "10000022", "10000003" };
        var records = ids.Select((id, n) => TestData.Record((n + 1).ToString(), rank: "5") with { ItemId = id }).ToArray();
        var report = new PersonalAnalysisService(Catalog()).Analyze(records, new(2025, 1, 15));
        Assert.Equal(2, report.ConsecutiveLosses);
    }
}
