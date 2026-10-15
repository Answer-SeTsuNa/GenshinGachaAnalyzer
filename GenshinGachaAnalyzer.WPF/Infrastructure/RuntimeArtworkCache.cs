using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using GenshinGachaAnalyzer.Core.Services;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

/// <summary>新资料图片按需获取，保留官方出处与 SHA256；不占用安装目录，不接触账号凭据。</summary>
internal static class RuntimeArtworkCache
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly ConcurrentDictionary<string, string[]> Sources = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();
    private static string? _root;
    private static bool _online;
    public static void Configure(string dataDirectory, bool online) { _root = Path.Combine(dataDirectory, "catalog", "images"); _online = online; }
    public static void Register(WishCatalogService catalog)
    {
        foreach (var item in catalog.Items.Where(i => WishCatalogUpdateService.IsOfficialAsset(i.Icon))) Sources["Items/" + item.Id + ".png"] = [item.Icon];
        foreach (var e in catalog.Events.Where(e => e.BannerFile is not null))
            Sources["Banners/" + e.BannerFile] = new[] { e.BannerUrl, e.BannerAlternate }.Where(WishCatalogUpdateService.IsOfficialAsset).Cast<string>().Distinct().ToArray();
    }
    public static async Task<string?> ResolveAsync(string group, string file)
    {
        if (_root is null || group is not ("Items" or "Banners") || string.IsNullOrWhiteSpace(file) || file != Path.GetFileName(file) || file.Contains('/') || file.Contains('\\')) return null;
        string installed = Path.Combine(AppContext.BaseDirectory, "Resources", group, file);
        if (File.Exists(installed)) return installed;
        string path = Path.Combine(_root, group, file), key = group + "/" + file;
        if (File.Exists(path)) return path;
        if (!_online || !Sources.TryGetValue(key, out var urls)) return null;
        var gate = Locks.GetOrAdd(key, _ => new(1)); await gate.WaitAsync();
        try
        {
            if (File.Exists(path)) return path;
            foreach (string url in urls)
            {
                if (!WishCatalogUpdateService.IsOfficialAsset(url)) continue;
                string temporary = path + ".partial";
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 10 * 1024 * 1024) continue;
                    await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
                    using var memory = new MemoryStream(); byte[] buffer = new byte[16384]; int count;
                    while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
                    { if (memory.Length + count > 10 * 1024 * 1024) throw new InvalidDataException(); memory.Write(buffer, 0, count); }
                    byte[] bytes = memory.ToArray();
                    if (bytes.Length < 32 || !(bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255)) continue;
                    string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                    if (hash == "ae5d4d9be2e3ee89c0fc94a326aaefd8412c55d916bef6f7d28c88ebf771af7b") continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllBytesAsync(temporary, bytes);
                    await File.WriteAllTextAsync(path + ".source.json", JsonSerializer.Serialize(new { Url = url, Sha256 = hash }));
                    File.Move(temporary, path, true); return path;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException) { }
                finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } }
            }
            return null;
        }
        finally { gate.Release(); }
    }
}
