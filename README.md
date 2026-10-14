# 木偶原神抽卡分析工具

<img src="GenshinGachaAnalyzer.WPF/Resources/AppIcon.png" width="96" alt="木偶应用图标"/>

**Windows 本地祈愿档案管理器 · 0.1.20**

记录永久留存，使用 UIGF 自由迁移。.NET 10 LTS / WPF / MVVM / LiveCharts2 / SQLite，代码采用 MIT 协议。

## 获取与运行

从仓库 Releases 下载 `GenshinGachaAnalyzer-Setup-v版本号-win-x64.exe`，双击中文安装向导，可选择安装目录和桌面快捷方式；安装后从桌面或开始菜单启动。默认只为当前用户安装，无需管理员权限；新版安装包覆盖升级，Windows 设置中可卸载，祈愿档案和设置会保留。

也可下载 win-x64.zip 便携版，解压整个目录后运行 GenshinGachaAnalyzer.exe，不能只复制 EXE。两种版本均自带运行环境，无需另装 .NET。支持 Windows 10 2004 及以上的 x64 兼容系统；原生亚克力材质需要 Windows 11 22H2 及以上，并遵循系统的透明效果设置。安装与维护说明见 [INSTALLER.md](docs/INSTALLER.md)。

主动获取祈愿时连接国服官方接口；主动点击“检查版本更新”时连接 GitHub，仅查询最新正式发布，不发送档案或游戏凭据。不上传祈愿数据，不埋点，不建立在线账号，不支持国际服或云原神。应用首次启动会检查 HoYoPlay 注册表与常见安装目录；仍可手动选择含 YuanShen.exe 的游戏目录。

- 游戏获取：先在国服游戏里打开一次祈愿历史，再点击更新数据。自动按接口实际 UID 切换已有账号或新建账号，游戏保持运行时也能共享只读缓存。
- URL 获取：顶部 URL 或添加账号入口可粘贴国服官方祈愿链接；凭据仅用于本次获取，不持久化。
- UIGF 导入：选择 JSON、查看离线预检、确认后事务写入。同 ID 重复跳过，冲突保留原件并给出逐字段差异。
- 多账号：UID 下拉切换。清空当前账号需要完整 UID 确认，并先创建一致性备份；其他账号不受影响，可重新导入同一文件。
- 导出：选账号和版本，默认纯标准 UIGF v4.1，不附加来源或分析字段。
- 原始记录没有逐条手动添加、修改或删除入口。

## 页面与分析

| 页面 | 内容 |
| --- | --- |
| 总览 | 四池抽数、五星、平均、UP 平均、最欧最非；紧凑列表 / 网格、非 UP 合并、定位高亮与五星比例 |
| 详情 | 出金时间线、10 抽区间分布；底部只读记录表，名称 / 日期搜索，每页 50 条 |
| 历史 | 角色、武器、集录的全部已收录卡池；足迹 / 时间线切换，大版本从至冬到蒙德排序；卡池图下切换汇总 / 原始记录 |
| 角色 / 武器 | 按品质分组，原始橙 / 紫 / 蓝底图与记录内获取数量 |
| 复刻日历 | 基于胡桃完整 UP 排期，时间按钮打开图片弹层，不依赖用户是否抽取 |
| 我的 | 总抽数、UP 均值、已知小保底连歪、活动空档、月度足迹与五星收藏；月度图可过滤常驻 |
| 设置 | 六个主题、日夜 / 跟随系统、透明度与壁纸、缩放、游戏路径、备份、开发者日志 |

### 统计口径

计数按胡桃 TypedWishSummaryBuilder 的起点方式：从档案第一条记录累计，首次五星也纳入均值、最欧最非和分布。例如档案中首次五星前共 78 条，显示 **78 抽**，没有大于等于符号。计算始终只描述已保存档案，不能证明未保存的历史没有遗漏。

