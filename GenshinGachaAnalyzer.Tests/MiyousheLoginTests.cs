using System.Net;
using System.Text.Json;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Services;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;

public sealed class MiyousheLoginTests
{
    public const string Confirmed = "{\"retcode\":0,\"data\":{\"status\":\"Confirmed\",\"tokens\":[{\"token_type\":1,\"token\":\"SYNTHETIC_LOGIN_SECRET\"}],\"user_info\":{\"aid\":\"12345678\",\"mid\":\"SYNTHETIC_MID\"}}}";
    [Fact]
    public async Task QrLoginRolesAndSelectedUidProduceWishCredentialWithoutGame()
    {
        var requests = new List<string>();
        using var client = new HttpClient(new Handler(request =>
        {
            string path = request.RequestUri!.AbsolutePath; requests.Add(path);
            Assert.True(MiyousheLoginService.IsOfficialPage(request.RequestUri));
            Assert.Null(request.Headers.Authorization);
            if (path.EndsWith("createQRLogin"))
            {
                Assert.False(request.Headers.Contains("Cookie"));
                return Json("{\"retcode\":0,\"data\":{\"url\":\"https://user.mihoyo.com/?ticket=SYNTHETIC\",\"ticket\":\"SYNTHETIC\"}}");
            }
            if (path.EndsWith("queryQRLoginStatus")) { Assert.False(request.Headers.Contains("Cookie")); return Json(Confirmed); }
            Assert.Contains("stoken=SYNTHETIC_LOGIN_SECRET", request.Headers.GetValues("Cookie").Single());
            Assert.True(request.Headers.Contains("DS"));
            if (path.EndsWith("getUserGameRolesByStoken")) return Json("{\"retcode\":0,\"data\":{\"list\":[{\"game_uid\":\"100000001\",\"region\":\"cn_gf01\",\"nickname\":\"合成账号\",\"region_name\":\"天空岛\"},{\"game_uid\":\"600000001\",\"region\":\"os_usa\"}]}}");
            using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            Assert.Equal(100000001, body.RootElement.GetProperty("game_uid").GetInt64());
            Assert.Equal("webview_gacha", body.RootElement.GetProperty("auth_appid").GetString());
            return Json("{\"retcode\":0,\"data\":{\"authkey\":\"SYNTHETIC_WISH_KEY\"}}");
        }));
        var service = new MiyousheLoginService(client);
        Assert.Empty(requests);
        var qr = await service.CreateQrAsync(default);
        Assert.DoesNotContain("SYNTHETIC", qr.ToString());
        var progress = await service.PollQrAsync(qr, default);
        Assert.DoesNotContain("SYNTHETIC_LOGIN_SECRET", progress.Session!.ToString());
        var role = Assert.Single(await service.GetRolesAsync(progress.Session, default));
        var credential = await service.GetWishCredentialAsync(progress.Session, role, default);
        Assert.Equal(role.Uid, credential.ExpectedUid);
        Assert.Equal("SYNTHETIC_WISH_KEY", credential.Parameters["authkey"]);
        Assert.Equal(4, requests.Count);
    }
    [Theory]
    [InlineData("Created")]
    [InlineData("Scanned")]
    public async Task PendingLoginDoesNotExposeSession(string state)
    {
        using var client = new HttpClient(new Handler(_ => Json(JsonSerializer.Serialize(new { retcode = 0, data = new { status = state } }))));
        var result = await new MiyousheLoginService(client).PollQrAsync(new("https://user.mihoyo.com", "SYNTHETIC"), default);
        Assert.Null(result.Session); Assert.Equal(state, result.Status);
    }
    [Fact]
    public async Task ExpiredQrRequiresRefresh()
    {
        using var client = new HttpClient(new Handler(_ => Json("{\"retcode\":-3501}")));
        Assert.Equal("Expired", (await new MiyousheLoginService(client).PollQrAsync(new("", "SYNTHETIC"), default)).Status);
    }
    [Fact]
    public void OfficialWebLoginParsesOnlyUsefulAuthorization()
    {
        var session = MiyousheLoginService.ParseWebLogin(Confirmed);
        Assert.NotNull(session);
        Assert.DoesNotContain("SYNTHETIC", session.ToString());
        Assert.Null(MiyousheLoginService.ParseWebLogin(Confirmed.Replace("\"token_type\":1", "\"token_type\":4")));
        Assert.Null(MiyousheLoginService.ParseWebLogin("not json"));
    }
    [Theory]
    [InlineData("https://passport-api.mihoyo.com/account/ma-cn-passport/web/loginByPassword", true)]
    [InlineData("https://evil.example/account/ma-cn-passport/web/loginByPassword", false)]
    [InlineData("https://passport-api.mihoyo.com.evil.example/account/ma-cn-passport/web/loginByPassword", false)]
    [InlineData("http://passport-api.mihoyo.com/account/ma-cn-passport/web/loginByPassword", false)]
    [InlineData("https://passport-api.mihoyo.com:444/account/ma-cn-passport/web/loginByPassword", false)]
    public void LoginResponseOriginIsStrictlyValidated(string url, bool valid) => Assert.Equal(valid, MiyousheLoginService.IsLoginResponse(new Uri(url)));

