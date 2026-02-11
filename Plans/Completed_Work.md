# VANTAGE: Milestone - Completed Work

This document tracks completed features and fixes. Items are moved here from Project_Status.md after user confirmation.

---

## Unreleased

### February 11, 2026
- **Filter and grid UX fixes:** (1) ProgressView metadata errors button now activates Clear Filters green border. (2) ScheduleView Clear Filters button now properly clears column header filters (was only clearing view-level filter). (3) ProgressView Delete/Backspace keys now clear cell value when cell is selected but not in edit mode (arrow key navigation). (4) ProgressView Find/Replace now updates metadata error counter immediately after replacements.
- **ActStart/ActFin required metadata validation:** Removed auto-set behavior for ActStart (SchStart) and ActFin (SchFinish) fields. Instead of auto-populating dates when percent changes, these fields are now conditionally required metadata: ActStart is required when % > 0, ActFin is required when % = 100. Missing dates show red cell backgrounds (same as other metadata errors) and block sync/submit operations. Auto-clear behavior preserved: dates clear when percent returns to 0. Fixed metadata error counter not updating after cell edits. Fixed grid flash when clicking Metadata Errors button (was caused by double reload). Updated Activity/ProgressSnapshot models with HasMissingSchStart/HasMissingSchFinish properties. Updated ExcelImporter, ProgressView, ScheduleView, and Help manual.
- **Rename date column headers for clarity:** Changed Schedule master grid columns: MS Start/Finish → V-Start/V-Finish, P6 Start/Finish → P6 Plan Strt/P6 Plan Fin. Changed Schedule detail grid columns: Start/Finish → ActStart/ActFin. Changed Progress grid columns: SchStart/SchFinish → ActStart/ActFin. Updated Help manual references.

### February 10, 2026
- **Persist MissedReasons and 3WLA dates across P6 imports:** MissedStartReason, MissedFinishReason, ThreeWeekStart, and ThreeWeekFinish values now persist across P6 imports. Stored in ThreeWeekLookahead table with comparison dates (P6_Start, P6_Finish, MS_Start, MS_Finish) to detect when underlying dates change. MissedReasons only apply if stored P6/MS dates still match current values (date changes invalidate old reasons). 3WLA dates only apply if no actual MS date exists and the 3WLA date is not stale (>= WeekEndDate). Added database migration for existing installations.
- **7-day rule for MissedReasons:** Don't require MissedStartReason/MissedFinishReason for activities that actualized more than 7 days before the WeekEndDate. This prevents legacy data from triggering required field warnings for long-completed activities.
- **Import From P6 project list from snapshots:** Changed P6 Import dialog project list from local Activities table to Azure VMS_ProgressSnapshots query. Dialog now opens immediately with "Loading projects from snapshots..." indicator while async query runs. Shows "No submitted snapshots found" message if none exist. Added footer hint: "If desired project is not visible, cancel and Submit Week in Progress module first."
- **Snapshot submit skip conflicts and export:** Snapshot submit now skips conflicting records instead of aborting. Uses NOT EXISTS clause to safely skip any existing snapshots. Queries for pre-existing snapshots before insert to capture skipped record details. When records are skipped, offers to export them to Excel with UniqueID, SchedActNO, Description (including who submitted and under which project), and WeekEndDate columns.
- **Find/Replace dialog enhancements:** Added three modes for replacing values: (1) normal text find/replace, (2) "Replace BLANK cells in column" checkbox to target empty/null/whitespace cells, (3) "Replace ALL cells in column" checkbox to replace every cell value. Checkboxes are mutually exclusive and disable the Find textbox when checked. Added "Count" button to show match counts without replacing. Replace ALL shows confirmation dialog before proceeding.
- **Fix ProgressView not refreshing after Clear Local Activities from other modules:** When user cleared local activities while on a different module (Schedule, Work Package, etc.), the cached ProgressView still showed stale records upon navigation. Fixed by invalidating `_cachedProgressView` in the else branch of `MenuClearLocalActivities_Click` so next navigation creates a fresh view.

### February 9, 2026
- **ManageProgressLogDialog bug fixes:** Fixed checkbox selection not working (added `AllowEditing="True"` to SfDataGrid, `AllowEditing="False"` to text columns). Fixed delete operation returning 0 records by using tolerance-based datetime comparison (2-second tolerance for timestamp, date-only comparison for week ending). Increased ConfirmDeleteDialog height from 220 to 260 to prevent text overlap with input field.
- **User tutorial outline:** Created `Plans/Tutorial_Outline.txt` with structured ~90-minute Teams meeting outline covering all modules (Progress, Schedule, Progress Books, Work Packages), grid operations, syncing, snapshots, Tools/Settings menus, and keyboard shortcuts. Tailored for OldVantage migrators.

### February 6, 2026
- **ManageProgressLogDialog enhancements:** Replaced ListView with SfDataGrid for filter/sort capabilities on all columns. Added REFRESH dropdown button (7 days / 30 days / All time) to scan `VANTAGE_global_ProgressLog` and import legacy upload batches not yet tracked. Added ConfirmDeleteDialog requiring user to type "DELETE" before batch deletion proceeds. Confirmation dialogs added before REFRESH operations. Fixed datetime format mismatches in duplicate detection (uses 2-second tolerance). Unlimited query timeout for large table scans.
- **Schedule cell indicator interface:** Created `IScheduleCellIndicators` interface to document the contract for cell styling properties (`IsMissedStartReasonRequired`, `HasStartVariance`, etc.). Both `ScheduleMasterRow` and `ScheduleViewModel` implement it — ViewModel returns false for all (handles Syncfusion cell recycling), Row returns computed values. Added `BasedOn` to 8 cell styles matching ProgressView pattern. Tested MultiBinding approach but it breaks PropertyChanged reactivity.
- **Project plan doc updates:** Updated `Milestone_Project_plan.md` with current modules (Analysis, Progress Books, Help Sidebar, Theme System), publishing/credentials architecture, and Schedule module features. Renamed to "VANTAGE: Milestone".
- **Completed_Work.md versioning:** Added version markers to track changes since last published release. Unreleased section for pending changes, version headers (e.g., `## v26.1.7`) mark release boundaries.

---

## v26.1.7