- 五星重置五星计数；四星仅重置四星计数，五星不会重置四星进度。四星后紧接五星，四星进度为 1。
- UP 周期包含途中非 UP；已知起点的首次 UP 同样计数。缺少对应排期时不推导 UP，受影响周期不参与 UP 均值。
- 无法从元数据识别的星级或超过硬保底的异常区间仍显示未知，不填造统计。
- 两个角色祈愿接口编号 301 / 400 按胡桃与 UIGF 共享编号归入角色祈愿。武器为 302、常驻为 200、集录为 500；100 是新手祈愿，仅存档。
- 个人欧非指数采用平均秩百分位，至少 10 次可统计出金时显示，基于当前 UID 全部主池历史。
- 不显示下一抽或累计出金概率。武器不显示大小保底、定轨或命定值。集录仅显示跨期继承的抽数，不推导命定选择。

排期使用参考项目 Snap.Metadata 离线快照：298 个活动排期与 298 张卡池图片。物品经官方米游社养成计算器同步后有 374 个条目与 374 张图标；本轮直接从官方来源补齐尼可等 126 张缺失图片，早期镜像图片保留。元数据只用于显示与分析，不覆盖 SQLite 原始记录，不写入 UIGF。未覆盖时期保持未知；复刻日历不预测复刻。

维护者可运行 `./scripts/Sync-OfficialItemAssets.ps1` 获取官方物品列表并补齐图片；配合 `-Force` 可刷新全部图标，不需要下载参考项目。来源核对、官方接口、命令及校验清单说明见 [角色与武器资源更新](docs/ITEM_ASSETS.md)。应用运行时继续离线使用内置图片。

### 主题与亚克力

六个主题为木偶、神里绫华、可莉、钟离、芙芙、牛牛，采用角色配色、简约细线与官方元素符号。木偶使用参考官网眼内纹理绘制的正圆齿轮；绫华使用冰图标、可莉火、钟离岩、芙芙水、牛牛雷。木偶齿轮按主题色绘制；其余主题在设置与个人页保留元素图标原色，背景以原图透明轮廓适配主题色。图标离线嵌入，白天 / 夜晚 / 跟随系统、亚克力与透明度设置均保留。

参考 FufuLauncher 的材质分层方式，提供系统 Desktop Acrylic、独立背景遮罩、面板不透明度、本地壁纸、壁纸不透明度和壁纸模糊强度。移动窗口时不切换材质或遮罩。系统亚克力适用于 Windows 11 22621 及以上，失焦或系统禁用透明效果时遵循系统回退；壁纸模糊在后台预处理，不模糊文本。图片后台加载并限制解码尺寸，缓存页面直接显示。

旧版的主题祈愿宣传图已移除。默认使用离线元素纹饰，可自行选择本地图片；没有运行时联网下载壁纸。作者提供的资源图已生成 16–256px 应用 ICO 并用于窗口与 EXE。

## UIGF 兼容性

支持 **v4.2 / v4.1 / v4.0 / v3.0 / v2.4 / v2.3 / v2.2** 导入与导出；默认导出 v4.1。未宣称 UIGF 官方认证。

- v4.x 顶层为 info + hk4e 多账号结构；v2 / v3 为 info.uid + list 单账号结构。
- v2.x 无法表示集录记录；旧版导出不静默丢弃记录，会提示改用新版。
- 内置官方原神 Schema 的离线约束子集，验证类型、必填字段、版本、日期、时区、池编号和国服 UID。
- 记录 ID 使用字符串，避免大数精度丢失。SQLite 和导出统一 UTC+8。
- name、rank_type 等可选元数据缺失时保留原值，显示和统计副本由离线物品表识别。
- 星铁、绝区零和 v4.2 千星奇域内容按兼容规则忽略，不写入本地普通祈愿档案。
- 导出完整写入同目录临时文件并同步后才替换目标，不附带 source、保底或分析结果。

