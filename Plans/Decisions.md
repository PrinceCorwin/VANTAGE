# VANTAGE: Milestone — Design Decisions

Permanent record of architectural choices, design rationale, and implementation decisions. Consult this when asking "why did we do X?" before changing existing behavior.

---

## Data Model

### Dates Stored as TEXT, Not DATETIME
**Decision:** All date columns use TEXT/VARCHAR in both SQLite and Azure SQL.
**Why:** P6 Primavera exports dates as text strings. Using TEXT avoids conversion issues and format mismatches during import/export cycles.

### ActStart/ActFin Are Required Metadata, Not Auto-Populated
**Decision:** Removed auto-set behavior. ActStart required when % > 0, ActFin required when % = 100. Red cell highlights and sync blocking enforce compliance.
**Why:** Auto-populating dates was silently overriding user intent. Explicit control ensures data completeness without surprises.
**Date:** February 2026

### AzureUploadUtcDate Is Pull-Only
**Decision:** Removed from SyncManager push columns. Admin sets value on Azure during upload; users receive on pull but cannot overwrite.
**Why:** Prevents users from accidentally corrupting the upload timestamp that only admins should set during Progress Log upload.
**Date:** January 2026

### V2 Data Model: ClientEarnedEquivQty Deferred
**Decision:** Column exists in OldVantage (`VAL_Client_Earned_EQ-QTY`) but is ignored during import. Will be added to Activities, Azure VMS_Activities, and ColumnMappings in V2.

---

## Sync

### Push Failure Blocks Pull to Protect Local Data
**Decision:** If push to Azure fails, the pull is skipped entirely. Local dirty records are preserved for retry.
**Why:** Previously, push failures were silent — the pull ran anyway, overwriting local changes with old Azure data and clearing LocalDirty flags. Users saw edits revert with no error shown.
**Date:** April 2026

### Ownership Check Removed from Sync, Moved to Tools Menu
**Decision:** Removed `CheckSplitOwnershipAsync` (JOIN on SchedActNO across 65K+ records) from the sync path. Created standalone "ActNO Split Ownership Check" tool under Tools menu.
**Why:** The query had no index and caused timeouts, blocking syncing entirely. The check is useful but doesn't need to run on every sync.
**Date:** April 2026

### Push Verifies Actual Rows Updated via OUTPUT INTO
**Decision:** The Azure UPDATE in push Step 3 uses `OUTPUT INSERTED.[UniqueID] INTO #UpdatedIds` to capture exactly which rows were updated. Only confirmed rows get LocalDirty cleared. Previously, all records were blindly marked as successfully pushed regardless of actual update count.
**Why:** If the UPDATE affected fewer rows than expected (transient Azure issue, staging table mismatch), unverified records had LocalDirty cleared and the pull overwrote local data with old Azure values. Users saw pasted values revert after sync with no error. Now missed rows keep LocalDirty=1 and retry on next sync.
**Date:** April 2026

### Unlimited SQL Timeouts in Sync Flow
**Decision:** All CommandTimeout and BulkCopyTimeout values in push/pull set to 0 (unlimited). Azure ConnectTimeout also 0.
**Why:** Users with slow internet were hitting 30-second default timeouts, causing "Execution Timeout Expired" errors. The sync operations must complete regardless of connection speed — there's no benefit to aborting partway through.
**Date:** April 2026

### LastPulledSyncVersion Tracks Pulled Records, Not Live MAX
**Decision:** After pulling, use the max SyncVersion from records actually pulled — not a separate query for current MAX.
**Why:** Race condition — if another user pushed between the pull query and the MAX query, those records would be permanently skipped on future pulls.
**Date:** April 2026

### Submit Week Snapshots from Local Data, Not Forced Sync
**Decision:** Removed forced sync and split ownership check from Submit Week. Snapshots capture local data directly.
**Why:** Enables historical snapshot scenarios (e.g., restore a backup and snapshot that point-in-time state). Faster submits with fewer network round-trips.
**Date:** February 2026

---

## Takeoff Pipeline

### CUT/BEV Folded into Connection Rows, Not Separate Labor
**Decision:** CUT and BEV no longer generate separate labor rows. BW connections include CUT+BEV rate additions, SW/THRD include CUT.
**Why:** Cut/bevel are always performed with the connection. Folding them in prevents row explosion in the Labor tab and matches how labor is actually performed.
**Date:** March 2026

### GSKT/BOLT Excluded from Labor Generation
**Decision:** GSKT and BOLT material items no longer create any labor records.
**Why:** These are material-only items. Their labor is accounted for in the BU (bolt-up) connection rows.
**Date:** March 2026

### Rate Lookup: Simplified 4-Step Fallback Chain
**Decision:** Removed OLW/SW class rating tier fallback system. New chain: try thickness as-is → toggle leading "S" → try class rating → try size-only.
**Why:** The tier system (40/S40/STD/2000 equivalence groups) was complex. The S-toggle approach handles the same edge cases more simply. Dual-size parsing for all components further reduced misses.
**Date:** March 2026

