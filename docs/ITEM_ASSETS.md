# 角色与武器图片来源、更新方法

核对日期：2026-10-15。自 0.1.22 起，应用会更新公开物品资料，按需下载安装包尚未包含的官方图片，保留来源 URL 与 SHA256 到数据目录 catalog/images；内置图片仍可离线使用。维护者同步脚本继续用于更新安装包内的回退资源。两种同步均不需要 UID、authkey 或 Cookie，不读取抽卡数据库，不上传用户信息。

## 两个参考项目分别从哪里下载

### Snap.Hutao.Remastered 1.20.3

本地源码中，`Model/Metadata/Converter/AvatarIconConverter.cs` 和 `EquipIconConverter.cs` 将元数据里的图标名转换为下载地址。`Web/Endpoint/Hutao/StaticResourcesEndpoints.cs` 拼接 `/static/raw/{category}/{fileName}`；`Web/ServerDomain.cs` 选择 `https://api.snaphutaorp.org` 或备用 `https://api.hutaorp.org`。

其直接来源是胡桃自己的静态资源服务，例如 `AvatarIcon/UI_AvatarIcon_Nicole.png`、`EquipIcon/UI_EquipIcon_*.png`；**这两个服务不是米哈游官方域名**。当前公开客户端和元数据仓库没有为每张镜像图片提供原始官方 URL，不能据此声称它们来自某个特定官网页面。图标名称是游戏资源标识，也不能用它猜测一个必定存在的官方图片 URL。

核对代码：[StaticResourcesEndpoints](https://github.com/SnapHutaoRemasteringProject/Snap.Hutao.Remastered/blob/main/src/Snap.Hutao.Remastered/Snap.Hutao.Remastered/Web/Endpoint/Hutao/StaticResourcesEndpoints.cs)、[AvatarIconConverter](https://github.com/SnapHutaoRemasteringProject/Snap.Hutao.Remastered/blob/main/src/Snap.Hutao.Remastered/Snap.Hutao.Remastered/Model/Metadata/Converter/AvatarIconConverter.cs)。本项目早期图标通过该镜像下载，不是从参考项目打包目录复制。

### FufuLauncher 1.7.0.3

本地 `ViewModels/GachaAnalysisModel.Metadata.cs` 的 `FetchMetadataFromApiAsync` / `FetchCalculatorListAsync` 请求米游社**原神养成计算器**列表，读取每个条目的 `id`、`name`、`icon` 和星级。接口常量在 `Constants/ApiEndpoints.cs` 第 91–92 行。

| 内容 | 官方 POST 接口 |
| --- | --- |
| 角色 | `https://api-takumi.mihoyo.com/event/e20200928calculate/v1/avatar/list` |
| 武器 | `https://api-takumi.mihoyo.com/event/e20200928calculate/v1/weapon/list` |

角色请求示例：`{"page":1,"size":200,"is_all":true}`。武器请求示例：`{"page":1,"size":200,"weapon_levels":[1,2,3,4,5]}`。需要普通浏览器 User-Agent、`Referer: https://webstatic.mihoyo.com/` 与 `Origin: https://webstatic.mihoyo.com`；本次不带 Cookie 的请求均返回 `retcode=0`。这是该官方网页使用的接口，并非承诺永久稳定的开发者 API；若日后发生变化，脚本会停止并提示核对。

`icon` 返回实际图片地址。本次来源包括 `act-webstatic.mihoyo.com/hk4e/e20200928calculate/item_icon/`、`uploadstatic.mihoyo.com/hk4e/e20200928calculate/common/` 和 `fastcdn.mihoyo.com/static-resource-v2/`。脚本只接受明确核对的官方域名与路径，不走社区图床，不生成图片。

原网页：[原神养成计算器](https://webstatic.mihoyo.com/ys/event/e20200923calculate/index.html)。官方图片示例：[尼可头像](https://act-webstatic.mihoyo.com/hk4e/e20200928calculate/item_icon/6a0cf98d/233e3baf9bd2c030a49b46c31173da9e.png)。完整地址由接口返回，包含版本目录和哈希，不应自行拼接。旧式 `game_record/genshin/character_icon/UI_AvatarIcon_*.png` 在本次探测中对不同名字返回同一张占位图；HTTP 200 不能当作下载成功的唯一证据。

## 以后更新版本

在项目根目录使用 PowerShell 7：

```powershell
# 只查询、查看待下载数量，不改文件。
./scripts/Sync-OfficialItemAssets.ps1 -DryRun

# 默认：更新官方物品名称 / 星级快照，下载全部缺少的角色和武器图片。
./scripts/Sync-OfficialItemAssets.ps1

# 已存在的图片也全部重新从官方接口返回的地址下载。
./scripts/Sync-OfficialItemAssets.ps1 -Force

# 只修复指定物品，例如尼可；已有图片需要配合 -Force。
./scripts/Sync-OfficialItemAssets.ps1 -ItemId 10000131 -Force

./scripts/Build.ps1 -Publish
./scripts/Verify-Startup.ps1 -WithData -ComplexAppearance
./scripts/Verify-Ui.ps1 -Published
./scripts/Package.ps1
```

不需要下载两个参考项目。脚本分页读取列表，按物品 ID 去重多元素变体，排除旅行者；全批下载校验通过后才替换资源。网络失败保留已有资源，不接受 HTML、异常尺寸或已知占位图；图片和 JSON 采用原子替换。暂存图片在 `artifacts/official-item-sync`，不进入源码包。不会改变抽卡原始记录、账号数据库或 UIGF 文件。

资源输出：

- `GenshinGachaAnalyzer.Core/Resources/items.json`：显示和分析用的离线物品快照。
- `GenshinGachaAnalyzer.WPF/Resources/Items/{物品ID}.png`：离线头像 / 武器图标。
- `GenshinGachaAnalyzer.WPF/Resources/Items/sources.json`：实际下载文件的官方 URL、元数据接口、UTC 下载时间及 SHA256。

本轮更新后，快照有 374 个物品，每个都有本地 PNG。尼可与其余缺少的图片共 126 张从上述官方接口补齐；其余早期镜像图片保留，**没有伪造它们的官方下载来源或写进官方来源清单**。若要把所有旧图统一为直接官方来源，运行 `-Force`。以后默认只补缺失图片；已有图片需要刷新时也使用 `-Force`。

脚本不会更新 UP 排期、卡池宣传图或判断命定选择。它们仍由单独的已收录排期快照提供，不能从角色列表推导。当期名称与星级仅用于显示，原始抽卡记录不补写。

原神图片版权归米哈游 / HoYoverse，项目代码 MIT 协议不授予游戏素材版权。请保留第三方声明与来源清单。
