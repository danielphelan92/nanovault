<#
.SYNOPSIS
    Runs every NanoVault automated test project.
.EXAMPLE
    pwsh build/test.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Running NanoVault tests ($Configuration)..." -ForegroundColor Cyan
dotnet test (Join-Path $repoRoot "NanoVault.sln") -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed."
}

Write-Host "All tests passed." -ForegroundColor Green