### February 4, 2026
- **Help manual — Analysis module documentation:** Added section 5 for Analysis module with overview, layout description, and summary grid documentation. Renumbered subsequent sections (Progress Books→6, Work Packages→7, Administration→8, Reference→9).
- **ANALYSIS module — summaryGrid styling:** Added theme-aware header styling (GridHeaderBackground, GridHeaderForeground) and custom filter icon template matching ProgressView (FilterIconColor/FilterIconActiveColor). Grid splitter positions now save immediately on drag via DragCompleted event.
- **ANALYSIS module — independent row splitters and UI polish:** Restructured the 4×2 grid so each row has independent column splitters (dragging a vertical splitter in the top row no longer affects the bottom row). Updated GridSplitter styles to modern thin look with centered grip dots. Removed section borders for cleaner appearance. Reduced Projects combobox width by 40%.
- **ANALYSIS module — initial implementation:** Added new ANALYSIS nav button to the right of WORK PKGS with 4×2 resizable grid layout using GridSplitters. Section (2,2) contains an aggregated metrics grid with:
  - Group By dropdown: priority fields (AssignedTo, CompType, DwgNO, PhaseCategory, PhaseCode, PjtSystem, SchedActNO, Service, SubArea, WorkPackage) first, then all other text fields alphabetically
  - Current User / All Users radio buttons
  - Projects multi-select dropdown (from local Activities data)
  - SfDataGrid with columns: GroupValue (dynamic header), BudgetMHs, EarnedMHs, Quantity, QtyEarned, % Complete
  - Conditional cell coloring on % Complete: red (0-25%), orange (>25-50%), yellow (>50-75%), green (>75-100%) using theme-aware color resources (AnalysisRedBg/OrangeBg/YellowBg/GreenBg)
  - Grid columns are sortable, filterable, resizable, and reorderable
  - All settings persist to UserSettings: group field, user filter, selected projects, GridSplitter positions
- **Remove Toggle Legacy I/O Menu setting:** Removed the settings menu button that toggled visibility of Legacy Export items. Legacy Export Activities and Legacy Export Template menu items are now always visible in the File menu. Cleaned up associated code from MainWindow, SettingsManager, and help manual.
- **Rename LocalDirty filter button to "Unsynced":** Changed the filter button text from "LocalDirty" (internal field name) to "Unsynced" (user-friendly). Updated help manual filter table and AI Progress Scan workflow documentation.
- **Other users' records text color — disabled grey:** Changed `NotOwnedRowForeground` color in Light and Orchid themes from bluish/purple (#4A6D8C, #7B5FA0) to standard disabled grey (#888888). Makes other users' read-only records more visually distinct from editable records.
- **Schedule Change Log clears on P6 import:** Added `ScheduleChangeLogger.ClearAll()` method and call it when importing a P6 schedule. Prevents stale change log entries from referencing activities that no longer exist after a new schedule import. Updated help manual.

---

### February 3, 2026
- **P6 import: flexible column header matching:** Added support for secondary headers (row 2 display names like "Activity ID", "Start", "Finish") with fallback to primary headers (row 1 technical field names). Secondary headers are normalized by stripping `(*)` prefix and unit suffixes like `(h)`, `(%)`, `(d)`. Also added support for both `start_date`/`end_date` and `target_start_date`/`target_end_date` column name variants in primary headers. Fixes import failures when P6 exports use different column naming conventions.

---

### February 2, 2026 (Session 4)
- **Fix Required Fields count not updating after past 3WLA date rejection:** When a 3WLA date was rejected (earlier than WeekEndDate) and reverted to null, the Required Fields badge didn't update to reflect the newly blank field. Added `UpdateRequiredFieldsCount()` call after reverting the date.

### February 2, 2026 (Session 3)
- **Fix Schedule save only persisting filtered rows:** `btnSave_Click` and `SaveChangesAsync` were passing `_viewModel.MasterRows` (filtered collection) to `SaveAllScheduleRowsAsync`. When any toggle filter was active (Required Fields, Missed Start, etc.), only visible rows were saved — edits to MissedFinishReason, ThreeWeekStart, ThreeWeekFinish on non-visible rows were lost on restart. Changed both save paths to use `_viewModel.GetAllMasterRows()` (unfiltered list). Also updated empty-check guards to use the unfiltered list so saving isn't blocked when a filter shows zero rows.
- **Add P6_% and %_Mismatch columns to 3WLA Excel report:** Added P6_% column (from `P6_PercentComplete`) to the right of MS_% and %_Mismatch column (True/False, red highlight when True) to the right of P6_%. Mismatch threshold is >0.5 difference. Both new columns use the same `#FCD5B4` light orange header fill as MS_%. All subsequent column indices shifted by +2 (20→22 columns) across master rows, P6 Not In MS rows, and MS Not In P6 rows.
- **Block past 3WLA dates:** Added validation in Schedule master grid that rejects ThreeWeekStart/ThreeWeekFinish dates earlier than the WeekEndDate. Shows MessageBox explaining that past dates should be actualized via detail grid edits instead. Updated Help manual with Date Validation section.
- **Update README.md:** Full rewrite reflecting current project state. Added Progress Books, AI Progress Scan, Help Sidebar, Multi-Theme sections. Updated module statuses, tech stack, project structure, Getting Started. Renamed references from "MILESTONE" to "VANTAGE: Milestone", legacy system to "OldVantage", company to "Summit Industrial".
- **Add naming conventions to CLAUDE.md:** Official name "VANTAGE: Milestone", casual refs (Vantage, VMS, newVantage), legacy = OldVantage. Instruction to not use just "Milestone" in new code/UI/docs.

### February 2, 2026 (Session 2)
- **ManageProgressLogDialog — remove RespParty granularity:** Rolled up upload batches from per-RespParty rows to per-(Username, ProjectID, WeekEndDate, UploadUtcDate) groups. Query now uses GROUP BY with SUM(RecordCount). Delete removes all RespParty records for a batch at once instead of individually. Removed RespParty column from dialog grid, UploadID and RespParty from model class. Rationale: snapshots are always created as a unit across all RespParty values, so there's no use case for deleting only one RespParty's records.
- **Summit Constructors → Summit Industrial:** Renamed company name in About dialog (MainWindow.xaml.cs), CLAUDE.md, and plan docs. Historical changelog entries in Completed_Work.md left as-is.
- **Column visibility checkbox foreground fix:** Changed checkbox text in BtnColumnVisibility_Click (ProgressView) from hardcoded `Brushes.White` to `FindResource("ForegroundColor")`. Fixes invisible text on Light and Orchid themes.
- **About dialog renamed:** Menu item and dialog changed from "About MILESTONE" to "About Vantage: Milestone". Dialog body header also updated.
- **Schedule module — replace P6 baseline dates with current schedule dates:** Renamed `P6_PlannedStart`/`P6_PlannedFinish` to `P6_Start`/`P6_Finish` across 9 source files. Changed P6 import mapping from `target_start_date`/`target_end_date` (baseline) to `start_date`/`end_date` (current schedule). The 3WLA requirement logic and missed start/finish reason logic now evaluate against the current P6 schedule dates instead of stale baseline dates. Grid columns renamed from "P6 Planned Start/Finish" to "P6 Start/Finish". Updated DB schema (Schedule table), models, repository, view model, report exporter, importer, XAML, and help manual. **NOTE:** Local SQLite DB must be deleted and P6 data re-imported after this change (Schedule table schema changed).

