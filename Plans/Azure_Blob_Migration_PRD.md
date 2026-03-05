# PRD: Migrate Update Hosting to Azure Blob Storage

## Background

VANTAGE currently hosts update files (manifest.json and release ZIPs) on GitHub, which requires the repository to be public. Summit wants to make the source code private while maintaining the auto-update functionality.

## Goal

Host update files on Azure Blob Storage with public read access, allowing the GitHub repository to be made private.

## Current State

- **Repository**: `https://github.com/PrinceCorwin/VANTAGE` (public)
- **Manifest URL**: `https://raw.githubusercontent.com/PrinceCorwin/VANTAGE/main/updates/manifest.json`
- **Release ZIPs**: `https://github.com/PrinceCorwin/VANTAGE/releases/download/vX.Y.Z/VANTAGE-X.Y.Z.zip`
- **Update check**: `UpdateService.cs` fetches manifest from `CredentialService.UpdateBaseUrl`

## Target State

- **Repository**: Private (GitHub or transferred to Summit-owned hosting)
- **Manifest URL**: `https://<storageaccount>.blob.core.windows.net/vantage-updates/manifest.json`
- **Release ZIPs**: `https://<storageaccount>.blob.core.windows.net/vantage-updates/VANTAGE-X.Y.Z.zip`

## Prerequisites

1. Access to Summit Industrial's Azure subscription
2. Permissions to create Storage Account (Contributor role on a resource group)
3. Azure CLI installed locally (optional, for automated uploads)

## Implementation Steps

### Phase 1: Azure Setup

1. **Create Storage Account**
   - Name: `summitvantage` (or similar, must be globally unique)
   - Region: East US (or closest to users)
   - Performance: Standard
   - Redundancy: LRS (Locally-redundant storage)
   - Estimated cost: ~$0.01-0.10/month

2. **Create Container**
   - Name: `vantage-updates`
   - Access level: **Blob (anonymous read access for blobs only)**
   - This allows anyone to download files if they know the URL, but cannot list contents

3. **Upload Initial Files**
   - Upload current `manifest.json`
   - Upload current release ZIP(s)

### Phase 2: Code Changes

1. **Update `Credentials.cs`**
   ```csharp
   // Change UpdateBaseUrl from:
   // "https://raw.githubusercontent.com/PrinceCorwin/VANTAGE/main/updates"
   // To:
   // "https://<storageaccount>.blob.core.windows.net/vantage-updates"
   ```

2. **Update `manifest.json` structure**
   - Change `downloadUrl` to point to Azure Blob Storage
   ```json
   {
     "currentVersion": "X.Y.Z",
     "downloadUrl": "https://<storageaccount>.blob.core.windows.net/vantage-updates/VANTAGE-X.Y.Z.zip",
     ...
   }
   ```

3. **Update `Scripts/publish-update.ps1`**
   - Add Azure Blob upload step after ZIP creation
   - Options:
     - **Azure CLI**: `az storage blob upload` (requires Azure CLI installed)
     - **AzCopy**: Standalone tool for Azure uploads
     - **Manual**: Output instructions for manual upload via Azure Portal

### Phase 3: Testing

1. Publish a test version with Azure URLs
2. Verify manifest download works: `curl https://<storageaccount>.blob.core.windows.net/vantage-updates/manifest.json`
3. Verify ZIP download works
4. Test full update cycle on a test machine
5. Verify SHA-256 hash validation passes

### Phase 4: Migration

1. Deploy updated app version with new Azure URLs to all users
2. Confirm all users can receive updates from Azure
3. Make GitHub repository private
4. Verify no functionality is broken

## Publish Script Changes

Add to `Scripts/publish-update.ps1`:

```powershell
# Azure Blob Storage upload (requires Azure CLI and login)
param(
    [switch]$UploadToAzure
)

$StorageAccount = "summitvantage"
$Container = "vantage-updates"

if ($UploadToAzure) {
    Write-Host "Uploading to Azure Blob Storage..."

    # Upload ZIP
    az storage blob upload `
        --account-name $StorageAccount `
        --container-name $Container `
        --file $zipPath `
        --name "VANTAGE-$Version.zip" `
        --overwrite

    # Upload manifest
    az storage blob upload `
        --account-name $StorageAccount `
        --container-name $Container `
        --file "updates/manifest.json" `
        --name "manifest.json" `
        --overwrite

    Write-Host "Upload complete!"
}
```

## Alternative: Manual Upload Process

If Azure CLI is not installed, after running `publish-update.ps1`:

1. Go to Azure Portal > Storage Account > Containers > vantage-updates
2. Click "Upload"
3. Upload the ZIP file from `publish/VANTAGE-X.Y.Z.zip`
4. Upload `updates/manifest.json`

## Files to Modify

| File | Change |
|------|--------|
| `Credentials.cs` | Update `UpdateBaseUrl` to Azure Blob URL |
| `Scripts/publish-update.ps1` | Add Azure upload option |
| `updates/manifest.json` | Update `downloadUrl` to Azure Blob URL |
| `CLAUDE.md` | Update release process documentation |

## Rollback Plan

If issues occur after migration:
1. Make GitHub repo public again temporarily
2. Revert `Credentials.cs` to GitHub URLs
3. Publish hotfix with GitHub URLs

## Security Notes

- Blob container uses "anonymous read" - anyone with the URL can download
- This is acceptable because:
  - Update files are meant to be distributed anyway
  - Container listing is disabled (can't enumerate files)
  - Source code remains private in GitHub

## Future Considerations

- Could add SAS token authentication for downloads if needed
- Could implement CDN in front of blob storage for faster global downloads
- Consider Azure DevOps Artifacts as alternative if already using Azure DevOps
