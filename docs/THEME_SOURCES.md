# 主题设计与来源

0.1.2 按作者引用的 UI 对话最后一轮方案，其余主题使用 HoYoWiki 官方站点的元素原图，木偶按作者追加要求改为眼内齿轮。六款主题为木偶、神里绫华、可莉、钟离、芙芙、牛牛。

| 主题 | 元素图标 | 配色 |
| --- | --- | --- |
| 木偶 | 眼内齿轮（矢量纹饰） | 铜棕 / 米白 |
| 神里绫华 | 冰 | 冰蓝 |
| 可莉 | 火 | 暖红 |
| 钟离 | 岩 | 琥珀金 |
| 芙芙 | 水 | 湛蓝 |
| 牛牛（瓦雷莎） | 雷 | 粉色 |

0.1.9 恢复 0.1.0 的主题色板、正文和辅助文字颜色；牛牛采用它初次加入的 0.1.1 色板。图标与配色独立维护，替换图案不同时调整配色。

使用作者所引用对话中保存的五枚 84×84 透明 PNG 原件，文件逐字节保持不变。预览保留原色原形；背景通过原图透明通道显示单色剪影，始终等比缩放。背景仅两枚低对比元素与少量细线，木偶单独使用正圆齿轮；其余移除旧自绘雪花、天星、水滴、四叶草与牛角面罩。

图标嵌入程序，只在本地读取，冻结后共享解码结果。软件运行时不请求 HoYoWiki、不下载主题素材。官方提供的原图为小尺寸，放大背景主要作淡色装饰，不宣称其为高清矢量素材。用户仍可选择本地壁纸，模糊仅作用于壁纸。

## 原图来源

- 冰：[cryo.b810caa.png](https://wiki.hoyolab.com/_nuxt/img/cryo.b810caa.png)
- 火：[pyro.2267e27.png](https://wiki.hoyolab.com/_nuxt/img/pyro.2267e27.png)
- 水：[hydro.3e969aa.png](https://wiki.hoyolab.com/_nuxt/img/hydro.3e969aa.png)
- 雷：[electro.be07020.png](https://wiki.hoyolab.com/_nuxt/img/electro.be07020.png)
- 岩：[geo.2498e06.png](https://wiki.hoyolab.com/_nuxt/img/geo.2498e06.png)

来源文件及 SHA256 保存于 Resources/Elements/sources.json。木偶参考[官网角色原画](https://genshin.hoyoverse.com/en/character/snezhnaya?char=15)及[官网角色介绍](https://genshin.hoyoverse.com/en/news/detail/165011)；其余角色信息可见[绫华](https://wiki.hoyolab.com/pc/genshin/entry/32)、[可莉](https://wiki.hoyolab.com/pc/genshin/entry/3457)、[钟离](https://wiki.hoyolab.com/pc/genshin/entry/3840)、[芙宁娜](https://wiki.hoyolab.com/pc/genshin/entry/4376)、[瓦雷莎](https://wiki.hoyolab.com/pc/genshin/entry/7488)。这次只改主题，未更新游戏卡池快照或改写抽卡记录。

## 木偶眼内齿轮

官方原画：[桑多涅 PNG](https://fastcdn.hoyoverse.com/content-v2/hk4e/164628/b7ac8ad4f04ac902fe72d21fea626b85_7160228098189552413.png)，取自官网角色页公开内容（164628）。2026-10-10 已查看原图，围绕圆形瞳孔的齿轮环作为轮廓参考。原画存于 artifacts/design-reference，仅供开发核对，不随程序分发。

ThemeArtwork.CreatePupilGear 以圆孔、短齿和细外环简化为矢量徽记，背景与预览复用同一绘图。简化纹饰使用八短齿，不宣称官方眼内齿数或官方图标；不使用冰元素图标，也不引入实时模糊/透明遮罩合成。统一圆心、半径和等比变换，白天/夜晚按主题色绘制。

## 材质与其他素材

- 亚克力材质参考 FufuLauncher 1.7.0.3 的背景分层；[原项目](https://github.com/CodeCubist/FufuLauncher)，MIT 全文见 FufuLauncher.LICENSE。
- [Microsoft DWM_SYSTEMBACKDROP_TYPE](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type)：Desktop Acrylic 需要 Windows 11 22621。
- 卡池图片、物品身份继续来自胡桃 Snap.Metadata；来源和许可证见 THIRD_PARTY_NOTICES.md。
- 应用图标由作者提供，正式资源为 AppIcon.png 与多尺寸 App.ico。

原神元素图标、其他游戏美术与角色名的权利归米哈游 / HoYoverse 或对应原权利人。MIT 只覆盖本项目代码，不自动授予素材权利；没有把官方图标标为本项目原创或 MIT 素材。