### February 2, 2026 (Session 1)
- **Snapshot save — immediate spinner:** Moved BusyDialog creation to right after WE date selection (before Azure validation queries). Shows status updates through each phase: "Validating...", "Checking ownership...", "Checking for existing snapshots...". Hides/shows around MessageBox prompts.
- **Snapshot save — date auto-fix:** Changed future date validation from hard block to Yes/No dialog offering to set offending SchStart/SchFinish dates to the selected WE date. Fixes Monday import → Sunday snapshot scenario.
- **Snapshot save — conditional NOT EXISTS:** Optimized INSERT query to only include NOT EXISTS subquery when other users have conflicting snapshots (`skipped > 0`). Eliminates per-row existence check for the common case.
- **Snapshot save — sync optimization:** Replaced unconditional push+pull with LocalDirty count check — only pushes if dirty records exist. Removed post-snapshot pull and final push (LocalDirty=1 will sync on next regular push).
- **Snapshot save — elapsed timer:** Added Stopwatch showing total elapsed seconds in completion message. Simplified completion message with N0 number formatting.
- **Snapshot timeout fixes:** Added `CommandTimeout = 120` to load queries in AdminSnapshotsDialog and ManageSnapshotsDialog. Added `CommandTimeout = 0` (unlimited) to all delete commands in both dialogs.
- **ManageSnapshotsDialog — group by project/date/submission:** Changed snapshot grouping from WeekEndDate-only to ProjectID + WeekEndDate + ProgDate. Display shows "ProjectID | WE date | Submitted datetime". Delete and revert operations filter by all three fields. Widened dialog from 500×450 to 600×500.
- **ManageSnapshotsDialog — delete spinner:** Added BusyDialog to delete operation showing "Deleting snapshots..." while Azure DELETE executes.
- **Snapshot timeout fix:** Added `CommandTimeout = 0` (unlimited) to all snapshot SQL commands — insert, delete, and purge in ProgressView, and backup insert in ManageSnapshotsDialog. Matches existing pattern in AdminSnapshotsDialog. Fixes "Execution Timeout Expired" errors on large snapshot submissions.
- **Remove grid column summaries:** Removed Syncfusion `TableSummaryRow` from Progress grid (too slow on large datasets). Deleted `GridSummaryHelper.cs` and `PRD_TableSummaryRow.md`. DIY summary panel in toolbar provides the same information.
- **Optimize DIY summary panel:** Cached `PropertyInfo` lookup in ProgressViewModel (resolved once on column change, not per-calculation). Replaced reflection-based record extraction in `UpdateSummaryPanel` with direct `RecordEntry` casting. Added 200ms debounce timer for filter-triggered summary updates; cell edits and initial load remain immediate.
- **Cache Progress view for instant navigation:** `MainWindow.LoadProgressModule()` now caches the ProgressView instance and reuses it on subsequent navigations instead of recreating the view and reloading all data from SQLite. First load unchanged; every navigation after is instant. Force-reloads on Excel import and Reset Grid Layouts. Added `_dataLoaded` guard in `OnViewLoaded` to prevent redundant data loads.
- **Optimize Find & Replace:** Replaced per-record database writes with single-transaction batch update. Added `BulkUpdateColumnAsync` to ActivityRepository — opens one connection, prepares one UPDATE statement, executes per record within a single transaction (only changed column + metadata, not all 88 columns). Added BusyDialog spinner during operation. Handles derived field recalculation for progress columns.
- **Remove ButtonAdv default icons:** Set `SmallIcon="{x:Null}"` on ButtonAdv controls in FindReplaceDialog and DeletedRecordsView to remove Syncfusion's default icon. Normalized Refresh button in DeletedRecordsView from `SizeMode="Large"` to `Normal` to match other buttons.

---

