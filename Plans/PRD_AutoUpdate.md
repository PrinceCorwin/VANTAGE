# PRD: MILESTONE Auto-Update System

## Overview

Implement automatic application updates for MILESTONE using Velopack with Azure Blob Storage as the update source. Users should receive updates seamlessly with minimal disruption to their workflow.

## Goals

- Enable push updates to all MILESTONE installations without manual intervention
- Minimize download size using delta updates
- Support offline scenarios gracefully (field engineers with intermittent connectivity)
- Provide user control over when updates are applied
- Maintain update history for rollback capability

## Non-Goals

- Mandatory forced updates (users always choose when to restart)
- Auto-update of the local SQLite database schema (handled separately by existing migration logic)
- Beta/alpha channel distribution (single release channel for now)

## Technical Context

- Framework: WPF .NET 8 (net8.0-windows)
- Existing infrastructure: Azure SQL Server for sync
- Target storage: Azure Blob Storage container
- Package: Velopack (NuGet: Velopack)
- CLI tool: vpk (installed via `dotnet tool install -g vpk`)

## Architecture

```
Azure Blob Storage (milestone-releases container)
├── releases.json          # Version manifest (auto-generated)
├── MILESTONE-1.0.0-full.nupkg
├── MILESTONE-1.0.1-full.nupkg
├── MILESTONE-1.0.1-delta.nupkg
├── MILESTONE-Setup.exe    # Fresh install bootstrapper
└── assets/
    └── RELEASES           # Legacy compatibility file
```

## Implementation Phases

### Phase 1: Project Configuration

#### 1.1 Add Velopack NuGet Package

Add to MILESTONE.csproj:
```xml
<PackageReference Include="Velopack" Version="0.*" />
```

#### 1.2 Update App.xaml.cs Entry Point

Velopack requires initialization before any WPF code runs. Modify App.xaml.cs:

- Add `VelopackApp.Build().Run()` as the first line in the application entry
- This must execute before `InitializeComponent()` or any other code
- Velopack uses this hook to handle update installation and restart scenarios

#### 1.3 Add Assembly Metadata

Add to MILESTONE.csproj for consistent versioning:
```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0</AssemblyVersion>
  <FileVersion>1.0.0</FileVersion>
</PropertyGroup>
```

### Phase 2: Update Manager Service

#### 2.1 Create UpdateService Class

Location: `/Utilities/UpdateService.cs`

Responsibilities:
- Check for updates on application startup (background, non-blocking)
- Download updates in background when available
- Expose update state for UI binding
- Handle apply and restart when user confirms

Properties to expose:
- `bool IsUpdateAvailable`
- `string? AvailableVersion`
- `bool IsDownloading`
- `double DownloadProgress` (0-100)
- `bool IsReadyToInstall`
- `string? ReleaseNotes`

Methods:
- `Task CheckForUpdatesAsync()` - Called on startup
- `Task DownloadUpdateAsync()` - Downloads if update available
- `void ApplyUpdateAndRestart()` - Closes app, applies update, restarts
- `void SkipUpdate()` - User declines, don't prompt again for this version

Events:
- `event EventHandler<UpdateInfo>? UpdateAvailable`
- `event EventHandler? UpdateReady`
- `event EventHandler<Exception>? UpdateError`

#### 2.2 Configuration

Store update source URL in application settings or constants:
```
Update URL: https://{storageaccount}.blob.core.windows.net/milestone-releases
```

This should be configurable for testing (local folder) vs production (Azure).

#### 2.3 Network Handling

Before checking for updates:
- Verify network connectivity using `NetworkInterface.GetIsNetworkAvailable()`
- If offline, skip check silently and log info-level message
- Do not show errors to user for network failures during update check

#### 2.4 Logging

Log all update operations using existing AppLogger:
- Info: "Checking for updates..."
- Info: "Update available: v{version}"
- Info: "Downloading update: {progress}%"
- Info: "Update ready to install"
- Info: "Applying update and restarting"
- Warning: "Update check skipped - offline"
- Error: Any exceptions during update process

### Phase 3: User Interface

#### 3.1 Update Notification

Location: MainWindow status area or toast notification

When update is downloaded and ready:
- Show non-modal notification: "Update ready (v{version})"
- Buttons: "Restart Now" | "Later" | "Release Notes"
- Notification should be dismissible and not block workflow
- If user clicks "Later", do not prompt again until next app launch

#### 3.2 Manual Update Check

Add menu item: Help → Check for Updates

Behavior:
- If update available: Show dialog with version and release notes, offer to download/install
- If already on latest: Show brief message "You're running the latest version"
- If offline: Show message "Unable to check for updates. Please verify your internet connection."

#### 3.3 Update Progress Indicator

When downloading:
- Show progress in status bar or small overlay
- Format: "Downloading update... {percent}%"
- Must not block UI or user workflow

#### 3.4 About Dialog Enhancement

Update existing About dialog to show:
- Current version: "Version 1.2.3"
- Update status: "Up to date" or "Update available (v1.2.4)"

### Phase 4: Azure Blob Storage Setup

#### 4.1 Create Storage Container

Container name: `milestone-releases`
Access level: Blob (anonymous read access for blobs only)

This allows MILESTONE to download updates without authentication while keeping container listing private.

#### 4.2 CORS Configuration (if needed)

