using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenshinGachaAnalyzer.Core.Helpers;

namespace GenshinGachaAnalyzer.Core.Services;

// 登录链路参考 Snap Hutao Remastered（MIT），仅保留官方认证和祈愿查询。
public sealed class MiyousheSession
{
    internal string Aid { get; }
    internal string Token { get; }
    internal string Mid { get; }
    public MiyousheSession(string aid, string token, string mid = "")
    {
        if (string.IsNullOrEmpty(aid) || aid.Length > 20 || !aid.All(char.IsAsciiDigit)
            || string.IsNullOrWhiteSpace(token) || token.Length > 4096 || mid.Length > 128
            || (token + mid).Any(c => c is ';' or '\r' or '\n' || char.IsControl(c)))
            throw new UserFacingException("官方登录信息不完整，请重新登录。");
        Aid = aid; Token = token; Mid = mid;
    }
    internal string Cookie => $"stuid={Aid};stoken={Token}" + (Mid.Length > 0 ? $";mid={Mid}" : "");
    public override string ToString() => "米游社临时登录会话（凭据已隐藏）";
}

public sealed class MiyousheQrCode(string url, string ticket)
{
    public string Url { get; } = url;
    internal string Ticket { get; } = ticket;
    public override string ToString() => "米游社登录二维码（内容已隐藏）";
}
public sealed record MiyousheGameRole(string Uid, string Region, string Nickname, string RegionName)
{
    public string Display => $"{Nickname} · UID {Uid} · {RegionName}";
}
public sealed record MiyousheLoginProgress(string Status, MiyousheSession? Session);

