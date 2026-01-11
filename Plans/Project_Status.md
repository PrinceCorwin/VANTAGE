# MILESTONE - Project Status

**Last Updated:** January 11, 2026

## Module Status

| Module | Status | Notes |
|--------|--------|-------|
| Progress | READY FOR TESTING | Core features complete |
| Schedule | READY FOR TESTING | Core features complete |
| Sync | COMPLETE | Bidirectional sync working |
| Admin | COMPLETE | User/project/snapshot management |
| Work Package | IN DEVELOPMENT | PDF generation working; Drawings editor and template editors need testing |
| Help Sidebar | IN DEVELOPMENT | Infrastructure complete; content writing in progress |
| AI Features | NOT STARTED | Requires ClaudeApiService infrastructure first |

## Active Development

### Work Package Module
- [ ] Test Cover editor - editing, saving, preview
- [ ] Test List editor - item add/remove/reorder, saving, preview
- [ ] Test Grid editor - column add/remove/reorder, row count, saving, preview
- [ ] Test Form editor - sections/items/columns, saving, preview
- [ ] Test Type selection dialog - creating new templates of each type
- [ ] Implement Drawings editor (folder path, images per page, source selection)

### Help Sidebar
- [ ] Write Getting Started content
- [ ] Write Progress Module content
- [ ] Write Schedule Module content
- [ ] Write Work Packages content
- [ ] Write Progress Books content
- [ ] Write Administration content
- [ ] Write Reference content
- [ ] Capture screenshots (45+ total)
- [ ] Implement PDF export

## Feature Backlog

### High Priority
- [ ] Progress Book creation
- [ ] Theme selection by user - save preference, apply on startup
- [ ] Add Offline Indicator in status bar - clickable to retry connection
- [x] Add 'Revert to Snapshot' functionality (via Manage Snapshots dialog)

### Medium Priority
- [x] Review project files for hard coded colors, replace with theme variables
- [x] Review project file organization and clean up (Phase 1 complete - dialogs moved, files renamed)
- [ ] Shift+Scroll horizontal scrolling (see ShiftScroll_Horizontal_Implementation_Plan.md)
- [ ] User-editable header template for WP (allow customizing header layout)
- [ ] Import/Export WP templates to JSON

### AI Features (see InCode_AI_Plan.md)
| Feature | Status |
|---------|--------|
| ClaudeApiService infrastructure | Not Started |
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

### Procore Integration (see Procore_Plan.md)
- [ ] Procore Drawings integration for WP module

### Shelved
- [ ] Find-Replace in Schedule Detail Grid
- [ ] Disable Tooltips setting (see DisableTooltips_Plan.md)

## Recent Completions

### January 11, 2026
- Work Package Module - Generate tab improvements:
  - Expanded "+ Field" dropdown with all Activity fields
  - Priority fields at top: Area, CompType, PhaseCategory, PhaseCode, SchedActNO, SystemNO, UDF2, WorkPackage
  - Separator line between priority and remaining fields
  - All fields alphabetically sorted within their sections
  - WP Name Pattern now persists (saves on focus lost, restores on view load)
- Work Package Module - Grid editor improvements:
  - Added Edit button (✎) for columns with edit panel (name + width fields)
  - Edit panel with Save/Cancel buttons, Enter/Escape keyboard support
- Work Package Module - Column width prorate fix (Grid and Form editors):
  - Fixed prorate algorithm: edited/added column keeps its input value
  - Other columns scale proportionally to fill remaining space (100 - fixedValue)
  - Prevents negative values and ensures columns always sum to 100%

### January 10, 2026
- Work Package Module - Form (Checklist) editor improvements:
  - Added Edit buttons (✎) for columns, sections, and section items
  - Edit panels with Save/Cancel buttons, Enter/Escape keyboard support
  - Auto-prorate column widths when adding new column (keeps total at 100%)
- Work Package Module - Font Size Adjust slider:
  - Added to Form, Grid, and List (TOC) editors
  - Range: -30% to +50% adjustment
  - Row height/line height scales proportionally with font size
  - Footer text remains at base font size for consistency
- Work Package Module - List (TOC) editor improvements:
  - Added "+ Add Item" dropdown with predefined items (WP Doc Expiration Date, Printed Date, WP Name, Schedule Activity No, Phase Code)
  - Added Blank Line and Line Separator options to dropdown
  - Blank lines display as italic dimmed "blank line" in editor
  - Line separators display as italic dimmed "line separator" and render as horizontal line in PDF
  - Reworked Edit/Add workflow: Edit button shows edit field (in-place update), Add New shows separate add field
  - Both panels hidden by default, appear only when triggered
