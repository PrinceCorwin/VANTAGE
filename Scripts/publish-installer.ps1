# VANTAGE Installer Publisher
# Builds the installer as a self-contained single-file exe.
# Usage: .\publish-installer.ps1

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$InstallerProject = "$RepoRoot\VANTAGE.Installer\VANTAGE.Installer.csproj"
$PublishDir = "$RepoRoot\VANTAGE.Installer\bin\Release\net8.0-windows\win-x64\publish"

Write-Host "=== VANTAGE Installer Publisher ===" -ForegroundColor Cyan
Write-Host ""

# Build
Write-Host "[1/2] Publishing VANTAGE.Installer..." -ForegroundColor Yellow
dotnet publish $InstallerProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { throw "Installer publish failed" }

# Copy to repo root for easy access
Write-Host "[2/2] Copying to repo root..." -ForegroundColor Yellow
$outputExe = "$PublishDir\VANTAGE.Installer.exe"
$destExe = "$RepoRoot\VANTAGE-Setup.exe"
Copy-Item $outputExe $destExe -Force

$sizeMB = [math]::Round((Get-Item $destExe).Length / 1MB, 1)

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host "  Output: $destExe"
Write-Host "  Size: $sizeMB MB"
Write-Host ""
Write-Host "Distribute VANTAGE-Setup.exe to users." -ForegroundColor Cyan