public sealed class MiyousheLoginService(HttpClient client)
{
    public const string LoginPage = "https://user.mihoyo.com/login-platform/index.html?app_id=bll8iq97cem8&theme=passport&token_type=1&game_biz=bbs_cn&ux_mode=popup&iframe_level=1#/login";
    private const string Passport = "https://passport-api.mihoyo.com/account/ma-cn-passport/app/";
    private readonly string _device = Convert.ToHexString(RandomNumberGenerator.GetBytes(27)).ToLowerInvariant()[..53];
    public static bool IsOfficialPage(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && (uri.Host == "mihoyo.com" || uri.Host.EndsWith(".mihoyo.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host == "miyoushe.com" || uri.Host.EndsWith(".miyoushe.com", StringComparison.OrdinalIgnoreCase));
    public static bool IsLoginResponse(Uri uri) => IsOfficialPage(uri) && uri.Host == "passport-api.mihoyo.com"
        && uri.AbsolutePath is "/account/ma-cn-passport/web/loginByPassword" or "/account/ma-cn-passport/web/loginByMobileCaptcha" or "/account/ma-cn-passport/web/queryQRLoginStatus";

    public async Task<MiyousheQrCode> CreateQrAsync(CancellationToken token)
    {
        using var doc = await SendAsync(Passport + "createQRLogin", "{}", null, true, token);
        var data = Data(doc);
        string url = data.GetProperty("url").GetString() ?? "", ticket = data.GetProperty("ticket").GetString() ?? "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsOfficialPage(uri) || ticket.Length is 0 or > 1024)
            throw new UserFacingException("官方未返回有效登录二维码，请稍后刷新。");
        return new(url, ticket);
    }
    public async Task<MiyousheLoginProgress> PollQrAsync(MiyousheQrCode qr, CancellationToken token)
    {
        using var doc = await SendAsync(Passport + "queryQRLoginStatus", JsonSerializer.Serialize(new { ticket = qr.Ticket }), null, true, token);
        int code = doc.RootElement.GetProperty("retcode").GetInt32();
        if (code == -3501) return new("Expired", null);
        var data = Data(doc); string status = data.GetProperty("status").GetString() ?? "";
        return status == "Confirmed" ? new(status, ParseSession(data)) : new(status, null);
    }
    public static MiyousheSession? ParseWebLogin(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetProperty("retcode").GetInt32() != 0) return null;
            return ParseSession(doc.RootElement.GetProperty("data"));
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or UserFacingException) { return null; }
    }
    private static MiyousheSession ParseSession(JsonElement data)
    {
        string? value = null;
        if (data.TryGetProperty("tokens", out var tokens))
            foreach (var item in tokens.EnumerateArray())
                if (item.GetProperty("token_type").GetInt32() == 1) value = item.GetProperty("token").GetString();
        if (data.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.Object && token.GetProperty("token_type").GetInt32() == 1)
            value = token.GetProperty("token").GetString();
        var user = data.GetProperty("user_info");
        return new(user.GetProperty("aid").GetString() ?? "", value ?? "", user.TryGetProperty("mid", out var mid) ? mid.GetString() ?? "" : "");
    }
    public async Task<MiyousheSession?> FromLoginCookiesAsync(IReadOnlyDictionary<string, string> cookies, CancellationToken token)
    {
        string? Aid() => new[] { "stuid", "stuid_v2", "login_uid", "ltuid", "ltuid_v2", "account_id", "account_id_v2" }.Select(k => cookies.GetValueOrDefault(k)).FirstOrDefault(v => !string.IsNullOrEmpty(v));
        string? aid = Aid(), stoken = cookies.GetValueOrDefault("stoken") ?? cookies.GetValueOrDefault("stoken_v2");
        if (aid is not null && stoken is not null) return new(aid, stoken, cookies.GetValueOrDefault("mid") ?? "");
        if (aid is null || !aid.All(char.IsAsciiDigit) || !cookies.TryGetValue("login_ticket", out var ticket)) return null;
        using var doc = await SendAsync("https://api-takumi.mihoyo.com/auth/api/getMultiTokenByLoginTicket?login_ticket=" + Uri.EscapeDataString(ticket) + "&uid=" + Uri.EscapeDataString(aid) + "&token_types=3", null, null, false, token);
        foreach (var item in Data(doc).GetProperty("list").EnumerateArray())
            if (item.GetProperty("name").GetString() == "stoken") return new(aid, item.GetProperty("token").GetString() ?? "");
        return null;
    }
    public async Task<IReadOnlyList<MiyousheGameRole>> GetRolesAsync(MiyousheSession session, CancellationToken token)
    {
        using var doc = await SendAsync("https://api-takumi.miyoushe.com/binding/api/getUserGameRolesByStoken?game_biz=hk4e_cn", null, session, false, token);
        var result = new List<MiyousheGameRole>();
        foreach (var role in Data(doc).GetProperty("list").EnumerateArray())
        {
            string uid = role.GetProperty("game_uid").GetString() ?? "", region = role.GetProperty("region").GetString() ?? "";
            if (region is not ("cn_gf01" or "cn_qd01") || uid.Length != 9 || !uid.All(char.IsAsciiDigit)) continue;
            result.Add(new(uid, region, role.GetProperty("nickname").GetString() ?? "原神账号", role.GetProperty("region_name").GetString() ?? region));
        }
        if (result.Count == 0) throw new UserFacingException("该米游社账号未返回可用的国服原神角色，请确认登录账号。");
        return result.DistinctBy(r => r.Uid).ToArray();
    }
    public async Task<GachaCredential> GetWishCredentialAsync(MiyousheSession session, MiyousheGameRole role, CancellationToken token)
    {
        if (role.Uid.Length != 9 || !role.Uid.All(char.IsAsciiDigit) || role.Region is not ("cn_gf01" or "cn_qd01"))
            throw new UserFacingException("原神账号信息无效，请重新选择。");
        string body = JsonSerializer.Serialize(new { auth_appid = "webview_gacha", game_biz = "hk4e_cn", game_uid = long.Parse(role.Uid, System.Globalization.CultureInfo.InvariantCulture), region = role.Region });
        using var doc = await SendAsync("https://api-takumi.mihoyo.com/binding/api/genAuthKey", body, session, false, token);
        string key = Data(doc).GetProperty("authkey").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(key)) throw new UserFacingException("官方未返回祈愿查询授权，请重新登录。");
        return new(new Dictionary<string, string> { ["authkey"] = key, ["authkey_ver"] = "1", ["sign_type"] = "2", ["region"] = role.Region, ["game_biz"] = "hk4e_cn", ["auth_appid"] = "webview_gacha" }) { ExpectedUid = role.Uid };
    }
    private static JsonElement Data(JsonDocument doc)
    {
        int code = doc.RootElement.GetProperty("retcode").GetInt32();
        if (code != 0) throw new UserFacingException($"官方登录服务未完成操作（代码 {code}），请重新登录或稍后重试。");
        return doc.RootElement.GetProperty("data");
    }
    private async Task<JsonDocument> SendAsync(string url, string? body, MiyousheSession? session, bool qr, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, url);
            if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("User-Agent", qr ? "HYPContainer/1.1.4.133" : "Mozilla/5.0 miHoYoBBS/2.95.1");
            request.Headers.TryAddWithoutValidation("x-rpc-app_id", "ddxf5dufpuyo");
            request.Headers.TryAddWithoutValidation("x-rpc-client_type", qr ? "3" : "5");
            request.Headers.TryAddWithoutValidation("x-rpc-device_id", _device);
            request.Headers.TryAddWithoutValidation("x-rpc-app_version", "2.95.1");
            request.Headers.Referrer = new Uri("https://app.mihoyo.com/");
            if (session is not null)
            {
                request.Headers.TryAddWithoutValidation("Cookie", session.Cookie);
                string t = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
                string r = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();
                string hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes($"salt=sidQFEglajEz7FA0Aj7HQPV88zpf17SO&t={t}&r={r}"))).ToLowerInvariant();
                request.Headers.TryAddWithoutValidation("DS", $"{t},{r},{hash}");
            }
            using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode is >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest)
                throw new UserFacingException("官方登录服务发生异常跳转，本次未继续发送凭据。");
            response.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new UserFacingException("连接官方登录服务超时，请稍后重试。"); }
        catch (HttpRequestException) { throw new UserFacingException("无法连接官方登录服务，请检查网络后重试。"); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException) { throw new UserFacingException("官方登录返回格式异常，请稍后重试。"); }
    }
}
