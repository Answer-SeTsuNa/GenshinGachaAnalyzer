using System.Globalization;
using System.Net;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Models;
using Microsoft.Win32;

namespace GenshinGachaAnalyzer.Core.Services;

public sealed record GachaCredential(IReadOnlyDictionary<string, string> Parameters)
{
    public string? ExpectedUid { get; init; }
}

/// <summary>仅 GET 国服官方 API；凭据只存在内存，从不写文件、日志或提示文本。</summary>
public sealed partial class GachaLogService(HttpClient client)
{
    // 对照胡桃 ApiEndpoints.csv 的国服接口；旧 hk4e-api 地址已返回 HTTP 404。
    public const string Endpoint = "https://public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog";
    [GeneratedRegex("https://[^\\x00-\\x20\"'<>\\\\]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
    [GeneratedRegex("[A-Za-z]:[^\\r\\n\"<>|]*?webCaches", RegexOptions.IgnoreCase)]
    private static partial Regex CachePathPattern();

    public static string? DetectGamePath()
    {
        var candidates = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            // FufuLauncher GamePathFinder：HoYoPlay 国服游戏安装项。
            try
            {
                using var hyp = Registry.CurrentUser.OpenSubKey(@"Software\miHoYo\HYP\1_1\hk4e_cn");
                if (hyp?.GetValue("GameInstallPath") is string install) candidates.Add(install);
            }
            catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException)
            { DiagnosticLog.Info("HoYoPlay 安装项不可读：" + error.GetType().Name); }
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            foreach (string keyPath in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\原神", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\原神" })
            {
                try
                {
                    using var key = hive.OpenSubKey(keyPath);
                    if (key?.GetValue("InstallLocation") is string install) candidates.Add(install);
                }
                catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException)
                { DiagnosticLog.Info("旧启动器安装项不可读：" + error.GetType().Name); }
            }
        }
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        foreach (string relative in new[] { @"Program Files\miHoYo\原神", @"Program Files\HoYoPlay\games\Genshin Impact game", @"Program Files\Genshin Impact", @"Games\原神", @"原神", @"miHoYo\原神" })
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, relative));
        foreach (string candidate in candidates)
            foreach (string path in new[] { candidate, Path.Combine(candidate, "Genshin Impact game"), Path.Combine(candidate, "GenshinImpactGame"), Path.Combine(candidate, "games", "Genshin Impact game") })
            if (IsGameDirectory(path)) return path;
        return null;
    }
    public static bool IsGameDirectory(string path) => Directory.Exists(path) &&
        (File.Exists(Path.Combine(path, "YuanShen.exe")) || Directory.Exists(Path.Combine(path, "YuanShen_Data")));

    public async Task<GachaCredential> ExtractCredentialAsync(string gamePath, CancellationToken token = default)
    {
        if (!IsGameDirectory(gamePath)) throw new UserFacingException("未找到国服游戏文件。请手动选择含 YuanShen.exe 的目录，也可以导入 UIGF 文件。");
        DiagnosticLog.Info("开始定位祈愿缓存，游戏目录：" + gamePath);
        var cacheRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(gamePath, "YuanShen_Data", "webCaches") };
        string localLow = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow", "miHoYo", "原神"));
        var logs = new[] { Path.Combine(localLow, "output_log.txt"), Path.Combine(localLow, "Player.log"),
            Path.Combine(gamePath, "YuanShen_Data", "output_log.txt"), Path.Combine(gamePath, "output_log.txt") };
        foreach (var log in logs.Where(File.Exists))
        {
            token.ThrowIfCancellationRequested();
            string text;
            try { text = await ReadSharedTailAsync(log, 8 * 1024 * 1024, token); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { DiagnosticLog.Info("日志不可读，继续直接检测缓存：" + log); continue; }
            foreach (Match match in CachePathPattern().Matches(text.Replace('/', '\\')))
                if (Directory.Exists(match.Value)) cacheRoots.Add(match.Value);
        }
        var files = new List<string>();
        foreach (string root in cacheRoots.Where(Directory.Exists))
        {
            try { files.AddRange(Directory.EnumerateFiles(root, "data_2", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })); }
            catch (UnauthorizedAccessException) { /* 一个旧缓存目录不可读时继续尝试其他目录。 */ }
        }
        DiagnosticLog.Info($"发现 {files.Distinct().Count()} 个 data_2 文件；优先最新缓存版本。");
        foreach (string file in files.Distinct().OrderByDescending(CacheVersion).ThenByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                DiagnosticLog.Info("扫描完整缓存：" + file);
                var credential = await ScanCacheAsync(file, token);
                if (credential is not null) { DiagnosticLog.Info("已找到国服祈愿凭据（URL 不写入日志）。"); return credential; }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { DiagnosticLog.Info("缓存文件读取失败，继续尝试其他文件：" + file + " · " + error.GetType().Name); }
        }
        throw new UserFacingException("未找到可用祈愿链接。请先在国服游戏内打开一次祈愿历史页面，然后点击重试；也可以导入 UIGF 文件。");
    }
    private static Version CacheVersion(string path) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Select(segment => Version.TryParse(segment, out var version) ? version : new Version(0, 0)).Max()!;

    private static async Task<GachaCredential?> ScanCacheAsync(string path, CancellationToken token)
    {
        // 胡桃扫描整个 data_2 并选择最后一条链接。分块实现同一顺序，内存不随缓存体积增长。
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 65536);
        var buffer = new char[1024 * 1024]; string carry = "";
        GachaCredential? latest = null;
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), token);
            string text = carry + new string(buffer, 0, read);
            foreach (Match match in UrlPattern().Matches(text))
            {
                // 块尾链接可能被截断，下个块用重叠区域补全后再解析。
                if (read > 0 && match.Index + match.Length == text.Length) continue;
                if (TryParseCredential(WebUtility.HtmlDecode(match.Value)) is { } credential) latest = credential;
            }
            if (read == 0) return latest;
            carry = text[^Math.Min(text.Length, 65536)..];
        }
    }
    private static async Task<string> ReadSharedTailAsync(string path, int maximum, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
        stream.Seek(Math.Max(0, stream.Length - maximum), SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(token);
    }
    public static GachaCredential? TryParseCredential(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Port != 443 || !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.Host is not ("public-operation-hk4e.mihoyo.com" or "hk4e-api.mihoyo.com" or "webstatic.mihoyo.com")) return null;
        if (uri.Host == "public-operation-hk4e.mihoyo.com" && uri.AbsolutePath != "/gacha_info/api/getGachaLog") return null;
        if (uri.Host == "webstatic.mihoyo.com" && !uri.AbsolutePath.StartsWith("/hk4e/", StringComparison.Ordinal)) return null;
        var allowed = new HashSet<string> { "authkey", "authkey_ver", "sign_type", "region", "game_biz", "auth_appid", "gacha_id" };
        var parameters = new Dictionary<string, string>();
        foreach (string part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = part.Split('=', 2);
            string key = WebUtility.UrlDecode(pair[0]);
            if (pair.Length == 2 && allowed.Contains(key)) parameters[key] = WebUtility.UrlDecode(pair[1]);
        }
        if (!parameters.TryGetValue("authkey", out var authkey) || string.IsNullOrWhiteSpace(authkey) ||
            !parameters.TryGetValue("region", out var region) || region is not ("cn_gf01" or "cn_qd01")) return null;
        if (parameters.TryGetValue("game_biz", out var biz) && biz != "hk4e_cn") return null;
        parameters["game_biz"] = "hk4e_cn";
        parameters.TryAdd("authkey_ver", "1"); parameters.TryAdd("sign_type", "2");
        return new(new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(parameters));
    }
    public static Uri BuildRequestUri(GachaCredential credential, string type, string endId, int page)
    {
        if (!PoolCatalog.IsKnown(type)) throw new ArgumentException("卡池不合法。", nameof(type));
        var parameters = credential.Parameters.ToDictionary();
        parameters["gacha_type"] = type; parameters["size"] = "20"; parameters["end_id"] = endId;
        parameters["page"] = page.ToString(CultureInfo.InvariantCulture); parameters["lang"] = "zh-cn";
        return new Uri(Endpoint + "?" + string.Join("&", parameters.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value))));
    }

    public async Task<FetchResult> FetchAsync(GachaCredential credential, DatabaseService database,
        IProgress<FetchProgress>? progress = null, CancellationToken token = default,
        Func<string, Task>? accountResolved = null)
    {
        int fetched = 0, added = 0, duplicates = 0;
        // 账号以官方响应为准，与更新前下拉框的选中项无关；同一次拉取仍必须属于同一 UID。
        string? sessionUid = null;
        var conflicts = new HashSet<string>();
        var details = new List<RecordConflict>();
        var fetchedRecords = new List<GachaRecord>();
        string? logWarning = null;
        foreach (var pool in PoolCatalog.MainPools)
        {
            string endId = "0";
            for (int page = 1; ; page++)
            {
                token.ThrowIfCancellationRequested();
                progress?.Report(new(pool.Name, page, fetched, added));
                using var document = await RequestPageAsync(BuildRequestUri(credential, pool.Type, endId, page),
                    () => progress?.Report(new(pool.Name + " · 正在重试", page, fetched, added)), token);
                var root = document.RootElement;
                if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
                    !data.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
                    throw new UserFacingException("国服接口返回格式变化，已保存数据保留，请稍后重试或导入 UIGF。");
                if (data.TryGetProperty("region", out var region) && region.GetString() is not ("cn_gf01" or "cn_qd01"))
                    throw new UserFacingException("接口返回了非国服账号，本工具不支持此服务器。");
                if (list.GetArrayLength() == 0) break;
                var records = ParsePage(list);
                string uid = records[0].Uid;
                if (credential.ExpectedUid is { } expectedUid && uid != expectedUid)
                    throw new UserFacingException("官方返回的 UID 与本次选择的账号不一致，已停止获取，该页未写入。");
                if (sessionUid is not null && uid != sessionUid)
                    throw new UserFacingException("本次获取过程中接口返回了另一个 UID，已停止获取。该页未写入，此前已保存的数据保留，请重新更新。");
                sessionUid = uid;
                if (records.Any(r => r.Uid != uid || r.PoolType != pool.Type))
                    throw new UserFacingException("接口返回的账号或卡池不一致，该页未写入。已保存的数据保留。");
                string cursor = records[^1].Id;
                if (cursor == endId || (endId != "0" && BigInteger.Parse(cursor) >= BigInteger.Parse(endId)))
                    throw new UserFacingException("接口翻页游标未前进，已停止拉取，避免无限请求。已保存的数据保留。");
                // 每页完整提交：网络失败时此前已提交的页永久保留；重试从最新页开始并去重。
                var result = await Task.Run(() => database.Insert(records, cancellationToken: token), token);
                // 事务提交后账号才真实存在。只通知一次，让界面切到已有账号或刚创建的账号；
                // 后续网络中断也能继续显示已经提交的目标账号，而不是原来选中的账号。
                if (fetched == 0 && accountResolved is not null) await accountResolved(uid);
                fetched += records.Count; added += result.Added; duplicates += result.Duplicates;
                foreach (var id in result.ConflictIds) conflicts.Add(id);
                details.AddRange(result.Details); fetchedRecords.AddRange(records);
                logWarning ??= result.LogWarning;
                progress?.Report(new(pool.Name, page, fetched, added));
                endId = cursor;
                await Task.Delay(350, token);
            }
        }
        string? logPath = null;
        if (conflicts.Count > 0)
        {
            // 完成后汇总各已提交页面的差异。网络中断时各页日志仍可单独查看。
            var report = new WriteReport(added, duplicates, conflicts.ToArray()) { Details = details };
            var log = await Task.Run(() => ConflictLogService.Write(database.DatabasePath, report, fetchedRecords));
            logPath = log.Path; logWarning ??= log.Warning;
            logWarning ??= ConflictLogService.Finish(log.Path, true);
        }
        return new(fetched, added, duplicates, conflicts.ToArray(), sessionUid) { Details = details, LogPath = logPath, LogWarning = logWarning };
    }
    private async Task<JsonDocument> RequestPageAsync(Uri uri, Action retryProgress, CancellationToken token)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt == 2) throw new UserFacingException("国服服务繁忙。已保存的数据保留，请稍后重试。");
                    retryProgress(); await Task.Delay(1500 * (attempt + 1), token); continue;
                }
                if (!response.IsSuccessStatusCode)
                {
                    int status = (int)response.StatusCode;
                    DiagnosticLog.Info($"国服祈愿接口返回 HTTP {status}");
                    // 404 / 拒绝访问 / 重定向不是超时。永久错误立即结束，不反复请求或回显服务器正文。
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        throw new UserFacingException("国服祈愿接口地址不可用（HTTP 404）。请更新软件后重试；已保存的数据保留。");
                    if (response.StatusCode == HttpStatusCode.RequestTimeout || status >= 500)
                        throw new HttpRequestException($"国服接口 HTTP {status}", null, response.StatusCode);
                    throw new UserFacingException($"国服祈愿接口拒绝请求（HTTP {status}）。请检查网络或代理后重试；已保存的数据保留。");
                }
                // Content 阶段另设超时；ResponseHeadersRead 不会把 HttpClient.Timeout 应用于正文读取。
                using var bodyTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                bodyTimeout.CancelAfter(TimeSpan.FromSeconds(20));
                await using var stream = await response.Content.ReadAsStreamAsync(bodyTimeout.Token);
                var document = await JsonDocument.ParseAsync(stream, cancellationToken: bodyTimeout.Token);
                if (!document.RootElement.TryGetProperty("retcode", out var code) || !code.TryGetInt32(out int retcode))
                { document.Dispose(); throw new UserFacingException("国服接口返回格式变化，已保存数据保留。"); }
                if (retcode == 0) return document;
                DiagnosticLog.Info($"国服祈愿接口返回错误码 {retcode}");
                document.Dispose();
                if (retcode is -100 or -101 or -102 or -104) throw new AuthKeyExpiredException();
                if (retcode == -110 && attempt < 2) { retryProgress(); await Task.Delay(1500 * (attempt + 1), token); continue; }
                throw new UserFacingException($"国服接口暂时不可用（错误码 {retcode}）。已保存数据保留，请重试。");
            }
            catch (Exception error) when (error is HttpRequestException || error is OperationCanceledException && !token.IsCancellationRequested)
            {
                DiagnosticLog.Error($"祈愿接口请求失败，第 {attempt + 1} 次尝试", error);
                if (attempt == 2)
                {
                    string cause = error switch
                    {
                        HttpRequestException { StatusCode: { } status } => $"国服祈愿接口暂时不可用（HTTP {(int)status}）",
                        OperationCanceledException => "连接国服祈愿接口超时",
                        _ => "无法连接国服祈愿接口"
                    };
                    throw new UserFacingException(cause + "。已提交的页面已保存，请检查网络或代理后重试。");
                }
                retryProgress(); await Task.Delay(1000 * (attempt + 1), token);
            }
            catch (JsonException) { throw new UserFacingException("接口未返回有效 JSON，已保存的数据保留，请稍后重试。"); }
        }
        throw new UserFacingException("请求失败，请重试。");
    }
    private static IReadOnlyList<GachaRecord> ParsePage(JsonElement list)
    {
        var result = new List<GachaRecord>();
        try
        {
            foreach (var row in list.EnumerateArray())
            {
                string Required(string key) => row.GetProperty(key).GetString() ?? throw new FormatException();
                string? Optional(string key) => row.TryGetProperty(key, out var value) ? value.GetString() : null;
                var record = new GachaRecord
                {
                    Id = Required("id"), Uid = Required("uid"), GachaType = Required("gacha_type"), ItemId = Required("item_id"),
                    Name = Optional("name"), ItemType = Optional("item_type"), RankType = Optional("rank_type"), Count = Optional("count"),
                    Time = DateTime.SpecifyKind(DateTime.ParseExact(Required("time"), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), DateTimeKind.Unspecified), Source = "游戏获取"
                };
                RecordValidator.Validate(record); result.Add(record);
            }
            if (result.Count > 20 || result.Select(r => r.Id).Distinct().Count() != result.Count) throw new FormatException();
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new UserFacingException("接口返回的抽卡字段不完整或类型不正确，该页未写入，已保存的数据保留。"); }
        return result;
    }
}
