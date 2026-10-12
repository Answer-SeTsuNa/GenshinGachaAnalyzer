$ErrorActionPreference = 'Stop'
# 固定下载版本与摘要；只解压构建工具，不安装到系统或注册文件关联。
$toolRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/tools'
$compiler = Join-Path $toolRoot 'inno-6.7.3/app/ISCC.exe'
function Set-CompilerIntegrity {
    # 与应用发布入口一样，修正工作区 Low 继承；否则无法覆盖上轮 Medium 安装 EXE。
    & icacls.exe $compiler /setintegritylevel Medium | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '安装器编译入口完整性标记修正失败。' }
}
if (Test-Path -LiteralPath $compiler) { Set-CompilerIntegrity; return $compiler }
[IO.Directory]::CreateDirectory($toolRoot) | Out-Null
function Get-VerifiedTool([string]$Url, [string]$Path, [string]$Hash) {
    if (!(Test-Path -LiteralPath $Path)) { Invoke-WebRequest -Uri $Url -OutFile $Path }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Hash) {
        throw "构建工具校验失败，请删除后重新下载：$Path"
    }
}
$setup = Join-Path $toolRoot 'innosetup-6.7.3.exe'
Get-VerifiedTool 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' $setup '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
$extractArchive = Join-Path $toolRoot 'innoextract670.zip'
Get-VerifiedTool 'https://github.com/UserUnknownFactor/innoextract_win/releases/download/670/innoextract670.zip' $extractArchive '79b69b9b1fcd98f42ccd4b245efdf6a03bcfb674ba6af482f5a46891c9ed4d14'
$extractDirectory = Join-Path $toolRoot 'innoextract670'
Expand-Archive -LiteralPath $extractArchive -DestinationPath $extractDirectory -Force
& (Join-Path $extractDirectory 'innoextract.exe') --extract --output-dir (Join-Path $toolRoot 'inno-6.7.3') $setup | Out-Host
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $compiler)) { throw '无法解压安装器编译工具。' }
Set-CompilerIntegrity
return $compiler
