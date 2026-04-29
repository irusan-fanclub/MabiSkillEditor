# MabiSkillEditor — release 腳本
#
# 用法：
#   pwsh scripts/release.ps1                  # 用 csproj 的 Version
#   pwsh scripts/release.ps1 -Version 0.2.0   # 覆寫版本號
#
# 流程：
#   1. 呼叫 build.ps1 → publish/v<Version>/
#   2. 把 README.md 複製進去
#   3. 壓縮成 publish/MabiSkillEditor_v<Version>.zip

[CmdletBinding()]
param(
    [string] $Version
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$csproj = Join-Path $root 'MabiSkillEditor.csproj'
if (-not $Version) {
    [xml]$xml = Get-Content $csproj
    $Version  = $xml.Project.PropertyGroup.Version
    if (-not $Version) { throw 'csproj 中找不到 <Version>' }
}

# 1. build
& (Join-Path $PSScriptRoot 'build.ps1') -Version $Version
if ($LASTEXITCODE -ne 0) { throw "build.ps1 失敗 (exit $LASTEXITCODE)" }

$outDir = Join-Path $root "publish\v$Version"
$zipOut = Join-Path $root "publish\MabiSkillEditor_v$Version.zip"

# 2. 帶上 README
$readme = Join-Path $root 'README.md'
if (Test-Path $readme) {
    Copy-Item $readme $outDir -Force
}

# 3. 壓縮
if (Test-Path $zipOut) { Remove-Item -Force $zipOut }
Write-Host "==> 壓縮 → $zipOut" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zipOut -CompressionLevel Optimal

$zipSize = (Get-Item $zipOut).Length / 1MB
Write-Host ("==> 完成：{0}  ({1:N1} MB)" -f $zipOut, $zipSize) -ForegroundColor Green

