using System.Net;
using System.Text;
using System.Text.Json;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Services;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;

public sealed class GachaLogTests
{
    private const string Link = "https://hk4e-api.mihoyo.com/event/gacha_info/api/getGachaLog?authkey=a%2Bb%2Fc%3D&region=cn_gf01&game_biz=hk4e_cn";
    private static readonly GachaCredential Credential = GachaLogService.TryParseCredential(Link)!;
    private static string Page(string id = "20", string uid = "100000001", string itemId = "10001") => JsonSerializer.Serialize(new
    {
        retcode = 0, data = new { region = "cn_gf01", list = new[] { new
        {
            id, uid, gacha_type = "400", item_id = itemId, name = "测试物品", item_type = "角色", rank_type = "5", count = "1", time = "2025-01-01 00:00:00"
        } } }
    });
    private const string Empty = "{\"retcode\":0,\"data\":{\"region\":\"cn_gf01\",\"list\":[]}}";
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UrlFetchAndMinimalUigfAreDuplicatesInBothOrders(bool importFirst)
    {
        using var store = new TestDatabase(); int requests = 0;
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(++requests == 1 ? Page(id: "1", itemId: "") : Empty))));
        string file = Path.Combine(store.DirectoryPath, "minimal.json");
        await File.WriteAllTextAsync(file, TestData.Json(type: "400", shared: "301", optional: false).GetRawText());
        var prepared = await new UigfService().PreflightAsync(file);
        if (importFirst) store.Database.Insert(prepared.Records, prepared.Accounts);
        var fetched = await new GachaLogService(client).FetchAsync(Credential, store.Database);
        var saved = Assert.Single(store.Database.GetRecords("100000001"));
        if (importFirst)
        {
            Assert.Equal(1, fetched.Duplicates); Assert.Equal(0, fetched.Added); Assert.Empty(fetched.ConflictIds);
            Assert.Equal(prepared.Records[0], saved);
        }
        else
        {
            Assert.Equal(1, fetched.Added);
            var imported = store.Database.Insert(prepared.Records, prepared.Accounts);
            Assert.Equal(1, imported.Duplicates); Assert.Equal(0, imported.Added); Assert.Equal(0, imported.Conflicts);
        }
        Assert.Equal(saved, Assert.Single(store.Database.GetRecords("100000001")));
    }
    [Theory]
    [InlineData("https://hk4e-api-os.hoyoverse.com/?authkey=secret&region=os_asia")]
    [InlineData("https://evil.example/?authkey=secret&region=cn_gf01")]
    [InlineData("http://hk4e-api.mihoyo.com/?authkey=secret&region=cn_gf01")]
    [InlineData("https://hk4e-api.mihoyo.com:444/?authkey=secret&region=cn_gf01")]
    [InlineData("https://hk4e-api.mihoyo.com/?authkey=secret&region=os_asia")]
    [InlineData("https://hk4e-api.mihoyo.com/?authkey=secret&region=cn_gf01&game_biz=hk4e_global")]
    [InlineData("https://public-operation-hk4e-sg.hoyoverse.com/gacha_info/api/getGachaLog?authkey=secret&region=os_asia")]
    [InlineData("https://public-operation-hk4e.mihoyo.com.evil.example/gacha_info/api/getGachaLog?authkey=secret&region=cn_gf01")]
    [InlineData("https://public-operation-hk4e.mihoyo.com/gacha_info/api/getBeyondGachaLog?authkey=secret&region=cn_gf01")]
    public void CredentialsOnlyAcceptChineseOfficialLinks(string link) => Assert.Null(GachaLogService.TryParseCredential(link));
    [Fact]
    public void AuthKeyRoundTripEscapingIsLossless()
    {
        Assert.Equal("a+b/c=", Credential.Parameters["authkey"]);
        var request = GachaLogService.BuildRequestUri(Credential, "301", "20", 2);
        Assert.Equal("public-operation-hk4e.mihoyo.com", request.Host);
        Assert.Equal("/gacha_info/api/getGachaLog", request.AbsolutePath);
        Assert.Contains("authkey=a%2Bb%2Fc%3D", request.Query);
        Assert.Contains("size=20", request.Query); Assert.Contains("end_id=20", request.Query);
    }
    [Fact]
    public async Task LatestOfficialLinkIsExtractedAndNeverSendsToLegacyEndpoint()
    {
        using var store = new TestDatabase();
        string cache = Path.Combine(store.DirectoryPath, "game", "YuanShen_Data", "webCaches", "2.55.0", "Cache", "Cache_Data");
        Directory.CreateDirectory(cache);
        const string current = "https://public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog?authkey=current%2Bkey&region=cn_gf01&game_biz=hk4e_cn";
        string file = Path.Combine(cache, "data_2");
        await File.WriteAllTextAsync(file, "\0" + Link + "\0" + current + "\0");
        var requests = new List<Uri>();
        using var client = new HttpClient(new Handler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return Task.FromResult(Json(Empty));
        }));
        var service = new GachaLogService(client);
        var credential = await service.ExtractCredentialAsync(Path.Combine(store.DirectoryPath, "game"));
        Assert.Equal("current+key", credential.Parameters["authkey"]);
        await service.FetchAsync(credential, store.Database);
        Assert.Equal(4, requests.Count);
        Assert.All(requests, request => Assert.Equal("public-operation-hk4e.mihoyo.com", request.Host));
        Assert.Empty(store.Database.GetAccounts());
    }
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task PermanentHttpErrorsAreNotReportedAsTimeoutOrRetried(HttpStatusCode status)
    {
        using var store = new TestDatabase(); int requests = 0;
        using var client = new HttpClient(new Handler((_, _) =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("authkey=SERVER_SECRET") });
        }));
        var error = await Assert.ThrowsAsync<UserFacingException>(() => new GachaLogService(client).FetchAsync(Credential, store.Database));
        Assert.Contains($"HTTP {(int)status}", error.Message);
        Assert.DoesNotContain("超时", error.Message);
        Assert.DoesNotContain("SERVER_SECRET", error.Message);
        Assert.Equal(1, requests); Assert.Empty(store.Database.GetAccounts());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransientHttpFailureAndActualTimeoutRetainDifferentMessages(bool timeout)
    {
        using var store = new TestDatabase(); int requests = 0;
        using var client = new HttpClient(new Handler((_, _) =>
        {
            requests++;
            if (timeout) throw new TaskCanceledException("authkey=NETWORK_SECRET");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }));
        var error = await Assert.ThrowsAsync<UserFacingException>(() => new GachaLogService(client).FetchAsync(Credential, store.Database));
        Assert.Contains(timeout ? "超时" : "HTTP 500", error.Message);
        Assert.DoesNotContain("NETWORK_SECRET", error.Message);
        Assert.Equal(3, requests); Assert.Empty(store.Database.GetAccounts());
    }
    [Fact]
    public async Task FetchPaginatesAllFourPoolsAndMergesCharacterBanners()
    {
        using var store = new TestDatabase(); var requests = new List<Uri>();
        using var client = new HttpClient(new Handler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return Task.FromResult(Json(requests.Count == 1 ? Page() : Empty));
        }));
        var result = await new GachaLogService(client).FetchAsync(Credential, store.Database);
        Assert.Equal(1, result.Added); Assert.Equal(5, requests.Count);
        Assert.Contains("end_id=20", requests[1].Query); Assert.Contains("gacha_type=302", requests[2].Query);
        Assert.Contains("gacha_type=500", requests[3].Query); Assert.Contains("gacha_type=200", requests[4].Query);
        Assert.Equal("400", Assert.Single(store.Database.GetRecords("100000001")).GachaType);
    }
    [Fact]
    public async Task FetchConflictRetainsFieldDetailsAndWritesNoCredentialToLogs()
    {
        using var store = new TestDatabase();
        var original = TestData.Record("20", "400", "5") with { Name = "旧名称" }; store.Database.Insert([original]);
        int count = 0;
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(++count == 1 ? Page() : Empty))));
        var result = await new GachaLogService(client).FetchAsync(Credential, store.Database);
        Assert.Equal("20", Assert.Single(result.ConflictIds)); Assert.Single(result.Details); Assert.NotNull(result.LogPath);
        Assert.Equal(original, Assert.Single(store.Database.GetRecords(original.Uid)));
        foreach (var path in Directory.EnumerateFiles(Path.Combine(store.DirectoryPath, "logs")))
        {
            var text = File.ReadAllText(path); Assert.DoesNotContain("authkey", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Credential.Parameters["authkey"], text); Assert.Contains("已提交", text);
        }
    }
    [Fact]
    public async Task NetworkFailurePreservesCommittedPageAndRetryDeduplicates()
    {
        using var store = new TestDatabase(); int requests = 0;
        using (var client = new HttpClient(new Handler((_, _) =>
        {
            if (++requests == 1) return Task.FromResult(Json(Page()));
            throw new HttpRequestException("authkey=NEVER_DISPLAY");
        })))
        {
            var error = await Assert.ThrowsAsync<UserFacingException>(() => new GachaLogService(client).FetchAsync(Credential, store.Database));
            Assert.DoesNotContain("NEVER_DISPLAY", error.Message); Assert.Single(store.Database.GetRecords("100000001"));
        }
        int retryRequests = 0;
        using var retryClient = new HttpClient(new Handler((_, _) => Task.FromResult(Json(++retryRequests == 1 ? Page() : Empty))));
        var result = await new GachaLogService(retryClient).FetchAsync(Credential, store.Database);
        Assert.Equal(0, result.Added); Assert.Equal(1, result.Duplicates); Assert.Single(store.Database.GetRecords("100000001"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FetchResolvesActualAccountAndCreatesOrUpdatesIt(bool alreadyExists)
    {
        using var store = new TestDatabase();
        var selectedRecord = TestData.Record("999", "400", uid: "200000001");
        store.Database.Insert([selectedRecord]);
        if (alreadyExists) store.Database.Insert([TestData.Record("20", "400", "5")]);
        int requests = 0, notifications = 0;
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(++requests == 1 ? Page() : Empty))));
        var result = await new GachaLogService(client).FetchAsync(Credential, store.Database, accountResolved: uid =>
        {
            Assert.Equal("100000001", uid);
            Assert.Single(store.Database.GetRecords(uid)); // 通知发生在事务已提交之后。
            notifications++;
            return Task.CompletedTask;
        });
        Assert.Equal("100000001", result.Uid); Assert.Equal(1, notifications);
        Assert.Equal(alreadyExists ? 0 : 1, result.Added);
        Assert.Equal(alreadyExists ? 1 : 0, result.Duplicates);
        Assert.Equal(2, store.Database.GetAccounts().Count);
        Assert.Equal(selectedRecord, Assert.Single(store.Database.GetRecords("200000001")));
    }
    [Fact]
    public async Task ResolvedAccountIsNotLostWhenLaterPageFails()
    {
        using var store = new TestDatabase(); int requests = 0; string? resolvedUid = null;
        using var client = new HttpClient(new Handler((_, _) =>
        {
            if (++requests == 1) return Task.FromResult(Json(Page()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
        }));
        await Assert.ThrowsAsync<UserFacingException>(() => new GachaLogService(client).FetchAsync(Credential, store.Database,
            accountResolved: uid => { resolvedUid = uid; return Task.CompletedTask; }));
        Assert.Equal("100000001", resolvedUid);
        Assert.Single(store.Database.GetRecords(resolvedUid!));
    }
    [Fact]
    public async Task DifferentUidDuringOneFetchStillRejectsThatPage()
    {
        using var store = new TestDatabase(); int requests = 0;
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(
            ++requests == 1 ? Page() : Page("19", "200000001")))));
        var error = await Assert.ThrowsAsync<UserFacingException>(() => new GachaLogService(client).FetchAsync(Credential, store.Database));
        Assert.Contains("本次获取过程中", error.Message);
        Assert.Single(store.Database.GetRecords("100000001"));
        Assert.Empty(store.Database.GetRecords("200000001"));
        Assert.Single(store.Database.GetAccounts());
    }
    [Fact]
    public async Task EmptyResponseDoesNotInventAccountOrNotifySelection()
    {
        using var store = new TestDatabase(); bool notified = false;
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(Empty))));
        var result = await new GachaLogService(client).FetchAsync(Credential, store.Database,
            accountResolved: _ => { notified = true; return Task.CompletedTask; });
        Assert.Null(result.Uid); Assert.False(notified); Assert.Empty(store.Database.GetAccounts());
    }
    [Fact]
    public async Task ExpiredAuthKeyIsReadableAndNeverEchoesServerMessage()
    {
        using var store = new TestDatabase();
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json("{\"retcode\":-100,\"message\":\"secret authkey\"}"))));
        var error = await Assert.ThrowsAsync<AuthKeyExpiredException>(() => new GachaLogService(client).FetchAsync(Credential, store.Database));
        Assert.DoesNotContain("secret", error.Message); Assert.Empty(store.Database.GetAccounts());
    }
    [Fact]
    public async Task NonAdvancingCursorStopsWithoutInfiniteLoop()
    {
        using var store = new TestDatabase(); int requests = 0;
        using var client = new HttpClient(new Handler((_, _) => { requests++; return Task.FromResult(Json(Page())); }));
        await Assert.ThrowsAsync<UserFacingException>(() => new GachaLogService(client).FetchAsync(Credential, store.Database));
        Assert.Equal(2, requests); Assert.Single(store.Database.GetRecords("100000001"));
    }
    [Fact]
    public async Task ExtractsLatestCredentialFromVersionedChromiumCacheWithoutChangingIt()
    {
        using var store = new TestDatabase(); string game = Path.Combine(store.DirectoryPath, "game");
        string cache = Path.Combine(game, "YuanShen_Data", "webCaches", "2.34.0", "Cache", "Cache_Data"); Directory.CreateDirectory(cache);
        string file = Path.Combine(cache, "data_2");
        var bytes = Encoding.UTF8.GetBytes("\0" + Link + "\0https://hk4e-api.mihoyo.com/?authkey=newest&region=cn_gf01\0"); File.WriteAllBytes(file, bytes);
        using var client = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("提取凭据不应联网")));
        var credential = await new GachaLogService(client).ExtractCredentialAsync(game);
        Assert.Equal("newest", credential.Parameters["authkey"]); Assert.Equal(bytes, File.ReadAllBytes(file));
    }
    [Fact]
    public void HttpErrorsNeverLeakCredentialToUserMessage() =>
        Assert.DoesNotContain("SECRET", ErrorMessages.Readable(new HttpRequestException("https://example/?authkey=SECRET")));
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RateLimitsRetryThenResumeWithoutWritingInvalidResponse(bool httpRateLimit)
    {
        using var store = new TestDatabase(); int requests = 0;
        using var client = new HttpClient(new Handler((_, _) =>
        {
            requests++;
            if (requests == 1) return Task.FromResult(httpRateLimit ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : Json("{\"retcode\":-110}"));
            return Task.FromResult(Json(requests == 2 ? Page() : Empty));
        }));
        var result = await new GachaLogService(client).FetchAsync(Credential, store.Database);
        Assert.Equal(6, requests); Assert.Equal(1, result.Added); Assert.Single(store.Database.GetRecords("100000001"));
    }
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}