规则来源：[UIGF-Org](https://uigf.org/zh/)，迁移说明见 [MIGRATION.md](docs/MIGRATION.md)。

## 数据目录与日志

默认 %AppData%/GenshinGachaAnalyzer，设置里可打开：

    gacha.db             原始记录和账号
    settings.json        游戏路径、主题与外观偏好，不含 authkey
    backups/             导入、获取和清空前的一致性数据库快照
    logs/runtime-*.log   缓存定位、错误类型与调用位置，凭据自动隐藏
    logs/               导入结果和冲突字段差异日志

开发者模式开启后，操作报错会弹出非阻塞日志窗口。也可主动查看 / 刷新当天最近日志；完整文件保留在 logs。运行日文件按 8MB 分段，窗口最多读取尾部 256KB。冲突日志包含 UID 和个人记录，提交 Issue 前请脱敏。

所有写入使用事务，网络失败保留完整提交的页面。缓存提取按最新版本优先扫描整个 data_2，使用分块与重叠区域控制内存；不再仅检查文件尾部。凭据只接受国服官方 HTTPS 链接，请求固定到 public-operation-hk4e.mihoyo.com（与参考胡桃项目的国服接口一致），禁止重定向与 Cookie。兼容旧链接的凭据提取，实际请求统一使用现行国服接口。HTTP 地址错误、拒绝访问与连接超时分别提示。

同一记录 ID 的 UID、卡池与时间必须一致；物品 ID、名称、类型、星级、数量只在双方都已知时比较。UIGF 可选字段缺省、游戏物品 ID 为空不判为冲突；双方已知值矛盾仍列入冲突日志。重复和冲突均保留已有记录，不覆盖、不自动补齐原始字段。

启动检查数据库完整性；发现损坏时保留原件。手动备份请关闭工具后复制整个数据目录，包括可能存在的 WAL / SHM。恢复时先把旧目录改名留存，建空目录再将备份 DB 复制为 gacha.db，避免混用旧 WAL。也可通过 UIGF 迁移。

## 构建与验证

需要 Windows 与 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。首次还原依赖需要网络。

    ./scripts/Build.ps1
    ./scripts/Build.ps1 -Publish
    ./scripts/Verify-Ui.ps1 -Published
    ./scripts/Package.ps1
    ./scripts/Verify-Installer.ps1

发布输出 artifacts/publish/v0.1.20-win-x64；打包生成 EXE 安装包、二进制 ZIP、源码 ZIP 和 SHA256SUMS。首次安装包构建会下载并校验固定版本的编译工具，后续使用本地缓存。UI 和安装验证使用合成账号及新的隔离目录，拒绝实际 AppData 数据库；详见 [VERIFICATION.md](docs/VERIFICATION.md)。

手动构建：

    dotnet restore GenshinGachaAnalyzer.sln
    dotnet build GenshinGachaAnalyzer.sln -c Release --no-restore
    dotnet test GenshinGachaAnalyzer.Tests -c Release

构建前关闭从该构建目录运行的程序，或使用独立 OutputPath，避免 DLL 被锁定。

## GitHub 发布

[发布指南](docs/RELEASE.md) 包含仓库准备、版本、标签和资产操作。.github/workflows/build.yml 自动执行 Windows 编译、测试和打包；推送与版本一致的 v0.1.x 标签后创建 Release。

版本只维护在 Directory.Build.props。本次基线 0.1.20；默认仅递增最后一位：

    ./scripts/Bump-Version.ps1 -Preview
    ./scripts/Bump-Version.ps1

前两位仅由作者决定。应用、UIGF 元信息和打包自动读取版本，避免多处不一致。实际仓库为公开的 [Answer-SeTsuNa/GenshinGachaAnalyzer](https://github.com/Answer-SeTsuNa/GenshinGachaAnalyzer)，已配置 WPF/appsettings.json 的 RepositoryUrl。项目代码使用 MIT 协议；游戏素材及第三方组件遵循 [第三方声明](THIRD_PARTY_NOTICES.md)。

### Code signing policy

维护者 / 提交审核 / 发布批准：[@Answer-SeTsuNa](https://github.com/Answer-SeTsuNa)。正式版本通过 GitHub Actions 构建、测试和发布，并为安装包与 ZIP 生成 GitHub 签名构建来源证明。验证方法及 Windows 签名申请状态见 [SIGNING.md](docs/SIGNING.md)。

Windows EXE 签名采用 SignPath Foundation 免费开源服务申请流程，目前尚未获批，不宣称已有可信 Windows 签名。GitHub 构建证明不等同于 Authenticode 签名，不承诺消除 SmartScreen 提示。

隐私政策：本程序不会将任何祈愿档案、游戏凭据或本地设置传到网络。只有用户主动获取记录、检查更新或打开外部链接时才请求对应服务。没有埋点和在线账号体系；详情见本页的数据管理说明。

## 项目结构与来源

- Core：不可变记录、SQLite、UIGF / 离线 Schema、获取、统计、设置与脱敏日志。
- WPF：Views、ViewModels、Controls、Infrastructure、Services 和本地 Resources。
- Tests：数据库、七版 UIGF、统计、缓存 / 模拟 API、脱敏与多账号回归；Fixtures 为合成资料。
- docs / scripts / .github：兼容性、验证、来源、构建与发布。
- 参考项目和原始资源目录不进入仓库或源码包；日志、数据库、备份、缓存和构建输出同样排除。

参考模块定位见 [REFERENCE_MODULE.md](docs/REFERENCE_MODULE.md)。代码 [MIT](LICENSE)，第三方声明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)，主题资料见 [THEME_SOURCES.md](docs/THEME_SOURCES.md)。原神美术与角色名称权利归原权利人。本项目与米哈游无官方关联。

实际游戏获取仍需要真实国服客户端联调；当前自动化覆盖模拟接口和合成缓存，不宣称已验证所有启动器、所有 4K / 多屏配置或实际磁盘满场景。

在受限构建环境里，若 VSTest 因父进程访问被拒绝而中断，可运行 ./scripts/Build.ps1 -Publish -InProcessTests。该选项使用已安装的官方 xUnit 执行器运行相同测试程序集与全部断言，不跳过用例；JSON 报告在 artifacts/TestResults，临时测试数据限定在 artifacts/test-temp。

### 启动兼容与 Windows 提示

WPF 通常使用默认硬件加速 / 系统回退；检测到游戏加加时，在初始化前限制本进程的旧式扩展点并选择软件渲染。壁纸在后台预先模糊，页面动画复用冻结位图，保留亚克力与日夜外观。启动阶段记录在本地 logs/runtime-日期.log。发布检查同时执行 Verify-Startup.ps1（真实选择控件、缓存与切换首帧耗时）和 Verify-Ui.ps1（页面布局）。

普通 Build / Publish、固定发布入口和安装器输出均修正生成 EXE 继承工作区 Low 标签的问题，避免正常双击后无法写入 AppData；不提权，不修改用户数据权限。启动验证检查实际进程完整性，覆盖普通 bin/Release 入口。详见 [启动排查](docs/STARTUP_TROUBLESHOOTING.md)。

旧版已在作者电脑确认游戏加加 GPP64.dll 注入后的启动挂起。0.1.16 增加本进程的叠加层兼容处理；仍需按具体叠加层版本验证，不保证阻止所有注入。启动检查提供 `-ShellLaunch`，记录进程完整性、渲染模式与 GPP 模块状态。具体记录和排查方法见 [STARTUP_TROUBLESHOOTING.md](docs/STARTUP_TROUBLESHOOTING.md)。

当前 EXE 尚未正式签名，Windows SmartScreen 可能显示“Windows 已保护你的电脑”；详见 [发行签名](docs/SIGNING.md)。未宣称消除此提示，未更改系统安全设置。
