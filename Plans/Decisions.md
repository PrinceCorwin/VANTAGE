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

### JC Cost Code Normalization: Strip Whitespace + Per-Segment Leading Zeros
**Decision:** When comparing JC cost codes (ProjectID, PhaseCode) between the JC Labor Productivity report and Vantage `VMS_Activities`, both sides are normalized by: stripping all whitespace, splitting on `.`, trimming leading zeros from each segment (preserving `0` for all-zero segments), dropping trailing empty segments, rejoining with `.`. Implemented in `Utilities/VPvsVtgReportAugmenter.NormalizeKey`.
**Why:** JC codes appear in multiple equivalent forms across systems — `26.001.001`, `26.001.  1.`, and `26.001.1` are all meant to represent the same phase but differ in zero-padding, whitespace padding, and trailing separators. A naive string match would split them into separate buckets and produce spurious "Not Found" results. Normalization is applied symmetrically so match rates are insensitive to whichever cosmetic format each side happens to use.
**Date:** April 2026

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

### Asset Folder Structure: `Assets/Images/{System, HelpSidebar, Dialogs}`
**Decision:** All image resources live under a single top-level `Assets/Images/` tree with purpose-named subfolders — `System/` (app icons, logos, cover art), `HelpSidebar/` (help manual screenshots), `Dialogs/` (one-off dialog imagery). Previously split across `Images/` and `Help/*.png`.
**Why:** One discoverable home for every image file, with semantic grouping that tells a developer where a new image should land without having to ask. A flat `Assets/HelpSidebar/` + `Assets/System/` structure was rejected because future one-off dialog images (like `vp-vtg-prep.png`) wouldn't have an obvious home and the top level would grow noisily with each new image category.
**Date:** April 2026

### csproj `Link` Attribute Decouples Source Layout from Output Layout
**Decision:** When reorganizing the source tree of asset files, the csproj uses `<Content Include="Assets\Images\HelpSidebar\*.png" Link="Help\%(Filename)%(Extension)">` so the files live in the new source location but get copied to the legacy output path at build time.
**Why:** The WebView2 virtual host mapping (`help.local` → `{baseDir}/Help`) and manual.html's sibling-relative `<img src>` tags expect the runtime output to have `Help/*.png`. Using `Link` lets us reorganize source freely without touching any runtime code or risking WebView2 behavior changes. Also collapses 25 `<None Remove>` + 29 `<Content Include>` entries into a single glob line, so adding a new help screenshot is zero-config.
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
