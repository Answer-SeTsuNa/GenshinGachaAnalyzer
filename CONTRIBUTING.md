# 参与开发

安装 .NET 10 SDK，在 Windows 上执行 `./scripts/Build.ps1`。
核心服务位于 Core，展示与交互位于 WPF。业务计算尽量放在 Core 并添加能重现问题的回归测试。

数据入口只有游戏获取和 UIGF。禁止补造记录、上传个人档案、引入遥测或存储 authkey。
游戏元数据和统计口径参照胡桃祈愿模块。UP 未覆盖时保留未知。

版本从 `Directory.Build.props` 读取，默认只递增补丁：

```powershell
./scripts/Bump-Version.ps1 -Preview
./scripts/Bump-Version.ps1
```

前两位版本号只由作者决定。提交前检查 `AGENTS.md`，运行构建、测试和新目录的隔离 UI 验证。
Issue 或 PR 可附合成复现数据，不附真实 UID、日志中的个人信息或认证链接。
