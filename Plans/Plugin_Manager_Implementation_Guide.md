# VANTAGE: Milestone Plugin Manager Implementation Guide

## Purpose

This document captures the full implementation and debugging history for:

1. `Tools > Project Specific` menu item and dialog.
2. `...` (top-right settings popup) `Plugin Manager...` dialog.
3. GitHub feed-based plugin discovery/install/uninstall.
4. Startup automatic plugin update behavior.

This is intended as a handoff artifact so work can continue from another machine/session without losing context.

---

## High-Level Outcome

The app now supports:

1. A `Project Specific` dialog (currently seeded with one placeholder action).
2. A `Plugin Manager` dialog that shows:
   - Installed plugins (from local plugin store)
   - Available plugins (from GitHub feed `plugins-index.json`)
3. Install selected plugin from GitHub feed.
4. Uninstall selected installed plugin.
5. Automatic startup plugin updates:
   - Checks installed plugins vs feed versions
   - Installs newer versions automatically
   - Removes older versions of the same plugin ID

---

## UX Placement Decisions

## Menus

1. `Tools > Project Specific`
   - Kept in Tools menu.
   - Intended for project-specific user-run actions.

2. `...` settings popup > `Plugin Manager...`
   - Added in top-right toolbar settings popup.
   - Intended for plugin lifecycle management.

## Why split this way

1. `Project Specific` = run business functions.
2. `Plugin Manager` = install/update/uninstall administration.

---

## File-by-File Implementation

## Main Window / Menus

### `MainWindow.xaml`

Added:

1. Tools menu item:
   - Header: `Project Specific`
   - Click: `MenuProjectSpecific_Click`
2. Settings popup menu item:
   - Button: `Plugin Manager...`
   - Click: `MenuPluginManager_Click`

### `MainWindow.xaml.cs`

Added:

1. `MenuProjectSpecific_Click`:
   - Opens `ProjectSpecificFunctionsDialog`.
2. `MenuPluginManager_Click`:
   - Opens `PluginManagerDialog`.

---

## Project Specific Dialog

### `Dialogs/ProjectSpecificFunctionsDialog.xaml`

Created a themed dialog with:

1. Read-only grid (`SfDataGrid`).
2. Columns:
   - `Project`
   - `Description`
3. Buttons:
   - `Run`
   - `Close`

### `Dialogs/ProjectSpecificFunctionsDialog.xaml.cs`

Implemented:

1. Initial in-memory entry:
   - Project: `Fluor T&M 25.005`
   - Description: `Update Pipe Support Fab`
2. `Run` button behavior:
   - Shows `Coming soon.` placeholder message.

---

## Plugin Manifest and Feed Models

### `Models/PluginManifest.cs`

Local package manifest model:

1. `id`, `name`, `version`
2. `project`, `description`
3. `assemblyFile`, `entryType`
4. `minAppVersion`, `maxAppVersion`

### `Models/PluginFeedIndex.cs`

Feed index model for `plugins-index.json`:

1. Root:
   - `plugins` array
2. Each feed item:
   - `id`, `name`, `version`
   - `project`, `description`
   - `packageUrl`
   - `sha256`

---

## App Configuration Wiring

### `Models/AppConfig.cs`

Added:

1. `PluginsConfig` with `IndexUrl`.
2. `AppConfig.Plugins`.

### `Utilities/CredentialService.cs`

Added:

1. `PluginsIndexUrl` property (`Config.Plugins.IndexUrl`).

### `appsettings.json`

Added:

1. `Plugins.IndexUrl` set to:
   - `https://raw.githubusercontent.com/PrinceCorwin/VANTAGE-Plugins/main/plugins-index.json`

---

## Plugin Catalog / Discovery (Installed)

### `Services/Plugins/PluginCatalogService.cs`

Created service to read installed plugins from:

1. Local root:
   - `%LocalAppData%\VANTAGE\Plugins`
2. Scans for `plugin.json` files recursively.
3. Parses manifest into `InstalledPluginInfo`.
4. Returns list sorted by project/name.

Notes:

1. `InstalledPluginInfo` includes:
   - `Id`, `Name`, `Version`, `Project`, `Description`
   - `PluginDirectory` (for uninstall)
   - `ManifestPath`

---

## Plugin Feed / Discovery (Available)

### `Services/Plugins/PluginFeedService.cs`

Created service to:

1. Download `plugins-index.json` from `CredentialService.PluginsIndexUrl`.
2. Deserialize into `PluginFeedIndex`.
3. Return sorted `PluginFeedItem` list.

---

## Plugin Install/Uninstall Service

### `Services/Plugins/PluginInstallService.cs`

Implemented install/uninstall behavior.

## Install from feed (`InstallFromFeedAsync`)

Flow:

1. Validate feed item has `id`, `version`, `packageUrl`.
2. Download zip from `packageUrl` to temp.
3. Optional SHA-256 verify (`sha256` if provided).
4. Extract zip to temp.
5. Find `plugin.json` anywhere in extracted content.
6. Parse and validate manifest:
   - must have valid JSON
   - must include `id` and `version`
   - `manifest.id` must match `feed.id`
   - `manifest.version` must match `feed.version`
7. Resolve install target:
   - `%LocalAppData%\VANTAGE\Plugins\<id>\<version>`
8. Handle stale partial installs:
   - If target exists without a manifest, delete stale folder and continue.
   - If target exists with manifest, return already-installed message.
9. Copy package contents to target.
10. Log install with `AppLogger`.

## Uninstall (`UninstallAsync`)

Flow:

