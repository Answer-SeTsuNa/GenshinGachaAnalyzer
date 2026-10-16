param([string]$Version, [switch]$FullInstaller)
$ErrorActionPreference = 'Stop'
$verifyRoot = Split-Path -Parent $PSScriptRoot
if (!$Version) { [xml]$props = Get-Content (Join-Path $verifyRoot 'Directory.Build.props'); $Version = [string]$props.Project.PropertyGroup.Version }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid version.' }
$outputDirectory = Join-Path $verifyRoot "artifacts/download-proxies-v$Version"
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$handler = [Net.Http.HttpClientHandler]::new(); $handler.UseCookies = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.DefaultRequestHeaders.UserAgent.ParseAdd('GenshinGachaAnalyzer-DownloadVerification')
$client.Timeout = [TimeSpan]::FromSeconds(60)
$results = [Collections.Generic.List[object]]::new()
try {
    $release = $client.GetStringAsync("https://api.github.com/repos/Answer-SeTsuNa/GenshinGachaAnalyzer/releases/tags/v$Version").GetAwaiter().GetResult() | ConvertFrom-Json
    $proxies = @(Get-Content (Join-Path $verifyRoot 'GenshinGachaAnalyzer.Core/Resources/download-proxies.json') -Raw | ConvertFrom-Json)
    foreach ($proxy in $proxies) {
        foreach ($fileName in @("GenshinGachaAnalyzer-Setup-v$Version-win-x64.exe", "GenshinGachaAnalyzer-v$Version-win-x64.zip")) {
            $asset = @($release.assets | Where-Object {$_.name -eq $fileName})
            if ($asset.Count -ne 1) { throw "Missing release asset: $fileName" }
            $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $proxy.Prefix + $asset[0].browser_download_url)
            $request.Headers.Range = [Net.Http.Headers.RangeHeaderValue]::new(0, 65535)
            $response = $null; $stream = $null; $timeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(60))
            try {
                $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $timeout.Token).GetAwaiter().GetResult()
                $response.EnsureSuccessStatusCode() | Out-Null
                $stream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                $buffer = [byte[]]::new(65536); $received = 0
                while ($received -lt $buffer.Length) {
                    $count = $stream.ReadAsync($buffer, $received, $buffer.Length - $received, $timeout.Token).GetAwaiter().GetResult()
                    if ($count -eq 0) { break }; $received += $count
                }
                $signature = if ($fileName.EndsWith('.exe')) { '4D-5A' } else { '50-4B' }
                if ($received -lt 2 -or [BitConverter]::ToString($buffer, 0, 2) -ne $signature) { throw 'Proxy returned an unexpected document instead of a package.' }
                $range = $response.Content.Headers.ContentRange
                if ($range -and $range.HasLength -and $range.Length -ne $asset[0].size) { throw 'Remote package length differs from GitHub release.' }
                $results.Add([pscustomobject]@{Provider=$proxy.Name;File=$fileName;Status=[int]$response.StatusCode;SampleBytes=$received;GitHubBytes=$asset[0].size;Range=[string]$range})
            } finally { if ($stream) {$stream.Dispose()}; if ($response) {$response.Dispose()}; $request.Dispose(); $timeout.Dispose() }
        }
    }
    if ($FullInstaller) {
        $installer = @($release.assets | Where-Object {$_.name -eq "GenshinGachaAnalyzer-Setup-v$Version-win-x64.exe"})[0]
        $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $proxies[0].Prefix + $installer.browser_download_url)
        $timeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromMinutes(10))
        $response = $null; $input = $null; $output = $null
        $filePath = Join-Path $outputDirectory $installer.name
        try {
            $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $timeout.Token).GetAwaiter().GetResult()
            $response.EnsureSuccessStatusCode() | Out-Null
            $input = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult(); $output = [IO.File]::Create($filePath)
            $input.CopyToAsync($output, $timeout.Token).GetAwaiter().GetResult(); $output.Dispose(); $output = $null
            $digest = 'sha256:' + (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($digest -ne $installer.digest -or (Get-Item -LiteralPath $filePath).Length -ne $installer.size) { throw 'Downloaded installer differs from the GitHub asset digest.' }
            $results.Add([pscustomobject]@{Provider=$proxies[0].Name;File=$installer.name;FullDownload=$true;Digest=$digest;MatchesGitHub=$true})
        } finally {if ($output) {$output.Dispose()}; if ($input) {$input.Dispose()}; if ($response) {$response.Dispose()}; $request.Dispose(); $timeout.Dispose()}
    }
    $results | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $outputDirectory 'verification.json') -Encoding utf8
    $results | Format-Table -AutoSize
} finally { $client.Dispose() }
