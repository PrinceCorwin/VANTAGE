# Manage UDF Names — Per-User Column Rename Feature for Progress Grid (v1)

> **Cross-machine handoff:** Plan drafted on the work machine (2026-05-06). Implementation will happen on the home machine. Before starting: `git pull`, then read `CLAUDE.md`, then skim `Plans/Project_Status.md` and `Plans/Decisions.md` for current context. The May 6 entry in `Plans/Completed_Work.md` documents the immediately-prior session that this feature builds on (info icon prototype + dynamic token resolver in the WP module).
>
> **User decisions confirmed during planning:**
> - Launch the dialog from the **Settings menu** (not Tools, not sidebar, not header context menu).
> - **Apply and Save Map are separate actions.** Apply writes the active state and closes; Save Map saves a named mapping without applying or closing.
> - **v1 scope: UDFs only.** Required-metadata fields (RespParty, WorkPackage, etc.) are NOT renameable yet — they would require a centralized error-message helper first.
> - User's preferred storage shape: **one row per saved mapping**, mirroring the `ManageLayoutsDialog` precedent in `Utilities/SettingsManager.cs:320-440`.
> - Tooltip on column headers showing the original (Mapping) name was discussed; deferred to v2 since the dialog now handles all rename UX.

## Context

The Activities grid on the Progress module exposes 18 generic-named UDF columns (`UDF1`–`UDF17`, `UDF20`) that have no project-meaningful labels by default. Different organizations use these UDFs for entirely different purposes — some treat `UDF7` as "Trade Discipline," others as "Material Spec," etc. Today users have no way to relabel column headers, so they have to memorize which UDF holds what.

This feature lets users:
1. Rename any of the 18 UDFs to a custom visible label.
2. Save groups of renames as named "mappings" (e.g., "Production," "Demolition").
3. Switch between saved mappings via Apply.
4. Export a mapping to a `.json` file and Import a mapping from a `.json` file (so labels can be shared across machines).

Renames are **per-user, per-machine** (UserSettings is a local SQLite table). Renames affect only the displayed `HeaderText` — all underlying data binding, sync, exports, tokens, and validators remain keyed off `MappingName` / SQL column name.

**v1 scope is intentionally limited** to UDFs because they have zero entanglement with required-metadata error messages, conditional validators, or hardcoded user-facing strings. Expansion to other columns can come later (would require building a centralized error-message helper first; the agent investigation that confirmed this is documented in the same-day session that produced this plan).

---

## Storage shape

Three UserSetting key shapes, mirroring the existing `Utilities/SettingsManager.cs` grid-layout helpers (lines 320–440):

| Key | Type | Purpose |
|---|---|---|
| `ProgressUDFNames.Active` | JSON `Dictionary<string,string>` | Currently-applied state. Read at view init; absent or empty key for a UDF = use the default `MappingName`. |
| `ProgressUDFNames.Index` | JSON `List<string>` | Names of saved mappings (matches `GridLayouts.Index`). |
| `ProgressUDFNames.{name}.Data` | JSON `UDFNameMap` | One row per saved mapping (matches `GridLayout.{name}.Data`). |

**One row per named mapping** (not one row per field per mapping, not one giant blob). This matches the proven `ManageLayoutsDialog` precedent: clean delete, clean export, easy enumeration.

`Active` is intentionally a separate snapshot rather than a pointer to a saved-map name — that lets users edit and apply without forcing them to name and save first.

`MaxUDFMaps = 25` constant on `SettingsManager` (parallel to `MaxLayouts = 5`).

---

## Recommended approach

### New files

1. **`Utilities/ProgressRenameableColumns.cs`** — single source of truth for the v1 allowlist:
   ```csharp
   public static class ProgressRenameableColumns
   {
       public static readonly string[] UDFs = {
           "UDF1","UDF2","UDF3","UDF4","UDF5","UDF6","UDF7","UDF8","UDF9",
           "UDF10","UDF11","UDF12","UDF13","UDF14","UDF15","UDF16","UDF17","UDF20"
       };
   }
   ```
   Adding more renameable columns later = one-line change here.

2. **`Models/UDFNameMap.cs`** — model for a saved mapping:
   ```csharp
   public class UDFNameMap
   {
       public string Name { get; set; } = string.Empty;
       public Dictionary<string, string> ColumnNames { get; set; } = new();
   }
   ```

3. **`Dialogs/ManageUDFNamesDialog.xaml`** + **`.xaml.cs`** — the new dialog.

### Dialog UI

Two-pane layout in a single `Window`:

