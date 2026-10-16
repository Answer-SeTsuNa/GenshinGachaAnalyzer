# 下载渠道与发布

桌面版设置页提供安装版和便携版，每种包均可选择公益代理 1（gh-proxy.org）、公益代理 2（ghproxy.net）或 GitHub 直连。代理转发公开 GitHub Release 文件，不是独立托管镜像，服务可能限流或失效；连接失败时切换渠道。

当前安装版本的入口不依赖检查更新成功即可使用。检查更新成功后，只接受与本仓库、版本标签和标准文件名一致、已上传完成且非空的 GitHub 下载资产。新版尚无可用资产时，可通过“查看新版发布说明”进入 GitHub；检查失败时回退当前安装版本的入口，避免继续显示旧的更新下载地址。

代理入口只在用户主动点击时由系统浏览器打开，不自动下载或运行安装包，不传输 UID、Cookie、认证地址或本地档案。商店版不显示桌面下载渠道，继续由 Microsoft Store 更新。

## 后续发布无需手填链接

1. 使用既有构建、测试和打包流程生成新版本。
2. 推送与 Directory.Build.props 一致的版本标签，例如 v0.1.27。
3. GitHub Actions 自动上传资产，并调用 scripts/New-ReleaseNotes.ps1 生成包含安装版/便携版、两个公益代理和直连入口的发布说明。
4. 用户检查更新后，软件自动使用新版本的资产链接。

应用与发布脚本共用 GenshinGachaAnalyzer.Core/Resources/download-proxies.json；只有代理服务变更时才需要维护此配置。普通源码提交不会自动创建 Release，仍需版本标签。

## 渠道验证

scripts/Verify-DownloadProxies.ps1 -Version 0.1.27 会只读取两个渠道各包的前 64 KiB，核对文件类型及可用时的总长度。加上 -FullInstaller 会通过代理 1 完整下载安装包，并与 GitHub 公布的 SHA256 和长度核对。产物保存在 artifacts，不自动运行下载的安装器。

Microsoft Store 更新程序包需要重新提交认证，不能把已批准的旧包直接替换成新二进制。2026-10-16 后台显示 0.1.23 认证通过、准备手动发布；桌面渠道版本独立递增。
