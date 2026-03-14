# VANTAGE: Milestone - Completed Work

This document tracks completed features and fixes. Items are moved here from Project_Status.md after user confirmation.

---

## Unreleased

### March 13, 2026 (Takeoff — Project Rates, ROC Rates, & Rate/ROC Dropdowns)
- **Per-project unit rate overrides:** New `VMS_ProjectRates` Azure table allows uploading project-specific rate sheets from Excel. Multiple named rate sets per project. Rates are looked up before the embedded default rate sheet — fallback to defaults for unmatched entries. `RateSource` column on Labor tab shows "Project" or "Default" for each row.
- **UOM column on Labor tab:** Rate lookups now write the Unit of Measure (UOM) alongside BudgetMHs.
- **Project Rate Management dialog:** Admin > Project Rates opens a management dialog showing all uploaded rate sets with Project, Set Name, rate count, Created By, Created Date, Updated By, Updated Date columns. Upload New, Delete Selected, Export Template, and Close buttons. Template exports an Excel with the expected column headers (Item, Size, Sch-Class, Unit, MH).
- **Project Rate upload flow:** Upload validates Excel column headers (accepts aliases for backward compatibility), prompts for Project ID (dropdown from VMS_Projects) and Rate Set Name, bulk-imports via SqlBulkCopy. Replaces existing set if same name exists.
- **ROC Rates admin dialog:** Admin > ROC Rates opens a dialog for managing per-project ROC percentage split sets. Inline-editable grid with ROCStep, Percentage, ShopField (Shop/Field dropdown), SortOrder columns. Percentage sum validation (must equal 100%). Full CRUD for admins, read-only for estimators.
- **TakeoffView dropdowns:** Two new dropdowns — "Unit Rates" (Default Embedded + project-specific sets + Upload New option) and "ROC Set" (None + named sets + Create New option). Both "Upload New" and "Create New" open their respective management dialogs.
- **Column rename:** Project rate columns renamed from (EST_GRP, Size, SCH_RTG, Unit, FLD_MHU) to (Item, Size, Sch-Class, Unit, MH) across all code, Azure table, upload validation, template export, and lookup cache.
- **Key files:** `Data/ProjectRateRepository.cs` (new), `Services/AI/ProjectRateUploader.cs` (new), `Dialogs/ManageProjectRatesDialog.xaml/.cs` (new), `Dialogs/ManageROCRatesDialog.xaml/.cs` (new), `Services/AI/RateSheetService.cs`, `Services/AI/TakeoffPostProcessor.cs`, `Views/TakeoffView.xaml/.cs`, `MainWindow.xaml/.cs`

### March 13, 2026 (Takeoff — Debug Cleanup & Admin Missed Data Notification)
- **Removed FRH debug logging:** Deleted 5 per-drawing/per-pipe `AppLogger.Info` calls from `TakeoffPostProcessor.cs` (FRH section). High-level summary logs retained.
- **Send Missed Makeups and Rates to Admin:** New checkbox on the Takeoff view (default checked). After post-processing, if there are missed fitting makeups or unmatched rates, a separate Excel containing only the Missed Makeups and Missed Rates tabs is emailed to all administrators via Azure Communication Services.
- **GenerateLaborAndSummary return value:** Method now returns `(int MissedMakeups, int MissedRates)` tuple so callers can act on missed data counts.
- **Key files:** `Services/AI/TakeoffPostProcessor.cs`, `Views/TakeoffView.xaml`, `Views/TakeoffView.xaml.cs`

