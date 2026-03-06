# VANTAGE: Milestone Plugin System Implementation Guide

## Purpose

This document captures the full implementation of the VANTAGE plugin system:

1. Plugin discovery, install, uninstall via GitHub feed
2. Startup automatic plugin updates
3. Plugin execution framework (runtime assembly loading)
4. Dynamic UI injection (menu items)

---

## Current Status

**Fully Implemented:**
- Plugin Manager dialog (install/uninstall from GitHub feed)
- Feed-based plugin discovery (`plugins-index.json`)
- Startup auto-update for installed plugins
- Plugin execution framework (`IVantagePlugin`, `IPluginHost`, `PluginLoaderService`)
- Dynamic menu injection via `host.AddToolsMenuItem()`
- Plugin type classification (`pluginType` field in manifests)

**Next Step:**
- Release new VANTAGE version with plugin system included

---

## Architecture Overview

### Plugin Flow

1. **Discovery:** `PluginFeedService` downloads `plugins-index.json` from GitHub
2. **Install:** `PluginInstallService` downloads ZIP, verifies SHA-256, extracts to local store
3. **Catalog:** `PluginCatalogService` scans `%LocalAppData%\VANTAGE\Plugins` for installed plugins
4. **Load:** `PluginLoaderService` loads assemblies, instantiates `IVantagePlugin` implementations
5. **Initialize:** Each plugin's `Initialize(IPluginHost)` is called, allowing UI registration
6. **Shutdown:** On app close, `Shutdown()` is called on each plugin

### Key Interfaces

```csharp
// What plugins must implement
public interface IVantagePlugin
{
    string Id { get; }
    string Name { get; }
    void Initialize(IPluginHost host);
    void Shutdown();
}

// What the app provides to plugins
public interface IPluginHost
{
    void AddToolsMenuItem(string header, Action onClick, bool addSeparatorBefore = false);
    Window MainWindow { get; }
    string CurrentUsername { get; }
    void ShowInfo(string message, string title = "Information");
    void ShowError(string message, string title = "Error");
    bool ShowConfirmation(string message, string title = "Confirm");
    void LogInfo(string message, string source);
    void LogError(Exception ex, string source);
    Task RefreshProgressViewAsync();
}
```

---

## File Reference

### Plugin Services (`Services/Plugins/`)

| File | Purpose |
|------|---------|
| `IVantagePlugin.cs` | Interface plugins must implement |
| `IPluginHost.cs` | Interface for app capabilities provided to plugins |
| `PluginLoaderService.cs` | Loads assemblies, instantiates plugins, manages lifecycle |
| `PluginCatalogService.cs` | Scans local store for installed plugins |
| `PluginFeedService.cs` | Downloads and parses plugin feed from GitHub |
| `PluginInstallService.cs` | Handles download, verification, extraction, install/uninstall |
| `PluginAutoUpdateService.cs` | Checks for updates at startup, auto-upgrades |

### Models (`Models/`)

| File | Purpose |
|------|---------|
| `PluginManifest.cs` | Local plugin metadata (from `plugin.json`) |
| `PluginFeedIndex.cs` | Feed index structure (from `plugins-index.json`) |

### Dialogs (`Dialogs/`)

| File | Purpose |
|------|---------|
| `PluginManagerDialog.xaml/.cs` | Two-tab UI for installed/available plugins |

### Integration Points

| File | Change |
|------|--------|
| `MainWindow.xaml` | Tools menu group named `ToolsMenuGroup` for plugin access |
| `MainWindow.xaml.cs` | Plugin loading on `Loaded`, cleanup on `Closing` |
| `App.xaml.cs` | Startup auto-update call |
| `appsettings.json` | `Plugins.IndexUrl` configuration |

---

## Manifest Format

### Local `plugin.json`

```json
{
  "id": "plugin-id",
  "name": "Plugin Display Name",
  "version": "1.0.0",
  "pluginType": "action",
  "project": "",
  "description": "What this plugin does",
  "assemblyFile": "PluginName.dll",
  "entryType": "PluginNamespace.PluginClassName",
  "minAppVersion": "1.0.0",
  "maxAppVersion": ""
}
```

### Feed `plugins-index.json`

```json
{
  "plugins": [
    {
      "id": "plugin-id",
      "name": "Plugin Display Name",
      "version": "1.0.0",
      "pluginType": "action",
      "project": "",
      "description": "What this plugin does",
      "packageUrl": "https://github.com/PrinceCorwin/VANTAGE-Plugins/releases/download/tag/plugin-id.1.0.0.zip",
      "sha256": ""
    }
  ]
}
```

