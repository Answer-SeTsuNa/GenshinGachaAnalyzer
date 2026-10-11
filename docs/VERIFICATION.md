# 0.1.13 交付验证

日期：2026-10-12（Asia/Shanghai）。首次上传 GitHub，配置实际仓库入口。

- 实际仓库为 Answer-SeTsuNa/GenshinGachaAnalyzer，初次上传使用私有仓库，main 为主分支。应用内 RepositoryUrl 已配置实际 HTTPS 地址。
- Release 使用 artifacts/github-build 独立输出目录编译，0 警告 / 0 错误；Core xUnit 149 通过 / 0 失败 / 0 跳过。VSTest 遇到已记录的 Testhost 父进程权限错误后，使用项目官方 xUnit 进程内执行器运行全部测试，没有跳过断言。
- 正常 bin/Release 输出被正在运行的旧窗口占用，使用独立输出构建并发布；保留旧窗口，不强制结束用户进程。
- artifacts/github-startup-v0.1.13：latest 通过 ShellExecute、2,832 条合成记录、4K 合成壁纸与模糊 40 的启动 / 页面 / 主题检查。
- artifacts/github-ui-v0.1.13：完整 UI 验证通过，93 张 PNG，bindings.log 0 字节；374 张角色 / 武器图片和 298 张卡池图片完整解码通过。已查看总览截图。
- 版本发布目录与 latest 已同步，EXE 版本为 0.1.13.0。保留代码与第三方许可证；数据库、日志、备份、参考源码、原始资源目录和构建输出不提交到 Git。
- 所有 UI 验证使用新建隔离目录与合成档案，未读取或修改作者真实 AppData 数据库。

下列为前几轮的历史验证记录。

# 0.1.12 交付验证

日期：2026-10-12（Asia/Shanghai）。按实际获取 UID 自动切换 / 新建账号，补齐尼可图标并建立官方资源同步入口。

- 获取服务不再用当前选中 UID 限制首个有效响应。首个页面完整提交后通知界面立即选择实际 UID；已有账号按 ID 去重，未收录账号由同一事务创建。后续失败也按该 UID 刷新已保存数据。一次请求会话中的 UID 变化、混合 UID、卡池不一致仍拒绝异常页面；空列表不猜测账号。
- 米游社养成计算器官方 POST 列表实测无需 Cookie / UID / authkey 即返回 retcode 0。角色响应 148 行（多元素变体去重并排除旅行者），武器响应 252 行；脚本分页取得 374 个唯一物品。尼可 ID 10000131 对应实际官方 icon 地址已下载并人工检查头像。未将 HTTP 200 的旧式占位图片当作成功资源。
- 本轮共从官方 icon URL 补齐 126 张缺失 PNG，目录现有 374 张，374 个物品条目无缺图。来源清单 126 个 SHA256 全部匹配。保留其余早期镜像图片及历史出处，没有伪造官方来源。
- Sync-OfficialItemAssets.ps1 实测完整同步成功，后续 DryRun 待下载 0。语法检查通过；针对非官方域名、相似域名、明文 HTTP、含凭据查询参数与已知占位 PNG 的拒绝检查通过。维护脚本不改变原始记录、UP 排期或 UIGF 格式。
- Release 在独立输出目录编译 0 警告 / 0 错误；Core xUnit 149 通过 / 0 失败 / 0 跳过。首次普通 Release 输出被旧窗口占用，未强制结束用户进程；先通过独立输出构建完成自包含发布。作者确认关闭后，标准 Build.ps1 -Publish -InProcessTests 也成功：0 警告 / 0 错误、149 项测试通过，普通 Release、版本目录与 latest 均已更新。发布 EXE、WPF DLL、Core DLL 的 SHA256 与已通过完整 UI 验证的产物一致。
- artifacts/startup-v0.1.12-latest：固定发布入口通过 ShellExecute、2,832 条合成记录、4K 合成壁纸与模糊 40 的启动 / 页面 / 主题验证，进程完整性 8192。版本目录与 latest 共 1,118 个文件逐项哈希一致，EXE 版本 0.1.12.0。
- 作者关闭旧窗口后的复核：artifacts/startup-v0.1.12-final-build 与 artifacts/startup-v0.1.12-final-latest 均通过 ShellExecute、合成档案加载、页面 / 主题切换；两个进程完整性均为 8192。latest 另含 4K 合成壁纸与模糊 40 检查。
- artifacts/ui-v0.1.12-release：完整 UI 验证通过，93 张 PNG，bindings.log 0 字节；374 张角色 / 武器 PNG 和 298 张卡池图片解码通过。
- 新增真实 WPF URL 命令验证：切换已有 UID、新建 UID、首个页面保存后网络中断均保留正确选中账号，原账号内容不变。已查看新账号总览截图，UID 为合成 200000001，尼可头像正确显示。所有 API 响应为合成内容，数据库 / 设置为隔离目录；没有使用作者真实档案或凭据测试。
- 官方接口和两个参考项目的直接来源、更新命令、保留的旧镜像边界详见 ITEM_ASSETS.md。项目约定已明确角色 / 武器图标使用官方图片，不生成替代素材。