If any web-based admin tools will access the container:
```json
{
  "AllowedOrigins": ["*"],
  "AllowedMethods": ["GET"],
  "AllowedHeaders": ["*"],
  "MaxAgeInSeconds": 3600
}
```

### Phase 5: Build and Release Process

#### 5.1 Publish Configuration

Create publish profile or script that:
1. Builds Release configuration
2. Publishes to a staging folder
3. Runs Velopack packaging

Publish settings:
- Configuration: Release
- Runtime: win-x64
- Self-contained: true (recommended for field deployment)
- Single file: false (Velopack needs separate assemblies for delta)
- ReadyToRun: true (faster startup)

#### 5.2 Velopack Pack Command

```bash
vpk pack \
  --packId "MILESTONE" \
  --packVersion "{version}" \
  --packDir "./publish" \
  --mainExe "MILESTONE.exe" \
  --icon "./Resources/milestone.ico"
```

Output directory will contain:
- `MILESTONE-{version}-full.nupkg`
- `MILESTONE-{version}-delta.nupkg` (if previous version exists)
- `MILESTONE-Setup.exe`
- `releases.json`

#### 5.3 Upload Command

```bash
vpk upload azure \
  --container "milestone-releases" \
  --account "{storage-account-name}" \
  --key "{storage-account-key}"
```

#### 5.4 Version Increment Checklist

Before each release:
1. Update Version in MILESTONE.csproj
2. Update release notes (if maintaining a changelog)
3. Build and test locally
4. Run vpk pack
5. Test update from previous version locally
6. Upload to Azure

### Phase 6: Testing Requirements

#### 6.1 Local Testing Setup

Velopack supports local folder as update source for testing:
```csharp
var source = new SimpleFileSource(@"C:\releases\milestone");
```

Use this during development to test update flow without Azure.

#### 6.2 Test Scenarios

| Scenario | Expected Behavior |
|----------|-------------------|
| Fresh install via Setup.exe | App installs and runs, no update prompt |
| App launch with update available | Background download, notification when ready |
| App launch when offline | Silent skip, no errors shown |
| App launch with no updates | No notification, normal startup |
| User clicks "Restart Now" | App closes, updates, restarts within 10 seconds |
| User clicks "Later" | Notification dismissed, no prompt until next launch |
| User manually checks (Help menu) | Shows current status accurately |
| Update fails mid-download | Graceful error, app continues working |
| Update fails during apply | Rollback to previous version |
| Skip multiple versions (1.0 → 1.5) | Downloads full package, installs correctly |

#### 6.3 Rollback Testing

Verify that if an update fails during application:
- User is not left with broken installation
- Previous version remains functional
- Error is logged for diagnostics

## Data Flow

### Startup Sequence

```
1. App.Main() starts
2. VelopackApp.Build().Run() executes (handles post-update restart)
3. Normal WPF initialization continues
4. MainWindow loads
5. UpdateService.CheckForUpdatesAsync() fires (background)
6. If update found → download in background
7. When download complete → raise UpdateReady event
8. UI shows notification
```

### Update Apply Sequence

```
1. User clicks "Restart Now"
2. App saves any pending work (existing save logic)
3. UpdateService.ApplyUpdateAndRestart() called
4. Velopack takes over:
   a. Closes current process
   b. Extracts new version
   c. Removes old version files
   d. Launches new version
5. New version starts fresh
```

## File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| MILESTONE.csproj | Modify | Add Velopack package, version properties |
| App.xaml.cs | Modify | Add Velopack initialization, update check on startup |
| Utilities/UpdateService.cs | New | Core update logic and state management |
| MainWindow.xaml | Modify | Add update notification area |
| MainWindow.xaml.cs | Modify | Wire up update UI events |
| Views/AboutDialog.xaml | Modify | Show version and update status |
| Resources/Strings.resx | Modify | Add update-related strings (if using resources) |

## Constants and Configuration

```csharp
public static class UpdateConstants
{
    public const string UpdateUrl = "https://{account}.blob.core.windows.net/milestone-releases";
    public const string PackageId = "MILESTONE";
    public const int CheckDelaySeconds = 5; // Delay after startup before checking
    public const int CheckIntervalHours = 4; // Re-check interval if app stays open
}
```

## Error Handling

All update operations should:
- Never crash the application
- Never block the UI thread
- Log errors with full exception details
- Fail silently from user perspective (updates are not critical path)
- Allow manual retry via Help menu

## Security Considerations

- Azure storage key must not be embedded in client application
- Client only needs read access (anonymous blob read is sufficient)
- Consider code signing the application for Windows SmartScreen trust
- Release uploads should only happen from authorized build environment

## Future Enhancements (Out of Scope)

- Multiple release channels (stable/beta)
- Staged rollouts (percentage-based)
- Forced updates for critical security fixes
- In-app release notes display
- Update scheduling (install overnight)

## Acceptance Criteria

1. User can install MILESTONE fresh using Setup.exe
2. Running application checks for updates within 10 seconds of launch
3. Updates download in background without blocking UI
4. User receives non-intrusive notification when update is ready
5. Clicking "Restart Now" applies update and restarts app within 15 seconds
6. Clicking "Later" dismisses notification without further prompts
7. Help → Check for Updates works when triggered manually
8. Offline users experience no errors or disruption
9. About dialog shows current version accurately
10. All update operations are logged appropriately
