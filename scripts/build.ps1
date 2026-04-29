# MabiSkillEditor — build (publish) script
#
# 用法：
#   pwsh scripts/build.ps1                    # 用 csproj 的 Version
#   pwsh scripts/build.ps1 -Version 0.2.0     # 覆寫版本號
#   pwsh scripts/build.ps1 -Configuration Debug
#
# 輸出：publish/v<Version>/

[CmdletBinding()]
param(
    [string] $Version,
    [string] $Configuration = 'Release',
    [string] $Runtime       = 'win-x64'
)

$ErrorActionPreference = 'Stop'

# 切到專案根目錄（腳本所在目錄的上層）
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$csproj = Join-Path $root 'MabiSkillEditor.csproj'

if (-not $Version) {
    [xml]$xml = Get-Content $csproj
    $Version  = $xml.Project.PropertyGroup.Version
    if (-not $Version) { throw 'csproj 中找不到 <Version>' }
}

$outDir = Join-Path $root "publish\v$Version"
Write-Host "==> 發佈 MabiSkillEditor v$Version → $outDir" -ForegroundColor Cyan

if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }

dotnet publish $csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:Version=$Version `
    -o $outDir `
    -nologo

if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失敗 (exit $LASTEXITCODE)" }

# 移除 .pdb（release 不需要）
Get-ChildItem $outDir -Filter *.pdb | Remove-Item -Force

Write-Host "==> 完成" -ForegroundColor Green
Get-ChildItem $outDir | Format-Table Name, Length -AutoSize