    [Fact]
    public async Task FailureDoesNotEchoResponseOrCredentialUrl()
    {
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.Forbidden) { Content = new StringContent("SYNTHETIC_LOGIN_SECRET") }));
        var error = await Assert.ThrowsAsync<UserFacingException>(() => new MiyousheLoginService(client).CreateQrAsync(default));
        Assert.DoesNotContain("SYNTHETIC", error.ToString()); Assert.DoesNotContain("https://", error.ToString());
    }
    [Fact]
    public async Task RedirectDoesNotProceedWithSession()
    {
        int calls = 0;
        using var client = new HttpClient(new Handler(_ => { calls++; return new(HttpStatusCode.Redirect); }));
        await Assert.ThrowsAsync<UserFacingException>(() => new MiyousheLoginService(client).GetRolesAsync(new("123", "SYNTHETIC"), default));
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task AccountLoginCanUseCurrentCookiesWithoutAnotherLoginRequest()
    {
        using var client = new HttpClient(new Handler(_ => throw new InvalidOperationException("不应发出网络请求")));
        var session = await new MiyousheLoginService(client).FromLoginCookiesAsync(new Dictionary<string,string>
        { ["stuid_v2"] = "123", ["stoken_v2"] = "SYNTHETIC_TOKEN", ["mid"] = "SYNTHETIC_MID" }, default);
        Assert.NotNull(session); Assert.DoesNotContain("SYNTHETIC", session.ToString());
    }
    [Fact]
    public async Task AccountLoginCanExchangeLegacyOfficialLoginTicket()
    {
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal("api-takumi.mihoyo.com", request.RequestUri!.Host);
            Assert.EndsWith("getMultiTokenByLoginTicket", request.RequestUri.AbsolutePath);
            Assert.False(request.Headers.Contains("Cookie"));
            return Json("{\"retcode\":0,\"data\":{\"list\":[{\"name\":\"stoken\",\"token\":\"SYNTHETIC_TOKEN\"}]}}");
        }));
        var session = await new MiyousheLoginService(client).FromLoginCookiesAsync(new Dictionary<string,string>
        { ["login_uid"] = "123", ["login_ticket"] = "SYNTHETIC_TICKET" }, default);
        Assert.NotNull(session); Assert.DoesNotContain("SYNTHETIC", session.ToString());
    }
    [Fact]
    public async Task WrongSelectedUidIsRejectedBeforeDatabaseWrite()
    {
        using var store = new TestDatabase();
        using var client = new HttpClient(new Handler(_ => Json("{\"retcode\":0,\"data\":{\"region\":\"cn_gf01\",\"list\":[{\"id\":\"1\",\"uid\":\"200000001\",\"gacha_type\":\"301\",\"item_id\":\"10000030\",\"name\":\"钟离\",\"item_type\":\"角色\",\"rank_type\":\"5\",\"count\":\"1\",\"time\":\"2025-01-01 00:00:00\"}]}}")));
        var credential = GachaLogService.TryParseCredential("https://public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog?authkey=SYNTHETIC&region=cn_gf01&game_biz=hk4e_cn")! with { ExpectedUid = "100000001" };
        await Assert.ThrowsAsync<UserFacingException>(() => new GachaLogService(client).FetchAsync(credential, store.Database));
        Assert.Empty(store.Database.GetRecords("200000001"));
    }
    [Theory]
    [InlineData("x;other=secret")]
    [InlineData("x\r\nAuthorization: secret")]
    public void RejectsCookieHeaderInjection(string value) => Assert.Throws<UserFacingException>(() => new MiyousheSession("123", value));

    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request));
    }
}
