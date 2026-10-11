using GenshinGachaAnalyzer.Core.Services;
using GenshinGachaAnalyzer.Core.Models;
using Xunit;
namespace GenshinGachaAnalyzer.Tests;

public sealed class WishCatalogTests
{
    private static readonly DateTimeOffset Start = new(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(8));
    private static WishEvent Period(string type, string[]? purple = null, DateTimeOffset? from = null, DateTimeOffset? to = null) =>
        new("测试排期", "5.0", 1, type, from ?? Start, to ?? Start.AddDays(20), ["10000022"], purple ?? ["10000032"]);
    private static readonly WishItem[] Items = [new("10000022", "温迪", "角色", "5", ""), new("10000032", "班尼特", "角色", "4", "")];
    [Fact]
    public void EmbeddedSnapshotLoadsAndContainsBothTypesAndChronicle()
    {
        var catalog = new WishCatalogService(); Assert.True(catalog.Events.Count > 200);
        Assert.Contains(catalog.Events, e => e.Type == "500");
        Assert.Equal("温迪", catalog.Resolve("10000022").Name);
        Assert.Equal("武器", catalog.Resolve("11501").Kind);
        Assert.True(catalog.CoveredThrough.Year >= 2025);
    }
    [Fact]
    public void MetadataResolvesKnownItemRankWithoutChangingOriginalRecord()
    {
        var catalog = new WishCatalogService(Items, [Period("301")]);
        var original = TestData.Record(rank: null) with { ItemId = "10000022", Name = null, ItemType = null };
        Assert.Equal("温迪", catalog.Resolve(original).Name); Assert.Equal("5", catalog.Resolve(original).Rank);
        Assert.Null(original.Name); Assert.Null(original.ItemType); Assert.Null(original.RankType);
    }
    [Fact]
    public void HistoryMatchesRawPoolInsteadOfSharedPityType()
    {
        var catalog = new WishCatalogService(Items, [Period("301"), Period("400") with { Name = "第二角色池" }]);
        var groups = catalog.BuildHistory([TestData.Record(), TestData.Record("2", "400")]);
        Assert.Equal(2, groups.Count); Assert.Contains(groups, g => g.Name == "第二角色池");
    }
    [Fact]
    public void HistoryPreservesUnknownActivityRecordsAndExcludesStandardWish()
    {
        var catalog = new WishCatalogService(Items, [Period("301")]);
        var groups = catalog.BuildHistory([TestData.Record() with { Time = new(2026, 1, 1) }, TestData.Record("2", "200")]);
        Assert.Equal(1, groups.Sum(g => g.Records.Count));
        Assert.All(groups.Where(g => g.Records.Count > 0), g => Assert.Null(g.Event)); Assert.Contains(groups, g => g.Period.Contains("排期未覆盖"));
    }
    [Fact]
    public void EmptyHistoryStillContainsAllKnownBannersAndUpInformation()
    {
        var catalog = new WishCatalogService(Items, [Period("301"), Period("400"), Period("302"), Period("500")]);
        var history = catalog.BuildHistory([]);
        Assert.Equal(4, history.Count); Assert.All(history, g => { Assert.Empty(g.Records); Assert.NotNull(g.Event); Assert.NotEmpty(g.Event.UpGold); });
    }
    [Fact]
    public void DualCharacterPoolPurpleUpCountsOnlyOnce()
    {
        var catalog = new WishCatalogService(Items, [Period("301"), Period("400")]);
        var result = catalog.BuildCountdown(Start.AddDays(21));
        Assert.Single(result.Single(c => c.Item.Id == "10000032").Histories);
    }
    [Fact]
    public void CountdownSkipsFutureEventsAndCountsActiveRemainingDays()
    {
        var catalog = new WishCatalogService(Items, [Period("301"), Period("301", from: Start.AddDays(40), to: Start.AddDays(60))]);
        var result = catalog.BuildCountdown(Start.AddDays(5));
        Assert.All(result, c => { Assert.True(c.IsActive); Assert.Equal(15, c.Days); Assert.Single(c.Histories); });
    }
    [Fact]
    public void ExpiredSnapshotDoesNotClaimUnobservedDaysWithoutReruns()
    {
        var catalog = new WishCatalogService(Items, [Period("301"), Period("302", from: Start.AddDays(30), to: Start.AddDays(50)) with { UpGold = ["11501"], UpPurple = [] }]);
        var item = catalog.BuildCountdown(Start.AddDays(100)).Single(c => c.Item.Id == "10000022");
        Assert.Equal(30, item.Days); Assert.False(item.IsActive);
    }
    [Fact]
    public void ItemCountsIncludeAllPoolsWithoutAssumingPossessionOrConstellations()
    {
        var catalog = new WishCatalogService(Items, [Period("301")]);
        var a = TestData.Record() with { ItemId = "10000022", Name = "温迪" };
        var items = catalog.Summarize([a, a with { Id = "2", GachaType = "100" }], "角色");
        Assert.Equal(2, Assert.Single(items).Count); Assert.Equal(2, items[0].Records.Count);
    }
}

