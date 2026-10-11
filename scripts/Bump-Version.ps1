param([switch]$Preview)
$ErrorActionPreference = 'Stop'
$versionFile = Join-Path (Split-Path -Parent $PSScriptRoot) 'Directory.Build.props'
$source = [IO.File]::ReadAllText($versionFile)
$match = [regex]::Match($source, '<Version>([0-9]+)\.([0-9]+)\.([0-9]+)</Version>')
if (!$match.Success) { throw 'Version must use major.minor.patch in Directory.Build.props.' }
$next = '{0}.{1}.{2}' -f $match.Groups[1].Value,$match.Groups[2].Value,([int]$match.Groups[3].Value + 1)
if (!$Preview) { [IO.File]::WriteAllText($versionFile, $source.Replace($match.Value, "<Version>$next</Version>"), [Text.UTF8Encoding]::new($false)) }
Write-Output $next