### Rate Sheet Keys Match Component Names Directly
**Decision:** Renamed all 56 EstGrp keys to short names. Components like INST, PIPE, SPL, BEV match rate keys directly. Only valve types (VBL/VGT→VLV) and fittings (90L/TEE→FTG) need the mapping dictionary.
**Why:** Eliminates indirection. The `DirectMatchComponents` set was redundant when `ResolveEstGrp` can just use the component name as the key.
**Date:** March 2026

### BudgetMHs Formula: RateSheet × RollupMult × MatlMult
**Decision:** Changed from `RateSheet × RollupMult × max(RollupMult, MatlMult)` to simple multiplication.
**Why:** The max() approach double-counted the rollup multiplier when it exceeded the material multiplier. Simple multiplication is more transparent and matches industry conventions.
**Date:** March 2026

### FS Material Group Auto-Corrected from Pipe Material
**Decision:** Field support (FS) `Matl_Grp` values are auto-corrected to match the pipe of the same size on the same drawing.
**Why:** AI often defaults FS to CS (carbon steel) because material isn't in the FS description. When multiple pipe materials exist, picks the non-CS value since CS is the assumed default.
**Date:** April 2026

### ShopField Post-Processing: Lambda Sets All to Shop, Post-Processor Corrects
**Decision:** Lambda sets all material rows to ShopField=1 (Shop). Post-processor corrects to Field (2) for: BU/SCRD-only connections, inherently field components (FS/BOLT/GSKT/WAS/INST/GAUGE), and items with zero connections.
**Why:** Simpler Lambda logic. Correction rules are data-driven and easier to maintain in the post-processor. PIPE always stays Shop. Mixed connection types stay Shop.
**Date:** April 2026

### Send Missed to Admin: Default Unchecked, No Persistence
**Decision:** "Send Missed Makeups and Rates to Admin" checkbox defaults to unchecked on every tab load. No saved preference.
**Why:** Users were unintentionally emailing admins on every batch because the checkbox persisted as checked. Default-off makes the email an intentional opt-in action each time.
**Date:** April 2026

### S3 Drawings Deleted After Processing
**Decision:** Uploaded drawings automatically deleted from S3 after takeoff processing completes.
**Why:** Drawings were being overwritten on each run anyway (to support new revisions with same filename), so persisting them served no purpose.
**Date:** March 2026

### Config Naming by Username, Not Client/Project
**Decision:** Config S3 path uses `clients/{username}/{config-name}.json` instead of separate client/project fields.
**Why:** Each user gets their own namespace. Simpler than requiring a client/project hierarchy for configs.
**Date:** March 2026

### In-App Results Panel Removed, Excel Is the Deliverable
**Decision:** Removed the in-app results summary panel from TakeoffView.
**Why:** The same information is available in the downloaded Excel file. The in-app panel was redundant.
**Date:** March 2026

### MakeupEquiv Two-Pass Lookup
**Decision:** `MakeupEquiv` dictionary (ADPT→FLG, FLGR→FLG) with two-pass lookup: direct match first, then equivalent component.
**Why:** Performance — most lookups succeed on direct match. Two-pass also makes the lookup hierarchy explicit and predictable.
**Date:** March 2026

### ROC Splits Read Raw Takeoff Data, Not Mapped Activity Properties
**Decision:** `ApplyROCSplitsAsync` reads ShopField and Component directly from the raw takeoff Excel row data instead of from the mapped Activity object properties (UDF1/UDF6).
**Why:** Users configure column mappings in their import profile — ShopField might map to `UDF1`, `ShopField`, or be unmapped entirely. Hardcoding to `activity.UDF1` made ROC splits silently fail if the mapping didn't match. Reading from raw data makes the feature work regardless of mapping configuration. Also added text value support ("Shop"/"Field") alongside numeric ("1"/"2").
**Date:** April 2026

---

## VP vs Vtg Report

### JC Cost Code: Exact Match After Outer-Whitespace Trim Only
**Decision:** ProjectID and PhaseCode matching between the JC Labor Productivity report and Vantage `VMS_Activities` uses `String.Trim()` on both sides (stripping only leading and trailing whitespace), then compares as an exact string. No leading-zero stripping, no internal whitespace collapsing, no trailing-separator trimming. The previous `NormalizeKey` helper was removed.
**Why:** Reversed an earlier decision that normalized both sides to match cosmetic variants like `26.001.001` ↔ `26.1.1`. The normalization was hiding a real data-quality problem: Vantage phase codes should match VP's canonical format exactly. "Not Found" is now a useful signal — it tells the user which Vantage records have drift from VP and need their codes corrected to match VP. Outer-whitespace trim is the only concession, because Excel cell formatting and SQL CHAR padding can introduce leading/trailing spaces that aren't user-meaningful differences; everything else (internal spaces, digits, punctuation, zero-padding) must match character-for-character or the row is reported as `Not Found`.
**Date:** April 2026 (reversed same month it was introduced)

### Color Coding Scoped to Added Columns Only
**Decision:** Only the two generated columns (`Vtg Budget`, `Vtg Earned`) receive conditional fill (green within 1%, red over 1%, orange `Not Found`). The companion Excel columns (`Est Hours`, `JTD ERN`) are left untouched.
**Why:** Initial implementation paired the red fill across both cells of a mismatch. User preferred minimizing modifications to the source report and keeping the visual signal scoped to the Vantage-sourced values. Every data row gets a color on the two new columns so mismatches are never ambiguous with "not yet checked".
**Date:** April 2026