- **Left pane (main editor)** — modeled on `Dialogs/ImportTakeoffDialog.xaml:264-351`. `Border` with `BorderBrush="{DynamicResource SidebarButtonBorder}"`, `CornerRadius="4"`, `Padding="12"`. `ItemsControl` with a `DataTemplate` per row. **Drop the middle "Source" ComboBox column** that ImportTakeoffDialog uses — keep just two visible columns:
  - Col 0: width 130, `TextBlock` showing `MappingName` (e.g., "UDF1")
  - Col 2: width *, `TextBox` bound to `EditableName` (TwoWay)
  - 8px spacer column between them
  - Header row above with "Field" and "Visible Name" subtle labels (FontSize 11, Opacity 0.7) matching the precedent

- **Right pane** — saved mappings panel:
  - "Map Name:" `TextBox` (used both to name new saves and as a rename target)
  - `ListBox` of saved mapping names (selecting one populates the editor + Map Name)
  - Delete button, Rename button (mirror `ManageLayoutsDialog` button affordances)

- **Bottom button row**:
  - Left-aligned: Export, Import
  - Right-aligned: Reset to Defaults | Save Map | Apply | Cancel

- **Apply** writes editor → `ProgressUDFNames.Active`, calls `ProgressView.ApplyUDFNames()`, closes the dialog.
- **Save Map** writes editor + Map Name → `ProgressUDFNames.{name}.Data` (and adds to Index if new). Does NOT apply, does NOT close. Prompts on name collision (3-way: Overwrite / Rename / Cancel).
- **Reset to Defaults** clears `ProgressUDFNames.Active`, restores all `HeaderText` values to `MappingName`, clears the editor, leaves saved maps untouched.
- **Cancel** closes without applying.

Theme integration: `SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()))` in the constructor (matching the codebase pattern). `DynamicResource` for all brushes — no hardcoded colors.

### Validation

- Trim whitespace on editor `TextBox` LostFocus.
- Empty editor value → remove that key from the dictionary on Apply/Save (so it falls back to `MappingName`).
- Hard cap visible-name length at 50 characters (prevents grid layout breakage). Use `TextBox.MaxLength="50"`.
- Disallow control characters (filter on input).

### Import / Export

**Export:** `Microsoft.Win32.SaveFileDialog`, default filename `{MapName}.udfmap.json`. Writes a `UDFNameMap` JSON. If a saved map is selected, exports that; otherwise exports the current editor state (require the user to type a Map Name in the field first).

**Import:** `OpenFileDialog` filtered to `*.udfmap.json,*.json`. Validates schema (`Name` non-empty, `ColumnNames` is dict). On collision prompts 3-way (use `AppMessageBox`, never raw `MessageBox.Show` per CLAUDE.md): **Overwrite** existing / **Rename** (suffix `(Imported)`) / **Cancel**.

### `SettingsManager` additions

Mirror the layout helpers at `Utilities/SettingsManager.cs:320-440`:
- `GetUDFMapNames()` / `SaveUDFMapNames(List<string>)`
- `GetUDFMap(string name)` / `SaveUDFMap(UDFNameMap)`
- `DeleteUDFMap(string name)`
- `GetActiveUDFNames()` returning `Dictionary<string,string>` / `SetActiveUDFNames(Dictionary<string,string>)`
- `ClearActiveUDFNames()`
- Constants: `UDFMapIndexKey = "ProgressUDFNames.Index"`, `UDFMapDataPrefix = "ProgressUDFNames."`, `UDFMapDataSuffix = ".Data"`, `ActiveUDFNamesKey = "ProgressUDFNames.Active"`, `MaxUDFMaps = 25`

### `ProgressView.xaml.cs` integration

New method (mirroring `ScheduleView.xaml.cs:1620-1650 UpdateUDFColumnHeaders`):

```csharp
private void ApplyUDFNames()
{
    var active = SettingsManager.GetActiveUDFNames();   // Dictionary<string,string>
    foreach (var col in sfActivities.Columns)
    {
        if (!ProgressRenameableColumns.UDFs.Contains(col.MappingName)) continue;
        col.HeaderText = active != null && active.TryGetValue(col.MappingName, out var custom) && !string.IsNullOrWhiteSpace(custom)
            ? custom
            : col.MappingName;
    }
}
```

Make the method `internal` or `public` so the dialog can invoke it after Apply via the cached `ProgressView` reference held by `MainWindow`.

Call sites:
1. End of `OnViewLoaded` (after the grid is populated).
2. After any `ManageLayoutsDialog` apply call — layout apply may rebuild/reset column state. Search for the existing layout-apply hook in `ProgressView.xaml.cs` (look for `ApplyGridLayout` or similar) and call `ApplyUDFNames()` immediately afterward.

