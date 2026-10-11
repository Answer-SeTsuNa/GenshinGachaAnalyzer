param([string]$ReportPath)
$ErrorActionPreference = 'Stop'
$testRoot = Split-Path -Parent $PSScriptRoot
[xml]$versionInfo = Get-Content (Join-Path $testRoot 'Directory.Build.props')
if (!$ReportPath) { $ReportPath = Join-Path $testRoot "artifacts/TestResults/core-tests-v$($versionInfo.Project.PropertyGroup.Version)-inprocess.json" }
$previousTemp = $env:TEMP; $previousTmp = $env:TMP
try {
    # 所有合成测试临时目录限定在工作区，不触碰真实 AppData 资料。
    $env:TEMP = Join-Path $testRoot 'artifacts/test-temp'
    $env:TMP = $env:TEMP
    [IO.Directory]::CreateDirectory($env:TEMP) | Out-Null
    dotnet run --project (Join-Path $PSScriptRoot 'TestRunner/TestRunner.csproj') -c Release -- $ReportPath
    if ($LASTEXITCODE -ne 0) { throw 'xUnit 进程内执行失败；请检查 JSON 中每个原始测试的结果。' }
} finally { $env:TEMP = $previousTemp; $env:TMP = $previousTmp }