### Prep Dialog + Native File Picker, Not a Custom Combined Picker
**Decision:** Before the `OpenFileDialog`, show a custom WPF verification dialog (`VPvsVtgPrepDialog`) with instructions and an annotated screenshot. On OK, the standard Windows file picker opens. Two separate dialogs, not one custom combined picker.
**Why:** Considered building a custom file browser to bundle the prep instructions into one screen. Rejected — the native `OpenFileDialog` is a Windows OS control users already understand, and replicating it would be significant work with no payoff. The only thing the OS picker can't do is show an image or rich text; a small pre-dialog is the right tool for that single gap.
**Date:** April 2026

### Prep Dialog Skip Flag Persists to UserSettings, Not AppSettings
**Decision:** The "Do not show this dialog again" checkbox writes `SkipVPvsVtgPrepDialog=true` to the `UserSettings` table (per-user), not `AppSettings` (app-wide).
**Why:** The prep step is a user-learning concern — once a user has read the instructions, they don't need to see them again. Other users on the same machine haven't necessarily learned yet. User-scoped persistence mirrors how other dismissible UI state is stored (grid layouts, filter selections, analysis group field, etc.).
**Date:** April 2026

### Trust the Admin on Snapshot Re-Upload (Not Implemented, Decision Logged)
**Decision:** Deferred adding a WeekEndDate override to the Admin Snapshots upload dialog. If users want to re-upload an older unchanged snapshot under a new week, they instead take a fresh snapshot.
**Why:** Considered adding an override so admins could reuse old snapshots for weeks where nothing changed, avoiding re-snapshotting closed-out activities. User ultimately judged the complexity not worth the bug surface for the small workflow gain, and preferred to keep the current simple invariant (WeekEndDate = when the snapshot was taken). Filed here so the conversation doesn't get re-litigated if the idea comes up again.
**Date:** April 2026

---

## Schedule Module

### P6 Current Schedule Dates, Not Baseline Dates
**Decision:** P6 import maps `start_date`/`end_date` (current schedule) instead of `target_start_date`/`target_end_date` (baseline).
**Why:** 3WLA requirement logic and missed start/finish reasons need current schedule dates, not stale baselines.
**Date:** February 2026

### 3WLA Dates Stored in Activities Table, Not Separate Table
**Decision:** Simplified from separate `ThreeWeekLookahead` table to `Activities.PlanStart/PlanFin` columns.
**Why:** Pre-populated from MIN/MAX of plan dates per SchedActNO. Persists across P6 imports. Eliminates separate table management.
**Date:** February 2026

### MissedReasons Are Session-Only, Not Persisted
**Decision:** MissedReasons stored in Schedule table, cleared on P6 import.
**Why:** Only required for P6 dates within the current week. Persisting required complex stale-detection logic for when underlying dates changed.
**Date:** February 2026

### Local SQLite Mirror for Snapshot Data
**Decision:** Schedule module reads from a local 12-column mirror instead of Azure's 89-column table. Azure stays authoritative; edits write through to both.
**Why:** Eliminated long lag on master/detail grid interactions. Local mirror self-heals on P6 import. Trimmed to 12 columns because that's all the Schedule module reads.
**Date:** April 2026

### MS Not In P6 Report: Per-User Only
**Decision:** After conversion to local mirror, report shows only current user's data (was previously all users).
**Why:** Matches the rest of the Schedule module's per-user filtering and is more intuitive for a per-user export.
**Date:** April 2026

### Snapshot Retention: 21 Days
**Decision:** Submit-time purge uses 21-day retention, not 28.
**Why:** Reduced data volume while still covering 3 full weekly cycles.
**Date:** April 2026

### Schedule Module: Dynamic Per-Cell Save, No SAVE Button
**Decision:** Removed the explicit SAVE button from the Schedule module. Every master-grid cell commit (Missed Reasons, lookahead Start/Finish, and cell-clear via Delete/Backspace) now saves immediately via a new `ScheduleRepository.SaveScheduleRowAsync(row, username)` — per-row equivalent of the old `SaveAllScheduleRowsAsync`, wrapping the single Schedule-row update plus the PlanStart/PlanFin bounds update scoped to that one SchedActNO. The `NotifyActivitiesModifiedAsync` callback (which reloads the Progress grid) is debounced with a 1-second trailing `DispatcherTimer` so rapid editing doesn't hammer the 100k+ row reload path. `HasUnsavedChanges`, the exit prompt, the "save first" export gate, and `SaveAllScheduleRowsAsync` are all deleted. Detail-grid edits were already auto-saving — the refactor brings the master grid up to that same pattern.
**Why:** The SAVE button introduced a bug class: clicking Refresh while edits were pending silently discarded them. Eliminating the unsaved state eliminates that bug by design and matches the Progress module's long-standing pattern.
**Date:** April 2026

