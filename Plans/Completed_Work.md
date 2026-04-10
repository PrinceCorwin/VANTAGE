# VANTAGE: Milestone - Completed Work

This document tracks completed features and fixes. Items are moved here from Project_Status.md after user confirmation.

---

## Unreleased

### April 10, 2026 (Snapshot Upload Tracking Fix, Timeout Hardening, Sync Dialog Sort)
- **Fixed snapshot upload tracking silently failing:** The tracking INSERT to `VMS_ProgressLogUploads` (which drives the "Uploaded" column in AdminSnapshotsDialog) was timing out on large snapshot groups (130K+ records). The `respCmd` and `trackCmd` had default 30-second timeouts while the main ProgressLog INSERT used unlimited. Set both to unlimited (`CommandTimeout = 0`).
- **Fixed Uploaded column date format mismatch:** The LEFT JOIN comparing `VMS_ProgressLogUploads.WeekEndDate` to `VMS_ProgressSnapshots.WeekEndDate` used raw string comparison, which failed when formats differed (e.g., `"4/8/2026"` vs `"2026-04-08"`). Changed to `TRY_CONVERT(datetime, ...)` date comparison.
- **Submit Week timeout fix:** The `existingCmd` query (checking for existing snapshots across all users for a week) had default 30-second timeout. Set to unlimited since this scans potentially hundreds of thousands of rows.
- **AssignTo timeout fix:** Both `SqlBulkCopy` operations (ownership check temp table and update batch temp table) had default 30-second `BulkCopyTimeout`. Set to unlimited. Also set `ownerQuery` and `updateCmd` to unlimited (were 120s).
- **Sync dialog project sort reversed:** Projects in "Select Projects to Sync" dialog now sort by ProjectID descending so newest projects (highest year numbers) appear at the top.
- **Project_Status.md cleanup:** Removed 7 completed feature narratives that were sitting in the status doc (Plugin System, Multi-Theme, Progress Books, AI Progress Scan, Help Sidebar, AI Takeoff Multi Title Block, Theme System Refactor, Import from AI Takeoff). All were already documented in Completed_Work archives.
- **Key files:** `Dialogs/AdminSnapshotsDialog.xaml.cs`, `Views/ProgressView.xaml.cs`, `Dialogs/SyncDialog.xaml.cs`

### April 8, 2026 (Schedule Module — Local Snapshot Mirror, 21-Day Retention)
- **Schedule Module — Local snapshot mirror for fast grid rendering:** All snapshot reads in the Schedule module (master rollups, detail grid, P6/MS discrepancy reports) now query a local SQLite mirror instead of Azure. This eliminates the long lag users were experiencing when interacting with the master and detail grids.
- **New local table `ProgressSnapshots`:** Trimmed 12-column mirror of Azure `VMS_ProgressSnapshots` (UniqueID, WeekEndDate, SchedActNO, Description, PercentEntry, BudgetMHs, ActStart, ActFin, AssignedTo, ProjectID, UpdatedBy, UpdatedUtcDate). Holds only the current user's rows for the week matching the imported P6 file. Azure stays at 89 columns and is unchanged — the revert flow in ManageSnapshotsDialog continues to read from Azure.
- **Refill triggers:** Local mirror is wiped and refilled (a) when the user imports a P6 file (in `MainWindow.ImportP6File_Click` after `ScheduleExcelImporter.ImportFromP6Async`), (b) silently in the background after login if the local table is empty but a P6 file is already imported (`App.OnStartup` post-login), and (c) when the user re-submits Progress for the week and project that's currently mirrored (in `ProgressView.BtnSubmit_Click`).
- **Detail grid edits sync to both copies:** `ScheduleRepository.UpdateSnapshotAsync` now writes through to both Azure and local SQLite. Best-effort on the local side — if local fails, Azure is still authoritative and self-heals on next P6 import.
- **Manage Snapshots delete syncs to both copies:** When a user deletes their snapshot weeks via the Manage Snapshots dialog, the matching local mirror rows are also deleted to prevent phantom data in the Schedule module.
- **Schema migrations v10 + v11:** v10 creates the local `ProgressSnapshots` table with all 89 columns. v11 trims it to the 12 columns the Schedule module actually reads, dropping and recreating the table. Existing users on upgrade get a one-time silent backfill from Azure on first launch.
- **Bulk refill optimizations:** `RefillLocalSnapshotsForWeekAsync` uses per-connection PRAGMAs (`synchronous = OFF`, `temp_store = MEMORY`, `cache_size = -200000`), drops indexes before insert and recreates them after, and uses a prepared statement loop for the bulk insert. Timing breakdown logged so future tuning has data to work from.
- **Schedule diagnostic updated:** `Diagnostics/ScheduleDiagnostic.cs` now queries the local mirror so the diagnostic reflects what the Schedule module actually consumes. Added a recommendation about re-importing the P6 file if the local mirror is empty.
- **Snapshot retention reduced from 28 to 21 days:** The submit-time purge in `ProgressView.BtnSubmit_Click` now uses `DateTime.Now.AddDays(-21)` instead of `-28`.
- **Help manual updated:** Added a "Snapshot Comparison Data (Local Caching)" callout in the Schedule → P6 Import section explaining the new local caching behavior and how to refresh stale data.
- **Behavior change to flag (intentional):** `ScheduleRepository.GetMSNotInP6Async` previously had no `AssignedTo` filter, so the "MS not in P6" report (in the 3WLA companion to the P6 export) showed all users' snapshot data. After conversion to local, it now shows only the current user's data — which matches the rest of the Schedule module's per-user filtering and is the more intuitive behavior for a per-user export.
- **Performance result:** With 132,566 rows for one week, the initial backfill takes ~41 seconds (Azure SELECT dominates at ~40s). Adding a covering Azure index on `(AssignedTo, WeekEndDate) INCLUDE (the 12 read cols)` is recommended to reduce this further; the user will create that index manually on Azure rather than via app code.

