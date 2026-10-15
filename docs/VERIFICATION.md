# 0.1.23 更名与商店分发准备验证

日期：2026-10-15（Asia/Shanghai）。公开名称统一为“木偶祈愿分析”，保留内部程序集与桌面版数据目录；增加 MSIX 构建、包身份检测及商店更新入口。

- Build.ps1 -Publish -InProcessTests 成功：Core xUnit 196 通过 / 0 失败 / 0 跳过；Release 0 警告 / 0 错误。AppPaths 测试覆盖桌面 / 商店两种目录解析。
- artifacts/ui-v0.1.23-final-brand：完整隔离 UI 回归通过，绑定日志为空；合成更新检查覆盖桌面 GitHub 通道与商店固定更新 URI，商店入口不请求 GitHub 发布接口。
- artifacts/startup-v0.1.23-final-latest：固定发布入口 ShellExecute 启动、2,832 条合成记录及页面 / 主题切换通过，用时 10,625ms。
- artifacts/installer-v0.1.23-final：独立安装标识下的中文空格路径、覆盖升级、开始菜单程序 / 卸载快捷方式、启动与 UI、卸载及合成文件保留检查通过；1,132 个运行文件、资源和许可证逐项 SHA256 一致。测试不替换正式安装，也不清理作者的正式快捷方式。
- artifacts/store-v0.1.23-final-brand：现有开发者模式下注册最终更名的开发包，真实包身份激活、合成记录启动、完整 UI、卸载后实际合成数据库哈希保留检查通过。未导入证书、未更改安全设置、未读取作者真实档案。
- artifacts/store/v0.1.23-submission：采用 Partner Center 实际产品身份生成未签名 MSIX，MakeAppx 结构验证通过。它是商店提交候选包，不能作为普通双击安装版。
- 产品名称已在 Partner Center 保留，发布者显示名由作者确认为“答元耐一”，作者选择免费、仅中国大陆。微软后台已将正式身份 MSIX 标记为 Validated；中文介绍和合成截图已保存。作者亲自完成 IARC，后台生成 Microsoft 3+ 分级。Submission 1 已提交认证，后台显示提交完成、预处理进行中，审核后手动发布。runFullTrust 审批、微软审核及正式商店分发仍待完成；WACK 完整认证检查尚未运行。开发包验证不代表正式安装或认证通过。

下列为历史验证记录。

# 0.1.22 卡池独立更新与账号展示验证

日期：2026-10-15（Asia/Shanghai）。增加启动时后台检查与设置页手动资料更新，登录成功页显示明确的昵称、UID 和服务器卡片。

- Build.ps1 -Publish -InProcessTests 成功；Release 0 警告 / 0 错误，Core xUnit 194 通过 / 0 失败 / 0 跳过。新增覆盖在线新排期 / 新物品、日期更正替换、离线缓存、损坏缓存、失败回退、重定向拒绝、响应大小与图片地址约束。
- artifacts/ui-v0.1.22-final：完整隔离 UI 回归通过，绑定日志为空；合成资料更新后新卡池立即出现在历史页面，失败不丢失资料，缓存可离线恢复。扫码成功卡片明确显示“合成测试账号”、UID 100000001 和天空岛，单账号隐藏切换列表，不显示 JSON、链接或临时凭据。
- artifacts/catalog-online-v0.1.22-final：真实公开资料检查通过，301 个排期、374 个物品，覆盖至 2026-11-03。同一类型 / 版本 / 期次无重复；当前来源列出虚星临渡、莓色香颂、神铸赋形，从 2026-10-13 18:00 开始。此记录只说明来源实际返回的资料，不认定用户所说的“今天的新卡池”一定已被来源收录。
- 同一隔离目录从实际官方 URL 下载卡池图与物品图到未预装的测试文件名，保存来源 / SHA256，缓存读取通过。公开资料请求不携带账号信息；无真实登录或作者 AppData 档案读取，也未使用屏幕控制。
- artifacts/startup-v0.1.22-final-latest：固定发布入口 ShellExecute、2,832 条合成记录、页面 / 主题切换通过，用时约 13.4 秒。
- 真实账号登录到记录获取仍需用户自行验证；官方验证码属于官方登录步骤，本工具的成功确认页只展示账号信息。资料来源未收录的新排期保持未知，不推测 UP 或命定选择。

下列为历史验证记录。

# 0.1.21 米游社登录获取验证

日期：2026-10-15（Asia/Shanghai）。默认扫码，可切换官方账号登录；登录前展示隐私说明并由用户主动开始。

