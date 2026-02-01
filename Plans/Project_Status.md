# MILESTONE - Project Status

**Last Updated:** February 1, 2026

## V1 Testing Scope

### In Scope
| Feature | Status |
|---------|--------|
| Progress Module | Ready for testing |
| Schedule Module | Ready for testing |
| Sync | Complete |
| Admin | Complete |
| Work Package (PDF generation) | Ready for testing (drawings hidden) |
| Help Sidebar | Complete for V1 |
| Progress Book creation | Ready for testing |
| AI Progress Scan | Complete - AWS Textract, 100% accuracy |

### Deferred to Post-V1
| Feature | Reason |
|---------|--------|
| Drawings in Work Packages | Per-WP location architecture needs design |
| AI Features (other than Progress Scan) | Lower priority for V1 |
| ~~Theme Selection~~ | Implemented (multi-themes branch) |
| Procore Integration | Can develop while users test |
| ~~Help Troubleshooting section~~ | Implemented — Azure firewall, SmartScreen, post-update |

## Module Status

| Module | Status | Notes |
|--------|--------|-------|
| Progress | READY FOR TESTING | Core features complete |
| Schedule | READY FOR TESTING | Core features complete |
| Sync | COMPLETE | Bidirectional sync working |
| Admin | COMPLETE | User/project/snapshot management |
| Work Package | READY FOR TESTING | PDF generation working; Drawings deferred to post-v1 |
| Help Sidebar | COMPLETE | All V1 sections written; Troubleshooting deferred to post-V1 |
| AI Progress Scan | COMPLETE | AWS Textract implementation - 100% accuracy |
| AI Features (other) | NOT STARTED | Error Assistant, Description Analysis, etc. |

## Active Development

### Multi-Theme System (branch: multi-themes)

**Current state:** Dark, Light, and Orchid themes implemented and working. Architecture supports adding more themes.

**How to add a new theme (e.g. "Warm", "Ocean", "HighContrast"):**

1. **Add Syncfusion theme NuGet** — Each theme needs a matching Syncfusion base theme. Currently using `Syncfusion.Themes.FluentDark.WPF` and `Syncfusion.Themes.FluentLight.WPF`. New themes should use one of these as a base (FluentDark for dark themes, FluentLight for light themes). Add the package to `VANTAGE.csproj` if not already present.

2. **Create `Themes/NewTheme.xaml`** — Copy `DarkTheme.xaml` or `LightTheme.xaml` as a starting point. All themes must define the exact same set of resource keys. Current keys (as of Feb 2026):
   - **Color palette:** BackgroundColor, DarkBackgroundColor, DarkestBackgroundColor, ForegroundColor, TextColorSecondary, AccentColor, BorderColor, DisabledColor
   - **Window:** WindowBackground, ControlBackground, ControlBackgroundGreen, ControlBackgroundRed, ControlForeground, ControlBorder, ControlHoverBackground, ControlPressedBackground
   - **DataGrid:** GridHeaderBackground
   - **Filter icons:** FilterIconColor (Color), FilterIconActiveColor (Color) — see filter icon section below
   - **Status:** StatusGreen, StatusYellow, StatusYellowBg, StatusRed, StatusRedBg, StatusInProgress, StatusNotStarted
   - **Active filter border:** ActiveFilterBorderColor
   - **Toolbar:** ToolbarBackground, ToolbarForeground, ToolbarHoverBackground, ToolbarHoverForeground
   - **Grid headers:** GridHeaderForeground
   - **Action buttons:** ActionButtonForeground
   - **StatusBar:** StatusBarBackground, StatusBarForeground
   - **Non-owned rows:** NotOwnedRowBackground, NotOwnedRowForeground
   - **Action buttons (bg):** ButtonSuccessBackground/Border/Hover, ButtonDangerBackground/Border/Hover, ButtonPrimaryBackground/Border/Hover
   - **Overlay:** OverlayBackground, OverlayText, OverlayTextSecondary
   - **Error/Warning:** ErrorText, WarningText, WarningHighlight, WarningHighlightBackground
   - **UI elements:** SidebarBackground, SidebarBorder, DividerColor, SplitterDots, DisabledText
   - **Font:** FontFamilyPrimary, FontSizeNormal
   - **Styles:** Default Button style (implicit, no key), ToolbarButtonStyle (x:Key)

