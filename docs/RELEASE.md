# GitHub 发布指南

当前版本：0.1.13。默认仅增加补丁位，前两位由作者决定。版本源为 Directory.Build.props。

## 创建仓库前

1. 在 GitHub 创建空仓库，填写名称与介绍；不要上传整个参考目录。
2. 本地先检查 .gitignore：参考、资源原件、artifacts、bin / obj、个人 DB、logs、settings、backups 已排除。
3. WPF/Resources/AppIcon.png 与 App.ico 是正式图标副本，进入仓库；核对作者提供图标的分发权限。
4. 将实际 HTTPS 仓库 URL 写入 GenshinGachaAnalyzer.WPF/appsettings.json 的 RepositoryUrl。
5. 检查 LICENSE、THIRD_PARTY_NOTICES 与 docs 内参考项目许可证均保留。
6. 建议仓库开启 Private vulnerability reporting、Issue 模板和主分支保护。

资源、卡池图与角色名不因本项目代码 MIT 协议而转让权利。第三方来源声明应随二进制保留。

## 本地验证与打包

    ./scripts/Build.ps1 -Publish
    ./scripts/Verify-Ui.ps1 -Published
    ./scripts/Package.ps1

所有命令成功后，artifacts 包含：

- GenshinGachaAnalyzer-v0.1.13-win-x64.zip：完整自包含应用。
- GenshinGachaAnalyzer-v0.1.13-source.zip：干净源码，含 GitHub 配置。
- v0.1.13-SHA256SUMS.txt：两个包的 SHA256。

检查解压后的 EXE 版本、应用图标、第三方许可证和完整资源。确认没有个人 DB、authkey、测试输出或参考源码。
程序未做代码签名，使用校验摘要确认下载完整性。解压完整目录运行，不仅复制 EXE。

## 上传和自动发布

项目仓库：[Answer-SeTsuNa/GenshinGachaAnalyzer](https://github.com/Answer-SeTsuNa/GenshinGachaAnalyzer)。初次上传使用私有仓库，主分支为 main；维护者使用获授权的账号执行 Git 提交与推送。

准备好 0.1.13 后：

    git tag v0.1.13
    git push origin v0.1.13

标签必须与 Directory.Build.props 完全一致。工作流在 Windows 上构建、测试、打包，成功后以 CHANGELOG.md 作为发布说明创建 Release 并附加 ZIP 和校验文件。
普通 push / PR / 手动运行只生成 Actions 制品，不自动创建 Release。

下次修改默认运行 Bump-Version.ps1，得到 0.1.14；同步更新 CHANGELOG 后推送 v0.1.14。
维护者自行决定发布时间，不让脚本自动改变 major / minor。项目包体较大，二进制放 Releases，不提交 artifacts。

每次 Build.ps1 -Publish 还会更新固定入口 artifacts/publish/latest-win-x64/GenshinGachaAnalyzer.exe，包含完整运行依赖；版本目录与 ZIP 继续保留。隔离 UI 验证检查对应版本程序，固定入口使用同一批产物。

受限宿主环境可使用 ./scripts/Build.ps1 -Publish -InProcessTests。此入口仅替换 VSTest 的进程通信，不改变测试与产品断言；发布前仍要求全部 Core 用例通过。

发行前还需运行 ./scripts/Verify-Startup.ps1 -WithData -ComplexAppearance。当前包未签名，正式签名流程见 SIGNING.md；SHA256 不等同于签名或信誉。
