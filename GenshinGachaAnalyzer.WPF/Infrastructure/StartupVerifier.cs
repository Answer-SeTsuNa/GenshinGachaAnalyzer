using System.Diagnostics;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
using System.Text.Json;
using GenshinGachaAnalyzer.WPF.Services;
using GenshinGachaAnalyzer.WPF.Controls;
using GenshinGachaAnalyzer.WPF.ViewModels;
using GenshinGachaAnalyzer.WPF.Views;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

/// <summary>启动回归：使用正式渲染路径、独立档案和后台心跳，不能用离屏截图代替响应检查。</summary>
internal static class StartupVerifier
{
    public static CancellationTokenSource Watch(string output, Dispatcher dispatcher)
    {
        Directory.CreateDirectory(output);
        var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token);
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Normal).Task.WaitAsync(TimeSpan.FromSeconds(5), token);
                    File.AppendAllText(Path.Combine(output, "startup-heartbeat.txt"), "UI dispatcher responsive\n");
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "startup-error.txt"), error.ToString());
                // 仅终止本次隔离测试进程，避免 UI 已死锁时 Shutdown 无法执行。
                Environment.Exit(3);
            }
        });
        return cancellation;
    }

    public static async Task RunAsync(MainWindow window, MainViewModel shell, string output)
    {
        File.WriteAllText(Path.Combine(output, "package-identity.txt"), GenshinGachaAnalyzer.Core.Helpers.AppPaths.HasPackageIdentity ? "Packaged\n" : "Unpackaged\n");
        int? integrity = ProcessSecurity.IntegrityLevel();
        if (integrity is null or < 8192) throw new InvalidOperationException("发布版没有以正常完整性权限启动。");
        File.WriteAllText(Path.Combine(output, "process-integrity.txt"), $"{integrity}\n");
        using (var process = Process.GetCurrentProcess())
        {
            bool overlayLoaded = process.Modules.Cast<ProcessModule>().Any(m => string.Equals(m.ModuleName, "GPP64.dll", StringComparison.OrdinalIgnoreCase));
            File.WriteAllText(Path.Combine(output, "overlay-compatibility.json"), JsonSerializer.Serialize(new {
                StartupCompatibility.OverlayDetected, StartupCompatibility.ExtensionPointsDisabled,
                RenderMode = RenderOptions.ProcessRenderMode.ToString(), GppModuleLoaded = overlayLoaded
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (shell.HasError) throw new InvalidOperationException("启动档案加载失败：" + shell.Status);
        var watch = Stopwatch.StartNew();
        var timings = new List<object>();
        var pages = new Dictionary<string, object>();
        foreach (int round in new[] { 0, 1, 2 })
        foreach (string page in new[] { "设置", "我的", "历史", "总览", "角色", "武器", "复刻日历" })
        {
            var navigation = Stopwatch.StartNew();
            shell.ShowSection(page); await shell.NavigationReady.WaitAsync(TimeSpan.FromSeconds(10));
            await window.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.Normal);
            await NextFrameAsync();
            timings.Add(new { Page = page, Cached = round > 0, FirstFrameMs = navigation.Elapsed.TotalMilliseconds });
            if (round == 0) pages[page] = shell.CurrentPage!;
            else if (shell.CurrentRecords.Count > 0 && !ReferenceEquals(pages[page], shell.CurrentPage))
                throw new InvalidOperationException("缓存页面在切换时被重建：" + page);
            await Task.Delay(320); // 分开测量下一帧与完整入场动画，不把固定等待算作首帧时间。
            if (shell.HasError) throw new InvalidOperationException("启动检查出现错误：" + shell.Status);
        }
        shell.ShowSection("设置"); await shell.NavigationReady; window.UpdateLayout();
        var combos = Descendants<ComboBox>(window).ToArray();
        var stylePicker = combos.Single(c => AutomationProperties.GetName(c) == "选择角色主题");
        var modePicker = combos.Single(c => AutomationProperties.GetName(c) == "选择白天、夜晚或跟随系统");
        var backdrop = Descendants<ThemeBackdrop>(window).Single();
        await backdrop.Ready.WaitAsync(TimeSpan.FromSeconds(10));
        var wallpaper = Descendants<Image>(backdrop).Single();
        if (wallpaper.Effect is not null || wallpaper.Source is BitmapSource { IsFrozen: false })
            throw new InvalidOperationException("壁纸仍使用实时效果或可变位图。");
        var source = wallpaper.Source;
        // 通过页面上真实的选择控件触发绑定，核对选择、全局状态与最终画刷，不能只赋值 VM。
        foreach (var style in shell.Styles)
        foreach (var mode in shell.Themes)
        {
            var themeWatch = Stopwatch.StartNew();
            stylePicker.SelectedItem = style; modePicker.SelectedItem = mode;
            await Dispatcher.Yield(DispatcherPriority.DataBind);
            if (shell.SelectedStyle.Key != style.Key || ThemeService.StyleKey != style.Key || ThemeService.Appearance.StyleKey != style.Key
                || shell.SelectedTheme.Value != mode.Value || ThemeService.Appearance.Theme != mode.Value)
                throw new InvalidOperationException("主题选择没有同步到实际外观。");
            var expected = (Color)ColorConverter.ConvertFromString(ThemeService.IsDark ? style.NightAccent : style.Accent);
            if (((SolidColorBrush)Application.Current.Resources["Gold"]).Color != expected)
                throw new InvalidOperationException("主题选择后界面画刷没有变化。");
            WindowChromeVerifier.VerifyCaption(window, output);
            await NextFrameAsync();
            timings.Add(new { Theme = style.Key, Mode = mode.Value, FirstFrameMs = themeWatch.Elapsed.TotalMilliseconds });
            shell.OverlayOpacity = shell.OverlayOpacity < .6 ? .72 : .45;
            if (ThemeService.StyleKey != style.Key || ThemeService.Appearance.StyleKey != style.Key || ThemeService.Preference != mode.Value)
                throw new InvalidOperationException("调整透明度后主题被旧状态覆盖。");
            if (!ReferenceEquals(source, wallpaper.Source)) throw new InvalidOperationException("换主题重新生成了同一张壁纸。");
            await Task.Delay(60);
        }
        await WindowChromeVerifier.VerifyMoveAsync(window, wallpaper, output);
        File.WriteAllText(Path.Combine(output, "interaction-timings.json"), JsonSerializer.Serialize(timings, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "startup-verification.txt"), $"正常渲染启动与页面/主题切换通过；记录 {shell.CurrentRecords.Count}；用时 {watch.ElapsedMilliseconds}ms。\n");
    }
    private static async Task NextFrameAsync()
    {
        var frame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Rendered(object? sender, EventArgs e) => frame.TrySetResult();
        CompositionTarget.Rendering += Rendered;
        try { await frame.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { CompositionTarget.Rendering -= Rendered; }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is UIElement { IsVisible: false }) continue;
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
