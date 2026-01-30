# VANTAGE Update Publisher
# Builds, packages, and prepares an update for distribution.
# Usage: .\publish-update.ps1 -Version "26.1.2" [-Upload]
#
# The -Upload flag requires Azure CLI (az) and uploads to Azure Blob Storage.
# Without -Upload, it creates the ZIP and outputs the manifest content for manual use.

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [switch]$Upload,

    [string]$StorageAccount = "",
    [string]$Container = "vantage-updates"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$PublishDir = "$RepoRoot\bin\Release\net8.0-windows\win-x64\publish"
$UpdaterPublishDir = "$RepoRoot\VANTAGE.Updater\bin\Release\net8.0-windows\win-x64\publish"
$ZipName = "VANTAGE-$Version.zip"
$ZipPath = "$RepoRoot\$ZipName"

Write-Host "=== VANTAGE Update Publisher ===" -ForegroundColor Cyan
Write-Host "Version: $Version"
Write-Host ""

# Step 1: Update version in csproj
Write-Host "[1/7] Updating version in VANTAGE.csproj..." -ForegroundColor Yellow
$csproj = Get-Content "$RepoRoot\VANTAGE.csproj" -Raw
$csproj = $csproj -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
$csproj = $csproj -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$csproj = $csproj -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
Set-Content "$RepoRoot\VANTAGE.csproj" $csproj -NoNewline

# Step 2: Build the updater
Write-Host "[2/7] Publishing VANTAGE.Updater..." -ForegroundColor Yellow
dotnet publish "$RepoRoot\VANTAGE.Updater\VANTAGE.Updater.csproj" -c Release -r win-x64 --self-contained false
if ($LASTEXITCODE -ne 0) { throw "Updater publish failed" }

# Step 3: Build the main app
Write-Host "[3/7] Publishing VANTAGE..." -ForegroundColor Yellow
dotnet publish "$RepoRoot\VANTAGE.csproj" -c Release -r win-x64 --self-contained false
if ($LASTEXITCODE -ne 0) { throw "Main app publish failed" }

# Step 4: Copy updater into publish output
Write-Host "[4/7] Copying updater to publish output..." -ForegroundColor Yellow
Copy-Item "$UpdaterPublishDir\VANTAGE.Updater.exe" "$PublishDir\" -Force
if (Test-Path "$UpdaterPublishDir\VANTAGE.Updater.dll") {
    Copy-Item "$UpdaterPublishDir\VANTAGE.Updater.dll" "$PublishDir\" -Force
}
if (Test-Path "$UpdaterPublishDir\VANTAGE.Updater.runtimeconfig.json") {
    Copy-Item "$UpdaterPublishDir\VANTAGE.Updater.runtimeconfig.json" "$PublishDir\" -Force
}

# Step 5: Create ZIP
Write-Host "[5/7] Creating $ZipName..." -ForegroundColor Yellow
if (Test-Path $ZipPath) { Remove-Item $ZipPath }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath

# Step 6: Compute hash and size
Write-Host "[6/7] Computing SHA-256 hash..." -ForegroundColor Yellow
$hash = (Get-FileHash $ZipPath -Algorithm SHA256).Hash
$size = (Get-Item $ZipPath).Length
$sizeMB = [math]::Round($size / 1MB, 1)

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host "  ZIP: $ZipPath"
Write-Host "  Size: $sizeMB MB ($size bytes)"
Write-Host "  SHA-256: $hash"
Write-Host ""

# Step 7: Generate/upload manifest
if ($Upload -and $StorageAccount) {
    Write-Host "[7/7] Uploading to Azure Blob Storage..." -ForegroundColor Yellow

    $downloadUrl = "https://$StorageAccount.blob.core.windows.net/$Container/releases/$ZipName"

    # Upload ZIP
    az storage blob upload `
        --account-name $StorageAccount `
        --container-name $Container `
        --name "releases/$ZipName" `
        --file $ZipPath `
        --overwrite

    # Generate and upload manifest
    $manifest = @{
        currentVersion = $Version
        releaseDate = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
        downloadUrl = $downloadUrl
        zipSizeBytes = $size
        sha256 = $hash
        releaseNotes = ""
    } | ConvertTo-Json

    $manifestPath = "$RepoRoot\updates\manifest.json"
    Set-Content $manifestPath $manifest

    az storage blob upload `
        --account-name $StorageAccount `
        --container-name $Container `
        --name "manifest.json" `
        --file $manifestPath `
        --overwrite

    Write-Host "Upload complete!" -ForegroundColor Green
} else {
    Write-Host "[7/7] Manifest content (copy to updates/manifest.json):" -ForegroundColor Yellow
    Write-Host ""

    # For GitHub Releases, user needs to create the release and get the download URL
    $manifestContent = @"
{
  "currentVersion": "$Version",
  "releaseDate": "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')",
  "downloadUrl": "UPDATE_WITH_ACTUAL_DOWNLOAD_URL",
  "zipSizeBytes": $size,
  "sha256": "$hash",
  "releaseNotes": ""
}
"@
    Write-Host $manifestContent
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Create a GitHub Release tagged v$Version"
    Write-Host "  2. Attach $ZipName to the release"
    Write-Host "  3. Copy the direct download URL for the ZIP asset"
    Write-Host "  4. Update updates/manifest.json with the download URL"
    Write-Host "  5. Commit and push manifest.json"
}
