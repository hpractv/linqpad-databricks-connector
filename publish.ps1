#!/usr/bin/env pwsh
# publish.ps1 - Build and package the Azure Databricks LINQPad driver
# Usage: ./publish.ps1 [-Configuration Release]

param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$publishDir = Join-Path $dist "publish"

Write-Host "Building LinqPad.Databricks.Driver v$Version ($Configuration)..." -ForegroundColor Cyan

# Clean
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir | Out-Null

# Publish the driver
dotnet publish "$root/src/LinqPad.Databricks.Driver/LinqPad.Databricks.Driver.csproj" `
    -c $Configuration `
    -o $publishDir `
    /p:Version=$Version `
    --no-self-contained

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit 1
}

# Create .LPX6 (zip renamed)
$lpx6Path = Join-Path $dist "LinqPad.Databricks.Driver.$Version.LPX6"
if (Test-Path $lpx6Path) { Remove-Item $lpx6Path }
Compress-Archive -Path "$publishDir/*" -DestinationPath $lpx6Path
Write-Host "Created: $lpx6Path" -ForegroundColor Green

# Build NuGet package
dotnet pack "$root/src/LinqPad.Databricks.Driver/LinqPad.Databricks.Driver.csproj" `
    -c $Configuration `
    -o $dist `
    /p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet pack failed."
    exit 1
}

Write-Host ""
Write-Host "Artifacts in ${dist}:" -ForegroundColor Green
Get-ChildItem $dist | Select-Object Name, Length | Format-Table
