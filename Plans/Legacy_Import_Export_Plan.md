# Legacy Import/Export Format Options Plan

**Created:** January 16, 2026
**Status:** Ready for Implementation

## Overview

Update existing import/export menu items to use Milestone format (ColumnName headers) as the new default. Add new "Legacy" menu items for backward compatibility with old Vantage column headers. Include user toggle to hide legacy options when no longer needed.

## Background

Currently, all activity imports and exports use the legacy Vantage column headers (e.g., `Tag_ProjectID`, `Catg_ComponentType`) via the `ColumnMapper` class. As we transition away from legacy Vantage, new users who haven't used the legacy system will be confused by Excel headers that don't match the app's display.

**Goal:** Make the default format use internal property names (e.g., `ProjectID`, `CompType`) while preserving backward compatibility for existing Vantage users.

## Menu Structure Changes

### Current File Menu (Activities Section):
```
Import Activities (Replace)
Import Activities (Combine)
Export Activities
Export Activities Template
Create Activities
Clear Local Activities
─────────────────────────
[P6 section...]
```

### New File Menu Structure:
```
Import Activities (Replace)         → Keep name, change to Milestone format
Import Activities (Combine)         → Keep name, change to Milestone format
Export Activities                   → Keep name, change to Milestone format
Export Activities Template          → Keep name, change to Milestone format
Create Activities
Clear Local Activities
─────────────────────────
[P6 section...]
─────────────────────────
Import Legacy (Replace)             ← NEW (uses old Vantage headers)
Import Legacy (Combine)             ← NEW (uses old Vantage headers)
Export Legacy                       ← NEW (uses old Vantage headers)
Export Legacy Template              ← NEW (uses old Vantage headers)
```

### Tools Menu Addition:
```
[existing items...]
─────────────────────────
Toggle Legacy I/O                   ← NEW (hides/shows Legacy menu items)
```

## Format Comparison

| Format | Header Example | Use Case |
|--------|----------------|----------|
| Milestone (new default) | `ProjectID`, `CompType`, `Description` | New users, future standard |
| Legacy (Vantage) | `Tag_ProjectID`, `Catg_ComponentType`, `Progress_Description` | Backward compatibility |

## Implementation Steps

### 1. Add UserSetting for Legacy I/O Visibility
**File:** `Data/SettingsManager.cs`

Add new setting methods:
```csharp
// Default true (show legacy options initially)
public static bool GetShowLegacyIO() { ... }
public static void SetShowLegacyIO(bool value) { ... }
```

### 2. Add Export Format Enum
**File:** `Utilities/ExcelExporter.cs`

```csharp
public enum ExportFormat
{
    Milestone,  // Uses ColumnName directly (ProjectID, CompType, etc.)
    Vantage     // Uses OldVantageName (Tag_ProjectID, Catg_ComponentType, etc.)
}
```

### 3. Update ExcelExporter for Dual Format Support
**File:** `Utilities/ExcelExporter.cs`

- Modify `ExportActivities()` signature to accept `ExportFormat format` parameter
- Modify `ExportTemplate()` signature to accept `ExportFormat format` parameter
- When writing headers:
  - Milestone: Use `ColumnMapper.GetColumnName()` or just use the dbColumn name directly
  - Vantage: Use existing `GetOldVantageName()` behavior

Key change in header writing:
```csharp
// For each column in ExportColumnOrder
string headerName = format == ExportFormat.Milestone
    ? ColumnMapper.GetColumnNameFromOldVantage(oldVantageName)  // Returns ColumnName
    : oldVantageName;  // Keep legacy Vantage name
worksheet.Cells[1, col].Value = headerName;
```

### 4. Update ExcelImporter for Dual Format Support
**File:** `Utilities/ExcelImporter.cs`

- Add `ExportFormat format` parameter to `ImportActivitiesAsync()`
- Modify `BuildColumnMap()` to accept format parameter:
  - Milestone: Header name IS the ColumnName (direct property access)
  - Vantage: Use `GetColumnNameFromOldVantage()` translation (existing behavior)

```csharp
private static Dictionary<int, string> BuildColumnMap(IXLWorksheet worksheet, ExportFormat format)
{
    // Read header row
    foreach column in row 1:
        string headerValue = cell.GetString();
        string columnName = format == ExportFormat.Milestone
            ? headerValue  // Header IS the property name
            : ColumnMapper.GetColumnNameFromOldVantage(headerValue);  // Translate from legacy

        if (!string.IsNullOrEmpty(columnName))
            columnMap[colIndex] = columnName;
}
```

### 5. Update ExportHelper for Format Parameter
**File:** `Utilities/ExportHelper.cs`