### Field Reference

| Field | Required | Description |
|-------|----------|-------------|
| `id` | Yes | Unique plugin identifier (lowercase, hyphens OK) |
| `name` | Yes | Display name shown in Plugin Manager |
| `version` | Yes | Semantic version (X.Y.Z) |
| `pluginType` | Yes | `"action"` (has UI) or `"extension"` (passive) |
| `project` | No | Project scope (empty = global) |
| `description` | Yes | Brief description |
| `assemblyFile` | Yes | DLL filename |
| `entryType` | Yes | Full type name implementing `IVantagePlugin` |
| `minAppVersion` | No | Minimum VANTAGE version required |
| `maxAppVersion` | No | Maximum VANTAGE version (empty = no limit) |
| `packageUrl` | Feed only | GitHub Release asset URL |
| `sha256` | Feed only | Optional integrity hash |

---

## Plugin Development

See `VANTAGE-Plugins` repository `CLAUDE.md` for detailed plugin development instructions.

### Quick Reference

1. Create project in `VANTAGE-Plugins/src/<plugin-id>/`
2. Reference `VANTAGE.dll` for plugin interfaces
3. Implement `IVantagePlugin`
4. Create `plugin.json` manifest
5. Build, package as ZIP, create GitHub Release
6. Update `plugins-index.json` with new entry

---

## Local Paths

| Path | Purpose |
|------|---------|
| `%LocalAppData%\VANTAGE\Plugins\` | Plugin install root |
| `%LocalAppData%\VANTAGE\Plugins\<id>\<version>\` | Specific plugin version |

---

## Troubleshooting

### Plugin Not Loading

1. Check `plugin.json` has valid `assemblyFile` and `entryType`
2. Verify DLL exists in plugin directory
3. Check app logs for assembly load errors
4. Ensure entry type implements `IVantagePlugin`

### Install Failures

1. Check `packageUrl` is accessible
2. Verify ZIP contains `plugin.json` at some level
3. Check feed `id`/`version` matches manifest `id`/`version`
4. GitHub raw content may cache for ~5 minutes after push

### Menu Item Not Appearing

1. Verify `Initialize()` calls `host.AddToolsMenuItem()`
2. Check plugin loaded without errors in logs
3. Confirm `pluginType` is `"action"` if expecting UI

---

## Remaining Work

### Immediate: PTP Updater Plugin — COMPLETE

First plugin created and published: `ptp-tfs-mech-updater` v1.0.0
- Imports PTP vendor shipping reports, aggregates per CWP
- Creates/updates TFS Mechanical fabrication activities (7.SHP)
- Change detection, ownership check, date tracking from report
- Published to GitHub Releases, available via Plugin Manager feed

### Future Enhancements

1. **Version constraint validation** - Check `minAppVersion`/`maxAppVersion` before loading
2. **Plugin enable/disable toggle** - Allow disabling without uninstalling
3. **Extended IPluginHost** - Add database access, more UI capabilities as needed
4. **Signature validation** - Verify plugin authenticity
5. **Rollback mechanism** - Revert to previous version if update breaks

---

## Repository Structure

Plugins are developed in a **separate GitHub repository** from the main VANTAGE app:

| Repository | Purpose |
|------------|---------|
| `PrinceCorwin/VANTAGE` | Main application code |
| `PrinceCorwin/VANTAGE-Plugins` | Plugin source code, feed index, releases |

### Local Development Setup

Clone both repositories to your local machine in separate folders:

```
/your-repos-folder/
├── VANTAGE/              # Main app (this repo)
└── VANTAGE-Plugins/      # Plugin development repo
    ├── src/              # Plugin source code
    │   └── <plugin-id>/  # One folder per plugin
    ├── plugins-index.json
    ├── CLAUDE.md         # Plugin development instructions
    └── README.md
```

The `VANTAGE-Plugins` repo has its own `CLAUDE.md` with detailed plugin development workflows. Claude can work with both repos simultaneously using absolute paths - no directory switching needed.

### Why Separate Repos?

- **Clean separation** - App code and plugin code don't mix
- **Independent versioning** - Update plugins without app releases
- **Simple distribution** - GitHub Releases provides free ZIP hosting
- **Configurable feed** - Feed URL is in `appsettings.json` if needed

## Repository Links

- **Main app:** `https://github.com/PrinceCorwin/VANTAGE`
- **Plugins:** `https://github.com/PrinceCorwin/VANTAGE-Plugins`
- **Feed URL:** `https://raw.githubusercontent.com/PrinceCorwin/VANTAGE-Plugins/main/plugins-index.json`