### Lookahead Window Is User-Configurable (3 / 6 / 9 Weeks)
**Decision:** The Schedule lookahead window is selectable per-user via a ComboBox in the Schedule toolbar (3WLA / 6WLA / 9WLA; default 3WLA). Stored in UserSettings as `Schedule.LookaheadWeeks`. A single static `ScheduleMasterRow.LookaheadDays` is the source of truth for the 21-/42-/63-day threshold consumed by `IsThreeWeekStartRequired` / `IsThreeWeekFinishRequired`. Property and DB column names like `ThreeWeekStart` / `ThreeWeekFinish` are retained — the "3" is historical, not structural. Excel import/export file formats are unchanged; only in-app UI strings, the `AddDays(…)` highlighting threshold, and the Schedule Reports worksheet name + filename + dialog title adapt.
**Why:** Different trades and project phases need different forecast horizons. Hardcoding 21 days forced everyone into the same window. Per-user persistence lets each user set their own preference without affecting teammates.
**Date:** April 2026

---

## AI / Progress Scan

### Textract over Claude Vision for OCR
**Decision:** Switched from Claude Vision API to AWS Textract for table extraction.
**Why:** Textract provides proper table structure with row/column indices, yielding 100% accuracy. Claude Vision had inconsistent accuracy between PDF and JPEG. Tool Use (function calling) was tried first but Textract's native table detection was superior.
**Date:** January 2026

### ActivityID over UniqueID for OCR Identifier
**Decision:** Progress Book uses ActivityID (integer) instead of UniqueID (long string) as the record identifier.
**Why:** Shorter values are more reliable for OCR from scanned handwritten pages.
**Date:** January 2026

### Eliminated Checkboxes, "Write 100" Means Done
**Decision:** Removed Done checkbox concept entirely. Writing "100" in the % ENTRY box means done.
**Why:** Simplified the scan form and improved AI accuracy by reducing distinct columns to parse. Color-coded entry fields were also removed — AI relies on text labels, not colors.
**Date:** January 2026

### Sidebar AI Assistant Tab Shelved
**Decision:** Removed the two-tab (Help / AI Assistant) layout from the Help sidebar and deleted the AI tab scaffolding (button, placeholder content grid, tab-switching code in view and view-model). Kept `Plans/Sidebar_AI_Assistant_Plan.md` in the repo.
**Why:** The AI chat feature was never wired past a "Coming soon" placeholder and had no roadmap date. Carrying the empty tab in the UI misled users into thinking the feature was imminent, and the tab-switching code was dead weight. Shelving is preferred over leaving stubs; plan doc retained in case the feature is revived later.
**Date:** April 2026

---

## Edit Rules & Bulk Operations

### Hard Rules vs. Required Metadata
**Decision:** Activity date/percent rules are split into two tiers. Hard rules (future dates, ActFin before ActStart, ActStart set with %=0, ActFin set with %&lt;100) block the edit and revert. Required metadata (ActStart needed when %&gt;0, ActFin needed when %=100) does NOT block — the cell is flagged red and sync is gated.
**Why:** Treating "required metadata" as a hard block creates deadlocks when raising a record's progress before dates are known, or when bulk-setting % to 100 on records that still need ActFin. Users expressed frustration with being unable to move records forward. Red highlighting + sync gate still enforces data completeness, without blocking the in-progress edit.
**Date:** April 2026

### Single Validator as Source of Truth (`ActivityValidator.Validate`)
**Decision:** All edit paths — single-cell CurrentCellEndEdit, Find &amp; Replace, and both multi-row paste flows — call `ActivityValidator.Validate(percent, actStart, actFin)` to check the prospective state. The function returns the first violation message or null.
**Why:** Previously the same rules were re-implemented in three places and drifted. Centralising prevents new bulk paths from forgetting a rule.
**Date:** April 2026

### Bulk Operations Abort on Any Violation, Not Partial Apply
**Decision:** When Find &amp; Replace or a multi-row paste encounters a hard-rule violation on <em>any</em> affected row, the entire operation is rolled back in memory, nothing is written to the DB, and a dialog lists up to 10 offending ActivityIDs plus a "…and N more" footer.
**Why:** Silent partial apply (paste some rows, skip others) was reported as confusing — users couldn't tell what actually changed. Abort-on-any forces the user to correct the source data and re-run, which is more predictable than picking which rows to commit.
**Date:** April 2026

### Entering ActFin Auto-Bumps % Complete to 100
**Decision:** Single-cell edit, Find &amp; Replace, and paste all auto-set `PercentEntry = 100` on any row where a non-null Finish date was entered and current % is below 100.
**Why:** Users always have to follow a Finish-date entry with a %=100 update (ActFin requires %=100). The auto-bump saves a step. If ActStart is null when ActFin is entered, the record is still written — ActStart turns red as required metadata, consistent with the hard-rule / required-metadata split.
**Date:** April 2026