- Work Package Module - Template management improvements:
  - Created TemplateNameDialog for clone/save-as-new operations with duplicate name validation
  - Clone now saves immediately after naming (no need to click Save)
  - Save on built-in templates prompts for new name via dialog
  - Added delete confirmation dialog for form templates (matches WP templates)
  - Built-in form templates now display in logical order: Cover, TOC, Checklist, Punchlist, Signoff, Drawing Log, Drawings
- Theme resources refactoring:
  - Added 20+ new color resources to DarkTheme.xaml (action buttons, overlays, errors, warnings, UI elements)
  - Created ThemeHelper utility class for code-behind theme resource access
  - Updated 15 XAML files to use theme resources instead of hard-coded hex colors
  - Updated 4 code-behind files to use ThemeHelper (MainWindow, AdminSnapshotsDialog, ProgressView, P6ImportDialog)
  - Widened DeletedRecordsView sidebar from 170px to 190px for header text
- Bulk percent update performance optimization:
  - Added BulkUpdatePercentAsync to ActivityRepository for single-transaction batch updates
  - Batches updates in groups of 500 to avoid SQLite's 999 parameter limit
  - Replaced per-record database updates with bulk operation (40k records now updates in seconds vs minutes)
  - Added chunked enumeration for large selections (>5000 records) with Task.Delay yields to keep UI responsive
  - Filters selected records to user's records only without freezing UI
- UserSettings refactored to remove UserID:
  - Removed UserID column from UserSettings table (single user per machine)
  - Simplified all SettingsManager methods to not require userId parameter
  - Removed App.CurrentUserID usage from all settings calls
  - Updated ThemeManager to use parameter-less signatures
- Settings export now excludes LastSyncUtcDate:
  - Ensures full sync on new machines when importing settings
  - New machine gets fresh sync instead of potentially stale timestamp
- UI improvements:
  - Moved Import/Export Settings from File menu to Settings popup (hamburger menu)
  - Settings grouped between Feedback Board and About MILESTONE
  - Replaced WPF ProgressBar with Syncfusion SfLinearProgressBar (indeterminate marquee animation) in ProgressView, MainWindow overlay, and SyncDialog overlay
  - Custom Percent Buttons now show full blocking overlay during bulk updates
  - Fixed MainWindow reference issue (use Application.Current.Windows.OfType instead of Application.Current.MainWindow which returns VS design adorner)

### January 9, 2026
- 3 Decimal Place Precision enforcement:
  - Created NumericHelper.RoundToPlaces() centralized rounding utility
  - Excel import: rounds all double values on read
  - Model setters: rounds BudgetMHs, Quantity, EarnQtyEntry, PercentEntry
  - Grid edit: auto-rounds on cell exit before saving
  - Excel export: rounds all double values on write
  - Database save: defensive rounding in ActivityRepository
  - Future: admin-configurable decimal places (low priority)

- Revert to Snapshot feature:
  - Renamed DeleteSnapshotsDialog to ManageSnapshotsDialog
  - Added "Revert To Selected" button (enabled for single selection only)
  - Warning dialog with backup option before reverting
  - Backup creates snapshot with today's date
  - Pre-sync ensures pending changes are saved before revert
  - Ownership validation skips records now owned by others
  - Created SkippedRecordsDialog to show records that couldn't be restored
  - Restores all fields except UniqueID, AssignedTo, and calculated fields
  - Records marked LocalDirty=1 for user-controlled sync

### January 8, 2026
- File organization cleanup:
  - Moved 5 dialogs to Dialogs/ folder with namespace updates
  - Renamed 8 files to PascalCase (preserved git history)
  - Moved ScheduleProjectMapping from Utilities to Models
  - Created WorkPackageViewModel (partial extraction)
  - Extracted UserItem, ProjectItem to Models/
  - Extracted ColumnDisplayConverter to Converters/
- Reorganized MainWindow menus: File menu grouped with separators, removed Reports/Analysis menus, cleaned up placeholders
- Moved Help/AI Sidebar to Tools menu, About to hamburger menu
- Added separators to Admin and Tools menus
- Consolidated status docs into single Project_Status.md

### January 7-8, 2026
- Added "My Records Only" checkbox to SYNC dialog
- Help Sidebar infrastructure complete (WebView2, IHelpAware, context-aware navigation, F1 shortcut)

### January 6-7, 2026
- Work Package PDF generation working with proper page sizing
- Form template editors implemented (Cover, List, Grid, Form types)
- Preview uses actual UI selections for token resolution

## Known Issues

1. **Drawings editor not implemented** - Cannot configure Drawings templates yet (use default or clone)
2. **Drawings PDF source not supported** - PDF files are skipped (image formats only for now)

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