### March 13, 2026 (Grid Scrollbar & Theme Polish)
- **Always-visible grid scrollbars:** Replaced auto-hiding scrollbars on the Progress grid with always-visible, wider (14px) scrollbars using custom ControlTemplates for both vertical and horizontal orientations. Bottom padding prevents last row from being hidden by the horizontal scrollbar.
- **Themed scrollbar colors:** New theme variables `ScrollBarTrackColor`, `ScrollBarThumbColor`, `ScrollBarThumbHoverColor`, `ScrollBarBorderColor` added to all 4 themes. Thumb uses AccentColor per theme with 40% lighter hover effect.
- **Grid row header styling:** New `GridRowHeaderBackground` theme variable. Row selector column now matches `GridHeaderBackground` color across all themes.
- **Grid row hover background:** New `GridRowHoverBackground` theme variable replaces hardcoded `TextColorSecondary` for row hover. Light theme gets a readable light blue-gray (`#FFD0D8E0`) instead of the previous medium gray that made text hard to read.
- **Key files:** `Views/ProgressView.xaml` (ScrollBar templates), `Styles/RecordOwnershipRowStyleSelector.cs` (hover background), all 4 theme files (new variables)

### March 12, 2026 (Takeoff — Rate Application & CUT/Connection Fixes)
- **Rate application implemented:** New `Services/AI/RateSheetService.cs` loads embedded `Resources/RateSheet.json` (6,603 rate entries) and provides rate lookups. Component mapping dictionary translates our components to rate sheet EST_GRP keys (e.g., BEV→BEVEL, FSH→PIPE, FRH→SPOOL, valves→VLV, fittings→FTG, etc.). Fallback chain: try Thickness → try Class Rating → try size-only → missed.
- **STD↔S40 synonym:** If lookup with STD fails, automatically retries with S40 (and vice versa).
- **Schedule translation:** Numeric schedules auto-prefixed with "S" at lookup time (40→S40, 80→S80). WT values pass through as-is.
- **BudgetMHs populated on Labor tab:** `ApplyRates()` in TakeoffPostProcessor sets `BudgetMHs = Quantity × FLD_MHU` for each labor row. BU rates halved since each flanged end creates a BU row (2 rows per joint).
- **Missed Rates tab:** New Excel worksheet listing labor rows with no rate match, showing the lookup key attempted. Same styling as Missed Makeups tab.
- **CUT records require matching PIPE:** CUTs are only created when a PIPE entry exists on the same drawing with matching size and material. CUT inherits Thickness and Class Rating from the matching pipe. No pipe = no cut.
- **BEV unchanged:** Bevels still created for any BW connection regardless of pipe match.
- **Connection rows guaranteed thickness:** If source item has no thickness, checks matching PIPE entry, defaults to "40" if nothing found.
- **NIP and PLG excluded from connection explosion:** These components only create fab records, no connection/CUT/BEV rows.
- **Olet FTG lookup uses branch size:** Dual-size olets (e.g., "24x1") look up FTG rate using only the smaller/branch size.
- **S3 drawing cleanup:** Uploaded drawings deleted from S3 after processing completes.
- **Key files:** `Services/AI/RateSheetService.cs` (new), `Resources/RateSheet.json` (new), `Services/AI/TakeoffPostProcessor.cs`, `VANTAGE.csproj`

### March 12, 2026 (Takeoff — Results Panel Removal & Missed Makeups Reason Column)
- **Removed Results panel from TakeoffView:** The in-app results summary panel (status, expandable sections for drawings/connections/components) has been removed since the same information is available in the downloaded Excel file. Removed XAML panel, `ShowResults()`, `AddSummaryLine()`, `AddDetailLine()` methods, and all references.
- **Missed Makeups tab — Reason column:** Added a Reason column to the Missed Makeups Excel tab to distinguish between "No Makeup Found" (lookup attempted but no match in FittingMakeup.json) and "Unclaimed" (fitting had no matching pipe size on the drawing, so no lookup was attempted). This makes it easy to filter out unclaimed fittings and focus on actual lookup gaps.
- **Key files:** `Views/TakeoffView.xaml`, `Views/TakeoffView.xaml.cs`, `Services/AI/FittingMakeupService.cs`, `Services/AI/TakeoffPostProcessor.cs`