3. **Register in `ThemeManager.cs`** — Add entry to both dictionaries:
   ```csharp
   // In ThemeMap: "DisplayName" → "SyncfusionThemeName"
   { "Warm", "FluentDark" }  // or "FluentLight" depending on base
   // In AvailableThemes array:
   public static readonly string[] AvailableThemes = { "Dark", "Light", "Orchid" };
   ```
   The `ApplyTheme()` method handles swapping by URI pattern matching — it finds and removes the current theme dictionary (`/Themes/*.xaml`) and Syncfusion MSControl dictionaries, then adds the new ones. No changes needed to the swap logic itself.

4. **Update ThemeManagerDialog** — Add a RadioButton for the new theme in `Dialogs/ThemeManagerDialog.xaml` and wire up the selection in `ThemeManagerDialog.xaml.cs`.

5. **Update Help manual** — Update the Theme... entry in `Help/manual.html`.

**Key architecture decisions and pitfalls to avoid:**

- **StaticResource, not DynamicResource.** All XAML files use `StaticResource` bindings. This works because the theme dictionary is swapped *before* any windows are created during `App.xaml.cs` startup. Do NOT convert to DynamicResource — there are 1,000+ references and it would be a massive change for no benefit.

- **Theme is applied on restart, not live.** `ThemeManager.SaveTheme()` saves to UserSettings. `ThemeManager.LoadThemeFromSettings()` runs once at startup before `new MainWindow()`. The ThemeManagerDialog tells users "Theme will be applied when the application restarts."

- **App.xaml declares FluentDark + DarkTheme.xaml as defaults.** These are the compile-time defaults in the XAML. `ThemeManager.LoadThemeFromSettings()` swaps them only if the saved setting is not "Dark".

- **SfSkinManager calls are in code-behind, not XAML.** All 22 dialog/view code-behinds call `SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()))` in their constructors. The XAML `SkinManagerExtension` attributes were removed because they're compile-time constants that can't read a runtime setting.

- **Resource naming is role-based, not appearance-based.** Names like `ToolbarForeground`, `GridHeaderForeground`, `ActionButtonForeground` describe *where* the color is used, not *what* it looks like. A pink theme would set `ToolbarForeground` to whatever works on a pink toolbar. Do NOT name resources after their color (e.g. "LightText", "DarkSurface").

- **`ToolbarButtonStyle`** is a named Button style in the theme for Settings/Minimize/Maximize buttons. It has its own hover triggers using `ToolbarHoverBackground`/`ToolbarHoverForeground` because the default Button style's hover uses `ControlHoverBackground` which doesn't work on the always-dark toolbar in the Light theme.

- **`ActionFilterToggleStyle`** in `ScheduleView.xaml` resources — The Required Fields toggle button has a dark background (`StatusRedBg`) with light text (`ActionButtonForeground`). It needs its own style because WPF local values override style/template triggers, so the hover foreground can't be changed by the shared `FilterToggleButtonStyle`. This style has `ForegroundColor` on hover so text is readable on the light hover background.

- **Nav button foreground in code-behind.** `MainWindow.xaml.cs` `HighlightNavigationButton()` resets inactive nav buttons to `FindResource("ToolbarForeground")`. The active button gets `AccentColor`. This was a bug — it originally used `ForegroundColor` which is dark in light theme, making inactive nav buttons invisible on the dark toolbar.

