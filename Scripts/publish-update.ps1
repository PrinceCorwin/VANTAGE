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

# Encryption passphrase — must match CredentialService.cs
$EncryptionPassphrase = "VANTAGE-Summit-2026-Cr3d3nt1al-Encrypt10n-K3y-X9mPqR7vL2w"

# Encrypt appsettings.json to appsettings.enc using AES-256-CBC + PBKDF2
function Encrypt-ConfigFile {
    param(
        [string]$InputPath,
        [string]$OutputPath
    )

    $jsonBytes = [System.IO.File]::ReadAllBytes($InputPath)

    # Generate random salt and IV
    $salt = [byte[]]::new(16)
    $iv = [byte[]]::new(16)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($salt)
    $rng.GetBytes($iv)

    # Derive key from passphrase + salt using PBKDF2
    $keyDerivation = [System.Security.Cryptography.Rfc2898DeriveBytes]::new(
        $EncryptionPassphrase, $salt, 100000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $key = $keyDerivation.GetBytes(32) # AES-256

    # Encrypt with AES-256-CBC
    $aes = [System.Security.Cryptography.Aes]::Create()
    $aes.Key = $key
    $aes.IV = $iv
    $aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
    $aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7

    $encryptor = $aes.CreateEncryptor()
    $ciphertext = $encryptor.TransformFinalBlock($jsonBytes, 0, $jsonBytes.Length)

    # Write [salt][iv][ciphertext]
    $output = [System.IO.File]::Create($OutputPath)
    $output.Write($salt, 0, $salt.Length)
    $output.Write($iv, 0, $iv.Length)
    $output.Write($ciphertext, 0, $ciphertext.Length)
    $output.Close()

    # Cleanup
    $encryptor.Dispose()
    $aes.Dispose()
    $keyDerivation.Dispose()
    $rng.Dispose()
}

Write-Host "=== VANTAGE Update Publisher ===" -ForegroundColor Cyan
Write-Host "Version: $Version"
Write-Host ""

# Step 1: Update version in csproj
Write-Host "[1/8] Updating version in VANTAGE.csproj..." -ForegroundColor Yellow
$csproj = Get-Content "$RepoRoot\VANTAGE.csproj" -Raw
$csproj = $csproj -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
$csproj = $csproj -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$csproj = $csproj -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
Set-Content "$RepoRoot\VANTAGE.csproj" $csproj -NoNewline

# Step 2: Encrypt config file
Write-Host "[2/8] Encrypting appsettings.json..." -ForegroundColor Yellow
$plaintextConfig = "$RepoRoot\appsettings.json"
$encryptedConfig = "$RepoRoot\appsettings.enc"
if (-not (Test-Path $plaintextConfig)) {
    throw "appsettings.json not found at $plaintextConfig. Create it with credential values before publishing."
}
Encrypt-ConfigFile -InputPath $plaintextConfig -OutputPath $encryptedConfig
Write-Host "  Encrypted to appsettings.enc" -ForegroundColor Gray

# Step 3: Build the updater (self-contained single-file)
Write-Host "[3/8] Publishing VANTAGE.Updater..." -ForegroundColor Yellow
dotnet publish "$RepoRoot\VANTAGE.Updater\VANTAGE.Updater.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { throw "Updater publish failed" }

# Step 4: Build the main app (self-contained, not single-file due to Syncfusion)
Write-Host "[4/8] Publishing VANTAGE..." -ForegroundColor Yellow
dotnet publish "$RepoRoot\VANTAGE.csproj" -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) { throw "Main app publish failed" }

# Step 5: Copy updater into publish output
Write-Host "[5/8] Copying updater to publish output..." -ForegroundColor Yellow
Copy-Item "$UpdaterPublishDir\VANTAGE.Updater.exe" "$PublishDir\" -Force
if (Test-Path "$UpdaterPublishDir\VANTAGE.Updater.dll") {
    Copy-Item "$UpdaterPublishDir\VANTAGE.Updater.dll" "$PublishDir\" -Force
}
if (Test-Path "$UpdaterPublishDir\VANTAGE.Updater.runtimeconfig.json") {
    Copy-Item "$UpdaterPublishDir\VANTAGE.Updater.runtimeconfig.json" "$PublishDir\" -Force
}

# Ensure appsettings.enc is in publish output (should be via csproj, but verify)
if (Test-Path $encryptedConfig) {
    Copy-Item $encryptedConfig "$PublishDir\" -Force
}

# Remove plaintext config from publish output if it leaked in
$leakedPlaintext = "$PublishDir\appsettings.json"
if (Test-Path $leakedPlaintext) {
    Remove-Item $leakedPlaintext -Force
    Write-Host "  Removed plaintext appsettings.json from publish output" -ForegroundColor Gray
}

# Step 6: Create ZIP
Write-Host "[6/8] Creating $ZipName..." -ForegroundColor Yellow
if (Test-Path $ZipPath) { Remove-Item $ZipPath }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath

# Step 7: Compute hash and size
Write-Host "[7/8] Computing SHA-256 hash..." -ForegroundColor Yellow
$hash = (Get-FileHash $ZipPath -Algorithm SHA256).Hash
$size = (Get-Item $ZipPath).Length
$sizeMB = [math]::Round($size / 1MB, 1)

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Green
Write-Host "  ZIP: $ZipPath"
Write-Host "  Size: $sizeMB MB ($size bytes)"
Write-Host "  SHA-256: $hash"
Write-Host ""

# Step 8: Generate/upload manifest
if ($Upload -and $StorageAccount) {
    Write-Host "[8/8] Uploading to Azure Blob Storage..." -ForegroundColor Yellow

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
    Write-Host "[8/8] Manifest content (copy to updates/manifest.json):" -ForegroundColor Yellow
    Write-Host ""

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
    Write-Host "  1. Upload $ZipName to your update hosting location"
    Write-Host "  2. Update updates/manifest.json with the download URL"
    Write-Host "  3. Upload manifest.json to the same location"
}
