# MILESTONE - Project Status

**Last Updated:** January 16, 2026

## Module Status

| Module | Status | Notes |
|--------|--------|-------|
| Progress | READY FOR TESTING | Core features complete |
| Schedule | READY FOR TESTING | Core features complete |
| Sync | COMPLETE | Bidirectional sync working |
| Admin | COMPLETE | User/project/snapshot management |
| Work Package | IN DEVELOPMENT | PDF generation working; Drawings editor and template editors need testing |
| Help Sidebar | IN DEVELOPMENT | Infrastructure complete; search implemented; content writing in progress |
| AI Features | NOT STARTED | Requires ClaudeApiService infrastructure first |

## Active Development

### Work Package Module
- [ ] Test Cover editor - editing, saving, preview
- [ ] Test List editor - item add/remove/reorder, saving, preview
- [ ] Test Grid editor - column add/remove/reorder, row count, saving, preview
- [ ] Test Form editor - sections/items/columns, saving, preview
- [ ] Test Type selection dialog - creating new templates of each type
- [x] Change Punchlist template default header font size to -30% (current default too large)
- [x] Add Reset Defaults button to form template editors (resets all fields/settings to built-in template values)
- [x] Implement Drawings section in Generate tab (Local folder fetch working)
- [ ] Drawings - Fix preview display
- [ ] Drawings - Fix layout/orientation for 11x17 drawings
- [ ] Drawings - Implement Procore fetch
- [ ] **DISCUSS:** Drawings fetch architecture - consider AI-assisted matching (many factors: DwgNO formats, revisions, sheet numbers, naming conventions). May warrant separate Drawings Manager module/dialog where drawings are fetched/organized independently, then WP module simply pulls from that cache.

### Help Sidebar
- [x] Implement search functionality (WebView2 Find API with highlight and navigation)
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
- [x] User Access Request - add "Request Access" button at startup for unknown users to email admins (see User_Access_Request_Plan.md)
- [x] Add 'Revert to Snapshot' functionality (via Manage Snapshots dialog)

### Medium Priority
- [x] Review project files for hard coded colors, replace with theme variables
- [x] Review project file organization and clean up (Phase 1 complete - dialogs moved, files renamed)
- [ ] Rename UDF18 column to RespParty throughout the app (grid headers, code references, database)
- [ ] Schedule module: Check if user can apply detail grid edits to live activities - explore adding this option if not available
- [ ] Shift+Scroll horizontal scrolling (see ShiftScroll_Horizontal_Implementation_Plan.md)
- [ ] User-editable header template for WP (allow customizing header layout)
- [ ] Import/Export WP templates to JSON
- [x] Milestone/Legacy Import/Export formats - default to Milestone column names, add Legacy menu items for backward compatibility

### Infrastructure / Azure Migration
- [ ] Execute VMS_ table creation script on company Azure (see MILESTONE_Azure_Migration_Plan.md)
- [ ] Legacy Azure Table Save - Admin dialog to upload project snapshots to company Azure dbo_VANTAGE_global_ProgressLog (UPSERT, schema mapping TBD)

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
- [ ] Interactive Help Mode - click UI controls to navigate to documentation (see Sidebar_Help_Plan.md)

## Recent Completions

### January 16, 2026
- Legacy Import/Export format support:
  - Added ExportFormat enum (Legacy, NewVantage) to ExcelExporter
  - Default format is now NewVantage (property names as headers, percentages as 0-100)
  - Legacy format uses OldVantage column names and 0-1 percentage decimals
  - Added Legacy menu items to File menu (Import Replace, Import Combine, Export, Template)
  - Toggle Legacy I/O Menu button in Settings popup (saves visibility state to UserSettings)
  - Legacy items hidden by default, appear at bottom of File menu when enabled
  - Updated ExcelImporter with format-aware column mapping and percentage conversion
  - File names include "_Legacy" suffix for Legacy format exports
- User Access Request feature:
  - Created AccessRequestDialog with username (read-only), full name, and email fields
  - Added GetAdminEmailsAsync to AzureDbManager (joins Admins with Users to get emails)
  - Added SendAccessRequestEmailAsync to EmailService (styled HTML email to all admins)
  - Modified App.xaml.cs access denied flow to offer "Request Access" option
  - Handles offline scenario with appropriate message
- Form Template Editor - Column delete prorate fix:
  - Grid and Form editors now prorate remaining column widths to 100% after column deletion
  - Matches existing behavior for add/edit operations
  - Affects Checklist, Punchlist, Signoff, Drawing Log templates
