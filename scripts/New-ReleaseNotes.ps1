param([string]$Version, [string]$OutputFile)
$ErrorActionPreference = 'Stop'
$notesRoot = Split-Path -Parent $PSScriptRoot
[xml]$props = Get-Content (Join-Path $notesRoot 'Directory.Build.props')
if (!$Version) { $Version = [string]$props.Project.PropertyGroup.Version }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
if (!$OutputFile) { $OutputFile = Join-Path $notesRoot "artifacts/release-v$Version-notes.md" }
$proxies = @(Get-Content (Join-Path $notesRoot 'GenshinGachaAnalyzer.Core/Resources/download-proxies.json') -Raw | ConvertFrom-Json)
$assetRoot = "https://github.com/Answer-SeTsuNa/GenshinGachaAnalyzer/releases/download/v$Version/"
$lines = [Collections.Generic.List[string]]::new()
$lines.Add("# 木偶祈愿分析 $Version")
$lines.Add('')
$lines.Add('## 下载')
$lines.Add('')
$lines.Add('| 版本 | 公益代理 1 | 公益代理 2 | GitHub 直连 |')
$lines.Add('| --- | --- | --- | --- |')
foreach ($package in @(
    @{Label='安装版'; File="GenshinGachaAnalyzer-Setup-v$Version-win-x64.exe"},
    @{Label='便携版'; File="GenshinGachaAnalyzer-v$Version-win-x64.zip"}
)) {
    $url = $assetRoot + $package.File
    $links = @($proxies | ForEach-Object { '[下载](' + $_.Prefix + $url + ')' })
    $lines.Add('| ' + $package.Label + ' | ' + ($links -join ' | ') + ' | [下载](' + $url + ') |')
}
$lines.Add('')
$lines.Add('公益代理由第三方提供，连接失败时请切换渠道。程序无需管理员权限，自带 .NET 运行环境。')
$lines.Add('')
$lines.Add('### 第一次使用怎么选？')
$lines.Add('')
$lines.Add('- **安装版（推荐新手）**：双击下载的安装文件，按中文提示点击“下一步”。安装后从桌面或开始菜单打开“木偶祈愿分析”。升级时先关闭软件，再安装新版，已有记录和设置会保留。')
$lines.Add('- **便携版（免安装）**：右键下载的压缩包，选择“全部解压”；打开解压文件夹，双击 **GenshinGachaAnalyzer.exe**。请保留所有文件和子文件夹，不要在压缩包里直接运行，也不要只复制一个 EXE。')
$lines.Add('- 两种版本功能相同。便携版的记录仍保存在这台电脑的用户数据目录，搬走程序文件夹不会带走记录；换电脑请先在软件中导出全部账号，再在新电脑导入 UIGF。含 source 的源码包供开发者使用。')
$lines.Add('')
$lines.Add('### 微软商店安装')
$lines.Add('')
$lines.Add('截至 2026-10-16，商店版已通过认证、等待发布，暂未开放下载。现在请选安装版或便携版；是否开放请以实际商店页面为准。')
$lines.Add('')
$lines.Add('正式上架后：打开 Windows 开始菜单 → Microsoft Store（微软商店）→ 搜索“木偶祈愿分析” → 确认发布者“答元耐一” → 点击“获取 / 安装” → 完成后点击“打开”。商店版与桌面版档案独立，切换时请先从原版导出全部账号，再到商店版导入 UIGF。')
$lines.Add('')
$lines.Add('[微软商店页面](https://apps.microsoft.com/detail/9P34RSKFWZGJ?hl=zh-CN&gl=CN) · [完整下载与安装指南](https://github.com/Answer-SeTsuNa/GenshinGachaAnalyzer/blob/main/docs/DOWNLOADS.md)')
$lines.Add('')
$lines.Add('[SHA256 校验文件](' + $assetRoot + "v$Version-SHA256SUMS.txt" + ') · [源码包](' + $assetRoot + "GenshinGachaAnalyzer-v$Version-source.zip" + ')')
$lines.Add('')
$lines.Add('## 本版更新')
$lines.Add('')
$changelog = Get-Content (Join-Path $notesRoot 'CHANGELOG.md') -Raw
$entry = [regex]::Match($changelog, '(?ms)^## ' + [regex]::Escape($Version) + '\s[^\r\n]*\r?\n(.*?)(?=^## |\z)')
if (!$entry.Success) { throw "Missing changelog entry: $Version" }
$lines.Add($entry.Groups[1].Value.Trim())
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($OutputFile), $lines, [Text.UTF8Encoding]::new($false))
Write-Output "Release notes: $OutputFile"