### April 8, 2026 (V1 Modules Marked Complete)
- **Progress, Schedule, Work Package, AI Takeoff, and Progress Books modules** all marked complete. Going forward, any issues will be tracked as bugs as they appear rather than as "ready for testing" placeholders.
- Removed the V1 Testing Scope and Module Status tables from `Project_Status.md` since all in-scope modules are now complete.

### April 7, 2026 (Tutorial Video Planning, Claude Skills Sync Setup)
- **Tutorial video series scaffolded:** New `Plans/Tutorials/` folder with subfolders for Intro, Progress, Schedule, WorkPackages, ProgressBooks, Analyse, Takeoffs, and Admin. Each video gets its own folder for plans, scripts, and assets.
- **Intro video plan + script complete:** `Plans/Tutorials/Intro/Plan.md` and `Plans/Tutorials/Intro/Script.md`. Twelve sections covering installation (with SmartScreen warning walkthrough), first launch / login (no self-service — call admin if rejected), main window tour (toolbar / nav / settings), auto-updates, plugins, status bar, and F1 help sidebar. Two-column script format: on-screen action / voiceover. Demo project: 99.999 (Sandbox).
- **Takeoffs video plan complete:** `Plans/Tutorials/Takeoffs/Plan.md`. Covers AI Takeoff end-to-end — config concept, Config Creator (BOM/title block crop regions), batch setup, processing, Excel output tabs, Previous Batches, Recalc Excel, send-to-admin, and brief handoff to the Import from AI Takeoff dialog. Open questions logged for sample drawings, live-vs-prerecorded processing, and config creator depth.
- **Project_Status.md:** Added "Tutorial Videos" section with todo items for all eight planned videos (Intro, Progress, Schedule, Work Pkgs, Prog Books, Analyse, Takeoffs, Admin). Decisions captured: TAKEOFFS gets its own video, Admin menu is briefly mentioned in Intro and gets its own dedicated video.
- **Work PC Claude skills sync setup complete:** Replaced stale `~/.claude/skills/` folder with a clone of `PrinceCorwin/claude-skills`. Now in sync with the home PC. New skills available: `create-theme`, `finisher` (with merged improvements — `commit` trigger word, month-rollover archive logic, plan doc check), and `speedup`. Removed the one-time setup todo from Project_Status.md.

### April 6, 2026 (PercentEntry Decimal Input Fix)
- **Decimals now accepted in PercentEntry:** Users reported entering `0.5` resulted in `5` — the leading `0.` was being dropped. Two bugs combined: (1) the auto-BeginEdit handler in `SfActivities_KeyDown` only triggered on digit keys, so typing `.` from a non-editing cell was silently lost; (2) the `<TextBox>` in the `PercentEntry` GridTemplateColumn EditTemplate used `UpdateSourceTrigger=PropertyChanged`, which raced against the `Activity.PercentEntry` setter on every keystroke (clamp/round/multi-PropertyChanged/`UpdateEarnedQtyFromPercComplete` chain).
- **Fix 1:** Added `Key.OemPeriod` and `Key.Decimal` to the auto-BeginEdit key check so typing `.5` enters edit mode like digits do.
- **Fix 2:** Changed the EditTemplate TextBox binding to `UpdateSourceTrigger=LostFocus`. The value commits when editing ends (Tab/Enter/arrow nav/click-away), eliminating per-keystroke parsing races. Existing `EndEdit()` calls in `PercentEntryEditBox_PreviewKeyDown` still commit values during keyboard navigation because EndEdit causes the textbox to lose focus.
- **Why EarnQtyEntry was unaffected:** It uses Syncfusion's native `GridNumericColumn`, which has built-in decimal handling. Only PercentEntry uses a custom `GridTemplateColumn` (because of the progress-bar visual overlay), so it required a hand-rolled edit textbox.
- **Key files:** `Views/ProgressView.xaml`, `Views/ProgressView.xaml.cs`