下列为前几轮的历史验证记录。

# 0.1.11 交付验证

日期：2026-10-12（Asia/Shanghai）。修复同一账号先从 URL 获取、再导入 UIGF 时因缺省字段产生假冲突的问题。

- 只读最近一次本地写入诊断日志：输入 5,071 条，新增 4,450 条，报告冲突 621 条。621 条全部是数量缺省差异，其中 421 条另有名称、物品类型或星级缺省，200 条另有游戏记录物品 ID 为空而导入记录有值。未发现双方已知值矛盾，UID、卡池类型和时间也没有差异。日志显示事务已提交、已有记录保留；没有读取作者真实数据库进行测试。
- 根据 [UIGF 官方标准](https://uigf.org/zh/standards/uigf.html)，名称、物品类型、星级和数量是可选字段。判重现在将缺省视为信息未提供，只检查双方已知值是否矛盾；记录 ID、UID、卡池类型和时间仍严格比较。重复记录保留原始内容与来源，不补写、不覆盖。
- 冲突日志复用同一比较规则，真正冲突仅列出矛盾字段，避免将缺省字段列为问题。
- Release 编译 0 警告 / 0 错误；Core xUnit 145 通过 / 0 失败 / 0 跳过。回归覆盖实际获取服务与 UIGF 预检的两种导入顺序、缺省字段兼容、已知字段矛盾仍报冲突、已有原始记录不变以及日志排除缺省差异。
- artifacts/startup-v0.1.11-shell：版本发布通过 ShellExecute、2,832 条合成记录、4K 合成壁纸与模糊 40 的启动 / 页面 / 主题切换验证，进程完整性 8192。
- artifacts/startup-v0.1.11-latest：固定发布入口通过 ShellExecute 与合成档案启动 / 页面 / 主题切换验证，进程完整性 8192。
- artifacts/ui-v0.1.11-release：完整 UI 验证通过，90 张 PNG，bindings.log 0 字节。
- 首次同步 latest 时旧窗口占用发布文件，作者确认关闭后完成全量同步。验证仅使用隔离目录与合成档案，没有清空、重导或修改作者真实数据库与设置。作者可使用原文件再次导入验证；本轮未代替作者对真实账号执行重导。

下列为前几轮的历史验证记录。

# 0.1.10 交付验证

日期：2026-10-12（Asia/Shanghai）。修复祈愿更新请求旧接口并误报超时的问题。

- 现场只读日志确认 0.1.9 已找到国服缓存凭据，三次请求均快速返回 HTTP 404，最终被通用错误报成“网络失败或超时”。未读取真实数据库用于测试。
- 参考源码 ApiEndpoints.csv:45 与 GachaInfoClient.GetGachaLogPageAsync 使用 public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog。FufuLauncher 的 GachaService.FetchGachaLogAsync 保留所提取链接的域名 / 路径，而本产品旧代码强制使用已失效域名。
- 无凭据的公共连通检查：旧 hk4e-api 地址 HTTP 404；现行国服地址 HTTP 200、retcode -100（因检查不带 authkey，属于预期的缺少凭据响应）。仅证明接口可达，未用作者真实凭据或真实账号记录联调。
- Release 编译 0 警告 / 0 错误；Core xUnit 136 通过 / 0 失败 / 0 跳过。新增混合新旧链接缓存提取、现行请求地址、国际服 / 相似恶意域名 / 非普通祈愿路径拒绝、HTTP 永久错误不重试，以及实际超时 / 临时 HTTP 错误区分回归。
- artifacts/startup-v0.1.10-shell：版本发布通过 ShellExecute、2,832 条合成记录、4K 合成壁纸与模糊 40 的启动 / 页面 / 主题检查，进程完整性 8192。
- artifacts/startup-v0.1.10-latest：固定发布入口也通过 ShellExecute 与合成档案启动检查。版本目录与 latest 的 990 个文件逐项哈希一致。
- artifacts/ui-v0.1.10-release：完整 UI 验证通过，90 张 PNG，bindings.log 0 字节。
- 首次同步 latest 时旧窗口占用 Accessibility.dll，未强制结束用户进程。作者确认关闭后完成整目录文件同步和入口完整性修正。真实数据未清空、覆盖或用于验证；已提交页面保留机制不变。

下列为前几轮的历史验证记录。

# 0.1.9 交付验证

日期：2026-10-12（Asia/Shanghai）。恢复首版配色，保留当前图案和功能。

- 从本地 0.1.0 源码包核对：木偶的强调色 / 夜间强调色 / 日间背景 / 夜间背景恢复为 #866643 / #D6B180 / #F4EFE6 / #27231F；全局正文和辅助文字也恢复首版色值。
- 六主题四项日夜色板逐项与 0.1.1 源码包核对一致。原有五主题沿用 0.1.0；牛牛沿用初次加入的 0.1.1。
- Release 编译 0 警告 / 0 错误，Core xUnit 127 通过 / 0 失败 / 0 跳过。
- artifacts/startup-v0.1.9-shell：latest 通过 ShellExecute 启动，进程完整性 8192；2,832 条合成记录、4K 合成壁纸 / 模糊 40、页面与主题切换检查通过。
- artifacts/ui-v0.1.9-release：完整 UI 验证通过，90 张 PNG，bindings.log 0 字节；已查看木偶白天与夜晚的设置页，铜棕、米白和夜间配色显示正常。
- 验证仅使用隔离目录与合成档案；本轮未处理作者真实数据库或设置。

下列为前几轮的历史验证记录。

# 0.1.8 交付验证

日期：2026-10-12（Asia/Shanghai）。本轮将本地壁纸的恢复入口改为明确的垃圾桶图标加“清空”按钮，复用已有保存和恢复主题背景命令。原图片文件保留。

- Release 编译 0 警告 / 0 错误，Core xUnit 127 通过 / 0 失败 / 0 跳过。
- latest 自包含启动检查 artifacts/startup-v0.1.8-shell 通过；ShellExecute、2,832 条合成记录、4K 合成壁纸与模糊 40，进程完整性 8192，页面 / 主题切换通过。
- 完整 UI 验证 artifacts/ui-v0.1.8-release 通过，90 张 PNG，无绑定错误。
- 已查看 12-theme-Ayaka-Light-settings.png，“清空”图标与文字正常显示在选择本地壁纸按钮旁。
- 设置命令保存 WallpaperPath=null 并刷新主题背景；未新增原图片文件删除操作。所有界面验证使用隔离目录和合成档案，未用作者真实数据库测试。

下列为上一轮权限修复的历史验证记录。

# 0.1.7 交付验证

日期：2026-10-12（Asia/Shanghai）。Windows / .NET SDK 10.0.401 / .NET 10.0.12。

## 问题与修正范围

作者粘贴的 0.1.6 日志显示实际启动的是普通 bin/Release/net10.0-windows10.0.19041.0/GenshinGachaAnalyzer.exe，进程完整性为 4096（Low），AppData 日志无法写入，数据库初始化被权限检查拒绝。现场文件标签确认普通 Release 和 RID Release EXE 仍继承 Low；上一轮仅修正版本发布与 latest，遗漏普通编译入口。

0.1.7 在 WPF 项目的 Build / Publish 之后处理生成的本项目 EXE，覆盖直接编译、Debug、Release、RID 与直接发布。latest 复制后继续单独修正。修正失败即构建失败。manifest 保持 asInvoker，不使用管理员权限，不更改工作区、AppData 的 ACL 或 Windows 安全设置。档案加载失败不再记录“初始化完成”。

原 gacha.db 文件元数据仍为 1,388,544 字节，最后修改时间 2026-10-07 21:02:04。没有读取真实数据库内容用于测试，没有迁移、清空或覆盖数据；文件元数据不能证明所有记录数量和完整性。

## 构建和启动

- Release 与 Debug 编译均 0 警告 / 0 错误，生成 win-x64 自包含发布。
- Core xUnit 127 通过 / 0 失败 / 0 跳过，报告 artifacts/TestResults/core-tests-v0.1.7-inprocess.json。
- 以下四个入口均通过 ShellExecute 启动、正常窗口渲染、2,832 条合成记录加载、七页三轮切换与 18 组主题 / 日夜选择检查。所有 process-integrity.txt 为 8192（Medium）。

| 入口 | 隔离报告目录 | 交互阶段（包含固定等待） |
| --- | --- | --- |
| 普通 Release（作者日志路径） | artifacts/startup-v0.1.7-build | 10934ms |
| RID Release | artifacts/startup-v0.1.7-rid | 10273ms |
| Debug | artifacts/startup-v0.1.7-debug | 10483ms |
| latest 自包含 | artifacts/startup-v0.1.7-latest | 10504ms |

latest 用例同时使用 3840×2160 合成壁纸、模糊 40、夜晚牛牛、亚克力和半透明。以上总耗时包含固定测试等待，不是单次切换耗时；每次到下一 WPF 渲染帧的数据见 interaction-timings.json。

## 完整 UI 回归

- artifacts/ui-v0.1.7-release：退出码 0，90 张 PNG，bindings.log 0 字节。
- 包含六款主题的日夜页面、总览指标与详情、历史筛选和分页、角色 / 武器、复刻日历、我的、账号清空后另一账号图表，以及合成数据库错误页和重新加载回归。
- 故障档案测试只处理隔离目录：损坏文件保留，加载失败不展示虚构零账号统计，操作入口受限，重试成功恢复档案。

## 复现

    ./scripts/Build.ps1 -Publish -InProcessTests
    ./scripts/Verify-Startup.ps1 -OutputDirectory artifacts/startup-build-check -ShellLaunch -WithData -ExecutablePath GenshinGachaAnalyzer.WPF/bin/Release/net10.0-windows10.0.19041.0/GenshinGachaAnalyzer.exe
    ./scripts/Verify-Startup.ps1 -OutputDirectory artifacts/startup-latest-check -ShellLaunch -WithData -ComplexAppearance -WallpaperBlur 40 -ExecutablePath artifacts/publish/latest-win-x64/GenshinGachaAnalyzer.exe
    ./scripts/Verify-Ui.ps1 -Published -OutputDirectory artifacts/ui-check
    ./scripts/Package.ps1

## Windows 提示与验证边界

当前应用未签名。普通 Release、RID Release 和 latest 入口检查未发现 Zone.Identifier；没有最初版本的现场记录，无法确认它当时为什么没有提示。微软说明未签名文件的新构建需要重新积累文件信誉；本地修正 Medium 标签不等于代码签名，不保证 SmartScreen 提示消失。见 [SIGNING.md](SIGNING.md)。本次 ShellExecute 检查未被系统提示阻挡，没有自动绕过安全窗口。

上述检查只使用新建隔离目录和合成档案。作者原档案能否在新版本正常加载需要实际使用确认，没有用合成用例代替真实数据验证。游戏加加注入挂起是独立问题，未重新启用注入并宣称稳定共存。没有真实 authkey 联调；下一 WPF 渲染帧检查不保证所有硬件和叠加层组合的最终呈现性能。
