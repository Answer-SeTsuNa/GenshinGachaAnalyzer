# UIGF 历史档案迁移

本工具支持 UIGF **v4.2、v4.1、v4.0、v3.0、v2.4、v2.3、v2.2** 的 JSON 导入与导出，默认导出 v4.1。仅支持九位国服 UID（1–5 开头），仅处理普通原神祈愿。

保留原文件备份，直接选择文件导入并核对预检报告。不必手工改版本号：v4 的 info.version + hk4e 多账号结构，与旧版 info.uigf_version + info.uid + list 单账号结构不同。

- v4.2 新增 hk4e_ugc 千星奇域；本工具忽略它及星铁、绝区零数据。
- v3.0 加入集录祈愿 500；v2.x 无法导出集录数据时提示选择新版，绝不静默丢弃。
- v2.4/v3.0 的 region_time_zone 优先于 UID 推断；国服缺省为 UTC+8，入库统一转换为 UTC+8。
- v2.3 起 item_id 必填。v2.2 可以没有该字段，已知中文名称按胡桃物品表精确映射，未知名称保留，不猜测 ID。
- 记录 ID 必须是有效数字字符串。缺 ID 的不完整文件不能可靠去重，将说明缺失字段；请使用原工具重新导出完整档案，不会自动编造记录 ID。
- 旧版为单账号格式，导出 v2/v3 时只选择一个账号；同时导出多个账号使用 v4.x。
- v4 导出保留原始可选字段的缺省状态，展示与分析中的元数据补全不写回原始记录。v2.2 必填名称和类型缺省时仅使用可靠映射，无法匹配则提示选择 v2.3 或更新版本。

规则来源：[UIGF v4.2](https://uigf.org/zh/standards/uigf.html)、[v3.0](https://uigf.org/zh/standards/uigf-legacy-v3.0.html)、[v2.4](https://uigf.org/zh/standards/uigf-legacy-v2.4.html)、[v2.3](https://uigf.org/zh/standards/uigf-legacy-v2.3.html)、[v2.2](https://uigf.org/zh/standards/uigf-legacy-v2.2.html)。本工具未宣称取得 UIGF 官方认证。