- Build.ps1 -Publish -InProcessTests 成功；Release 0 警告 / 0 错误，Core xUnit 181 通过 / 0 失败 / 0 跳过。新增覆盖扫码状态、官方账号凭据解析、接口错误脱敏、官方域名约束、请求头注入拒绝和选定 UID 不匹配时不写入数据库。
- artifacts/ui-v0.1.21-final：完整隔离 UI 回归通过，97 张 PNG，bindings.log 0 字节。合成扫码登录覆盖开始前不发请求、账号与 UID 确认、取消及关闭清理；全程不使用真实账号或 AppData 数据库。
- 同目录 official-account-page-verification.txt：真实官方空白登录页加载成功，显示短信 / 密码登录，确认 WebView2 InPrivate 会话和禁用密码保存 / 自动填充；未输入真实账号或密码。关闭后清理独立浏览器会话。
- 匿名官方创建二维码请求返回 retcode 0，二维码与 ticket 均存在；未保存或输出其内容，也未扫码。二维码在本地生成，不发送给第三方二维码服务。
- 临时凭据不写入设置、档案或诊断日志；只向相应米哈游官方接口请求认证与祈愿数据。保留 QRCoder 和 WebView2 第三方许可证。本轮未使用屏幕控制。
- 真实账号授权、角色绑定与完整记录拉取尚未端到端实测，需要用户在新版中自行登录验证；合成测试与匿名页面检查不能代替该项。

下列为历史验证记录。

# 0.1.20 公开发布准备验证

日期：2026-10-15（Asia/Shanghai）。补全 Git 忽略、源码包排除规则、公开仓库与签名来源工作流。

- 本地 Build.ps1 -Publish -InProcessTests 成功；Release 0 警告 / 0 错误，Core xUnit 164 通过 / 0 失败 / 0 跳过。
- artifacts/startup-v0.1.20-latest：固定入口 ShellExecute 与 2,832 条合成记录启动、页面 / 主题检查通过。
- artifacts/ui-v0.1.20：完整 UI 回归通过，94 张 PNG，绑定日志为空；更新按钮继续使用合成网络响应验证。
- 公开前检查当前跟踪文件与所有历史对象，未发现数据库、日志、参考 / 原始资源目录、构建输出、签名私钥或常见 GitHub 令牌格式。忽略规则已验证覆盖 VS 缓存、二进制、环境配置和私钥；不读取真实 AppData 档案。
- 远程 GitHub Actions、Release 和构建来源签名属于推送后的独立检查，应以对应运行结果和发布资产为准。本地 Windows EXE 尚无可信 Authenticode 签名，SignPath 申请不会自动赋予证书。

下列为历史验证记录。

# 0.1.19 交付验证

日期：2026-10-14（Asia/Shanghai）。设置页版本与 GitHub 检查更新。

- Build.ps1 -Publish -InProcessTests 成功，Release 0 警告 / 0 错误；Core xUnit 164 通过 / 0 失败 / 0 跳过。新增覆盖数字版本比较、三段 / 四段版本归一化、404 / 限流 / 服务异常、异常响应、预发布拒绝与 GitHub 地址约束。
- artifacts/startup-v0.1.19-latest：固定入口 ShellExecute 启动、2,832 条合成记录、页面与主题切换通过。
- artifacts/ui-v0.1.19：完整页面回归通过，包含合成更新响应下的新版提示、对应 GitHub 下载入口、再次检查 404 时清除旧下载状态，以及通用发布页入口。见 update-verification.txt 和 13-settings-update.png；未打开真实浏览器，未使用真实数据库。
- 实际匿名 GET https://api.github.com/repos/Answer-SeTsuNa/GenshinGachaAnalyzer/releases/latest 返回 HTTP 404。不可据此断言已是最新版；应用显示尚无公开正式版本或私有仓库提示。用户普遍可用的自动比较需要公开可读取的发布信息；未修改仓库可见性或发布远程版本。

下列为历史验证记录。

# 0.1.18 交付验证

日期：2026-10-14（Asia/Shanghai）。按作者指定的 FufuLauncher 参考调整背景，并减少页面切换和图片解码开销。

- 标准 Build.ps1 -Publish -InProcessTests 成功；Release 0 警告 / 0 错误，Core xUnit 149 通过 / 0 失败 / 0 跳过。
- artifacts/startup-v0.1.18-complex：ShellExecute 启动、2,832 条合成记录、4K 合成壁纸、模糊 40、页面 / 主题切换及原生标题栏检查通过。进程完整性 8192；游戏加加仍在运行，SoftwareOnly，GppModuleLoaded=false。
- backdrop-verification.txt：遮罩 10% / 45% / 72% / 95% 的移动前 / 中 / 后 Alpha 分别为 26 / 115 / 184 / 242，各组三次一致；原生 DWM 背景类型保持 Desktop Acrylic。移动钩子三组进入 0.03–0.37ms、退出 0.27–0.36ms；这是消息处理耗时，不是实际拖动帧率。
- 相同复杂外观用例的一次首帧测量：缓存页面约 10–19ms；首次历史约 236ms、我的约 275ms、总览约 599ms。首次页面仍有布局成本，数据不代表所有硬件上的帧率，也不保证每页首次加载都比旧版快。
- artifacts/ui-v0.1.18：完整页面回归通过，93 张 PNG，bindings.log 0 字节；374 张物品图标、298 张卡池图片解码通过。缓存页面已验证无整页动画且不透明度为 1。
- 本轮未使用屏幕控制。检查只使用隔离目录和合成档案；遮罩像素与原生材质设置检查不代替桌面模糊的人工视觉验收。保留第三方许可证。

下列为历史验证记录。

# 0.1.17 交付验证

