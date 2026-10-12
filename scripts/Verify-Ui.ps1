param([string]$OutputDirectory, [switch]$Published, [string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
$verifyRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $verifyRoot ('artifacts/ui-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$verifyOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath (Join-Path $verifyOutput 'gacha.db')) { throw '请使用新的隔离目录，避免混入上一次的测试档案。' }
$verifyExecutable = Join-Path $verifyRoot 'GenshinGachaAnalyzer.WPF/bin/Release/net10.0-windows10.0.19041.0/GenshinGachaAnalyzer.exe'
if ($Published) {
    [xml]$props = Get-Content (Join-Path $verifyRoot 'Directory.Build.props')
    $verifyVersion = $props.Project.PropertyGroup.Version
    $verifyExecutable = Join-Path $verifyRoot "artifacts/publish/v$verifyVersion-win-x64/GenshinGachaAnalyzer.exe"
}
$verifyFixture = Join-Path $verifyRoot 'GenshinGachaAnalyzer.Tests/Fixtures/synthetic-ui-v4.1.json'
if ($ExecutablePath) { $verifyExecutable = [IO.Path]::GetFullPath($ExecutablePath) }
if (-not (Test-Path -LiteralPath $verifyExecutable)) { throw '请先运行 Build.ps1。' }
$verifyArguments = '--verify-ui "' + $verifyOutput + '" "' + $verifyFixture + '"'
$verifyStart = [Diagnostics.ProcessStartInfo]::new($verifyExecutable)
$verifyStart.UseShellExecute = $false
$verifyStart.ArgumentList.Add('--verify-ui'); $verifyStart.ArgumentList.Add($verifyOutput); $verifyStart.ArgumentList.Add($verifyFixture)
$verifyProcess = [Diagnostics.Process]::Start($verifyStart)
if (!$verifyProcess.WaitForExit(180000)) { $verifyProcess.Kill(); throw "UI 验证超时：$verifyOutput" }
if ($verifyProcess.ExitCode -ne 0) { throw "UI 验证失败，请检查 $verifyOutput 中的错误文件。" }
$bindingLog = Join-Path $verifyOutput 'bindings.log'
if ((Get-Item -LiteralPath $bindingLog).Length -gt 0) { throw "存在 WPF Binding 错误：$bindingLog" }
Write-Output "验证完成，PNG 与报告：$verifyOutput"