### Filter Does Not Auto-Refresh After Edits
**Decision:** Removed every `View.Refresh()` call that fired after a data mutation (paste success and rollback, Find &amp; Replace success, Prorate, single-cell rollback, Undo/Redo, ClearCurrentCell). Filters only re-evaluate when the user clicks a filter toggle or the Refresh button. Grid set to `LiveDataUpdateMode="Default"` so Syncfusion also doesn't re-shape data on property changes.
**Why:** Users reported frustration at rows silently disappearing from a filtered view the moment their edit made the row no longer match (e.g., raising % to 100 while viewing "In Progress"). They want to see the value they just changed before deciding to re-apply the filter. `INotifyPropertyChanged` on `Activity` still propagates value changes to grid cells; only the filter predicate stays stale until an explicit refresh.
**Date:** April 2026

### Paste Into ActStart/ActFin Validates Instead of Silently Skipping Rows
**Decision:** Removed the pre-filter that skipped rows failing the date-percent rules during multi-cell paste (both "single value to multiple rows" and "multi-value paste" flows). Paste is now abort-all-on-any-violation with a detailed error dialog.
**Why:** The silent-skip behavior (with a post-paste "N rows skipped" message) let users think a paste succeeded broadly when significant portions were dropped. Consistent abort-all semantics match Find &amp; Replace and single-cell edits.
**Date:** April 2026

---

## UI / UX

### ProgressView Cached for Instant Navigation
**Decision:** Cache the ProgressView instance in MainWindow and reuse on subsequent navigations.
**Why:** First load unchanged, but every subsequent navigation is instant. Force-reloads only on Excel import and Reset Grid Layouts.
**Date:** February 2026

### DIY Summary Panel Instead of Syncfusion TableSummaryRow
**Decision:** Replaced Syncfusion's `TableSummaryRow` with custom toolbar summary panel.
**Why:** TableSummaryRow was too slow on large datasets. DIY panel uses cached `PropertyInfo` lookups and 200ms debounce.
**Date:** February 2026

### DynamicResource + Role-Based Theme Token Names
**Decision:** Converted ~1,119 StaticResource refs to DynamicResource. Renamed resources to role-based names (e.g., `ToolbarForeground`, `GridHeaderForeground`).
**Why:** Enables live theme switching without app restart. Role-based names support future themes beyond Dark/Light.
**Date:** February 2026

### Custom Grid Filter Icons
**Decision:** Replaced Syncfusion's built-in FilterToggleButton with custom stroke-based funnel icons.
**Why:** Syncfusion's internal filter icon colors are resolved from compiled BAML and cannot be overridden via resource dictionaries. Custom template was the only option for theme-aware icons.
**Date:** February 2026

### Double-Click to Sort Grid Columns
**Decision:** All grids require double-click on column headers to sort.
**Why:** Prevents accidental sorting when clicking headers — users were inadvertently resorting data.
**Date:** February 2026

### Column Settings Graceful Schema Migration
**Decision:** Column preferences apply to matching columns when schema changes, rather than being fully rejected on hash mismatch.
**Why:** Previously, adding or removing any column discarded all user column preferences. Now new columns appear at end with defaults, removed columns are ignored.
**Date:** February 2026

### PercentEntry: Custom GridTemplateColumn with Progress Bar
**Decision:** Uses a custom `GridTemplateColumn` with progress-bar overlay instead of native `GridNumericColumn`.
**Why:** Enables the thin colored progress bar in each cell. Trade-off: decimal handling, arrow key navigation, and auto-edit-on-type all had to be hand-coded.

### PercentEntry Edit: LostFocus Trigger, Not PropertyChanged
**Decision:** EditTemplate TextBox binding uses `UpdateSourceTrigger=LostFocus` instead of `PropertyChanged`.
**Why:** PropertyChanged triggered the setter on every keystroke, running clamp/round/multi-PropertyChanged chains. This raced against input, causing `0.5` to become `5`. LostFocus commits only when editing ends.
**Date:** April 2026

### Notification Sounds Removed from Informational Dialogs
**Decision:** Changed ~90 `MessageBoxImage.Information` instances to `MessageBoxImage.None`.
**Why:** Windows notification sounds were disruptive and unnecessary for informational confirmations.
**Date:** February 2026

### Clone Buttons Removed, Save-As Pattern Instead
**Decision:** Removed Clone from WP Templates, Form Templates, and Prog Books layouts. To copy, change the name and save.
**Why:** Simplified template management. Clone required a naming dialog. Save-with-new-name is simpler and matches common patterns.
**Date:** February 2026

### Custom Scrollbar Templates over Syncfusion Defaults
**Decision:** Always-visible 14px custom scrollbar templates with theme-aware colors, replacing auto-hiding scrollbars.
**Why:** Auto-hiding scrollbars were hard to find and interact with. Custom `ScrollViewer` template avoids implicit style leaking into ComboBoxAdv dropdown internals.
**Date:** March-April 2026

### SfSkinManager.SetTheme in Constructor, Not Loaded Event
**Decision:** Theme application must happen in the constructor, before the control is in the visual tree.
**Why:** When applied in Loaded on a second instance, Syncfusion's theme engine interfered with SfDataGrid rendering, causing the grid not to display data.
**Date:** April 2026

---

## Architecture

### Auto-Update: Host-Agnostic Manifest-Based System
**Decision:** Custom auto-updater checking `manifest.json` with SHA-256 verified ZIP downloads and a separate Updater console app.
**Why:** Works with GitHub raw URLs now, can switch to Azure Blob by changing one URL. Graceful failure if offline. Self-contained publish means users don't need .NET runtime.
**Date:** January 2026

