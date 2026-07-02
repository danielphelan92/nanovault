<#
.SYNOPSIS
    Builds the NanoVault solution (Release by default).
.EXAMPLE
    pwsh build/build.ps1
    pwsh build/build.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Building NanoVault ($Configuration)..." -ForegroundColor Cyan
dotnet build (Join-Path $repoRoot "NanoVault.sln") -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

Write-Host "Build succeeded." -ForegroundColor Green
