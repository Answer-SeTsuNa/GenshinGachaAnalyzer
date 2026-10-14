using System.Net;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Services;
using Xunit;

namespace GenshinGachaAnalyzer.Tests;

public sealed class ReleaseUpdateTests
{
    private const string Repository = "https://github.com/Answer-SeTsuNa/GenshinGachaAnalyzer";
    [Theory]
    [InlineData("v0.1.20", "0.1.19.0", true)]
    [InlineData("0.1.19", "0.1.19.0", false)]
    [InlineData("v0.1.9", "0.1.19", false)]
    [InlineData("v0.1.100", "0.1.99", true)]
    public async Task ChecksFormalReleaseAndComparesNumericVersions(string tag, string current, bool newer)
    {
        using var client = new HttpClient(new Handler(request =>
        {
            Assert.Equal("https://api.github.com/repos/Answer-SeTsuNa/GenshinGachaAnalyzer/releases/latest", request.RequestUri!.AbsoluteUri);
            Assert.NotEmpty(request.Headers.UserAgent);
            Assert.Null(request.Headers.Authorization);
            Assert.Null(request.Content);
            return new(HttpStatusCode.OK) { Content = new StringContent($$"""{"tag_name":"{{tag}}","draft":false,"prerelease":false,"html_url":"https://untrusted.example/"}""") };
        }));
        var release = await new ReleaseUpdateService(client).CheckAsync(Repository);
        Assert.Equal(newer, release.IsNewerThan(Version.Parse(current)));
        Assert.Equal(Repository + "/releases/tag/" + tag, release.PageUrl);
    }
    [Theory]
    [InlineData(404, "未找到")]
    [InlineData(403, "限制访问")]
    [InlineData(429, "限制访问")]
    [InlineData(502, "无法连接")]
    public async Task HttpFailureIsNotReportedAsUpToDate(int status, string message)
    {
        using var client = new HttpClient(new Handler(_ => new((HttpStatusCode)status)));
        var error = await Assert.ThrowsAsync<UserFacingException>(() => new ReleaseUpdateService(client).CheckAsync(Repository));
        Assert.Contains(message, error.Message);
    }
    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"tag_name\":\"v0.1.20-beta\",\"draft\":false,\"prerelease\":true}")]
    [InlineData("{\"tag_name\":\"nonsense\",\"draft\":false,\"prerelease\":false}")]
    public async Task InvalidOrPreviewResponseDoesNotOfferUpdate(string content)
    {
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(content) }));
        await Assert.ThrowsAsync<UserFacingException>(() => new ReleaseUpdateService(client).CheckAsync(Repository));
    }
    [Theory]
    [InlineData("http://github.com/owner/repo")]
    [InlineData("https://other.example/owner/repo")]
    [InlineData("https://github.com/owner/repo?token=secret")]
    public void RejectsUnexpectedRepositoryAddress(string url) => Assert.Throws<UserFacingException>(() => ReleaseUpdateService.ReleasesUrl(url));

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
