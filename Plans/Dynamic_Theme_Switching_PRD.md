# PRD: Dynamic Theme Switching

**Feature:** Real-time theme switching without application restart
**Priority:** Enhancement
**Status:** Planned

---

## Overview

Currently, VANTAGE requires an application restart when users change themes via the Theme Manager dialog. This PRD outlines the implementation plan to make theme changes apply instantly, providing a modern UX with live preview.

---

## Requirements

### Functional Requirements

1. **Live Preview**: Theme changes apply instantly when a radio button is selected in ThemeManagerDialog
2. **Apply Button**: Saves the currently previewed theme to user settings
3. **Close Button**: Closes dialog; if user didn't click Apply, reverts to the original theme
4. **Persistence**: Applied theme persists across application sessions (existing behavior maintained)
5. **All Windows Updated**: Theme changes affect MainWindow and all open dialogs simultaneously

### Non-Functional Requirements

1. **Performance**: Theme switching should be instant (<100ms perceived)
2. **No Breaking Changes**: Existing theme files and settings remain compatible

---

## Technical Design

### Current Architecture

**Theme Files:**
- `Themes/DarkTheme.xaml` - Dark background (#FF1E1E1E)
- `Themes/LightTheme.xaml` - Light background (#FFF3F3F3)
- `Themes/OrchidTheme.xaml` - Purple theme (#FFF6F0FA)

**Key Classes:**
- `ThemeManager.cs` - Contains `ApplyTheme()` method that swaps resource dictionaries
- `ThemeManagerDialog.xaml.cs` - Currently only saves preference, doesn't apply immediately

**Current Limitation:**
All XAML files use `StaticResource` bindings, which are evaluated once at load time and don't update when resource dictionaries change.

### Solution: DynamicResource Conversion

**Why DynamicResource?**
- `StaticResource`: Resolved once at XAML load time
- `DynamicResource`: Creates a binding that updates when the resource changes

By converting theme color references from `StaticResource` to `DynamicResource`, the UI will automatically update when `ThemeManager.ApplyTheme()` swaps the resource dictionaries.

---

## Implementation Plan

### Phase 1: ThemeManager.cs Updates

**File:** `Utilities/ThemeManager.cs`

**Add new method:**
```csharp
// Apply theme immediately to all windows
public static void ApplyThemeImmediately(string themeName)
{
    if (!Array.Exists(AvailableThemes, t => t.Equals(themeName, StringComparison.OrdinalIgnoreCase)))
        themeName = "Dark";

    CurrentTheme = themeName;
    ApplyTheme(themeName);
    RefreshSyncfusionThemeOnAllWindows();
}

// Refresh Syncfusion theme on all open windows
private static void RefreshSyncfusionThemeOnAllWindows()
{
    string sfThemeName = GetSyncfusionThemeName();
    foreach (Window window in Application.Current.Windows)
    {
        SfSkinManager.SetTheme(window, new Theme(sfThemeName));
    }
}
```

### Phase 2: ThemeManagerDialog Updates

**File:** `Dialogs/ThemeManagerDialog.xaml`

Replace the info text (Grid.Row="6") and Close button (Grid.Row="8") with:

```xml
<!-- Buttons row (replaces info text and old close button) -->
<StackPanel Grid.Row="6" Orientation="Horizontal" HorizontalAlignment="Right">
    <Button Content="Apply"
            Width="80"
            Margin="0,0,10,0"
            Click="BtnApply_Click"
            ToolTip="Apply theme and save preference"/>
    <Button Content="Close"
            Width="80"
            Click="BtnClose_Click"
            ToolTip="Close theme selector"/>
</StackPanel>
```

Remove Grid.Row="8" button section entirely.

**File:** `Dialogs/ThemeManagerDialog.xaml.cs`

Add fields:
```csharp
private string _originalTheme = null!;
private string _previewTheme = null!;
private bool _applied;
```

Update constructor:
```csharp
public ThemeManagerDialog()
{
    InitializeComponent();
    _originalTheme = ThemeManager.CurrentTheme;
    _previewTheme = _originalTheme;
    _applied = false;

    // Apply Syncfusion theme to this dialog
    SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

    // Set initial radio button state
    switch (_originalTheme)
    {
        case "Light": rbLight.IsChecked = true; break;
        case "Orchid": rbOrchid.IsChecked = true; break;
        default: rbDark.IsChecked = true; break;
    }

    _initialized = true;
}
```

Update RbTheme_Checked handler:
```csharp
private void RbTheme_Checked(object sender, RoutedEventArgs e)
{
    if (!_initialized) return;

    string selectedTheme = rbOrchid.IsChecked == true ? "Orchid"
        : rbLight.IsChecked == true ? "Light" : "Dark";

    _previewTheme = selectedTheme;
    ThemeManager.ApplyThemeImmediately(selectedTheme);

    // Refresh this dialog's Syncfusion theme
    SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
}
```

Add Apply handler:
```csharp
private void BtnApply_Click(object sender, RoutedEventArgs e)
{
    ThemeManager.SaveTheme(_previewTheme);
    _applied = true;
    _originalTheme = _previewTheme; // Update original so Close won't revert
}
```

Update Close handler:
```csharp
private void BtnClose_Click(object sender, RoutedEventArgs e)
{
    // If user previewed but didn't apply, revert to original theme
    if (!_applied && _previewTheme != _originalTheme)
    {
        ThemeManager.ApplyThemeImmediately(_originalTheme);
    }
    Close();
}
```

### Phase 3: Convert StaticResource to DynamicResource

**Scope:** ~1,178 occurrences across ~40 XAML files

**Rule:** Convert ONLY theme color resources. Keep StaticResource for:
- Style references (e.g., `Style="{StaticResource ToolbarButtonStyle}"`)
- Converters (e.g., `Converter="{StaticResource BoolToVisibilityConverter}"`)
- Resources inside ControlTemplate.Triggers (WPF limitation - see Phase 4)

**Theme color resource keys to convert:**
```
BackgroundColor, DarkBackgroundColor, DarkestBackgroundColor
ForegroundColor, TextColorSecondary
AccentColor, BorderColor, DisabledColor
ControlBackground, ControlForeground, ControlBorder
ControlHoverBackground, ControlPressedBackground
StatusBarBackground, StatusBarForeground
ToolbarForeground, ToolbarBackground
StatusGreen, StatusGreenBorder, StatusYellow, StatusYellowBorder
StatusRed, StatusRedBorder, StatusBlue, StatusBlueBorder
StatusOrange, StatusOrangeBorder
ButtonSuccessBackground, ButtonSuccessForeground, ButtonSuccessBorder
ButtonSuccessHover, ButtonSuccessPressed
ButtonDangerBackground, ButtonDangerForeground, ButtonDangerBorder
ButtonDangerHover, ButtonDangerPressed
OverlayBackground, ErrorText, WarningText, SuccessText
SidebarHeaderBackground, SidebarTitleBackground, SidebarContentBackground
DividerBrush, DividerBrushDark
AnalysisSidePanelBackground, AnalysisBorderColor, AnalysisHeaderBackground
```

**Files to modify (by priority):**

| # | File | ~Count | Notes |
|---|------|--------|-------|
| 1 | `Views/ProgressView.xaml` | 172 | Largest, convert first to validate approach |
| 2 | `Views/ScheduleView.xaml` | 92 | |
| 3 | `MainWindow.xaml` | 54 | Close button template has triggers |
| 4 | `Views/WorkPackageView.xaml` | 44 | |
| 5 | `Views/ProgressBooksView.xaml` | 34 | |
| 6 | `Views/AnalysisView.xaml` | 30 | |
| 7 | `Views/SidePanelView.xaml` | 30 | |
| 8 | `Views/DeletedRecordsView.xaml` | 23 | |
| 9 | `Dialogs/ProgressScanDialog.xaml` | 65 | |
| 10 | `Dialogs/AdminProjectsDialog.xaml` | 58 | |
| 11 | `Dialogs/FeedbackDialog.xaml` | 52 | |
| 12 | `Dialogs/AdminUsersDialog.xaml` | 34 | |
| 13 | `Dialogs/ManageFiltersDialog.xaml` | 29 | |
| 14 | `Dialogs/AdminSnapshotsDialog.xaml` | 29 | |
| 15 | `Dialogs/P6ExportDialog.xaml` | 28 | |
| 16 | `Dialogs/ExportLogsDialog.xaml` | 28 | |
| 17 | `Dialogs/ProrateDialog.xaml` | 28 | |
| 18 | `Dialogs/ManageLayoutsDialog.xaml` | 27 | |
| 19 | `Dialogs/ManageProgressLogDialog.xaml` | 23 | |
| 20 | `Dialogs/GenerateProgressBookDialog.xaml` | 22 | |
| 21 | `Dialogs/AccessRequestDialog.xaml` | 21 | |
| 22 | `Dialogs/P6ImportDialog.xaml` | 21 | |
| 23 | `Dialogs/ManageSnapshotsDialog.xaml` | 20 | |
| 24 | `Dialogs/UnsyncedChangesWarningDialog.xaml` | 19 | |
| 25 | `Dialogs/ProcoreAuthDialog.xaml` | 19 | |
| 26 | `Dialogs/ScheduleChangeLogDialog.xaml` | 18 | |
| 27 | `Dialogs/SyncDialog.xaml` | 18 | |
| 28 | `Dialogs/TemplateTypeDialog.xaml` | 17 | |
| 29 | `Dialogs/ConnectionRetryDialog.xaml` | 16 | |
| 30 | `Dialogs/SkippedRecordsDialog.xaml` | 12 | |
| 31 | `Dialogs/TemplateNameDialog.xaml` | 12 | |
| 32 | `Dialogs/ResetTemplateDialog.xaml` | 11 | |
| 33 | `Dialogs/CustomPercentDialog.xaml` | 11 | |
| 34 | `Dialogs/ThemeManagerDialog.xaml` | 8 | |
| 35 | `Dialogs/LoadingSplashWindow.xaml` | 5 | |
| 36 | `Dialogs/BusyDialog.xaml` | 4 | |

**Search/Replace pattern:**
```
Find: StaticResource BackgroundColor
Replace: DynamicResource BackgroundColor
```
(Repeat for each theme color resource key)

### Phase 4: ControlTemplate Trigger Handling

**WPF Limitation:** `DynamicResource` cannot be used inside `ControlTemplate.Triggers` setters.

**Files with trigger issues:**
- `MainWindow.xaml` - CloseButtonStyle
- `Views/ProgressView.xaml` - RoundedButtonStyle
- Theme files (DarkTheme.xaml, etc.) - Button styles

**Solution:**
- **Theme file styles:** Leave as StaticResource. Since the entire dictionary is swapped, the new styles with correct colors are loaded.
- **View-local styles with triggers:** Leave StaticResource in trigger setters. These won't update dynamically but will be correct on next view load. Accept this minor limitation.

### Phase 5: Documentation Update

**File:** `Help/manual.html`

Update the Theme section to remove the restart requirement language and document the new Apply/Close behavior.

---

## Testing Checklist

### Basic Functionality
- [ ] Open app with Dark theme (default)
- [ ] Open ThemeManagerDialog from Settings menu
- [ ] Click Light radio button → MainWindow updates instantly
- [ ] Click Orchid radio button → MainWindow updates instantly
- [ ] Click Dark radio button → MainWindow updates instantly
- [ ] Click Apply → Theme is saved
- [ ] Close dialog → Theme persists
- [ ] Restart app → Saved theme loads correctly

### Revert Behavior
- [ ] Open dialog, change theme via radio button
- [ ] Click Close without Apply → Theme reverts to original
- [ ] Verify MainWindow shows original theme

### Multiple Windows
- [ ] Open a dialog (e.g., Sync dialog)
- [ ] Open ThemeManagerDialog
- [ ] Change theme → Both dialogs update
- [ ] All open windows reflect new theme

### Edge Cases
- [ ] Change theme multiple times before Apply → Only final theme saved
- [ ] Apply, then change again, then Close → Stays on applied theme (not second preview)
- [ ] Theme works correctly on all modules (Progress, Schedule, Work Package, Analysis)

### Visual Verification
Each theme should have:
- [ ] Correct background color
- [ ] Readable text (contrast)
- [ ] Consistent button styling
- [ ] Status bar colors correct
- [ ] DataGrid styling correct

---

## Known Limitations

1. **ControlTemplate Triggers:** Custom styles defined in Views that use StaticResource in trigger setters won't update dynamically. They will show correct colors on next view navigation.

2. **Syncfusion Control Override Colors:** If any Syncfusion control has hardcoded color overrides (not using theme resources), those won't update. Review and use theme resources instead.

---

## Rollback Plan

If issues arise:
1. Revert ThemeManagerDialog changes
2. Revert ThemeManager.cs changes
3. Keep DynamicResource conversions (these don't break anything)
4. Restore "restart required" message

---

## Success Criteria

1. Users can preview all three themes without restart
2. Apply button persists theme choice
3. Close without Apply reverts preview
4. No visual regressions in any theme
5. Build succeeds with no errors
