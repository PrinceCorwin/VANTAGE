# Packaging, Credentials & Installer Plan

**Created:** February 1, 2026
**Branch:** `update-pack-cred`

Three workstreams, implemented in order. Each workstream is tested before starting the next.

---

## Workstream 1: Credential Migration

Move from compiled `Credentials.cs` to encrypted config file.

### 1A: Create AppConfig model
**New file:** `Models\AppConfig.cs`

Simple POCOs matching a JSON structure with sections: Azure, Email, Aws, Procore, Update. Mirrors every field currently in `Credentials.cs` (20 active values).

### 1B: Create appsettings.json (gitignored)
**New file:** `appsettings.json` (repo root, gitignored)

Plaintext JSON with all current credential values from `Credentials.cs`, organized by section. Developer-only source file that gets encrypted for distribution.

### 1C: Create CredentialService.cs
**New file:** `Utilities\CredentialService.cs`

Static class that replaces `Credentials`. On first access:
1. Looks for `appsettings.json` in `AppContext.BaseDirectory` — if found, deserialize directly (dev mode)
2. Otherwise looks for `appsettings.enc` — decrypt with AES-256-CBC + PBKDF2, then deserialize (production mode)
3. Neither found → clear error message

Exposes identical property names (`AzureServer`, `ActiveProcoreClientId`, etc.) so migration is a mechanical find-and-replace.

**Encryption:** AES-256-CBC, key derived via PBKDF2 (100k iterations) from a compiled passphrase + random salt. File format: `[16-byte salt][16-byte IV][ciphertext]`.

### 1D: Migrate all references (30 refs across 7 files)
Find-and-replace `Credentials.` → `CredentialService.` in:

| File | Refs |
|------|------|
| `Utilities\AzureDbManager.cs` | 4 |
| `Utilities\EmailService.cs` | 5 |
| `Utilities\UpdateService.cs` (line 116) | 1 |
| `Services\AI\TextractService.cs` | 3 |
| `Services\Procore\ProcoreAuthService.cs` | 13 |
| `Services\Procore\ProcoreApiService.cs` | 1 |
| `MainWindow.xaml.cs` | 4 |

### 1E: Add encrypt function to publish script
Add a PowerShell function in `Scripts\publish-update.ps1` that encrypts `appsettings.json` → `appsettings.enc` using the same AES-256/PBKDF2 algorithm. Runs as a new step before `dotnet publish`.

### 1F: Update .gitignore, delete Credentials.cs
- Add `**/appsettings.json` to `.gitignore`
- Remove `**/Credentials.cs` from `.gitignore`
- Delete `Credentials.cs`
- `appsettings.enc` gets checked in (encrypted data only, safe)

### 1G: Copy config files to output on build
Add to `VANTAGE.csproj`:
```xml
<None Update="appsettings.json" Condition="Exists('appsettings.json')">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
<None Update="appsettings.enc" Condition="Exists('appsettings.enc')">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
</None>
```

### Workstream 1 Verification
1. **After 1A-1D:** `dotnet build` succeeds. Run app from VS — connects to Azure SQL, email works, Textract works, update check works. All via CredentialService reading plaintext appsettings.json.
2. **After 1E:** Run encrypt function from PowerShell to generate appsettings.enc. Delete appsettings.json from output dir. Run app — loads credentials from encrypted file.
3. **After 1F:** Build succeeds with Credentials.cs deleted. App runs normally.

### Workstream 1 Files
**Modified:** AzureDbManager.cs, EmailService.cs, UpdateService.cs, TextractService.cs, ProcoreAuthService.cs, ProcoreApiService.cs, MainWindow.xaml.cs, publish-update.ps1, .gitignore, VANTAGE.csproj
**Created:** Models\AppConfig.cs, Utilities\CredentialService.cs, appsettings.json
**Deleted:** Credentials.cs

---

## Workstream 2: Publish Configuration & Azure Blob Storage

### 2A: Update VANTAGE.csproj for self-contained publish
Add Release publish properties: `RuntimeIdentifier=win-x64`, `SelfContained=true`. **Not** single-file — Syncfusion loads theme resources at runtime and single-file extraction can break resource resolution.

### 2B: Update VANTAGE.Updater.csproj
Self-contained **and** single-file (no Syncfusion, safe for single-file). The updater becomes a standalone exe with no dependencies.

### 2C: Update publish-update.ps1
- Change `--self-contained false` → `--self-contained true` on both publish commands
- Add `--self-contained true` and `-p:PublishSingleFile=true` for the updater
- Remove GitHub-specific instructions from the non-upload path
- Add release notes prompt

### 2D: Azure Blob Storage Setup (Walkthrough for Steve)
This is a guided walkthrough, not code changes:
1. Create Storage Account in Azure Portal (e.g., `vantageupdates`, Standard/LRS, same region as existing Azure SQL)
2. Create Blob Container `vantage-updates` with **Blob-level anonymous read** access
3. Install Azure CLI: `winget install Microsoft.AzureCLI`
4. Login: `az login`
5. Expected blob structure:
   ```
   vantage-updates/
     manifest.json
     releases/
       VANTAGE-26.1.1.zip
       VANTAGE-26.2.0.zip
     installer/
       VANTAGE-Setup.exe
   ```

### 2E: Update appsettings.json with new UpdateBaseUrl
Change from `https://raw.githubusercontent.com/PrinceCorwin/VANTAGE/main/updates` to `https://{account}.blob.core.windows.net/vantage-updates`

