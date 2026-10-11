using System.Text.Json;
using System.Text.Json.Nodes;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.Core.Services;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;

public sealed class CompatibilityRegressionTests
{
    [Theory]
    [InlineData("v4.2")][InlineData("v4.1")][InlineData("v4.0")]
    [InlineData("v3.0")][InlineData("v2.4")][InlineData("v2.3")][InlineData("v2.2")]
    public async Task EverySupportedVersionExportsAndImportsWithoutPrivateFields(string version)
    {
        using var store = new TestDatabase(); var service = new UigfService();
        var raw = TestData.Record() with { ItemId = "10000022", Name = "温迪", ItemType = "角色", RankType = "5" };
        store.Database.Insert([raw]); string path = Path.Combine(store.DirectoryPath, "exchange.json");
        await service.ExportAsync(path, store.Database.Snapshot([raw.Uid]), version: version);
        var prepared = await service.PreflightAsync(path);
        Assert.Equal(version, prepared.Version); Assert.Equal(raw, Assert.Single(prepared.Records));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(version.StartsWith("v4.") ? new[] { "info", "hk4e" } : new[] { "info", "list" }, json.RootElement.EnumerateObject().Select(p => p.Name));
        var entry = version.StartsWith("v4.") ? json.RootElement.GetProperty("hk4e")[0].GetProperty("list")[0] : json.RootElement.GetProperty("list")[0];
        Assert.False(entry.TryGetProperty("source", out _));
    }
    [Fact]
    public void V22SupportsAbsentItemIdByExactHutaoNameMapping()
    {
        using var json = JsonDocument.Parse("""{"info":{"uid":"100000001","uigf_version":"v2.2"},"list":[{"id":"1","gacha_type":"301","uigf_gacha_type":"301","name":"温迪","item_type":"角色","time":"2025-01-01 00:00:00"}]}""");
        var prepared = new UigfService().Parse(json.RootElement);
        Assert.Equal("10000022", Assert.Single(prepared.Records).ItemId);
        Assert.Null(prepared.Records[0].RankType);
        Assert.Equal("5", new WishCatalogService().Resolve(prepared.Records[0]).Rank);
    }
    [Theory][InlineData("v3.0")][InlineData("v2.4")][InlineData("v2.3")]
    public void LegacyExplicitTimezoneTakesPrecedenceOverNationalUid(string version)
    {
        var list = JsonNode.Parse(TestData.Json().GetRawText())!["hk4e"]![0]!["list"]!.DeepClone();
        var root = new JsonObject { ["info"] = new JsonObject { ["uid"] = "100000001", ["uigf_version"] = version, ["region_time_zone"] = 5.5 }, ["list"] = list };
        var record = Assert.Single(new UigfService().Parse(JsonSerializer.SerializeToElement(root)).Records);
        Assert.Equal(new DateTime(2025, 1, 1, 2, 30, 0), record.Time);
    }
    [Fact]
    public async Task LegacyExportCannotSilentlyDropChronicleOrOtherAccounts()
    {
        using var store = new TestDatabase(); var service = new UigfService();
        store.Database.Insert([TestData.Record("1", "500"), TestData.Record("2", uid: "200000001")]);
        string path = Path.Combine(store.DirectoryPath, "old.json");
        await Assert.ThrowsAsync<UserFacingException>(() => service.ExportAsync(path, store.Database.Snapshot(["100000001"]), version: "v2.4"));
        await Assert.ThrowsAsync<UserFacingException>(() => service.ExportAsync(path, store.Database.Snapshot(["100000001", "200000001"]), version: "v3.0"));
        Assert.False(File.Exists(path));
    }
    [Fact]
    public void DeletingOtherAccountPreservesChartsForRecordsWithoutOptionalRank()
    {
        using var store = new TestDatabase(); var analysis = new AnalysisService(new EmbeddedBannerProvider());
        var second = TestData.Sequence("301", 1, 10, 20).Select(r => r with { Uid = "200000001", ItemId = r.RankType == "5" ? "10000022" : "11301", RankType = null, Name = null, ItemType = null }).ToArray();
        store.Database.Insert(second.Append(TestData.Record("999", uid: "100000001")).ToArray());
        var before = analysis.Analyze(store.Database.GetRecords("200000001"), PoolCatalog.MainPools[0]);
        Assert.Equal(3, before.FiveStarCount); Assert.Equal(3, before.Histogram.Sum(b => b.Count));
        store.Database.ClearAccount("100000001");
        var saved = store.Database.GetRecords("200000001"); var after = analysis.Analyze(saved, PoolCatalog.MainPools[0]);
        Assert.Equal(second, saved); Assert.All(saved, r => Assert.Null(r.RankType));
        Assert.Equal(before.FiveStarCount, after.FiveStarCount); Assert.Equal(before.Average, after.Average);
        Assert.Equal(before.Histogram.Select(b => b.Count), after.Histogram.Select(b => b.Count));
        Assert.Equal(before.Golds.Select(g => g.Pulls), after.Golds.Select(g => g.Pulls));
    }
    [Fact]
    public void OldSettingsDefaultToSystemAndThemePersistsWithoutChangingGamePath()
    {
        using var store = new TestDatabase(); string path = Path.Combine(store.DirectoryPath, "settings.json");
        File.WriteAllText(path, """{"GamePath":"D:\\Game","PrivacyMask":true}""");
        var service = new SettingsService(path); var settings = service.Load(); Assert.Equal("System", settings.Theme);
        service.Save(settings with { Theme = "Dark" }); Assert.Equal("Dark", service.Load().Theme); Assert.Equal(settings.GamePath, service.Load().GamePath);
    }
}