### March 12, 2026 (Takeoff — S3 Drawing Cleanup & Rate Application Plan)
- **S3 drawing cleanup after processing:** Uploaded drawings are now automatically deleted from S3 after takeoff processing completes (success or failure). Drawings were being overwritten on each run anyway (to support new revisions with same filename), so persisting them served no purpose.
- **Rate Application Plan:** Created `Plans/Rate_Application_Plan.md` with component-to-EST_GRP mapping table, schedule translation rules, and gap analysis TODO list. Rate sheet will be embedded as JSON resource (universal, not per-project).

### March 11, 2026 (Takeoff Post-Processing — FRH Records & Fitting Makeup)
- **FRH (Field Handling) records for PIPE items:** New FRH row generated on Labor tab for each PIPE BOM item. Quantity = pipe length (ft) + fitting makeup (inches converted to ft).
- **New file: `Services/AI/FittingMakeupService.cs`** — Loads `Resources/FittingMakeup.json` (2,078 entries, embedded resource) and provides lookup methods. Handles standard fittings (1x), TEE (3x), CROSS (4x), 90L/45L (2x), REDT (run+outlet), olets, and reducing fittings.
- **Fitting-to-pipe matching:** Groups material rows by Drawing Number. For each pipe, finds fittings on same drawing with matching size AND material. Olets parse dual size (e.g., "6x1") and match on the smaller size.
- **Olet lookup improvements:** Olets now use actual component names (WOL, SOL, TOL, ELB, LOL, NOL) instead of always SOL. Non-OLW connection type is extracted for lookup. Falls back to Thickness field when Class Rating is empty.
- **Class field changed to string:** `FittingMakeupEntry.Class` is now a string to support values like STD, XS, 160, 3000, 6000. ClassMatches does case-insensitive string comparison.
- **100 new olet entries added:** WOL (STD/XS/160), SOL (3000/6000), TOL (3000/6000), ELB (3000/6000), LOL (3000/6000), NOL (3000/6000 across BW/SW/THRD).
- **Exclusions list expanded:** Added FLGB, PIPET to components excluded from makeup lookup (also FS, GSKT, BOLT, WAS, HEAT, HOSE, INST, PLG, SAFSHW, F8B, DPAN, ACT).
- **Missed Makeups tab:** New Excel worksheet listing fittings that couldn't be looked up in the JSON table OR weren't claimed by any pipe. Includes LookupKey column showing connection type/component/size/class used for lookup.
- **Key files:** `Services/AI/FittingMakeupService.cs`, `Services/AI/TakeoffPostProcessor.cs`, `Resources/FittingMakeup.json`

### March 10, 2026 (Takeoff Post-Processing — Connection & Description Refinements)
- **ROCStep column added** to Labor tab (between ShopField and Confidence), empty for now.
- **Removed VLV rule:** Valve connections are no longer excluded. Specific valve component types (VBL, VGT, etc.) are used instead of generic VLV.
- **NIP connections excluded:** Connection rows are not created for NIP (nipple) connection types.
- **CUT rows restricted to BW and SW** connections only (previously created for all non-BU).
- **Connection size used for connections/CUT/BEV:** Size column and descriptions for connection, CUT, and BEV rows now use connection size from Material tab instead of component size. BOM fab records still use original size.
- **Connection description simplified:** Removed commodity code from connection row descriptions. Format is now `{connSize} IN - {thickness} - {pipeSpec} - {material} - {connType}`.
- **BOM fab record descriptions:** Now append commodity code to raw description (`{rawDesc} - {commodityCode}`).
- **Key file:** `Services/AI/TakeoffPostProcessor.cs`

### March 10, 2026 (Bulk Reassignment Timeout Fix)
- **Fixed SQL timeout on large bulk reassignments:** Added `CommandTimeout = 120` (2 minutes) to the ownership verification query and bulk UPDATE command in `MenuAssignToUser_Click`. Default 30-second timeout was exceeded when reassigning large numbers of records, causing "Execution Timeout Expired" errors.
- **Key file:** `Views/ProgressView.xaml.cs`

### March 9, 2026 (Takeoff Post-Processing â€” FSH Handling Records)
- **FSH records for PIPE items:** Each PIPE BOM item now generates one FSH (Fab Shop Handling) record on the Labor tab with raw description from Material tab. One record per BOM item regardless of pipe length/quantity.
- **Key file:** `Services/AI/TakeoffPostProcessor.cs`