### April 6, 2026 (Copy/Paste Fix, Arrow Key Navigation, Sync Safety, Date Paste Validation)
- **Fixed multi-cell copy/paste:** Ctrl+V paste was completely broken due to `IsEditing` bail-out checks added in a prior commit. When `EditTrigger="OnTap"` put cells into edit mode, both paste handlers (`UserControl_PreviewKeyDown` and `SfActivities_PreviewKeyDown`) would exit early without pasting. Fix: removed the bail-outs, added smart routing — multi-value clipboard or multi-cell selection triggers the custom paste handler, single-value + single-cell lets the built-in editor handle it (paste at cursor).
- **Arrow keys always navigate cells:** Up/Down/Left/Right arrow keys in edit mode now always commit the edit and move to the next cell. Previously, date columns (GridDateTimeColumn) would scroll the date value when pressing arrows. New handler at the top of `SfActivities_PreviewKeyDown` intercepts arrows before the date picker sees them.
- **Sync EndEdit safety:** Added `EndEdit()` + `await Task.Delay(250)` at the top of `BtnSync_Click` to commit any active cell edit before syncing. Prevents data loss when user clicks Sync while still editing a cell.
- **Date paste validation:** Multi-cell paste to ActStart/ActFin columns now enforces the same percent-based rules as single-cell editing. ActStart blocked for rows with 0% Complete; ActFin blocked for rows with less than 100%. Valid rows are pasted, violating rows are skipped, and a summary message shows how many were pasted vs skipped. Both paste handlers (multi-cell and single-value-to-multiple-rows) are covered.
- **Key files:** `Views/ProgressView.xaml.cs`

### April 6, 2026 (Completed_Work Monthly Archiving)
- **Monthly archiving for Completed_Work.md:** Set up `Plans/Archives/` directory and automated archiving workflow. At the start of each new month, the finisher skill moves previous month's entries to `Plans/Archives/Completed_Work_YYYY-MM.md` and resets the file. Prevents infinite file growth.
- **Finisher skill updated:** Step 2 now includes an archive check before adding new entries — detects entries from previous months and archives them automatically.
- **CLAUDE.md updated:** Added "Completed_Work.md Monthly Archiving" section documenting the convention.
- **Dev Tooling backlog item added:** Task to sync Claude Code skill files (`~/.claude/skills/`) across machines — currently local-only with no sync mechanism.

### April 5, 2026 (SCRD ShopField Fix)
- **SCRD labor rows set to ShopField=2 (Field):** SCRD connection rows generated during takeoff post-processing now set ShopField=2 (Field) instead of 1 (Shop). BU was already Field; SCRD now matches. Other connection types (BW, SW, etc.) remain Shop.
- **Key files:** `Services/AI/TakeoffPostProcessor.cs`

### April 3, 2026 (Lift AI Takeoff User Restriction)
- **AI Takeoff open to all Estimators:** Removed hardcoded `IsTakeoffAllowed()` method that restricted the Takeoff module to users `steve` and `Steve.Amalfitano`. Takeoff button visibility and Import from AI Takeoff menu item now use standard `App.CurrentUser.IsEstimator` role check. Removed all TEMPORARY/TO REVERT comments from `MainWindow.xaml` and `MainWindow.xaml.cs`.
- **Key files:** `MainWindow.xaml`, `MainWindow.xaml.cs`

### April 3, 2026 (ROC Split Logic, SPL ShopField Fix)
- **ROC split logic in import pipeline:** When a ROC set is selected in Import from AI Takeoff, rows whose component is in the set's applicable components list and whose ShopField matches a ROC step's ShopField are split. Original row is modified with the first matching step's ROCStep and percentage of BudgetMHs; additional rows are cloned for remaining matching steps. Non-matching rows pass through unchanged. Runs as the last data transformation before UniqueID generation.
- **SPL ShopField = 2 (Field):** SPL (spool handling) rows in takeoff post-processing now explicitly set ShopField=2. Previously inherited ShopField=1 from parent PIPE row.
- **GetROCSetDataAsync:** New method in `ProjectRateRepository` loads ROC set steps and applicable components from Azure for the import pipeline.
- **Key files:** `Dialogs/ImportTakeoffDialog.xaml.cs`, `Data/ProjectRateRepository.cs`, `Services/AI/TakeoffPostProcessor.cs`

