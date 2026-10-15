param([string]$IdentityFile, [switch]$Preview, [string]$OutputDirectory, [string]$MakeAppxPath)
$ErrorActionPreference = 'Stop'
$storeRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
[xml]$storeProps = Get-Content (Join-Path $storeRoot 'Directory.Build.props')
$storeVersion = [string]$storeProps.Project.PropertyGroup.Version
$published = Join-Path $storeRoot "artifacts/publish/v$storeVersion-win-x64"
if (!(Test-Path -LiteralPath (Join-Path $published 'GenshinGachaAnalyzer.exe'))) { throw '先运行 Build.ps1 -Publish。' }
if ($Preview -and $IdentityFile) { throw 'Preview 不得使用正式身份文件。' }
if ($Preview) {
    $identity = [pscustomobject]@{ Name='Muou.GenshinGachaAnalyzer.Preview'; Publisher='CN=Muou Development'; PublisherDisplayName='木偶'; DisplayName='木偶祈愿分析（商店开发预览）' }
} else {
    if (!$IdentityFile) { throw '正式商店包需要 Partner Center 的准确身份信息；本地检查使用 -Preview。' }
    $identity = Get-Content -LiteralPath $IdentityFile -Raw | ConvertFrom-Json
    if ($identity.Name -match 'Preview|REPLACE' -or $identity.Publisher -match 'Development|REPLACE') { throw '正式商店包不得使用示例身份。' }
}
if ($identity.Name -notmatch '^[A-Za-z0-9.-]{3,50}$' -or $identity.Publisher -notmatch '^CN=.{1,200}$' -or
    [string]::IsNullOrWhiteSpace($identity.PublisherDisplayName) -or [string]::IsNullOrWhiteSpace($identity.DisplayName)) { throw '商店身份字段无效。' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $storeRoot "artifacts/store/v$storeVersion-$(if ($Preview) {'preview'} else {'submission'})" }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$allowed = [IO.Path]::GetFullPath((Join-Path $storeRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (!$output.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $output)) { throw '必须使用 artifacts 内的新建目录。' }
$layout = Join-Path $output 'layout'; [IO.Directory]::CreateDirectory($layout) | Out-Null
# 仅复制完整发布产物；档案、缓存、日志和任何私钥不得进入 MSIX。
Get-ChildItem -LiteralPath $published -File -Recurse | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($published,$_.FullName)
    if ($relative -match '(^|[\\/])(catalog|logs|backups|TestResults)([\\/]|$)|\.(db|db-wal|db-shm|log|pfx|p12|pem|key|partial)$|(^|[\\/])settings\.json$') { throw "发布目录混入个人或测试资料：$relative" }
    $target = Join-Path $layout $relative; [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    [IO.File]::Copy($_.FullName,$target,$false)
}
& icacls.exe (Join-Path $layout 'GenshinGachaAnalyzer.exe') /setintegritylevel Medium | Out-Null
if ($LASTEXITCODE -ne 0) { throw '无法规范开发预览入口的完整性标记。' }
$assets = Join-Path $layout 'StoreAssets'; [IO.Directory]::CreateDirectory($assets) | Out-Null
Add-Type -AssemblyName System.Drawing
$source = [Drawing.Image]::FromFile((Join-Path $storeRoot 'GenshinGachaAnalyzer.WPF/Resources/AppIcon.png'))
try {
    foreach ($spec in @(@('StoreLogo',50,50),@('Square44x44Logo',44,44),@('Square150x150Logo',150,150),@('Wide310x150Logo',310,150))) {
        $bitmap = [Drawing.Bitmap]::new([int]$spec[1],[int]$spec[2]); $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([Drawing.Color]::Transparent); $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $side = [int]([Math]::Min($spec[1],$spec[2]) * .82)
            $graphics.DrawImage($source,[int](($spec[1]-$side)/2),[int](($spec[2]-$side)/2),$side,$side)
            $bitmap.Save((Join-Path $assets ($spec[0]+'.png')),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $source.Dispose() }
function Escape-StoreXml([string]$Text) { [Security.SecurityElement]::Escape($Text) }
$name = Escape-StoreXml $identity.Name; $publisher = Escape-StoreXml $identity.Publisher
$display = Escape-StoreXml $identity.DisplayName; $publisherDisplay = Escape-StoreXml $identity.PublisherDisplayName
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap rescap">
 <Identity Name="$name" Publisher="$publisher" Version="$storeVersion.0" ProcessorArchitecture="x64"/>
 <Properties><DisplayName>$display</DisplayName><PublisherDisplayName>$publisherDisplay</PublisherDisplayName><Logo>StoreAssets\StoreLogo.png</Logo></Properties>
 <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0"/></Dependencies>
 <Resources><Resource Language="zh-cn"/></Resources>
 <Applications><Application Id="App" Executable="GenshinGachaAnalyzer.exe" EntryPoint="Windows.FullTrustApplication">
  <uap:VisualElements DisplayName="$display" Description="本地原神祈愿记录管理与分析工具" BackgroundColor="#27231F" Square150x150Logo="StoreAssets\Square150x150Logo.png" Square44x44Logo="StoreAssets\Square44x44Logo.png"><uap:DefaultTile Wide310x150Logo="StoreAssets\Wide310x150Logo.png"/></uap:VisualElements>
 </Application></Applications>
 <Capabilities><Capability Name="internetClient"/><rescap:Capability Name="runFullTrust"/></Capabilities>
</Package>
"@
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'),$manifest,[Text.UTF8Encoding]::new($false))
if (!$MakeAppxPath) {
    $MakeAppxPath = Get-ChildItem 'C:/Program Files (x86)/Windows Kits/10/bin' -Filter makeappx.exe -Recurse -File |
        Where-Object FullName -match '[\\/]x64[\\/]makeappx.exe$' | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (!$MakeAppxPath) { throw '未找到 Windows SDK MakeAppx。布局和材料已保留。' }
$package = Join-Path $output "GenshinGachaAnalyzer-v$storeVersion-x64$(if($Preview){'-preview'}).msix"
& $MakeAppxPath pack /d $layout /p $package /o /v *> (Join-Path $output 'makeappx.log')
if ($LASTEXITCODE -ne 0) { throw "MSIX 打包失败，请检查 $output/makeappx.log" }
$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'),$hash+'  '+[IO.Path]::GetFileName($package)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
[pscustomobject]@{ Name=$identity.Name; Publisher=$identity.Publisher; Version="$storeVersion.0"; Preview=[bool]$Preview; Signed=$false; Layout=$layout; Package=$package } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'package-info.json') -Encoding utf8
Write-Output "MSIX 已生成（未签名，$(if($Preview){'仅供开发验证，不可提交商店'}else{'待 Partner Center 审核签名'})）：$package"
