# MILESTONE - Project Status

**Last Updated:** January 10, 2026

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
- [ ] Review project files for hard coded colors, replace with theme variables
- [x] Review project file organization and clean up (Phase 1 complete - dialogs moved, files renamed)
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

### January 10, 2026
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
