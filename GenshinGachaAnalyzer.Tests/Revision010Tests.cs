using System.Text;
using GenshinGachaAnalyzer.Core.Services;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;
public sealed class Revision010Tests
{
    [Fact]
    public void GoldAfterFourStarKeepsOnePullOfFourStarProgress()
    {
        Assert.Equal(1, GachaCounters.FourStarProgress([TestData.Record("1", "302", "4"), TestData.Record("2", "302", "5")]));
        Assert.Equal(0, GachaCounters.FourStarProgress([TestData.Record("1", "302", "5"), TestData.Record("2", "302", "4")]));
        Assert.Null(GachaCounters.FourStarProgress([]));
    }
    [Theory]
    [InlineData("Failed https://hk4e-api.mihoyo.com/log?region=cn_gf01&authkey=NEVER_LOG#log")]
    [InlineData("authkey=NEVER_LOG")]
    [InlineData("\"stoken\":\"NEVER_LOG\"")]
    [InlineData("authkey%3DNEVER_LOG")]
    [InlineData("\\\"authkey\\\":\\\"NEVER_LOG\\\"")]
    [InlineData("Cookie: other=ok; session=NEVER_LOG")]
    public void DiagnosticMessagesRemoveCredentials(string text) => Assert.DoesNotContain("NEVER_LOG", DiagnosticLog.Sanitize(text));

    [Fact]
    public async Task EntireCacheIsScannedIncludingUrlBeforeLast64Megabytes()
    {
        using var store = new TestDatabase();
        string root = Path.Combine(store.DirectoryPath, "YuanShen_Data", "webCaches", "2.40.0.0", "Cache", "Cache_Data");
        Directory.CreateDirectory(root);
        await using (var stream = File.Create(Path.Combine(root, "data_2")))
        {
            var bytes = Encoding.UTF8.GetBytes("\0https://webstatic.mihoyo.com/hk4e/event/e20190909gacha/index.html?authkey=early&region=cn_gf01#/log\0");
            await stream.WriteAsync(bytes); stream.SetLength(70L * 1024 * 1024);
        }
        using var client = new HttpClient();
        var credential = await new GachaLogService(client).ExtractCredentialAsync(store.DirectoryPath);
        Assert.Equal("early", credential.Parameters["authkey"]);
    }
    [Fact]
    public async Task NewestCacheVersionTakesPriorityOverOldFileTimestamp()
    {
        using var store = new TestDatabase();
        foreach (var version in new[] { "2.9.0.0", "2.10.0.0" })
        {
            string root = Path.Combine(store.DirectoryPath, "YuanShen_Data", "webCaches", version, "Cache", "Cache_Data"); Directory.CreateDirectory(root);
            string file = Path.Combine(root, "data_2");
            await File.WriteAllTextAsync(file, $"\0https://hk4e-api.mihoyo.com/log?authkey={version}&region=cn_gf01\0");
            File.SetLastWriteTimeUtc(file, version == "2.9.0.0" ? DateTime.UtcNow : DateTime.UtcNow.AddDays(-1));
        }
        using var client = new HttpClient();
        Assert.Equal("2.10.0.0", (await new GachaLogService(client).ExtractCredentialAsync(store.DirectoryPath)).Parameters["authkey"]);
    }
    [Fact]
    public async Task UrlAtChunkBoundaryIsNotTruncatedAndLastUrlWins()
    {
        using var store = new TestDatabase();
        string root = Path.Combine(store.DirectoryPath, "YuanShen_Data", "webCaches"); Directory.CreateDirectory(root);
        string link = "https://hk4e-api.mihoyo.com/log?region=cn_gf01&authkey=LAST_COMPLETE_KEY\0";
        await File.WriteAllTextAsync(Path.Combine(root, "data_2"), "https://hk4e-api.mihoyo.com/log?region=cn_gf01&authkey=old\0" + new string('\0', 1024 * 1024 - 90) + link);
        using var client = new HttpClient();
        Assert.Equal("LAST_COMPLETE_KEY", (await new GachaLogService(client).ExtractCredentialAsync(store.DirectoryPath)).Parameters["authkey"]);
    }
    [Fact]
    public void RemovedBlueThemeMigratesAndTransparencySettingsPersist()
    {
        using var store = new TestDatabase(); var service = new SettingsService(Path.Combine(store.DirectoryPath, "settings.json"));
        service.Save(new(StyleKey: "Puppet", OverlayOpacity: .45, PanelOpacity: .7, WallpaperBlur: 24, DeveloperMode: true));
        var saved = service.Load(); Assert.Equal("Gear", saved.StyleKey); Assert.Equal(.45, saved.OverlayOpacity);
        Assert.Equal(.7, saved.PanelOpacity); Assert.Equal(24, saved.WallpaperBlur); Assert.True(saved.DeveloperMode);
    }
}