### February 1, 2026
- **Remove test menu (v26.1.3):** Removed TEST button and all 7 menu items from MainWindow toolbar (Toggle Admin, Reset LocalDirty, Toggle UpdatedBy, Schedule Diagnostics, Clear Azure Activities, Test Procore Drawings, Test AWS Textract). Removed test-only `ResetAllLocalDirtyAsync` and `SetAllUpdatedByAsync` from ActivityRepository. Kept Procore services and ScheduleDiagnostic as feature/utility code.
- **Connection error IP display:** Enhanced ConnectionRetryDialog to detect and display user's public IP address when Azure connection fails. Uses api.ipify.org with 5-second timeout. COPY button puts IP on clipboard so user can email admin for firewall whitelisting. Graceful fallback ("Could not detect") when no internet.
- **Help Troubleshooting section:** Populated the placeholder with three entries — Azure firewall/IP whitelisting, SmartScreen unblocking, and post-update appsettings.enc check.
- **SidePanelViewModel cleanup:** Made `_helpHtmlPath` readonly, `ContentColumnWidth` static, removed dead PropertyChanged notification.
- **Publishing Guide:** Created `Plans/Publishing_Guide.md` — step-by-step reference for publishing updates and building the installer.
- **Packaging Workstream 4 complete:** Full publish and auto-update cycle validated. v26.1.1 installed via `VANTAGE-Setup.exe`, v26.1.2 auto-update detected and applied on next launch. All 4 workstreams now complete. See `Plans/Publishing_Guide.md` for release workflow reference.
- **First publish (Workstream 4 step 1):** Ran `publish-update.ps1 -Version "26.1.1"` — produced 142 MB self-contained ZIP. Fixed two bugs discovered during publish: (1) `publish-update.ps1` used `Set-Content` which stripped UTF-8 BOM and corrupted `©` in csproj — switched to `[System.IO.File]::WriteAllText()` with explicit UTF-8 BOM encoding. (2) Updater csproj had `SelfContained=true` in Release condition causing NETSDK1151 error when main app references updater — moved self-contained flags to be command-line-only via publish script.
- **Installer app (Workstream 3):** Created `VANTAGE.Installer` WPF project — branded dark window with Summit logo, "VANTAGE: Milestone" install button, disabled "REQit" button. Downloads manifest from update URL, downloads ZIP with progress, verifies SHA-256, extracts to `%LOCALAPPDATA%\VANTAGE\App\`, creates desktop shortcut via COM Shell. Publishes as self-contained single-file exe. Added `publish-installer.ps1` script.
- **Self-contained publish config (Workstream 2):** Updated `VANTAGE.Updater.csproj` for self-contained single-file publish. Changed `publish-update.ps1` to use `--self-contained true` for both main app and updater. Users won't need .NET runtime installed.
- **Credential migration (Workstream 1):** Replaced compiled `Credentials.cs` with runtime-loaded config system. New `CredentialService.cs` reads plaintext `appsettings.json` in dev or AES-256 encrypted `appsettings.enc` in production. Created `AppConfig.cs` model, migrated 31 references across 7 files, added encryption function to `publish-update.ps1` (now 8 steps), updated `.gitignore` and csproj. Full plan in `Packaging_Credentials_Installer_Plan.md`.
- **Orchid theme:** Added third theme — a light-based feminine theme with deep purple (#7B1FA2) primary and bright pink (#E040FB) accent. Lavender-tinted backgrounds, purple grid headers, and purple-branded toolbar. Uses Syncfusion FluentLight as base.
- **ActiveFilterBorderColor resource:** New theme-aware resource for Clear Filters button border highlight. Green in Dark/Light themes, pink accent in Orchid. Replaces hardcoded StatusGreen reference.
- **Schedule view Clear Filters border:** Ported filter-active border indicator from ProgressView to ScheduleView. Highlights when any filter is active (toggle buttons, discrepancy dropdown, column header filters). Wired via PropertyChanged and FilterChanged events.
- **Multi-theme support:** Added Theme Manager dialog (Settings > Theme...) with Dark and Light themes. Theme applies on app restart. Includes Syncfusion FluentDark/FluentLight switching, per-theme resource dictionaries, and UserSettings persistence.
- **Light theme polish:** Toolbar, grid headers, Sync/Submit buttons, and Required Fields button keep dark backgrounds in both themes with correct foreground colors. Hover states adjusted for light theme visibility.
- **Theme resource architecture:** Renamed resources to universal, role-based names (`ToolbarForeground`, `GridHeaderForeground`, `ActionButtonForeground`, `ToolbarButtonStyle`) to support future themes beyond Dark/Light. Each theme independently defines colors for its palette.
- **Fixed theme persistence bug:** `InitializeDefaultUserSettings()` was resetting Theme to "Dark" on every startup because its guard checked a setting that was never written. Fixed to check Theme setting directly.
- **Custom grid filter icons:** Replaced Syncfusion's built-in FilterToggleButton template with custom stroke-based funnel icons. Syncfusion's internal filter icon colors are resolved from compiled BAML and cannot be overridden via any resource dictionary approach. Custom template uses theme-aware `FilterIconColor`/`FilterIconActiveColor` Color resources, outline-only rendering, and VisualStateManager for filtered/unfiltered state transitions.
- **Required Fields button hover fix:** Created `ActionFilterToggleStyle` so the button's light foreground switches to dark on hover (WPF local values override style triggers, requiring a dedicated style).

---

### January 31, 2026
- **Import/Export WP templates:** Export all user-created form and WP templates to JSON with index-based form references. Import with automatic ID remapping, name conflict handling (" (Imported)" suffix), and UI refresh. Documented in Help manual.
- **Updated app icon and logo**
- **Clear Filters button:** Green border now only appears when filters are active (covers all 9 filter types: sidebar buttons, column headers, global search, Today, user-defined, scan results). Border resets to default after clearing filters.
- **Help manual updates:**
  - Renamed all standalone "MILESTONE" references to "VANTAGE: Milestone" (22 instances, kept format/column-naming labels as-is)
  - Changed "Summit Constructors" to "Summit Industrial"
  - Rewrote app description: "developed by" instead of "built for", added feature highlights (rules of credit, unit rates, 3WLA, AI scan, P6 integration)
  - Rewrote Progress Books editor section with thorough documentation of all controls (layout, paper size, font size, filter, grouping, columns, sort, preview)
  - Added Form Templates tab overview with initial empty state screenshot placeholder, type selection dialog placeholder, and built-in templates table
  - Added screenshot placeholders throughout: pb-editor (top/bottom), pb-preview, pb-scan-dialog, pb-scan-results, wp-form-templates-tab, wp-form-type-dialog, wp-form-cover, wp-form-list, wp-form-form (top/bottom), wp-form-grid (top/bottom), wp-preview-panel
  - Improved Work Packages section: expanded Generate Tab (+ Field dropdown, Select All/Clear, settings persistence), WP Templates (+ Add Form clarification), Form Templates (fixed Reset Defaults description, expanded List/Form/Grid editor docs), Token System (added Phase Code to predefined items), Previewing (context label, placeholder behavior, all-tabs availability)
  - Added WP Name Pattern example with mixed text and tokens
  - Changed "default Summit logo" to "default company logo" in WP overview
- **Token bug fix in WorkPackageView.xaml.cs:**
  - Fixed `{PrintDate}` → `{PrintedDate}` in predefined TOC items (didn't match TokenResolver)
  - Fixed `{ScheduleActivityNo}` → `{SchedActNO}` in predefined TOC items (didn't match TokenResolver)
- **Progress grid horizontal tilt wheel fix:**
  - Added native WM_MOUSEHWHEEL hook to ProgressView (matching existing ScheduleView implementation)
  - Side/tilt scroll wheel now scrolls ProgressView grid in correct direction (down = right)
  - Hooks attached on Loaded, detached on Unloaded for proper cleanup
- **ProrateDialog height increase:**
  - Increased dialog height from 480 to 520 for better content fit

---

### January 30, 2026 (Session 2)
- **Help Sidebar polish and content updates:**
  - Renamed "Help / AI Sidebar" → "Help Sidebar" in MainWindow.xaml menu items (2 locations)
  - Removed glossary section and TOC entry from manual.html
  - Converted all 17 screenshot `<div class="placeholder">` tags to `<img>` tags
  - Added WebView2 virtual host mapping in SidePanelView.xaml.cs (`help.local` → Help folder) to enable local image loading
  - Changed SidePanelViewModel.cs help URL from `file:///` to `https://help.local/manual.html`
  - Added `<None Update="Help\*.png">` to VANTAGE.csproj for screenshot copy-to-output
  - Added Help Sidebar access note (F1 and settings menu)
  - Added active filter highlighted border note to Sidebar Filters section
  - Split Progress toolbar into 3 section screenshots (left/center/right)
  - Added SCAN button to toolbar left section documentation
  - Added filter manager documentation with screenshot and step-by-step instructions
  - Updated Budget description (clickable dropdown for column selection)
  - Updated Discrepancies description (VANTAGE vs P6 Schedule values)
  - Swapped Progress Books (section 5) and Work Packages (section 6)
  - Removed redundant `wp-full-view.png` (Generate tab is default view)
  - Fixed SCAN button references (on Progress toolbar, not Progress Books toolbar)
- **User filter save bug fix:**
  - Fixed ManageFiltersDialog.xaml.cs: changed save condition from `if (_isNewFilter)` to `if (_isNewFilter || _currentFilter == null)` to handle users typing directly without clicking New
  - Fixed save-then-select to use `lstFilters.SelectedIndex` instead of `lstFilters.SelectedItem`
  - Added ListBoxItem style to ManageFiltersDialog.xaml for FluentDark theme foreground visibility
  - Fixed empty catch block in SettingsManager.SetUserSetting to log errors