- **`SettingsManager.cs` guard bug (fixed).** `InitializeDefaultUserSettings()` had a guard checking `GetUserSetting("LastModuleUsed")` which was never written anywhere, so it re-initialized Theme to "Dark" on every startup. Fixed to check `GetUserSetting("Theme")` instead. Don't reintroduce this pattern.

- **Syncfusion MSControl dictionaries.** Each Syncfusion theme has 4 MSControl dictionaries (Button, Window, StatusBar, TabControl) that must be swapped along with the app theme dictionary. `ThemeManager.ApplyTheme()` handles this by matching URI patterns containing `MSControl` and the Syncfusion theme name.

- **Grid filter icons use a custom template, not Syncfusion's.** The Syncfusion `FilterToggleButton` template resolves its icon colors from internal compiled BAML that cannot be overridden via resource dictionaries, `Application.Current.Resources`, or `SfDataGrid.Resources` — SfSkinManager injects its own styles at a scope that overrides all of these. The solution: a custom `FilterToggleButton` style with a full `ControlTemplate` override is defined in each grid's `SfDataGrid.Resources` (ScheduleView master+detail, ProgressView, ProgressScanDialog). The template provides:
  - Our own funnel Path geometry: `M 1,2 L 11,2 L 7,6 L 7,10 L 5,10 L 5,6 Z`
  - `Stroke`-based rendering (outline only, `Fill="Transparent"`)
  - `VisualStateManager` with `FilterStates` group (`Filtered`/`UnFiltered` states) — Syncfusion's control code triggers these state transitions
  - `FilterIconColor` (Color resource from theme) for the default stroke, `FilterIconActiveColor` for active filter stroke (red)
  - `Background="Transparent"` on the Grid wrapper for hit testing (clickable area)
  - No `PART_FilterToggleButtonIndicator` name on the Path — intentionally omitted so Syncfusion code can't override our path data
  - When adding a new theme, set `FilterIconColor` to a color that's visible on the `GridHeaderBackground` (dark headers need light icon, light headers need dark icon). `FilterIconActiveColor` should be a high-contrast red/accent to indicate active filtering.

### Progress Book Module
- Phases 1-6 complete: Data models, repository, layout builder UI, PDF generator, live preview, generate dialog
- PDF features: Auto-fit column widths, description wrapping, project description in header
- Layout features: Separate grouping and sorting, up to 10 levels each, exclude completed option

### AI Progress Scan - AWS Textract Implementation (COMPLETE)

**Current State:**
- Switched from Claude Vision API to AWS Textract for table extraction
- 100% accuracy achieved on PDF and JPEG scans
- Simplified PDF layout: ID first, single % ENTRY column at far right

**PDF Layout:**
```
| ID | [user cols] | MHs | QTY | REM MH | CUR % | % ENTRY |
```
- ID (ActivityID) as first column - protected from accidental marks
- Data columns: MHs (BudgetMHs), QTY (Quantity), REM MH, CUR %
- % ENTRY box at far right - natural stopping point for field hands
- Writing "100" = done (no checkbox needed)

**Key Files:**
- `Services/AI/TextractService.cs` - AWS Textract API wrapper
- `Services/AI/PdfToImageConverter.cs` - PDF to image conversion (Syncfusion)
- `Services/AI/ProgressScanService.cs` - Scan orchestration
- `Services/ProgressBook/ProgressBookPdfGenerator.cs` - PDF generation

**Enhancements:**
- Image preprocessing with contrast adjustment (slider in results dialog, default 1.2)
- OCR heuristic: "00" auto-converts to "100" (handles missed leading 1)
- Results grid: column filtering, BudgetMHs column, Select All/Select Ready/Clear buttons

### Work Package Module
- Template editors testing
- PDF preview testing

### Help Sidebar — Complete for V1
All V1 sections of `Help/manual.html` are written. Screenshots configured with correct Build Action.

