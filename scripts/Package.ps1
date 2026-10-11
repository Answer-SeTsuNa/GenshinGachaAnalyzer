param([string]$Version)
$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactFolder = Join-Path $packageRoot 'artifacts'
[xml]$versionProps = Get-Content (Join-Path $packageRoot 'Directory.Build.props')
$projectVersion = [string]$versionProps.Project.PropertyGroup.Version
if (!$Version) { $Version = $projectVersion }
if ($Version -ne $projectVersion) { throw 'Package version must match Directory.Build.props.' }
$publishFolder = Join-Path $artifactFolder "publish/v$Version-win-x64"
if (!(Test-Path -LiteralPath (Join-Path $publishFolder 'GenshinGachaAnalyzer.exe'))) { throw '请先完成自包含发布。' }
Add-Type -AssemblyName System.IO.Compression

function Write-Package([string]$Path, [string]$Base, [string[]]$Files) {
    $archiveStream = [IO.File]::Open($Path, [IO.FileMode]::Create)
    $archive = [IO.Compression.ZipArchive]::new($archiveStream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($relative in $Files) {
            $full = [IO.Path]::GetFullPath((Join-Path $Base $relative))
            if (!$full.StartsWith($Base.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '打包路径超出项目范围。' }
            $entry = $archive.CreateEntry($relative.Replace('\', '/'), [IO.Compression.CompressionLevel]::Optimal)
            $inputStream = [IO.File]::OpenRead($full)
            $outputStream = $entry.Open()
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose(); $inputStream.Dispose() }
        }
    } finally { $archive.Dispose(); $archiveStream.Dispose() }
}

Push-Location $packageRoot
try {
    # 参考源码、缓存、构建输出和隔离测试数据库不进入源码交付包。
    $sourceFiles = @(foreach ($sourceRoot in @('GenshinGachaAnalyzer.Core', 'GenshinGachaAnalyzer.WPF', 'GenshinGachaAnalyzer.Tests', 'docs', 'scripts', '.github')) {
        Get-ChildItem -LiteralPath (Join-Path $packageRoot $sourceRoot) -File -Recurse -Force | ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($packageRoot, $_.FullName).Replace('\', '/')
            if ($relative -notmatch '/(bin|obj|\.vs)/|^scripts/dev/|/Resources/Themes/|\.(db|db-wal|db-shm|log|partial)$') { $relative }
        }
    })
    $sourceFiles += @('.editorconfig', '.gitignore', 'Directory.Build.props', 'GenshinGachaAnalyzer.sln', 'global.json', 'LICENSE', 'NuGet.Config', 'README.md', 'THIRD_PARTY_NOTICES.md', 'AGENTS.md', 'CONTRIBUTING.md', 'SECURITY.md', 'CHANGELOG.md')
    $sourceArchive = Join-Path $artifactFolder "GenshinGachaAnalyzer-v$Version-source.zip"
    Write-Package $sourceArchive $packageRoot $sourceFiles
    $binaryFiles = @(Get-ChildItem -LiteralPath $publishFolder -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($publishFolder, $_.FullName) })
    $binaryArchive = Join-Path $artifactFolder "GenshinGachaAnalyzer-v$Version-win-x64.zip"
    Write-Package $binaryArchive $publishFolder $binaryFiles
    # 发布校验文件仅含包名和摘要，不暴露构建机绝对路径。
    $checksums = @($binaryArchive, $sourceArchive) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) }
    [IO.File]::WriteAllLines((Join-Path $artifactFolder "v$Version-SHA256SUMS.txt"), $checksums, [Text.UTF8Encoding]::new($false))
    Get-Item -LiteralPath $sourceArchive, $binaryArchive | Select-Object Name, Length
} finally { Pop-Location }


