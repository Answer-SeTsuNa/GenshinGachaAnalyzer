using System.Windows;
using System.Windows.Media.Imaging;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using QRCoder;

namespace GenshinGachaAnalyzer.WPF.Views;

public partial class MiyousheLoginWindow : Window
{
    private readonly MiyousheLoginService _service;
    private CancellationTokenSource? _operation;
    private MiyousheSession? _session;
    private WebView2? _browser;
    private bool _closed, _processingWeb, _closing;
    private readonly string _cacheRoot;
    private string? _browserDirectory;
    private TaskCompletionSource<bool>? _accountNavigation;
    public GachaCredential? Credential { get; private set; }
    internal Task OperationReady { get; private set; } = Task.CompletedTask;
    internal bool HasLoginSession => _session is not null;

    public MiyousheLoginWindow(MiyousheLoginService service, string? cacheRoot = null)
    {
        InitializeComponent(); _service = service;
        _cacheRoot = Path.GetFullPath(cacheRoot ?? Path.Combine(Path.GetTempPath(), "GenshinGachaAnalyzer-login"));
        Closing += async (_, e) =>
        {
            if (_closed) return;
            e.Cancel = true;
            if (_closing) return;
            _closing = true; _operation?.Cancel(); _session = null;
            await DisposeBrowserAsync(); _closed = true;
            _ = Dispatcher.BeginInvoke(new Action(Close)); // 等本次 Closing 返回，避免在关闭事件内重入 Close。
        };
        Closed += (_, _) => { _operation?.Dispose(); _operation = null; _session = null; };
    }
    private void ConsentClick(object sender, RoutedEventArgs e) { ConsentPanel.Visibility = Visibility.Collapsed; OperationReady = StartQrAsync(); }
    private void QrClick(object sender, RoutedEventArgs e) => OperationReady = StartQrAsync();
    private void AccountClick(object sender, RoutedEventArgs e) => OperationReady = StartAccountAsync();
    private void CancelClick(object sender, RoutedEventArgs e) { Credential = null; Close(); }
    private CancellationTokenSource BeginOperation()
    {
        _operation?.Cancel(); _operation?.Dispose(); _operation = new(); _session = null;
        RolePanel.Visibility = Visibility.Collapsed; RolePicker.ItemsSource = null;
        LoginPanel.Visibility = Visibility.Visible; FetchButton.IsEnabled = true;
        return _operation;
    }
    private async Task StartQrAsync()
    {
        var operation = BeginOperation();
        BrowserPanel.Visibility = Visibility.Collapsed; QrPanel.Visibility = Visibility.Visible; QrImage.Source = null;
        StatusText.Text = "正在连接米哈游官方服务，生成登录二维码…";
        try
        {
            await DisposeBrowserAsync();
            operation.Token.ThrowIfCancellationRequested();
            var qr = await _service.CreateQrAsync(operation.Token);
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(qr.Url, QRCodeGenerator.ECCLevel.M);
            using var image = new PngByteQRCode(data);
            using var stream = new MemoryStream(image.GetGraphic(8));
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); QrImage.Source = bitmap;
            StatusText.Text = "等待扫码，请在米游社 App 中确认登录。";
            using var expiry = CancellationTokenSource.CreateLinkedTokenSource(operation.Token); expiry.CancelAfter(TimeSpan.FromMinutes(3));
            while (!expiry.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), expiry.Token);
                var progress = await _service.PollQrAsync(qr, expiry.Token);
                if (progress.Session is { } session) { await ShowRolesAsync(session, operation.Token); return; }
                if (progress.Status == "Expired") { StatusText.Text = "二维码已过期，请刷新二维码。"; return; }
                StatusText.Text = progress.Status == "Scanned" ? "已扫码，请在米游社 App 中确认。" : "等待扫码，请在米游社 App 中确认登录。";
            }
        }
        catch (OperationCanceledException) { if (ReferenceEquals(_operation, operation) && !operation.IsCancellationRequested && !_closed) StatusText.Text = "二维码已过期，请刷新二维码。"; }
        catch (Exception error) { ShowSafeError(error, operation); }
    }
    private async Task StartAccountAsync()
    {
        var operation = BeginOperation(); QrPanel.Visibility = Visibility.Collapsed; BrowserPanel.Visibility = Visibility.Visible;
        StatusText.Text = "正在打开米哈游官方账号登录页…";
        try
        {
            await DisposeBrowserAsync(); operation.Token.ThrowIfCancellationRequested();
            _browserDirectory = Path.Combine(_cacheRoot, Guid.NewGuid().ToString("N"));
            var browser = new WebView2 { CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = _browserDirectory, IsInPrivateModeEnabled = true,
                AdditionalBrowserArguments = System.Windows.Media.RenderOptions.ProcessRenderMode == System.Windows.Interop.RenderMode.SoftwareOnly ? "--disable-gpu" : null } };
            _browser = browser; BrowserPanel.Children.Add(browser);
            await browser.EnsureCoreWebView2Async(); operation.Token.ThrowIfCancellationRequested();
            var core = browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false; core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreHostObjectsAllowed = false; core.Settings.IsWebMessageEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false; core.Settings.IsGeneralAutofillEnabled = false;
            core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (e.IsUserInitiated && Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && MiyousheLoginService.IsOfficialPage(uri))
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
                    catch (Exception) { StatusText.Text = "未能打开官方说明页面，请稍后重试。"; }
            };
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.NavigationStarting += (_, e) =>
            {
                if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || !MiyousheLoginService.IsOfficialPage(uri))
                { e.Cancel = true; StatusText.Text = "已阻止离开官方登录页面。"; }
            };
            core.WebResourceResponseReceived += OnLoginResponse;
            _accountNavigation = new(TaskCreationOptions.RunContinuationsAsynchronously);
            core.NavigationCompleted += (_, e) => _accountNavigation?.TrySetResult(e.IsSuccess && e.HttpStatusCode < 400);
            core.Navigate(MiyousheLoginService.LoginPage);
            StatusText.Text = "请在米哈游官方页面登录。密码由官方页面处理，本工具不读取密码。";
        }
        catch (OperationCanceledException) { }
        catch (WebView2RuntimeNotFoundException) { if (!_closed) StatusText.Text = "账号登录需要 Microsoft Edge WebView2 运行时；当前可直接使用扫码登录。"; }
        catch (Exception error) { ShowSafeError(error, operation); }
    }
    internal async Task VerifyOfficialAccountPageAsync(string output)
    {
        ConsentPanel.Visibility = Visibility.Collapsed;
        await StartAccountAsync();
        if (_browser?.CoreWebView2 is not { } core || _accountNavigation is null)
            throw new InvalidOperationException("官方账号网页登录组件未初始化。");
        if (!await _accountNavigation.Task.WaitAsync(TimeSpan.FromSeconds(30)) || !core.Profile.IsInPrivateModeEnabled
            || !Uri.TryCreate(core.Source, UriKind.Absolute, out var source) || !MiyousheLoginService.IsOfficialPage(source))
            throw new InvalidOperationException("官方账号页面未在隔离隐私会话中正常加载。");
        bool hasLogin = false;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            string pageText = await core.ExecuteScriptAsync("document.body.innerText");
            if ((pageText.Contains("登录") || pageText.Contains("Login", StringComparison.OrdinalIgnoreCase)) && !pageText.Contains("加载中")) { hasLogin = true; break; }
            await Task.Delay(500);
        }
        if (!hasLogin) throw new InvalidOperationException("官方账号页面未显示登录内容。");
        using var image = File.Create(Path.Combine(output, "16-official-account-page.png"));
        await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
        File.WriteAllText(Path.Combine(output, "official-account-page-verification.txt"), "官方账号登录页面载入通过，隐私模式开启；密码保存、自动填充、主机对象、网页消息桥、下载与额外权限均禁用。未输入真实账号或密码。\n");
    }
    private async void OnLoginResponse(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        if (_processingWeb || _closed || _operation is not { } operation || operation.IsCancellationRequested
            || !Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) || !MiyousheLoginService.IsLoginResponse(uri) || e.Response.StatusCode != 200) return;
        _processingWeb = true;
        try
        {
            using var content = await e.Response.GetContentAsync();
            using var reader = new StreamReader(content);
            var session = MiyousheLoginService.ParseWebLogin(await reader.ReadToEndAsync(operation.Token));
            if (session is null && _browser?.CoreWebView2 is { } core)
            {
                var cookies = new Dictionary<string, string>();
                foreach (var origin in new[] { "https://user.mihoyo.com", "https://passport-api.mihoyo.com", "https://mihoyo.com" })
                    foreach (var cookie in await core.CookieManager.GetCookiesAsync(origin)) cookies[cookie.Name] = cookie.Value;
                session = await _service.FromLoginCookiesAsync(cookies, operation.Token); cookies.Clear();
            }
            if (session is not null) await ShowRolesAsync(session, operation.Token);
            else if (!operation.IsCancellationRequested) StatusText.Text = "官方账号已完成验证，但未返回祈愿查询授权。可切换扫码登录继续。";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { ShowSafeError(error, operation); }
        finally { _processingWeb = false; }
    }
    private async Task ShowRolesAsync(MiyousheSession session, CancellationToken token)
    {
        StatusText.Text = "登录成功，正在读取官方绑定账号列表…";
        var roles = await _service.GetRolesAsync(session, token); token.ThrowIfCancellationRequested();
        _session = session; RolePicker.ItemsSource = roles; RolePicker.SelectedIndex = 0;
        RolePicker.Visibility = roles.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        await DisposeBrowserAsync(); token.ThrowIfCancellationRequested();
        LoginPanel.Visibility = Visibility.Collapsed; RolePanel.Visibility = Visibility.Visible;
        QrImage.Source = null;
        StatusText.Text = "确认账号后点击开始获取。已有祈愿记录会保留，重复记录会自动跳过。";
    }
    private async void FetchClick(object sender, RoutedEventArgs e)
    {
        if (_session is not { } session || RolePicker.SelectedItem is not MiyousheGameRole role || _operation is not { } operation) return;
        FetchButton.IsEnabled = false;
        try
        {
            StatusText.Text = "正在准备获取该账号的祈愿记录…";
            Credential = await _service.GetWishCredentialAsync(session, role, operation.Token);
            operation.Token.ThrowIfCancellationRequested(); _session = null; Close();
        }
        catch (OperationCanceledException) { Credential = null; }
        catch (Exception error) { Credential = null; ShowSafeError(error, operation); }
        finally { if (!_closed) FetchButton.IsEnabled = true; }
    }
    private void ShowSafeError(Exception error, CancellationTokenSource operation)
    {
        if (!_closed && ReferenceEquals(_operation, operation) && !operation.IsCancellationRequested)
            StatusText.Text = error is UserFacingException ? error.Message : "官方登录暂未完成，请刷新二维码或重新登录。";
        // 不将浏览器异常、响应正文、认证 URL 或登录凭据交给诊断日志。
    }
    private async Task DisposeBrowserAsync()
    {
        var browser = _browser; _browser = null;
        string? directory = _browserDirectory; _browserDirectory = null;
        if (browser is not null)
        {
            try { if (browser.CoreWebView2 is { } core) { core.WebResourceResponseReceived -= OnLoginResponse; core.Stop(); core.CookieManager.DeleteAllCookies(); await core.Profile.ClearBrowsingDataAsync().WaitAsync(TimeSpan.FromSeconds(3)); } }
            catch (Exception) { /* 浏览器可能已经退出。隐私模式仍不持久保存登录 Cookie。 */ }
            finally { BrowserPanel.Children.Remove(browser); browser.Dispose(); }
        }
        if (directory is null) return;
        string full = Path.GetFullPath(directory);
        if (!full.StartsWith(_cacheRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { if (Directory.Exists(full)) Directory.Delete(full, true); return; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { await Task.Delay(200); }
        }
    }
}
