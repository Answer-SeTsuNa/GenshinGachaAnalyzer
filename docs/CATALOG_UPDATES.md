# 卡池资料独立更新

0.1.22 起，卡池排期、物品名称 / 星级和新增图片不再依赖应用重新发布。

- 应用启动先读取已验证的本地缓存，再打开档案；距上次成功检查至少 6 小时时，在后台检查公开资料。设置中的“更新卡池资料”可随时主动检查。
- 0.1.25 起，当期国服卡池直接读取米哈游公开端点 `https://operation-webstatic.mihoyo.com/gacha_info/hk4e/cn_gf01/gacha/list.json`，再按列表中的 gacha_id 读取同目录下的 `{gacha_id}/zh-cn.json`。起止时间按国服 UTC+8 解释；UP 物品按官方名称、类型、星级匹配真实 ID（详情中的 item_id 可能是负数占位符）。官方未提供版本号和期次时显示日期，不推算。
- 历史排期继续读取 SnapHutaoRemasteringProject/Snap.Metadata 的 main/Genshin/CHS/GachaEvent.json，并保留已缓存历史；官方当期结果优先覆盖重叠区间。两类排期资料源独立回退，一方失效不阻止另一方更新，均失败则保留可用缓存。
- 参考版 FufuLauncher 1.7.0.3 的 GachaAnalysisModel.PoolMetadata 从 yshelper 的 getWishHistory 接口读取卡池，Metadata 模块从官方养成计算器获取角色与武器。本工具的物品与新增图片仍使用官方养成计算器实际返回的地址。
- 更新请求不携带 UID、Cookie 或授权信息，使用独立无 Cookie、禁止自动重定向的 HTTP 会话。排期最多 8 MiB，验证时间范围、卡池类型、物品 ID、数量与文件路径后，再原子替换本地 snapshot.json。
- 同一类型、游戏版本、期次的日期更正替换已有排期，避免同时保留旧日期而重复展示。已有历史中上游未返回的期次继续保留；覆盖范围倒退或异常响应不会覆盖可用缓存。
- 图片按需从米哈游 / 米游社 / HoYoverse 官方 HTTPS 域名下载，最大 10 MiB；保存来源及 SHA256，拒绝已知占位图。内置图片优先，新增缓存不写入安装目录。
- 数据只参与显示与统计，不修改抽卡数据库原始字段，也不进入 UIGF 导出。未收录的时期保持未知，不推测未来复刻或命定选择。

这套功能无需自建付费服务器。资料更新速度取决于公开来源；上游接口或数据格式发生不兼容变化时，仍可能需要应用更新。连接失败时继续使用本地资料。

开发验证：Core 的 WishCatalogUpdateTests 覆盖新增、更正、缓存损坏、失败回退、重定向、大小限制与官方图片地址约束。默认 UI 回归使用合成响应。需要检查真实公开资料时，可运行 `scripts/Verify-Ui.ps1 -Published -CatalogOnlineOnly -OutputDirectory artifacts/新的隔离目录`；不会登录账号或读取真实档案。