### Credentials: Encrypted Config over Compiled Constants
**Decision:** Replaced `Credentials.cs` with `CredentialService.cs` reading `appsettings.json` (dev) or AES-256 encrypted `appsettings.enc` (production).
**Why:** Published builds carry credentials without embedding them in source code. Publish script handles encryption automatically.
**Date:** February 2026

### Schema Migrations: Formal Versioned System
**Decision:** `SchemaMigrator.cs` with numbered sequential migrations, replacing ad-hoc column checks.
**Why:** Local DB contains user data that can't be deleted. Migrations must be idempotent and backward-compatible. Failed migrations offer to delete local DB and re-sync. Formal versioning prevents missed or double-applied changes.
**Date:** February 2026

### Plugin Architecture: Dynamic Menu Injection
**Decision:** Plugins inject their own Tools menu items via `host.AddToolsMenuItem()`. Each plugin has its own assembly loaded at startup with `IVantagePlugin` interface.
**Why:** Plugins create their own UI dynamically. Replaced the static `ProjectSpecificFunctionsDialog`. Auto-update checks installed plugins against a feed index on startup.
**Date:** March 2026

### Import Format Auto-Detection by Column Headers
**Decision:** Single import detects Legacy vs NewVantage format by column headers (`UDFNineteen` = Legacy, `UniqueID` = NewVantage).
**Why:** Previous threshold-based percent detection (1.5 threshold to guess 0-1 vs 0-100 format) caused edge cases. Column headers are definitive.
**Date:** January 2026

### Drawings Module Deferred to Post-V1
**Decision:** Disabled with `Visibility="Collapsed"` and code filters. Re-enable instructions documented in Project_Status.md.
**Why:** Per-WP drawing location architecture needs design (token paths, per-WP config, or Drawings Manager). The feature works but the architecture isn't settled.

### Asset Folder Structure: `Assets/Images/{System, Sidebar, Dialogs}`
**Decision:** All image resources live under a single top-level `Assets/Images/` tree with purpose-named subfolders — `System/` (app icons, logos, cover art), `Sidebar/` (help sidebar screenshots), `Dialogs/` (one-off dialog imagery). Applies to source tree AND build output — no `Link` indirection. Previously split across `Images/` and `Help/*.png`.
**Why:** One discoverable home for every image file, with semantic grouping that tells a developer where a new image should land without having to ask. `Help/` retains only `manual.html`, not the 30 screenshots it consumes. Flat sibling folders under `Assets/` (without the `Images/` parent) were rejected because future one-off dialog images wouldn't have an obvious home and the top level would grow noisily. An earlier iteration used MSBuild's `Link` attribute to keep output at `Help/*.png` while source lived at `Assets/Images/Sidebar/` — reversed because it defeated the point of organizing: the user saw images "back in the Help folder" in the build output.
**Date:** April 2026

### Reset User Settings: Registry-Based Whitelist, Not Nuclear Clear
**Decision:** A "Reset User Settings" dialog in the Settings popup lets users selectively clear groups of preferences from the `UserSettings` table. Exposed groups and their member keys are declared in `Utilities/UserSettingsRegistry.cs`; anything not in the registry is excluded by policy. Rule: if a setting already has a manager UI where the user can modify/add/delete it (Theme submenu, Grid Layouts dialog, Manage Filters dialog, Analysis chart filter Reset button, Schedule UDF dialog, in-view dropdowns/checkboxes/paths), it does NOT appear in the Reset dialog. Only settings that have no other way to clear — grid column prefs, splitter ratios, dialog window dimensions, one-time skip flags — are included. System bookkeeping keys (`LastSyncUtcDate`, `LastSeenVersion`) are also excluded.
**Why:** A naive "reset all" would delete `LastSyncUtcDate` (forcing a costly full re-sync), wipe named grid layouts that the user deliberately created, and duplicate functionality that's already accessible via dedicated dialogs. A curated registry also gives us a single source of truth for natural-language labels and defaults, which the finisher skill (Step 3.7) keeps in sync as new settings are added.
**Date:** April 2026

### Grid Reset Must Recreate the Current View, Not Just Delete the Row
**Decision:** When the Reset dialog clears `ProgressGrid.PreferencesJson` / `ScheduleGrid.PreferencesJson`, `MenuResetUserSettings_Click` detects that case, calls `SkipSaveOnClose()` on the currently-loaded view, and force-recreates the view (`LoadProgressModule(forceReload: true)` or nulling `ContentArea.Content` + new `ScheduleView()`). Mirrors the existing `ResetGridLayoutsToDefault` pattern.
**Why:** The Progress and Schedule views save their in-memory column state to `UserSettings` on unload. If we only deleted the row and left the view in place, closing the app would trigger the Unload handler and re-save the in-memory widths back over our reset — a silent no-op. The view must be recreated so its Unload handler fires while the row is empty (and the new instance loads default columns).
**Date:** April 2026

