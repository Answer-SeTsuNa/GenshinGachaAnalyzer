using System.Diagnostics;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GenshinGachaAnalyzer.Core.Models;
using GenshinGachaAnalyzer.Core.Services;
using GenshinGachaAnalyzer.WPF.ViewModels;
using GenshinGachaAnalyzer.WPF.Views;
using LiveChartsCore.SkiaSharpView;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

/// <summary>开发验证：渲染真实 WPF 页面，检查 Binding 错误；测试资料必须通过同一 UIGF 导入服务。</summary>
internal static class UiVerifier
{
    public static async Task RunAsync(MainWindow window, MainViewModel shell, DatabaseService database, string output, string? fixture)
    {
        using var listener = new TextWriterTraceListener(Path.Combine(output, "bindings.log"));
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        try
        {
            if (Environment.GetCommandLineArgs().Contains("--verify-catalog-online"))
            {
                await VerifyOnlineCatalogAsync(window, shell, output); listener.Flush();
                File.WriteAllText(Path.Combine(output, "ui-verification.txt"), "公开资料在线更新与离线缓存验证通过，不使用真实档案。\n");
                return;
            }
            if (Environment.GetCommandLineArgs().Contains("--verify-login-only"))
            {
                await VerifyMiyousheLoginAsync(window, output); listener.Flush();
                File.WriteAllText(Path.Combine(output, "ui-verification.txt"), "米游社登录专项验证通过。\n");
                return;
            }
            int decoded = await Task.Run(() =>
            {
                int count = 0;
                foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Resources", "Banners")))
                {
                    var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 32; image.UriSource = new Uri(file); image.EndInit(); image.Freeze();
                    if (image.PixelHeight < 1) throw new InvalidOperationException("卡池图片解码失败：" + Path.GetFileName(file));
                    count++;
                }
                return count;
            });
            if (decoded != shell.Catalog.Events.Select(e => e.BannerFile).Distinct().Count())
                throw new InvalidOperationException("本地卡池图片数量不完整。");
            File.WriteAllText(Path.Combine(output, "banner-assets.txt"), $"完整解码验证通过：{decoded} 张本地卡池图片。\n");
            int itemIcons = await Task.Run(() =>
            {
                int count = 0;
                foreach (var file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Resources", "Items"), "*.png"))
                {
                    var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 32; image.UriSource = new Uri(file); image.EndInit(); image.Freeze();
                    if (image.PixelHeight < 1) throw new InvalidOperationException("物品图片解码失败：" + Path.GetFileName(file));
                    count++;
                }
                return count;
            });
            File.WriteAllText(Path.Combine(output, "item-assets.txt"), $"完整解码验证通过：{itemIcons} 张本地角色 / 武器图片。\n");
            shell.SelectedDisplayScale = shell.DisplayScales.Single(s => s.Value == "1");
            window.Width = 1640; window.Height = 1020;
            shell.SelectedTheme = shell.Themes.Single(t => t.Value == "Light");
            await RenderAsync(window, Path.Combine(output, "01-welcome.png"));
            if (fixture is not null)
            {
                var service = new UigfService();
                // 第二个合成账号移除标准允许省略的元数据，重现用户的最小 UIGF 文件。
                // 修改的仅是隔离目录内的测试 JSON，仍通过正式预检与入库服务。
                var input = JsonNode.Parse(await File.ReadAllTextAsync(Path.GetFullPath(fixture)))!;
                if (input["hk4e"] is JsonArray inputAccounts && inputAccounts.Count >= 2)
                    foreach (var entry in inputAccounts[1]!["list"]!.AsArray())
                    {
                        entry!.AsObject().Remove("rank_type"); entry.AsObject().Remove("name"); entry.AsObject().Remove("item_type");
                    }
                string minimalFixture = Path.Combine(output, "synthetic-missing-metadata.json");
                await File.WriteAllTextAsync(minimalFixture, input.ToJsonString());
                var import = await service.PreflightAsync(minimalFixture);
                database.Insert(import.Records, import.Accounts);
                await shell.InitializeAsync();
                string? originalUid = shell.SelectedAccount?.Uid;
                shell.ToggleUidPrivacyCommand.Execute(null);
                if (!shell.HideUid || shell.UidText != originalUid![..^4] + "****"
                    || shell.Accounts.Where(a => a.Uid is not null).Any(a => !a.Display.EndsWith("****"))
                    || !new SettingsService(Path.Combine(output, "settings.json")).Load().HideUid)
                    throw new InvalidOperationException("UID 隐藏或设置保存失败。");
                await RenderAsync(window, Path.Combine(output, "02-uid-hidden.png"));
                if (((System.Windows.Shapes.Path)window.FindName("UidPrivacySlash")).Visibility != Visibility.Visible)
                    throw new InvalidOperationException("隐藏 UID 时眼睛未显示斜杠。");
                shell.ToggleUidPrivacyCommand.Execute(null);
                if (shell.SelectedAccount?.Uid != originalUid || shell.UidText != originalUid)
                    throw new InvalidOperationException("UID 隐藏修改了原始账号或未恢复显示。");
                await RenderAsync(window, Path.Combine(output, "02-dashboard.png"));
                if (((System.Windows.Shapes.Path)window.FindName("UidPrivacySlash")).Visibility != Visibility.Collapsed)
                    throw new InvalidOperationException("显示 UID 时眼睛仍显示斜杠。");
                foreach (var card in Descendants<Controls.PoolCard>(window))
                {
                    if (card.DataContext is not PoolViewModel pool) continue;
                    var metrics = (Grid)card.FindName("MetricsPanel");
                    var average = (Border)card.FindName("AverageTile");
                    var upAverage = (Border)card.FindName("UpAverageTile");
                    var extrema = (Grid)card.FindName("ExtremaColumn");
                    if (extrema.TranslatePoint(new Point(), metrics).X <= average.TranslatePoint(new Point(), metrics).X)
                        throw new InvalidOperationException("最欧最非未位于平均抽数右侧。");
                    if (upAverage.IsVisible != pool.Analysis.UpAverage.HasValue)
                        throw new InvalidOperationException("缺少 UP 平均抽数时仍显示占位。");
                }
                // 用实际渲染控件检查低抽数等比例填充，防止头像遮挡或模板阈值回归。
                var progress = new Controls.ProportionalProgress { Width = 270, Height = 40, Maximum = 90 };
                progress.Measure(new Size(270, 40)); progress.Arrange(new Rect(0, 0, 270, 40));
                foreach (int count in new[] { 1, 5, 10, 20, 80, 90 })
                {
                    progress.Value = count;
                    if (Math.Abs(progress.FillWidth - count * 3) > .001)
                        throw new InvalidOperationException("五星背景进度未按抽数线性填充。");
                }
                File.WriteAllText(Path.Combine(output, "overview-layout.txt"), "最欧最非位于均值右侧；无 UP 均值隐藏；1/5/10/20/80/90 抽的填充宽度均等比例。\n");
                if (shell.CurrentPage is DashboardViewModel proportions)
                {
                    proportions.RatioCommand.Execute(null);
                    if (!proportions.IsRatioOpen || Math.Abs(proportions.Shares.Sum(s => s.Percent) - 100) > .001)
                        throw new InvalidOperationException("五星比例面板分母或开关错误。");
                    await RenderAsync(window, Path.Combine(output, "02-five-star-ratio.png"));
                    proportions.CloseRatioCommand.Execute(null);
                }
                var detailButtons = Descendants<Button>(window).Where(b => b.Content?.ToString() == "查看详细  →").ToArray();
                if (detailButtons.Length != 4 || detailButtons.Any(b => b.Command is null || b.CommandParameter is not PoolViewModel))
                    throw new InvalidOperationException("仪表盘查看详细命令绑定缺失。");
                detailButtons[0].Command!.Execute(detailButtons[0].CommandParameter);
                if (shell.CurrentPage is not DetailViewModel) throw new InvalidOperationException("仪表盘详情导航失败。");
                foreach (var pool in PoolCatalog.MainPools)
                {
                    shell.ShowDetail(pool);
                    await RenderAsync(window, Path.Combine(output, $"03-detail-{pool.Type}.png"));
                    if (shell.CurrentPage is DetailViewModel detail)
                    {
                        if (detail.RecordsTable.Rows.Count != Math.Min(50, detail.Pool.Analysis.Total))
                            throw new InvalidOperationException("详情原始记录表格未限制每页 50 条。");
                        if (detail.RecordsTable.Rows.Any(r => r.Record.Uid != shell.SelectedAccount?.Uid || r.Record.PoolType != pool.Type))
                            throw new InvalidOperationException("详情表格混入其他 UID 或卡池记录。");
                        if (pool.Type == "301")
                        {
                            var table = Descendants<Controls.RecordTable>(window).First(t => t.IsVisible);
                            table.BringIntoView();
                            await RenderAsync(window, Path.Combine(output, "03-detail-original-table.png"));
                            detail.RecordsTable.NextCommand.Execute(null);
                            if (detail.Pool.Analysis.Total > 50 && !detail.RecordsTable.PageText.StartsWith("第 2"))
                                throw new InvalidOperationException("详情表格下一页未生效。");
                            detail.RecordsTable.PreviousCommand.Execute(null);
                        }
                        var axis = detail.YAxes[0];
                        if (!axis.ForceStepToMin || axis.MinStep < 1 || axis.MaxLimit / axis.MinStep > 8)
                            throw new InvalidOperationException("直方图 Y 轴刻度过密。");
                        var many = new PoolViewModel(detail.Pool.Analysis with { Golds = Enumerable.Repeat(detail.Pool.Analysis.Golds.First(), 200).ToArray() });
                        if (many.GridGoldItems.Count != 40) throw new InvalidOperationException("出金网格未限制初次创建的物品卡数。");
                        many.LoadMoreGrid();
                        if (many.GridGoldItems.Count != 80) throw new InvalidOperationException("出金网格分批加载失败。");
                        detail.SelectBin(0);
                        if (detail.Timeline.Any(g => g.Pulls is < 1 or > 10)) throw new InvalidOperationException("图表区间筛选失败。");
                    }
                }
                var preview = new ImportPreviewWindow { DataContext = new ImportPreviewViewModel(import), ShowInTaskbar = false, ShowActivated = false };
                preview.Show(); await RenderAsync(preview, Path.Combine(output, "04-import-preview.png")); preview.Close();
                var export = new ExportWindow { DataContext = new ExportSelectionViewModel(database.GetAccounts()), ShowInTaskbar = false, ShowActivated = false };
                export.Show(); await RenderAsync(export, Path.Combine(output, "05-export.png")); export.Close();
                var original = import.Records[0];
                var conflictReport = database.Insert([original with { Name = "合成冲突名称" }]);
                if (conflictReport.Details.Count != 1 || conflictReport.LogPath is null)
                    throw new InvalidOperationException("未生成可读的冲突详情日志。");
                var result = new WriteResultWindow { DataContext = new WriteResultViewModel(conflictReport), ShowInTaskbar = false, ShowActivated = false };
                result.Show(); await RenderAsync(result, Path.Combine(output, "05-result.png")); result.Close();
                var addAccount = new AddAccountWindow { ShowInTaskbar = false, ShowActivated = false };
                addAccount.Show(); await RenderAsync(addAccount, Path.Combine(output, "05-add-account.png")); addAccount.Close();
                var clearModel = new ClearAccountViewModel(import.Accounts[0].Uid, import.Records.Count);
                if (clearModel.CanConfirm) throw new InvalidOperationException("清空确认不能默认通过。");
                clearModel.Confirmation = import.Accounts[0].Uid;
                if (!clearModel.CanConfirm) throw new InvalidOperationException("完整 UID 确认失败。");
                var clear = new ClearAccountWindow { DataContext = clearModel, ShowInTaskbar = false, ShowActivated = false };
                clear.Show(); await RenderAsync(clear, Path.Combine(output, "05-clear-account.png")); clear.Close();
                shell.ShowDashboard(); await shell.NavigationReady;
                if (shell.CurrentPage is DashboardViewModel dashboard) foreach (var pool in dashboard.Pools) pool.IsGridLayout = true;
                await RenderAsync(window, Path.Combine(output, "06-overview-grid.png"));
                if (shell.CurrentPage is DashboardViewModel anchoring)
                {
                    var pool = anchoring.Pools[0]; pool.AnchorCommand.Execute("min");
                    await RenderAsync(window, Path.Combine(output, "06-grid-anchor.png"));
                    if (pool.AnchorTarget is null || !pool.AnchorTarget.IsHighlighted || !pool.GridGoldItems.Contains(pool.AnchorTarget)) throw new InvalidOperationException("网格定位与高亮失败。");
                    pool.IsUpCycle = true;
                    if (pool.GoldItems.Any(g => shell.Catalog.IsCurrentUp(g.Entry.Record) == false)) throw new InvalidOperationException("合并模式仍包含已知非 UP。");
                    await RenderAsync(window, Path.Combine(output, "06-up-cycle.png")); pool.IsUpCycle = false;
                }
                if (Descendants<Controls.PoolCard>(window).Any(card => card.ActualWidth > window.ActualWidth / 2))
                    throw new InvalidOperationException("网格显示把单个卡池撑宽，四列宽度约束失败。");
                if (shell.CurrentPage is DashboardViewModel collapsed) foreach (var pool in collapsed.Pools) pool.IsExpanded = false;
                await RenderAsync(window, Path.Combine(output, "06-overview-collapsed.png"));
                if (shell.CurrentPage is DashboardViewModel expanded) foreach (var pool in expanded.Pools) pool.IsExpanded = true;
                await RenderAsync(window, Path.Combine(output, "06-expanded.png"));
                if (SystemParameters.ClientAreaAnimation && shell.CurrentPage is DashboardViewModel motion)
                {
                    var reveal = Descendants<Controls.AnimatedReveal>(window).First();
                    await ObserveIntermediateAsync(reveal, Controls.AnimatedReveal.RevealProperty, () => motion.Pools[0].IsExpanded = false);
                    await Task.Delay(300);
                    if (reveal.Reveal > .001) throw new InvalidOperationException("收起动画未完成。");
                    motion.Pools[0].IsExpanded = true;
                }
                window.Width = 1180; window.Height = 800;
                await RenderAsync(window, Path.Combine(output, "06-small-window.png"));
                window.Height = 650;
                await RenderAsync(window, Path.Combine(output, "06-low-height-window.png"));
                window.Width = 1640; window.Height = 1020;
                foreach (string page in new[] { "历史", "角色", "武器", "复刻日历", "我的" })
                {
                    var timer = Stopwatch.StartNew();
                    shell.ShowSection(page); await shell.NavigationReady;
                    if (shell.CurrentPage is CountdownViewModel readyCountdown) await readyCountdown.Ready;
                    await RenderAsync(window, Path.Combine(output, $"08-{page}.png"));
                    if (page == "复刻日历")
                    {
                        File.WriteAllText(Path.Combine(output, "countdown-performance.txt"), $"首次打开并渲染：{timer.Elapsed.TotalMilliseconds:F0} ms\n");
                        if (timer.Elapsed > TimeSpan.FromSeconds(5)) throw new InvalidOperationException("复刻日历首帧耗时超过 5 秒。");
                        int realized = Descendants<ListBoxItem>(window).Count();
                        int count = ((CountdownViewModel)shell.CurrentPage!).Sections.Sum(s => s.Items.Count);
                        if (count > 50 && realized >= count) throw new InvalidOperationException("复刻日历列表未虚拟化，创建了全部条目。");
                    }
                    if (shell.CurrentPage is HistoryViewModel history)
                    {
                        if (history.Groups.SelectMany(g => g.Group.Records).Any(r => r.PoolType == "200")) throw new InvalidOperationException("历史混入常驻祈愿。");
                        if (!history.Groups.Any(g => g.Group.Event is not null && g.Group.Records.Count == 0)) throw new InvalidOperationException("历史未显示无本地记录的排期。");
                        history.TimelineMode = false;
                        if (history.Groups.Any(g => g.Group.Records.Count == 0)) throw new InvalidOperationException("抽卡足迹包含无记录卡池。");
                        await RenderAsync(window, Path.Combine(output, "08-history-footprints.png"));
                        history.TimelineMode = true;
                        history.Search = "10000046";
                        if (history.Groups.Count != 0) throw new InvalidOperationException("历史仍支持物品 ID 查询。");
                        history.Search = "";
                        history.SelectedGroup = history.Groups.FirstOrDefault(g => g.Group.Event is not null && g.Group.Records.Any(r => r.RankType == "5")) ?? history.SelectedGroup;
                        await RenderAsync(window, Path.Combine(output, "08-history-period.png"));
                        int initial = history.Groups.Sum(g => g.Group.Records.Count);
                        history.ShowSummary = false;
                        if (history.Groups.Sum(g => g.Group.Records.Count) != initial) throw new InvalidOperationException("历史筛选丢失记录。");
                        var paged = history.Groups.FirstOrDefault(g => g.Group.Records.Count > HistoryViewModel.PageSize);
                        if (paged is not null)
                        {
                            history.SelectedGroup = paged; var firstId = history.Records[0].RecordId;
                            if (history.Records.Count != HistoryViewModel.PageSize) throw new InvalidOperationException("历史首分页大小错误。");
                            history.NextPageCommand.Execute(null);
                            if (history.PageIndex != 2 || history.Records[0].RecordId == firstId || history.Records.Count > HistoryViewModel.PageSize) throw new InvalidOperationException("历史分页失败。");
                            history.PreviousPageCommand.Execute(null);
                            if (history.PageIndex != 1 || history.Records[0].RecordId != firstId) throw new InvalidOperationException("返回历史首页丢失记录。");
                        }
                        // 将右侧滚动至原始记录，检查分页与星级颜色的实际展示。
                        var right = Descendants<ScrollViewer>(window).FirstOrDefault(v => v.Content is StackPanel && v.ScrollableHeight > 0);
                        right?.ScrollToBottom();
                        await RenderAsync(window, Path.Combine(output, "08-history-records.png"));
                        history.SelectedEra = history.Eras.Single(e => e.Value == "2");
                        if (history.Groups.Any(g => g.Group.Event?.Version.Split('.')[0] != "2")) throw new InvalidOperationException("大版本筛选失败。");
                        await RenderAsync(window, Path.Combine(output, "08-history-era-inazuma.png")); history.SelectedEra = history.Eras[0];
                    }
                    if (shell.CurrentPage is CollectionViewModel collection && collection.Sections.FirstOrDefault()?.Items.FirstOrDefault() is { } item)
                    {
                        collection.SelectItemCommand.Execute(item);
                        if (!collection.HasSelection || collection.SelectedRecords.Any(r => r.Item.Id != item.Id)) throw new InvalidOperationException("物品明细选择失败。");
                        await RenderAsync(window, Path.Combine(output, $"08-{page}-selected.png"));
                        collection.Search = item.Id;
                        if (collection.HasItems) throw new InvalidOperationException("物品汇总仍支持 ID 查询。");
                        collection.Search = "";
                    }
                    if (shell.CurrentPage is CountdownViewModel countdown && countdown.Sections.SelectMany(s => s.Items).FirstOrDefault(i => i.Name == "胡桃") is { } up)
                    {
                        countdown.SelectItemCommand.Execute(up);
                        if (countdown.Histories.Count != 5 || !countdown.HistoryTitle.Contains("5 次")) throw new InvalidOperationException("胡桃 UP 排期没有完整展示五次。");
                        await RenderAsync(window, Path.Combine(output, "08-countdown-selected.png"));
                    }
                }
                // 热缓存直接呈现，避免整页动画拖慢软件渲染。
                shell.ShowSection("历史"); await shell.NavigationReady;
                var active = Descendants<Controls.TransitionPresenter>(window).Single().Children.OfType<ContentPresenter>().Single(p => p.Visibility == Visibility.Visible);
                window.UpdateLayout();
                if (active.Opacity != 1 || active.HasAnimatedProperties)
                    throw new InvalidOperationException("缓存页面仍在执行整页淡入动画。");
                await RenderAsync(window, Path.Combine(output, "09-page-transition.png"));
                shell.ShowDashboard(); await shell.NavigationReady; shell.SelectedDisplayScale = shell.DisplayScales.Single(s => s.Value == "1.5");
                window.Width = 2800; window.Height = 1600;
                await RenderAsync(window, Path.Combine(output, "09-large-screen-150-percent.png"));
                shell.SelectedDisplayScale = shell.DisplayScales.Single(s => s.Value == "1");
                window.Width = 1640; window.Height = 1020;
                shell.SelectedTheme = shell.Themes.Single(t => t.Value == "Dark");
                shell.ShowDashboard(); await shell.NavigationReady; await RenderAsync(window, Path.Combine(output, "09-night-overview.png"));
                shell.ShowDetail(PoolCatalog.MainPools[0]); await RenderAsync(window, Path.Combine(output, "09-night-chart.png"));
                shell.ShowSection("历史"); await RenderAsync(window, Path.Combine(output, "09-night-history.png"));
                shell.ShowSection("复刻日历"); await RenderAsync(window, Path.Combine(output, "09-night-countdown.png"));
                shell.SettingsCommand.Execute(null); await RenderAsync(window, Path.Combine(output, "09-night-settings.png"));
                shell.SelectedTheme = shell.Themes.Single(t => t.Value == "Light");
                shell.ShowSection("我的"); await shell.NavigationReady;
                if (shell.CurrentPage is PersonalViewModel footprint)
                {
                    footprint.ExcludeStandard = false;
                    if (((ColumnSeries<int>)footprint.Series[0]).Values!.Sum() != shell.CurrentRecords.Count)
                        throw new InvalidOperationException("月度足迹包含常驻时与账号记录总数不一致。");
                    await RenderAsync(window, Path.Combine(output, "11-months-with-standard.png"));
                    footprint.ExcludeStandard = true;
                    if (((ColumnSeries<int>)footprint.Series[0]).Values!.Sum() != shell.CurrentRecords.Count(r => r.PoolType is "301" or "302" or "500"))
                        throw new InvalidOperationException("月度足迹过滤常驻未生效。");
                }
                if (shell.Styles.Count != 6 || shell.Styles.Any(s => s.Key == "Puppet" || s.Name.Contains('·')))
                    throw new InvalidOperationException("已移除主题或主题旧名称仍在显示。");
                var expectedElements = new Dictionary<string, string> { ["Ayaka"] = "cryo", ["Klee"] = "pyro", ["Zhongli"] = "geo", ["Furina"] = "hydro", ["Varesa"] = "electro" };
                foreach (var style in shell.Styles)
                {
                    shell.SelectedStyle = style;
                    if (style.Key == "Gear")
                    {
                        if (Application.Current.Resources["ThemeDrawing"] is not DrawingGroup pupil || !pupil.IsFrozen || pupil.Children.Count != 2 || pupil.Children.Any(c => c is not GeometryDrawing))
                            throw new InvalidOperationException("木偶未使用独立眼内齿轮矢量纹饰。");
                        var contour = ((GeometryDrawing)pupil.Children[1]).Geometry;
                        if (contour.FillContains(new Point(160, 160)) || !contour.FillContains(new Point(160, 40)) || Math.Abs(pupil.Bounds.Width - pupil.Bounds.Height) > .01)
                            throw new InvalidOperationException("木偶齿轮中心孔、齿尖或正圆比例有误。");
                    }
                    else
                    {
                    var icon = Services.ThemeArtwork.IconFor(style.Key);
                    if (Services.ThemeArtwork.ElementFor(style.Key) != expectedElements[style.Key] || !icon.IsFrozen || icon.PixelWidth != 84 || icon.PixelHeight != 84)
                        throw new InvalidOperationException("元素图标映射、共享缓存或原图尺寸有误。");
                    if (Application.Current.Resources["ThemeDrawing"] is not DrawingGroup drawing || drawing.Children.Single() is not ImageDrawing originalIconDrawing || !ReferenceEquals(originalIconDrawing.ImageSource, icon))
                        throw new InvalidOperationException("主题预览未使用官方元素原图。");
                    }
                    await RenderAsync(window, Path.Combine(output, $"11-theme-{style.Key}.png"));
                    foreach (var mode in new[] { "Light", "Dark" })
                    {
                        shell.SelectedTheme = shell.Themes.Single(t => t.Value == mode);
                        shell.SettingsCommand.Execute(null);
                        await RenderAsync(window, Path.Combine(output, $"12-theme-{style.Key}-{mode}-settings.png"));
                        shell.ShowDashboard();
                        await RenderAsync(window, Path.Combine(output, $"13-theme-{style.Key}-{mode}-dashboard.png"));
                        shell.SettingsCommand.Execute(null);
                        var emblem = new Controls.ThemeEmblem { Width = 640, Height = 640 };
                        emblem.Measure(new Size(640, 640)); emblem.Arrange(new Rect(0, 0, 640, 640));
                        var image = new RenderTargetBitmap(640, 640, 96, 96, PixelFormats.Pbgra32); image.Render(emblem);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                        using var imageFile = File.Create(Path.Combine(output, $"12-emblem-{style.Key}-{mode}.png")); encoder.Save(imageFile);
                    }
                    shell.ShowSection("我的"); await shell.NavigationReady;
                }
                shell.SelectedStyle = shell.Styles[0];
                shell.SemiTransparent = true; shell.OverlayOpacity = .45; shell.PanelOpacity = .7;
                shell.SettingsCommand.Execute(null);
                await RenderAsync(window, Path.Combine(output, "11-acrylic-settings.png"));
                shell.SemiTransparent = false;
                await RenderAsync(window, Path.Combine(output, "11-opaque-settings.png"));
                shell.SemiTransparent = true; shell.OverlayOpacity = .72; shell.PanelOpacity = .88;
                shell.DeveloperMode = true;
                DiagnosticLog.Error("合成错误验证：凭据应隐藏", new InvalidOperationException("authkey=SYNTHETIC_TOKEN_TO_REDACT"));
                if (DiagnosticLog.ReadLatest().Contains("SYNTHETIC_TOKEN_TO_REDACT")) throw new InvalidOperationException("开发者日志泄露凭据。");
                var devLog = new DeveloperLogWindow { ShowInTaskbar = false, ShowActivated = false };
                devLog.Show(); await RenderAsync(devLog, Path.Combine(output, "11-developer-log.png")); devLog.Close();
                shell.DeveloperMode = false;
                var url = new UrlWindow { ShowInTaskbar = false, ShowActivated = false }; url.Show();
                await RenderAsync(url, Path.Combine(output, "11-url-dialog.png")); url.Close();
                // 重新加载使缓存失效，模拟后台统计途中用户立即切至设置。
                await shell.InitializeAsync(); shell.ShowSection("角色"); var pendingNavigation = shell.NavigationReady;
                shell.SettingsCommand.Execute(null); await pendingNavigation;
                if (shell.CurrentPage is not SettingsViewModel) throw new InvalidOperationException("旧导航覆盖了新页面。");
                // 在隔离数据库重现删除后切换账号的刷新，断言图表数据与剩余原始记录均保留。
                var accounts = database.GetAccounts();
                if (accounts.Count >= 2)
                {
                    var remaining = database.GetRecords(accounts[1].Uid);
                    var analyzer = new AnalysisService(new CatalogBannerProvider(shell.Catalog), shell.Catalog);
                    var expected = PoolCatalog.MainPools.Select(p => analyzer.Analyze(remaining, p)).ToArray();
                    if (expected.Sum(p => p.Golds.Count) == 0 || expected.Sum(p => p.Histogram.Sum(b => b.Count)) == 0)
                        throw new InvalidOperationException("缺省星级的合成账号未从元数据恢复图表统计。");
                    database.ClearAccount(accounts[0].Uid); await shell.InitializeAsync(); shell.ShowDashboard(); await shell.NavigationReady;
                    if (shell.CurrentPage is not DashboardViewModel restored || restored.Pools.Where((p, i) => p.Analysis.Total != expected[i].Total || p.Analysis.Golds.Count != expected[i].Golds.Count || !p.Analysis.Histogram.Select(b => b.Count).SequenceEqual(expected[i].Histogram.Select(b => b.Count))).Any())
                        throw new InvalidOperationException("删除另一个账号后图表数据丢失。");
                    if (!remaining.SequenceEqual(database.GetRecords(accounts[1].Uid))) throw new InvalidOperationException("删除影响了其他账号记录。");
                    await RenderAsync(window, Path.Combine(output, "10-after-clear-other-account.png"));
                    shell.ShowDetail(PoolCatalog.MainPools[0]); await RenderAsync(window, Path.Combine(output, "10-after-clear-chart.png"));
                }
            }
            shell.SettingsCommand.Execute(null);
            await RenderAsync(window, Path.Combine(output, "07-settings.png"));
            shell.SelectedTheme = shell.Themes[0];
            // 从首帧到正常初始化完成，加载状态不得误显示为数据库故障。
            string loadingDirectory = Path.Combine(output, "synthetic-startup-loading");
            using var loadingClient = new HttpClient();
            var loadingShell = new MainViewModel(new DatabaseService(Path.Combine(loadingDirectory, "gacha.db")), new UigfService(),
                new AnalysisService(new CatalogBannerProvider()), new GachaLogService(loadingClient),
                new SettingsService(Path.Combine(loadingDirectory, "settings.json")), shell.Dialogs, null);
            bool falseFailurePage = false;
            loadingShell.PropertyChanged += (_, change) =>
            {
                if (change.PropertyName == nameof(MainViewModel.CurrentPage) && loadingShell.CurrentPage is DatabaseUnavailableViewModel)
                    falseFailurePage = true;
            };
            loadingShell.ShowDashboard(); await loadingShell.NavigationReady;
            if (loadingShell.CurrentPage is not LoadingViewModel || loadingShell.HasError)
                throw new InvalidOperationException("首次加载前显示了错误页。");
            window.DataContext = loadingShell;
            await RenderAsync(window, Path.Combine(output, "00-archive-loading.png"));
            await loadingShell.InitializeAsync();
            if (falseFailurePage || loadingShell.HasError || !loadingShell.ImportCommand.CanExecute(null))
                throw new InvalidOperationException("正常初始化期间曾显示数据库故障。");
            window.DataContext = shell;
            File.WriteAllText(Path.Combine(output, "archive-loading-verification.txt"), "首帧、初始化前导航及初始化全程不显示虚假数据库故障；成功后开放档案入口。\n");
            // 合成故障库：加载失败必须显示专门页面，禁止把错误渲染成零账号引导。
            string failureDirectory = Path.Combine(output, "synthetic-database-failure");
            Directory.CreateDirectory(failureDirectory);
            string failurePath = Path.Combine(failureDirectory, "gacha.db");
            const string brokenContent = "synthetic-not-a-database";
            await File.WriteAllTextAsync(failurePath, brokenContent);
            using var failureClient = new HttpClient();
            var failureShell = new MainViewModel(new DatabaseService(failurePath), new UigfService(),
                new AnalysisService(new CatalogBannerProvider()), new GachaLogService(failureClient),
                new SettingsService(Path.Combine(failureDirectory, "settings.json")), shell.Dialogs, null);
            await failureShell.InitializeAsync();
            if (!failureShell.HasError || failureShell.CurrentPage is not DatabaseUnavailableViewModel
                || !failureShell.ArchiveText.Contains("未加载") || failureShell.ImportCommand.CanExecute(null))
                throw new InvalidOperationException("数据库读取失败被当成空账号，或仍开放写入入口。");
            failureShell.ShowSection("我的"); await failureShell.NavigationReady;
            if (failureShell.CurrentPage is not DatabaseUnavailableViewModel)
                throw new InvalidOperationException("加载失败后导航显示了虚构的空统计。");
            window.DataContext = failureShell;
            await RenderAsync(window, Path.Combine(output, "14-database-unavailable.png"));
            if (await File.ReadAllTextAsync(failurePath) != brokenContent)
                throw new InvalidOperationException("损坏的合成数据库被覆盖。");
            // 仅在隔离目录把合成故障文件留存，再验证重试重新加载而非误走游戏获取。
            File.Move(failurePath, failurePath + ".preserved");
            failureShell.RetryCommand.Execute(null);
            for (int retry = 0; failureShell.IsBusy && retry < 150; retry++) await Task.Delay(20);
            if (failureShell.IsBusy || failureShell.HasError || failureShell.CurrentPage is DatabaseUnavailableViewModel
                || !failureShell.ImportCommand.CanExecute(null))
                throw new InvalidOperationException("故障解除后重试未恢复本地档案入口。");
            window.DataContext = shell;
            await VerifyFetchRoutingAsync(window, shell, output);
            await VerifyUpdatesAsync(window, shell, output);
            await VerifyCatalogUpdatesAsync(window, shell, output);
            await VerifyMiyousheLoginAsync(window, output);
            listener.Flush();
            File.WriteAllText(Path.Combine(output, "ui-verification.txt"), "WPF 页面渲染、详情区间筛选、五个页签、历史筛选、物品明细、复刻日历历史与清空确认验证完成。全部档案为合成测试资料，请检查 PNG 和 bindings.log。\n");
        }
        finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(listener); }
    }
    private static async Task VerifyMiyousheLoginAsync(MainWindow owner, string output)
    {
        int requests = 0;
        using var client = new HttpClient(new SyntheticFetchHandler(() =>
        {
            requests++;
            return new(System.Net.HttpStatusCode.OK) { Content = new StringContent(requests switch
            {
                1 => "{\"retcode\":0,\"data\":{\"url\":\"https://user.mihoyo.com/?ticket=SYNTHETIC_ONLY\",\"ticket\":\"SYNTHETIC_ONLY\"}}",
                2 => "{\"retcode\":0,\"data\":{\"status\":\"Confirmed\",\"tokens\":[{\"token_type\":1,\"token\":\"SYNTHETIC_LOGIN_ONLY\"}],\"user_info\":{\"aid\":\"12345678\",\"mid\":\"SYNTHETIC_MID\"}}}",
                3 => "{\"retcode\":0,\"data\":{\"list\":[{\"game_uid\":\"100000001\",\"nickname\":\"合成测试账号\",\"region\":\"cn_gf01\",\"region_name\":\"天空岛\"}]}}",
                _ => "{\"retcode\":0,\"data\":{\"authkey\":\"SYNTHETIC_WISH_ONLY\"}}"
            }) };
        }));
        var login = new MiyousheLoginWindow(new MiyousheLoginService(client), Path.Combine(output, "login-browser-cache")) { Owner = owner };
        try
        {
            login.Show(); await RenderAsync(login, Path.Combine(output, "16-login-privacy.png"));
            if (requests != 0 || login.HasLoginSession) throw new InvalidOperationException("未同意隐私说明前已发出登录请求。");
            login.ConsentButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await login.OperationReady.WaitAsync(TimeSpan.FromSeconds(10));
            if (!login.HasLoginSession || login.RolePicker.Items.Count != 1 || login.RolePanel.Visibility != Visibility.Visible)
                throw new InvalidOperationException("米游社合成登录未显示账号确认。");
            await RenderAsync(login, Path.Combine(output, "16-login-account-confirmation.png"));
            if (login.RoleNickname.Text != "合成测试账号" || login.RoleUid.Text != "UID：100000001" || login.RoleServer.Text != "服务器：天空岛"
                || login.RolePicker.Visibility != Visibility.Collapsed || login.QrImage.Source is not null
                || login.StatusText.Text.Contains("JSON", StringComparison.OrdinalIgnoreCase) || login.StatusText.Text.Contains("http"))
                throw new InvalidOperationException("登录成功页没有清晰显示昵称、UID、服务器，或暴露了开发信息。");
            login.FetchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            for (int attempt = 0; login.IsVisible && attempt < 100; attempt++) await Task.Delay(20);
            if (login.IsVisible || login.HasLoginSession || login.Credential?.ExpectedUid != "100000001" || requests != 4)
                throw new InvalidOperationException("米游社获取授权或关闭时的临时会话清理失败。");
            var cancelled = new MiyousheLoginWindow(new MiyousheLoginService(client), Path.Combine(output, "login-browser-cache")) { Owner = owner };
            cancelled.Show(); cancelled.Close();
            if (cancelled.Credential is not null || cancelled.HasLoginSession || requests != 4)
                throw new InvalidOperationException("取消登录仍保存授权或产生额外请求。");
            if (Environment.GetCommandLineArgs().Contains("--verify-official-login-page"))
            {
                var browserLogin = new MiyousheLoginWindow(new MiyousheLoginService(client), Path.Combine(output, "login-browser-cache")) { Owner = owner };
                try { browserLogin.Show(); await browserLogin.VerifyOfficialAccountPageAsync(output); }
                finally { browserLogin.Close(); }
                for (int attempt = 0; browserLogin.IsVisible && attempt < 300; attempt++) await Task.Delay(20);
                if (browserLogin.IsVisible || browserLogin.HasLoginSession) throw new InvalidOperationException("官方网页登录窗口未正确清理关闭。");
            }
            File.WriteAllText(Path.Combine(output, "miyoushe-login-verification.txt"), "隐私说明前不联网、扫码确认、UID 选择、查询授权、取消与临时会话清理通过。仅合成响应，不使用真实登录凭据。\n");
        }
        finally { login.Close(); }
    }
    private static async Task VerifyOnlineCatalogAsync(MainWindow window, MainViewModel original, string output)
    {
        string directory = Path.Combine(output, "online-catalog"); Directory.CreateDirectory(directory);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        var updater = new WishCatalogUpdateService(client, Path.Combine(directory, "catalog", "snapshot.json"));
        var target = new MainViewModel(new DatabaseService(Path.Combine(directory, "gacha.db")), new UigfService(), new AnalysisService(new CatalogBannerProvider()),
            new GachaLogService(client), new SettingsService(Path.Combine(directory, "settings.json")), new SyntheticFetchDialogs(), null, autoUpdateCatalog: true, catalogUpdates: updater);
        await target.InitializeAsync(); await target.CatalogUpdateReady.WaitAsync(TimeSpan.FromSeconds(70));
        if (target.Catalog.UpdatedAt is null || !target.CatalogStatus.Contains("检查完成") || target.Catalog.Events.Count < original.Catalog.Events.Count)
            throw new InvalidOperationException("真实公开资料更新失败：" + target.CatalogStatus);
        target.SettingsCommand.Execute(null); window.DataContext = target;
        await RenderAsync(window, Path.Combine(output, "17-online-catalog-settings.png"));
        var latest = target.Catalog.Events.OrderByDescending(e => e.From).First();
        RuntimeArtworkCache.Register(target.Catalog);
        if (latest.BannerFile is not null && latest.BannerUrl is not null)
        {
            var artwork = new WishCatalogService(target.Catalog.Items, [latest with { BannerFile = "online-test-current-banner.jpg" }]);
            RuntimeArtworkCache.Register(artwork);
            if (await RuntimeArtworkCache.ResolveAsync("Banners", "online-test-current-banner.jpg") is null) throw new InvalidOperationException("当前卡池官方图片不可用。");
        }
        // 找一个安装包没有的新图标，验证更新后不需要重新安装即可显示。
        var newItem = target.Catalog.Items.FirstOrDefault(i => WishCatalogUpdateService.IsOfficialAsset(i.Icon) && !File.Exists(Path.Combine(AppContext.BaseDirectory, "Resources", "Items", i.Id + ".png")));
        newItem ??= target.Catalog.Items.First(i => WishCatalogUpdateService.IsOfficialAsset(i.Icon)) with { Id = "999990001" };
        RuntimeArtworkCache.Register(new WishCatalogService([newItem], []));
        if (await RuntimeArtworkCache.ResolveAsync("Items", newItem.Id + ".png") is null) throw new InvalidOperationException("新增物品官方图片下载失败。");
        var offline = updater.LoadCache(new WishCatalogService());
        if (offline is null || offline.Events.Length != target.Catalog.Events.Count) throw new InvalidOperationException("更新资料无法在离线启动时恢复。");
        File.WriteAllText(Path.Combine(output, "catalog-online-verification.txt"), $"公开数据更新通过：排期 {target.Catalog.Events.Count}，物品 {target.Catalog.Items.Count}；覆盖至 {target.Catalog.CoveredThrough:yyyy-MM-dd}。缓存读取、官方图片加载通过。未发送 UID 或登录信息，未使用真实数据库。\n");
        window.DataContext = original; RuntimeArtworkCache.Configure(original.DataDirectory, false); RuntimeArtworkCache.Register(original.Catalog);
    }
    private static async Task VerifyCatalogUpdatesAsync(MainWindow window, MainViewModel original, string output)
    {
        string directory = Path.Combine(output, "synthetic-catalog"); Directory.CreateDirectory(directory);
        var next = original.Catalog.Events.Last() with { Name = "合成新卡池", Version = "99.0", Order = 1, From = original.Catalog.CoveredThrough.AddDays(1), To = original.Catalog.CoveredThrough.AddDays(21) };
        string events = System.Text.Json.JsonSerializer.Serialize(original.Catalog.Events.Append(next).Select(e => new
        { e.Name, e.Version, e.Order, Type = int.Parse(e.Type), e.From, e.To, UpOrangeList = e.UpGold.Select(long.Parse), UpPurpleList = e.UpPurple.Select(long.Parse), Banner = e.BannerUrl, Banner2 = e.BannerAlternate }));
        int requests = 0;
        using var client = new HttpClient(new SyntheticFetchHandler(() =>
        {
            requests++;
            return new(System.Net.HttpStatusCode.OK) { Content = new StringContent(requests switch
            {
                1 => events,
                2 => "{\"retcode\":0,\"data\":{\"total\":1,\"list\":[{\"id\":10009999,\"name\":\"合成新角色\",\"avatar_level\":5,\"icon\":\"https://act-webstatic.mihoyo.com/synthetic.png\"}]}}",
                3 => "{\"retcode\":0,\"data\":{\"total\":1,\"list\":[{\"id\":99999,\"name\":\"合成新武器\",\"weapon_level\":5,\"icon\":\"https://act-webstatic.mihoyo.com/synthetic.png\"}]}}",
                _ => "invalid-response"
            }) };
        }));
        var updater = new WishCatalogUpdateService(client, Path.Combine(directory, "catalog", "snapshot.json"));
        var target = new MainViewModel(new DatabaseService(Path.Combine(directory, "gacha.db")), new UigfService(), new AnalysisService(new CatalogBannerProvider()),
            new GachaLogService(client), new SettingsService(Path.Combine(directory, "settings.json")), new SyntheticFetchDialogs(), null, catalogUpdates: updater);
        await target.InitializeAsync();
        if (requests != 0) throw new InvalidOperationException("隔离验证在未授权在线验证时自动联网。");
        target.UpdateCatalogCommand.Execute(null);
        for (int i = 0; target.CatalogStatus.StartsWith("正在") && i < 300; i++) await Task.Delay(20);
        if (requests != 4 || target.Catalog.CoveredThrough != next.To || target.Catalog.FindById("10009999")?.Name != "合成新角色")
            throw new InvalidOperationException("卡池资料更新未生效：" + target.CatalogStatus);
        target.ShowSection("历史"); await target.NavigationReady;
        if (target.CurrentPage is not HistoryViewModel history || !history.Groups.Any(g => g.Name == "合成新卡池")) throw new InvalidOperationException("新卡池未出现在历史页面。");
        target.SettingsCommand.Execute(null); window.DataContext = target;
        await RenderAsync(window, Path.Combine(output, "17-catalog-updated-settings.png"));
        target.UpdateCatalogCommand.Execute(null);
        for (int i = 0; target.CatalogStatus.StartsWith("正在") && i < 300; i++) await Task.Delay(20);
        if (!target.CatalogStatus.Contains("继续使用本地") || target.Catalog.CoveredThrough != next.To) throw new InvalidOperationException("更新失败没有保留可用资料。");
        if (updater.LoadCache(new WishCatalogService())?.Events.Length != target.Catalog.Events.Count) throw new InvalidOperationException("离线缓存恢复失败。");
        File.WriteAllText(Path.Combine(output, "catalog-update-verification.txt"), "合成新卡池和新物品更新、页面刷新、失败保留和离线缓存恢复通过。不使用真实档案，不连接网络。\n");
        window.DataContext = original; RuntimeArtworkCache.Configure(original.DataDirectory, false); RuntimeArtworkCache.Register(original.Catalog);
    }
    private static async Task VerifyUpdatesAsync(MainWindow window, MainViewModel originalShell, string output)
    {
        string directory = Path.Combine(output, "synthetic-updates"); Directory.CreateDirectory(directory);
        int requests = 0;
        using var client = new HttpClient(new SyntheticFetchHandler(() =>
        {
            requests++;
            if (requests == 3) return new(System.Net.HttpStatusCode.OK)
            { Content = new StringContent("{\"tag_name\":\"v0.0.1\",\"draft\":false,\"prerelease\":false}") };
            return requests == 1 ? new(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
                {
                    tag_name = "v99.0.0", draft = false, prerelease = false,
                    assets = ReleaseDownloads.ForTag("https://github.com/Answer-SeTsuNa/GenshinGachaAnalyzer", "v99.0.0")
                        .Select(file => new { name = file.FileName, browser_download_url = file.Url, size = 123, state = "uploaded" })
                })) }
                : new(System.Net.HttpStatusCode.NotFound);
        }));
        var dialogs = new SyntheticFetchDialogs();
        const string repository = "https://github.com/Answer-SeTsuNa/GenshinGachaAnalyzer";
        var target = new MainViewModel(new DatabaseService(Path.Combine(directory, "gacha.db")), new UigfService(),
            new AnalysisService(new CatalogBannerProvider()), new GachaLogService(client),
            new SettingsService(Path.Combine(directory, "settings.json")), dialogs, repository, new ReleaseUpdateService(client), storeDistribution: false);
        if (requests != 0) throw new InvalidOperationException("未点击检查更新就产生网络请求。");
        target.SettingsCommand.Execute(null); window.DataContext = target;
        target.CheckUpdateCommand.Execute(null);
        for (int attempt = 0; target.UpdateStatus.StartsWith("正在") && attempt < 150; attempt++) await Task.Delay(20);
        if (!target.HasNewVersion || !target.UpdateStatus.Contains("99.0.0")) throw new InvalidOperationException("新版本未显示。");
        target.DownloadUpdateCommand.Execute(null);
        if (dialogs.LastOpenedLink != repository + "/releases/tag/v99.0.0") throw new InvalidOperationException("更新下载未指向 GitHub 发布页。");
        if (target.DownloadPackages.Count != 2 || target.DownloadVersionText != "下载 v99.0.0") throw new InvalidOperationException("新版下载渠道没有跟随版本刷新。");
        foreach (var package in target.DownloadPackages)
            foreach (var link in package.Links)
            {
                target.OpenReleaseDownloadCommand.Execute(link);
                if (dialogs.LastOpenedLink != link.Url || !link.Url.Contains("v99.0.0")) throw new InvalidOperationException("下载渠道没有打开对应新版文件。");
            }
        await RenderAsync(window, Path.Combine(output, "13-settings-update.png"));
        target.CheckUpdateCommand.Execute(null);
        for (int attempt = 0; target.UpdateStatus.StartsWith("正在") && attempt < 150; attempt++) await Task.Delay(20);
        if (target.HasNewVersion || !target.UpdateStatus.Contains("未找到")) throw new InvalidOperationException("404 被误报为最新版或仍显示旧下载按钮。");
        if (target.DownloadPackages.Count != 2 || target.DownloadPackages.Any(file => file.Url.Contains("v99.0.0"))) throw new InvalidOperationException("检查失败后仍保留旧版下载渠道。");
        target.CheckUpdateCommand.Execute(null);
        for (int attempt = 0; target.UpdateStatus.StartsWith("正在") && attempt < 150; attempt++) await Task.Delay(20);
        if (target.HasNewVersion || target.UpdateStatus != "已是最新版本") throw new InvalidOperationException("最新版提示文案不正确。");
        await RenderAsync(window, Path.Combine(output, "13-settings-latest.png"));
        var installGuide = Descendants<Expander>(window).Single(control => control.Header?.ToString() == "微软商店安装指南");
        installGuide.IsExpanded = true;
        installGuide.BringIntoView();
        await RenderAsync(window, Path.Combine(output, "13-settings-install-guide.png"));
        installGuide.IsExpanded = false;
        Descendants<ScrollViewer>(window).First(view => view.Content is StackPanel).ScrollToHome();
        target.ReleasesCommand.Execute(null);
        if (dialogs.LastOpenedLink != repository + "/releases") throw new InvalidOperationException("GitHub 发布页入口错误。");
        var store = new MainViewModel(new DatabaseService(Path.Combine(directory, "store-test.db")), new UigfService(), new AnalysisService(new CatalogBannerProvider()),
            new GachaLogService(client), new SettingsService(Path.Combine(directory, "store-settings.json")), dialogs, repository, new ReleaseUpdateService(client), storeDistribution: true);
        store.CheckUpdateCommand.Execute(null);
        if (requests != 3 || dialogs.LastOpenedLink != "ms-windows-store://downloadsandupdates" || store.HasNewVersion || store.DownloadPackages.Count != 0)
            throw new InvalidOperationException("商店版仍走 GitHub 更新或没有打开商店更新入口。");
        store.SettingsCommand.Execute(null); window.DataContext = store;
        await RenderAsync(window, Path.Combine(output, "13-settings-store-channel.png"));
        window.DataContext = originalShell;
        File.WriteAllText(Path.Combine(output, "update-verification.txt"), "主动检查、新版提示、安装/便携两种包及各三个下载渠道、版本自动刷新、404 后回退当前版本、商店渠道分流通过；全部响应为合成，不打开真实浏览器或商店。\n");
    }
    private static async Task VerifyFetchRoutingAsync(MainWindow window, MainViewModel originalShell, string output)
    {
        // 真实 URL 命令 + 合成 API 响应，验证 UI 在首个事务提交后自动切 UID。
        // 单独的新数据库与设置，不连接真实账号，也不读取作者的缓存或档案。
        foreach (string scenario in new[] { "existing", "new", "interrupted" })
        {
            string directory = Path.Combine(output, "synthetic-fetch-" + scenario);
            Directory.CreateDirectory(directory);
            var database = new DatabaseService(Path.Combine(directory, "gacha.db")); database.Initialize();
            var original = new GachaRecord { Id = "999", Uid = "100000001", GachaType = "301", ItemId = "10000030",
                Name = "钟离", ItemType = "角色", RankType = "5", Count = "1", Time = new DateTime(2025, 1, 1), Source = "UIGF导入" };
            var incoming = original with { Id = "20", Uid = "200000001", ItemId = "10000131", Name = "尼可", Source = "游戏获取" };
            database.Insert([original]);
            if (scenario == "existing") database.Insert([incoming with { Source = "UIGF导入" }]);
            MainViewModel? target = null; int requests = 0;
            using var client = new HttpClient(new SyntheticFetchHandler(() =>
            {
                requests++;
                if (requests > 1 && target?.SelectedAccount?.Uid != incoming.Uid)
                    throw new InvalidOperationException("首个事务提交后未立即切到实际 UID。");
                if (scenario == "interrupted" && requests > 1)
                    return new(System.Net.HttpStatusCode.Forbidden);
                string response = requests == 1 ? System.Text.Json.JsonSerializer.Serialize(new
                {
                    retcode = 0, data = new { region = "cn_gf01", list = new[] { new
                    { id = incoming.Id, uid = incoming.Uid, gacha_type = incoming.GachaType, item_id = incoming.ItemId,
                        name = incoming.Name, item_type = incoming.ItemType, rank_type = incoming.RankType,
                        count = "1", time = "2025-01-01 00:00:00" } } }
                }) : "{\"retcode\":0,\"data\":{\"region\":\"cn_gf01\",\"list\":[]}}";
                return new(System.Net.HttpStatusCode.OK) { Content = new StringContent(response) };
            }));
            var settings = new SettingsService(Path.Combine(directory, "settings.json"));
            settings.Save(new(GamePath: directory));
            target = new(database, new UigfService(), new AnalysisService(new CatalogBannerProvider()),
                new GachaLogService(client), settings, new SyntheticFetchDialogs(), null);
            await target.InitializeAsync();
            if (target.SelectedAccount?.Uid != original.Uid) throw new InvalidOperationException("合成账号初始选择错误。");
            window.DataContext = target;
            target.FetchUrlCommand.Execute(null);
            for (int attempt = 0; target.IsBusy && attempt < 750; attempt++) await Task.Delay(20);
            if (target.IsBusy || target.SelectedAccount?.Uid != incoming.Uid || target.HasError != (scenario == "interrupted")
                || database.GetAccounts().Count != 2 || database.GetRecords(original.Uid).Single() != original
                || database.GetRecords(incoming.Uid).Count != 1)
                throw new InvalidOperationException("自动切换 / 新建 UID 或网络中断后的账号保留验证失败：" + scenario);
            if (await ItemIconCache.GetAsync(incoming.ItemId) is not BitmapSource image || image.PixelWidth < 1)
                throw new InvalidOperationException("尼可的官方本地图标未能解码。");
            await RenderAsync(window, Path.Combine(output, "15-fetch-" + scenario + ".png"));
        }
        window.DataContext = originalShell;
        File.WriteAllText(Path.Combine(output, "fetch-account-routing.txt"),
            "真实 URL 命令：自动切换已有 UID、自动创建新 UID、后续网络中断仍显示已保存的目标 UID；原账号不变；尼可官方 PNG 解码通过。仅使用合成 API / 隔离档案。\n");
    }
    private sealed class SyntheticFetchHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond());
    }
    private sealed class SyntheticFetchDialogs : Services.IAppDialogs
    {
        public string? LastOpenedLink { get; private set; }
        public GachaCredential? ChooseGachaUrl() => GachaLogService.TryParseCredential(
            "https://public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog?authkey=SYNTHETIC_TEST_ONLY&region=cn_gf01");
        public string? ChooseImportFile() => null;
        public string? ChooseExportFile() => null;
        public string? ChooseGameFolder() => null;
        public Services.DataSourceChoice ChooseDataSource() => Services.DataSourceChoice.Cancel;
        public bool ConfirmImport(PreparedImport import) => false;
        public IReadOnlyList<string>? SelectExportAccounts(IReadOnlyList<Account> accounts) => null;
        public bool ConfirmClearAccount(string uid, int count) => false;
        public void ShowWriteResult(WriteReport result) => throw new InvalidOperationException("合成 URL 获取产生意外冲突。");
        public void Explain(string title, string message) => throw new InvalidOperationException("合成 URL 获取出现意外对话框。");
        public void OpenPath(string path) { }
        public void OpenLink(string url) { LastOpenedLink = url; }
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
    private static async Task ObserveIntermediateAsync(DependencyObject target, DependencyProperty property, Action begin)
    {
        // 观察真实属性变化，不假定固定 60ms 时渲染线程已经提交了第一帧。
        var descriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(property, target.GetType());
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(object? sender, EventArgs args)
        {
            if (target.GetValue(property) is double value && value > 0 && value < 1) observed.TrySetResult();
        }
        descriptor.AddValueChanged(target, Changed);
        try { begin(); Changed(null, EventArgs.Empty); await observed.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { descriptor.RemoveValueChanged(target, Changed); }
    }
    private static async Task RenderAsync(Window window, string path)
    {
        File.AppendAllText(Path.Combine(Path.GetDirectoryName(path)!, "ui-render-progress.log"), Path.GetFileName(path) + " navigation\n");
        if (window.DataContext is MainViewModel shell) await shell.NavigationReady.WaitAsync(TimeSpan.FromSeconds(15));
        // 连续图表渲染时 ApplicationIdle 可能始终无法调度；用正常优先级并限制等待。
        await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.Normal).Task.WaitAsync(TimeSpan.FromSeconds(15));
        File.AppendAllText(Path.Combine(Path.GetDirectoryName(path)!, "ui-render-progress.log"), Path.GetFileName(path) + " layout ready\n");
        await Task.Delay(450); // 等待 Skia 绘制帧，不参与产品流程。
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        File.AppendAllText(Path.Combine(Path.GetDirectoryName(path)!, "ui-render-progress.log"), Path.GetFileName(path) + " capture\n");
        bitmap.Render(window);
        File.AppendAllText(Path.Combine(Path.GetDirectoryName(path)!, "ui-render-progress.log"), Path.GetFileName(path) + " captured\n");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
