# 启动无响应排查

## 已打开祈愿历史却提示网络超时

2026-10-12 的现场日志确认，0.1.9 已找到缓存凭据，但旧 hk4e-api.mihoyo.com/event/gacha_info/api/getGachaLog 返回 HTTP 404，随后被通用错误误报成超时。

0.1.10 使用参考胡桃项目 ApiEndpoints.csv 的现行国服地址 public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog，缓存与手动输入同时接受该域名的普通祈愿链接。旧缓存链接只提取凭据，不请求已失效地址。请求仍固定到国服官方 HTTPS 地址，不跟随重定向、不携带 Cookie、不支持国际服。

404 与拒绝访问不会反复重试或误报超时，连接失败 / 真正超时 / 临时服务错误保留有限重试。网络失败时此前完整提交的页面保留。诊断日志不记录 authkey、Cookie 或完整认证 URL；无需把祈愿链接粘贴给开发者。

## 已确认的游戏加加兼容问题

2026-10-11，0.1.3 在作者电脑由资源管理器双击启动时挂起。现场进程路径指向 `artifacts/publish/latest-win-x64/GenshinGachaAnalyzer.exe`。

本地线程诊断发现 `GamePPSDK/1.2.1618.725/GPP64.dll`（文件版本 3.7.63.725）已注入该进程。窗口线程的原生调用栈反复经过该 DLL、`RtlEnterCriticalSection` 与 `KiUserExceptionDispatcher`，并等待另一条线程；被等待线程停在 `LdrInitializeThunk` / DLL 加载锁。作者从托盘退出游戏加加后，重新双击同一个 EXE，确认正常打开。

这说明该环境中的挂起与叠加层注入有关。没有把它归因于账号记录、图片数量或数据库损坏。应用不自动结束其他软件，也不尝试卸载已注入的 DLL。

当前可用方法是运行本工具时退出游戏加加。若游戏加加版本提供针对单个程序的叠加层排除功能，可由用户将本工具设为排除对象；具体选项以其实际界面为准。应用尚未声称可以在该注入组件保持启用时稳定运行。

## 日志与验证

- 本地数据目录的 `logs/runtime-日期.log` 记录版本、进程路径、应用构造、资源初始化、单实例检查、窗口显示与档案初始化。不会记录命令行、authkey、Cookie 或认证 URL。
- 0.1.4 起日志入口提前到 WPF 资源加载之前；如果进程连此入口都未到达，可能没有启动日志，不能仅据此判断数据库问题。
- `scripts/Verify-Startup.ps1 -ShellLaunch -WithData -ComplexAppearance` 使用 ShellExecute 并正常显示、激活窗口，覆盖资源管理器的启动路径和复杂外观。它只使用新的隔离目录与合成记录。
- 页面截图检查用于布局，不代替前台启动或用户实际环境的检查。
- SmartScreen 的“Windows 已保护你的电脑”是另一个发行信誉问题，见 [SIGNING.md](SIGNING.md)，退出叠加层不会消除签名提示。
## 数据库未加载且日志也无法写入

0.1.5 本地发布曾继承工作区的 Low 完整性标签。即使从资源管理器双击，应用进程也只有低完整性权限，不能写入普通用户 AppData；日志同样会提示 Access denied。页面零账号是读取失败，并不表示原文件被删除。0.1.6 发布流程只修正生成的应用入口 EXE 为 Medium，运行仍是 asInvoker，不要求管理员，不修改数据目录 ACL 或系统安全开关。

使用最新发布入口重新打开。新版本在加载失败时显示专门页面，点击“查看诊断信息”可读取本次内存日志，即使日志目录不可写。开发验证应检查 process-integrity.txt 为 8192（或系统正常授予的更高值），不能仅用位于同一低完整性工作区里的合成库证明真实用户目录可写。

2026-10-12，作者日志显示实际运行的是 `bin/Release/net10.0-windows10.0.19041.0/GenshinGachaAnalyzer.exe`，完整性仍为 4096。0.1.6 仅修正发布目录，遗漏了这个普通构建入口。0.1.7 将修正加入 WPF 项目的 Build / Publish 目标，覆盖直接 dotnet build、IDE 编译、Debug、Release 和 RID 输出；只处理该项目生成的入口 EXE，失败则构建报错。固定 latest 复制后仍单独修正标签。

指定入口的隔离复现：

    ./scripts/Verify-Startup.ps1 -OutputDirectory artifacts/startup-build-check -ShellLaunch -WithData -ExecutablePath GenshinGachaAnalyzer.WPF/bin/Release/net10.0-windows10.0.19041.0/GenshinGachaAnalyzer.exe

不要将真实数据目录设为低完整性，不要清空或覆盖数据库，不要以关闭 Windows 安全功能作为解决方法。解压 GitHub ZIP 时使用普通用户目录；NTFS 完整性标签不是 ZIP 内的软件配置，源工作区的限制可能再次由目标目录继承。

