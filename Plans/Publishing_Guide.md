# VANTAGE Publishing Guide

**Created:** February 1, 2026

## Prerequisites

- PowerShell (Windows PowerShell 5.1 or newer)
- .NET 8 SDK installed
- `appsettings.json` in repo root (plaintext credentials — gitignored)
- GitHub account with push access to PrinceCorwin/VANTAGE
- PowerShell execution policy must allow local scripts. If you get "running scripts is disabled on this system", run once:
  ```powershell
  Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
  ```

## First-Time Setup (Already Done)

These steps were completed during the initial v26.1.1 publish. Listed here for reference if setting up on a new machine.

1. Ensure `appsettings.json` exists in the repo root with all credential values
2. Ensure `Scripts/publish-update.ps1` and `Scripts/publish-installer.ps1` exist
3. Ensure `updates/manifest.json` exists (can be a skeleton with empty values)

---

## Publishing an Update

Run these steps every time you want to release a new version to users.

### Step 1: Finish Development

- Complete all code changes, test in Visual Studio
- Commit and push everything to `main`
- Make sure `git status` shows a clean working tree

### Step 2: Choose a Version Number

Format: `Major.Minor.Patch` (e.g., `26.1.2`, `26.2.0`)

- **Patch** (26.1.1 → 26.1.2): Bug fixes, small tweaks
- **Minor** (26.1.x → 26.2.0): New features, significant changes
- **Major** (26.x.x → 27.0.0): Breaking changes, major overhaul

### Step 3: Run the Publish Script

Open PowerShell in the repo root:

```powershell
.\Scripts\publish-update.ps1 -Version "26.1.2"
```

The script will:
1. Update the version in `VANTAGE.csproj`
2. Encrypt `appsettings.json` → `appsettings.enc`
3. Publish `VANTAGE.Updater` (self-contained single-file)
4. Publish `VANTAGE` (self-contained)
5. Copy updater + encrypted config into publish output
6. Create `VANTAGE-{version}.zip` in the repo root
7. Compute SHA-256 hash
8. Print the manifest values you'll need

**Save the output** — you need the SHA-256 hash and file size for the manifest.

Example output:
```
=== Build Complete ===
  ZIP: C:\...\VANTAGE-26.1.2.zip
  Size: 142.1 MB (149027550 bytes)
  SHA-256: 07F37B9ABA0DA555E5EE79D51A295FDBBFE9E861C00A174D5730356F41E4A64E
```

### Step 4: Create a GitHub Release

1. Go to https://github.com/PrinceCorwin/VANTAGE/releases/new
2. **Tag:** `v26.1.2` (match your version with a `v` prefix)
3. **Target:** `main`
4. **Title:** `VANTAGE: Milestone v26.1.2`
5. **Description:** Brief release notes (what changed)
6. Drag and drop the ZIP file (`VANTAGE-26.1.2.zip`) into the assets area
7. Click **Publish release**
8. The download URL always follows this pattern — just type it manually:
   ```
   https://github.com/PrinceCorwin/VANTAGE/releases/download/v{VERSION}/VANTAGE-{VERSION}.zip
   ```
   **Do not right-click and copy the link from the browser** — some browsers (Edge) insert encoded characters like `%2C` that will break the URL.

### Step 5: Update the Manifest

Edit `updates/manifest.json` with the values from steps 3 and 4:

```json
{
  "currentVersion": "26.1.2",
  "releaseDate": "2026-02-01T15:40:29Z",
  "downloadUrl": "https://github.com/PrinceCorwin/VANTAGE/releases/download/v26.1.2/VANTAGE-26.1.2.zip",
  "zipSizeBytes": 149027550,
  "sha256": "07F37B9ABA0DA555E5EE79D51A295FDBBFE9E861C00A174D5730356F41E4A64E",
  "releaseNotes": "Bug fixes and improvements"
}
```

### Step 6: Commit and Push

The publish script modified `VANTAGE.csproj` (version bump), and you updated `manifest.json`. Commit and push:

```
git add -A
git commit -m "Publish v26.1.2"
git push
```

This push is important — the manifest must be live on GitHub for existing users to detect the update.

### Step 7: Verify

- Existing installed copies of VANTAGE will check the manifest on next launch
- If the manifest version is higher than the installed version, the app prompts to update
- The app downloads the ZIP, verifies the hash, hands off to the updater, and restarts

---

## Publishing the Installer (Less Frequent)

Only needed when you want to update the installer exe itself (e.g., new branding, new app added to the suite). Users who already have VANTAGE installed get updates via the auto-updater, not the installer.

```powershell
.\Scripts\publish-installer.ps1
```

Produces `VANTAGE-Setup.exe` (~69 MB) in the repo root. Distribute to new users or attach to the GitHub Release.

---

## Quick Reference

| Action | Command |
|--------|---------|
| Publish update | `.\Scripts\publish-update.ps1 -Version "X.X.X"` |
| Build installer | `.\Scripts\publish-installer.ps1` |
| Manifest location (repo) | `updates/manifest.json` |
| Manifest URL (live) | `https://raw.githubusercontent.com/PrinceCorwin/VANTAGE/main/updates/manifest.json` |
| Install directory (user) | `%LOCALAPPDATA%\VANTAGE\App\` |

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Publish fails with "appsettings.json not found" | Ensure `appsettings.json` exists in repo root (it's gitignored, so it won't be there on a fresh clone — copy from secure backup) |
| ZIP is huge (~150 MB) | Normal for self-contained publish — includes .NET runtime so users don't need it installed |
| Users don't see the update | Check that `manifest.json` was pushed to `main` and the `currentVersion` is higher than what they have installed |
| App crashes after update | Check that `appsettings.enc` is in the install directory — if missing, the app can't load credentials |
| SmartScreen blocks the exe | Right-click → Properties → Unblock, or click "More info" → "Run anyway" |
| Encoding error during publish | The publish script uses `System.IO.File` methods to preserve UTF-8 BOM — if you see encoding errors, check that the csproj still has its BOM |
