using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.Core.Services;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;

public sealed class AnalysisTests
{
    private static readonly PoolDefinition Character = PoolCatalog.MainPools[0];
    private static readonly AnalysisService Analysis = new(new EmbeddedBannerProvider([]));
    [Fact]
    public void EmptyPoolHasNoInventedStatistics()
    {
        var result = Analysis.Analyze([], Character);
        Assert.Null(result.Pity); Assert.Null(result.Average); Assert.Null(result.LuckPercentile); Assert.Empty(result.Golds);
    }
    [Fact]
    public void FirstGoldIsCountedFromArchiveBeginningLikeHutao()
    {
        var result = Analysis.Analyze(TestData.Sequence("301", 9, 30, 70), Character);
        Assert.Equal(9, result.Golds[^1].Pulls); Assert.Equal(109d / 3, result.Average); Assert.Equal(3, result.Histogram.Sum(b => b.Count));
    }
    [Fact]
    public void BothCharacterBannersSharePity()
    {
        var records = TestData.Sequence("301", 2); records.Add(TestData.Record("3", "400") with { Time = new DateTime(2025, 1, 2) });
        var result = Analysis.Analyze(records, Character); Assert.Equal(3, result.Total); Assert.Equal(1, result.Pity);
    }
    [Fact]
    public void NoPreviousGoldCountsSavedArchive()
    {
        var result = Analysis.Analyze([TestData.Record(), TestData.Record("2")], Character);
        Assert.False(result.PityIsLowerBound); Assert.Equal(2, result.Pity); Assert.Null(result.Average);
    }
    [Fact]
    public void UnknownRankSuppressesPityAndGoldCount()
    {
        var result = Analysis.Analyze([TestData.Record(rank: "5"), TestData.Record("2", rank: null)], Character);
        Assert.Null(result.Pity); Assert.Null(result.FiveStarCount); Assert.Equal(1d, result.Average);
    }
    [Fact]
    public void UnknownRankInvalidatesOnlyAffectedIntervals()
    {
        var records = TestData.Sequence("301", 1, 5, 10); records[2] = records[2] with { RankType = null };
        var result = Analysis.Analyze(records, Character); Assert.Equal(5.5d, result.Average); Assert.Null(result.Golds[1].Pulls);
    }
    [Fact]
    public void ImpossibleIntervalDoesNotEnterAverageOrHistogram()
    {
        var result = Analysis.Analyze(TestData.Sequence("302", 1, 81), PoolCatalog.MainPools[1]);
        Assert.Equal(1d, result.Average); Assert.Null(result.Golds[0].Pulls); Assert.Equal(1, result.Histogram.Sum(b => b.Count));
    }
    [Fact]
    public void HistogramUsesInclusiveTenPullBoundaries()
    {
        var result = Analysis.Analyze(TestData.Sequence("301", 1, 10, 11, 90), Character);
        Assert.Equal(2, result.Histogram[0].Count); Assert.Equal(1, result.Histogram[1].Count); Assert.Equal(1, result.Histogram[8].Count);
    }
    [Fact]
    public void LuckIncludesArchiveFirstIntervalsAndUsesAverageRanksForTies()
    {
        var records = TestData.Sequence("301", 1, 10);
        records.AddRange(TestData.Sequence("302", 1, 10, 10, 10, 10, 10, 10, 10, 10, 10).Select(r => r with { Id = (int.Parse(r.Id) + 1000).ToString() }));
        var result = Analysis.Analyze(records, Character); Assert.Equal(100d * 7 / 12, result.LuckPercentile!.Value, 10);
    }
    [Fact]
    public void FewerThanTenGoldIntervalsHasNoLuckIndex()
    {
        var result = Analysis.Analyze(TestData.Sequence("301", Enumerable.Repeat(1, 9).ToArray()), Character); Assert.Null(result.LuckPercentile);
    }
    [Fact]
    public void IdOrderIsNumericForSimultaneousPulls()
    {
        var records = new[] { TestData.Record("10", rank: "5"), TestData.Record("9", rank: "3"), TestData.Record("8", rank: "5") };
        var result = Analysis.Analyze(records, Character); Assert.Equal(2, result.Golds[0].Pulls);
    }
    [Theory]
    [InlineData("302")]
    [InlineData("200")]
    public void WeaponAndStandardNeverShowGuarantee(string type)
    {
        var result = Analysis.Analyze(TestData.Sequence(type, 1, 10), PoolCatalog.MainPools.Single(p => p.Type == type)); Assert.Equal("", result.Guarantee);
    }
    [Fact]
    public void ChronicleDoesNotInventChosenTarget()
    {
        var result = Analysis.Analyze(TestData.Sequence("500", 1, 10), PoolCatalog.MainPools[2]);
        Assert.Contains("未知", result.Guarantee); Assert.All(result.Golds, g => Assert.Equal("当期选择未知", g.UpStatus));
    }
    [Fact]
    public void UncoveredLatestGoldDoesNotReuseOlderGuarantee()
    {
        var first = TestData.Record(rank: "5") with { ItemId = "10001", Time = new DateTime(2025, 1, 1) };
        var later = first with { Id = "2", Time = new DateTime(2025, 2, 1) };
        var provider = new EmbeddedBannerProvider([new("301", new(2025, 1, 1), new(2025, 1, 20), ["10002"], ["10001", "10002"], "https://ys.mihoyo.com/")]);
        var service = new AnalysisService(provider);
        Assert.Equal("大保底", service.Analyze([first], Character).Guarantee);
        Assert.Contains("不足", service.Analyze([first, later], Character).Guarantee);
    }
}