### April 3, 2026 (Import Profiles, ROC Manager Components, UniqueID Fix)
- **UniqueID generation fixed:** `SetSystemFields()` now matches `ExcelImporter` pattern — `i` prefix, `yyMMddHHmmss` (local time), last 3 chars of username lowercased, `"usr"` fallback. Previously used `t` prefix with `yyyyMMddHHmmss` (UTC) and first 3 chars uppercased.
- **Import profiles:** Save/load/delete named presets in the Import from AI Takeoff dialog. Profiles store output mode, handling, options, ROC set, column mappings, and metadata values. Stored in UserSettings as JSON (same pattern as GridLayouts). Profiles can be loaded before file selection — saved column mappings re-apply automatically when file loads.
- **Batch source removed:** Removed From File/From Batch radio buttons, Batches button, and all batch-related code from ImportTakeoffDialog. Source is now just a Select File button with inline file name display.
- **ROC Manager — applicable components checklist:** New right panel with scrollable checkbox list of all component types from the rate sheet. All/None quick-select buttons with count display (e.g., "12 / 95"). Checkboxes enabled only in edit mode. Component list built dynamically via `RateSheetService.GetAllComponents()`. Selections stored as comma-separated string in new `Components` column on `VMS_ROCRates` table.
- **ROC Manager — bug fixes:** Fixed set data never loading on dialog open (`_isLoading` guard prevented `CboSet_SelectionChanged` from firing). No set auto-selected on open — user picks from dropdown. Dialog opens in New Set mode when accessed via "+ Create New..." from ImportTakeoffDialog.
- **ROC Manager in Tools menu:** Added "ROC Manager" menu item to MainWindow Tools menu for standalone access to create, view, modify, and delete ROC rate sets.
- **Help manual updated:** Comprehensive AI Takeoff documentation — added Blank Components, Recalc Excel, ROC Manager sections. Updated Import from AI Takeoff with profiles, output modes, handling, options, column mapping, metadata. Updated multi title block regions, Previous Batches rename, Tools menu ROC Manager.
- **Key files:** `Dialogs/ImportTakeoffDialog.xaml/.cs`, `Dialogs/ManageROCRatesDialog.xaml/.cs`, `Models/ImportProfile.cs`, `Services/AI/RateSheetService.cs`, `MainWindow.xaml/.cs`, `Help/manual.html`

### April 3, 2026 (Import from AI Takeoff — Dialog & Import/Export Logic)
- **Import Takeoff Dialog redesigned:** Two-column layout (1200px wide, SizeToContent height). Left column: Source, Output, Handling, Options, ROC Set, Metadata. Right column: Column Mapping with SfBusyIndicator spinner during file loading.
- **Source selection:** From File (local .xlsx) or From Batch (S3, TODO: wire up download). File picker populates column mapping grid with headers and first non-blank sample values.
- **Output modes:** Import Records (DB only), Create Excel (Vantage-formatted .xlsx with SaveFileDialog), Import And Excel (both).
- **Handling filter:** Keep PIPE, Keep SPL (default), or Keep PIPE and SPL. Only affects PIPE/SPL rows — all other component types always kept.
- **Options checkboxes:** Roll Up BU Hardware (prorates GSKT/WAS/HARD/BOLT MHs into BU rows per drawing proportionally, removes ALL hardware rows including unclaimed). Roll Up Fab Per DWG (collapses ShopField=1 rows into one FAB row per drawing with ROCStep=FAB).
- **Metadata section:** 9 required fields (ProjectID, WorkPackage, PhaseCode, CompType, PhaseCategory, SchedActNO, Description, ROCStep, RespParty) with Enter Value / Use Source toggle. Two-way sync with column mapping — mapping a column auto-sets metadata to Use Source; changing metadata to Enter Value unmaps the column.
- **Column Mapping:** 15 static default mappings for Labor tab columns (Drawing Number→DwgNO, Component→UDF6, Size→PipeSize1, etc.). Available mappings exclude read-only, calculated, date, and display fields. Comboboxes dynamically update to prevent duplicate mappings.
- **Dual size handling:** `ResolveNumericValue()` parses "6x4" format for PipeSize columns, takes larger value. Data type validation allows dual sizes for PipeSize fields.
- **Import pipeline:** Read Labor rows → Handling filter → BU Hardware rollup → Fab Per DWG rollup → Map to Activities via reflection → Apply metadata overrides → Generate UniqueIDs + system fields → Insert to SQLite / Create Excel.
- **Vantage Excel export:** Full template with 83 columns matching the standard Activity export format, styled headers, auto-fit columns, frozen header row. User chooses save location via SaveFileDialog.
- **Matl_Grp_Desc column ordering fix:** Added `Matl_Grp_Desc` to explicit columns list in `TakeoffPostProcessor.WriteLaborTab()` — was previously falling into alphabetically-sorted title block fields at the end. Now placed right after `Matl_Grp`.
- **CLAUDE.md:** Added Loading Indicators convention (SfBusyIndicator with DualRing animation).
- **New models:** `ColumnMappingItem.cs`, `MetadataFieldItem.cs`
- **TODOs added:** Multi-drawing documents (multi-page PDFs), Second Size in AI extraction output, UniqueID generation alignment with ExcelImporter pattern.
- **Key files:** `Dialogs/ImportTakeoffDialog.xaml`, `Dialogs/ImportTakeoffDialog.xaml.cs`, `Models/ColumnMappingItem.cs`, `Models/MetadataFieldItem.cs`, `Services/AI/TakeoffPostProcessor.cs`, `CLAUDE.md`