### March 9, 2026 (Takeoff Post-Processing â€” Labor Tab Improvements)
- **CUT/BEV descriptions:** Fabrication rows now get proper dash-separated descriptions (`{size} IN - {thickness} - {pipeSpec} - {material} - CUT/BEVEL`) instead of inheriting the parent connection description.
- **Connection type moved to Component column:** Connection rows (BW, SW, BU, etc.) now use the Component column for the connection type instead of a separate Connection Type column. Connection Type column removed from Labor tab entirely.
- **BOM fab records added to Labor tab:** All non-PIPE BOM items (ELL, TEE, VLV, STUB, etc.) now generate a fab record on the Labor tab with original component and raw description from the Material tab.
- **Raw Description column removed from Labor tab:** Simplified Labor tab by removing the Raw Description column. BOM fab records carry the raw description in the Description column; connection/CUT/BEV rows use their built descriptions.
- **Multi title block regions testing confirmed:** Lambda deployed and end-to-end testing validated for multi title block region support.
- **Key file:** `Services/AI/TakeoffPostProcessor.cs`

### March 9, 2026 (AI Takeoff - Multi Title Block Regions)
- **Multi title block region support:** Users can now draw multiple boxes around different sections of a drawing's title block (e.g., PIPE INFO section, Project info section) to exclude noise like logos and revision history. All regions are sent as separate images to Claude, which extracts them into ONE unified `title_block` object.
- **Data model change:** `CropRegionConfig.TitleBlockRegion` (single) changed to `TitleBlockRegions` (list). Backward compatibility maintained via setter that auto-populates list when deserializing old configs with `title_block_region`.
- **Config Creator UI changes:** Removed replace logic that limited title block to one region. Labels now show "Title Block", "Title Block 2", etc. Save builds list with labels `title_block`, `title_block_2`.
- **Lambda changes:** Checks for `title_block_regions` (list) first, falls back to `title_block_region` (single). Crops each region separately with labels "Title block section 1", "Title block section 2". Prompt instructs Claude to combine all sections into single unified `title_block` object.
- **Key files:** `Models/AI/CropRegionConfig.cs`, `Dialogs/ConfigCreatorWindow.xaml.cs`, `Plans/AWS Agent/extraction_lambda_function.py`

### March 7, 2026 (Admin Snapshots & Query Optimization)
- **Fixed Progress Log upload tracking not saving:** Tracking records inserted by AdminSnapshotsDialog into `VMS_ProgressLogUploads` were silently failing due to a `using var` SqlDataReader not being disposed before subsequent INSERT commands on a non-MARS connection. Changed to block-scoped `using` to ensure reader disposal before tracking inserts. This caused uploads to not appear in ManageProgressLogDialog until REFRESH was clicked.
- **Upgraded AdminSnapshotsDialog grid to Syncfusion SfDataGrid:** Replaced basic WPF ListView with Syncfusion `SfDataGrid` matching ManageProgressLogDialog's style â€” column header filters, sorting, resizable columns, checkbox column for selection.
- **Optimized AdminSnapshotsDialog load query:** Combined two sequential Azure queries (snapshot groups + upload status check) into a single query with LEFT JOIN, eliminating a full round-trip and in-memory cross-referencing.
- **Added Azure index for VMS_ProgressSnapshots:** New covering index `IX_ProgressSnapshots_Group_Lookup` on `(AssignedTo, ProjectID, WeekEndDate) INCLUDE (ProgDate)` auto-created via `EnsureAzureIndexes()`. Speeds up GROUP BY queries in both AdminSnapshotsDialog and ManageSnapshotsDialog.
- **ObservableCollection for AdminSnapshotsDialog:** Switched `_groups` from `List<T>` to `ObservableCollection<T>` with proper `INotifyPropertyChanged` on `IsUploaded` property for live grid updates.

