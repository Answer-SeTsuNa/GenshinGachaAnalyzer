param([string]$OutputDirectory, [string]$PreviousInstallerPath, [switch]$LowIntegrityDirectory, [switch]$IsolatedRegistration)
$ErrorActionPreference = 'Stop'
$verificationRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = Join-Path $verificationRoot 'artifacts'
[xml]$props = Get-Content (Join-Path $verificationRoot 'Directory.Build.props')
$version = [string]$props.Project.PropertyGroup.Version
if (!$OutputDirectory) { $OutputDirectory = Join-Path $artifactRoot ('installer-check-' + [guid]::NewGuid().ToString('N')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (!$output.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $output)) {
    throw '安装验证必须使用 artifacts 内的新建隔离目录。'
}
$verificationId = if ($IsolatedRegistration) { 'GachaVerify.' + [guid]::NewGuid().ToString('N') } else { 'GenshinGachaAnalyzer' }
if ($IsolatedRegistration -and $PreviousInstallerPath) { throw '隔离登记不支持使用正式旧安装包。' }
$registryPath = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $verificationId + '_is1'
$registry = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryView]::Registry64)
$existing = $registry.OpenSubKey($registryPath)
if ($existing) { $existing.Dispose(); $registry.Dispose(); throw '检测到已有正式安装，拒绝覆盖。请在干净测试用户或 CI 中运行安装验证。' }
$setup = Join-Path $artifactRoot "GenshinGachaAnalyzer-Setup-v$version-win-x64.exe"
if (!(Test-Path -LiteralPath $setup)) { throw '请先运行 Package.ps1。' }
[IO.Directory]::CreateDirectory($output) | Out-Null
if ($IsolatedRegistration) {
    # 单独 AppId 与安装包，绝不覆盖作者已有安装/快捷方式/卸载登记。
    $compiler = & (Join-Path $PSScriptRoot 'Get-InnoSetup.ps1')
    $publish = Join-Path $artifactRoot "publish/v$version-win-x64"
    & $compiler '/Qp' "/DAppVersion=$version" "/DProjectRoot=$verificationRoot" "/DPublishDir=$publish" "/DInstallerAppId=$verificationId" "/O$output" '/Fverification-setup' (Join-Path $PSScriptRoot 'installer/GenshinGachaAnalyzer.iss')
    if ($LASTEXITCODE -ne 0) { throw '隔离安装包构建失败。' }
    $setup = Join-Path $output 'verification-setup.exe'
    & icacls.exe $setup /setintegritylevel Medium | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '隔离安装器运行标记设置失败。' }
}
# 可使用 Low 继承标签复现作者的自定义安装目录。
# 只设置本次隔离目录，不在安装后修正 EXE，从而真实验证安装器输出。
$testIntegrity = if ($LowIntegrityDirectory) { '(OI)(CI)L' } else { '(OI)(CI)M' }
& icacls.exe $output /setintegritylevel $testIntegrity | Out-Null
if ($LASTEXITCODE -ne 0) { throw '无法准备正常完整性的隔离安装目录。' }
$installDirectory = Join-Path $output '中文 安装目录'
$groupName = 'GachaInstallerVerification-' + [guid]::NewGuid().ToString('N')
$groupDirectory = Join-Path ([Environment]::GetFolderPath('Programs')) $groupName
$uninstaller = Join-Path $installDirectory 'unins000.exe'
$steps = [Collections.Generic.List[string]]::new()
function Invoke-InstallerProcess([string]$Executable, [string[]]$Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new($Executable)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    try {
        if (!$process.WaitForExit(180000)) { $process.Kill($true); throw '安装器操作超时。' }
        if ($process.ExitCode -ne 0) { throw "安装器退出码：$($process.ExitCode)" }
    } finally { $process.Dispose() }
}
function Read-Registration {
    $key = $registry.OpenSubKey($registryPath)
    if (!$key) { throw '缺少 Windows 卸载登记。' }
    try { return @{ Version=$key.GetValue('DisplayVersion'); Location=$key.GetValue('InstallLocation') } }
    finally { $key.Dispose() }
}
# WScript.Shell 的快捷方式属性经过系统 ANSI 代码页；英文 CI 无法可靠读取中文路径。
# 直接使用 IShellLinkW，验证安装器实际写入的 Unicode 目标与工作目录。
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
public static class InstallerShortcutReader {
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, IntPtr findData, uint flags);
        void GetIDList(out IntPtr itemList);
        void SetIDList(IntPtr itemList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int capacity);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int capacity);
    }
    public static string[] Read(string path) {
        object shortcut = new ShellLink();
        try {
            ((IPersistFile)shortcut).Load(path, 0);
            var link = (IShellLinkW)shortcut;
            var target = new StringBuilder(32768);
            var directory = new StringBuilder(32768);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 4);
            link.GetWorkingDirectory(directory, directory.Capacity);
            return new[] { target.ToString(), directory.ToString() };
        } finally { Marshal.FinalReleaseComObject(shortcut); }
    }
}
'@
try {
    $firstSetup = if ($PreviousInstallerPath) { [IO.Path]::GetFullPath($PreviousInstallerPath) } else { $setup }
    Invoke-InstallerProcess $firstSetup @('/SP-', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/RESTARTEXITCODE=10', '/TASKS=', "/DIR=$installDirectory", "/GROUP=$groupName", "/LOG=$(Join-Path $output 'install.log')")
    $registration = Read-Registration
    if ([IO.Path]::GetFullPath($registration.Location).TrimEnd('\') -ne $installDirectory) { throw '安装目录登记不正确。' }
    $steps.Add('中文与空格路径安装通过；Windows 卸载入口已登记。')
    $sentinel = Join-Path $installDirectory 'synthetic-user-data'
    [IO.Directory]::CreateDirectory($sentinel) | Out-Null
    [IO.File]::WriteAllText((Join-Path $sentinel 'gacha.db'), 'synthetic archive; not a real database')
    [IO.File]::WriteAllText((Join-Path $sentinel 'settings.json'), '{"synthetic":true}')
    $sentinelHashes = @(Get-ChildItem -LiteralPath $sentinel -File | Get-FileHash)
    # 第二次不传 DIR/GROUP，验证固定 AppId 会复用原安装位置和开始菜单组。
    Invoke-InstallerProcess $setup @('/SP-', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/RESTARTEXITCODE=10', '/TASKS=', "/LOG=$(Join-Path $output 'upgrade.log')")
    $registration = Read-Registration
    if ($registration.Version -ne $version -or [IO.Path]::GetFullPath($registration.Location).TrimEnd('\') -ne $installDirectory) { throw '覆盖升级后版本或位置不正确。' }
    foreach ($hash in $sentinelHashes) { if ((Get-FileHash -LiteralPath $hash.Path).Hash -ne $hash.Hash) { throw '升级修改了用户文件。' } }
    $steps.Add('覆盖升级复用安装位置，版本正确，合成用户文件完整。')
    $publish = Join-Path $artifactRoot "publish/v$version-win-x64"
    $fileCount = 0
    foreach ($file in Get-ChildItem -LiteralPath $publish -Recurse -File) {
        if ($file.Extension -eq '.pdb') { continue }
        $relative = [IO.Path]::GetRelativePath($publish, $file.FullName)
        $installed = Join-Path $installDirectory $relative
        if (!(Test-Path -LiteralPath $installed) -or (Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw "安装文件缺失或摘要不一致：$relative" }
        $fileCount++
    }
    $steps.Add("$fileCount 个运行文件、资源与许可证逐项 SHA256 校验通过。")
    $linkPath = Join-Path $groupDirectory '木偶祈愿分析.lnk'
    if (!(Test-Path -LiteralPath $linkPath)) { throw "缺少开始菜单快捷方式：$linkPath" }
    $link = [InstallerShortcutReader]::Read($linkPath)
    $exe = Join-Path $installDirectory 'GenshinGachaAnalyzer.exe'
    if ($link[0] -ne $exe -or $link[1] -ne $installDirectory) { throw "开始菜单快捷方式指向错误。目标：$($link[0])；工作目录：$($link[1])；预期：$exe" }
    if (!(Test-Path -LiteralPath (Join-Path $groupDirectory '卸载 木偶祈愿分析.lnk'))) { throw '缺少卸载快捷方式。' }
    $steps.Add('开始菜单程序与卸载快捷方式正确。')
    & (Join-Path $PSScriptRoot 'Verify-Startup.ps1') -ExecutablePath $exe -OutputDirectory (Join-Path $output 'startup') -ShellLaunch -WithData -ComplexAppearance
    & (Join-Path $PSScriptRoot 'Verify-Ui.ps1') -ExecutablePath $exe -OutputDirectory (Join-Path $output 'ui')
    $steps.Add('已安装应用启动与合成档案 UI 验证通过，未读取真实 AppData 数据库。')
    Invoke-InstallerProcess $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=$(Join-Path $output 'uninstall.log')")
    if (Test-Path -LiteralPath $exe) { throw '卸载后仍存在应用入口。' }
    $remainingKey = $registry.OpenSubKey($registryPath)
    if ($remainingKey) { $remainingKey.Dispose(); throw '卸载登记未移除。' }
    if (Test-Path -LiteralPath $groupDirectory) { throw '开始菜单快捷方式未清理。' }
    foreach ($hash in $sentinelHashes) { if ((Get-FileHash -LiteralPath $hash.Path).Hash -ne $hash.Hash) { throw '卸载修改了用户文件。' } }
    $steps.Add('卸载清理程序、快捷方式和登记；合成用户文件保留。')
    $steps | Set-Content -LiteralPath (Join-Path $output 'installer-verification.txt') -Encoding utf8
    $steps | Write-Output
} finally {
    # 失败时也仅卸载此次隔离安装，不枚举或删除作者的数据目录。
    # Inno 的卸载器自删除有短暂延迟；以登记判断是否仍需卸载，不能只看 EXE。
    $cleanupKey = $registry.OpenSubKey($registryPath)
    $cleanupLocation = $null
    if ($cleanupKey) {
        try { $cleanupLocation = [string]$cleanupKey.GetValue('InstallLocation') }
        finally { $cleanupKey.Dispose() }
    }
    if ($cleanupLocation -and $cleanupLocation.TrimEnd('\') -eq $installDirectory -and (Test-Path -LiteralPath $uninstaller)) {
        Invoke-InstallerProcess $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=$(Join-Path $output 'cleanup.log')")
    }
    $registry.Dispose()
}