### Settings menu wiring

Add a `MenuItem` "Manage UDF Names..." to the Settings popup in `MainWindow.xaml`. Click handler in `MainWindow.xaml.cs` opens `ManageUDFNamesDialog`; on `ShowDialog() == true` (Apply pressed), invoke `ApplyUDFNames()` on the cached `ProgressView` instance (`_cachedProgressView` if present in MainWindow) so the change is visible immediately without a navigation refresh.

If the user opens the dialog before ever visiting Progress, the Apply still writes `ProgressUDFNames.Active` to settings — the value will be picked up by `ApplyUDFNames()` on the next `OnViewLoaded`.

### `UserSettingsRegistry.cs` deny-list

Append three entries to the comment block (around lines 196-199):
```
//   ProgressUDFNames.Active        — Manage UDF Names dialog
//   ProgressUDFNames.Index         — Manage UDF Names dialog
//   ProgressUDFNames.{name}.Data   — Manage UDF Names dialog (one per saved mapping)
```

### `Help/manual.html`

Short note in the Progress module section: "Use Settings → Manage UDF Names... to relabel UDF columns. You can save named mappings, switch between them, and export/import as JSON to share across machines."

---

## Critical files

**To modify:**
- `Views/ProgressView.xaml.cs` — add `ApplyUDFNames()`, hook into view-load and layout-apply
- `MainWindow.xaml` + `MainWindow.xaml.cs` — Settings menu entry + click handler (find the existing `popupSettings` MenuItem block)
- `Utilities/SettingsManager.cs` — six new helpers + four constants (insert after the existing layout helpers ~line 440)
- `Utilities/UserSettingsRegistry.cs` — three new deny-list comment entries
- `Help/manual.html` — short user-facing note

**To create:**
- `Utilities/ProgressRenameableColumns.cs`
- `Models/UDFNameMap.cs`
- `Dialogs/ManageUDFNamesDialog.xaml`
- `Dialogs/ManageUDFNamesDialog.xaml.cs`

**Reference (no changes — these are precedents to mirror):**
- `Dialogs/ImportTakeoffDialog.xaml:264-351` — Metadata section style to mirror (minus Source column)
- `Dialogs/ManageLayoutsDialog.xaml.cs` — save/delete/rename/Index pattern to mirror
- `Views/ScheduleView.xaml.cs:1620-1650` — `UpdateUDFColumnHeaders` precedent for programmatic `HeaderText`
- `Utilities/AppMessageBox.cs` — for collision prompts (per CLAUDE.md, never call `MessageBox.Show` directly)
- `Utilities/ThemeManager.cs` — for `GetSyncfusionThemeName()` used in dialog ctor

---

## Verification

1. **Build clean** — `dotnet build` succeeds with 0 errors.
2. **Open dialog** from Settings → Manage UDF Names...
   - 18 rows render in MappingName order with the correct two-column layout.
   - Theme matches the active app theme (try FluentDark and FluentLight).
3. **Edit + Apply** → close → re-open Progress module → grid headers reflect the renamed labels. Restart app → labels still applied.
4. **Save Map** with name "Production" → editor stays open → close dialog → re-open → "Production" appears in the saved-mappings list.
5. **Select saved map** → editor populates with that map's values → Apply → grid updates.
6. **Export** selected map → produces a `.udfmap.json` file with `{"name":"...","columnNames":{"UDF1":"...",...}}`.
7. **Import** that file → adds to saved list. Re-import same file → 3-way collision prompt (Overwrite / Rename / Cancel) appears and behaves correctly for each choice.
8. **Reset to Defaults** → grid reverts to MappingName labels; saved maps untouched.
9. **Apply a grid layout** (existing Manage Layouts feature) → verify UDF labels stay applied (the post-layout-apply hook is wired).
10. **Validate edge cases:** empty visible name reverts to default; >50-char input is capped; pasting a control character is rejected.
11. **Verify zero side effects** — required-metadata error messages, sync, Excel exports, WP token resolution all still reference SQL column names. Smoke-check: rename `UDF1` to "Trade Discipline", export Activities to Excel, confirm the Excel header shows "Trade Discipline" (or `UDF1`, depending on whether the exporter uses HeaderText or MappingName — verify which today). Confirm the WP-module token `{UDF1}` still resolves to the data.
12. **Reset User Settings dialog** does not list any of the three UDF-name keys (deny-list working).
13. **`/finisher`** at the end of implementation handles Project_Status / Completed_Work / Decisions / commit / push per usual.