### 2F: Publishing Workflow (what Steve does each release)
1. Finish development and testing, merge to main
2. Open PowerShell in repo root
3. Run: `.\Scripts\publish-update.ps1 -Version "26.2.0" -Upload -StorageAccount "vantageupdates"`
   - Bumps version in VANTAGE.csproj
   - Encrypts appsettings.json → appsettings.enc
   - Publishes VANTAGE.Updater (self-contained single-file)
   - Publishes VANTAGE (self-contained)
   - Copies updater + appsettings.enc into publish output
   - Creates VANTAGE-26.2.0.zip
   - Computes SHA-256 hash
   - Uploads ZIP to Azure Blob `releases/VANTAGE-26.2.0.zip`
   - Generates and uploads manifest.json
4. Commit the version bump
5. Existing users get the update automatically on next app launch

### Workstream 2 Verification
1. `publish-update.ps1 -Version "X.X.X"` produces a working ZIP locally
2. ZIP uploaded to Azure Blob with `-Upload` flag
3. App reads manifest from Azure Blob URL and detects "no update needed"
4. Bump version, publish again, verify auto-update triggers on next app launch

### Workstream 2 Files
**Modified:** VANTAGE.csproj, VANTAGE.Updater\VANTAGE.Updater.csproj, Scripts\publish-update.ps1, appsettings.json (UpdateBaseUrl)

---

## Workstream 3: Installer App

### 3A: Create VANTAGE.Installer project
**New directory:** `VANTAGE.Installer\`

Files:
- `VANTAGE.Installer.csproj` — WPF, net8.0-windows, self-contained single-file (no Syncfusion)
- `App.xaml` / `App.xaml.cs`
- `MainWindow.xaml` / `MainWindow.xaml.cs`
- `InstallerService.cs` — download/extract/shortcut logic

### 3B: UI Layout (~500x400 branded window)
```
+------------------------------------------------+
|         [Summit Logo]                          |
|         Summit Industrial Constructors         |
|         Software Suite                         |
|                                                |
|   +--------------------------------------------+
|   | [Milestone Logo] VANTAGE: Milestone        |
|   | Construction Progress Tracking      [Install]
|   +--------------------------------------------+
|                                                |
|   +--------------------------------------------+
|   | REQit                                      |
|   | Coming Soon                       [disabled]
|   +--------------------------------------------+
|                                                |
|   [=============================] 45%          |
|   Downloading VANTAGE: Milestone...            |
+------------------------------------------------+
```

- Dark background, Summit branding
- Progress bar + status text (hidden until install starts)
- Checks if already installed → offers to reinstall or launch existing

### 3C: Install logic (InstallerService.cs)
1. Read manifest from Azure Blob `manifest.json` (same manifest the updater uses — single source of truth)
2. Download ZIP from `manifest.DownloadUrl` (with progress bar)
3. Verify SHA-256 hash
4. Extract to `%LOCALAPPDATA%\VANTAGE\App\`
5. Create Desktop shortcut (`VANTAGE Milestone.lnk`) using COM Shell interop
6. Prompt: "Installation complete! Would you like to launch VANTAGE: Milestone?"
7. Clean up temp files

### 3D: Add publish-installer.ps1
Small script that runs `dotnet publish` on the installer project (self-contained single-file) and copies the output to a known location. Optionally uploads to Azure Blob `installer/VANTAGE-Setup.exe`.

### 3E: Add to solution
Add `VANTAGE.Installer` to `VANTAGE.sln`. No ProjectReference to the main app — completely independent.

### Workstream 3 Verification
1. Publish installer as single-file exe
2. Run installer on a clean machine (or clean `%LOCALAPPDATA%\VANTAGE\App\`)
3. App installs, desktop shortcut works, app launches
4. Bump version, publish update, verify app auto-updates on next launch
5. Full cycle: clean install → use → auto-update

### Workstream 3 Files
**Created:** VANTAGE.Installer\VANTAGE.Installer.csproj, App.xaml, App.xaml.cs, MainWindow.xaml, MainWindow.xaml.cs, InstallerService.cs, Scripts\publish-installer.ps1
**Modified:** VANTAGE.sln

---

## Key Architecture Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Config encryption | AES-256-CBC + PBKDF2 with compiled passphrase | Simple, effective deterrent; no external key storage needed |
| Dev mode config | Plaintext `appsettings.json` fallback first | Seamless development without encryption tooling |
| Main app publish | Self-contained, NOT single-file | Avoids Syncfusion resource loading issues with single-file extraction |
| Updater publish | Self-contained + single-file | Simple console app, no Syncfusion, safe for single-file |
| Installer publish | Self-contained + single-file | No Syncfusion, clean single exe for IT distribution |
| Installer reads manifest | Same `manifest.json` as updater | Single source of truth, no duplicate config |
| Install location | `%LOCALAPPDATA%\VANTAGE\App\` | No admin rights needed, consistent with existing data path |
| Blob storage access | Anonymous blob read | App downloads without auth; uploads authenticated via `az` CLI |
| Taskbar pinning | Manual (prompt user) | Programmatic taskbar pinning restricted since Windows 10 |

## Notes

- **SmartScreen warning:** Unsigned exe triggers Windows SmartScreen. For internal distribution, users click "Run anyway." Consider code-signing certificate ($70-200/yr) for wider distribution.
- **Self-contained publish size:** Expect ~150-200MB ZIP. First download is large but subsequent updates replace only changed files.
- **Config key rotation:** If the passphrase changes, the new `appsettings.enc` and new app code ship together in the same update ZIP, so it works naturally.
- **Credentials.cs shared across branches:** Per CLAUDE.md, this file is shared. The migration in Workstream 1 should be merged into all active branches promptly after completion.
