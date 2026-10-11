param([switch]$Publish, [switch]$InProcessTests)
$ErrorActionPreference = 'Stop'
$buildRoot = Split-Path -Parent $PSScriptRoot
Push-Location $buildRoot
try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    dotnet restore GenshinGachaAnalyzer.sln
    if ($LASTEXITCODE -ne 0) { throw '依赖还原失败' }
    dotnet build GenshinGachaAnalyzer.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw '构建失败' }
    if ($InProcessTests) {
        # 受限宿主环境显式选用官方 xUnit 执行器；不是跳过测试。
        & (Join-Path $PSScriptRoot 'Test-InProcess.ps1')
    } else {
        dotnet test GenshinGachaAnalyzer.Tests/GenshinGachaAnalyzer.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=core-tests.trx' --results-directory artifacts/TestResults
        if ($LASTEXITCODE -ne 0) { throw '测试失败；若仅为 Testhost 父进程权限错误，可显式使用 -InProcessTests。' }
    }
    if ($Publish) {
        [xml]$props = Get-Content Directory.Build.props
        $publishVersion = $props.Project.PropertyGroup.Version
        dotnet publish GenshinGachaAnalyzer.WPF/GenshinGachaAnalyzer.WPF.csproj -c Release -r win-x64 --self-contained true -o "artifacts/publish/v$publishVersion-win-x64"
        if ($LASTEXITCODE -ne 0) { throw '发布失败' }
        # 固定入口随每次发布更新；版本目录保留便于回溯。所有文件来自完整自包含产物。
        $versionDirectory = Join-Path $buildRoot "artifacts/publish/v$publishVersion-win-x64"
        $latestDirectory = Join-Path $buildRoot 'artifacts/publish/latest-win-x64'
        foreach ($publishedFile in Get-ChildItem -LiteralPath $versionDirectory -File -Recurse) {
            $relativeFile = [IO.Path]::GetRelativePath($versionDirectory, $publishedFile.FullName)
            $latestFile = Join-Path $latestDirectory $relativeFile
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($latestFile)) | Out-Null
            [IO.File]::Copy($publishedFile.FullName, $latestFile, $true)
        }
        # 工作区可能带有沙箱的 Low 标签，EXE 会继承它，双击也会变为低完整性进程。
        # 仅修正这两个生成的应用入口为普通 Medium，不提权、不修改源目录或用户数据 ACL。
        foreach ($entryDirectory in @($versionDirectory, $latestDirectory)) {
            $entry = Join-Path $entryDirectory 'GenshinGachaAnalyzer.exe'
            & icacls.exe $entry /setintegritylevel Medium | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "发布入口完整性标记修正失败：$entry" }
        }
        Write-Output "最新发布入口：$latestDirectory/GenshinGachaAnalyzer.exe"
    }
} finally { Pop-Location }