- **Today filter change:**
  - Changed PassesTodayFilter in ProgressViewModel.cs from 3WLA-based logic to simple SchStart/SchFinish == today check
  - Updated help manual description to match
- **Schedule Required Fields button:**
  - Changed from red text (`StatusRed` foreground) to red background (`StatusRedBg`) with standard foreground for better visibility on dark theme

### January 30, 2026
- **Auto-update mechanism:**
  - Created UpdateService (checks manifest.json on startup, downloads ZIP, verifies SHA-256 hash)
  - Created VANTAGE.Updater console app (waits for main app exit, extracts ZIP, relaunches)
  - Integrated update check into App.xaml.cs startup (before DB init, after splash)
  - Host-agnostic: works with GitHub raw URLs now, switch to Azure Blob later by changing one URL
  - Graceful failure: if offline or update server unreachable, app starts normally
  - Created publish-update.ps1 script for building and packaging releases
  - Added auto-update documentation to Help sidebar
- **Select All context menu and app shutdown fix:**
  - Added "Select All" item to Progress grid right-click context menu (selects all filtered rows)
  - Fixed app not terminating on close: changed ShutdownMode from OnExplicitShutdown to OnMainWindowClose, set Application.Current.MainWindow before closing splash screen
  - Moved Find-Replace in Schedule Detail Grid to Shelved (deferred to V2)
  - Added backlog items: V1 production packaging plan, auto-update plan

### January 29, 2026
- **Progress Log Performance Optimization:**
  - Added Azure index (`IX_ProgressLog_Delete_Lookup`) on ProgressLog delete filter columns with auto-creation at app startup
  - Fixed parameter type mismatch (string vs datetime) that prevented index usage on delete operations
  - Delete performance: 18 min → 17 sec for 57K records
  - Replaced client-side SqlBulkCopy upload with server-side INSERT...SELECT (data never leaves Azure)
  - Upload performance: 157 sec → 37 sec for 57K records
  - Fixed PercentEntry conversion (0-100 → 0-1 decimal) for Val_Perc_Complete column
  - Added explicit system column exclusion list to prevent internal fields from reaching ProgressLog
  - Added elapsed time display to delete result messages and per-batch logging
  - Replaced batched AzureUploadUtcDate UPDATE (570 calls) with single server-side UPDATE
  - Renamed Admin menu "Edit Snapshots" → "Manage Snapshots"

### January 28, 2026
- **Progress Log Management Dialog & Upload Tracking (WIP - needs retest):**
  - Created `VMS_ProgressLogUploads` Azure tracking table for upload batch management
  - Added duplicate warning before uploading if records for same ProjectID/WeekEndDate already exist
  - Inserts one tracking record per unique RespParty after each upload batch
  - Created ManageProgressLogDialog (Admin menu) to view and delete tracked upload batches
  - Delete removes matching records from VANTAGE_global_ProgressLog then tracking table
  - Added ProgressLog column truncation to match old VANTAGE silent truncation behavior
  - Changed upload Timestamp format to `M/d/yyyy h:mm:ss tt` (local time, matching old VANTAGE)
  - Added Stopwatch timing to upload success message
  - Updated Help sidebar with new dialog documentation

- **PjtSystemNo Field (New Activity Column):**
  - Added PjtSystemNo property across models, repository, sync, import/export, views, and snapshot dialogs
  - Maps to OldVantage `Tag_SystemNo` column (separate from existing PjtSystem/Tag_System)
  - Added new row in VMS_ColumnMappings for the mapping

- **Schedule Grid - Horizontal Scrolling:**
  - Added Ctrl+ScrollWheel horizontal scrolling to schedule master grid (matching Progress grid)
  - Added native horizontal scroll wheel (tilt wheel) support for mice like Logitech MX Master
  - Direct WM_MOUSEHWHEEL hook on ScheduleView for reliable tilt wheel handling
  - Improved HorizontalScrollBehavior to find ScrollViewers with actual horizontal content

- **Admin Snapshots Dialog - Loading Overlay:**
  - Added Syncfusion SfBusyIndicator overlay during upload and delete operations
  - Shows animated double-circle spinner with contextual status message
  - Messages: "Uploading X snapshot(s) to Progress Log...", "Deleting X snapshot(s)...", "Deleting all X snapshot(s)..."
  - Semi-transparent dark overlay covers entire dialog during operations

---

### January 27, 2026
- **AWS Textract Migration to Company Account:**
  - Migrated AWS Textract service from personal AWS account to Summit Constructors company AWS account
  - Updated Credentials.cs with company AWS credentials

- **Progress Log Upload Feature (Admin > Manage Snapshots):**
  - Added "Upload to Progress Log" button to AdminSnapshotsDialog
  - Uploads selected snapshot groups to VANTAGE_global_ProgressLog table on Azure
  - Uses VMS_ColumnMappings table for dynamic column mapping (ColumnName → AzureName)
  - Calculates fields at upload time: Status (from PercentEntry), EarnMHsCalc (BudgetMHs × PercentEntry / 100), ClientEquivEarnQTY
  - Sets UserID to current admin username, Timestamp to upload time (same for all records in batch)
  - Updates AzureUploadUtcDate on VMS_Activities for uploaded records (pull-only field)
  - SqlBulkCopy for efficient bulk transfer

- **AzureUploadUtcDate Pull-Only Field:**
  - Removed AzureUploadUtcDate from SyncManager push columns
  - Admin sets value on Azure during upload, users receive on pull but cannot overwrite
  - Allows users to see when their activities were last uploaded to Progress Log

- **ProgDate Bug Fix in Snapshot Creation:**
  - Fixed ProgressView.xaml.cs snapshot INSERT to use @progDate parameter instead of copying NULL from Activities
  - ProgDate now correctly captures submission timestamp in snapshots

- **AssignedTo Import Fix:**
  - Changed ExcelImporter to always set AssignedTo to currentUser during import
  - Prevents erroneous file values from assigning records to invalid or other users

---

### January 26, 2026
- **Azure Migration to Company Server (summitpc.database.windows.net):**
  - Migrated from personal Azure subscription to Summit Constructors company Azure
  - Created 12 VMS_ prefixed tables on company Azure (VMS_Activities, VMS_Users, VMS_Projects, etc.)
  - Created TR_VMS_Activities_SyncVersion trigger for auto-increment SyncVersion
  - Migrated data for 5 reference tables: Users, Projects, Admins, ColumnMappings, Managers
  - Left 7 tables empty for fresh start: Activities, ProgressSnapshots, Schedule, GlobalSyncVersion, Feedback, InMilestoneNotInP6, InP6NotInMilestone
  - Updated 19 C# files with VMS_ table name prefixes for all Azure queries
  - DatabaseSetup.MirrorTablesFromAzure() now maps Azure VMS_* tables to local unprefixed tables
  - Updated Credentials.cs to use company Azure connection string
  - Added VAL_Client_Earned_EQ-QTY to import ignored columns (V2 data model item)
  - All tests passed: sync, import, admin dialogs, deleted records restore

