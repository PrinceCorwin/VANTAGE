# MILESTONE - Project Status

**Last Updated:** January 6, 2026

## Current Status

| Module | Status | Notes |
|--------|--------|-------|
| Progress | READY FOR TESTING | All core features complete |
| Schedule | READY FOR TESTING | All core features complete |
| Sync | COMPLETE | Bidirectional sync working |
| Admin | COMPLETE | User/project/snapshot management |
| Work Package | IN DEVELOPMENT | PDF generation working, Drawings type and template editors pending |

## Feature Backlog

### To Do
- [x] Fix btnFilterLocalDirty filter - active border highlight doesn't clear when Clear Filters is clicked
- [x] Add user defined filters
- [x] Admin able to edit ideas/bug reports (Type/Title/Desc editable by owner or admin; admin can view/restore/permanently delete soft-deleted items; 30-day auto-purge)
- [x] Today filter button - filter to activities where today is within 3WLA dates (loads 3WLA from local DB, filters by in-progress OR should-have-started)
- [x] Are we programmatically maintaining 3WLA table? (Yes - on P6 import: clears stale dates for all projects, deletes empty rows)
- [x] Schedule view yellow conditional formatting not affecting PercentComplete - should trigger if P6 > MS
- [x] Discrepancy filter - filter by Actual Start, Actual Finish, MHs, % Complete where P6 ≠ MS
- [x] Multiple grid layouts - add new, delete, save named layouts
- [x] Review all views and dialogs - add tooltips to controls
- [x] Clean up DarkTheme of unused variables
- [x] Prorate filtered activities BudgetMHs (New Total/Add/Subtract with Keep Percent or Keep Earned options)
- [x] Update Schedule view Discrepancies button to dropdown filter with Actual Start, Actual Finish, MHs, % Complete as selectable items
- [ ] Add "Only My Records" option to SYNC dialog that is default selected which only syncs records owned by the current user and removes records from local DB that are owned by other users after they are uploaded if necessary
- [ ] Progress Book creation
- [~] Work Package creation (in progress - see WorkPackage_Status.md) - Need to add Drawings form type and implement template editors
- [ ] Procore Drawings integration (planned - see Procore_Plan.md)
- [ ] Theme selection by user - save preference in UserSettings and apply on startup
- [ ] Review project files for hard coded colors and replace with theme variables
- [ ] Add Offline Indicator in MainWindow status bar when no internet connection detected-clickable to retry connection
- [ ] Review project file organization and clean up as needed
- [ ] Add 'Revert to Snapshot' menu item in Tools - user selects from list of thier snapshots to revert local data to that snapshot state

### AI Integration
See `AI_Implementation_Items.md` for detailed AI feature planning.

### Procore Integration
See `Procore_Plan.md` for detailed Procore feature planning.

### Help and AI Sidebar
See `Help_AI_Sidebar_Plan.md` for detailed Help and AI sidebar feature planning

### Shelved
- [ ] Find-Replace in Schedule Detail Grid
- [ ] Disable Tooltips setting in Settings dropdown (see `Plans/DisableTooltips_Plan.md`)

## Recently Completed

### January 6, 2026
- Fixed PDF page size in MergeDocuments (was causing right-side cutoff)
- Fixed preview token resolution - body tokens (SchedActNO, PhaseCode, WPName) now resolve from database when work package is selected
- Preview now uses actual UI selections (project, work package, PKG Manager, Scheduler) instead of placeholder data

### January 4, 2026 (Session 2)
- Fixed Work Package UI styling - applied SfSkinManager FluentDark theme to all controls
- Fixed Form Template tab layout - dropdown 30% width, Type to right of dropdown
- Redesigned Generate tab - shortened fields, moved Generate button right of Output
- Converted Add Form dropdown to menu-style button (DropDownButtonAdv like File/Tools)
- Re-enabled PDF preview with Syncfusion PdfViewerControl and proper error handling
- Verified folder browser and Generate button save PDFs correctly
- Planned Procore Drawings integration (see Procore_Plan.md)

