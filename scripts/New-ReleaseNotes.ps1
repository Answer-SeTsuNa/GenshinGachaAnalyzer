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
