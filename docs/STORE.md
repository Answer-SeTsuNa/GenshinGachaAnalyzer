# Microsoft Store 分发

状态：正在准备，尚未上架。使用 MSIX 提交，微软审核通过后在商店分发时签名；现有 GitHub EXE 不会因此获得 Authenticode 签名。

## 账号与正式身份

从 https://storedeveloper.microsoft.com/ 进入个人开发者注册，由维护者本人登录、验证身份并查看协议。保留应用名称后，从 Partner Center 的产品身份页取得 Package/Identity/Name、Package/Identity/Publisher 和发布者显示名。复制 scripts/store/store-identity.example.json 到 artifacts 内，填入后台准确值。

不要使用示例值、自造 GUID 或开发预览身份提交。不要把密码、验证码、证件或访问令牌放入配置。

## 构建

先执行 `scripts/Build.ps1 -Publish -InProcessTests`。本地开发包：

```powershell
./scripts/Build-Store.ps1 -Preview
./scripts/Verify-Store.ps1 -PackageDirectory artifacts/store/v0.1.23-preview
```

验证脚本仅在本机原本已经开启开发者模式时注册固定的开发预览布局，使用真实包身份激活、全新隔离目录及合成档案，完成后注销该开发包。它不启用开发者模式、不导入证书、不覆盖已有正式安装，也不读取维护者真实数据库。此验证不等于正式商店安装或微软审核通过。

正式提交包：

```powershell
./scripts/Build-Store.ps1 -IdentityFile artifacts/store-identity.json
```

输出包含布局、未签名 MSIX、SHA256 及 MakeAppx 结构校验日志。未签名提交包不能作为普通用户双击安装版；交由 Partner Center 验证和商店签名。

## 档案迁移与保留

桌面版继续使用原 AppData/Roaming/GenshinGachaAnalyzer，不自动移动数据库。商店版在用户目录的“木偶祈愿档案”保存记录、设置、日志、备份及公开资料缓存，不使用随卸载清理的包数据目录。商店版与桌面版档案互相独立；先在原版导出全部账号的 UIGF，再导入商店版。确认 UID 和记录数量后再处理原安装。

商店版只由 Microsoft Store 更新程序，卡池资料继续独立检查。不开启管理员权限，也不申请用于绕过 AppData 虚拟化的 unvirtualizedResources。

## 上架材料与剩余验收

中文商店草稿：docs/store/LISTING.zh-CN.md；隐私政策：docs/store/PRIVACY.md。截图只用合成档案；后台支持直接填写隐私政策文本，若使用 URL 则必须公开可访问。素材权利、身份信息、市场、IARC 问卷和后台协议由维护者确认。

正式提交前还需：使用后台准确身份重新打包；在测试包中验证安装 / 更新、数据迁移、文件选择、官方登录页和扫码功能；运行 Windows App Certification Kit 并检查结果；上传后处理 Partner Center 的能力说明和审核反馈。任何本地测试均不能保证商店批准。