- Add `ExportFormat format` parameter to:
  - `ExportActivitiesWithOptionsAsync()`
  - `ExportSelectedActivitiesAsync()`
  - `ExportDeletedRecordsAsync()`
  - `ExportTemplateAsync()`
- Update file naming based on format:
  - Milestone: `MILESTONE_Export_yyyyMMdd_HHmmss.xlsx`
  - Vantage: `VANTAGE_Export_yyyyMMdd_HHmmss.xlsx`

### 6. Update MainWindow.xaml Menu Items
**File:** `MainWindow.xaml`

Add new Legacy menu items with `x:Name` for visibility binding:

```xml
<!-- After P6 section, add separator and Legacy items -->
<Separator x:Name="LegacySeparator" />
<MenuItem x:Name="MenuImportLegacyReplace" Header="Import Legacy (Replace)"
          Click="MenuLegacyImportReplace_Click" />
<MenuItem x:Name="MenuImportLegacyCombine" Header="Import Legacy (Combine)"
          Click="MenuLegacyImportCombine_Click" />
<MenuItem x:Name="MenuExportLegacy" Header="Export Legacy"
          Click="MenuLegacyExport_Click" />
<MenuItem x:Name="MenuExportLegacyTemplate" Header="Export Legacy Template"
          Click="MenuLegacyExportTemplate_Click" />
```

Add to Tools menu:
```xml
<Separator />
<MenuItem Header="Toggle Legacy I/O" Click="MenuToggleLegacyIO_Click" />
```

### 7. Update MainWindow.xaml.cs Event Handlers
**File:** `MainWindow.xaml.cs`

Update existing handlers to pass Milestone format:
```csharp
private async void MenuExcelImportReplace_Click(object sender, RoutedEventArgs e)
{
    await ImportActivities(replaceMode: true, ExportFormat.Milestone);
}
```

Add new handlers for Legacy format:
```csharp
private async void MenuLegacyImportReplace_Click(object sender, RoutedEventArgs e)
{
    await ImportActivities(replaceMode: true, ExportFormat.Vantage);
}

private async void MenuLegacyExport_Click(object sender, RoutedEventArgs e)
{
    await ExportHelper.ExportActivitiesWithOptionsAsync(..., ExportFormat.Vantage);
}
```

Add toggle handler and visibility management:
```csharp
private void MenuToggleLegacyIO_Click(object sender, RoutedEventArgs e)
{
    bool currentValue = SettingsManager.GetShowLegacyIO();
    SettingsManager.SetShowLegacyIO(!currentValue);
    UpdateLegacyMenuVisibility();
}

private void UpdateLegacyMenuVisibility()
{
    bool show = SettingsManager.GetShowLegacyIO();
    Visibility visibility = show ? Visibility.Visible : Visibility.Collapsed;

    LegacySeparator.Visibility = visibility;
    MenuImportLegacyReplace.Visibility = visibility;
    MenuImportLegacyCombine.Visibility = visibility;
    MenuExportLegacy.Visibility = visibility;
    MenuExportLegacyTemplate.Visibility = visibility;
}
```

Call `UpdateLegacyMenuVisibility()` in window initialization.

## Files to Modify

| File | Changes |
|------|---------|
| `Data/SettingsManager.cs` | Add ShowLegacyIO setting |
| `Utilities/ExcelExporter.cs` | Add ExportFormat enum, format parameter |
| `Utilities/ExcelImporter.cs` | Add format parameter to import methods |
| `Utilities/ExportHelper.cs` | Pass format to exporter, update file naming |
| `MainWindow.xaml` | Add Legacy menu items, Tools menu toggle |
| `MainWindow.xaml.cs` | New handlers, visibility logic |

## Verification Checklist

- [ ] **Export Activities** (default) - Headers use ColumnName values (ProjectID, CompType, etc.)
- [ ] **Export Legacy** - Headers match old Vantage format (Tag_ProjectID, Catg_ComponentType, etc.)
- [ ] **Import Activities** (default) - Reads Milestone format files (ColumnName headers)
- [ ] **Import Legacy** - Reads old Vantage format files correctly
- [ ] **Toggle Legacy I/O** - Hides/shows Legacy menu items
- [ ] **Toggle persists** - Setting saved to UserSettings, restored on restart
- [ ] **Template exports** - Both formats produce correct header-only files
- [ ] **File naming** - Default: `MILESTONE_Export_...`, Legacy: `VANTAGE_Export_...`

## Notes

- Milestone format uses the internal `ColumnName` values directly - no new mapping table needed
- Legacy users can continue using the "Legacy" menu items indefinitely
- Once all users have migrated, the admin can hide Legacy options via the toggle
- The toggle is in Tools menu (not Admin) so any user can hide Legacy options if desired
