# 第三方来源与声明

- EXE 安装器使用 Inno Setup 6.7.3：https://github.com/jrsoftware/issrc/tree/is-6_7_3。Copyright (C) 1997–2026 Jordan Russell；Portions Copyright (C) 2000–2026 Martijn Laan。许可全文随附 InnoSetup.LICENSE。
- 安装向导简体中文翻译：https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation，固定提交 1ff90acc4ed4aee82b1cda43253243deee3daed4，MIT 全文随附 InnoSetup.ChineseSimplified.LICENSE。

祈愿模块的布局、页面分工和历史 / UP 计时思路参考用户提供的 Snap.Hutao.Remastered 1.20.3。该项目使用 WinUI 3；本工具用 WPF 独立实现，使用本工具的 SQLite、UIGF 和本地统计服务。首段计数和四星进度按胡桃口径，未知星级 / 排期不补造。未移植其云服务、遥测、其他游戏模块和概率预测。

- 0.1.21 的米游社扫码、登录结果解析、绑定角色与查询授权流程参考 Snap Hutao Remastered 的 HoyoPlayPassportClient / GachaLogQuerySTokenProvider / BindingClient2，网页登录参考 FufuLauncher 的官方通行证入口。独立实现临时会话、用户确认 UID 和不记录凭据的错误处理；未迁入第三方账号存储或遥测。
- 二维码由 QRCoder 1.8.0 本地生成，MIT 许可随附 QRCoder.LICENSE。
- 官方网页登录使用 Microsoft.Web.WebView2 SDK 1.0.4191.47；遵循随附 Microsoft.WebView2.LICENSE 和 Microsoft.WebView2.NOTICE。SDK 不包含完整 Edge 运行时；账号网页登录使用系统的微软 WebView2 运行时，扫码方式不依赖该运行时。

- 原项目：https://github.com/SnapHutaoRemasteringProject/Snap.Hutao.Remastered
- 版权所有：2022–2025 DGPStudio；2026 Snap.HutaoRemasteringProject。MIT 全文见 `Snap.Hutao.LICENSE`。
- 名称、图标标识及排期元数据：https://github.com/SnapHutaoRemasteringProject/Snap.Metadata
- 内置元数据提交：`b3aef3dbe0299e512654bb296930b3b6571fc87f`。自 0.1.22 起，运行时检查同一公开仓库 main 分支的原神中文祈愿排期，物品列表与新增图片从米哈游官方接口更新。MIT 全文见 `Snap.Metadata.LICENSE`。
- 游戏图标通过参考项目的公开静态资源接口取得；卡池画卷按 GachaEvent.Banner / Banner2 指向的公开官方素材地址取得，原神的角色、武器、美术及名称相关权利归米哈游 / HoYoverse 所有；本工具代码的 MIT 协议不授予游戏素材的权利。图标与卡池图片内置供离线显示，不在运行时请求外部图片。
- 0.1.12 起，维护者可直接从米游社原神养成计算器的角色 / 武器列表读取官方 icon URL。本轮补齐尼可等 126 张缺失图标，并同步物品显示快照；旧镜像图标保留。已直接下载的官方资源 URL 与 SHA256 见 Resources/Items/sources.json；来源证据和维护脚本见 docs/ITEM_ASSETS.md。游戏素材版权不属于本项目 MIT 授权。

排期是社区维护的资料，内置快照与本地缓存用于断网回退，界面显示覆盖边界；没有覆盖的历史只按月份归档，不推导 UP。计时表示已收录 UP 的历史时间间隔，不表示未来复刻预测。元数据不写入或覆盖原始抽卡记录，不混入 UIGF 导出。

依赖库：Microsoft.Data.Sqlite（MIT）；LiveCharts2（MIT）；SkiaSharp（MIT）。其许可证随对应 NuGet 包提供。


- 物品底图使用参考项目 Resource/ItemIcon/UI_QUALITY_ORANGE、PURPLE、BLUE.png 原始资源。旧主题祈愿横幅已移除；当前六主题使用眼内齿轮纹饰、官方元素图标与可选本地壁纸，详见 THEME_SOURCES.md。
- FufuLauncher 1.7.0.3：https://github.com/CodeCubist/FufuLauncher。游戏定位、URL 获取流程核对与亚克力 / 遮罩分层参考。Copyright (c) 2025 Dev. CodeCubist，MIT 全文随附 FufuLauncher.LICENSE。没有迁入注入、签到、账号登录或生成认证凭据功能。
- 应用图标为作者提供的图标_木偶.png 的多尺寸转换副本。其图片权利不由代码 MIT 协议自动授予。

0.1.2 主题使用 HoYoWiki 官方站点的五枚元素透明 PNG（冰 / 火 / 水 / 雷 / 岩）。预览保留原图，背景仅以透明通道适配颜色。素材权利归 miHoYo / HoYoverse，不适用本项目 MIT 授权。原始 URL、离线使用方式及 SHA256 清单见 docs/THEME_SOURCES.md 和 Resources/Elements/sources.json。

木偶眼内齿轮纹饰参考官网桑多涅角色原画（https://genshin.hoyoverse.com/en/character/snezhnaya?char=15），以代码作简化轮廓；不是官方发布的齿轮图标，不宣称原画精确齿数。参考原画只保存在开发产物目录，未进入源码或程序包。
