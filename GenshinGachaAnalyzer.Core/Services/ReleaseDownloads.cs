using System.Text.Json;
using System.Text.RegularExpressions;
using GenshinGachaAnalyzer.Core.Helpers;

namespace GenshinGachaAnalyzer.Core.Services;

public sealed record ReleaseDownloadLink(string Name, string Url);
public sealed record ReleaseDownload(string Label, string FileName, string Url)
{
    public string Description { get; init; } = "";
    public IReadOnlyList<ReleaseDownloadLink> Links => ReleaseDownloads.Links(Url);
}

public static class ReleaseDownloads
{
    private sealed record Proxy(string Name, string Prefix);
    private static readonly Proxy[] Proxies = LoadProxies();

    public static IReadOnlyList<ReleaseDownload> ForTag(string repositoryUrl, string tag)
    {
        string repository = ReleaseUpdateService.ReleasesUrl(repositoryUrl)[..^9];
        if (!Regex.IsMatch(tag, @"^[vV]?\d+\.\d+\.\d+$", RegexOptions.CultureInvariant))
            throw new UserFacingException("发布版本号无法识别。");
        string version = tag.TrimStart('v', 'V');
        string assetRoot = repository + "/releases/download/" + tag + "/";
        string installer = $"GenshinGachaAnalyzer-Setup-v{version}-win-x64.exe";
        string portable = $"GenshinGachaAnalyzer-v{version}-win-x64.zip";
        return [
            new("安装版 · 推荐新手", installer, assetRoot + installer)
            { Description = "下载后双击安装文件，按提示点击「下一步」。安装后从桌面或开始菜单打开。" },
            new("便携版 · 解压后使用", portable, assetRoot + portable)
            { Description = "下载后右键压缩包，选择「全部解压」，再打开文件夹中的 GenshinGachaAnalyzer.exe。请保留整个文件夹。" }
        ];
    }

    public static IReadOnlyList<ReleaseDownloadLink> Links(string assetUrl)
    {
        // Only public, credential-free GitHub release assets can be passed to a proxy.
        if (!Uri.TryCreate(assetUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com"
            || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || !Regex.IsMatch(uri.AbsolutePath, @"^/[^/]+/[^/]+/releases/download/[vV]?\d+\.\d+\.\d+/GenshinGachaAnalyzer(?:-Setup)?-v\d+\.\d+\.\d+-win-x64\.(exe|zip)$"))
            throw new UserFacingException("下载地址不是公开的 GitHub 发布文件。");
        return Proxies.Select(proxy => new ReleaseDownloadLink(proxy.Name + " ↗", proxy.Prefix + assetUrl))
            .Append(new ReleaseDownloadLink("GitHub 直连 ↗", assetUrl)).ToArray();
    }

    private static Proxy[] LoadProxies()
    {
        using var stream = typeof(ReleaseDownloads).Assembly.GetManifestResourceStream("GenshinGachaAnalyzer.Core.Resources.download-proxies.json")!;
        return JsonSerializer.Deserialize<Proxy[]>(stream)!;
    }
}
