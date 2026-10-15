using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenshinGachaAnalyzer.Core.Helpers;

namespace GenshinGachaAnalyzer.Core.Services;

public sealed record CatalogSnapshot(DateTimeOffset CheckedAt, WishItem[] Items, WishEvent[] Events);

/// <summary>公开资料更新不携带账号凭据。整批验证后原子缓存，失败保留上次可用资料。</summary>
public sealed class WishCatalogUpdateService(HttpClient client, string cachePath)
{
    public const string EventUrl = "https://raw.githubusercontent.com/SnapHutaoRemasteringProject/Snap.Metadata/main/Genshin/CHS/GachaEvent.json";
    public const string OfficialBaseUrl = "https://operation-webstatic.mihoyo.com/gacha_info/hk4e/cn_gf01";
    public const string OfficialListUrl = OfficialBaseUrl + "/gacha/list.json";
    private const int MaxBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public CatalogSnapshot? LoadCache(WishCatalogService catalog)
    {
        try
        {
            if (!File.Exists(cachePath) || new FileInfo(cachePath).Length > MaxBytes) return null;
            var saved = JsonSerializer.Deserialize<CatalogSnapshot>(File.ReadAllText(cachePath), JsonOptions)!;
            Validate(saved);
            return saved.Events.Max(e => e.To) >= catalog.CoveredThrough ? saved : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException) { return null; }
    }
    public async Task<CatalogSnapshot> DownloadAsync(WishCatalogService catalog, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            WishEvent[] events = [];
            using (var historyTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token))
            {
                historyTimeout.CancelAfter(TimeSpan.FromSeconds(8));
                try
                {
                    using var response = await ReadAsync(new(HttpMethod.Get, EventUrl), historyTimeout.Token);
                    events = ParseEvents(response.RootElement);
                    if (events.Length < catalog.Events.Count / 2 || events.Max(e => e.To) < catalog.CoveredThrough)
                        events = [];
                }
                catch (Exception error) when (RecoverableSourceError(error) && !timeout.IsCancellationRequested) { }
            }
            var items = catalog.Items.ToDictionary(i => i.Id);
            foreach (string kind in new[] { "avatar", "weapon" })
                foreach (var item in await ReadItemsAsync(kind, timeout.Token)) items[item.Id] = item;
            // 保留上游尚未包含的已有历史，原始数据库和 UIGF 导出完全不变。
            static string Identity(WishEvent e) => e.Order == 0 ? e.Key : $"{e.Type}:{e.Version}:{e.Order}";
            var previous = catalog.Events.GroupBy(Identity).ToDictionary(g => g.Key, g => g.Last());
            var merged = catalog.Events.Concat(events.Select(e => e.BannerFile is null && previous.TryGetValue(Identity(e), out var old) ? e with { BannerFile = old.BannerFile } : e))
                .GroupBy(Identity).Select(g => g.Last()).Select(e => e with
                { BannerUrl = IsOfficialAsset(e.BannerUrl) ? e.BannerUrl : null, BannerAlternate = IsOfficialAsset(e.BannerAlternate) ? e.BannerAlternate : null }).Reverse().DistinctBy(e => e.Key).Reverse().ToArray();
            bool officialSucceeded = false;
            using (var officialTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token))
            {
                officialTimeout.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    using var list = await ReadAsync(new(HttpMethod.Get, OfficialListUrl), officialTimeout.Token);
                    if (list.RootElement.GetProperty("retcode").GetInt32() != 0) throw new InvalidDataException();
                    var active = new List<WishEvent>();
                    foreach (var entry in list.RootElement.GetProperty("data").GetProperty("list").EnumerateArray())
                    {
                        if (entry.GetProperty("gacha_type").ToString() is not ("301" or "400" or "302" or "500")) continue;
                        string id = entry.GetProperty("gacha_id").GetString()!;
                        if (id.Length is < 1 or > 80 || !id.All(char.IsAsciiHexDigit)) throw new InvalidDataException();
                        using var detail = await ReadAsync(new(HttpMethod.Get, $"{OfficialBaseUrl}/{id}/zh-cn.json"), officialTimeout.Token);
                        active.Add(ParseOfficialEvent(entry, detail.RootElement, items.Values, merged));
                        if (active.Count > 10) throw new InvalidDataException();
                    }
                    if (active.Count == 0) throw new InvalidDataException();
                    Validate(new(DateTimeOffset.UtcNow, items.Values.ToArray(), active.ToArray()));
                    merged = merged.Where(e => !active.Any(a => a.Type == e.Type && a.From < e.To && e.From < a.To))
                        .Concat(active).ToArray();
                    officialSucceeded = true;
                }
                catch (Exception error) when (RecoverableSourceError(error) && !timeout.IsCancellationRequested) { }
            }
            if (!officialSucceeded && events.Length == 0) throw new InvalidDataException("排期资料源均不可用。");
            var snapshot = new CatalogSnapshot(DateTimeOffset.UtcNow, items.Values.ToArray(), merged);
            Validate(snapshot);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cachePath))!);
            string temporary = cachePath + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(snapshot), timeout.Token);
                File.Move(temporary, cachePath, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return snapshot;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new UserFacingException("卡池资料更新超时，继续使用本地资料。可稍后重试。"); }
        catch (Exception error) when (error is HttpRequestException or JsonException or InvalidDataException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        { throw new UserFacingException("暂时无法更新卡池资料，继续使用本地资料。请稍后重试。"); }
    }
    private static bool RecoverableSourceError(Exception error) => error is HttpRequestException or JsonException or InvalidDataException
        or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or OperationCanceledException;

    public static WishEvent ParseOfficialEvent(JsonElement entry, JsonElement detail, IEnumerable<WishItem> items, IEnumerable<WishEvent> previous)
    {
        string type = entry.GetProperty("gacha_type").ToString();
        if (detail.GetProperty("gacha_type").ToString() != type) throw new InvalidDataException();
        DateTimeOffset Time(string field) => new(DateTime.ParseExact(entry.GetProperty(field).GetString()!, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), TimeSpan.FromHours(8));
        var from = Time("begin_time"); var to = Time("end_time");
        // 官方详情 item_id 可能是负数占位符，按官方名称、类型和星级匹配真实物品 ID。
        string[] Up(string field, string rank) => detail.GetProperty(field).EnumerateArray()
            .Where(i => i.GetProperty("is_up").ToString() == "1").Select(i =>
            {
                var matches = items.Where(item => item.Name == i.GetProperty("item_name").GetString()
                    && item.Kind == i.GetProperty("item_type").GetString() && item.Rank == rank).ToArray();
                return matches.Length == 1 ? matches[0].Id : throw new InvalidDataException("官方 UP 物品无法唯一匹配。");
            }).Distinct().ToArray();
        var gold = Up("r5_prob_list", "5"); var purple = Up("r4_prob_list", "4");
        if (type != "500" && gold.Length == 0) throw new InvalidDataException();
        var known = previous.FirstOrDefault(e => e.Type == type && (e.From - from).Duration() < TimeSpan.FromMinutes(15)
            && (e.To - to).Duration() < TimeSpan.FromMinutes(15));
        string name = System.Text.RegularExpressions.Regex.Replace(detail.GetProperty("title").GetString()!, "<[^>]+>", "");
        return new(name, known?.Version ?? "官方排期", known?.Order ?? 0, type, from, to, gold, purple,
            known?.BannerFile, known?.BannerUrl, known?.BannerAlternate);
    }
    public static WishEvent[] ParseEvents(JsonElement root)
    {
        var events = new List<WishEvent>();
        foreach (var e in root.EnumerateArray())
        {
            string type = e.GetProperty("Type").ToString();
            if (type is not ("301" or "400" or "302" or "500")) continue;
            string? banner = e.TryGetProperty("Banner", out var b) ? b.GetString() : null;
            string? alternate = e.TryGetProperty("Banner2", out var a) ? a.GetString() : null;
            string? file = IsOfficialAsset(banner) ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(banner!)))[..20].ToLowerInvariant() + (new Uri(banner!).AbsolutePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg") : null;
            events.Add(new(e.GetProperty("Name").GetString()!, e.GetProperty("Version").GetString()!, e.GetProperty("Order").GetInt32(), type,
                e.GetProperty("From").GetDateTimeOffset(), e.GetProperty("To").GetDateTimeOffset(),
                e.GetProperty("UpOrangeList").EnumerateArray().Select(i => i.ToString()).ToArray(), e.GetProperty("UpPurpleList").EnumerateArray().Select(i => i.ToString()).ToArray(),
                file, IsOfficialAsset(banner) ? banner : null, IsOfficialAsset(alternate) ? alternate : null));
        }
        return events.ToArray();
    }
    private async Task<IReadOnlyList<WishItem>> ReadItemsAsync(string kind, CancellationToken token)
    {
        var result = new Dictionary<string, WishItem>();
        int received = 0;
        for (int page = 1; page <= 20; page++)
        {
            var body = kind == "avatar" ? JsonSerializer.Serialize(new { page, size = 200, is_all = true }) : JsonSerializer.Serialize(new { page, size = 200, weapon_levels = new[] { 1, 2, 3, 4, 5 } });
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api-takumi.mihoyo.com/event/e20200928calculate/v1/{kind}/list") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Referrer = new Uri("https://webstatic.mihoyo.com/");
            using var doc = await ReadAsync(request, token);
            if (doc.RootElement.GetProperty("retcode").GetInt32() != 0) throw new InvalidDataException();
            var data = doc.RootElement.GetProperty("data"); var list = data.GetProperty("list");
            int before = result.Count;
            foreach (var item in list.EnumerateArray())
            {
                if (kind == "avatar" && item.GetProperty("name").GetString() == "旅行者") continue;
                string id = item.GetProperty("id").ToString(), icon = item.GetProperty("icon").GetString()!;
                if (!IsOfficialAsset(icon)) throw new InvalidDataException();
                result.TryAdd(id, new(id, item.GetProperty("name").GetString()!, kind == "avatar" ? "角色" : "武器", item.GetProperty(kind == "avatar" ? "avatar_level" : "weapon_level").ToString(), icon));
            }
            received += list.GetArrayLength();
            if (list.GetArrayLength() < 200 || data.TryGetProperty("total", out var total) && received >= int.Parse(total.ToString(), CultureInfo.InvariantCulture)) break;
            if (before == result.Count || page == 20) throw new InvalidDataException();
        }
        if (result.Count == 0) throw new InvalidDataException();
        return result.Values.ToArray();
    }
    private async Task<JsonDocument> ReadAsync(HttpRequestMessage request, CancellationToken token)
    {
        using (request)
        {
            request.Headers.UserAgent.ParseAdd("GenshinGachaAnalyzer-Catalog/1.0");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and < 400) throw new InvalidDataException();
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxBytes) throw new InvalidDataException();
            await using var input = await response.Content.ReadAsStreamAsync(token);
            using var output = new MemoryStream(); byte[] buffer = new byte[16384];
            int count;
            while ((count = await input.ReadAsync(buffer, token)) > 0)
            { if (output.Length + count > MaxBytes) throw new InvalidDataException(); output.Write(buffer, 0, count); }
            return JsonDocument.Parse(output.ToArray());
        }
    }
    public static bool IsOfficialAsset(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.IsDefaultPort
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && new[] { "mihoyo.com", "miyoushe.com", "hoyoverse.com" }.Any(host => uri.Host == host || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));
    public static void Validate(CatalogSnapshot? snapshot)
    {
        static bool Id(string? id) => id is { Length: > 0 and <= 20 } && id.All(char.IsAsciiDigit);
        static bool Text(string? text) => text is { Length: > 0 and <= 200 } && !text.Any(char.IsControl);
        if (snapshot is null || snapshot.CheckedAt > DateTimeOffset.UtcNow.AddDays(1) || snapshot.Items is not { Length: > 0 and <= 10000 }
            || snapshot.Events is not { Length: > 0 and <= 10000 }
            || snapshot.Items.Any(i => i is null || !Id(i.Id) || !Text(i.Name) || i.Kind is not ("角色" or "武器") || i.Rank is not ("1" or "2" or "3" or "4" or "5")
                || i.Icon is null || i.Icon.Length > 2048 || i.Icon.Contains("://") && !IsOfficialAsset(i.Icon))
            || snapshot.Items.DistinctBy(i => i.Id).Count() != snapshot.Items.Length
            || snapshot.Events.Any(e => e is null || !Text(e.Name) || !Text(e.Version) || e.Type is not ("301" or "400" or "302" or "500") || e.Order is < 0 or > 10 || e.Order == 0 && e.Version != "官方排期"
                || e.From.Year < 2020 || e.To.Year > DateTime.UtcNow.Year + 2 || e.From >= e.To || (e.To - e.From).TotalDays > 100
                || e.UpGold is null || e.UpPurple is null || e.UpGold.Length > 100 || e.UpPurple.Length > 100 || e.UpGold.Concat(e.UpPurple).Any(i => !Id(i))
                || e.BannerFile is not null && (e.BannerFile != Path.GetFileName(e.BannerFile) || e.BannerFile.Contains('/') || e.BannerFile.Contains('\\') || e.BannerFile.Length > 100)
                || e.BannerUrl is not null && !IsOfficialAsset(e.BannerUrl) || e.BannerAlternate is not null && !IsOfficialAsset(e.BannerAlternate))
            || snapshot.Events.DistinctBy(e => e.Key).Count() != snapshot.Events.Length
            || snapshot.Events.Where(e => e.Order != 0).DistinctBy(e => (e.Type, e.Version, e.Order)).Count() != snapshot.Events.Count(e => e.Order != 0))
            throw new InvalidDataException("卡池资料格式异常。");
    }
}