日期：2026-10-14（Asia/Shanghai）。修复日间标题栏按钮对比度和拖动延迟。

- 标题栏单独设置主题实色底色及文字色。artifacts/startup-v0.1.17-final-complex/caption-verification.txt 覆盖六种主题的 System / Light / Dark，共 18 组原生设置成功；色值对比度 11.40–13.93:1。当前系统对这些属性的 DwmGet 返回 E_INVALIDARG，改为检查 DwmSet 的成功结果，并另做实际原生窗口视觉检查，没有把不可读取的属性伪装成读取成功。
- 使用 Computer Use 在 artifacts/preview-v0.1.17 的合成档案窗口检查日间标题栏，确认最小化、最大化、关闭按钮清晰。实际标题栏拖动后截图原点从 (849,354) 变为 (619,505)，调用约 151ms（包含工具开销，不是逐帧延迟指标）。检查后关闭本次隔离预览。
- 拖动 / 缩放进入和退出通过实际 HWND 消息走 MainWindow 的原生钩子。artifacts/startup-v0.1.17-final-complex/window-move-verification.json 的三组进入处理为 0.59 / 13.05 / 12.65ms，退出为 16.40 / 18.10 / 15.86ms；断言期间使用不透明合成、退出后恢复透明背景、移动中主题刷新不重新启用模糊、重复消息幂等、关闭模糊时不错误开启，以及壁纸对象保持复用。
- Core xUnit 149 通过 / 0 失败 / 0 跳过；Release 编译 0 警告 / 0 错误。复杂外观、2,832 条合成记录、4K 合成壁纸和模糊 40 的 ShellExecute 启动、页面及主题切换通过。游戏加加运行时保留 SoftwareOnly 和扩展点限制。
- artifacts/ui-v0.1.17-final：完整页面回归通过，93 张 PNG，bindings.log 0 字节；374 张角色 / 武器图标和 298 张卡池图片解码通过。
- 初次固定 latest 同步被运行中的旧版占用 Accessibility.dll 阻止，版本目录已完整生成；作者确认关闭后继续同步和最终入口检查。使用新建隔离目录与合成档案，未读取或修改真实 AppData 数据库。保留第三方许可证，不推送远程发布。

下列为历史验证记录。

# 0.1.16 交付验证

日期：2026-10-14（Asia/Shanghai）。接手安装权限、游戏加加兼容、失焦模糊和应用图标修改。

- 作者提供的 0.1.15 日志显示进程完整性 4096，AppData 日志与数据库加载被拒绝；只读检查确认自定义安装目录中的 EXE 继承 Low 标签。安装器现在修正自己维护的应用 / 卸载 EXE 为 Medium，保留 asInvoker 与当前用户安装，不修改真实档案或数据目录权限。
- 标准 Build.ps1 -Publish -InProcessTests 成功，Release 0 警告 / 0 错误，Core xUnit 149 通过 / 0 失败 / 0 跳过；版本为 0.1.16.0。版本目录与 latest 的 1,120 个文件逐项 SHA256 一致。
- artifacts/startup-v0.1.16-final-complex、final-empty、final-latest：ShellExecute 启动、页面 / 主题切换通过，含 2,832 条合成记录、4K 合成壁纸和模糊 40，以及空档案。均为完整性 8192；游戏加加保持运行，报告 OverlayDetected=true、ExtensionPointsDisabled=true、SoftwareOnly、GppModuleLoaded=false。历史旧版现场已加载 GPP64.dll 并无响应；测试未结束或改动游戏加加及作者卡死窗口。此结果限定于当前机器 / 当前叠加层版本，不保证阻止所有注入。
- artifacts/ui-v0.1.16-final：93 张页面 PNG，bindings.log 0 字节；374 张物品图片与 298 张卡池图片完整解码通过。隔离交互预览正常显示，已核对新图标与合成档案界面。
- artifacts/installer-v0.1.16-final-low：随机独立 AppId，在继承 Low 标签的中文 / 空格目录中安装、覆盖升级、快捷方式、1,118 个运行文件摘要、已安装应用启动与 93 张 UI PNG、卸载全部通过；合成用户文件保持原摘要。已安装应用完整性 8192，游戏加加共存与模块报告同上；测试登记和快捷方式已清理，未覆盖作者正式安装。
- 应用与关于页图标使用资源/木偶图标.png 的 WPF 正式副本，PNG SHA256 与指定原图一致；ICO 含 16–256 像素的七种尺寸，安装器使用同一 ICO。
- 背景改用不随输入焦点切换的窗口模糊策略，保留系统 / 实色回退。最终 -VerifyBackdrop 检查未获得实际前台焦点，Computer Use 随后被作者按 Esc 中止；该项不计入通过项，失焦时的实际视觉效果仍待人工验收。离屏截图不作为原生材质正确的证据。
- 所有数据库、设置、壁纸和导入内容均来自新建隔离目录或合成档案，未读取或修改作者真实 AppData 数据库。发布保留第三方许可证；安装目录、原始资源、日志、数据库和构建输出不进入源码包。CI 的安装回归已增加 Low 目录场景；本轮未推送或触发远程发布。

下列为历史验证记录。

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
