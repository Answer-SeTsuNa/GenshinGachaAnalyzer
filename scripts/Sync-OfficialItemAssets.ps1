#requires -Version 7.0
<#
维护者资源更新入口：米游社原神养成计算器 -> 官方 icon URL -> 本地离线图片。
不需要 Cookie、UID、authkey，也不读取用户档案。未覆盖的卡池 UP 排期不在此脚本中推导。
#>
param(
    [string[]]$ItemId,
    [switch]$Force,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$catalogPath = Join-Path $workspace 'GenshinGachaAnalyzer.Core/Resources/items.json'
$assetDirectory = Join-Path $workspace 'GenshinGachaAnalyzer.WPF/Resources/Items'
$manifestPath = Join-Path $assetDirectory 'sources.json'
$headers = @{
    'User-Agent' = 'Mozilla/5.0'
    'Referer' = 'https://webstatic.mihoyo.com/'
    'Origin' = 'https://webstatic.mihoyo.com'
}
$endpoints = @{
    avatar = 'https://api-takumi.mihoyo.com/event/e20200928calculate/v1/avatar/list'
    weapon = 'https://api-takumi.mihoyo.com/event/e20200928calculate/v1/weapon/list'
}

function Get-OfficialList([string]$Kind) {
    $all = [Collections.Generic.List[object]]::new()
    $seen = [Collections.Generic.HashSet[string]]::new()
    $received = 0
    for ($page = 1; $page -le 100; $page++) {
        $payload = @{ page = $page; size = 200 }
        if ($Kind -eq 'avatar') { $payload.is_all = $true }
        else { $payload.weapon_levels = @(1, 2, 3, 4, 5) }
        $response = $null
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            try {
                $response = Invoke-RestMethod -Uri $endpoints[$Kind] -Method Post -Headers $headers `
                    -ContentType 'application/json' -Body ($payload | ConvertTo-Json -Compress) -TimeoutSec 25 -MaximumRedirection 0
                break
            } catch {
                if ($attempt -eq 3) { throw "官方 $Kind 列表请求失败，未改动资源。请检查网络后重试。" }
                Start-Sleep -Milliseconds (500 * $attempt)
            }
        }
        if ($response.retcode -ne 0 -or !$response.data -or $null -eq $response.data.list) {
            throw "官方 $Kind 列表返回错误或格式已变化，未改动资源。"
        }
        $entries = @($response.data.list)
        if ($entries.Count -eq 0) { break }
        $previousCount = $all.Count
        foreach ($entry in $entries) {
            # 旅行者等多元素角色会在同一列表重复出现，物品图标按 ID 收录一次。
            if (!$seen.Add([string]$entry.id)) { continue }
            $all.Add($entry)
        }
        if ($all.Count -eq $previousCount) { throw "官方 $Kind 列表分页未前进，已停止，未改动资源。" }
        $received += $entries.Count
        if ($null -ne $response.data.total -and $received -ge [int]$response.data.total) { break }
        if ($entries.Count -lt 200) { break }
        if ($page -eq 100) { throw '官方列表分页超出上限，未改动资源。' }
    }
    if ($all.Count -eq 0) { throw "官方 $Kind 列表为空，未改动资源。" }
    return $all.ToArray()
}

function Assert-OfficialIcon([string]$Url) {
    $uri = $null
    if (![Uri]::TryCreate($Url, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'https' -or
        $uri.Port -ne 443 -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or
        !(($uri.Host -eq 'act-webstatic.mihoyo.com' -and $uri.AbsolutePath -match '^/hk4e/e20200928calculate/item_icon/.+\.png$') -or
          ($uri.Host -eq 'uploadstatic.mihoyo.com' -and $uri.AbsolutePath -match '^/hk4e/e20200928calculate/common/.+\.png$') -or
          ($uri.Host -eq 'fastcdn.mihoyo.com' -and $uri.AbsolutePath -match '^/static-resource-v2/.+\.png$') -or
          ($uri.Host -eq 'upload-bbs.mihoyo.com' -and $uri.AbsolutePath -match '^/game_record/genshin/.+\.png$'))) {
        throw '官方接口中的图片地址不在已核对的官方资源范围内，未改动资源。请先核对接口变化。'
    }
}

function Assert-Png([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 33 -or $bytes.Length -gt 8MB -or
        [Convert]::ToHexString($bytes[0..7]) -ne '89504E470D0A1A0A' -or
        [Text.Encoding]::ASCII.GetString($bytes, 12, 4) -ne 'IHDR') {
        throw '下载结果不是有效的 PNG 图标，未替换本地资源。'
    }
    $width = [uint32]($bytes[16] * 16777216 + $bytes[17] * 65536 + $bytes[18] * 256 + $bytes[19])
    $height = [uint32]($bytes[20] * 16777216 + $bytes[21] * 65536 + $bytes[22] * 256 + $bytes[23])
    if ($width -lt 32 -or $height -lt 32 -or $width -gt 4096 -or $height -gt 4096) {
        throw '下载图标尺寸异常，未替换本地资源。'
    }
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    # 旧 game_record 猜测地址实测会返回同一张占位图，HTTP 200 不代表取得了角色图片。
    if ($hash -eq 'ae5d4d9be2e3ee89c0fc94a326aaefd8412c55d916bef6f7d28c88ebf771af7b') {
        throw '官方地址返回了已知占位图，未替换本地资源。'
    }
    return $hash
}

function Write-AtomicJson([string]$Path, $Value) {
    $temporary = $Path + '.partial'
    try {
        [IO.File]::WriteAllText($temporary, (ConvertTo-Json -InputObject $Value -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporary, $Path, $true)
    } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
}

$official = @(foreach ($kind in @('avatar', 'weapon')) {
    foreach ($entry in Get-OfficialList $kind) {
        if ($entry.name -eq '旅行者') { continue }
        $id = [string]$entry.id
        $rank = [string]$(if ($kind -eq 'avatar') { $entry.avatar_level } else { $entry.weapon_level })
        if ($id -notmatch '^\d+$' -or [string]::IsNullOrWhiteSpace($entry.name) -or $rank -notin @('1', '2', '3', '4', '5')) {
            throw '官方列表物品字段异常，未改动资源。'
        }
        Assert-OfficialIcon $entry.icon
        [pscustomobject]@{ Id = $id; Name = $entry.name; Kind = $(if ($kind -eq 'avatar') { '角色' } else { '武器' });
            Rank = $rank; Url = [string]$entry.icon; Endpoint = $endpoints[$kind] }
    }
})
if (@($official | Group-Object Id | Where-Object Count -gt 1).Count -gt 0) { throw '官方列表包含重复物品 ID，未改动资源。' }
if ($ItemId) {
    foreach ($id in $ItemId) { if ($id -notin $official.Id) { throw "官方列表没有物品 $id，未改动资源。" } }
    $official = @($official | Where-Object Id -in $ItemId)
}
$jobs = @($official | Where-Object {
    $path = Join-Path $assetDirectory ($_.Id + '.png')
    $Force -or !(Test-Path -LiteralPath $path)
})
if ($DryRun) {
    [pscustomobject]@{ OfficialItems = $official.Count; ImagesToDownload = $jobs.Count; DryRun = $true }
    return
}

$stage = Join-Path $workspace ('artifacts/official-item-sync/' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($stage) | Out-Null
$downloaded = @{}
for ($index = 0; $index -lt $jobs.Count; $index++) {
    $item = $jobs[$index]
    Write-Progress -Activity '同步官方角色 / 武器图标' -Status "$($item.Name) ($($index + 1)/$($jobs.Count))" `
        -PercentComplete (100 * $index / $jobs.Count)
    $path = Join-Path $stage ($item.Id + '.png')
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            Invoke-WebRequest -Uri $item.Url -Headers $headers -OutFile $path -TimeoutSec 25 -MaximumRedirection 0
            $downloaded[$item.Id] = Assert-Png $path
            break
        } catch {
            if ($attempt -eq 3) { throw "图标下载或校验失败：$($item.Name)。本批次未替换原资源；暂存目录：$stage" }
            Start-Sleep -Milliseconds (500 * $attempt)
        }
    }
}
Write-Progress -Activity '同步官方角色 / 武器图标' -Completed

# 全部下载通过校验后才发布文件。新图采用原子替换，写失败也不会留下半截 PNG。
$catalog = @{}
$originalItems = @(Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json)
foreach ($entry in $originalItems) { $catalog[[string]$entry.Id] = $entry }
$manifest = @{}
if (Test-Path -LiteralPath $manifestPath) {
    foreach ($entry in (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).Items) { $manifest[[string]$entry.Id] = $entry }
}
$timestamp = [DateTimeOffset]::UtcNow.ToString('O')
foreach ($item in $official) {
    $existing = $catalog[$item.Id]
    $catalog[$item.Id] = [ordered]@{ Id = $item.Id; Name = $item.Name; Kind = $item.Kind; Rank = $item.Rank;
        Icon = $(if ($existing -and $existing.Icon) { $existing.Icon } else { $item.Url }) }
    if (!$downloaded.ContainsKey($item.Id)) { continue }
    $destination = Join-Path $assetDirectory ($item.Id + '.png')
    [IO.File]::Copy((Join-Path $stage ($item.Id + '.png')), $destination + '.partial', $true)
    [IO.File]::Move($destination + '.partial', $destination, $true)
    $manifest[$item.Id] = [ordered]@{ Id = $item.Id; Name = $item.Name; Kind = $item.Kind; File = $item.Id + '.png';
        Url = $item.Url; MetadataEndpoint = $item.Endpoint; RetrievedAtUtc = $timestamp; Sha256 = $downloaded[$item.Id] }
}
$updatedCatalog = @(
    foreach ($entry in $originalItems) { $catalog[[string]$entry.Id] }
    foreach ($entry in $official | Where-Object Id -notin $originalItems.Id | Sort-Object { [long]$_.Id }) { $catalog[$entry.Id] }
)
Write-AtomicJson $catalogPath $updatedCatalog
Write-AtomicJson $manifestPath ([ordered]@{ Provider = '米游社 · 原神养成计算器';
    Items = @($manifest.Values | Sort-Object { [long]$_.Id }) })
[pscustomobject]@{ OfficialItems = $official.Count; Downloaded = $downloaded.Count; CatalogItems = $catalog.Count; Manifest = $manifestPath }
