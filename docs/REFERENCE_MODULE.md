# 参考模块定位与 WPF 适配

只读取祈愿与相关外观代码，未修改参考项目，也没有迁入账号登录、云服务、遥测或其他游戏功能。

## 胡桃 Snap.Hutao.Remastered 1.20.3

主体为 src/Snap.Hutao.Remastered/Snap.Hutao.Remastered，采用 WinUI 3 + MVVM + 依赖注入。WPF 页面、模板和动效独立实现。

| 参考代码 | 本工具用途 |
| --- | --- |
| UI/Xaml/View/Page/GachaLogPage.xaml | 总览、历史、角色、武器、计时导航和页面结构 |
| UI/Xaml/View/Specialized/StatisticsCard.xaml | 紧凑统计卡、展开动效、列表 / 网格 |
| Service/GachaLog/Factory/TypedWishSummaryBuilder.cs | 从档案首条计数；首次五星参与平均；四星只由四星重置；UP 包含非 UP |
| Factory/HistoryWishBuilder.cs | 用全部排期构建历史，品质汇总 |
| GachaLogWishCountdownService.cs | 完整排期 UP 间隔，独立于用户记录 |
| QueryProvider/GachaLogQueryWebCacheProvider.cs | 优先最新版本 webCaches，完整 data_2 最后一条有效链接 |
| Service/Metadata、Model/Metadata/GachaEvent.cs | 离线物品星级、名称、常驻身份、UP 排期、Banner |
| Resource/ItemIcon/UI_QUALITY_ORANGE/PURPLE/BLUE.png | 所有角色武器品质底图 |
| Int32ToGradientColorConverter | 出金抽数由绿到红的颜色端点 |
| UI/Xaml/Control/Theme/TransitionCollection.xaml | 200–300ms 页面入场、按钮和折叠交互参考 |

本轮按作者要求取消先前添加的首段下限口径，首个五星直接显示档案计数并参与统计。未知星级、未知 UP 与异常区间仍不补造。两个角色原始接口编号 301 / 400 保留，分析归入共享角色池 301；不是用户自定义规则。

缓存读取采用与胡桃相同的完整文件顺序，分块与重叠区域控制内存，选择最后有效国服 URL。不复制临时凭据文件；凭据只存在内存。

## FufuLauncher 1.7.0.3

| 参考代码 | 本工具用途 |
| --- | --- |
| Helpers/GamePathFinder.cs | HKCU Software/miHoYo/HYP/1_1/hk4e_cn 的 GameInstallPath，兼容启动器目录和 GenshinImpactGame |
| Services/GachaService.cs | 手动 URL 的认证参数、游标分页和重试流程核对 |
| MainWindow.Settings.cs | 亚克力开关、半透明开关、页面与背景不透明度 |
| MainWindow.Background.cs | 背景图片、Tint 遮罩与可读面板分层 |
| MainWindow.Window.cs | DesktopAcrylicBackdrop 对应的系统材质选择 |

WPF 使用 DwmSetWindowAttribute 的系统背景类型 3 和背景图片 BlurEffect。Windows 不支持或关闭系统透明效果时回退，文字层不模糊。壁纸只从本机读取并后台解码。

未迁入 FufuLauncher 的登录、Cookie / stoken 生成 authkey、注入、签到等模块，符合纯本地档案与国服官方祈愿接口的边界。

第三方来源与 MIT 许可证见 THIRD_PARTY_NOTICES.md 和 docs 内 LICENSE 副本。
