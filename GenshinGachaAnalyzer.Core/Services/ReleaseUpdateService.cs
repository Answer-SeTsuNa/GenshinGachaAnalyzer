using System.Net;
using System.Text.Json;
using GenshinGachaAnalyzer.Core.Helpers;

namespace GenshinGachaAnalyzer.Core.Services;

public sealed record ReleaseUpdate(Version Version, string PageUrl)
{
    public bool IsNewerThan(Version current) => Normalize(Version) > Normalize(current);
    private static Version Normalize(Version value) => new(value.Major, value.Minor, Math.Max(0, value.Build), Math.Max(0, value.Revision));
}

/// <summary>主动查询 GitHub 正式发布；不携带账号、游戏凭据或本地档案。</summary>
public sealed class ReleaseUpdateService(HttpClient client)
{
    public static string ReleasesUrl(string repositoryUrl) => Repository(repositoryUrl).AbsoluteUri.TrimEnd('/') + "/releases";

    public async Task<ReleaseUpdate> CheckAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var repository = Repository(repositoryUrl);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos{repository.AbsolutePath}/releases/latest");
            request.Headers.UserAgent.ParseAdd("GenshinGachaAnalyzer/" + typeof(ReleaseUpdateService).Assembly.GetName().Version!.ToString(3));
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new UserFacingException("未找到可公开读取的正式版本，可能尚未发布或仓库为私有。可打开 GitHub 发布页查看。");
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                throw new UserFacingException("GitHub 暂时限制访问，请稍后重试，或打开 GitHub 发布页查看。");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
            var root = json.RootElement;
            string? tag = root.GetProperty("tag_name").GetString();
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()
                || !Version.TryParse(tag?.Trim().TrimStart('v', 'V'), out var version))
                throw new UserFacingException("GitHub 返回的正式版本号无法识别，请打开发布页查看。");
            return new(version, repository.AbsoluteUri.TrimEnd('/') + "/releases/tag/" + Uri.EscapeDataString(tag!));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new UserFacingException("检查更新超时，请检查网络后重试，或打开 GitHub 发布页查看。"); }
        catch (HttpRequestException)
        { throw new UserFacingException("暂时无法连接 GitHub，请检查网络后重试，或打开 GitHub 发布页查看。"); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new UserFacingException("GitHub 更新信息格式异常，请打开发布页查看。"); }
    }

    private static Uri Repository(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com"
            || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.AbsolutePath.Trim('/').Split('/') is not [var owner, var repo] || owner.Length == 0 || repo.Length == 0)
            throw new UserFacingException("尚未配置有效的 GitHub 项目地址。");
        return new Uri(uri.GetLeftPart(UriPartial.Path).TrimEnd('/'));
    }
}
