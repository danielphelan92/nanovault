<#
.SYNOPSIS
    Publishes the self-contained win-x64 release and builds the Windows
    installer (NanoVault-Setup-<version>.exe).

.DESCRIPTION
    Steps:
      1. dotnet publish  →  artifacts/publish/win-x64 (self-contained, no PDBs)
      2. Installer compile, using the first available toolchain:
           - Inno Setup 6 (ISCC.exe) on Windows — preferred
           - NSIS (makensis) on any OS — produces the same per-user installer

    Prerequisites: .NET 8 SDK, plus Inno Setup 6 (Windows) or NSIS 3.x.

.EXAMPLE
    pwsh build/package.ps1
    pwsh build/package.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "artifacts/publish/win-x64"
$artifactsDir = Join-Path $repoRoot "artifacts"
$installerDir = Join-Path $repoRoot "src/NanoVault.Installer"
$setupName = "NanoVault-Setup-$Version.exe"

# ---------------------------------------------------------------- publish
if (-not $SkipPublish) {
    Write-Host "Publishing self-contained win-x64 release..." -ForegroundColor Cyan
    dotnet publish (Join-Path $repoRoot "src/NanoVault.App") `
        -c Release -r win-x64 --self-contained true `
        -p:DebugType=None -p:DebugSymbols=false `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed."
    }
}

if (-not (Test-Path (Join-Path $publishDir "NanoVault.exe"))) {
    throw "Publish output not found at $publishDir. Run without -SkipPublish first."
}

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

# ------------------------------------------------------------- installer
function Find-Iscc {
    $candidates = @(
        (Get-Command "iscc.exe" -ErrorAction SilentlyContinue)?.Source,
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    return $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}

function Find-Makensis {
    return (Get-Command "makensis" -ErrorAction SilentlyContinue)?.Source
}

$iscc = if ($IsWindows -or $env:OS -eq "Windows_NT") { Find-Iscc } else { $null }
$makensis = Find-Makensis

if ($iscc) {
    Write-Host "Building installer with Inno Setup: $iscc" -ForegroundColor Cyan
    & $iscc "/DAppVersion=$Version" "/DPublishDir=$publishDir" "/DOutputDir=$artifactsDir" `
        (Join-Path $installerDir "NanoVault.iss")
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compile failed."
    }
}
elseif ($makensis) {
    Write-Host "Building installer with NSIS: $makensis" -ForegroundColor Cyan
    $outFile = Join-Path $artifactsDir $setupName
    & $makensis "-DAPP_VERSION=$Version" "-DPUBLISH_DIR=$publishDir" "-DOUT_FILE=$outFile" `
        (Join-Path $installerDir "NanoVault.nsi")
    if ($LASTEXITCODE -ne 0) {
        throw "NSIS compile failed."
    }
}
else {
    throw "No installer toolchain found. Install Inno Setup 6 (Windows) or NSIS 3.x, then re-run."
}

$setupPath = Join-Path $artifactsDir $setupName
if (Test-Path $setupPath) {
    $size = [math]::Round((Get-Item $setupPath).Length / 1MB, 1)
    Write-Host "Installer ready: $setupPath ($size MB)" -ForegroundColor Green
}
else {
    throw "Installer output missing: $setupPath"
}
