param([string]$OutputDirectory, [switch]$WithData, [switch]$ComplexAppearance, [switch]$ShellLaunch, [double]$WallpaperBlur = 2.112, [string]$ExecutablePath, [switch]$VerifyBackdrop)
$ErrorActionPreference = 'Stop'
$startupRoot = Split-Path -Parent $PSScriptRoot
[xml]$versionProps = Get-Content (Join-Path $startupRoot 'Directory.Build.props')
$version = [string]$versionProps.Project.PropertyGroup.Version
if (!$OutputDirectory) { $OutputDirectory = Join-Path $startupRoot ('artifacts/startup-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw '启动检查必须使用新的隔离目录。' }
[IO.Directory]::CreateDirectory($output) | Out-Null
if ($ComplexAppearance) {
    # 合成壁纸，不读取作者壁纸或真实数据库；复现高透明度、夜间和壁纸模糊组合。
    Add-Type -AssemblyName System.Drawing
    $wallpaper = Join-Path $output 'synthetic-wallpaper.bmp'
    $bitmap = [Drawing.Bitmap]::new(3840, 2160)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(40, 30, 65))
        $graphics.FillRectangle([Drawing.Brushes]::LightPink, 350, 350, 900, 800)
        $bitmap.Save($wallpaper, [Drawing.Imaging.ImageFormat]::Bmp)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
    @{ Theme='Dark'; StyleKey='Varesa'; Acrylic=$true; SemiTransparent=$true; OverlayOpacity=.387; PanelOpacity=.35; WallpaperOpacity=.869; WallpaperBlur=$WallpaperBlur; WallpaperPath=$wallpaper; UiScale=0 } | ConvertTo-Json | Set-Content (Join-Path $output 'settings.json')
}
$exe = if ($ExecutablePath) { [IO.Path]::GetFullPath($ExecutablePath) } else { Join-Path $startupRoot "artifacts/publish/v$version-win-x64/GenshinGachaAnalyzer.exe" }
if (!(Test-Path -LiteralPath $exe -PathType Leaf)) { throw "找不到待检查的应用入口：$exe" }
$start = [Diagnostics.ProcessStartInfo]::new($exe)
$start.UseShellExecute = [bool]$ShellLaunch
# ShellExecute 使用与资源管理器相同的启动路径；路径逐项引号，不能依赖仅 CreateProcess 的检查。
$start.Arguments = '--verify-startup "' + $output + '"'
if ($WithData) { $start.Arguments += ' "' + (Join-Path $startupRoot 'GenshinGachaAnalyzer.Tests/Fixtures/synthetic-ui-v4.1.json') + '"' }
if ($VerifyBackdrop) { $start.Arguments += ' --verify-backdrop' }
if (-not ('GachaStartupLaunch' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Diagnostics;
using System.Threading.Tasks;
public static class GachaStartupLaunch {
    public static Task<Process> Start(ProcessStartInfo info) => Task.Run(() => Process.Start(info));
}
'@
}
# ShellExecute 自身可能停在 Windows 文件安全提示，必须连进程创建阶段一起限制等待。
$launch = [GachaStartupLaunch]::Start($start)
if (!$launch.Wait(30000)) { throw "启动器 30 秒未返回，可能有等待人工处理的系统提示；本次未通过：$output" }
$helper = $launch.GetAwaiter().GetResult()
if ($null -eq $helper) { throw "启动器未返回应用进程：$output" }
try {
    if (!$helper.WaitForExit(60000)) { $helper.Kill(); throw "启动检查超时：$output" }
    if ($helper.ExitCode -ne 0 -or !(Test-Path (Join-Path $output 'startup-verification.txt'))) { throw "启动检查失败，退出码 $($helper.ExitCode)：$output" }
    Get-Content (Join-Path $output 'startup-verification.txt')
} finally { $helper.Dispose() }