### March 7, 2026 (Theme Generator)
- **Theme Generator Script & Skill:** Created `Scripts/Generate-Theme.ps1` PowerShell script that generates a complete theme XAML (103 keys) from 4 hex colors (Primary, Accent, Secondary, Surface) + dark/light base. HSL color math for all derivations. Created `/create-theme` Claude Code skill (`.claude/skills/create-theme/skill.md`) that automates the full workflow: gather inputs, run script, register theme, build.
- **Dark Forest theme:** New dark green theme generated with the theme builder. Primary `#18230F`, Accent `#1F7D53`, Secondary `#27391C`, Surface `#255F38`. Registered in ThemeManager, ThemeManagerDialog, and help manual.
- **Independent highlight theme keys:** Decoupled 6 new theme keys from AccentColor so they can be tuned per theme without affecting other themes:
  - `ScanButtonForeground` â€” SCAN button text (was AccentColor)
  - `SummaryBudgetForeground` â€” Budget stat value (was AccentColor)
  - `SummaryEarnedForeground` â€” Earned stat value (was StatusGreen)
  - `SummaryPercentForeground` â€” % Complete stat value (was StatusInProgress)
  - `SidebarButtonHoverBorder` â€” Sidebar button hover border (was AccentColor)
  - `SidebarButtonHoverBackground` â€” Sidebar button hover background (new, didn't exist before)
- **Wired up GridCellBackground:** `RecordOwnershipRowStyleSelector` now applies `GridCellBackground` to even rows and `GridAlternatingRowBackground` to odd rows. Previously `GridCellBackground` was a reserved/unused key and even rows fell through to Syncfusion defaults.
- **Status button colors locked per base type:** Generator hardcodes Complete/In Progress/Not Started button colors from Dark or Light theme. Status buttons now look identical regardless of custom theme palette.
- **THEME_GUIDE.md rewrite:** Comprehensive update with generator script docs, status button locking rule, grid row background explanation, new key documentation, line endings note, and `/create-theme` skill reference.
- **CLAUDE.md â€” CRLF rule:** Added instruction requiring all generated files use CRLF line endings to prevent Visual Studio "Inconsistent Line Endings" dialog.

### March 6, 2026
- **Manager Role & User Table Consolidation:** Added Manager role allowing reassignment of any user's records (same as Admin for AssignTo only). Consolidated role management into Edit Users dialog with Admin/Estimator/Manager checkboxes. Role checks use fallback logic (new VMS_Users columns â†’ old VMS_Admins/VMS_Estimators/VMS_Managers tables) for backward compatibility during migration. Removed Toggle User Roles dialog and menu item. Added email notifications when user roles change. Key files: `AzureDbManager.cs` (IsUserAdmin/IsUserEstimator/IsUserManager with fallbacks), `AdminUsersDialog.xaml/.cs` (role checkboxes, dual-schema save logic, email notifications), `ProgressView.xaml.cs` (IsManager permission checks for AssignTo), `DatabaseSetup.cs` (CopyUsersTableFromAzure excludes role columns for security), `Models/User.cs` (IsManager property).
- **AI Takeoff module restricted to specific users:** Temporarily restricted Takeoff button visibility to users `steve` and `Steve.Amalfitano` only (case-insensitive). Added `IsTakeoffAllowed()` helper method in `MainWindow.xaml.cs`. Revert instructions documented in `Project_Status.md` under "Temporary Restrictions" section.
- **Help manual - Plugins section:** Added Section 11 (Plugins) with Plugin Manager documentation and PTP TFS MECH Updater plugin usage guide including file preparation steps.
- **Fixed encoding issues in Help manual:** Replaced mojibake characters (Ã¢â‚¬") with proper em-dashes throughout manual.html.
- **Removed Package URL column from Plugin Manager:** Removed GitHub URL column from Available tab to keep UI cleaner.

### March 5, 2026 (Plugin System Fixes)
- **Fixed missing Plugins config in appsettings.json:** Added `Plugins.IndexUrl` pointing to `plugins-index.json` in VANTAGE-Plugins repo. Plugin Manager was showing empty Available tab because the feed URL was not configured.
- **Added `RefreshProgressViewAsync()` to `IPluginHost`:** Plugins that modify activity data can now refresh the Progress view, summary stats, and metadata error count after import.
- **Fixed variable naming in `PluginInstallService.UninstallAsync`:** Renamed misleading `versionDir` to `pluginIdDir` with clarifying comment.
- **PTP TFS MECH Updater plugin v1.0.0 published:** First real plugin live on GitHub Releases feed. Imports PTP vendor shipping reports, creates/updates TFS Mechanical fabrication activities with change detection and ownership checks.

### March 5, 2026 (Plugin Execution Framework)
- **Plugin execution framework:** Added `IVantagePlugin` interface (Id, Name, Initialize, Shutdown) and `IPluginHost` interface (AddToolsMenuItem, MainWindow, ShowInfo/Error/Confirmation, logging). New `PluginLoaderService` loads plugin assemblies at startup, instantiates entry types, calls Initialize.
- **Dynamic menu item injection:** Plugins can add items to Tools menu via `host.AddToolsMenuItem(header, onClick, addSeparatorBefore)`. Menu items are cleaned up on shutdown.
- **Plugin type system:** Added `pluginType` field to `PluginManifest` and `PluginFeedIndex` ("action" for UI plugins, "extension" for passive features). Added `AssemblyFile` and `EntryType` to `InstalledPluginInfo`.
- **Removed Project Specific dialog:** Deleted `ProjectSpecificFunctionsDialog.xaml/.cs` and menu item â€” plugins now create their own UI dynamically.
- **MainWindow integration:** Plugin loading wired into `Loaded` event, cleanup in `Closing` event. Tools menu group named for programmatic access.

### March 5, 2026
- **Plugin Manager (settings menu):** Added `Plugin Manager...` in top-right `â‹®` settings popup. New dialog includes Installed and Available tabs, feed-backed plugin discovery, install from feed, uninstall, refresh, and status feedback.
- **Plugin feed architecture:** Added plugin feed models and services (`PluginFeedIndex`, `PluginFeedService`) using `Plugins.IndexUrl` in app config (`appsettings.json`, `AppConfig`, `CredentialService`).
- **Local plugin catalog:** Added recursive manifest scan service (`PluginCatalogService`) for installed plugins under `%LocalAppData%\\VANTAGE\\Plugins`.
- **Plugin install/uninstall service:** Added `PluginInstallService` for feed package download, optional SHA-256 validation, zip extraction, manifest validation, stale-folder cleanup, install copy, and uninstall delete flow.
- **Startup automatic plugin updates:** Added `PluginAutoUpdateService` and startup integration in `App.xaml.cs`. On startup, app checks installed plugins against feed versions, installs newer versions, and removes older versions of the same plugin ID.
- **Validation and hardening from live testing:** Fixed nested-zip manifest detection, stale partial install handling, manifest parse/required field checks, feed-to-manifest ID/version mismatch checks, and improved install error diagnostics (including attempted URL).
- **Implementation handoff doc:** Added `Plans/Plugin_Manager_Implementation_Guide.md` with full architecture, file-by-file changes, troubleshooting, and next steps for continuing development on another machine.

### March 4, 2026
- **Takeoff fabrication items (CUT/BEV) â€” initial implementation:** Added fabrication row generation to `TakeoffPostProcessor.cs`. Each non-BU connection gets 1 CUT child row; each BW connection gets 2 BEV child rows. Fabrication rows inherit parent fields with Component overridden to "CUT"/"BEV" and ShopField=1. Removed Item ID and Connection Qty from Labor tab columns. WIP â€” descriptions and column layout still need refinement.

- **UI styling improvements across Takeoff and Config Creator:** Added Cancel button to Config Creator dialog. Applied SidebarButtonStyle (from ProgressView) to Delete Config, Cancel, and Save Config buttons with status colors (red/amber/green). Converted BOM Region and Title Block mode toggles from ButtonAdv to standard Buttons to fix Syncfusion theme interference with programmatic color changes. Removed window-level SfSkinManager (applied per-control instead). Applied Unsynced button styling (SidebarButtonBorder + drop shadow) to Edit, Refresh, Select Files in TakeoffView and Load Drawing, Undo Last, Clear All in ConfigCreatorWindow. Changed Process Batch button to green status colors. Changed "Mode:" label to "Draw:" in Config Creator toolbar.
- **Nav toolbar cleanup:** Converted all 6 navigation buttons from Syncfusion ButtonAdv to standard WPF Buttons to fix left-padding asymmetry from ButtonAdv's internal icon column. Removed hardcoded Width="100" from underline borders (now stretch to button width). Added consistent Margin="10,0" on Grid wrappers for even gaps between nav labels.
- **Takeoff integration guide cleanup:** Rewrote summit-takeoff-integration-guide.md to reflect current implementation state. Removed outdated code samples, corrected NuGet versions (v4), S3 paths, credential storage approach. Added key implementation files table and known gotchas.
- **Takeoff post-processing pipeline in backlog:** Replaced vague "Import Takeoff to Create Records" backlog item with explicit 6-step pipeline: fabrication item generation (cuts/bevels/handling), rate sheet upload, rate application, ROC splits, VANTAGE tab, fitting makeup table.
- **AI Takeoff auto-download and Previous Batches:** Removed Download Excel button â€” Excel now downloads automatically when batch processing completes (Save As dialog â†’ post-processing â†’ opens file). Added Previous Batches dropdown to re-download results from past batches. Batch metadata (username, config name, drawing count, timestamp) stored in S3 `metadata.json`. Removed test button. Fixed UTC time display for old batches without metadata. Key changes: `TakeoffService.cs` (WriteMetadataAsync, ListBatchesAsync, BatchInfo class), `TakeoffView.xaml/.cs`.
- **AI Takeoff post-processor wired to Download Excel:** `BtnDownload_Click` now calls `TakeoffPostProcessor.GenerateLaborAndSummary()` after downloading the raw AWS output, generating Labor and Summary tabs automatically.
- **AI Takeoff post-processor for Labor and Summary tabs:** New `Services/AI/TakeoffPostProcessor.cs` generates Labor and Summary tabs from AWS takeoff output. Labor tab explodes material rows by quantity Ã— connections (VLV rules apply â€” excludes THRD/BU connections). Summary tab displays statistics in two-column format with green section headers: total drawings, BOM items, connections by type/size, components by type, and connections by drawing. Tab order: Summary, Material, Labor, Flagged. Includes test button for development iteration (to be removed once wired to download).
- **AI Takeoff UI polish and theme compliance:** Reworked `TakeoffView.xaml` action buttons from `syncfusion:ButtonAdv` to standard WPF `Button` with `RoundedButtonStyle` and `DropShadowEffect` to match app conventions. Widened Select Files button (70â†’90). Added explicit theme bindings (`ControlBackground`, `ForegroundColor`, `BorderColor`) to Edit, Refresh, Select Files buttons. Fixed hardcoded `Foreground="White"` â†’ `DynamicResource ActionButtonForeground`. Fixed code-behind `Brushes.Orange` â†’ `FindResource("WarningText")` for warning messages.
- **Log retention policy update:** App startup purge window reduced from 30 days to 15 days for AppLogger file logs, AppLogger database log entries, and ScheduleChangeLogger JSON logs. This is now enforced by default purge settings at startup. - Codex
- **Snapshot retention verified:** `VMS_ProgressSnapshots` maintenance remains event-driven in submit flow and continues to purge any rows older than 28 days globally (not user-scoped). - Codex

### March 2, 2026 (Config Creator)
- **AI Takeoff â€” Config Creation UI:** New `ConfigCreatorWindow` (maximized modal) for creating and editing crop region configs. Load a PDF drawing, draw BOM regions (green) and Title Block region (orange) as rectangles on a canvas overlay, save to S3. Key files: `Dialogs/ConfigCreatorWindow.xaml/.cs`, `Models/AI/CropRegionConfig.cs`.
  - **PDF preview with rectangle drawing:** Renders PDF page 0 via `PdfToImageConverter` at 150 DPI. Mouse drag creates rectangles with percentage-based coordinates that survive window resize. BOM mode allows multiple regions; Title Block mode replaces existing.
  - **Create/Edit/Delete configs:** "Create New Config..." as first combo item opens creator. Edit button loads existing config from S3, downloads a drawing for preview, overlays saved regions. Delete button (edit mode only) removes config JSON and associated drawings from S3 with confirmation.
  - **Username-based S3 folders:** Config naming changed from separate client/project fields to single "Config Name" field. S3 path: `clients/{username}/{config-name}.json`, drawings: `{username}/{config-name}/`. Each user gets their own folder.
- **TakeoffView layout improvements:** Narrowed top config section to half-width using Grid column layout. Removed Batch ID row (internal only). Added Edit button. Switched toolbar buttons to standard WPF Button style. File selection shows count in field, filenames in status box.
- **Failed execution handling:** When Step Functions execution succeeds but app-level output status is "failed", Download Excel button is now hidden and status explains no Excel was generated. Results panel shows warning about partial/cached data.
- **TakeoffService additions:** `SaveConfigAsync`, `GetConfigAsync`, `DeleteConfigAsync`, `DownloadDrawingToTempAsync`. Null-guard on `ListConfigsAsync` for empty S3 buckets (AWS SDK v4 nullable `S3Objects`).

### March 2, 2026
- **AI Takeoff Module â€” Full integration:** New TAKEOFFS nav module integrating the AWS-based Summit Takeoff pipeline (Step Functions + Lambda + Bedrock Claude Vision) into VANTAGE. Uses AWS SDK direct (AWSSDK.S3 + AWSSDK.StepFunctions) with dedicated IAM user (`vantage-takeoff-user`), not shared Textract credentials. Key files: `Services/AI/TakeoffService.cs`, `Views/TakeoffView.xaml/.cs`.
  - **Config selection:** Loads crop region configs from S3 (`summit-takeoff-config/clients/`), Syncfusion ComboBoxAdv dropdown with refresh button.
  - **File upload + batch processing:** Multi-file PDF picker, uploads to config-based S3 prefixes (`{client_id}/{project_id}/filename.pdf`), overwrites existing files (latest rev wins). Starts Step Functions execution, polls for completion with elapsed timer, displays parsed results summary.
  - **Results UI:** Parsed JSON summary with expandable sections â€” Drawings Processed, Connections by Type/Size, Components by Type, Connections by Drawing. Replaces raw JSON output.
  - **Excel download:** Downloads output Excel from S3 processing bucket via SaveFileDialog.
  - **Manage Drawings dialog:** `Dialogs/ManageDrawingsDialog.xaml/.cs` â€” Browse and delete S3 drawings per config prefix. ListView with File Name, Size, Last Modified columns. Multi-select delete with confirmation. Available to estimators.
- **Estimator role system:** New VMS_Estimators Azure SQL table (EstimatorID IDENTITY, Username, FullName, DateAdded). `User.IsEstimator` property, `AzureDbManager.IsUserEstimator()` method. TAKEOFFS nav button visible only to estimators (not admin-gated).
- **Toggle User Roles dialog:** Extracted inline admin toggle from MainWindow (~250 lines) into proper `Dialogs/ToggleUserRolesDialog.xaml/.cs`. Shows users with role tags (ADMIN, ESTIMATOR). Toggle Admin + Toggle Estimator buttons. Email notification on role change. `RoleChanged` event for live MainWindow UI updates.
- **AppConfig + CredentialService for Takeoff:** `TakeoffConfig` class in `Models/AppConfig.cs` with AccessKey, SecretKey, Region, StateMachineArn, DrawingsBucket, ProcessingBucket, ConfigBucket. Corresponding `CredentialService.Takeoff*` static property accessors.


---

**Archives:** See Plans/Archives/ for previous months.