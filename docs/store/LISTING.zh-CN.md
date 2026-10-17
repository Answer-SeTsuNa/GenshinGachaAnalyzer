# 商店详情草稿

商店名称已保留；发布者“答元耐一”已经维护者确认。0.1.23 已通过认证，后续版本重新提交审核；实时状态以 Partner Center 和商店页面为准。

产品名称：木偶祈愿分析

短介绍：在本机管理、备份和分析原神祈愿记录，支持米游社登录获取与 UIGF 数据迁移。

## 详细介绍

木偶祈愿分析是一款非官方的原神祈愿记录管理与分析工具。祈愿档案留在你的电脑，不上传给开发者。

- 无需启动游戏即可登录米游社获取国服祈愿记录，默认扫码，也支持官方账号登录页。登录后清晰展示昵称、UID 和服务器，由你选择要获取的账号。
- 支持 UIGF 文件导入与导出，方便从其他工具迁移、备份已有档案。自动跳过重复记录，发现冲突时保留原件。
- 按卡池、时间和物品查看历史，展示抽数、已记录区间、角色与武器统计以及已知排期中的复刻时间间隔。
- 卡池与物品资料独立更新，新增官方图片按需缓存；断网仍能查看已有档案。
- 支持日间、夜间主题和本地壁纸。
- 商店版应用更新由 Microsoft Store 管理，档案放在用户拥有的独立目录，卸载应用后仍保留。

本工具不提供抽卡、充值、游戏修改、作弊或概率预测功能。显示结果取决于已有记录和已收录的排期，不能证明历史完整性，不推导武器定轨或集录命定选择。

目前支持国服原神；不支持国际服或云原神的本地缓存获取。官方服务可能限制可查询的历史范围，请及时导出备份。

本应用与米哈游 / HoYoverse 无隶属、赞助或官方授权关系。相关游戏名称和素材权利归各自权利人所有；本项目代码的 MIT 许可不授予游戏素材权利。

## 提交字段建议

- 类别：实用工具（以后台可选项为准）。
- 定价：免费；不含广告和应用内购买。
- 网站 / 支持： https://github.com/Answer-SeTsuNa/GenshinGachaAnalyzer
- 隐私政策：已在后台属性页直接填写 docs/store/PRIVACY.md 的完整政策文本；若以后改为公开 URL，须先同步文档并确认可匿名打开。
- 关键词建议：原神、祈愿记录、抽卡分析、UIGF、本地档案、备份。
- 年龄分级：由维护者按 IARC 问卷如实填写，不预填虚构评级。
- 市场：以后台允许的地区及维护者确认结果为准，不默认承诺全球可用。

## 审核备注草稿

This is an unofficial local wish-history management utility for Genshin Impact. It does not perform purchases, gacha transactions, game injection, or gameplay modification. Core offline functionality can be reviewed by importing a synthetic UIGF fixture supplied in the submission materials; no real miHoYo account is required for offline review. Optional login uses official miHoYo services with transient in-memory credentials, and QR codes are generated locally. Only the standard runFullTrust capability needed by the existing WPF desktop app is requested; the app does not request elevation, drivers, services, or unvirtualizedResources. Store builds open Microsoft Store for application updates. Archive files are intentionally kept in a user-owned directory outside package-managed application data so that uninstalling the app does not delete user records.

## 提交前由维护者确认

- 发布者与联系方式、商店名称、免费定价、分发市场。
- 应用图标及所有随包游戏素材具有用于商店分发的必要权利；来源声明不能代替权利许可。
- 身份验证与后台协议由维护者本人查看和确认。
- 正式包身份必须来自 Partner Center；开发预览包及自造身份不能提交。