1. Delete selected plugin version directory.
2. If plugin ID parent folder is now empty, delete it too.
3. Log uninstall with `AppLogger`.

## Diagnostics

1. Install failure message includes the attempted `packageUrl`.
2. Download URL is logged for troubleshooting.

---

## Plugin Manager Dialog

### `Dialogs/PluginManagerDialog.xaml`

Created a themed dialog with:

1. Two tabs:
   - `Installed`
   - `Available`
2. Installed grid columns:
   - `Name`, `Project`, `Description`, `Version`
3. Available grid columns:
   - `Name`, `Project`, `Description`, `Version`, `Installed`, `Package URL`
4. Buttons:
   - `Install Selected` (feed-based)
   - `Uninstall`
   - `Refresh`
   - `Close`

### `Dialogs/PluginManagerDialog.xaml.cs`

Implemented:

1. `RefreshAllAsync()`:
   - Load installed list from local store
   - Load available list from feed
2. Install button:
   - Feed install path only (GitHub URL)
   - Confirmation dialog
   - Status text and error/result message
3. Uninstall button:
   - Confirmation dialog
   - Calls uninstall service
4. Selection-based button enable/disable.

Important:

1. Local/manual install path was intentionally removed.
2. Plugin installs are controlled via GitHub feed only.

---

## Startup Automatic Plugin Update

### `Services/Plugins/PluginAutoUpdateService.cs`

Created service to auto-update installed plugins at startup.

Flow:

1. Read installed plugins.
2. Read available plugins from feed.
3. Compare latest installed version vs latest feed version per plugin ID.
4. If feed is newer:
   - Install newer version via `InstallFromFeedAsync`.
   - Remove older installed versions of that plugin ID.
5. Return summary counts (`checked`, `updated`, `failed`).
6. Log update outcomes.

### `App.xaml.cs`

Added startup step in `InitializeApplicationAsync` after app binary update check:

1. Splash status: `Checking plugin updates...`
2. Runs `PluginAutoUpdateService.CheckAndUpdateInstalledPluginsAsync(...)`
3. If updates applied, splash shows count briefly.

---

## Repository / Feed Setup Used

Feed repository:

1. `https://github.com/PrinceCorwin/VANTAGE-Plugins`

Feed file:

1. `plugins-index.json` at repo root.

Package source:

1. GitHub Release asset URLs referenced by `packageUrl`.

---

## Required `plugins-index.json` Format

```json
{
  "plugins": [
    {
      "id": "test-hello",
      "name": "Test Hello",
      "version": "1.0.2",
      "project": "Fluor T&M 25.005",
      "description": "Test plugin for install pipeline",
      "packageUrl": "https://github.com/PrinceCorwin/VANTAGE-Plugins/releases/download/v1.0.2-test/test-hello.1.0.2.zip",
      "sha256": ""
    }
  ]
}
```

---

## Required Plugin Package Content

Each plugin zip must contain (at minimum):

1. `plugin.json`
2. Plugin assembly file referenced by `assemblyFile`

Example `plugin.json`:

```json
{
  "id": "test-hello",
  "name": "Test Hello",
  "version": "1.0.2",
  "project": "Fluor T&M 25.005",
  "description": "Test plugin for install pipeline",
  "assemblyFile": "TestHelloPlugin.dll",
  "entryType": "TestHelloPlugin.HelloPlugin",
  "minAppVersion": "1.0.0",
  "maxAppVersion": ""
}
```

Validation now enforces:

1. valid JSON
2. required `id` + `version`
3. feed `id/version` must match manifest `id/version`

---

## Troubleshooting Lessons Learned

These issues were encountered and addressed:

1. `plugin.json` malformed JSON
   - Symptom: install or installed listing behaved inconsistently.
   - Fix: added strict JSON manifest validation.

2. Zip layout variance (extra top-level folder)
   - Symptom: manifest not found.
   - Fix: installer scans recursively for `plugin.json`.

3. Stale partial install directory
   - Symptom: `already installed` message while not shown under Installed.
   - Fix: if target version folder exists without manifest, auto-clean stale folder.

4. GitHub index caching delay
   - Symptom: Available tab lagged behind pushed `plugins-index.json`.
   - Mitigation: refresh after push delay; diagnostics now include package URL.

5. File lock during local build
   - Symptom: copy errors when app running.
   - Mitigation for validation: build using isolated output dir.

---

## Current Verified Status

Validated by user:

1. Install works from GitHub feed.
2. Uninstall works.
3. Startup plugin auto-update works (`1.0.1` -> `1.0.2` confirmed).

---

## Current Scope vs Future Work

Implemented now:

1. Plugin lifecycle management (feed install/uninstall/refresh).
2. Startup auto-update for installed plugins.
3. Project Specific menu/dialog scaffold.

Not yet implemented:

1. Executing installed plugin code assemblies (`entryType`) at runtime.
2. `Project Specific` data source refactor from hardcoded list to installed plugin actions.
3. Plugin type classification (`project-action` vs passive extension) in UI.
4. Authenticated feed handling for private-only access (if needed).
5. Signature validation and rollback policy.

---

## Recommended Next Development Steps

1. Add manifest field `pluginType` and enforce allowed values.
2. Refactor Project Specific dialog to list installed plugins where `pluginType = project-action`.
3. Implement runtime plugin loader and controlled `Run` execution pipeline.
4. Build first real plugin:
   - `Update Pipe Support Fab` for `Fluor T&M 25.005`.
5. Add update policy controls:
   - startup auto-update toggle (if needed)
   - release channel pinning (optional).

