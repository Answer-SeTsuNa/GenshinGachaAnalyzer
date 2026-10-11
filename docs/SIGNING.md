# Windows 发行签名

0.1.12 的本地发布版尚未签名，Get-AuthenticodeSignature 状态为 NotSigned。
“Windows 已保护你的电脑 / 更多信息”属于 SmartScreen 应用信誉提示，不能据此认定病毒；新签名应用也可能尚无足够信誉。

SmartScreen 会参考发行者和文件哈希信誉。未签名 EXE 重新编译后哈希改变，新版本需要重新积累信誉，因此可能反复提示。没有最初版本的现场记录，不能确定它当时未提示的原因。2026-10-12 检查的普通 Release、RID Release 和 latest 入口均未签名，未发现 Zone.Identifier；没有把当前提示归因于该下载标记。

应用 EXE 的 Medium 完整性标签只修复本地工作区继承 Low 导致的数据目录无法写入，不是签名，也不会赋予 SmartScreen 信誉。

正式发行需要作者持有的可信代码签名证书或授权签名服务。目前项目没有此类证书，不能通过改代码保证消除 Windows 提示。
不创建自签证书冒充正式签名，不导入根证书、不关闭安全设置，也不通过修改文件名承诺消除警告。
SHA256 校验用于确认文件完整性，不代替发行签名或安全检测。

取得合法证书后，使用 Windows SDK SignTool，先签名，再验证、检查启动、打包并重新生成校验摘要：

```powershell
signtool sign /sha1 <作者的证书指纹> /fd SHA256 /tr <证书服务商的RFC3161时间戳地址> /td SHA256 artifacts/publish/v0.1.12-win-x64/GenshinGachaAnalyzer.exe
signtool verify /pa /v artifacts/publish/v0.1.12-win-x64/GenshinGachaAnalyzer.exe
Get-AuthenticodeSignature artifacts/publish/v0.1.12-win-x64/GenshinGachaAnalyzer.exe
./scripts/Verify-Startup.ps1 -WithData -ComplexAppearance
./scripts/Package.ps1
```

签名后的文件需同步到固定发布入口；第三方 DLL 保留原有签名，不使用本项目证书冒充第三方作者。
现有 GitHub 工作流仍生成未签名包，不宣称已建立 Windows 信任。

来源：[微软 SmartScreen 发行信誉](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)、[SignTool 文档](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool)。
