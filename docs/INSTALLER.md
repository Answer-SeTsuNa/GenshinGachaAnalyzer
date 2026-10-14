# EXE 安装包

下载 Releases 中的 `GenshinGachaAnalyzer-Setup-v版本号-win-x64.exe`，双击按中文向导安装。可选择目录和桌面快捷方式，开始菜单提供程序及卸载入口。默认目录为 `%LocalAppData%\Programs\GenshinGachaAnalyzer`，按当前用户安装，不请求管理员权限。

安装包包含 .NET 运行环境、依赖库、官方游戏图标和许可证，不需要在线下载运行组件。支持 Windows 10 2004 及以上的 x64 兼容系统。ZIP 便携版继续提供。

0.1.16 起，安装器在复制完成后为自己维护的应用 EXE 和卸载 EXE 设置 Medium 完整性标签，避免自定义目录继承 Low 后启动受限。不会修改数据目录或安装目录的整体权限，不要求提权；旧版本用户运行新版安装包覆盖升级即可修正入口。

## 升级与卸载

关闭程序后运行新版安装包，会沿用安装位置和同一个 Windows 卸载登记。安装向导遇到文件被占用时提供关闭应用的选项。安装结束可选择启动程序。

从 Windows 设置的“已安装的应用”中卸载，或使用开始菜单的卸载入口。安装器仅维护自己的程序文件和快捷方式，不读写 `%AppData%\GenshinGachaAnalyzer` 的数据库、设置、日志或备份。用户自行添加到安装目录的文件也不使用整目录递归删除。

程序与安装包当前未签名，Windows 可能显示发行信誉提示。SHA256 用于核对文件完整性，签名说明见 [SIGNING.md](SIGNING.md)。

## 构建

版本仅从 `Directory.Build.props` 读取。发布完整程序后执行 `scripts/Package.ps1`，会调用 `scripts/Build-Installer.ps1`，将安装 EXE、便携 ZIP 和源码 ZIP 一同列入校验文件。也可单独执行 `scripts/Build-Installer.ps1 -CompilerPath 路径`。

首次构建通过 `scripts/Get-InnoSetup.ps1` 下载以下固定工具并验证 SHA256，仅解压到 `artifacts/tools`，不向系统安装编译器，不注册文件关联。缓存目录和生成产物不进入 Git 或源码包。

| 工具 | 固定来源 | SHA256 |
| --- | --- | --- |
| Inno Setup 6.7.3 | https://github.com/jrsoftware/issrc/releases/tag/is-6_7_3 | `9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732` |
| innoextract 6.7.0 格式 Windows 构建 | https://github.com/UserUnknownFactor/innoextract_win/releases/tag/670 | `79b69b9b1fcd98f42ccd4b245efdf6a03bcfb674ba6af482f5a46891c9ed4d14` |

简体中文语言文件固定在 `scripts/installer/ChineseSimplified.isl`，来源提交 `1ff90acc4ed4aee82b1cda43253243deee3daed4`：https://github.com/kira-96/Inno-Setup-Chinese-Simplified-Translation。原文件保留来源，MIT 许可随附 `InnoSetup.ChineseSimplified.LICENSE`；安装器许可随附 `InnoSetup.LICENSE`。

## 隔离验证

运行 `scripts/Verify-Installer.ps1`：在 artifacts 下的新目录安装实际发行 EXE，使用中文 / 空格路径；再次运行安装包，验证复用目录、版本登记、全部安装文件摘要和开始菜单快捷方式；以合成档案执行启动与 UI 验证，然后卸载并验证程序、快捷方式和卸载登记均已移除，合成用户文件保留。验证默认关闭桌面快捷方式，不占用作者的桌面入口。

脚本遇到已有正式安装会拒绝测试，避免覆盖作者环境。可在干净 Windows 用户或 GitHub Actions 中运行。通过 `-PreviousInstallerPath` 指定旧安装包可验证旧版到新版升级。验证日志和截图仅保存在本地 artifacts；自动构建保留对应测试制品。

已有正式安装的开发机可运行 `./scripts/Verify-Installer.ps1 -IsolatedRegistration -LowIntegrityDirectory`。它重新编译使用随机独立 AppId 的测试安装器，在 artifacts 的 Low 目录中安装 / 覆盖升级 / 启动 / 卸载，不覆盖正式安装的登记、目录或快捷方式；安装之后不替安装器修正应用标签，以验证实际修复有效。
