param([string]$MetadataDirectory = 'artifacts/reference-metadata')
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$metadataPath = Join-Path $workspace $MetadataDirectory
$events = Get-Content -LiteralPath (Join-Path $metadataPath 'GachaEvent.json') -Raw | ConvertFrom-Json
$assetDirectory = Join-Path $workspace 'GenshinGachaAnalyzer.WPF/Resources/Banners'
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
$jobs = foreach ($event in $events) {
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($event.Banner))).Substring(0, 20).ToLowerInvariant()
    $extension = if (([Uri]$event.Banner).AbsolutePath.EndsWith('.png')) { '.png' } else { '.jpg' }
    [pscustomobject]@{ Url=$event.Banner; Alternate=$event.Banner2; File=$hash+$extension }
}
$results = $jobs | Sort-Object File -Unique | ForEach-Object -Parallel {
    $destination = Join-Path $using:assetDirectory $_.File
    if (Test-Path -LiteralPath $destination) { return [pscustomobject]@{File=$_.File;Success=$true} }
    foreach($url in @($_.Url, $_.Alternate) | Select-Object -Unique) {
        if (!$url) { continue }
        try {
            Invoke-WebRequest -Uri $url -OutFile ($destination+'.partial') -TimeoutSec 25
            Move-Item -LiteralPath ($destination+'.partial') -Destination $destination -Force
            return [pscustomobject]@{File=$_.File;Success=$true}
        } catch { }
    }
    if (Test-Path -LiteralPath ($destination+'.partial')) { Remove-Item -LiteralPath ($destination+'.partial') }
    [pscustomobject]@{File=$_.File;Success=$false;Url=$_.Url}
} -ThrottleLimit 8
$snapshot = for($i=0; $i -lt $events.Count; $i++) {
    $e=$events[$i]
    [ordered]@{Name=$e.Name;Version=$e.Version;Order=$e.Order;Type=[string]$e.Type;From=$e.From;To=$e.To;
        UpGold=@($e.UpOrangeList | ForEach-Object {[string]$_});UpPurple=@($e.UpPurpleList | ForEach-Object {[string]$_});
        BannerFile=$jobs[$i].File;BannerUrl=$e.Banner;BannerAlternate=$e.Banner2}
}
$snapshot | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $workspace 'GenshinGachaAnalyzer.Core/Resources/wish-events.json') -Encoding utf8
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $workspace 'artifacts/banner-download/results.json') -Encoding utf8
[pscustomobject]@{ Events=$events.Count; Downloaded=@($results | Where-Object Success).Count; Unavailable=@($results | Where-Object {!$_.Success}).Count }
