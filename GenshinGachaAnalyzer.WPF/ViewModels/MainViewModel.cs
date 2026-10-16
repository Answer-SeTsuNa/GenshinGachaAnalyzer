using System.Collections.ObjectModel;
using System.Windows.Input;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.Core.Services;
using GenshinGachaAnalyzer.WPF.Infrastructure;
using GenshinGachaAnalyzer.WPF.Services;

namespace GenshinGachaAnalyzer.WPF.ViewModels;

public sealed class AccountOption(string? uid, bool hideUid = false) : ObservableObject
{
    private bool _hideUid = hideUid;
    public bool HideUid { get => _hideUid; set { if (Set(ref _hideUid, value)) Raise(nameof(Display)); } }
    public string? Uid { get; } = uid;
    public string Display => Uid is null ? "+ 添加新账号" : HideUid ? Uid[..Math.Max(0, Uid.Length - 4)] + "****" : Uid;
    public override string ToString() => Display;
}
public sealed class MainViewModel : ObservableObject
{
    private readonly DatabaseService _database;
    private readonly UigfService _uigf;
    private AnalysisService _analysis;
    private readonly GachaLogService _gacha;
    private readonly SettingsService _settingsService;
    private IReadOnlyList<Account> _accounts = [];
    private IReadOnlyList<GachaRecord> _records = [];
    private UserSettings _settings = new();
    private AccountOption? _selectedAccount;
    private object? _currentPage;
    private string _status = "正在加载本地档案…";
    private bool _isBusy, _databaseReady, _databaseLoadFailed, _hasError, _canRetry, _refreshing;
    private bool _retryAddingAccount;
    private string? _exportPath;
    private CancellationTokenSource? _cancellation;
    public WishCatalogService Catalog { get; } = new();
    private readonly WishCatalogUpdateService _catalogUpdates;
    private readonly bool _autoUpdateCatalog;
    private bool _catalogUpdating;
    private string _catalogStatus = "内置资料可离线使用；启动后自动检查新卡池。";
    public string CatalogStatus { get => _catalogStatus; private set => Set(ref _catalogStatus, value); }
    public string CatalogCoverage => Catalog.CoverageText;
    public ICommand UpdateCatalogCommand { get; }
    public Task CatalogUpdateReady { get; private set; } = Task.CompletedTask;
    private string _selectedSection = "总览";
    private CountdownViewModel? _countdown;
    public string SelectedSection { get => _selectedSection; private set { Set(ref _selectedSection, value); Raise(nameof(Sections)); } }
    public IReadOnlyList<SectionTab> Sections => new[] { "总览", "历史", "角色", "武器", "复刻日历", "我的" }.Select(s => new SectionTab(s, s == SelectedSection)).ToArray();
    public IAppDialogs Dialogs { get; }
    public ObservableCollection<AccountOption> Accounts { get; } = [];
    public AccountOption? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (_refreshing || ReferenceEquals(value, _selectedAccount)) return;
            if (value?.Uid is null)
            {
                Raise(); if (!IsBusy) AddAccountCommand.Execute(null); return;
            }
            Set(ref _selectedAccount, value); Raise(nameof(UidText));
            if (!IsBusy) _ = RunAsync(LoadSelectedAsync);
        }
    }
    public object? CurrentPage { get => _currentPage; private set => Set(ref _currentPage, value); }
    public bool IsBusy { get => _isBusy; private set { Set(ref _isBusy, value); Raise(nameof(IsIdle)); CommandManager.InvalidateRequerySuggested(); } }
    public bool IsIdle => !IsBusy;
    public bool HasError { get => _hasError; private set => Set(ref _hasError, value); }
    public bool CanRetry { get => _canRetry; private set => Set(ref _canRetry, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string UidText => SelectedAccount?.Display ?? (_databaseReady ? "尚未添加账号" : _databaseLoadFailed ? "档案未加载" : "正在加载档案…");
    public bool HideUid
    {
        get => _settings.HideUid;
        set
        {
            if (value == _settings.HideUid) return;
            try { var next = _settings with { HideUid = value }; _settingsService.Save(next); _settings = next; }
            catch (Exception error) { ReportError(error); Raise(); return; }
            Raise();
            foreach (var account in Accounts) account.HideUid = _settings.HideUid;
            Raise(nameof(UidText)); Raise(nameof(UidPrivacyHint));
        }
    }
    public string UidPrivacyHint => HideUid ? "显示 UID 后四位" : "隐藏 UID 后四位";
    public ICommand ToggleUidPrivacyCommand { get; }
    public string DataDirectory => Path.GetDirectoryName(_database.DatabasePath)!;
    public string DatabasePath => _database.DatabasePath;
    public IReadOnlyList<GachaRecord> CurrentRecords => _records;
    public string ProductVersion => typeof(MainViewModel).Assembly.GetName().Version!.ToString(3);
    public bool IsStoreDistribution { get; }
    public string CheckUpdateLabel => IsStoreDistribution ? "在商店中检查更新" : "检查版本更新";
    public string DistributionNote => IsStoreDistribution ? "商店版 · 应用更新由 Microsoft Store 管理。档案位于用户目录的「木偶祈愿档案」，卸载应用后保留。旧版档案可通过 UIGF 导入迁移。" : "桌面版 · 应用更新来自 GitHub，档案保存在当前用户的数据目录。";
    private static readonly System.Net.Http.HttpClient UpdateClient = new(new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
    private string _updateStatus = "点击检查 GitHub 最新正式版本。";
    private string? _updatePageUrl;
    public string UpdateStatus { get => _updateStatus; private set => Set(ref _updateStatus, value); }
    public bool HasNewVersion => _updatePageUrl is not null;
    public ObservableCollection<ReleaseDownload> DownloadPackages { get; } = [];
    private string _downloadVersionText = "";
    public string DownloadVersionText { get => _downloadVersionText; private set => Set(ref _downloadVersionText, value); }
    public bool Acrylic { get => _settings.Acrylic; set => SaveAppearance(_settings with { Acrylic = value }, nameof(Acrylic)); }
    public bool SemiTransparent { get => _settings.SemiTransparent; set => SaveAppearance(_settings with { SemiTransparent = value }, nameof(SemiTransparent)); }
    public double OverlayOpacity { get => _settings.OverlayOpacity; set => SaveAppearance(_settings with { OverlayOpacity = value }, nameof(OverlayOpacity)); }
    public double PanelOpacity { get => _settings.PanelOpacity; set => SaveAppearance(_settings with { PanelOpacity = value }, nameof(PanelOpacity)); }
    public double WallpaperOpacity { get => _settings.WallpaperOpacity; set => SaveAppearance(_settings with { WallpaperOpacity = value }, nameof(WallpaperOpacity)); }
    public double WallpaperBlur { get => _settings.WallpaperBlur; set => SaveAppearance(_settings with { WallpaperBlur = value }, nameof(WallpaperBlur)); }
    public string WallpaperText => _settings.WallpaperPath is { } path ? Path.GetFileName(path) : "本地主题纹饰 · 可选择自己的壁纸";
    public bool DeveloperMode
    {
        get => _settings.DeveloperMode;
        set { SaveAppearance(_settings with { DeveloperMode = value }, nameof(DeveloperMode)); DiagnosticLog.Info(value ? "开发者模式已开启" : "开发者模式已关闭"); }
    }
    public ICommand DeveloperLogCommand { get; }
    public ICommand SelectWallpaperCommand { get; }
    public ICommand ResetWallpaperCommand { get; }
    private void SaveAppearance(UserSettings next, string property)
    {
        if (next == _settings) return;
        try { _settingsService.Save(next); _settings = next; ThemeService.Configure(next); Raise(property); }
        catch (Exception error) { ReportError(error); Raise(property); }
    }
    public string GamePath => _settings.GamePath ?? "尚未设置，将尝试自动检测国服安装目录";
    public IReadOnlyList<FilterOption> Themes { get; } = [new("System", "跟随系统"), new("Light", "白天"), new("Dark", "夜晚")];
    private double _automaticScale = 1;
    public double EffectiveScale => _settings.UiScale > 0 ? Math.Clamp(_settings.UiScale, 1, 1.75) : _automaticScale;
    public IReadOnlyList<FilterOption> DisplayScales { get; } = [new("0", "自动适配屏幕"), new("1", "100%"), new("1.25", "125%"), new("1.5", "150%"), new("1.75", "175%")];
    public FilterOption SelectedDisplayScale
    {
        get => DisplayScales.FirstOrDefault(s => s.Value == _settings.UiScale.ToString(System.Globalization.CultureInfo.InvariantCulture)) ?? DisplayScales[0];
        set
        {
            if (value is null) return;
            double scale = double.Parse(value.Value, System.Globalization.CultureInfo.InvariantCulture);
            if (scale == _settings.UiScale) return;
            try { var next = _settings with { UiScale = scale }; _settingsService.Save(next); _settings = next; Raise(); Raise(nameof(EffectiveScale)); }
            catch (Exception error) { ReportError(error); }
        }
    }
    public void SetAutomaticScale(double scale) { _automaticScale = scale; Raise(nameof(EffectiveScale)); }
    public string LogDirectory => Path.Combine(DataDirectory, "logs");
    public ICommand OpenLogsCommand { get; }
    public FilterOption SelectedTheme
    {
        get => Themes.FirstOrDefault(t => t.Value == _settings.Theme) ?? Themes[0];
        set
        {
            if (value is null || value.Value == _settings.Theme) return;
            var next = _settings with { Theme = value.Value };
            try { _settingsService.Save(next); _settings = next; ThemeService.Configure(next); Raise(); }
            catch (Exception error) { ReportError(error); Raise(); }
        }
    }
    public IReadOnlyList<ThemeProfile> Styles => ThemeProfile.All;
    public ThemeProfile SelectedStyle
    {
        get => Styles.FirstOrDefault(p => p.Key == _settings.StyleKey) ?? Styles[0];
        set
        {
            if (value is null || value.Key == _settings.StyleKey) return;
            try { var next = _settings with { StyleKey = value.Key }; _settingsService.Save(next); _settings = next; ThemeService.Configure(next); Raise(); Raise(nameof(ThemeDescription)); Raise(nameof(ThemeArtwork)); }
            catch (Exception error) { ReportError(error); }
        }
    }
    public string ThemeDescription => SelectedStyle.Description;
    public string? ThemeArtwork => null;
    public string UpdatedText => _accounts.FirstOrDefault(a => a.Uid == SelectedAccount?.Uid) is { } a ? $"最近写入 {a.LastUpdated:yyyy-MM-dd HH:mm}" : "本地档案";
    public string ArchiveText => _databaseReady ? $"{_accounts.Count} 个账号 · 当前账号 {_records.Count:N0} 条记录" : _databaseLoadFailed ? "本地档案尚未加载" : "正在加载本地档案…";
    public string SourceSummary => _records.Count == 0 ? "暂无记录" : string.Join(" · ", _records.GroupBy(r => r.Source).Select(g => $"{g.Key} {g.Count():N0} 条"));
    public string? RepositoryUrl { get; }
    public bool HasRepository => !string.IsNullOrEmpty(RepositoryUrl);
    public bool HasExportPath => _exportPath is not null;
    public ICommand SectionCommand { get; }
    public ICommand ClearAccountCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand FetchCommand { get; }
    public ICommand FetchUrlCommand { get; }
    public ICommand FetchMiyousheCommand { get; }
    public ICommand AddAccountCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand DashboardCommand { get; }
    public ICommand DetailCommand { get; }
    public ICommand SwitchPoolCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand SelectGamePathCommand { get; }
    public ICommand OpenDataCommand { get; }
    public ICommand OpenExportFolderCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand UigfLinkCommand { get; }
    public ICommand MigrationCommand { get; }
    public ICommand RepositoryCommand { get; }
    public ICommand CheckUpdateCommand { get; }
    public ICommand ReleasesCommand { get; }
    public ICommand DownloadUpdateCommand { get; }
    public ICommand OpenReleaseDownloadCommand { get; }
    public ICommand LicenseCommand { get; }

    public MainViewModel(DatabaseService database, UigfService uigf, AnalysisService analysis, GachaLogService gacha,
        SettingsService settings, IAppDialogs dialogs, string? repositoryUrl, ReleaseUpdateService? updateService = null,
        bool autoUpdateCatalog = false, WishCatalogUpdateService? catalogUpdates = null, bool? storeDistribution = null)
    {
        _database = database; _uigf = uigf; _analysis = analysis; _gacha = gacha; _settingsService = settings; Dialogs = dialogs;
        IsStoreDistribution = storeDistribution ?? AppPaths.HasPackageIdentity;
        if (IsStoreDistribution) _updateStatus = "应用版本通过 Microsoft Store 更新；卡池资料仍可独立更新。";
        _autoUpdateCatalog = autoUpdateCatalog;
        _catalogUpdates = catalogUpdates ?? new WishCatalogUpdateService(UpdateClient, Path.Combine(DataDirectory, "catalog", "snapshot.json"));
        RuntimeArtworkCache.Configure(DataDirectory, autoUpdateCatalog); RuntimeArtworkCache.Register(Catalog);
        UpdateCatalogCommand = new AsyncCommand(() => UpdateCatalogAsync(true), ReportError, () => !_catalogUpdating && !IsBusy);
        RepositoryUrl = repositoryUrl;
        ResetReleaseDownloads();
        DiagnosticLog.Configure(LogDirectory);
        DeveloperLogCommand = new RelayCommand(_ => Dialogs.ShowDiagnosticLog());
        ToggleUidPrivacyCommand = new RelayCommand(_ => HideUid = !HideUid);
        SelectWallpaperCommand = new RelayCommand(_ =>
        {
            var path = Dialogs.ChooseWallpaperFile();
            if (path is not null) SaveAppearance(_settings with { WallpaperPath = path }, nameof(WallpaperText));
        });
        ResetWallpaperCommand = new RelayCommand(_ => SaveAppearance(_settings with { WallpaperPath = null }, nameof(WallpaperText)));
        bool Ready() => !IsBusy && _databaseReady;
        ImportCommand = new AsyncCommand(() => RunAsync(ImportAsync), ReportError, Ready);
        ExportCommand = new AsyncCommand(() => RunAsync(ExportAsync), ReportError, () => Ready() && _accounts.Count > 0);
        FetchCommand = new AsyncCommand(() => RunAsync(() => FetchAsync(false)), ReportError, Ready);
        FetchUrlCommand = new AsyncCommand(() => FetchUrlAsync(false), ReportError, Ready);
        FetchMiyousheCommand = new AsyncCommand(() => FetchMiyousheAsync(false), ReportError, Ready);
        AddAccountCommand = new AsyncCommand(AddAccountAsync, ReportError, Ready);
        RetryCommand = new AsyncCommand(() => RunAsync(() => _databaseReady ? FetchAsync(_retryAddingAccount) : InitializeDatabaseAsync()), ReportError, () => !IsBusy && CanRetry);
        CancelCommand = new RelayCommand(_ => _cancellation?.Cancel(), () => IsBusy);
        DashboardCommand = new RelayCommand(_ => ShowDashboard(), () => !IsBusy);
        SectionCommand = new RelayCommand(p => ShowSection(p is SectionTab tab ? tab.Name : p?.ToString() ?? "总览"), () => !IsBusy);
        ClearAccountCommand = new AsyncCommand(() => RunAsync(ClearAccountAsync), ReportError, () => Ready() && SelectedAccount?.Uid is not null);
        DetailCommand = new RelayCommand(p => { if (p is PoolViewModel pool) { _navigationRevision++; CurrentPage = new DetailViewModel(this, pool); } }, () => !IsBusy);
        SwitchPoolCommand = new RelayCommand(p => { if (p is PoolDefinition pool) ShowDetail(pool); }, () => !IsBusy);
        SettingsCommand = new RelayCommand(_ => ShowSection("设置"), () => !IsBusy);
        SelectGamePathCommand = new RelayCommand(_ => ChooseGamePath(), () => !IsBusy);
        OpenDataCommand = new RelayCommand(_ => SafeOpen(DataDirectory));
        OpenLogsCommand = new RelayCommand(_ =>
        {
            try { Directory.CreateDirectory(LogDirectory); SafeOpen(LogDirectory); }
            catch (Exception error) { ReportError(error); }
        });
        OpenExportFolderCommand = new RelayCommand(_ => { if (_exportPath is not null) SafeOpen(Path.GetDirectoryName(_exportPath)!); });
        BackupCommand = new AsyncCommand(() => RunAsync(async () =>
        {
            Status = "正在创建一致性备份…";
            string file = await Task.Run(_database.CreateBackup);
            Status = $"备份完成：{file}";
        }), ReportError, Ready);
        UigfLinkCommand = new RelayCommand(_ => OpenLink("https://uigf.org/"));
        MigrationCommand = new RelayCommand(_ => SafeOpen(Path.Combine(AppContext.BaseDirectory, "MIGRATION.md")));
        RepositoryCommand = new RelayCommand(_ => { if (RepositoryUrl is not null) OpenLink(RepositoryUrl); }, () => HasRepository);
        CheckUpdateCommand = new AsyncCommand(async () =>
        {
            _updatePageUrl = null; Raise(nameof(HasNewVersion)); ResetReleaseDownloads();
            if (IsStoreDistribution) { Dialogs.OpenStoreUpdates(); UpdateStatus = "已打开 Microsoft Store，请在下载与更新页面检查新版。"; return; }
            UpdateStatus = "正在检查 GitHub 最新版本…";
            try
            {
                var release = await (updateService ?? new ReleaseUpdateService(UpdateClient)).CheckAsync(RepositoryUrl!);
                bool newer = release.IsNewerThan(typeof(MainViewModel).Assembly.GetName().Version!);
                if (newer) _updatePageUrl = release.PageUrl;
                if (release.Downloads.Count > 0 && release.Version >= Version.Parse(ProductVersion))
                {
                    DownloadPackages.Clear(); foreach (var download in release.Downloads) DownloadPackages.Add(download);
                    DownloadVersionText = $"下载 v{release.Version.ToString(3)}";
                }
                UpdateStatus = newer ? $"发现新版本 v{release.Version}，可选择下方下载渠道。"
                    : "已是最新版本";
                Raise(nameof(HasNewVersion));
            }
            catch (Exception error) when (error is UserFacingException or OperationCanceledException)
            { UpdateStatus = error is UserFacingException ? error.Message : "检查更新已取消。"; }
        }, _ => UpdateStatus = IsStoreDistribution ? "未能打开 Microsoft Store，请手动打开商店检查更新。" : "检查更新未完成，请打开 GitHub 发布页查看。", () => IsStoreDistribution || HasRepository);
        ReleasesCommand = new RelayCommand(_ => OpenLink(ReleaseUpdateService.ReleasesUrl(RepositoryUrl!)), () => HasRepository);
        DownloadUpdateCommand = new RelayCommand(_ => { if (_updatePageUrl is { } url) OpenLink(url); }, () => HasNewVersion);
        OpenReleaseDownloadCommand = new RelayCommand(p =>
        {
            if (p is ReleaseDownloadLink link && DownloadPackages.SelectMany(package => package.Links).Contains(link)) OpenLink(link.Url);
        }, () => !IsStoreDistribution);
        LicenseCommand = new RelayCommand(_ => SafeOpen(Path.Combine(AppContext.BaseDirectory, "LICENSE")));
        CurrentPage = new LoadingViewModel("本地档案");
        (string Style, bool Dark)? chartTheme = null;
        ThemeService.Changed += () =>
        {
            var next = (ThemeService.StyleKey, ThemeService.IsDark);
            if (chartTheme == next) return;
            chartTheme = next; // 透明度和壁纸变化不重建图表画笔。
            if (CurrentPage is DetailViewModel detail) detail.ApplyTheme();
            foreach (var personal in _pageCache.Values.OfType<PersonalViewModel>()) personal.ApplyTheme();
        };
    }
    public async Task InitializeAsync()
    {
        var cached = await Task.Run(() => _catalogUpdates.LoadCache(Catalog));
        if (cached is not null)
        {
            ApplyCatalog(cached);
            CatalogStatus = $"上次检查：{cached.CheckedAt.ToOffset(TimeSpan.FromHours(8)):yyyy-MM-dd HH:mm} · 本地缓存可离线使用。";
        }
        try
        {
            _settings = _settingsService.Load(); ThemeService.Configure(_settings);
            Raise(nameof(HideUid)); Raise(nameof(UidPrivacyHint));
            // 首次打开即检测安装目录，无需先点击更新；失效的旧路径也重新检测。
            if (_settings.GamePath is null || !GachaLogService.IsGameDirectory(_settings.GamePath))
            {
                string? detected = await Task.Run(GachaLogService.DetectGamePath);
                if (detected is not null) { _settings = _settings with { GamePath = detected }; _settingsService.Save(_settings); }
                DiagnosticLog.Info(detected is null ? "启动检测：未发现国服游戏目录，可手动选择或导入 UIGF。" : "启动检测游戏目录：" + detected);
            }
            Raise(nameof(GamePath)); Raise(nameof(SelectedTheme)); Raise(nameof(SelectedStyle)); Raise(nameof(ThemeDescription)); Raise(nameof(SelectedDisplayScale)); Raise(nameof(EffectiveScale));
            Raise(nameof(Acrylic)); Raise(nameof(SemiTransparent)); Raise(nameof(OverlayOpacity)); Raise(nameof(PanelOpacity)); Raise(nameof(WallpaperOpacity)); Raise(nameof(WallpaperBlur)); Raise(nameof(WallpaperText)); Raise(nameof(DeveloperMode));
        }
        catch (Exception error) { ReportError(error); }
        await RunAsync(InitializeDatabaseAsync);
        if (_autoUpdateCatalog && _databaseReady && (Catalog.UpdatedAt is null || DateTimeOffset.UtcNow - Catalog.UpdatedAt > TimeSpan.FromHours(6)))
            CatalogUpdateReady = UpdateCatalogAsync(false);
    }
    private void ApplyCatalog(CatalogSnapshot snapshot)
    {
        Catalog.Apply(snapshot); _analysis = new AnalysisService(new CatalogBannerProvider(Catalog), Catalog);
        RuntimeArtworkCache.Register(Catalog); ItemIconCache.RefreshMissing();
        _countdown = null; _pageCache.Clear(); Raise(nameof(CatalogCoverage));
    }
    private async Task UpdateCatalogAsync(bool manual)
    {
        if (_catalogUpdating) return;
        _catalogUpdating = true; CommandManager.InvalidateRequerySuggested();
        CatalogStatus = "正在检查卡池与角色、武器资料…";
        try
        {
            var snapshot = await _catalogUpdates.DownloadAsync(Catalog, CancellationToken.None);
            while (IsBusy) await Task.Delay(100); // 写入完成后再刷新分析页面。
            await NavigationReady;
            ApplyCatalog(snapshot);
            if (_databaseReady) { _navigationRevision++; await LoadSelectedAsync(); }
            CatalogStatus = $"资料检查完成 · 排期截至 {Catalog.CoveredThrough:yyyy-MM-dd} · 无需重装软件。";
            if (Catalog.CoveredThrough < DateTimeOffset.UtcNow) CatalogStatus += " 上游尚未提供当前排期，未覆盖部分保持未知。";
            if (manual) Status = CatalogStatus;
        }
        catch (Exception error)
        {
            CatalogStatus = error is UserFacingException ? error.Message : "卡池资料暂时无法更新，继续使用本地资料。可稍后重试。";
            if (manual) Status = CatalogStatus;
        }
        finally { _catalogUpdating = false; CommandManager.InvalidateRequerySuggested(); }
    }
    private async Task InitializeDatabaseAsync()
    {
        _databaseLoadFailed = false;
        CurrentPage = new LoadingViewModel("本地档案");
        Raise(nameof(UidText)); Raise(nameof(ArchiveText));
        Status = "正在检查本地数据库…";
        try
        {
            if (ProcessSecurity.IntegrityLevel() is < 8192)
                throw new UserFacingException("程序以 Windows 低完整性权限运行，无法写入原数据目录。请使用修正发布标记后的版本正常启动，无需清空或重新导入数据。诊断信息可查看本次内存日志。");
            await Task.Run(_database.Initialize);
            // 初始化和账号读取都成功后才开放写入入口，读取失败时重试仍走加载流程。
            await RefreshAsync(); _databaseReady = true;
            ShowSection(SelectedSection); await NavigationReady;
            Raise(nameof(UidText)); Raise(nameof(ArchiveText));
            Status = "本地数据已就绪 · 无遥测，无云端同步。"; CanRetry = false;
        }
        catch { _databaseReady = false; _databaseLoadFailed = true; CanRetry = true; CurrentPage = new DatabaseUnavailableViewModel(this); Raise(nameof(UidText)); Raise(nameof(ArchiveText)); throw; }
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; HasError = false; _cancellation = new();
        try { await action(); }
        catch (Exception error) { ReportError(error); }
        finally { _cancellation.Dispose(); _cancellation = null; IsBusy = false; }
    }
    public void ReportError(Exception error)
    {
        Status = ErrorMessages.Readable(error); HasError = true;
        DiagnosticLog.Error("操作未完成 · " + Status, error);
        if (DeveloperMode && error is not OperationCanceledException) Dialogs.ShowDiagnosticLog();
        if (error is UnsupportedUigfException) Dialogs.Explain("UIGF 版本不兼容", Status + "\n\n设置与关于页可打开本地迁移指南。");
    }
    private async Task RefreshAsync(string? preferredUid = null)
    {
        _accounts = await Task.Run(_database.GetAccounts);
        RebuildAccounts(preferredUid ?? SelectedAccount?.Uid);
        await LoadSelectedAsync();
    }
    private void RebuildAccounts(string? uid)
    {
        _refreshing = true;
        try
        {
            Accounts.Clear(); foreach (var account in _accounts) Accounts.Add(new(account.Uid, HideUid));
            Accounts.Add(new(null)); _selectedAccount = Accounts.FirstOrDefault(a => a.Uid == uid && uid is not null) ?? Accounts.FirstOrDefault(a => a.Uid is not null);
            Raise(nameof(SelectedAccount)); Raise(nameof(UidText)); Raise(nameof(UpdatedText));
        }
        finally { _refreshing = false; }
    }
    private async Task LoadSelectedAsync()
    {
        _navigationRevision++; // 账号刷新立即作废上一账号尚未完成的页面构建。
        string? uid = SelectedAccount?.Uid;
        _records = uid is null ? [] : await Task.Run(() => _database.GetRecords(uid));
        _pageCache.Clear();
        Raise(nameof(ArchiveText)); Raise(nameof(SourceSummary)); Raise(nameof(UpdatedText));
        if (CurrentPage is DetailViewModel detail) ShowDetail(detail.Pool.Analysis.Pool); else { ShowSection(SelectedSection); await NavigationReady; }
    }
    public void ShowDashboard() => ShowSection("总览");
    private readonly Dictionary<string, object> _pageCache = new();
    private int _navigationRevision;
    public Task NavigationReady { get; private set; } = Task.CompletedTask;
    public void ShowSection(string section) => NavigationReady = NavigateAsync(section);
    private async Task NavigateAsync(string section)
    {
        SelectedSection = section; int revision = ++_navigationRevision;
        if (!_databaseReady && section != "设置")
        {
            CurrentPage = _databaseLoadFailed ? new DatabaseUnavailableViewModel(this) : new LoadingViewModel("本地档案");
            return;
        }
        if (section == "复刻日历")
        {
            var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).DateTime);
            if (_countdown is null || _countdown.CalculatedDay != today) _countdown = new CountdownViewModel(this, Catalog);
            CurrentPage = _countdown; await _countdown.Ready; return;
        }
        if (_pageCache.TryGetValue(section, out var saved)) { CurrentPage = saved; return; }
        if (section == "设置") { var settings = new SettingsViewModel(this); _pageCache[section] = settings; CurrentPage = settings; return; }
        if (_records.Count == 0 && section != "历史" && section != "我的") { CurrentPage = new WelcomeViewModel(this); return; }
        var snapshot = _records;
        try
        {
            var construction = Task.Run<object>(() => section switch
            {
                "历史" => new HistoryViewModel(this, snapshot, Catalog),
                "角色" => new CollectionViewModel(this, snapshot, Catalog, "角色"),
                "武器" => new CollectionViewModel(this, snapshot, Catalog, "武器"),
                "我的" => new PersonalViewModel(this, snapshot),
                _ => new DashboardViewModel(this, PoolCatalog.MainPools.Select(p => new PoolViewModel(_analysis.Analyze(snapshot, p), snapshot, Catalog)).ToArray())
            });
            // 短任务保留上一页，避免“加载页 → 目标页”连续闪烁；耗时统计仍在后台执行。
            if (await Task.WhenAny(construction, Task.Delay(120)) != construction && revision == _navigationRevision)
                CurrentPage = new LoadingViewModel(section);
            object page = await construction;
            if (revision != _navigationRevision) return;
            _pageCache[section] = page; CurrentPage = page;
        }
        catch (Exception error) { if (revision == _navigationRevision) { ReportError(error); CurrentPage = new WelcomeViewModel(this); } }
    }
    public void ShowDetail(PoolDefinition pool)
    {
        _navigationRevision++;
        CurrentPage = new DetailViewModel(this, new(_analysis.Analyze(_records, pool), _records, Catalog));
    }
    private async Task AddAccountAsync()
    {
        switch (Dialogs.ChooseDataSource())
        {
            case DataSourceChoice.Game: await RunAsync(() => FetchAsync(true)); break;
            case DataSourceChoice.Uigf: await RunAsync(ImportAsync); break;
            case DataSourceChoice.Url: await FetchUrlAsync(true); break;
            case DataSourceChoice.Miyoushe: await FetchMiyousheAsync(true); break;
        }
    }
    private async Task ImportAsync()
    {
        string? path = Dialogs.ChooseImportFile(); if (path is null) return;
        var progress = new Progress<ImportProgress>(p => Status = p.Total > 0 ? $"{p.Stage} · {p.Processed:N0} / {p.Total:N0}" : p.Stage + "…");
        // 预检在后台执行，确认时使用相同的不可变内存快照，避免文件在确认后被替换。
        var import = await Task.Run(() => _uigf.PreflightAsync(path, progress, _cancellation!.Token), _cancellation!.Token);
        if (!Dialogs.ConfirmImport(import)) { Status = "已取消导入，未写入数据。"; return; }
        Status = "正在备份现有数据库…";
        await Task.Run(_database.CreateBackup);
        var result = await Task.Run(() => _database.Insert(import.Records, import.Accounts, progress, _cancellation!.Token));
        await RefreshAsync(import.Accounts.First().Uid);
        Status = $"导入完成 · 新增 {result.Added:N0} · 重复跳过 {result.Duplicates:N0} · 冲突 {result.Conflicts:N0}";
        Dialogs.ShowWriteResult(result);
    }
    private async Task ExportAsync()
    {
        var selected = Dialogs.SelectExportAccounts(_accounts); if (selected is null || selected.Count == 0) return;
        string? path = Dialogs.ChooseExportFile(); if (path is null) return;
        Status = $"正在生成 UIGF {Dialogs.ExportVersion}…";
        var snapshot = await Task.Run(() => _database.Snapshot(selected));
        await _uigf.ExportAsync(path, snapshot, _cancellation!.Token, Dialogs.ExportVersion);
        _exportPath = path; Raise(nameof(HasExportPath)); Status = $"已导出 UIGF {Dialogs.ExportVersion}：{path}";
    }
    private async Task FetchUrlAsync(bool addingAccount)
    {
        var credential = Dialogs.ChooseGachaUrl();
        if (credential is not null) await RunAsync(() => FetchAsync(addingAccount, credential));
    }
    private async Task FetchMiyousheAsync(bool addingAccount)
    {
        var credential = await Dialogs.ChooseMiyousheCredentialAsync();
        if (credential is null) { Status = "已结束本次米游社登录，已有档案保持不变。"; return; }
        await RunAsync(() => FetchAsync(addingAccount, credential, "米游社登录授权"));
    }
    private async Task FetchAsync(bool addingAccount, GachaCredential? pastedCredential = null, string? sourceLabel = null)
    {
        _retryAddingAccount = addingAccount; CanRetry = false;
        string? gamePath = pastedCredential is null ? _settings.GamePath ?? await Task.Run(GachaLogService.DetectGamePath) : null;
        if (pastedCredential is null && gamePath is null)
        {
            Dialogs.Explain("未检测到国服游戏", "请手动选择含 YuanShen.exe 的安装目录。游戏未安装时，建议使用「导入 UIGF」。");
            gamePath = ChooseGamePath();
            if (gamePath is null) { Status = "尚未选择游戏路径，可导入 UIGF 离线使用。"; CanRetry = true; return; }
        }
        Status = pastedCredential is null ? "正在读取本地祈愿缓存…" : $"正在使用{sourceLabel ?? "国服祈愿链接"}获取…";
        string? fetchedUid = null;
        try
        {
            var credential = pastedCredential ?? await _gacha.ExtractCredentialAsync(gamePath!, _cancellation!.Token);
            await Task.Run(_database.CreateBackup);
            var progress = new Progress<FetchProgress>(p => Status = (fetchedUid is null ? "" : $"UID {fetchedUid} · ") +
                $"{p.Pool} · 第 {p.Page} 页 · 已获取 {p.Fetched:N0} 条 · 已保存新增 {p.Saved:N0} 条");
            var result = await _gacha.FetchAsync(credential, _database, progress, _cancellation!.Token, async uid =>
            {
                fetchedUid = uid;
                await RefreshAsync(uid);
            });
            fetchedUid = result.Uid;
            await RefreshAsync(fetchedUid);
            Status = result.Fetched == 0 ? "游戏接口未返回记录。可先在游戏内打开祈愿历史，也可导入历史 UIGF 文件。" :
                $"UID {result.Uid} · 获取完成 · 新增 {result.Added:N0} · 重复跳过 {result.Duplicates:N0} · 冲突 {result.ConflictIds.Count:N0}";
            if (result.ConflictIds.Count > 0 || result.LogWarning is not null)
                Dialogs.ShowWriteResult(new(result.Added, result.Duplicates, result.ConflictIds)
                { Details = result.Details, LogPath = result.LogPath, LogWarning = result.LogWarning });
        }
        catch
        {
            CanRetry = pastedCredential is null;
            // 网络失败仍刷新此前已经完整提交的页，让用户立即看到保留的数据。
            await RefreshAsync(fetchedUid); throw;
        }
    }
    private async Task ClearAccountAsync()
    {
        string? uid = SelectedAccount?.Uid;
        if (uid is null || !Dialogs.ConfirmClearAccount(uid, _records.Count)) { Status = "已取消清空，数据保持不变。"; return; }
        Status = $"正在备份并清空账号 {uid}…";
        var result = await Task.Run(() => _database.ClearAccount(uid, _cancellation!.Token));
        SelectedSection = "总览"; CurrentPage = new WelcomeViewModel(this); await RefreshAsync();
        Status = $"已清空 {uid} 的 {result.Deleted:N0} 条记录，可重新导入。备份：{result.BackupPath}";
    }
    private string? ChooseGamePath()
    {
        string? path = Dialogs.ChooseGameFolder(); if (path is null) return null;
        try
        {
            if (!GachaLogService.IsGameDirectory(path)) throw new UserFacingException("请选择包含 YuanShen.exe 的国服游戏目录。云原神和国际服不支持。");
            var next = _settings with { GamePath = path }; _settingsService.Save(next); _settings = next;
            Raise(nameof(GamePath)); Status = "国服游戏路径已保存。"; return path;
        }
        catch (Exception error) { ReportError(error); return null; }
    }
    private void ResetReleaseDownloads()
    {
        DownloadPackages.Clear();
        if (IsStoreDistribution || !HasRepository) return;
        foreach (var package in ReleaseDownloads.ForTag(RepositoryUrl!, "v" + ProductVersion)) DownloadPackages.Add(package);
        DownloadVersionText = $"下载当前版本 v{ProductVersion}";
    }
    private void SafeOpen(string path) { try { Dialogs.OpenPath(path); } catch (Exception error) { ReportError(error); } }
    private void OpenLink(string url) { try { Dialogs.OpenLink(url); } catch (Exception error) { ReportError(error); } }
}
