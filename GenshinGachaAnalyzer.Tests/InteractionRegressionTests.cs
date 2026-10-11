using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.Core.Services;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;

public sealed class InteractionRegressionTests
{
    [Fact]
    public void HutaoUpHistoryUsesFiveMetadataPeriodsWithoutAnyAccountRecords()
    {
        var catalog = new WishCatalogService();
        var hu = catalog.BuildCountdown(catalog.CoveredThrough.AddDays(10)).Single(c => c.Item.Name == "胡桃");
        Assert.Equal(5, hu.Histories.Count);
        Assert.Equal(new[] { "1.3", "2.2", "3.4", "4.1", "5.1" }, hu.Histories.Reverse().Select(h => h.Version));
        Assert.All(catalog.Events, e => { Assert.False(string.IsNullOrEmpty(e.BannerFile)); Assert.StartsWith("https://", e.BannerUrl); });
    }

    [Fact]
    public void StandardCharacterCanBeFeaturedAndUnknownItemMustNotBeAssumedOffBanner()
    {
        var catalog = new WishCatalogService();
        var keqing = catalog.Events.Single(e => e.Version == "1.3" && e.UpGold.Contains("10000042"));
        var record = TestData.Record(rank: "5") with { ItemId = "10000042", GachaType = keqing.Type, Time = keqing.From.DateTime.AddHours(1) };
        Assert.True(catalog.IsCurrentUp(record)); Assert.Contains("当期 UP", catalog.DescribeStatus(record));
        Assert.False(catalog.IsCurrentUp(record with { ItemId = "10000016" }));
        Assert.Null(catalog.IsCurrentUp(record with { ItemId = "99999999" }));
        Assert.Null(catalog.IsCurrentUp(record with { Time = new(2035, 1, 1) }));
        Assert.Null(catalog.IsCurrentUp(record with { GachaType = "500" }));
    }

    [Fact]
    public void WeaponMetadataIdentifiesUpAndStandardWithoutInventingEpitomizedPath()
    {
        var catalog = new WishCatalogService();
        var period = catalog.Events.First(e => e.Type == "302" && !e.UpGold.Contains("11501"));
        var record = TestData.Record(type: "302", rank: "5") with { ItemId = period.UpGold[0], Time = period.From.DateTime.AddHours(1) };
        Assert.True(catalog.IsCurrentUp(record));
        Assert.False(catalog.IsCurrentUp(record with { ItemId = "11501" }));
        var result = new AnalysisService(new CatalogBannerProvider(catalog), catalog).Analyze([record], PoolCatalog.MainPools[1]);
        Assert.Empty(result.Guarantee); Assert.Contains("当期 UP", Assert.Single(result.Golds).UpStatus);
    }

    private sealed class KnownUpProvider : IBannerProvider
    {
        public bool? IsUp(GachaRecord record) => record.ItemId switch { "up" => true, "off" => false, _ => null };
    }
    private static List<GachaRecord> UpSequence(int[] pulls, string[] kinds)
    {
        var records = TestData.Sequence("301", pulls); int gold = 0;
        return records.Select(r => r.RankType == "5" ? r with { ItemId = kinds[gold++] } : r).ToList();
    }

    [Fact]
    public void UpAverageIncludesOffBannerPullsAndInitialArchiveCycle()
    {
        var service = new AnalysisService(new KnownUpProvider());
        var result = service.Analyze(UpSequence([5, 20, 30, 10], ["up", "off", "up", "up"]), PoolCatalog.MainPools[0]);
        Assert.Equal(new[] { 5, 50, 10 }, result.UpCycles); Assert.Equal(65d / 3, result.UpAverage);
    }

    [Theory]
    [InlineData(20, "unknown")]
    [InlineData(91, "off")]
    public void UpAverageSkipsUnknownOrImpossibleGoldIntervals(int middle, string kind)
    {
        var result = new AnalysisService(new KnownUpProvider()).Analyze(UpSequence([1, middle, 20, 10], ["up", kind, "up", "up"]), PoolCatalog.MainPools[0]);
        Assert.Equal(new[] { 1, 10 }, result.UpCycles); Assert.Equal(5.5d, result.UpAverage);
    }

    [Fact]
    public void LocalConflictLogShowsExactDifferenceAndKeepsOriginalRecord()
    {
        using var store = new TestDatabase(); var original = TestData.Record();
        store.Database.Insert([original]);
        var report = store.Database.Insert([original with { Uid = "200000001", ItemId = "10000046", Time = original.Time.AddHours(8) }]);
        var conflict = Assert.Single(report.Details); Assert.Contains("不同 UID", conflict.Reason);
        Assert.Contains(conflict.Differences, d => d.Field == "UID" && d.Existing == original.Uid && d.Incoming == "200000001");
        Assert.Contains("已有", conflict.DetailText); Assert.Contains("本次", conflict.DetailText);
        Assert.NotNull(report.LogPath); Assert.Null(report.LogWarning);
        var text = File.ReadAllText(report.LogPath!); Assert.Contains("冲突 ID：1", text); Assert.Contains("已有记录=", text); Assert.Contains("输入记录=", text); Assert.Contains("已提交", text);
        Assert.Equal(original, Assert.Single(store.Database.GetRecords(original.Uid)));
    }

    [Fact]
    public void MissingOptionalFieldsAreDuplicatesWithoutOverwritingOriginal()
    {
        using var store = new TestDatabase(); var original = TestData.Record(); store.Database.Insert([original]);
        var report = store.Database.Insert([original with { Name = null, ItemType = null, RankType = null }]);
        Assert.Empty(report.Details); Assert.Equal(0, report.Conflicts);
        Assert.Equal(1, report.Duplicates); Assert.Equal(0, report.Added);
        Assert.Equal(original, Assert.Single(store.Database.GetRecords(original.Uid)));
    }

    [Fact]
    public void LogFailureDoesNotMisreportSuccessfulDatabaseCommit()
    {
        using var store = new TestDatabase(); File.WriteAllText(Path.Combine(store.DirectoryPath, "logs"), "阻止日志目录创建");
        var report = store.Database.Insert([TestData.Record()]);
        Assert.Equal(1, report.Added); Assert.Null(report.LogPath); Assert.NotNull(report.LogWarning);
        Assert.Single(store.Database.GetRecords("100000001"));
    }
}
