using System.Text.Json;
using System.Text.Json.Nodes;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Services;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;

public sealed class UigfTests
{
    [Theory]
    [InlineData("v4.0")]
    [InlineData("v4.1")]
    [InlineData("v4.2")]
    public void BothSupportedVersionsParse(string version)
    {
        var result = new UigfService().Parse(TestData.Json(version));
        Assert.Equal(version, result.Version); Assert.Single(result.Accounts); Assert.Single(result.Records);
    }
    [Theory]
    [InlineData("v2.1")]
    [InlineData("v5.0")]
    [InlineData("v4.3")]
    public void UnsupportedVersionStopsBeforeRecordParsing(string version)
    {
        using var document = JsonDocument.Parse($$"""{"info":{"version":"{{version}}"},"hk4e":"invalid"}""");
        Assert.Throws<UnsupportedUigfException>(() => new UigfService().Parse(document.RootElement));
    }
    [Theory]
    [InlineData("id", "123")]
    [InlineData("gacha_type", "301")]
    [InlineData("item_id", "42")]
    [InlineData("rank_type", "5")]
    [InlineData("name", "null")]
    public void WrongFieldTypesAreReadable(string field, string json)
    {
        var node = JsonNode.Parse(TestData.Json().GetRawText())!;
        node["hk4e"]![0]!["list"]![0]![field] = JsonNode.Parse(json);
        var error = Assert.Throws<UserFacingException>(() => new UigfService().Parse(JsonSerializer.SerializeToElement(node)));
        Assert.Contains(field, error.Message); Assert.Contains("字段类型", error.Message);
    }
    [Fact]
    public void MissingRequiredFieldIsReported()
    {
        var node = JsonNode.Parse(TestData.Json().GetRawText())!; node["hk4e"]![0]!["list"]![0]!.AsObject().Remove("uigf_gacha_type");
        var error = Assert.Throws<UserFacingException>(() => new UigfService().Parse(JsonSerializer.SerializeToElement(node)));
        Assert.Contains("缺少必填字段 uigf_gacha_type", error.Message);
    }
    [Fact]
    public void OptionalMetadataIsNotFabricated()
    {
        var result = new UigfService().Parse(TestData.Json(optional: false));
        Assert.Null(result.Records[0].RankType); Assert.Null(result.Records[0].Name); Assert.NotEmpty(result.Warnings);
    }
    [Theory]
    [InlineData("400", "301")]
    [InlineData("500", "500")]
    [InlineData("302", "302")]
    [InlineData("100", "100")]
    public void CorrectPoolMappingIsPreserved(string raw, string shared)
    {
        var record = Assert.Single(new UigfService().Parse(TestData.Json(type: raw, shared: shared)).Records);
        Assert.Equal(raw, record.GachaType); Assert.Equal(shared, record.PoolType);
    }
    [Fact]
    public void InconsistentPoolIsRejected() => Assert.Throws<UserFacingException>(() => new UigfService().Parse(TestData.Json(type: "400", shared: "200")));
    [Fact]
    public void TimezoneIsNormalized()
    {
        var record = Assert.Single(new UigfService().Parse(TestData.Json(timezone: 0)).Records);
        Assert.Equal(new DateTime(2025, 1, 1, 8, 0, 0), record.Time); Assert.Equal(DateTimeKind.Unspecified, record.Time.Kind);
    }
    [Fact]
    public void NumericUidIsSupported()
    {
        var node = JsonNode.Parse(TestData.Json().GetRawText())!; node["hk4e"]![0]!["uid"] = 100000001;
        Assert.Equal("100000001", new UigfService().Parse(JsonSerializer.SerializeToElement(node)).Accounts[0].Uid);
    }
    [Fact]
    public void InternationalAccountIsRejected() => Assert.Throws<UserFacingException>(() => new UigfService().Parse(TestData.Json(uid: "800000001")));
    [Fact]
    public void DuplicatePropertiesAreRejected()
    {
        var json = TestData.Json().GetRawText().Replace("\"id\":\"1\"", "\"id\":\"1\",\"id\":\"2\"");
        using var document = JsonDocument.Parse(json);
        Assert.Throws<UserFacingException>(() => new UigfService().Parse(document.RootElement));
    }
    [Theory]
    [InlineData("2025-02-30 00:00:00")]
    [InlineData("2025/01/01 00:00:00")]
    public void InvalidDateIsRejected(string time)
    {
        var node = JsonNode.Parse(TestData.Json().GetRawText())!; node["hk4e"]![0]!["list"]![0]!["time"] = time;
        Assert.Throws<UserFacingException>(() => new UigfService().Parse(JsonSerializer.SerializeToElement(node)));
    }
    [Fact]
    public async Task ExportRoundTripsOnlyOfficialFieldsIncludingOptionalAbsence()
    {
        using var store = new TestDatabase(); var service = new UigfService();
        var import = service.Parse(TestData.Json(type: "400", optional: false)); store.Database.Insert(import.Records, import.Accounts);
        string path = Path.Combine(store.DirectoryPath, "export.json");
        await service.ExportAsync(path, store.Database.Snapshot(["100000001"]));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = document.RootElement;
        Assert.Equal(new[] { "info", "hk4e" }, root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("v4.1", root.GetProperty("info").GetProperty("version").GetString());
        var record = root.GetProperty("hk4e")[0].GetProperty("list")[0];
        Assert.False(record.TryGetProperty("source", out _)); Assert.False(record.TryGetProperty("rank_type", out _));
        Assert.Equal("301", record.GetProperty("uigf_gacha_type").GetString());
        Assert.Equal(import.Records, (await service.PreflightAsync(path)).Records);
    }
    [Fact]
    public async Task CancelledExportDoesNotReplaceExistingFile()
    {
        using var store = new TestDatabase(); store.Database.Insert([TestData.Record()]);
        string path = Path.Combine(store.DirectoryPath, "export.json"); await File.WriteAllTextAsync(path, "original");
        using var token = new CancellationTokenSource(); token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UigfService().ExportAsync(path, store.Database.Snapshot(["100000001"]), token.Token));
        Assert.Equal("original", await File.ReadAllTextAsync(path)); Assert.Empty(Directory.GetFiles(store.DirectoryPath, "*.partial"));
    }
    [Fact]
    public async Task NonJsonIsReadable()
    {
        using var store = new TestDatabase(); string path = Path.Combine(store.DirectoryPath, "bad.json"); File.WriteAllText(path, "this is not json");
        var error = await Assert.ThrowsAsync<UserFacingException>(() => new UigfService().PreflightAsync(path)); Assert.Contains("JSON", error.Message);
    }
}
