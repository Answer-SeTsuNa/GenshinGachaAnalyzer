using System.Net;
using System.Text.Json;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Services;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;

public sealed class WishCatalogUpdateTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gacha-catalog-tests-" + Guid.NewGuid().ToString("N"));
    private string CachePath => Path.Combine(_directory, "snapshot.json");
    private static WishItem Item => new("10000001", "合成角色", "角色", "5", "UI_SYNTHETIC");
    private static WishEvent Event => new("合成卡池", "1.0", 1, "301", new(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)), new(2026, 1, 21, 0, 0, 0, TimeSpan.FromHours(8)), ["10000001"], []);
    private static WishCatalogService Catalog() => new([Item], [Event]);
    private static string RemoteEvents(params WishEvent[] events) => JsonSerializer.Serialize(events.Select(e => new
    { e.Name, e.Version, e.Order, Type = int.Parse(e.Type), e.From, e.To, UpOrangeList = e.UpGold.Select(long.Parse), UpPurpleList = e.UpPurple.Select(long.Parse), Banner = e.BannerUrl, Banner2 = e.BannerAlternate }));
    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };
    private static string OfficialItems(string kind) => JsonSerializer.Serialize(new { retcode = 0, data = new { total = 1, list = new[] {
        new Dictionary<string, object> { ["id"] = kind == "avatar" ? 10000002 : 11501, ["name"] = "合成" + kind,
            [kind == "avatar" ? "avatar_level" : "weapon_level"] = 5, ["icon"] = "https://act-webstatic.mihoyo.com/hk4e/e20200928calculate/item_icon/synthetic.png" } } } });
    [Fact]
    public async Task UpdateAddsNewPeriodAndOfficialItemsAndSurvivesOfflineRestart()
    {
        var next = Event with { Name = "新卡池", Version = "1.1", From = Event.From.AddDays(21), To = Event.To.AddDays(21), BannerUrl = "https://sdk-webstatic.mihoyo.com/new.jpg" };
        int requests = 0;
        using var client = new HttpClient(new Handler(r =>
        {
            requests++; Assert.Null(r.Headers.Authorization); Assert.False(r.Headers.Contains("Cookie"));
            Assert.DoesNotContain("uid", r.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
            return Json(r.Method == HttpMethod.Get ? RemoteEvents(Event, next) : OfficialItems(r.RequestUri.AbsolutePath.Contains("avatar") ? "avatar" : "weapon"));
        }));
        var service = new WishCatalogUpdateService(client, CachePath); var catalog = Catalog();
        var snapshot = await service.DownloadAsync(catalog, default); catalog.Apply(snapshot);
        Assert.Equal(3, requests); Assert.Equal(next.To, catalog.CoveredThrough); Assert.Equal("新卡池", catalog.Events.Last().Name);
        Assert.Equal("合成avatar", catalog.FindById("10000002")!.Name);
        Assert.EndsWith(".jpg", catalog.Events.Last().BannerFile);
        Assert.Single(Directory.GetFiles(_directory));
        using var offline = new HttpClient(new Handler(_ => throw new HttpRequestException()));
        var restarted = Catalog(); var cached = new WishCatalogUpdateService(offline, CachePath).LoadCache(restarted);
        Assert.NotNull(cached); restarted.Apply(cached); Assert.Equal(next.To, restarted.CoveredThrough);
    }
    [Fact]
    public async Task CorrectedScheduleReplacesOldPeriodInsteadOfDuplicatingIt()
    {
        var corrected = Event with { From = Event.From.AddDays(3) };
        using var client = new HttpClient(new Handler(r => Json(r.Method == HttpMethod.Get ? RemoteEvents(corrected) : OfficialItems(r.RequestUri!.AbsolutePath.Contains("avatar") ? "avatar" : "weapon"))));
        var catalog = Catalog(); catalog.Apply(await new WishCatalogUpdateService(client, CachePath).DownloadAsync(catalog, default));
        Assert.Single(catalog.Events); Assert.Equal(corrected.From, catalog.Events.Single().From);
    }
    [Fact]
    public async Task FailedOrIncompleteUpdateKeepsPreviousCacheAndCatalog()
    {
        Directory.CreateDirectory(_directory); string original = JsonSerializer.Serialize(new CatalogSnapshot(DateTimeOffset.UtcNow, [Item], [Event]));
        File.WriteAllText(CachePath, original);
        using var client = new HttpClient(new Handler(_ => Json(RemoteEvents(Event with { From = Event.From.AddDays(-30), To = Event.To.AddDays(-30) }))));
        var catalog = Catalog();
        await Assert.ThrowsAsync<UserFacingException>(() => new WishCatalogUpdateService(client, CachePath).DownloadAsync(catalog, default));
        Assert.Equal(original, File.ReadAllText(CachePath)); Assert.Equal(Event.To, catalog.CoveredThrough);
    }
    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{\"Items\":null,\"Events\":null}")]
    public void CorruptCacheFallsBackToBundledData(string data)
    {
        Directory.CreateDirectory(_directory); File.WriteAllText(CachePath, data);
        using var client = new HttpClient(new Handler(_ => throw new InvalidOperationException("缓存加载不应联网")));
        Assert.Null(new WishCatalogUpdateService(client, CachePath).LoadCache(Catalog()));
    }
    [Theory]
    [InlineData("https://mihoyo.com.evil.invalid/image.png")]
    [InlineData("http://sdk-webstatic.mihoyo.com/image.png")]
    [InlineData("https://sdk-webstatic.mihoyo.com/image.png?authkey=secret")]
    [InlineData("file:///C:/secret.png")]
    public void RejectsUntrustedOrCredentialBearingImages(string url)
    {
        Assert.False(WishCatalogUpdateService.IsOfficialAsset(url));
        Assert.Throws<InvalidDataException>(() => WishCatalogUpdateService.Validate(new(DateTimeOffset.UtcNow, [Item with { Icon = url }], [Event])));
    }
    [Fact]
    public void RejectsInvalidTimeAndDuplicateKeysAndPathTraversal()
    {
        Assert.Throws<InvalidDataException>(() => WishCatalogUpdateService.Validate(new(DateTimeOffset.UtcNow, [Item], [Event with { To = Event.From }])));
        Assert.Throws<InvalidDataException>(() => WishCatalogUpdateService.Validate(new(DateTimeOffset.UtcNow, [Item], [Event, Event])));
        Assert.Throws<InvalidDataException>(() => WishCatalogUpdateService.Validate(new(DateTimeOffset.UtcNow, [Item], [Event with { BannerFile = "../secret.png" }])));
    }
    [Fact]
    public async Task RedirectAndRawResponseErrorsNeverOverwriteCacheOrExposePayload()
    {
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.Redirect) { Content = new StringContent("SYNTHETIC_PRIVATE_RESPONSE") }));
        var error = await Assert.ThrowsAsync<UserFacingException>(() => new WishCatalogUpdateService(client, CachePath).DownloadAsync(Catalog(), default));
        Assert.DoesNotContain("SYNTHETIC_PRIVATE_RESPONSE", error.ToString()); Assert.False(File.Exists(CachePath));
    }
    [Fact]
    public async Task OversizedResponseIsRejectedWithoutCaching()
    {
        using var client = new HttpClient(new Handler(_ => Json(new string('x', 8 * 1024 * 1024 + 1))));
        await Assert.ThrowsAsync<UserFacingException>(() => new WishCatalogUpdateService(client, CachePath).DownloadAsync(Catalog(), default));
        Assert.False(File.Exists(CachePath));
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request)); }
}