- Form Template Editor - Reset Defaults button:
  - Added Reset Defaults button for user-created templates (Cover, List, Grid, Form types)
  - Button hidden for built-in templates and Drawings placeholder
  - Resets template StructureJson to match a selected built-in template of same type
  - For types with multiple built-ins (Grid, Form), shows ResetTemplateDialog with ComboBox
  - For types with single built-in (Cover, List), resets directly with confirmation
  - Added GetBuiltInFormTemplatesByTypeAsync to TemplateRepository
  - Created Dialogs/ResetTemplateDialog.xaml for built-in selection
- Azure Migration Plan updated:
  - Marked Phase 2 (C# code changes) and Phase 3 (testing) as DEFERRED
  - Added Rollback Strategy section
  - Added Future Feature: Legacy Azure Table Save (admin uploads snapshots to dbo_VANTAGE_global_ProgressLog)
- Work Package PDF - Dynamic footer height:
  - Footer now auto-sizes based on content (was hardcoded 25pt, too small for long text)
  - Added MeasureFooterHeight() and GetFooterReservedHeight() to BaseRenderer
  - All renderers (Form, Grid, Cover, List) now use dynamic footer measurement
  - Page break logic accounts for actual footer size
  - Fixes Signoff Sheet footer text being cut off
- Work Package PDF - Punchlist base font size:
  - Added BaseHeaderFontSize property to GridStructure (default 9pt)
  - Punchlist template uses 6.3pt base (30% smaller) to fit many columns
  - Slider adjustment still works on top of reduced base size
  - Grid editor preserves BaseHeaderFontSize on save/load
- Form template names updated: Removed "WORK PACKAGE" prefix from all default templates (now just "Cover Sheet", "Punchlist", etc.)
- Help Sidebar - Search functionality:
  - Added search field below context header with ˄/˅ navigation buttons and match counter
  - Uses WebView2 Find API (CoreWebView2.Find) with SuppressDefaultFindDialog
  - Highlights all matches in yellow, scrolls first match into view
  - 300ms debounce on search input
  - Enter/Shift+Enter keyboard shortcuts for next/previous
  - Search clears when navigating to different module
  - Match counter shows "3 of 12" format
- Help Manual - Content writing (manual.html):
  - Restructured to 8 sections (added Main Interface as Section 2)
  - Rewrote Getting Started: What is MILESTONE, Before You Begin (admin setup, no login, first sync)
  - Wrote Main Interface: Layout, Navigation, Menus (all items listed), Status Bar, Help Sidebar, Shortcuts
  - Wrote comprehensive Progress Module (10 subsections): workflow, toolbar, filters, editing, metadata, sync
  - Wrote comprehensive Schedule Module (13 subsections): 3WLA, missed reasons, P6 import/export, discrepancies
  - Added nested TOC with anchor links to all subsections
  - Styled TOC (larger main sections, indented subsections)
  - Added screenshot placeholders throughout
- Documentation cleanup:
  - Merged Interactive_Help_Mode_Plan.md into Sidebar_Help_Plan.md
  - Deleted Interactive_Help_Mode_Plan.md
- Help Sidebar - UI simplification:
  - Removed anchor-based navigation (always opens to beginning now)
  - Replaced context header with action buttons row: Back to Top | Print PDF | View in Browser
  - Buttons styled as clickable text (accent color, no borders)
  - Removed IHelpAware interface and all implementations (no longer needed)
  - Deleted Interfaces/IHelpAware.cs and empty Interfaces folder

### January 11, 2026
- Work Package Module - Drawings integration (Phase 1):
  - Added Drawings section to Generate tab with Local Folder / Procore source selector
  - DwgNO grid auto-populates from selected work packages (queries distinct DwgNO values)
  - Local folder fetch with smart matching: full DwgNO → fallback to last two segments (e.g., "017004-01")
  - Copies all matching revisions of each drawing
  - Captures "Not in DB" files (PDFs in folder not matching any DwgNO)
  - Renamed "Drawings - Template" to "Drawings - Placeholder" (marks position in WP)
  - Hidden Drawings from Form Templates edit dropdown (configured via Generate tab now)
  - DrawingsRenderer merges fetched PDFs into work package at placeholder position
  - Fixed "Cannot access closed file" error (keep loaded PDFs alive until final save)
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

1. **Drawings preview not displaying** - Fetched drawings show in generated PDF but not in previewer
2. **Drawings layout/orientation** - 11x17 drawings may need rotation or scaling adjustment

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
