param([Parameter(Mandatory=$true)][string]$PackageDirectory, [string]$OutputDirectory, [switch]$StartupOnly)
$ErrorActionPreference = 'Stop'
$verificationRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $verificationRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
$directory = [IO.Path]::GetFullPath($PackageDirectory)
if (!$directory.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)) { throw '只能检查 artifacts 中的本地开发包。' }
$info = Get-Content -LiteralPath (Join-Path $directory 'package-info.json') -Raw | ConvertFrom-Json
if (!$info.Preview -or $info.Name -ne 'Muou.GenshinGachaAnalyzer.Preview' -or $info.Publisher -ne 'CN=Muou Development') { throw '此脚本只注册固定的开发预览身份，不安装或卸载正式商店版。' }
if (Get-AppxPackage -Name $info.Name) { throw '已有同名开发包，请先保留现场；本脚本不覆盖或卸载既有安装。' }
$developerMode = (Get-ItemProperty 'HKLM:/SOFTWARE/Microsoft/Windows/CurrentVersion/AppModelUnlock' -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
if ($developerMode -ne 1) { throw '当前 Windows 未启用开发者模式；本脚本不会更改安全设置或导入证书。布局和未签名 MSIX 已保留。' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $directory 'verification' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (!$output.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $output)) { throw '必须使用 artifacts 内的新建隔离验证目录。' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$manifest = [IO.Path]::GetFullPath((Join-Path $directory 'layout/AppxManifest.xml'))
if (!$manifest.StartsWith($directory.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw '清单路径越界。' }
if (-not ('GachaStoreActivation' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
[ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IGachaActivation {
 [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id,[MarshalAs(UnmanagedType.LPWStr)] string args,uint options,out uint processId);
 [PreserveSig] int ActivateForFile(IntPtr a,IntPtr b,IntPtr c,out uint processId);
 [PreserveSig] int ActivateForProtocol(IntPtr a,IntPtr b,out uint processId);
}
[ComImport, Guid("45ba127d-10a8-46ea-8ab7-56ea9078943c")] class GachaActivationManager { }
public static class GachaStoreActivation {
 public static uint Start(string id,string args) {
  object instance = new GachaActivationManager();
  try { uint processId; int result = ((IGachaActivation)instance).ActivateApplication(id,args,0,out processId); Marshal.ThrowExceptionForHR(result); return processId; }
  finally { Marshal.FinalReleaseComObject(instance); }
 }
}
'@
}
$registered = $null
try {
    Add-AppxPackage -Register $manifest
    $registered = Get-AppxPackage -Name $info.Name
    if (!$registered) { throw '开发预览包注册失败。' }
    $aumid = $registered.PackageFamilyName + '!App'
    $startup = Join-Path $output 'startup'; $fixture = Join-Path $verificationRoot 'GenshinGachaAnalyzer.Tests/Fixtures/synthetic-ui-v4.1.json'
    $arguments = '--verify-startup "' + $startup + '" "' + $fixture + '"'
    $childId = [GachaStoreActivation]::Start($aumid,$arguments)
    $child = [Diagnostics.Process]::GetProcessById($childId)
    $null = $child.Handle # 提前持有外部激活进程的句柄，确保退出后仍能取得退出码。
    try { if (!$child.WaitForExit(60000)) { $child.Kill(); throw '开发包启动验证超时。' }; $exitCode = $child.ExitCode; if($exitCode -ne 0) { throw "开发包启动验证失败，进程 $childId，退出码 [$exitCode]。" } } finally { $child.Dispose() }
    if ((Get-Content -LiteralPath (Join-Path $startup 'package-identity.txt') -Raw).Trim() -ne 'Packaged') { throw '测试进程没有实际包身份。' }
    # 再次激活同一已注册包，走完整 WPF 回归；每次都使用全新合成档案目录。
    $ui = Join-Path $output 'ui'
    if (!$StartupOnly) {
    $childId = [GachaStoreActivation]::Start($aumid,('--verify-ui "' + $ui + '" "' + $fixture + '"'))
    $child = [Diagnostics.Process]::GetProcessById($childId)
    $null = $child.Handle
    try { if (!$child.WaitForExit(180000)) { $child.Kill(); throw '开发包 UI 验证超时。' }; if($child.ExitCode -ne 0) { throw '开发包 UI 验证失败。' } } finally { $child.Dispose() }
    if ((Get-Item -LiteralPath (Join-Path $ui 'bindings.log')).Length -ne 0) { throw '开发包存在界面绑定错误。' }
    }
    # 核对实际由已打包进程写出的合成数据库，不以宿主脚本新建的占位文件代替档案。
    $sentinel = Join-Path $startup 'gacha.db'
    $sentinelHash = (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash
    Remove-AppxPackage -Package $registered.PackageFullName; $registered = $null
    if ((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash) { throw '卸载后外部用户档案被改动。' }
    $uiResult = if ($StartupOnly) { '未运行完整 UI；' } else { '完整 UI 通过；' }
    [IO.File]::WriteAllText((Join-Path $output 'store-verification.txt'),"现有开发者模式下注册开发布局、真实包身份激活、合成档案启动通过；${uiResult}卸载及外部数据保留通过。未导入证书、未更改系统安全设置、未读取真实档案。此结果不代表正式商店安装或微软审核已通过。`n")
    Get-Content -LiteralPath (Join-Path $output 'store-verification.txt')
} finally {
    if ($registered) { Remove-AppxPackage -Package $registered.PackageFullName }
}