### App-Close Guard via Single Counter, Not Per-Op Flags
**Decision:** One static counter in `Utilities/LongRunningOps.cs` tracks any critical operation via `using (LongRunningOps.Begin()) { ... }`. `MainWindow_Closing` reads `LongRunningOps.IsRunning` and warns the user before exiting if true. Six call sites currently wrap the scope: Submit Week, user snapshot delete, user snapshot revert, admin delete selected, admin delete all, admin upload-to-ProgressLog.
**Why:** Submit Week's Step 10 (local UPDATEs to WeekEndDate/ProgDate/PrevEarnMHs/PrevEarnQTY) and similar post-commit housekeeping can leave local SQLite in a partially updated state if the app process dies mid-sequence. A single counter with `Interlocked` increment/decrement is ~40ns per op (imperceptible overhead on second-scale operations), works across threads, and doesn't require per-op plumbing. Alternatives considered: (1) per-op boolean flags on MainWindow — rejected as six flags is worse than one counter, (2) service registry pattern with event notifications — rejected as overkill for a boolean "anything running?" check, (3) relocking the grid during Step 10 — rejected because the user explicitly wanted the grid usable after the snapshot was captured.
**Date:** April 2026

### WebView2 Virtual Host Maps to App Base Dir, Not Help/
**Decision:** The help sidebar's WebView2 virtual host `help.local` is mapped to the app base directory (`{baseDir}`), not the `Help` subfolder. Navigation URL is `https://help.local/Help/manual.html`, and `manual.html` references images via `<img src="../Assets/Images/Sidebar/xxx.png">`.
**Why:** When images moved to `Assets/Images/Sidebar/`, manual.html still lives at `Help/manual.html` — the sibling-relative `<img src>` pattern no longer works because the images are no longer siblings. Rooting the virtual host at the app base dir lets a single host cover both the HTML and the separately-located image folder. Alternative considered: duplicate the host mapping (one for `Help/`, one for `Assets/Images/Sidebar/`). Rejected because one mapping at the base dir is simpler and won't need further changes if the folder tree grows.
**Date:** April 2026

---

## Analysis Module

### Chart Filters Independent from Summary Grid
**Decision:** Chart filter panel applies to charts only. Summary table has its own independent filters (Group By, My Records/All Users, Projects).
**Why:** Different analytical needs — charts for visual exploration across many dimensions, summary table for its own grouping context.
**Date:** April 2026

### Project Selection Not Persisted
**Decision:** Auto-selects first project from current local data instead of saving/restoring selections.
**Why:** Stale saved selections pointed to projects no longer in local DB after clear/re-sync, causing the table to appear empty.
**Date:** April 2026

---

## Plugin System

### PTP Plugin: Match on UDF2, Not Description
**Decision:** Changed matching from Description pattern (`FABRICATION - 4.SHP {CWP}`) to UDF2 field containing CWP value directly.
**Why:** Users were editing the Description field after import, breaking update-vs-create detection. UDF2 is a stable identifier not typically modified.
**Date:** March 2026

---

## Admin / Snapshots

### ManageProgressLog: Rolled Up from Per-RespParty to Per-Batch
**Decision:** Upload batches grouped by (Username, ProjectID, WeekEndDate, UploadUtcDate) instead of per-RespParty rows.
**Why:** Snapshots are always created as a unit across all RespParty values. Per-RespParty tracking added complexity with no practical benefit.
**Date:** February 2026

### Snapshot Dialogs Kept Open + Modeless, Not Detached Background Work
**Decision:** Both `ManageSnapshotsDialog` (user) and `AdminSnapshotsDialog` were converted from modal (`ShowDialog`) to modeless (`Show`). The dialog stays open with its own spinner during long deletes/uploads; the user drags it aside and keeps working in the main window. `DialogResult = true/false` patterns replaced with a public `NeedsRefresh` property the caller reads in the `Closed` event. Re-entrancy guards on both menu items focus the existing instance instead of opening a second.
**Why:** Initial plan was to extract delete logic into services, close the dialog immediately on confirm, and show a non-modal status toast pinned to MainWindow. User picked the simpler approach: keep the dialog intact with its existing spinner, just remove the modal-ness so MainWindow stays interactive. Four lines per dialog instead of a service refactor.
**Date:** April 2026

### Submit Week Snapshot Is Frozen at SELECT, Not at Click
**Decision:** The Progress grid is locked (`sfActivities.IsEnabled = false`) the instant the busy dialog appears, then unlocked via `Dispatcher.InvokeAsync` the instant the local SELECT into the in-memory DataTable completes inside the background task. The SELECT was also moved ahead of the Azure DELETE step so the grid re-enables faster on the overwrite path.
**Why:** Before this change, the grid stayed live throughout an async submit, so edits made after clicking Submit could or could not end up in the snapshot depending on micro-timing of Azure pre-checks. Now the snapshot boundary is explicit: everything up to the SELECT is captured; everything after the SELECT is intentionally the NEXT week's progress. Users stay productive during the slow Azure writes (DELETE/bulk-copy/purge) that happen after the snapshot is already frozen in memory. No data loss either way — post-SELECT edits still set `LocalDirty=1` and push on next sync.
**Date:** April 2026