### April 2, 2026 (Takeoff FS Material Group Correction)
- **FS Matl_Grp auto-correction:** New post-processing step (A5) corrects field support (FS) component `Matl_Grp` values to match the pipe of the same size in the same drawing. AI often defaults FS to CS because material isn't in the description. When multiple pipe materials exist, picks the non-CS value. No matching pipe leaves FS unchanged.
- **Matl_Grp_Desc column:** Corrected FS rows get a `Matl_Grp_Desc` column populated with the full material group description. Column is inserted right after `Matl_Grp` in the Material tab.
- **MaterialGroupDescriptions dictionary:** Added 16-entry mapping from `Matl_Grp` codes to descriptions (e.g., CS→CARBON STL, SS→STAINLESS, HAST→HASTELLOY, TITANIUM, 99%NI).
- **Edge case logging:** Multi-material and non-CS selection decisions are logged with drawing number, size, and resolution note.
- **All three paths covered:** Process Batch, Previous Batches download, and Recalc Excel all run through the new correction step.
- **Key files:** `Services/AI/TakeoffPostProcessor.cs`

### April 1, 2026 (Takeoff ShopField Post-Processing, Dialog Fixes)
- **ShopField post-processing on Material tab:** Lambda now sets all material rows to ShopField=1 (Shop). Post-processor (`AssignMaterialShopField`) corrects to Field (2) for: items with all BU/SCRD connection types, inherently field components (FS, BOLT, GSKT, WAS, INST, GAUGE), and items with zero connections or empty connection type. PIPE always stays Shop. Mixed connection types (e.g., "BW, SCRD") stay Shop. `WriteMaterialShopField` writes corrected values back to the Material worksheet.
- **Previous Batches dialog — Rename button repositioned:** Moved Rename button from overlapping fixed-margin position into the right-side button panel, directly left of Cancel. Visible to all users.
- **Previous Batches dialog — Cancel button styling:** Cancel button now uses red background (`StatusRedBgBtn`/`StatusRedFgBtn`) matching Delete All.
- **InputDialog SizeToContent fix:** Changed from fixed `Height="180"` to `SizeToContent="Height"` so buttons are never cut off at different DPI/scaling.
- **Key files:** `Services/AI/TakeoffPostProcessor.cs`, `Dialogs/PreviousBatchesDialog.xaml`, `Dialogs/InputDialog.xaml`

### April 1, 2026 (Progress Grid — AzureUploadUtcDate Fix)
- **Fixed AzureUploadUtcDate not displaying:** The `AzureUploadUtcDate` column was always blank in the Progress grid even though values existed on Azure. The bug was in `ActivityRepository.GetPageAsync` — it simply never read the column from the database when loading Activity objects. Added the missing read in both GetPageAsync locations.
- **Consistent date/time formatting:** Added `AzureUploadUtcDateDisplay` property to `Activity.cs` matching the `UpdatedUtcDateDisplay` format (`yyyy-MM-dd HH:mm`). Changed the XAML column from `GridDateTimeColumn` to `GridTextColumn` bound to the display property for consistent formatting with `UpdatedUtcDate`.
- **Key files:** `Data/ActivityRepository.cs`, `Models/Activity.cs`, `Views/ProgressView.xaml`


---

**Archives:** See Plans/Archives/ for previous months.
