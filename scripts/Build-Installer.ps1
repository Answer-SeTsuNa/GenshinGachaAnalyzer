param([string]$CompilerPath)
$ErrorActionPreference = 'Stop'
$installerRoot = Split-Path -Parent $PSScriptRoot
[xml]$props = Get-Content (Join-Path $installerRoot 'Directory.Build.props')
$version = [string]$props.Project.PropertyGroup.Version
$publish = Join-Path $installerRoot "artifacts/publish/v$version-win-x64"
$entry = Join-Path $publish 'GenshinGachaAnalyzer.exe'
if (!(Test-Path -LiteralPath $entry)) { throw '请先运行 Build.ps1 -Publish。' }
if ((Get-Item -LiteralPath $entry).VersionInfo.ProductVersion.Split('+')[0] -ne $version) { throw '发布程序与当前版本不一致。' }
foreach ($license in @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'InnoSetup.LICENSE', 'InnoSetup.ChineseSimplified.LICENSE')) {
    if (!(Test-Path -LiteralPath (Join-Path $publish $license))) { throw "发布目录缺少许可证：$license" }
}
if (!$CompilerPath) { $CompilerPath = & (Join-Path $PSScriptRoot 'Get-InnoSetup.ps1') }
& $CompilerPath '/Qp' "/DAppVersion=$version" "/DProjectRoot=$installerRoot" "/DPublishDir=$publish" (Join-Path $PSScriptRoot 'installer/GenshinGachaAnalyzer.iss')
if ($LASTEXITCODE -ne 0) { throw '安装包编译失败。' }
$setup = Join-Path $installerRoot "artifacts/GenshinGachaAnalyzer-Setup-v$version-win-x64.exe"
if (!(Test-Path -LiteralPath $setup)) { throw '未生成安装包。' }
& icacls.exe $setup /setintegritylevel Medium | Out-Null
if ($LASTEXITCODE -ne 0) { throw '安装包完整性标记修正失败。' }
Get-Item -LiteralPath $setup | Select-Object FullName, Length