- **Progress Grid - Ctrl+ScrollWheel Horizontal Scrolling:**
  - Hold Ctrl and use mouse scroll wheel to scroll the grid horizontally
  - Standard Windows behavior for wide grids with many columns

- **Excel Import/Export - LineNumber Column Fix:**
  - Fixed typo in ExcelExporter.cs: `Tag_LineNumber` → `Tag_LineNo` (line 43)
  - Legacy exports now correctly map LineNumber data to the Tag_LineNo column
  - Updated MILESTONE_Column_Reference.md: corrected all LineNO references to LineNumber

- **Excel Import - Unmapped Column Detection:**
  - Import now detects columns that don't map to Activity properties
  - Aborts with clear error listing unrecognized columns (prevents silent data loss)
  - Added OldVantage names for calculated fields to skip list (Sch_Status, Val_EarnedHours_Ind, etc.)
  - Previously, unmapped columns were silently ignored causing data loss without warning

---

### January 25, 2026
- **Progress Grid - Grouping Feature:**
  - Added AllowGrouping and ShowGroupDropArea to ProgressView grid
  - Users can drag column headers to Group Drop Area to group rows by value
  - Multi-level grouping supported (drag multiple columns)
  - Expand/collapse groups with arrow icons
  - Right-click menu: "Freeze Columns to Here" and "Unfreeze All Columns"
  - Frozen column count persists in UserSettings
  - Documentation added to Help manual

- **Progress Grid - Visual Improvements:**
  - Fixed inconsistent cell border widths across column types
  - Added explicit BorderThickness (0.5px) via SfDataGrid.Resources style
  - Added cell padding (4px left/right) to all 92 columns for better readability
  - Added UseLayoutRounding and SnapsToDevicePixels for pixel-perfect rendering

- **Progress Grid - Performance Optimizations:**
  - Added UseDrawing="Default" for faster cell rendering (GDI+ vs TextBlock)
  - Added ColumnSizer optimization: auto-size on first load, then disable for performance

- **Progress Grid - Table Summary Row:**
  - Added summary row at bottom of grid showing Sum totals for numeric columns
  - Columns: Quantity, EarnQtyEntry, BudgetMHs, EarnMHsCalc, ClientBudget
  - Auto-updates when values change or filters applied (uses Syncfusion LiveDataUpdateMode)
  - Summary row stays frozen when scrolling vertically

- **Progress Grid - Multi-Cell Copy (Ctrl+C):**
  - Select multiple cells with Ctrl+Click or Shift+Click, press Ctrl+C
  - Copies in Excel-compatible format (tab-separated columns, newline-separated rows)
  - Pastes correctly into Excel cells
  - Uses PreviewKeyDown intercept to capture Ctrl+C before edit control

- **Progress Grid - Multi-Cell Paste (Ctrl+V):**
  - Single cell + multi-row clipboard: pastes downward from selected cell
  - Multi-cell selection: pastes to leftmost column only
  - Multi-column clipboard: uses first column only
  - Validates: editable column, user ownership, valid value types
  - Auto-dates for PercentEntry (SchStart/SchFinish auto-set)
  - Type conversion with clear error messages for mismatches
  - Marks LocalDirty, saves to database, refreshes grid

- **Progress Grid - Column Header Copy Options:**
  - Right-click any column header for new copy options
  - "Copy Column w/ Header" - copies header + all visible row values
  - "Copy Column w/o Header" - copies only visible row values
  - Works on read-only columns (Find & Replace hidden for read-only, copy options visible)

- **Help Manual - Copying Data Documentation:**
  - Added Ctrl+C to keyboard shortcuts table
  - Added new "Copying Data" section with multi-cell copy, column copy, and row copy docs
  - Updated Table of Contents

- **AI Progress Scan - OCR Improvements and UI Enhancements:**
  - Removed grayscale preprocessing (images are already B&W)
  - Changed default contrast from 1.3 to 1.2, slider range 1.0-2.0
  - Moved contrast slider from upload panel to results panel only (with Rescan button)
  - Added "00" → "100" OCR heuristic to handle missed leading 1 in handwritten entries
  - Added column header filtering (AllowFiltering) to results grid
  - Added BudgetMHs column to results grid (shows "NOT FOUND" if ActivityID not matched)
  - Added Select All button, renamed buttons to "Select Ready" and "Clear"
  - Removed Raw debug column
  - Widened dialog (1050x600), increased column widths for Cur %, New %, Conf
  - Added column resizing (AllowResizingColumns)
  - Added persistence for dialog size and column widths to UserSettings

- **Progress Grid - Global Search:**
  - Added pill-shaped search box in toolbar (left of REFRESH button)
  - Searches across commonly-used columns: ActivityID, Description, WorkPackage, PhaseCode, CompType, Area, RespParty, AssignedTo, Notes, TagNO, UniqueID, DwgNO, LineNumber, SchedActNO
  - Case-insensitive, filters on each keystroke
  - X button clears search
  - Combines with existing filters (Today, User Defined, column filters)
  - Clear Filters button also clears search text

---

### January 24, 2026
- **EarnQtyEntry Recalculation Bug Fixed:**
  - Added `RecalculateDerivedFields(changedField)` method to Activity.cs
  - Find/Replace now triggers recalculation after programmatic property changes
  - Progress summary panel updates after Find/Replace completes
  - Fixes: PercentEntry ↔ EarnQtyEntry sync, Quantity changes, BudgetMHs changes

- **AI Progress Scan - Image Preprocessing for OCR:**
  - Created ImagePreprocessor.cs with grayscale conversion and 30% contrast enhancement
  - Integrated preprocessing into ProgressScanService before Textract analysis
  - Fixes handwritten "100" being misread as "0" by improving image clarity

- **AI Progress Scan - Legacy Code Cleanup:**
  - Removed Done checkbox concept (legacy - now using % entry for completion)
  - Removed ExtractedDone, ExtractedQty, CurrentQty, NewQty from ScanReviewItem
  - Removed Done and Qty from ScanExtractionResult
  - Cleaned up TextractService to not set Done field

- **AI Progress Scan - Review Grid Fix (Proper Syncfusion Implementation):**
  - Changed to GridCheckBoxColumn (Syncfusion native) instead of template with WPF CheckBox
  - Set EditTrigger="OnTap" for single-click editing of all cells
  - Set SelectionMode="Single" with SelectionUnit="Cell" for proper cell interaction
  - Both checkboxes and New % cells now editable with single click
  - CurrentCellEndEdit event updates selection count

