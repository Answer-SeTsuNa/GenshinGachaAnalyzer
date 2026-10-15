using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Services;
using GenshinGachaAnalyzer.WPF.Infrastructure;
using GenshinGachaAnalyzer.WPF.Services;
using GenshinGachaAnalyzer.WPF.ViewModels;
using GenshinGachaAnalyzer.WPF.Views;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;

namespace GenshinGachaAnalyzer.WPF;

public partial class App : Application
{
    private HttpClient? _client;
    private Mutex? _mutex;
    public App()
    {
        // 仅检测到游戏加加时绕开其 Direct3D 叠加层钩子，其余环境保留硬件加速。
        System.Windows.Media.RenderOptions.ProcessRenderMode = StartupCompatibility.OverlayDetected
            ? System.Windows.Interop.RenderMode.SoftwareOnly : System.Windows.Interop.RenderMode.Default;
    }
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // UI 验证是开发用命令行选项，使用明确指定的独立目录，绝不触碰真实用户档案。
        bool startupCheck = e.Args.Length >= 2 && e.Args[0] == "--verify-startup";
        bool verify = startupCheck || e.Args.Length >= 2 && e.Args[0] == "--verify-ui";
        string dataDirectory = verify ? Path.GetFullPath(e.Args[1]) : AppPaths.DataDirectory;
        if (verify && (string.Equals(dataDirectory.TrimEnd(Path.DirectorySeparatorChar), AppPaths.DataDirectory, StringComparison.OrdinalIgnoreCase) || File.Exists(Path.Combine(dataDirectory, "gacha.db"))))
        {
            Shutdown(2); return; // 开发验证必须使用新的隔离目录，不能读取/混入真实档案。
        }
        string instanceId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataDirectory + Environment.UserName)))[..20];
        DiagnosticLog.Info("启动阶段：检查单实例");
        _mutex = new Mutex(true, @"Local\GenshinGachaAnalyzer-" + instanceId, out bool created);
        if (!created) { MessageBox.Show("工具已经运行，请使用现有窗口。", "木偶祈愿分析"); Shutdown(); return; }
        DiagnosticLog.Configure(Path.Combine(dataDirectory, "logs"));
        DiagnosticLog.Info("启动 " + typeof(App).Assembly.GetName().Version!.ToString(3));
        var startupWatchdog = startupCheck ? StartupVerifier.Watch(dataDirectory, Dispatcher) : null;
        DiagnosticLog.Info($"渲染模式：{System.Windows.Media.RenderOptions.ProcessRenderMode}；WPF 渲染层级 {System.Windows.Media.RenderCapability.Tier >> 16}；壁纸后台预处理。");
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            DiagnosticLog.Error("未处理的界面异常", args.Exception);
            if (verify) File.WriteAllText(Path.Combine(dataDirectory, "ui-dispatcher-error.txt"), args.Exception.ToString());
            else
            {
                MessageBox.Show(ErrorMessages.Readable(args.Exception) + "\n\n诊断日志：" + DiagnosticLog.CurrentPath, "操作未完成", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (ThemeService.Appearance.DeveloperMode) new DeveloperLogWindow { Owner = MainWindow }.ShowDialog();
            }
            Shutdown(1);
        };
        try
        {
            DiagnosticLog.Info("启动阶段：加载主题"); ThemeService.Apply("System");
            LiveCharts.Configure(config => config.AddLightTheme().HasTextSettings(new TextSettings
            {
                DefaultTypeface = SKTypeface.FromFamilyName("Microsoft YaHei UI")
            }));
            _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(20) };
            string? repository = ReadRepositoryUrl();
            var database = new DatabaseService(Path.Combine(dataDirectory, "gacha.db"));
            var shell = new MainViewModel(database, new UigfService(), new AnalysisService(new CatalogBannerProvider()),
                  new GachaLogService(_client), new SettingsService(Path.Combine(dataDirectory, "settings.json")), new AppDialogs(), repository, autoUpdateCatalog: !verify);
            DiagnosticLog.Info("启动阶段：创建窗口");
            var window = new MainWindow(shell); MainWindow = window;
            // 启动检查必须展示并激活真实窗口；此前隐藏任务栏/不激活的检查漏掉了叠加层注入问题。
            if (verify && !startupCheck) { window.ShowInTaskbar = false; window.ShowActivated = false; }
            window.Show(); DiagnosticLog.Info("启动阶段：窗口已显示，初始化本地档案");
            if (startupCheck && e.Args.Length >= 3 && !e.Args[2].StartsWith("--", StringComparison.Ordinal))
            {
                await Task.Run(database.Initialize);
                var seed = await new UigfService().PreflightAsync(Path.GetFullPath(e.Args[2]));
                await Task.Run(() => database.Insert(seed.Records, seed.Accounts));
            }
            await shell.InitializeAsync();
            DiagnosticLog.Info(shell.HasError ? "启动阶段：本地档案未能加载，请查看上方错误" : "启动阶段：本地档案初始化完成");
            if (startupCheck)
            {
                if (e.Args.Contains("--interactive"))
                {
                    // 开发用交互预览仍受全新隔离目录限制，供实际点击验证；不会进入真实用户档案。
                    startupWatchdog?.Cancel(); startupWatchdog?.Dispose();
                    File.WriteAllText(Path.Combine(dataDirectory, "preview-ready.txt"), "隔离合成档案交互预览\n");
                    return;
                }
                await StartupVerifier.RunAsync(window, shell, dataDirectory);
                if (e.Args.Contains("--verify-backdrop")) await BackdropVerifier.RunAsync(window, dataDirectory);
                startupWatchdog?.Cancel(); startupWatchdog?.Dispose(); Shutdown(0); return;
            }
            if (verify)
            {
                try { await UiVerifier.RunAsync(window, shell, database, dataDirectory, e.Args.Length >= 3 ? e.Args[2] : null); Shutdown(0); }
                catch (Exception error) { File.WriteAllText(Path.Combine(dataDirectory, "ui-verification-error.txt"), error.ToString()); Shutdown(1); }
            }
        }
        catch (Exception error)
        {
            DiagnosticLog.Error("启动失败", error);
            if (verify) { Directory.CreateDirectory(dataDirectory); File.WriteAllText(Path.Combine(dataDirectory, "ui-startup-error.txt"), error.ToString()); }
            else
            {
                MessageBox.Show(ErrorMessages.Readable(error) + "\n\n诊断日志：" + DiagnosticLog.CurrentPath, "启动未完成");
                if (ThemeService.Appearance.DeveloperMode) new DeveloperLogWindow().ShowDialog();
            }
            Shutdown(1);
        }
    }
    private static string? ReadRepositoryUrl()
    {
        try
        {
            using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
            string? value = config.RootElement.GetProperty("RepositoryUrl").GetString();
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" ? uri.AbsoluteUri : null;
        }
        catch (Exception error) when (error is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { return null; }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        ThemeService.Stop(); _client?.Dispose(); _mutex?.Dispose(); base.OnExit(e);
    }
}