### January 4, 2026 (Session 1)
- Created Work Package module - view shell with 60/40 split, tabs, preview panel
- Added FormTemplates/WPTemplates database tables and seeded built-in templates
- Implemented PDF renderers (Cover, List, Form, Grid types) using Syncfusion.Pdf
- Added token resolution system for dynamic content ({WorkPackage}, {ProjectName}, etc.)
- Replaced legacy SHBrowseForFolder P/Invoke with modern IFileDialog COM interface

### January 2, 2026
- Converted Discrepancies button to dropdown filter with 5 options (Clear Filter, Actual Start, Actual Finish, MHs, % Complete); each filters by that specific variance type; mutually exclusive with other filters
- Added Clear Filters button to Schedule view toolbar; clears all toggle filters, discrepancy filter, and column header filters
- Refresh button now also clears all filters when reloading data
- Fixed Refresh button focus styling (no longer retains highlight after click)
- Added Prorate MHs feature (Tools menu → Prorate MHs...): distribute BudgetMHs changes across filtered activities proportionally with New Total/Add/Subtract operations and Keep Percent Complete or Keep Earned MHs preserve modes; includes placeholder detection (<0.01 MHs), minimum constraint (0.001 MHs floor), 100% complete activity handling, input validation

### January 1, 2026
- Cleaned up DarkTheme.xaml - removed 22 unused variables (reduced from 122 to 94 lines)
- Added tooltips to ProgressView.xaml (12 buttons), MainWindow.xaml (10 controls), ScheduleView.xaml (8 controls)
- Renamed btnFilterActualsDiscrepancies to btnFilterDiscrepancies in ScheduleView
- Added multiple grid layouts feature (ManageLayoutsDialog: save up to 5 named layouts, apply/rename/delete, Default button resets to XAML defaults)
- Added user-defined filters feature (ManageFiltersDialog, dropdown button in sidebar, up to 5 conditions with AND/OR logic, saved to UserSettings)
- Fixed LocalDirty filter button (renamed to btnFilterLocalDirty) - now clears highlight when Clear Filters clicked
- Fixed Discrepancy filter to include all 4 variance types (Start, Finish, MHs, PercentComplete)
- Fixed SAVE button hover styling in Schedule view to use theme variables
- Added Today filter button to Progress view (filters by in-progress or 3WLA scheduled for today)
- Added PercentComplete yellow variance highlighting to Schedule view (P6 > MS)
- Enhanced Feedback Board: owner/admin can edit Type/Title/Description, admin can view/restore/permanently delete, 30-day auto-purge, changed "Closed" status to "Rejected"

### December 31, 2025
- Added Settings button (⋮) to toolbar with dropdown menu
- Added Reset Grid Layouts option (clears Progress/Schedule column preferences)
- Moved Feedback Board from Tools menu to Settings menu
- Added Feedback Board - combined Ideas/Bug Reports with Azure sync
- Admin email notifications on new feedback submissions
- Admin-only status management and delete functionality

### December 31, 2024
- Added Export Logs with email option (Tools menu)
- Added Export/Import UserSettings (Tools menu) for PC migration
- Added Ctrl+C/Ctrl+V cell copy/paste to Schedule detail grid
- Cleaned up duplicate and AI instruction comments
- Reorganized documentation (plan files, status file)

### December 30, 2024
- Refactored ScheduleRepository - Azure is source of truth for activity selection
- Removed InMS column usage
- Added Clear Local Activities (Tools menu)
- Added Clear Local Schedule (Tools menu)
- Added Clear Azure Activities (Test menu)
- Added AdminSnapshotsDialog, AdminUsersDialog, AdminProjectsDialog

### December 29, 2024
- Added ProjectID validation against Projects table
- Added Delete My Snapshots tool
- Added detail grid column persistence
- Added email notifications on assignment
- Fixed UpdatedUtcDate format inconsistency
- Added ProgressSnapshots auto-purge
- Added Progress module edit validation

## Known Issues

None currently tracked.

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
- Admin dialogs
- UserSettings export/import with immediate reload
- Log export to file and email with attachment
- User-defined filters create/edit/delete and apply
- Grid layouts save/apply/rename/delete and reset to default
- Prorate MHs with various operation/preserve combinations
- Discrepancy dropdown filter (each type individually, clear filter, clear all filters button)