**Completed sections:**
- 1. Getting Started
- 2. Main Interface
- 3. Progress Module
- 4. Schedule Module
- 5. Progress Books
- 6. Work Packages
- 7. Administration
- 8. Reference

**Deferred to post-V1:**
- Troubleshooting section — will populate after initial users report real issues

**Screenshots:**
- 20 screenshots in `Help/` folder, Build Action set to Content / Copy if newer
- WebView2 uses virtual host mapping (`https://help.local/manual.html`) — see `SidePanelView.xaml.cs`
- VS sometimes re-adds PNGs as `<Resource Include>` — always verify Content / Copy if newer

## Feature Backlog

### High Priority
- **NEXT:** First publish & end-to-end test (Workstream 4 in `Packaging_Credentials_Installer_Plan.md`). Run publish script, create GitHub Release, populate manifest, build installer exe, test full install → update cycle. Branch: `update-pack-cred`.
- ~~Credentials strategy~~ Complete — migrated to encrypted config file (Workstream 1)
- ~~Self-contained publish config~~ Code complete, untested — main app + updater (Workstream 2)
- ~~Installer app~~ Code complete, untested with real download — branded WPF installer with desktop shortcut (Workstream 3)


### Medium Priority
- **DISCUSS:** Add PlanStart and PlanFinish fields to Activities (for baseline schedule comparison?)
- Table Summary V2: Settings dialog to choose which columns to summarize and aggregate types (Sum/Avg/Count)
- User-editable header template for WP (allow customizing header layout)

### V2 Data Model
- Add ClientEarnedEquivQty column to Activities table, Azure VMS_Activities, and ColumnMappings (maps to OldVantage `VAL_Client_Earned_EQ-QTY`) - currently ignored during import

### V2 Architecture Revisit
- **Schedule CellStyle DataTrigger binding approach** -- The Schedule master grid uses `CellStyle` with `DataTrigger` bindings on 8 columns (MissedStartReason, MissedFinishReason, 3WLA Start/Finish, MS Start/Finish, MS %/MHs) to conditionally color cells red/yellow. These bindings reference bool properties on `ScheduleMasterRow` (e.g., `IsMissedStartReasonRequired`, `HasStartVariance`). Syncfusion's SfDataGrid cell recycling temporarily sets the GridCell DataContext to `ScheduleViewModel` instead of the row data, causing WPF Error 40 binding failures. Current fix: 8 dummy `=> false` properties on `ScheduleViewModel` (line ~30) so the binding resolves without error during the transient state. Proper fix would be replacing the simple `{Binding Path=PropName}` DataTrigger bindings with `MultiBinding` + `IMultiValueConverter` that type-checks the DataContext, preserving PropertyChanged reactivity. See `ScheduleView.xaml` lines 83-145 (styles) and `ScheduleViewModel.cs` dummy properties block.

### AI Features (see InCode_AI_Plan.md)
| Feature | Status |
|---------|--------|
| AI Progress Scan | Complete - AWS Textract, 100% accuracy |
| AI Scan pre-filter | Not Started - Local image analysis to detect marks before calling Claude API (skip blank pages, reduce API cost) |
| ClaudeApiService infrastructure | Complete (via Progress Scan) |
| AI Error Assistant | Not Started |
| AI Description Analysis | Not Started |
| Metadata Consistency Analysis | Not Started |
| AI MissedReason Assistant | Not Started |
| AI Schedule Analysis | Deferred |

### AI Sidebar Chat (see Sidebar_AI_Assistant_Plan.md)
| Phase | Status |
|-------|--------|
| Chat UI | Not Started |
| Conversation Management | Not Started |
| Tool Definitions | Not Started |
| Tool Execution | Not Started |

### Post-V1: Drawings Architecture
- Design per-WP drawing location system (options: token paths, per-WP config, Drawings Manager)
- Fix preview display
- Fix layout/orientation for 11x17 drawings
- Implement Procore fetch
- Consider AI-assisted drawing matching (DwgNO formats, revisions, sheet numbers)