### ProgressLog UserID: Concat Uploader + AssignedTo Instead of Adding a Column
**Decision:** The admin upload to `VANTAGE_global_ProgressLog` writes `UserID` as `"uploader|assignedto"` (pipe-separated), e.g. `"steve|Grant.Gilbert"`. Concatenation happens in the SQL expression `@userId + '|' + ISNULL([AssignedTo], '')` inside the existing `INSERT ... SELECT FROM VMS_ProgressSnapshots`. Wrapped in `LEFT(CAST(... AS NVARCHAR(MAX)), maxLen)` using the column's max length from `INFORMATION_SCHEMA`. Pipe chosen because it cannot appear in a Windows username.
**Why:** Before this, `UserID` was hardcoded to the admin who ran the upload, and the two rows in `VMS_ColumnMappings` that were supposed to route `AssignedTo` through were both broken (each had one field empty, so the mapping loader silently skipped them). Result: when two users' snapshots were uploaded at the same Timestamp + ProjectID, their ProgressLog rows were indistinguishable. Alternatives considered: (1) `ALTER TABLE` to add a dedicated `AssignedTo` column — rejected for now because the ProgressLog table has 14.9M rows and schema changes require DBA approval the admin doesn't currently have; backlogged for later, (2) fix the `VMS_ColumnMappings` rows so `AssignedTo` writes to its own column — but there's no suitable empty column and would need the ALTER anyway, (3) leave `UserID` alone and pull original owner from the separate `VMS_ProgressLogUploads` tracking table — rejected because the tracking table records per-batch aggregates, not per-row identity. The concat packs both pieces of info into a single existing column at zero I/O cost. Legacy rows keep their old `"steve"`-style UserID and are distinguishable from new rows by the absence of `|`.
**Date:** April 2026

### Dedup Check After DELETE, Not Before
**Decision:** In Submit Week, the order inside the background Task is now: SELECT local → unlock grid → DELETE old Azure snapshots (if overwriting) → dedup check (existing UniqueIDs for the week) → bulk copy.
**Why:** The dedup check only needs to skip records already submitted by *other* users for the same week. If we check before the DELETE, our own prior snapshots would match and incorrectly flag every row as a duplicate. Running the dedup after the DELETE leaves only other-user entries — which is exactly the conflict set we want to report. The DELETE scope is narrow (AssignedTo + ProjectID + WeekEndDate), so no risk of removing other users' data.
**Date:** April 2026

### ProgressLog Upload Timeouts: Tiered Ceilings, Not Unlimited
**Decision:** Every `CommandTimeout = 0` in `AdminSnapshotsDialog.xaml.cs` replaced with a tiered ceiling sized per operation: 3600s on large INSERT/UPDATE/DELETE paths (main `INSERT ... SELECT`, `UPDATE VMS_Activities` that fires `TR_VMS_Activities_SyncVersion`, per-group `DELETE`, full-table `DELETE ALL`), 120s on the snapshot-groups aggregate query, 60s on the single-row tracking INSERT.
**Why:** `CommandTimeout = 0` (infinite) was wedging the UI forever when a TCP socket dropped mid-operation — the ADO.NET client has no way to know the response isn't coming. On 2026-04-18 a 178,193-row upload committed all data on Azure, but the UI hung on the final `UPDATE VMS_Activities` because the trigger's response packet was lost. The timeout is not there to abort legitimate work; it's a dead-socket guard so the UI recovers. 3600s is well above the legitimate max (150k+ row uploads routinely exceed 10 minutes, so a 30-min ceiling would be too aggressive). Short ceilings make sense only on operations that can't legitimately take long. Note: this is the OPPOSITE choice from the Sync flow, which uses unlimited timeouts — see "Unlimited SQL Timeouts in Sync Flow". The difference: sync flow is a foreground operation blocking the user; this is an admin-triggered background operation where the UI sits on `SfBusyIndicator` — a dead socket is less tolerable here because the user has no signal anything is wrong. Consider applying the same tiered approach to the sync flow in the future.
**Date:** April 2026

### RespParty Write Path Removed — Aligning Writer with Feb 2026 Decision
**Decision:** `AdminSnapshotsDialog.UploadSnapshotsToProgressLog` no longer writes per-RespParty tracking rows to `VMS_ProgressLogUploads`. Writes one row per upload group (Username + ProjectID + WeekEndDate), with `RespParty = ""`. The column is retained in the schema, not dropped.
**Why:** The February 2026 decision "ManageProgressLog: Rolled Up from Per-RespParty to Per-Batch" aligned the READER side — grid XAML, REFRESH path, and DELETE path in `ManageProgressLogDialog` all ignore `RespParty`. But the WRITER in `AdminSnapshotsDialog` continued fanning uploads into N per-RespParty rows per batch, producing data nothing could read. Every upload cost one extra `SELECT ... GROUP BY RespParty` round-trip per group and N extra tracking INSERTs for zero information gain. Column retention (vs `DROP COLUMN`) is intentional: the column contains legacy data and dropping it requires DBA coordination on a 14.9M-row adjacent table; an empty-string column on new rows is harmless and keeps this a pure code change.
**Date:** April 2026