- **Progress Grid - Added ActivityID Column:**
  - Added ActivityID as visible column in ProgressView.xaml (after UniqueID)
  - Used for AI scan matching (shorter than UniqueID, easier for OCR)

- **AI Progress Scan - AWS Textract Implementation (100% accuracy achieved):**
  - Switched from Claude Vision API to AWS Textract for table extraction
  - Textract provides proper table structure with row/column indices and bounding boxes
  - Created TextractService.cs - AWS API wrapper with retry logic
  - Updated ProgressScanService.cs to use Textract instead of Claude Vision
  - Removed ClaudeVisionService.cs and ClaudeApiConfig.cs
  - PDF layout redesigned:
    - ID (ActivityID) moved to first column (Zone 1) - protected from accidental marks
    - Data columns: MHs (BudgetMHs), QTY (Quantity), REM MH, CUR % (removed REM QTY, CUR QTY)
    - Single % ENTRY box at far right - natural stopping point for field hands
    - Writing "100" = done (eliminated checkbox entirely)
  - Testing: 100% accuracy on 2 PDF scans and 1 JPEG scan
  - Added CLAUDE.md instruction: never modify Credentials.cs without explicit permission

- **AI Progress Scan - Architecture changes for accuracy (earlier):**
  - Switched to Claude Opus 4.5 model (`claude-opus-4-5-20251101`) for better vision accuracy
  - Implemented Tool Use (function calling) for structured output consistency
    - Defined `report_progress_entry` tool with strict schema
    - Eliminates JSON parsing variability - same results on repeated scans
  - Fixed PDF-to-image conversion:
    - Removed PdfiumViewer (incompatible with .NET 8, caused `FPDF_Release` entry point error)
    - Added Syncfusion.PdfToImageConverter.WPF package
    - PDF pages now convert to images before sending to API
  - Removed color fills from entry boxes (colors weren't helping AI accuracy):
    - All entry boxes now white background
    - AI relies on text labels instead of colors
  - Added text labels to all entry columns for AI identification:
    - DONE column: "C:" label (C = Complete)
    - QTY column: "Qty:" label
    - % ENTRY column: "%:" label
  - Updated AI prompts to focus on reading text labels, not colors
  - **Status:** Accuracy still inconsistent between PDF and JPEG scans - testing continues

---

### January 22, 2026
- **AI Progress Scan - Major accuracy improvements:**
  - Changed Progress Book format from UniqueID to ActivityID (shorter, easier to OCR)
  - Added color-coded entry fields for better AI column recognition:
    - DONE checkbox: Light green (230, 255, 230)
    - QTY entry: Light blue (230, 240, 255)
    - % ENTRY: Light yellow (255, 255, 230)
  - Entry fields only render for incomplete items (CUR % < 100) - reduces visual noise
  - Default font size increased from 6pt to 8pt with warning below 7pt
  - Updated AI extraction prompt to reference color-coded columns explicitly
  - Updated matching logic to use ActivityID (int) instead of UniqueID (string)
  - Removed PdfiumViewer dependency - PDFs now sent directly to Claude API
    - Claude handles multi-page PDFs natively with better quality
    - Removed PdfiumViewer and PdfiumViewer.Native.x86_64.v8-xfa packages
    - Simplified PdfToImageConverter.cs to just file type detection
  - Added font size warning display in Progress Books view
  - Testing confirmed: 7 extracted, 7 matched, accurate QTY vs % column distinction

### January 21, 2026
- **AI Progress Scan feature (Phases 4-5):**
  - Created Claude Vision API infrastructure in Services/AI/:
    - ClaudeApiConfig.cs - API configuration (key, version, endpoints)
    - ClaudeVisionService.cs - Image analysis with retry logic and rate limiting
    - PdfToImageConverter.cs - PDF-to-PNG conversion using PdfiumViewer at 200 DPI
    - ProgressScanService.cs - Orchestrates scan workflow with progress reporting
  - Created AI models in Models/AI/:
    - ScanExtractionResult.cs - JSON response model for Claude API
    - ScanReviewItem.cs - Bindable review grid item with INotifyPropertyChanged
    - ScanProgress.cs - Progress tracking with ScanBatchResult
  - Created ProgressScanDialog with 3-step workflow:
    - Step 1: Drag-drop or browse for PDF/PNG/JPG files
    - Step 2: Processing with progress bar and cancel support
    - Step 3: Review grid with checkbox selection, filtering, editable New %/QTY columns
  - Added SCAN button to ProgressView toolbar
  - Added PdfiumViewer and PdfiumViewer.Native.x86_64.v8-xfa NuGet packages

- **Progress Book - Exclude Completed Activities:**
  - Added checkbox to filter section in ProgressBooksView.xaml
  - Added ExcludeCompleted property to ProgressBookConfiguration model
  - Updated preview and generate queries to filter out 100% progress activities
  - Persists with layout save/load

- **Progress Book - UI cleanup:**
  - Removed Layout Zones section from ProgressBooksView (kept Save button)
  - Removed UpdateZone2Summary method and all calls to it

- **Progress Book PDF - Header redesign and fixes:**
  - New page header layout: Logo (half size) + project info on left, book title centered, date + page number on right
  - Removed footer - page numbers now in header (more vertical space for data)
  - Column headers fixed at 5pt font (not affected by slider)
  - Column padding halved from 8pt to 4pt
  - Header row height reduced from 20pt to 14pt
  - Fixed page numbering off-by-one error (was showing "Page 4 of 3")
  - Rewrote EstimatePageCount to simulate actual rendering logic for accurate page totals

### January 20, 2026
- **Progress Book PDF Generator - Auto-fit and layout improvements:**
  - Replaced percentage-based column widths with auto-fit based on actual content
  - Zone 2 columns (UniqueID, ROC, DESC, user columns) measure content to determine width
  - Zone 3 data columns (REM QTY, REM MH, CUR QTY, CUR %) also auto-fit
  - Only entry boxes (DONE, QTY, % ENTRY) have fixed widths
  - Description column wraps long lines (row height increases as needed)
  - Added project description to page header from Projects table (e.g., "24.005 - Fluor Lilly Near Site OSM Modules")
  - Page header fonts are static 12pt (not affected by font slider)
  - Column/group headers match font slider setting
  - Font slider range changed to 4-10pt with default 6pt
  - Increased cell padding for better readability
  - UniqueID moved to Zone 2 columns (user can reorder but not delete)
  - Separated Groups from Sorts: groups auto-sort alphanumerically, sorts stack like Excel
  - Up to 10 groups and 10 sort levels allowed
  - Removed SubGroupConfig.cs (no longer needed)
  - Added GetProjectDescription() to ProjectCache for header lookup

- **Progress Book Layout Builder - Style consistency update:**
  - Added Syncfusion SfSkinManager with FluentDark theme for proper control theming
  - Added RoundedButtonStyle and PrimaryButtonStyle matching WorkPackageView exactly
  - Updated GENERATE, Save, Refresh Preview buttons to use proper styles
  - Removed explicit Height from ComboBoxes (Syncfusion theme handles sizing)
  - Added Foreground to RadioButtons for visibility on dark background
  - GridSplitter persists position to UserSettings

### January 19, 2026
- **Progress Book Module - Phases 1-3 complete:**
  - Phase 1: Created data models in `Models/ProgressBook/`:
    - PaperSize.cs (Letter/Tabloid enum)
    - ColumnConfig.cs (Zone 2 column configuration)
    - SubGroupConfig.cs (sub-group level config)
    - ProgressBookConfiguration.cs (full layout config, serialized to JSON)
    - ProgressBookLayout.cs (database entity)
  - Phase 1: Added ProgressBookLayouts table to DatabaseSetup.cs with indexes
  - Phase 2: Created ProgressBookLayoutRepository.cs with full CRUD operations
  - Phase 3: Built Layout Builder UI in ProgressBooksView.xaml:
    - Layout name input and saved layouts dropdown
    - Paper size radio buttons (Letter/Tabloid landscape)
    - Font size slider (8-14pt)
    - Main group dropdown with starred common fields
    - Sub-groups section with add/remove
    - Zone 2 columns list with width inputs and remove buttons
    - Zone summary panel and preview placeholder
  - Phase 3: Created SelectFieldDialog.xaml for adding columns
  - Updated PRD with implementation decisions (Syncfusion PDF, 1 page/call, GlobalSettings limits)


- Help Manual - Work Packages section written (manual.html):
  - Added 7 subsections with TOC links: Overview, Layout, Generate Tab, WP Templates Tab, Form Templates Tab, Token System, Previewing Templates
  - Documented Generate tab: all settings (Project, Work Packages, WP Template, PKG Manager, Scheduler, WP Name Pattern, Logo, Output Folder), workflow steps, output structure
  - Documented WP Templates tab: template controls, forms list management, creation workflow
  - Documented Form Templates tab: all 5 form types (Cover, List, Form, Grid, Drawings) with settings tables
  - Documented complete token system: date/user tokens, work package tokens, project tokens, activity tokens (including UDF1-10)
  - Added notes and warnings for key concepts (WP Name Pattern usage, Grid vs Form differences, sample data in preview)
- Help Sidebar - Action buttons implemented:
  - Back to Top: scrolls WebView2 to top via JavaScript (window.scrollTo)
  - Print PDF: saves help content as PDF via WebView2.PrintToPdfAsync with SaveFileDialog
  - View in Browser: opens manual.html in default browser via Process.Start with UseShellExecute
- Help Sidebar - Search field improvements:
  - Added clear button (✕) that appears when text is present
  - Added italic "Search..." placeholder when field is empty
  - Clear button clears search and refocuses input field

### January 18, 2026
- **Auto-detecting Activity Import** - Consolidated Legacy and NewVantage imports into single smart import:
  - Added DetectFormat() method that identifies format by column headers (UDFNineteen/Val_Perc_Complete = Legacy, UniqueID/PercentEntry = NewVantage)
  - Removed ambiguous threshold-based percent detection (was using 1.5 threshold which caused edge cases)
  - Legacy format: ALWAYS multiply percent by 100 (strict conversion)
  - NewVantage format: ALWAYS use percent as-is (strict conversion)
  - Clear error messages if format cannot be determined or is mixed
  - Removed Legacy Import buttons from UI (auto-detect handles both formats)
  - Kept Legacy Export buttons for OldVantage system compatibility
  - Updated Import Activities tooltips to indicate auto-detection
- **Activity Import date/percent cleanup** - During import, cleans up date/percent inconsistencies:
  - PercentEntry = 0 → clears both SchStart and SchFinish
  - PercentEntry > 0 with no SchStart → sets SchStart to today
  - SchStart in future → clamps to today
  - PercentEntry < 100 → clears SchFinish
  - PercentEntry = 100 with no SchFinish → sets SchFinish to today
  - SchFinish in future → clamps to today
- **Export progress indicator** - Shows animated progress bar in bottom-right during Activity exports (both regular and Legacy)

### January 17, 2026
- **Schedule Change Log feature** - Apply Schedule detail grid edits to live Activities:
  - New ScheduleChangeLogEntry model and ScheduleChangeLogger utility class
  - ScheduleChangeLogDialog accessible via Tools → Schedule Change Log
  - Logs edits to PercentEntry, BudgetMHs, SchStart, SchFinish in detail grid
  - Dialog shows WeekEndDate, UniqueID, Description, Field, Old/New values with checkboxes
  - Smart duplicate handling: only applies most recent change per UniqueID+Field
  - Progress view auto-refreshes after applying changes
  - Log files stored in %LocalAppData%\VANTAGE\Logs\ScheduleChanges\
  - Help sidebar documentation added under Schedule Module section
- **Auto-purge for all log files** - Cleans up logs older than 30 days on startup:
  - AppLogger: Purges both physical log files (app-yyyyMMdd.log) and database Logs table entries
  - ScheduleChangeLogger: Purges schedule change JSON files
  - Uses filename date parsing (not file system dates) for reliable age detection
- **UDF18 renamed to RespParty (Responsible Party)** throughout the application:
  - Models, database layer, views, dialogs, import/export, documentation
  - Grid column now displays as "Resp Party" with required field styling
  - Legacy imports still work via ColumnMappings table
- Work Package template editors tested and validated:
  - Cover editor - editing, saving, preview
  - List editor - item add/remove/reorder, saving, preview
  - Grid editor - column add/remove/reorder, row count, saving, preview
  - Form editor - sections/items/columns, saving, preview
  - Type selection dialog - creating new templates of each type
- Summary stats column selector:
  - Added clickable column name in ProgressView summary stats (replaces static "Budget:" label)
  - Dropdown arrow indicator with context menu showing available numerical columns
  - Available columns: BudgetMHs, ClientBudget, Quantity, BudgetHoursGroup, BudgetHoursROC, ROCBudgetQTY, ClientEquivQty, BaseUnit
  - Earned calculation dynamically uses selected column: `selectedColumn * PercentEntry / 100`
  - Selection persists to UserSettings across sessions
  - Underline on hover indicates interactivity

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
  - Deleted separate Interactive_Help_Mode_Plan.md, Sidebar_Help_Status.md
  - Help navigation simplified: always opens to top of document (removed anchor navigation)
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