**Code disabled for v1 (re-enable when drawings architecture is ready):**
| File | What to re-enable |
|------|-------------------|
| `WorkPackageView.xaml` | Remove `Visibility="Collapsed"` from Drawings section Border (~line 238) |
| `WorkPackageView.xaml.cs` | Remove `.Where(t => t.TemplateType != TemplateTypes.Drawings)` from `PopulateAddFormMenu()` |
| `WorkPackageView.xaml.cs` | Remove filter in `ApplyWPFormsListFilter()` method |
| `WorkPackageView.xaml.cs` | Remove early return in `BuildDrawingsEditor()` and `#pragma warning` directives |
| `DrawingsRenderer.cs` | Remove early return in `Render()` method and `#pragma warning` directives |

### Syncfusion Features to Evaluate

**Dashboard Module (Post-V1):**
| Feature | Description |
|---------|-------------|
| Column/Stacked Chart | Daily/weekly activity completion counts, productivity trends |
| S-Curve (Line/Area Chart) | Planned vs actual progress over time |
| Pie/Doughnut Chart | Distribution by WorkPackage, PhaseCode, or RespParty |
| Gantt Chart | Visual schedule with dependencies (complements P6 import) |
| Radial Gauge | Dashboard widget for overall project % complete |
| Bullet Graph | Performance vs target KPIs per work package |

**V2:**
| Feature | Description |
|---------|-------------|
| Docking Manager | Visual Studio-like docking for flexible panel/toolbar layouts |

**Schedule Module:**
| Feature | Description |
|---------|-------------|
| TreeGrid (SfTreeGrid) | Hierarchical WBS display with parent/child relationships |
| Critical Path Highlighting | Auto-highlight critical path activities (P6 provides float data) |

**Evaluated and Removed:**
- Column Chooser - current checkbox popup is more intuitive
- Stacked Headers - adds complexity without benefit
- Custom Aggregates - Summary Panel already shows weighted progress
- Row Drag & Drop - conflicts with P6 sync and data model
- Checkbox Selection - Ctrl+Click multi-select is sufficient
- Export/Print - Syncfusion printing issues; use Progress Books instead

### Shelved
- Find-Replace in Schedule Detail Grid - deferred to V2; may need redesign of main/detail grid interaction
- Offline Indicator in status bar - clickable to retry connection
- Disable Tooltips setting (see DisableTooltips_Plan.md)
- Interactive Help Mode - click UI controls to navigate to documentation (see Sidebar_Help_Plan.md)

## Known Issues

### AI Progress Scan - Accuracy Issues (RESOLVED)
- ~~PDF scans less accurate than JPEG scans~~ Fixed with AWS Textract
- ~~Checkmarks sometimes missed~~ Removed checkbox, use % entry instead
- Now achieving 100% accuracy on both PDF and JPEG scans

## Test Scenarios Validated

- Import -> Edit -> Sync -> Pull cycle
- Multi-user ownership conflicts
- Deletion propagation
- Metadata validation blocking
- Offline mode with retry dialog
- P6 Import/Export cycle
- Schedule filters and conditional formatting
- Detail grid editing with rollup recalculation
- Email notifications
- Admin dialogs (Users, Projects, Snapshots)
- UserSettings export/import with immediate reload
- Log export to file and email with attachment
- User-defined filters create/edit/delete and apply
- Grid layouts save/apply/rename/delete and reset to default
- Prorate MHs with various operation/preserve combinations
- Discrepancy dropdown filter
- My Records Only sync (toggle on/off, full re-pull on disable)
- Work Package PDF generation and preview
- Manage Snapshots: delete multiple weeks, revert to single week with/without backup
- Schedule Change Log: log detail grid edits, view/apply to Activities, duplicate handling
- Activity Import: auto-detects Legacy/Milestone format, date/percent cleanup, strict percent conversion
