# VANTAGE: Milestone — Design Decisions

How VANTAGE works today. Each entry states a current rule or invariant; the **Why** captures the reasoning behind it. Every line should be true right now — if a rule changes, the existing entry is rewritten in place rather than appended to.

Sections follow VANTAGE's nav structure top to bottom. See `.claude/skills/finisher/SKILL.md` Step 3.5 for the full maintenance contract.

---

## Foundation

### Dates Stored as TEXT, Not DATETIME
**Rule:** All date columns use TEXT/VARCHAR in both SQLite and Azure SQL. Code paths read and write date strings; parsing happens at the consumer.
**Why:** P6 Primavera exports dates as text strings. TEXT storage avoids conversion issues and format mismatches during import/export cycles.

### ActStart and ActFin Are Required Metadata, Not Auto-Populated
**Rule:** ActStart is required when `PercentEntry > 0`; ActFin is required when `PercentEntry = 100`. Missing values are flagged with red cell highlights and block sync, but do not block in-progress edits. VANTAGE does not auto-fill either date on bulk-write paths (sync, plugins, imports).
**Why:** Auto-populating dates silently overrode user intent in earlier iterations. The required-metadata gate enforces data completeness without trapping users mid-edit when they raise a record's progress before dates are known. The auto-stamp logic that does exist (Schedule detail grid edits) only fires from explicit user cell edits, not bulk paths.
**Date:** February 2026

### AzureUploadUtcDate Is Pull-Only
**Rule:** `AzureUploadUtcDate` is set on Azure during admin Progress Log upload and received by users on pull. The push path does not include it — users cannot overwrite it.
**Why:** Prevents users from accidentally corrupting the upload timestamp that only admins should set during Progress Log upload.
**Date:** January 2026

### V2 Data Model: ClientEarnedEquivQty Is Read but Not Written
**Rule:** The `VAL_Client_Earned_EQ-QTY` column exists in OldVantage Excel imports but is currently ignored. Activities, Azure `VMS_Activities`, and ColumnMappings will gain the corresponding column in V2.
**Why:** Documented as a known gap so the column doesn't get treated as an unknown extra field on import. V2 work hasn't started, so plumbing it now would land dead code.

### Activity Validator Is the Single Source of Truth for Date/Percent Rules
**Rule:** Every edit path — single-cell `CurrentCellEndEdit`, Find &amp; Replace, both multi-row paste flows — calls `ActivityValidator.Validate(percent, actStart, actFin)`. The function returns the first violation message or null. New bulk paths must call it; do not reimplement.
**Why:** Before centralisation the same rules existed in three locations and drifted. One source eliminates that class of bug.
**Date:** April 2026

### Hard Rules vs. Required Metadata Are Two Tiers
**Rule:** Activity date/percent rules split into two tiers. **Hard rules** (future dates, ActFin before ActStart, ActStart populated with `%=0`, ActFin populated with `%<100`) block the edit and revert it. **Required metadata** (ActStart needed when `%>0`, ActFin needed when `%=100`) does NOT block — the cell turns red and sync is gated until corrected.
**Why:** Treating "required metadata" as a hard block creates deadlocks when raising progress before dates are known or when bulk-setting `%=100` on records that still need an ActFin. Red highlight + sync gate still enforces completeness without trapping the user mid-edit.
**Date:** April 2026

### Bulk Operations Abort On Any Violation
**Rule:** When Find &amp; Replace or a multi-row paste encounters a hard-rule violation on any affected row, the entire operation rolls back in memory, nothing writes to the DB, and a dialog lists up to 10 offending ActivityIDs plus a "...and N more" footer. There is no partial apply.
**Why:** Silent partial apply (commit some rows, skip others) was reported as confusing — users couldn't tell what actually changed. Abort-on-any forces the source data to be corrected and re-run, which is more predictable.
**Date:** April 2026

### Entering ActFin Auto-Bumps PercentEntry to 100
**Rule:** Single-cell edit, Find &amp; Replace, and paste all auto-set `PercentEntry = 100` on any row where a non-null Finish date is entered while current `%` is below 100.
**Why:** Users always have to follow a Finish-date entry with `%=100` (ActFin requires `%=100`). The auto-bump saves a step. If ActStart is null when ActFin is entered, the record still writes — ActStart turns red as required metadata, consistent with the hard-rule / required-metadata split.
**Date:** April 2026

### Paste Into ActStart/ActFin Validates, Does Not Silently Skip
**Rule:** Multi-cell paste into ActStart or ActFin runs the same validator as single-cell edits. Any violation aborts the whole paste with a detailed error dialog. There is no pre-filter that quietly drops offending rows.
**Why:** Earlier silent-skip behavior (with a "N rows skipped" message after) let users think a paste succeeded when significant portions were dropped. Consistent abort-all matches Find &amp; Replace and single-cell edits.
**Date:** April 2026

### Filter Does Not Auto-Refresh After Edits
**Rule:** Filters re-evaluate only when the user clicks a filter toggle or the Refresh button. Data mutations (paste, Find &amp; Replace, Prorate, Undo/Redo, single-cell rollback, ClearCurrentCell) do NOT call `View.Refresh()`. The grid uses `LiveDataUpdateMode="Default"` so Syncfusion also does not re-shape data on property changes.
**Why:** Users were frustrated by rows disappearing from a filtered view the moment their edit made the row no longer match (e.g., raising `%` to 100 while viewing "In Progress"). Showing the value before re-applying the filter is what they want. `INotifyPropertyChanged` still propagates value changes to grid cells; only the filter predicate stays stale until explicit refresh.
**Date:** April 2026

### `ActivityRequiredMetadata` Is the Single Source for the 10 Required Fields
**Rule:** The 10 required-metadata field names (`ProjectID`, `WorkPackage`, `PhaseCode`, `CompType`, `PhaseCategory`, `SchedActNO`, `Description`, `ROCStep`, `RespParty`, `UOM`) live in `Utilities/ActivityValidator.cs` as `ActivityRequiredMetadata`. Three helpers expose them: `Fields` (the array, ProjectID-first), `FieldsDisplay` (comma-joined for user messages), and `BuildMissingFieldSql(alias)` (generates `"X IS NULL OR X = '' OR Y IS NULL OR Y = '' ..."` for either unqualified or aliased columns). All call sites — sync-gate filter SQL, `CalculateMetadataErrorCount`, `CountMetadataErrorsForProject`, sync-block MessageBox, reassign-check in-memory filter, reassign-block MessageBox, `ImportTakeoffDialog.RequiredMetadataFields` — pull from this single list. Conditional rules (ActStart at `%>0`, ActFin at `%=100`) and the ProjectID existence check remain hand-coded alongside the generated fragments.
**Why:** Before consolidation the list was duplicated across 5 locations with two ordering variants; adding or removing a field would have required lockstep edits across all of them. One array with a SQL generator makes drift impossible. ProjectID-first canonical order preserves the Import Takeoff dialog's row order (the only interactive UI touching this list).
**Date:** 2026-04-24

### Auto-Update Uses a Host-Agnostic Manifest with SHA-256 Verification
**Rule:** A custom auto-updater checks `manifest.json` for a newer version, downloads the ZIP, verifies its SHA-256, and hands off to a separate `VANTAGE.Updater` console app to swap files. The manifest URL is configurable; current default points at GitHub raw URLs. The updater is self-contained so users don't need .NET runtime.
**Why:** Works against GitHub today, can switch to Azure Blob with one URL change. Graceful failure if offline. Self-contained publish keeps the update path identical for all users regardless of their machine state.
**Date:** January 2026

### Credentials Live in Encrypted Config, Not Compiled Constants
**Rule:** `CredentialService.cs` reads from `appsettings.json` in dev or AES-256-encrypted `appsettings.enc` in production. Connection strings and API keys are not embedded in source. The publish script handles encryption automatically.
**Why:** Published builds carry credentials without hardcoding them in the assembly. Same code path for dev and prod; only the file form differs.
**Date:** February 2026

### Schema Migrations Are Numbered and Idempotent
**Rule:** `SchemaMigrator.cs` runs numbered sequential migrations on local SQLite startup. Migrations are backward-compatible (additive where possible) and idempotent (safe to re-run). Failed migrations offer to delete the local DB and re-sync from Azure as recovery. Ad-hoc column existence checks scattered through the codebase are not allowed.
**Why:** Local SQLite holds user data that can't be silently deleted on schema change. Numbered sequential migrations prevent missed or double-applied changes. Backward-compatible additions also let older app versions tolerate a database last touched by a newer version.
**Date:** February 2026

### Logging Writes to Flat Files Only
**Rule:** `AppLogger` writes to `%LocalAppData%\VANTAGE\Logs\app-yyyyMMdd.log` (one file per UTC day, daily rotation, 15-day retention). There is no SQLite `Logs` table. The Export Logs dialog reads the flat files via `AppLogger.ReadLogFilesAsText(fromDate, toDate, minLevel)`, which parses the `[timestamp] Level ...` line prefix to apply level filters and groups multi-line exception stack traces with their owning log line.
**Why:** A parallel SQLite logs table added I/O on every log call (`CREATE TABLE IF NOT EXISTS` + `INSERT`) and duplicated content for one reader. Flat files are easier to grep/tail externally and survive SQLite corruption. Multi-line exception parsing uses a simple rule: any line not starting with `[timestamp] Level` continues the previous entry, so stack traces stay attached to their headers without a structured parser.
**Date:** April 2026

### Asset Folder Tree: `Assets/Images/{System,Sidebar,Dialogs}`
**Rule:** All image resources live under a single `Assets/Images/` tree with purpose-named subfolders: `System/` (app icons, logos, cover art), `Sidebar/` (help sidebar screenshots), `Dialogs/` (one-off dialog imagery). Source and build output match — no MSBuild `Link` indirection. `Help/` contains `manual.html` only.
**Why:** One discoverable home with semantic grouping tells a developer where a new image should land. Flat sibling folders directly under `Assets/` were rejected because future one-off dialog images had no obvious home.
**Date:** April 2026

### Reset User Settings Uses a Registry-Based Whitelist
**Rule:** The Reset User Settings dialog reads exposed groups and member keys from `Utilities/UserSettingsRegistry.cs`. Anything not in the registry is excluded by policy. Settings already managed through their own UI (Theme submenu, Grid Layouts dialog, Manage Filters, Manage UDF Names, Schedule UDF dialog, Analysis chart filter Reset, in-view dropdowns/checkboxes/paths) are listed in the deny-list comment with a reason and do NOT appear in the Reset dialog. System bookkeeping keys (`LastSyncUtcDate`, `LastSeenVersion`) are also excluded.
**Why:** A naive "reset all" would delete sync-state keys (forcing costly full re-sync), wipe named layouts the user deliberately created, and duplicate functionality already accessible via dedicated dialogs. The curated registry also gives a single source of truth for natural-language labels and defaults that the finisher skill keeps in sync.
**Date:** April 2026

### Resetting Grid Preferences Recreates the Current View
**Rule:** When the Reset dialog clears `ProgressGrid.PreferencesJson` or `ScheduleGrid.PreferencesJson`, the handler calls `SkipSaveOnClose()` on the currently-loaded view and force-recreates it (`LoadProgressModule(forceReload: true)` or nulling `ContentArea.Content` and instantiating a new `ScheduleView`).
**Why:** The Progress and Schedule views save in-memory column state to `UserSettings` on unload. If only the row is deleted, closing the app would re-save the in-memory widths over the reset. Recreating the view ensures Unload fires while the row is empty and the new instance loads default columns.
**Date:** April 2026

### App-Close Guard Uses a Single Counter, Not Per-Op Flags
**Rule:** `Utilities/LongRunningOps.cs` exposes `Begin()` (returns `IDisposable`) and `IsRunning`. Critical operations wrap their work in `using (LongRunningOps.Begin()) { ... }`. `MainWindow_Closing` reads `IsRunning` and warns the user before exiting if true. Six call sites currently use it: Submit Week, user snapshot delete, user snapshot revert, admin delete selected, admin delete all, admin upload-to-ProgressLog.
**Why:** A single `Interlocked` counter (~40ns per op) handles "is anything critical running?" across threads without per-op plumbing. Per-op booleans on MainWindow would scale poorly; service-registry plus event notifications was overkill for a boolean check.
**Date:** April 2026

---

## Sync

### Push Failure Blocks Pull
**Rule:** If push to Azure fails, the pull does not run. Local dirty records remain marked `LocalDirty=1` for the next attempt.
**Why:** Earlier behavior ran the pull regardless of push outcome — pulling old Azure data over local edits and silently clearing `LocalDirty` flags. Users saw edits revert with no error. Blocking the pull on push failure preserves local work for retry.
**Date:** April 2026

### SchedActNO Ownership Check Is a Tools Menu Action, Not a Sync Step
**Rule:** Sync does not run a SchedActNO ownership check. The standalone "ActNO Split Ownership Check" action under the Tools menu performs this check on demand.
**Why:** The check joined `VMS_Activities` against itself on `SchedActNO` across 65K+ records with no supporting index — minutes-long timeouts that blocked sync entirely. Useful information, but not on every sync.
**Date:** April 2026

### Push Verifies Updates via OUTPUT INTO
**Rule:** The Azure UPDATE in push Step 3 captures actually-updated UniqueIDs via `OUTPUT INSERTED.[UniqueID] INTO #UpdatedIds`. Only UniqueIDs in that temp table get `LocalDirty` cleared locally. Records the UPDATE missed keep `LocalDirty=1` and retry on the next sync.
**Why:** A trigger or transient Azure issue can cause an UPDATE to affect fewer rows than expected. Without verification, unverified records had `LocalDirty` cleared and the subsequent pull silently overwrote local data with old Azure values. The OUTPUT-INTO pattern guarantees the local clear matches the actual server-side mutation.
**Date:** April 2026

### Clearing `LocalDirty` in the DB Requires a Grid Reload; Indicators Read the DB
**Rule:** Sync push clears `LocalDirty = 0` with a bulk SQL UPDATE on the local `Activities` table only — it does not mutate the in-memory `Activity` objects. Every path that clears dirty state (sync push, MainWindow bulk resets) must follow with a reload of the grid from the DB (`ProgressView.RefreshData` / `RefreshAsync`). The Unsynced indicator is therefore derived from a `SELECT EXISTS(... LocalDirty = 1)` against the DB (folded into `CalculateMetadataErrorCount`), never from the in-memory collection.
**Why:** The in-memory objects stay stale-dirty after a push until a reload repopulates them, so any feature reading in-memory `LocalDirty` (reassign gate, Unsynced filter, dirty highlight, the red Unsynced button) would be wrong immediately after sync. Reading the DB — the sync source of truth — is always correct. Adding a new path that clears dirty in the DB without a reload would silently break these; a comment at the `SyncManager` clear site documents the invariant.
**Date:** July 2026

### Submit Week Refuses Weeks Older Than the Snapshot Retention Window
**Rule:** `SnapshotRetentionDays` (21) is a single shared constant driving both the Submit Week Step-9 purge (`DELETE FROM VMS_ProgressSnapshots WHERE WeekEndDate < today - SnapshotRetentionDays`) and a guard in the Submit Week date picker that rejects a chosen week-ending date older than that window.
**Why:** Without the guard, a user could pick a backdated week, submit it, and the same submit's cleanup step would immediately purge the just-created snapshot — it never appeared in the Snapshot Manager. One shared constant keeps the guard and the purge boundary from drifting apart.
**Date:** July 2026

### Sync Uses Unlimited SQL Timeouts
**Rule:** All `CommandTimeout` and `BulkCopyTimeout` values in the push and pull paths are set to 0 (unlimited). `ConnectTimeout` on the Azure connection is also 0.
**Why:** Users on slow internet hit 30-second default timeouts mid-sync. Aborting partway through corrupts the LocalDirty/SyncVersion state — there is no partial-sync recovery. The sync must run to completion regardless of connection speed. Compare with the admin Progress Log upload path, which uses tiered timeouts because it has SfBusyIndicator and a dead-socket guard makes more sense there.
**Date:** April 2026

### LastPulledSyncVersion Comes From Records Pulled, Not a Live MAX Query
**Rule:** After a pull, `LastPulledSyncVersion_{ProjectID}` is set to the maximum SyncVersion among records actually pulled — not from a separate `SELECT MAX(SyncVersion)` query.
**Why:** A separate MAX query opens a race window: another user can push between the pull and the MAX query, advancing the server-side max past records that didn't yet exist when the pull ran. Those records would be permanently skipped on future pulls. Reading max-of-pulled records guarantees the cursor advances only over rows actually received.
**Date:** April 2026

### Pull Preserves Locally-Dirty Rows Only When Ownership Is Stable
**Rule:** `SyncManager.PullRecordsAsync` reads each locally-dirty UniqueID's `AssignedTo` from local and compares it to the `AssignedTo` field on the pulled Azure record. Match → skip the `INSERT OR REPLACE` and add the UniqueID to `SyncResult.SkippedDirtyConflicts` so the sync-complete dialog can surface "N of your unsynced rows had newer data in Azure". Mismatch → let the pull through so the local copy updates to reflect Azure's current ownership. `LastPulledSyncVersion` still advances to the max version actually read from Azure regardless of skip decisions. Tombstones (`IsDeleted = 1`) are NOT covered — delete-wins stays.
**Why:** Push runs before pull and resets `LocalDirty = 0` on every successfully-pushed row, so the only `LocalDirty = 1` rows at pull time are the ones the per-row push gate rejected (validation-blocked: missing required metadata, date-rule violations, etc.). Without this guard, those exact rows could be silently clobbered on the next pull if Azure's `SyncVersion` for them bumped via an unrelated path (another user pushed, admin reassign, ProgressLog upload) or on any full re-pull (MyRecordsOnly toggle off resets `LastPulledSyncVersion`). The ownership-match check is necessary because a blanket "preserve every dirty row" rule wrongly preserves dirty edits on rows that have been reassigned away from the user — those edits are on stale ground (the user no longer owns the row), and preserving them leaves the local cache permanently showing the wrong owner under non-MyRecordsOnly mode. Forcing the user to fix metadata on a row they no longer own only to have it overwritten on push is also pointless. Under MyRecordsOnly, the pull filter already excludes reassigned-away rows before the guard sees them, so the check is a no-op there. Resolution path for legitimate dirty conflicts is unchanged: user fixes the violation and pushes, push wins over Azure's newer version (last-write-wins). Tombstones stay delete-wins because the alternative (preserve dirty rows from deletion too) creates a "ghost record" state with no clean push-side resolution; the `DeletedRecordsView` Restore flow scans for metadata errors before flipping `IsDeleted = 0` to catch the inverse case.
**Date:** 2026-06-25

### `TR_VMS_Activities_SyncVersion` Is Conditional, Not Toggled
**Rule:** The Azure trigger on `VMS_Activities` auto-increments `SyncVersion` only when the caller did not explicitly write the column. On UPDATE: `IF UPDATE(SyncVersion) RETURN`. On INSERT: skip if all incoming `SyncVersion` values are non-zero (push pre-reserves a range from `VMS_GlobalSyncVersion` and assigns per-row); a zero falls through to the auto-bump path as a safety net. `SyncManager.PushRecordsAsync` no longer issues `DISABLE TRIGGER ... ON VMS_Activities` around its bulk insert.
**Why:** `DISABLE TRIGGER` is a database-wide DDL, not connection-scoped. While push held the trigger off, every concurrent UPDATE from every other user — different project, different row, didn't matter — landed with `SyncVersion` unchanged. Those rows' Azure values diverged from every other user's local cache silently, and no future pull would catch them because `SyncVersion > LastPulledSyncVersion` never matched. Worse: a network drop between `DISABLE` and the `finally`-block `ENABLE` could have left the trigger permanently disabled until manual intervention. The conditional pattern keeps the trigger armed for every other writer while still letting push assign its own contiguous range without double-bump collisions.
**Date:** 2026-06-24

### Azure UPDATEs Against `VMS_Activities` Do Not Set `SyncVersion` In The SET Clause
**Rule:** Any C# code path that issues an `UPDATE` against `VMS_Activities` must omit `SyncVersion = ...` from its SET clause and let `TR_VMS_Activities_SyncVersion` bump the column from `VMS_GlobalSyncVersion`. Applies to push staging UPDATE, soft-delete UPDATE, `AzureUploadUtcDate` UPDATE in the ProgressLog upload path, the reassign UPDATE, and any future writer. The push INSERT path is the only exception: it pre-reserves a contiguous range from `VMS_GlobalSyncVersion` and assigns per-row in the bulk-insert DataTable; the trigger's INSERT path correctly skips when all incoming values are non-zero.
**Why:** The conditional trigger's `IF UPDATE(SyncVersion) RETURN` short-circuits as soon as the column appears in any UPDATE statement's column list — including arithmetic forms like `SyncVersion = SyncVersion + 1`. Whatever value the C# code wrote is the final value, with no fallback to the global counter. Pre-2026-06-24 this never mattered because the trigger fired unconditionally on every UPDATE and overwrote whatever the caller had set. After the trigger became conditional, the reassign UPDATE's latent `SyncVersion = SyncVersion + 1` line became load-bearing — the `+1`-bumped rows ended up below every user's `LastPulledSyncVersion` and pull never returned them, so reassignments visibly succeeded on Azure but didn't propagate to anyone's local cache (assigner, recipient, or third-party admin). Every Azure UPDATE writer must trust the trigger to keep this class of regression from recurring.
**Date:** 2026-06-25

### Pull Adapts Query Plan To Expected Row Count
**Rule:** `SyncManager.PullRecordsAsync` runs a cheap `SELECT COUNT(*)` against the existing `IX_VMS_Activities_Project_SyncVersion` index before the main pull `SELECT *`. If the count exceeds `ForceScanThreshold` (5,000), the main SELECT is built with `WITH (INDEX(0))` to force a clustered scan, and `ORDER BY [SyncVersion]` is dropped. Below the threshold, the SELECT keeps the index-seek path and the `ORDER BY`.
**Why:** `IX_VMS_Activities_Project_SyncVersion` is non-covering — it answers the WHERE and ORDER BY but `SELECT *` requires a key lookup back to the clustered index per matching row. Small deltas (well below SQL Server's ~0.3% seek-vs-scan tipping point) benefit hugely from the index — single-row pulls measured 31s → 1.4s on a 39k-row project. Bulk pulls suffer — 125k-row pull measured 107s with the index plan (~622k logical reads) vs 41s with the forced clustered scan (~147k reads). The optimizer keeps picking seek+lookup even when scan is clearly better, and `UPDATE STATISTICS WITH FULLSCAN` did not move the plan. Making the index covering would mean INCLUDE-ing ~100 columns — effectively a second copy of the 15GB+ table, doubling write cost on every push/UPDATE/trigger fire. Adaptive plan selection in C# gives both pull sizes the right plan with one index, one cheap COUNT round-trip, no covering-index storage cost. ORDER BY can be dropped for the forced-scan path because `maxVersionPulled` (the cursor advance) only needs the MAX of returned rows, not a pre-sorted read order; keeping the ORDER BY would add a Sort operator on top of the scan and spill to tempdb at 100k+ row scale, erasing the win.
**Date:** 2026-06-25

### MyRecordsOnly Cleanup Reads Ownership From Azure, Not Local State
**Rule:** `SyncDialog.RemoveNonOwnedLocalRecords` queries Azure for the authoritative `UniqueID` set assigned to the current user in the selected projects (`SELECT UniqueID FROM VMS_Activities WHERE ProjectID IN (...) AND AssignedTo = @username AND IsDeleted = 0`), then deletes any local row in those projects whose UniqueID is NOT in that set. Local `AssignedTo` is never consulted.
**Why:** The MyRecordsOnly pull filter (`AND AssignedTo = @owner`) excludes any row that's been reassigned away from the user. The row's update never reaches the local cache, so the local `AssignedTo` stays stale forever. A cleanup keyed on local `AssignedTo != @username` would miss every reassigned-away row indefinitely — the user's local cache would grow a permanent shadow of rows Azure no longer says are theirs. Authoritative ownership lives in Azure; cleanup must consult it directly.
**Date:** 2026-06-24

### Submit Week Snapshots From Local Data, No Forced Sync
**Rule:** Submit Week reads from local SQLite directly. It does not force a sync first and does not run the SchedActNO ownership check.
**Why:** Decoupling Submit Week from sync enables historical-snapshot scenarios (restore a backup, snapshot that point-in-time state without pulling current Azure data over it). Also faster — fewer network round-trips on a frequent operation.
**Date:** February 2026

### Activity Import Auto-Detects Legacy vs NewVantage by Column Headers
**Rule:** A single import path detects legacy vs NewVantage Excel format by inspecting the column header set. `UDFNineteen` indicates Legacy; `UniqueID` indicates NewVantage. The user does not pick a format.
**Why:** Earlier threshold-based percent detection (1.5 cutoff to guess 0-1 vs 0-100 format) had edge cases that produced silently wrong values. Header-based detection is definitive: each format has its own diagnostic column, and the presence/absence is unambiguous.
**Date:** January 2026

---

## Main Window

### Title-Bar Drag-from-Maximized Uses DIPs, Not `PointToScreen`
**Rule:** The restore-from-maximized repositioning (now in `MainWindow.TitleBar_MouseMove`) computes the cursor's screen position as `this.Left + cursorInWindow.X` rather than calling `PointToScreen(...)`. The window position is then assigned in DIPs.
**Why:** `PointToScreen` returns physical pixels on high-DPI displays while `Window.Left`/`Top` use device-independent units. Mixing the two positioned the restored window off-screen by the scale factor (e.g., at 150% scaling, the window jumped 50% further than the cursor). Direct DIP math avoids the conversion entirely — a maximized window is at screen origin in DIPs, so window-local coordinates are screen coordinates. Any future code that mixes `PointToScreen` results with `Window.Left`/`Top` is suspect.
**Date:** May 2026

### Title-Bar Undock Requires a Hold-and-Move Gate
**Rule:** Dragging the top toolbar undocks/restores a maximized window only after the left button is held ≥`TitleBarDragHoldMs` (200ms) AND the pointer moves ≥`TitleBarDragThreshold` (25 DIPs). Mouse-down only arms the drag and captures the mouse; `TitleBar_MouseMove` starts the actual `DragMove` once both gates clear; a release before then is treated as a click. Double-click still toggles maximize/restore.
**Why:** The old handler called `DragMove` on mouse-down, so any click that wasn't on a button — including a fast misclick while the cursor was already moving toward a button — ripped a maximized window loose. The hold gate is the decisive one (a quick click-release never drags regardless of distance); the distance gate prevents a stationary long-press from jumping. Capturing the mouse on press is required so `MouseMove` keeps firing after the cursor leaves the 40px toolbar strip — without it, a downward drag started lower than ~15px exited the strip before the threshold tripped and never engaged.
**Date:** July 2026

### Theme System Uses DynamicResource and Role-Based Token Names
**Rule:** All theme-aware brushes/colors are referenced via `{DynamicResource ...}` (~1,119 instances). Resource keys are role-based (`ToolbarForeground`, `GridHeaderForeground`) rather than color-named (`DarkBlue`, `LightGray`).
**Why:** DynamicResource enables live theme switching without app restart. Role-based names keep the brush set stable as new themes are added beyond the initial Dark/Light pair.
**Date:** February 2026

### `SfSkinManager.SetTheme` Runs in Constructor, Not Loaded Event
**Rule:** Every dialog and view that participates in theming calls `SfSkinManager.SetTheme(this, new Theme(themeName))` in its constructor, before the control enters the visual tree. Loaded-event application is forbidden.
**Why:** When applied in Loaded on a second instance of the same control type, Syncfusion's theme engine interferes with `SfDataGrid` rendering and the grid does not display data. Constructor-time application is the only reliable path.
**Date:** April 2026

### Grid Filter Icons Are Custom Stroke-Based, Not Syncfusion Defaults
**Rule:** `FilterToggleButton` is replaced by a custom stroke-based funnel icon template. Active/inactive states drive theme-aware stroke colors via `DynamicResource`.
**Why:** Syncfusion's built-in filter icon colors are resolved from compiled BAML and cannot be overridden via resource dictionaries. Custom templates were the only path to theme-aware icons that respect the active theme's foreground color.
**Date:** February 2026

### Grids Require Double-Click to Sort
**Rule:** All `SfDataGrid` instances require double-click on column headers to sort. Single-click does not sort.
**Why:** Single-click sort triggered accidental resorts when users clicked headers for other reasons (right-click menu, text selection). Double-click is explicit.
**Date:** February 2026

### Column-Settings Schema Migration Is Graceful
**Rule:** When the saved grid layout's column hash doesn't match the current grid's column set, preferences apply to matching columns rather than being rejected outright. New columns appear at the end with default widths; removed columns are ignored.
**Why:** Hash-strict rejection meant adding or removing any column discarded all user column preferences across that grid. Graceful migration preserves user effort across schema evolution.
**Date:** February 2026

### Custom Scrollbar Templates Are Always-Visible 14px
**Rule:** All app scrollbars use a custom 14px-wide always-visible template with theme-aware brushes. Auto-hide is disabled. The custom `ScrollViewer` template avoids implicit-style leakage into Syncfusion `ComboBoxAdv` dropdown internals.
**Why:** Auto-hiding scrollbars were hard to find and interact with. Always-visible at 14px gives a stable target. Custom template scoping prevents the implicit style from breaking Syncfusion's internal scroll behavior.
**Date:** March-April 2026

### `ProgressView` Is Cached Across Navigations, Reloaded Lazily When Invalidated
**Rule:** `MainWindow` caches the `ProgressView` instance and reuses it on every Progress-tab navigation. When Activities change from outside the module while Progress is hidden (Validate My Records, Schedule Change Log, Manage Snapshots revert/delete, Schedule save / P6 stubs), the caller marks the cached view stale via `ProgressView.InvalidateData()`; `OnViewLoaded` then does a one-time `RefreshData()` on the next navigation only if `_needsReload` is set. Normal tab-switching with no change stays instant — no reload, and no eager reload of a hidden grid. Wholesale-replacement paths (Excel import Replace/Combine, Clear Local, Reset Grid Layouts) drop/recreate the cached instance instead. MainWindow routes the activity-change cases through one helper, `RefreshProgressAfterActivityChangeAsync` (shown → reload now, hidden → invalidate).
**Why:** First-load cost is unchanged and subsequent navigations stay instant, but the old "reload only if Progress is the active view" logic silently left the cached view stale after a cross-module change — the dirty highlight, Unsynced button, and metadata badge showed pre-change state until the next manual refresh. The lazy flag fixes correctness without reintroducing a 100k-row reload on every tab switch or eagerly reloading a view the user isn't looking at.
**Date:** July 2026 (caching February 2026; lazy invalidation July 2026)

### `AnalysisView` Is Cached Across Navigations
**Rule:** `MainWindow` caches the `AnalysisView` instance and reuses it on every Analysis-tab navigation. `AnalysisView_Loaded` runs the one-time init path only when `_dataLoaded` is false; subsequent navs short-circuit. `ThemeManager.ThemeChanged` is subscribed once in the constructor (not in `Loaded`) so re-Loaded events do not double-subscribe.
**Why:** Same motivation as Progress — chart settings, filter selections, summary scroll, and grid layout survive nav back, and the 12 chart-filter DISTINCT queries / chart-data query only run on first open. Previously the view was destroyed and rebuilt on every navigation, which compounded the per-add rehydration cost from the now-removed filter persistence into a UI-thread freeze at 100k-record scale.
**Date:** May 2026

### Notification Sounds Are Suppressed on Informational Dialogs
**Rule:** All `MessageBox.Show` calls that previously used `MessageBoxImage.Information` now use `MessageBoxImage.None`. The OS notification chime does not play on informational confirmations.
**Why:** The Windows information sound was disruptive on routine confirmations and added no signal users acted on.
**Date:** February 2026

### `AppMessageBox.Show` Wrapper Is Mandatory for All User-Facing Dialogs
**Rule:** Every dialog goes through `Utilities/AppMessageBox.Show(...)`, never `MessageBox.Show(...)` directly. The wrapper finds the active window, calls `Activate()`, and toggles `Topmost` true→false to force the dialog to the front before parenting it. CLAUDE.md enforces this rule.
**Why:** After long-running awaits or focus loss, a parameterless `MessageBox.Show` can render behind the owning window — a bulk operation appeared to hang because the success dialog was hidden under the grid. Centralised wrapper is the maintainable answer over per-callsite `Activate()` calls (480+ sites, easy to forget for new code).
**Date:** April 2026

### Top-Level Menus Auto-Close on Hover-Out via Custom Polling
**Rule:** All app dropdowns (Progress sidebar Actions and USER, MainWindow toolbar File/Tools/Admin) auto-close when the cursor leaves them. `Utilities/MenuAutoClose.cs` implements this via a 150ms polling `DispatcherTimer` with `InitialOpenGraceMs = 1500` (long delay before close if cursor never enters) and `CursorLeftDelayMs = 400` (faster close once cursor has been in and then left). A `hasBeenOver` flag picks which delay applies. Open submenus suppress the parent close so navigating into a submenu doesn't kill the parent.
**Why:** WPF supports auto-close natively only for submenus, not top-level menus. ContextMenu uses `Mouse.GetPosition(menu)` against the menu rect (single-rect popup, reliable). `DropDownButtonAdv` lives in a separate visual tree from its dropdown popup, so reliable bounds checking required `Mouse.GetPosition(button)` plus a 240×600 region below the button (toolbar dropdowns always open downward).
**Date:** April 2026

### Click-and-Stay-Open Menu Items Use `Dispatcher.BeginInvoke` Reopen
**Rule:** Menu items that stay open on click (e.g., Select All in the Progress Actions menu) perform their work in a regular `Click` handler and schedule `ContextMenu.IsOpen = true` via `Dispatcher.BeginInvoke` at `Background` priority to reopen the menu after WPF's `MenuItem.OnClick` closes it. Brief flicker is accepted.
**Why:** WPF `MenuItem.OnClick` closes the parent menu unconditionally for non-`SubmenuHeader` items. `IsCheckable="True"` toggles `IsChecked` but doesn't suppress the close in a `ContextMenu`. `PreviewMouseLeftButtonUp` with `Handled=true` is ignored because `ButtonBase.OnMouseLeftButtonUp` is registered with `HandledEventsToo=true`. Dispatcher reopen is the canonical workaround.
**Date:** April 2026

### Menu-Item Icons Render Inline in the Header, Not via `<MenuItem.Icon>`
**Rule:** Menu items that need icons (Actions menu items in the Progress sidebar) render the icon as a `TextBlock` inside the `MenuItem.Header` `StackPanel`, with a fixed 22px-wide icon column so labels align. The `<MenuItem.Icon>` slot is not used.
**Why:** WPF's default `MenuItem` template renders an "icon column gutter" rail above and below items even when overridden via custom `ItemContainerStyle`. Inline-icon-in-Header bypasses the gutter entirely — the template only sees a Header content presenter and a submenu arrow.
**Date:** April 2026

### WebView2 Virtual Host Maps to App Base Dir, Not `Help/`
**Rule:** The help sidebar's WebView2 virtual host `help.local` is mapped to the app base directory. `manual.html` lives at `Help/manual.html`; images live at `Assets/Images/Sidebar/...`. Image references in the manual use `<img src="../Assets/Images/Sidebar/xxx.png">` relative to the manual's location.
**Why:** When images moved to `Assets/Images/Sidebar/`, the manual could no longer reach them via sibling-relative paths. Rooting the virtual host at the app base dir lets one mapping cover both locations. Two host mappings (one for `Help/`, one for the images folder) was rejected as needlessly complex.
**Date:** April 2026

### Info Icons Anchor Into the Help Manual via `HelpAnchors` Constants
**Rule:** Info icons (clickable ⓘ buttons next to complex fields) call `HelpService.OpenAt(anchor)`, which routes through `MainWindow.OpenHelpAt(anchor)` → `SidePanelViewModel.ShowHelp(anchor)` → URL fragment in `HelpNavigationUrl` (`https://help.local/Help/manual.html#wp-name-pattern`). Same URL with a different fragment scrolls the WebView2 without a full reload. Anchor IDs live as `public const string` values in `Utilities/HelpAnchors.cs`. Raw anchor strings at call sites are forbidden.
**Why:** HTML anchors are stable across manual growth — keyed to `id`, not file position. The `HelpAnchors` constants file makes anchor renames a compile error rather than a silent broken link. Centralised routing also lets info icons on dynamically built editors call one static method without knowing about MainWindow internals.
**Date:** May 2026

### Hover-Open Popups Hold a Hyperlink, Tooltips Don't
**Rule:** Info icons that need a clickable hyperlink in their hover content use a `<Popup>` with `StaysOpen="True"` and `MouseEnter`/`MouseLeave` handlers on both the icon and the popup body. A 200ms `DispatcherTimer` close-handoff lets the cursor slide from icon to popup without dismissing.
**Why:** Standard WPF `<ToolTip>` content can't host clickable controls — moving the mouse into a tooltip dismisses it before any click registers. Popups are the canonical interactive-tooltip pattern. The 200ms timer plus per-region MouseEnter/MouseLeave handlers are the standard hover-handoff dance.
**Date:** May 2026

### Sidebar Help Pane Has a Single Help Tab, No AI Assistant Tab
**Rule:** The help sidebar shows the help manual only. There is no AI Assistant tab.
**Why:** The AI Assistant tab was a placeholder with no implementation behind it; carrying empty UI misled users into thinking the feature was imminent. `Plans/Sidebar_AI_Assistant_Plan.md` is retained in the repo if the feature is ever revived.
**Date:** April 2026

---

## Progress Module

### Bulk Select Uses Transient `IsBulkSelected`, Not `SfDataGrid.SelectAll()`
**Rule:** Ctrl+A and `Actions → Select All` flip a transient `Activity.IsBulkSelected` (INPC, not persisted, not synced) on each filtered row. `RecordOwnershipRowStyleSelector` paints the row Background from a `DataTrigger` bound to that flag, using `sfActivities.RowSelectionBrush` so the highlight matches Syncfusion's native mouse-row-select color. `SelectAllFilteredRowsAsync` populates `sfActivities.SelectedItems` for the bulk-action handlers but does NOT call `sfActivities.SelectAll()`. Any cell-level interaction (`sfActivities_SelectionChanged`) calls `ClearBulkSelectionInternal` to unflip the previously-marked rows. Future bulk-select code must follow this pattern; never call `SelectAll()` on this grid.
**Why:** The grid is configured `SelectionMode="Extended" SelectionUnit="Any"` because cell-level features (multi-cell copy/paste, Count/Sum/Avg stats panel) require it. Under that configuration, `SelectAll()` materialises one `GridCellInfo` per (row × column) — at 36k rows × ~30 columns ≈ 1M `GridCellInfo` objects allocated synchronously inside Syncfusion on the UI thread. Stopping `SelectAll()` and driving the visual highlight from a row-style data trigger has zero `GridCellInfo` allocation, instant on 100k-row grids. Shift+Click first→last row at scale still freezes — that's Syncfusion's native gesture, deferred indefinitely. Ctrl+A is the supported scale-safe path.
**Date:** May 2026

### Loading Overlay for Slow Bulk Ops Is Fullscreen DualRing, Not Inline Bar
**Rule:** Slow user-initiated bulk operations on the Progress grid (Select All, Delete after confirmation) use the fullscreen `LoadingOverlay` Grid with `SfBusyIndicator AnimationType="DualRing"`. The inline `SfLinearProgressBar` bound to `viewModel.IsLoading` is reserved for background data loads where the operation isn't user-initiated.
**Why:** Both animations tick on the UI thread. For long synchronous work like populating `SelectedItems` with 100k+ rows, both freeze unless the UI thread gets regular yields. Chunked work with `Task.Delay(1)` keeps either moving, but the overlay's larger visual real estate is the better fit for user-initiated bulk actions. Pre-confirmation overlays for fast operations (e.g., Delete's ownership pre-check on a small selection) are removed because they flickered before the confirmation dialog and added no signal.
**Date:** April 2026

### Global Search Matches Every Writable String Column via a Cached Reflection Helper
**Rule:** The Progress toolbar search bar (`PassesGlobalSearch`) matches the query (case-insensitive substring) against every text column on `Activity`, resolved by `Utilities/ActivityTextSearch`. That helper reflects `Activity` once and caches a compiled getter delegate (`Delegate.CreateDelegate`) for each property that is `string`-typed AND writable (`CanWrite`). Numeric/date/bool columns are excluded because they aren't strings; calculated/display columns (Status, ROCLookupID, `*_Display`, AssignedToUsername) are excluded because they're getter-only. `ActivityID` (`Act ID`) is numeric and therefore not searched. Adding a new writable text property to `Activity` makes it searchable automatically — do not reintroduce a hand-maintained field list.
**Why:** The prior implementation was a hard-coded 15-field OR chain that silently missed ProjectID, all UDFs, material specs, and drawing fields while the tooltip claimed "all columns". The writable-string rule is self-maintaining and needs no upkeep, and compiled open delegates avoid per-call reflection so the filter stays fast on 100k-row grids. Text-only (dropping numeric/calculated columns) was an explicit product choice.
**Date:** 2026-07-18

### DIY Toolbar Summary Panel Replaces Syncfusion `TableSummaryRow`
**Rule:** The Progress grid's Count/Sum/Avg cell-selection stats render in a custom toolbar panel using cached `PropertyInfo` lookups and a 200ms debounce, not via Syncfusion's `TableSummaryRow`.
**Why:** `TableSummaryRow` was too slow on large datasets (it re-evaluates aggregates on every selection change without debouncing). The DIY panel is faster and gives full control over which selection states trigger updates.
**Date:** February 2026

### `PercentEntry` Uses a Custom `GridTemplateColumn` with Progress-Bar Overlay
**Rule:** `PercentEntry` renders via a custom `GridTemplateColumn` with a thin colored progress-bar overlay in each cell, not the native `GridNumericColumn`.
**Why:** Native column types don't support the inline progress-bar visual. Trade-off accepted: decimal handling, arrow-key navigation, and auto-edit-on-type are hand-coded.

### `PercentEntry` Edit Trigger Is `LostFocus`, Not `PropertyChanged`
**Rule:** The `PercentEntry` `EditTemplate` `TextBox` binding uses `UpdateSourceTrigger=LostFocus`. Setter runs only when the user leaves the cell.
**Why:** `PropertyChanged` triggered the setter on every keystroke, running clamp/round/multi-PropertyChanged chains that raced against input — `0.5` could become `5`. `LostFocus` commits exactly once per edit.
**Date:** April 2026

### Progress Row Actions Live in the Sidebar Dropdown, Not Grid Right-Click
**Rule:** Row-action commands (Select All, Delete, Copy submenu, Duplicate, Add Blank, Export Selected) live under the "Actions" button in the Progress filter sidebar. The grid's `RecordContextMenu` is removed. The column-header `HeaderContextMenu` (Find &amp; Replace, Copy Column, Freeze) is unaffected.
**Why:** Syncfusion `SfDataGrid`'s default right-click clears multi-row selection and reduces it to the single row under the cursor (unless Shift is held). Users routinely selected 50+ rows, right-clicked Delete, and got only one row deleted. Sidebar buttons don't touch grid selection.
**Date:** April 2026

### Submit Week Is Gated by Per-Project Validation, Not Per-User
**Rule:** `BtnSubmit_Click` runs a combined validation gate between project selection (Step 3) and the week-end date picker (Step 4). The gate (1) verifies `selectedProject` exists in the `Projects` table with one SQL hit, then (2) iterates `_viewModel.Activities` for that project + the current user and calls `ActivityValidator.GetAllViolations(activity)` on each. If any row fails — required metadata blank, project missing, conditional date-required violation, or any `ActivityValidator.Validate` rule (future date, finish-before-start, etc.) — the submit blocks with a combined offender dialog listing up to ten ActNo+violation pairs. All-or-nothing.
**Why:** Snapshot is a point-in-time copy that must be internally consistent — partial coverage would let bad dates ride into the closed week. Sync pushes every dirty record the user has, so its gate is user-wide; Submit Week is always scoped to one project, so its gate is project-scoped (matches "fix the project I'm submitting"). Both surfaces share `ActivityValidator.GetAllViolations` so rule changes propagate to both gates in lockstep. Date checks run in C# (not SQL) because date columns are TEXT and SQLite's `date()` would let legacy non-standard date strings slip the filter.
**Date:** 2026-06-18 (originally 2026-04-24, expanded from metadata-only to full validation)

### Manage UDF Names Stores One Mapping, No Saved-Map Library
**Rule:** The Manage UDF Names dialog stores exactly one set of UDF column-header overrides at `ProgressUDFNames.Active` (single JSON dictionary). There is no saved-mappings list, no per-mapping `Apply`/`Rename`/`Delete`, no `Save Map` with collision prompts. The dialog supports Save (apply to grid), Reset Defaults, Export, and Import. Renames affect only `HeaderText`; underlying data, sync, exports, and Work Package tokens still reference the original `MappingName` (`{UDF1}` keeps working).
**Why:** Most users have one set of UDF labels per project; switching mappings is rare. The named-mapping UI added cost (right pane, Map Name field, list selection enabling Apply) without a workflow that justified it. Single-mapping makes rename a one-click flow. If named mappings are proposed again, ask whether the user actually wants to switch sets often.
**Date:** May 2026

---

## Schedule Module

### P6 Import Maps Current Schedule Dates, Not Baseline
**Rule:** P6 import maps `start_date` / `end_date` (current schedule) into the local Schedule table, not `target_start_date` / `target_end_date` (baseline).
**Why:** 3WLA requirement logic and missed-start/missed-finish reasons need current schedule dates. Baselines are stale and not what the field uses for week-by-week planning.
**Date:** February 2026

### 3WLA Dates Live in Activities, Not a Separate Table
**Rule:** 3-Week-Lookahead dates are stored on `Activities.PlanStart` / `Activities.PlanFin`. They are pre-populated from MIN/MAX of plan dates per `SchedActNO` and persist across P6 imports.
**Why:** A separate `ThreeWeekLookahead` table required parallel management and stale-detection logic. Inlining onto Activities removes both.
**Date:** February 2026

### MissedReasons Are Session-Only
**Rule:** `MissedReasons` are stored in the Schedule table and cleared on every P6 import. They are not persisted across imports.
**Why:** They are only meaningful for P6 dates within the current week. Persisting required complex stale-detection when underlying dates changed; clearing on import sidesteps that entirely.
**Date:** February 2026

### Schedule Module Reads From a Local 12-Column Mirror
**Rule:** The Schedule module reads from a local 12-column mirror of the Azure 89-column snapshot table. Azure remains authoritative; edits write through to both. The mirror self-heals on P6 import.
**Why:** Master/detail grid interactions over Azure latency had multi-second lag. Local mirroring eliminates the lag. 12 columns is exactly what the Schedule module reads — anything wider would copy unused data.
**Date:** April 2026

### "MS Not In P6" Report Is Per-User
**Rule:** The "MS Not In P6" diagnostic in the Schedule module reports only the current user's data.
**Why:** Matches the rest of the Schedule module's per-user filtering and is more intuitive for a per-user export. Cross-user reporting belongs to admin tools.
**Date:** April 2026

### Snapshot Retention Is 21 Days
**Rule:** Submit-time purge uses 21-day retention on `VMS_ProgressSnapshots`.
**Why:** Covers 3 full weekly cycles, which is enough for week-over-week comparisons; longer retention multiplied storage with no clear use.
**Date:** April 2026

### Schedule Saves Per-Cell, No SAVE Button
**Rule:** Schedule master-grid cell commits (Missed Reasons, lookahead Start/Finish, cell-clear via Delete/Backspace) save immediately via `ScheduleRepository.SaveScheduleRowAsync(row, username)` — single-row update plus the PlanStart/PlanFin bounds update scoped to the affected SchedActNO. The `NotifyActivitiesModifiedAsync` callback (which reloads the Progress grid) is debounced with a 1-second trailing `DispatcherTimer` so rapid editing doesn't hammer the 100k-row reload path. There is no `HasUnsavedChanges` state, no exit prompt, no SAVE button, no save-first export gate. Detail-grid edits already auto-save.
**Why:** A SAVE button created a bug class: clicking Refresh while edits were pending silently discarded them. Eliminating the unsaved state eliminates that bug by design and matches the Progress module's long-standing pattern.
**Date:** April 2026

### Lookahead Window Is User-Configurable: 3 / 6 / 9 Weeks
**Rule:** The Schedule lookahead window is selectable per-user via a ComboBox in the Schedule toolbar (3WLA / 6WLA / 9WLA, default 3WLA), stored in `UserSettings` as `Schedule.LookaheadWeeks`. The static `ScheduleMasterRow.LookaheadDays` is the single source of truth for the 21/42/63-day threshold consumed by `IsThreeWeekStartRequired` / `IsThreeWeekFinishRequired`. Property and DB column names like `ThreeWeekStart` / `ThreeWeekFinish` are retained — the "3" is historical, not structural. Excel import/export file formats are unchanged; only in-app UI strings, the highlighting threshold, and the Schedule Reports worksheet name + filename + dialog title adapt.
**Why:** Different trades and project phases need different forecast horizons. Hardcoding 21 days forced everyone into the same window. Per-user persistence keeps preferences personal.
**Date:** April 2026

### Schedule Detail Grid Auto-Stamps Dates From PercentEntry Edits
**Rule:** When the user edits a `PercentEntry` cell in the Schedule detail grid, `ActStart` is set to `WeekEndDate` if going from 0 to a positive value, and `ActFin` is set to `WeekEndDate` when reaching 100. Going backward clears the appropriate dates. This auto-stamp logic fires only on Schedule detail grid edits — sync, plugin, paste, and bulk paths do not trigger it.
**Why:** Schedule users edit weekly snapshots, where the date IS the WeekEndDate by definition. Other write paths (sync, plugins) operate on values that originate elsewhere; auto-stamping there would silently overwrite real dates.

### New ActNOs from P6 Create Stubs Across Activities + Snapshot
**Rule:** When the P6 import detects SchedActNOs in the file that aren't in the local snapshot mirror for the selected ProjectIDs, the New ActNOs dialog lets the user create stub records for them. Each stub writes to Azure `VMS_ProgressSnapshots` (full schema, so it survives the next refill that wipes the local mirror), local `Activities` (full schema, `LocalDirty = 1`), and the local `ProgressSnapshots` mirror (lean 12 columns) in that order. The two local writes share a SQLite transaction; the Azure write commits first. Detection compares P6 against the local snapshot mirror, not against the local Activities table, because "P6 vs. snapshot" is the Schedule module's premise.
**Why:** Without this path, a SchedActNO that P6 expects the field to track but that nobody has created locally is silently invisible. Writing to all three locations means the stubs are immediately visible (Schedule view reads the local mirror, Progress view reads local Activities) AND survive the next P6 re-import (which wipes the local mirror and refills from Azure).
**Date:** June 2026

### P6 Stub Records Use "X" Placeholders for Missing Required Metadata
**Rule:** Stub Activities created from the New ActNOs dialog use the literal string `"X"` for the six required-metadata fields P6 cannot supply (`WorkPackage`, `PhaseCode`, `CompType`, `PhaseCategory`, `ROCStep`, `RespParty`). `ProjectID` comes from the dialog's ProjectID picker, `SchedActNO` and `Description` from P6's `task_code` / `task_name`. `BudgetMHs`, `PercentEntry`, `ActStart`, `ActFin` mirror P6's `target_work_qty`, `complete_pct`, `act_start_date`, `act_end_date`. Quantity defaults to `0.001`.
**Why:** Required-metadata fields cannot be empty (sync gate blocks empties). A non-empty placeholder is both visible enough to remind the user to come back and fix it AND not blocking — the records can sync, just not earn against an ROC curve until `ROCStep` is real. ROC lookup naturally returns nothing for `X|X|X|X` so EarnMHs stays a function of `PercentEntry × BudgetMHs`, which is the right default.
**Date:** June 2026

### P6 Stub Records Use UtcNow ProgDate, Not Existing Submission ProgDate
**Rule:** Stub records created from the New ActNOs dialog get `ProgDate = DateTime.UtcNow` and appear as a sibling submission group in `ManageSnapshotsDialog` (separate row from the original Submit Week's group for the same project + week). The dialog does not look up the original ProgDate.
**Why:** Earlier iteration tried to query Azure `VMS_ProgressSnapshots` for the original ProgDate so stubs would join the existing group. The lookup timed out at 60+ seconds against a production-sized table despite the supporting covering index — query-plan issue, not worth chasing. The local snapshot mirror has only 12 columns and does not carry ProgDate, so a local lookup isn't possible either without widening the mirror. Sibling-group divergence is cosmetic in ManageSnapshotsDialog only; data is intact and downstream consumers don't depend on group consolidation.
**Date:** June 2026

### Schedule Reports Export Carries an `AssignedTo` Origin Column
**Rule:** The Schedule Reports export (3WLA / 6WLA / 9WLA workbook produced by `ScheduleReportExporter`) writes a 23rd `AssignedTo` column at the far right, populated with `App.CurrentUser!.Username` of the user running the export. The same value writes on every row across master rows, P6-not-in-MS rows, and MS-not-in-P6 rows. The header gets the grey `#D9D9D9` band that the Identity/Flags group (cols 1-3) uses, distinct from the yellow `#FFEB9C` 3WLA/Planning band (cols 16-22). The P6 export (the file headed back into Primavera) does NOT carry this column.
**Why:** Schedulers receive Reports exports from multiple users for the same week and lose track of which file came from whom — especially after rename, merge, or sort. Filename stamps were considered safer but schedulers explicitly asked for an in-file column because filenames get renamed. Same-value-on-every-row preserves the origin marker through any slice/paste/merge. P6 stays clean because Primavera's import schema is strict and an unknown column risks rejection.
**Date:** April 2026

---

## Work Packages Module

### WP Name Pattern Drives the On-Page Header; Output Filenames Are Configurable Per-Template Patterns
**Rule:** The "WP Name Pattern" field (Generate tab) controls only the `{WPName}` token drawn top-right of every form page by `BaseRenderer.RenderHeader` — it never affects the filename. Output filenames come from two token patterns stored on the WP template (`WPTemplateSettings.IndividualFileNamePattern`, default `{FormIndex}. WP {FormName}`; `WPTemplateSettings.MergedFileNamePattern`, default `{WorkPackage} - WP`). `WorkPackageGenerator.ResolveFileName` substitutes the per-form tokens `{FormIndex}`/`{FormName}`, resolves the rest through `TokenResolver`, strips any unresolved `{tokens}`, and sanitizes via `FileNameHelper`. Empty/absent patterns fall back to the default constants, so legacy templates are unchanged. A per-run used-path set (`ClaimUniquePath`) disambiguates same-run collisions with " (n)" rather than overwriting; it does not consult the disk, so re-running still overwrites prior output.
**Why:** Users needed control over output naming (e.g. a leading form index for print ordering), but the on-page header token serves a different purpose and stays independent. Patterns live on the template (not a user setting) so each template names its output consistently for everyone, and they ride the existing `DefaultSettings` JSON — round-tripping through Export/Import for free. Storing default constants on `WPTemplateSettings` keeps the generator, editor load/save, and the live example in lockstep. The collision guard replaced the old hardcoded `{WorkPackage}`-prefixed names, which were previously the only thing preventing overwrites in No-Subfolders mode.
**Date:** 2026-07-18

### `TokenResolver` Uses a Dynamic Column Allowlist via PRAGMA
**Rule:** `Services/TokenResolver.cs` loads the Activity column list once via `PRAGMA table_info('Activities')` and caches it under a lock. That list is the token-name allowlist. Per-WP token loading runs a single wide `SELECT [col1], [col2], ... FROM Activities WHERE WorkPackage = @wp` and computes per-column distinct sets in-process. `SchedActNO` and `PhaseCode` (defined in a `CommaSeparatedFields` set) resolve to comma-joined sorted lists; every other column resolves to the first distinct value alphabetically. Special tokens (`{PrintedDate}`, `{ExpirationDate}`, `{WorkPackage}`, `{WPName}`, `{PKGManager}`, `{Scheduler}`, `{CurrentUser}`, `{CurrentDate}`, `{CurrentTime}`) and project tokens are unchanged.
**Why:** The earlier hardcoded 17-field allowlist meant `{Estimator}`, `{DwgNO}`, `{UDF11}`, etc. silently rendered as literal text in PDFs. Dynamic allowlist closes the gap automatically and stays in sync with future schema additions. One wide query is one round-trip and one prepared-statement plan instead of seventeen — material savings as the table grows.
**Date:** May 2026

### `"(none)"` Magic-String Sentinel Skips Image Drawing
**Rule:** When a Generate-tab user checks "No Image" for the logo, `GetResolvedLogoPath()` returns the literal string `"(none)"`. `BaseRenderer.LoadImage()` recognises that exact string and returns null without falling back to the embedded default. Cover template "No Image" takes a different shape — `bool NoImage` on `CoverStructure` (persisted in template JSON) — because the cover image is part of the saved template definition, not a runtime Generate-tab preference.
**Why:** Existing convention was `null/empty = use default embedded logo`, file path = use that file. Adding a third state ("skip entirely") via the sentinel uses the same single-string parameter end-to-end and only requires a one-line check in `LoadImage`. A `bool noLogo` parameter threaded through every renderer call would have been broader and more invasive.
**Date:** May 2026

### Mutually-Exclusive Output Checkboxes Cascade Naturally
**Rule:** "No Subfolders" (`chkNoSubfolders`) and "Individual PDFs" (`chkIndividualPdfs`) cannot both be checked. When the user checks one, the handler clears the other by setting `IsChecked = false`. That synchronously fires the other checkbox's `Unchecked` event, whose handler persists its own setting normally. Each handler is symmetric and only knows its own setting. No suppress-flag, no manual cascade.
**Why:** An earlier `_suppressOutputModeMutex` design left the auto-unchecked checkbox's setting unpersisted (the early-return guard skipped its `SetUserSetting` call), letting the two UserSettings drift from the actual checkbox state. Natural cascade is simpler and keeps both settings consistent: the mutex `if`-block (`own=true && other=true`) inherently fails on the cascaded inner call because the side that just got unchecked is now `false`.
**Date:** May 2026

### Drawings Are a Form Type Sourced From a Parent Folder of Per-WP Subfolders
**Rule:** The `Drawings` form type (`DrawingsStructure { Title, ParentFolderPath }`) references a parent folder whose immediate subfolders are named exactly per WorkPackage. At generation, `DrawingsRenderer` merges every top-level PDF in `{ParentFolderPath}\{WorkPackage}` (alphabetical, native page size) into that work package at the form's position; a missing/empty subfolder yields no pages and is skipped. Missing subfolders are surfaced to the user in the UI (`BtnGenerate_Click`, per selected WP) with a cancel/proceed-without prompt before generation. There is no DwgNO-matching, no image-per-page layout, and no Procore source (a Procore drawings source will be designed fresh later).
**Why:** A work package's drawings are inherently per-WP, and a folder-named-by-WorkPackage is the simplest location scheme users can maintain without configuration — it replaced the earlier undecided "per-WP location architecture" and the old DwgNO-filename-matching Fetch tool (which only copied files next to the output, never merged them). Keeping the missing-folder prompt in the UI keeps the renderer a pure service.
**Date:** 2026-07-17

### External File Form Templates Merge a Static PDF, Sized Per-Page
**Rule:** An `ExternalFile` form template (`ExternalFileStructure`) stores an absolute path to a PDF (no tokens — the same file for every WP). At generation, `ExternalFileRenderer` loads it and `WorkPackageGenerator.MergeDocuments` copies each page into the output at that page's own size (via a per-page `PdfSection`), so an 11x17 sheet is not clipped to letter. A form that yields zero pages (missing file, blank path) is dropped from both the merged and individual outputs. Missing files are surfaced to the user in the UI (`BtnGenerate_Click`) before generation — proceed-without or cancel — never as a silent skip inside the service.
**Why:** Merging referenced PDFs into a work package needs their native page geometry preserved; the previous single-letter-size merge would have clipped larger sheets. Keeping the missing-file prompt in the UI keeps the renderer a pure service (no dialogs) while still giving the user the cancel/continue choice. The path is stored verbatim (not run through the output-filename sanitizer, which is for names, not paths).
**Date:** 2026-07-17

### Embedded Progress Books Are Generated Inline, Scoped to the Work Package
**Rule:** A saved Progress Book layout can be added to a WP template as a form (`FormReference.ProgressBookLayoutId`, a nullable alternative to `FormTemplateId`; in the editor it rides a synthetic `FormTemplate` of type `ProgBook` carrying `LinkedProgressBookLayoutId`). At generation, `WorkPackageGenerator` loads the layout, overrides only its `FilterField`/`FilterValue` to `WorkPackage = <current WP>`, and produces the book via the shared `ProgressBookGenerationService` — honoring every other layout setting (columns, groups, sorts, paper size, exclude-completed, IncludeAllUsers). The full book (cover + data pages) merges at the form's position; a missing layout or an empty result is skipped. `ProgressBookGenerationService` is the single source for the activity-query + cover-page-totals logic, used by both this path and the Progress Books module's Generate dialog.
**Why:** A work package's progress book should reflect that package, so the layout's saved filter value is contextual and gets replaced; the rest of the layout is the user's presentation choice and is preserved. Sharing one service keeps the two entry points from drifting (they previously duplicated the query/cover logic). Representing a layout as a synthetic `FormTemplate` lets it reuse the existing WP-forms list/reorder/save plumbing without a parallel collection type. Template Export/Import does not yet carry these refs (layouts would need exporting too).
**Date:** 2026-07-17

### Form Template Save Creates a Copy on Name Change; Rename Is a Separate In-Place Action
**Rule:** Saving a form template inserts a new record when the selected template is absent, built-in, not yet in the DB (a brand-new `+ Add New` template), or its name differs from the loaded template's name — so changing the top Name field and clicking Save always produces a *copy*. Renaming a form in place (updating the existing record) is a distinct action: the External File editor exposes a dedicated Rename control that calls `UpdateFormTemplateAsync`. WP templates reference forms by `TemplateID`, so an in-place rename keeps those references valid.
**Why:** The Name-field-plus-Save "save as copy" behavior is long-standing and users rely on it; an earlier attempt to make a name change rename in place broke it. Splitting the two intents (Save = copy under a new name, Rename = edit this record) keeps the familiar behavior while giving a non-duplicating rename. The DB-membership check also fixes brand-new templates saved without renaming, which previously took the UPDATE path and persisted nothing.
**Date:** 2026-07-17

---

## Progress Books Module

### Textract Beats Claude Vision for OCR
**Rule:** Progress Book OCR uses AWS Textract for table extraction.
**Why:** Textract returns proper table structure with row/column indices, yielding 100% accuracy. Claude Vision had inconsistent accuracy between PDF and JPEG inputs. Tool Use (function calling) was tried first; Textract's native table detection still won.
**Date:** January 2026

### Progress Book Identifies Records by ActivityID, Not UniqueID
**Rule:** The OCR pipeline reads `ActivityID` (integer) from each scan row, not `UniqueID` (long string).
**Why:** Shorter integer values are more reliable to OCR from scanned handwritten pages. Long strings produced too many character-level errors.
**Date:** January 2026

### "Write 100" Means Done — No Done Checkbox
**Rule:** Progress Book scan forms have no Done checkbox. Writing `100` in the `% ENTRY` box is the way to mark a record complete.
**Why:** Simplified the scan form and improved AI accuracy by reducing distinct columns to parse. Color-coded entry fields were also removed — AI relies on text labels, not colors.
**Date:** January 2026

### `% ENTRY` Is the Only Un-Removable Progress Book Column
**Rule:** The handwriting entry cell (`FieldName = "% ENTRY"`, `SourceKind = EntryBox`) cannot be removed from a Progress Book layout. Every other column — `ActivityID`, `ROCStep`, `Description`, `BudgetMHs`, `Quantity`, `RemainingMHs`, `PercentEntry`, and any user-added Activity field — is removable.
**Why:** A Progress Book without an entry cell isn't a Progress Book. The handwriting cell is the feature's reason for existing; every other column is a presentational choice for field crews and should be theirs to shape.
**Date:** 2026-06-04

### Synthetic Display-Only Columns Can't Be Filter or Exclude Columns
**Rule:** The two synthetic Progress Book columns — `% ENTRY` (`EntryBox`) and `RemainingMHs` (computed) — are excluded from the "Select Progress Book(s)" Column picker and the Exclude-Column picker (via `NonFilterableFields` in `ProgressBooksView`). `GetSelectedFilterColumn()` also falls back to `WorkPackage` if a saved layout still names one, so a stale value can't reach SQL. Both remain fully available as PDF display columns.
**Why:** These fields map to no real Activity/DB column. Selecting `% ENTRY` built `WHERE % ENTRY IS NOT NULL`, which SQLite rejects with `near "%": syntax error`; `RemainingMHs` would fail as "no such column." Filtering records by a handwriting cell or a computed value is meaningless anyway — they're presentational, not filterable data.
**Date:** 2026-07-17

### Progress Book Value Filter Is a Search-Box-Plus-List, Not an Editable Combo
**Rule:** The Progress Books "Value" filter is a `ToggleButton` opening a `Popup` that holds a plain search `TextBox` above a `ListBox`. Typing filters the list by case-insensitive substring; selection is single, list-only (no free text). The combobox's own editable text is never involved.
**Why:** An editable dropdown that filters its own text is the fragile part in WPF — native and Syncfusion `ComboBoxAdv` editable variants glitched on caret/backspace, and `SfTextBoxExt` autocomplete with `ShowSuggestionsOnFocus` froze the UI on focus. Separating the typing (a normal TextBox) from the list (a normal ListBox) makes filtering deterministic and eliminates the whole class of editable-combo focus/caret bugs. Same shape as the Progress module's column-filter UI.
**Date:** 2026-07-17

### Progress Book Column Metadata Lives in a Single Catalog
**Rule:** `Models/ProgressBook/ProgressBookColumnCatalog.cs` is the single source of truth for column `SourceKind` and `DisplayHeader`. Both `ProgressBooksView` (column list label, Add dropdown, layout migration, `BuildCurrentConfiguration` round-trip) and `ProgressBookPdfGenerator` (header text + value dispatch + alignment) read from it. The catalog overrides whatever `SourceKind` / `DisplayHeader` was stored in a saved layout's JSON whenever its `FieldName` is catalogued — renaming a label in the catalog propagates to every saved layout on its next open with no JSON migration. Uncatalogued fields render with their `FieldName` as-is in both UI and PDF (no `.ToUpper()` fallback).
**Why:** Pre-catalog, two independent sources (`ProgressBooksView._columnMeta` covered the five promoted columns; `ProgressBookPdfGenerator.GetColumnDisplayName` covered a different nine, with `.ToUpper()` as the fallback) drifted out of sync and produced UI/PDF mismatches (e.g. UI list said "BudgetMHs", PDF header said "BUDGETMHS"). Centralising in one read-only dictionary makes the UI list and PDF header agree by construction.
**Date:** 2026-06-05

### Progress Book Layout SchemaVersion Drives Migration; Saver Must Stamp It
**Rule:** `ProgressBookConfiguration.SchemaVersion` has NO property initializer (defaults to `0`). `CreateDefault()` and `BuildCurrentConfiguration` both explicitly stamp `CurrentSchemaVersion` when constructing a new config so saved JSON carries the current version. `MigrateConfigurationIfNeeded` only treats a layout as legacy when `SchemaVersion < 2` AND the un-removable `% ENTRY` column is absent — its presence is the cleanest signal a layout was already saved under the new schema.
**Why:** A property initializer of `CurrentSchemaVersion` would make legacy JSON (which has no `schemaVersion` key) deserialize as if it were already current, and the migration would never fire. Conversely, treating any `SchemaVersion < 2` layout as legacy (regardless of column contents) caused user-deleted promoted columns to be silently re-added on every Load. The two-part check is the minimal correct heuristic.
**Date:** 2026-06-05

### VP-vs-Vtg Match Uses Outer-Trim-Only Exact String Comparison
**Rule:** ProjectID and PhaseCode matching between the JC Labor Productivity report and Vantage `VMS_Activities` uses `String.Trim()` on both sides (leading and trailing whitespace only) and compares the result as exact strings. No leading-zero stripping, no internal-whitespace collapsing, no trailing-separator trimming. The previous `NormalizeKey` helper does not exist.
**Why:** Vantage phase codes should match VP's canonical format exactly. A `Not Found` result is now a useful signal — it identifies Vantage records whose codes need correction to match VP. Outer-whitespace trim is the only concession because Excel cell formatting and SQL CHAR padding can introduce spaces that aren't user-meaningful.
**Date:** April 2026

### VP-vs-Vtg Color Coding Stays on the Two Added Columns
**Rule:** Only the two generated columns (`Vtg Budget`, `Vtg Earned`) receive conditional fill: green within 1% of source, red over 1%, orange `Not Found`. The companion source columns (`Est Hours`, `JTD ERN`) are not recolored. Every data row receives a color so mismatches are never ambiguous with "not yet checked".
**Why:** Minimises modifications to the source report and keeps the visual signal scoped to Vantage-sourced values. Cross-cell pairing for mismatches was tried first; the user preferred narrower marking.
**Date:** April 2026

### VP-vs-Vtg Prep Dialog Is Separate From the File Picker
**Rule:** Before the standard Windows `OpenFileDialog`, a custom WPF verification dialog (`VPvsVtgPrepDialog`) shows instructions and an annotated screenshot. On OK, the native file picker opens. Two dialogs, not one custom combined picker.
**Why:** The native `OpenFileDialog` is a Windows OS control users already understand; replicating it would be significant work for no gain. The OS picker can't show an image or rich text — a small pre-dialog is the right tool for that single gap.
**Date:** April 2026

### VP-vs-Vtg Skip-Dialog Flag Persists Per-User
**Rule:** The "Do not show this dialog again" checkbox in the prep dialog writes `SkipVPvsVtgPrepDialog=true` to `UserSettings` (per-user), not `AppSettings` (app-wide).
**Why:** Once a user has read the instructions, they don't need to see them again. Other users on the same machine haven't necessarily learned yet. Matches how other dismissible UI state is stored (grid layouts, filter selections, analysis group field).
**Date:** April 2026

---

## Analysis Module

### Chart Filters Are Independent From the Summary Grid
**Rule:** The chart-filter panel applies to charts only. The summary grid has its own independent filters (Group By, My Records / All Users, Projects).
**Why:** Different analytical needs — charts for visual exploration across many dimensions, summary grid for its own grouping context. A shared filter set would force one to compromise.
**Date:** April 2026

### Project Selection Is Not Persisted
**Rule:** Analysis auto-selects the first project from current local data on every load. No saved/restored selection.
**Why:** Stale saved selections pointed to projects no longer in local DB after clear/re-sync, leaving the table empty with no obvious cause.
**Date:** April 2026

### Chart Filters Are Session-Only and Lazy-Populated
**Rule:** The 12 chart-filter `ComboBoxAdv` dropdowns on the Analysis tab hold their selections only as long as the cached `AnalysisView` instance lives. Selections survive Progress ↔ Analysis nav (via view caching) and die at app close. Each dropdown's items are populated by a `SELECT DISTINCT` against `Activities` only on first `DropDownOpened` for that filter, tracked via a `_populatedFilters` HashSet. No `UserSettings` row is read or written for chart filter state.
**Why:** Cross-session persistence previously wrote every selected value to `AnalysisFilter_<field>` as a comma-joined string, and the cold-load restore loop called `combo.SelectedItems.Add(val)` once per saved value. At ~2,000+ saved SchedActNOs this rehydration wedged the UI thread on every Analysis tab open. Session-only is the same model the Progress module already uses for column filters and is acceptable for the analytics workflow.
**Date:** May 2026

### Chart Filter Dropdowns Have a `(Select All)` Sentinel Row and an ALL/[N] Count Badge
**Rule:** Every populated chart-filter `ComboBoxAdv` has `(Select All)` as the first item, injected during `ChartFilter_DropDownOpened` right after the optional `(blank)` row. Clicking it toggles every real item (clear-all when all are checked; select-all otherwise) under the `_isInitializing` guard, and the sentinel itself never remains in `SelectedItems`. The sentinel string is defensively filtered out of `AppendChartFilterClauses` so it cannot reach SQL. Each filter label carries an `lblCount_<Field>` badge displaying `ALL` when zero items or every item is selected, and `[N]` when 1..N-1 are selected.
**Why:** "Zero selected" and "all selected" produce the same chart query result (`AppendChartFilterClauses` skips the WHERE clause entirely when `selected.Count == 0`; a full IN list matches every row), so there is no semantic NONE state — both are "filter not narrowing the data". The toggle item gives Excel-style bulk control without modal dialogs or extra buttons. Per-item rehydration of large selections still pauses Syncfusion's internal layout on the order of seconds for ~1,500-item filters; the user opts in by explicitly clicking the toggle, and view caching limits this cost to once per session per filter.
**Date:** May 2026

### Analysis Summary Grid Has a Local / Snapshot Source Toggle Backed by a Local Cache Table
**Rule:** The Analysis summary grid aggregates from one of two local SQLite tables, picked by a Source radio pair in the toolbar: **Local** reads from `Activities` (the user's live working set), **Snapshot** reads from `SnapshotAnalysis` (a local cache mirror of the `Activities` schema, schema v13). The Snapshot picker (`Dialogs/SelectAnalysisSnapshotsDialog`) is the only thing that pulls from Azure `VMS_ProgressSnapshots` — it wipes and bulk-inserts the user's checked submission tuples into `SnapshotAnalysis` on Apply. The Snapshot radio itself does NOT auto-open the picker; it just switches the source and re-aggregates whatever's already cached. Empty cache = empty grid. The Select button is enabled only when the Snapshot radio is checked. Both radio pairs have explicit `GroupName`s (`AnalysisUserFilter`, `AnalysisSource`) so they behave as two independent groups.
**Why:** Selection-triggered Azure aggregation queries froze the UI on every click and re-ran the slow query for every filter change. Caching the chosen snapshots locally separates the slow Azure pull (once per Apply, off the UI thread, busy overlay) from the fast aggregation (always local, always sub-second), and the cache persists across app restarts. Mirroring the `Activities` schema lets the same aggregation SQL serve both sources — `LoadSummaryFromLocalTable(groupField, sourceTable)` with an `Activities` / `SnapshotAnalysis` allowlist. Selection state lives in the cache table itself (DISTINCT on Apply for pre-check), not a separate persisted list, so cache and remembered selection can never drift apart.
**Date:** June 2026

### Analysis Source Mode Default Is Local; My Records / All Users Default Is All Users
**Rule:** When no saved `AnalysisSourceMode` exists, the Source radio defaults to Local. When no saved `AnalysisCurrentUserOnly` exists, the user-filter pair defaults to All Users. Both settings persist on every change (immediate write to `UserSettings`) and survive app restart.
**Why:** Local is the safe, always-fast, no-network default for first-launch and for users who never engage with Snapshot mode. All Users is the more common "what's the project doing" Analysis question — biasing to My Records hid data new users were trying to find. Immediate save (vs. write-on-Unloaded) is required because the Analysis tab is often the last view active when users close the app, and Unloaded doesn't reliably fire in that path.
**Date:** June 2026

### Analysis Snapshot Picker Groups by `(AssignedTo, ProjectID, WeekEndDate)`, Not ProgDate
**Rule:** The Analysis snapshot picker dialog lists submissions grouped by `(AssignedTo, ProjectID, WeekEndDate)` — matching `ManageSnapshotsDialog`. ProgDate is intentionally not in the GROUP BY or in the cache table's selection key. The `INNER JOIN` that restricts the Azure pull joins on `(ProjectID, WeekEndDate, AssignedTo)` only.
**Why:** Scanning ProgDate across all rows of `VMS_ProgressSnapshots` times out at production volume — there's no index plan that makes it sub-second. The `(user, project, week)` tuple is effectively unique for a submission; the edge case is the sibling submission group the New ActNOs from P6 dialog creates with a `UtcNow` ProgDate, and in that case rolling those siblings into the same analysis result is the correct semantic anyway.
**Date:** June 2026

---

## Takeoffs

### Common

#### Takeoff Lifecycle Lives in `App.CurrentTakeoff`, Not the View
**Rule:** Upload, start, and poll lifecycle of an AI Takeoff batch are owned by `Services/AI/TakeoffSession.cs` held at `App.CurrentTakeoff` (mirroring `App.CurrentUser`), not by `TakeoffView.xaml.cs`. The view is a thin subscriber that rebuilds itself from session state on `Loaded` and unsubscribes on `Unloaded`. The session also owns its own `TakeoffService`, `CancellationTokenSource`, and any future timer.
**Why:** `MainWindow.BtnTakeoff_Click` destroys and recreates the `TakeoffView` instance on every navigation. With state in the view, switching to Schedule mid-batch orphaned the view but left its async state machine running invisibly — the polling loop kept writing to a `txtStatus` the user could no longer see, and a deferred `SaveFileDialog` could pop from a detached view. Hoisting state above the view lifecycle lets the user navigate freely while a takeoff is in flight.
**Date:** April 2026

#### Bottom-Bar "Takeoff: Complete" Is Sticky; Cancellation Doesn't Set It
**Rule:** `App.HasCompletedTakeoffSinceStartup` is set the first time a takeoff session raises `Completed` with `CompletedSuccessfully == true`. It stays set until the next batch starts (flips to Running) or the app closes. Cancelled or failed batches do not set the flag.
**Why:** The bottom-bar indicator confirms a successful batch happened in this app session. Reusing the Complete state for cancellation would mask user mistakes (cancel-by-accident on a long batch, walk away thinking it succeeded). Per-app-session lifetime keeps the indicator scoped to "what you did right now".
**Date:** April 2026

#### SaveFileDialog Auto-Opens on Return; Cancel Doesn't Re-Pop
**Rule:** When a takeoff completes successfully while the user is on another tab, `TakeoffSession.PendingDownloadBatchId` is set. On returning to the Takeoffs tab, `RestoreFromSessionIfActive` calls `ClearPendingDownload()` first and then opens the SaveFileDialog. Whether the user saves or cancels, the flag is already cleared. Recovery from a cancelled save is via the Previous Batches button.
**Why:** Frictionless on-return: dialog opens directly, no inline confirm. Re-popping the dialog on every navigation would punish users for cancelling once. Clearing before the await also defends against a SaveFileDialog crash leaving the flag set indefinitely.
**Date:** April 2026

#### Last Config Persists by Key; Rates / Bubble / Send-Missed Don't Persist
**Rule:** `Takeoff.LastConfigKey` UserSetting persists the last-selected config across tab navigations and app sessions. Lookup is by `_configs[i].Key`; if the saved key no longer exists, fall back to the first available config. The Unit Rates dropdown, Rev Bubble Items Only checkbox, and Send Missed Makeups to Admin checkbox are NOT persisted — each session defaults to "Default (Embedded)" / unchecked / unchecked.
**Why:** Config selection is high-friction to re-pick from a dropdown each session and almost always the same one; key-based persistence survives reorder/rename/delete (index-based did not). The other three controls are deliberately friction by design — Unit Rates and the two checkboxes change the meaning of the resulting Excel (project-specific rates, scope of extraction, who gets emailed).
**Date:** April 2026

#### Uploaded Drawings Are Deleted From S3 After Processing
**Rule:** S3 uploaded drawings are deleted automatically after takeoff processing completes.
**Why:** Drawings are overwritten on each run anyway (to support new revisions with the same filename), so persisting them serves no purpose.
**Date:** March 2026

#### Configs Are Namespaced by Username
**Rule:** Config S3 path uses `clients/{username}/{config-name}.json`.
**Why:** Each user gets their own namespace. A client/project hierarchy was overkill for what's effectively a personal config store.
**Date:** March 2026

#### In-App Results Panel Does Not Exist
**Rule:** `TakeoffView` does not show an in-app results summary after a batch. The Excel file is the deliverable.
**Why:** The same information is available in the downloaded Excel. The in-app panel was redundant.
**Date:** March 2026

#### Failed DWGs Tab Is Owned by the Aggregation Lambda
**Rule:** The "Failed DWGs" tab in the batch Excel is written exclusively by the AWS Aggregation Lambda from S3 failure markers (`failures/{drawing}.json`). The WPF app does not create, overwrite, or delete this tab on any path — initial download, Recalc Excel, or re-download from Previous Batches. The tab carries 4 columns: Drawing Name, Source Key, Error message, UTC timestamp.
**Why:** The prior app-side `WriteFailedDrawingsTab` helper wrote a 1-column tab from an in-memory list captured during SFN polling. The Lambda's 4-column schema carries diagnostic depth the app can't reconstruct. Reading failures from S3 markers also means re-runs and Recalc Excel present a stable, full history. Single writer eliminates the class of bug where a null/empty `failedDrawings` parameter silently destroyed the tab.
**Date:** April 2026

#### Failed-Drawing Counts Come From the Excel, Not the Step Functions Output
**Rule:** The polling loop checks only the Step Functions `status` field for aggregation-level failure. Success/failure counts are computed after download by counting data rows in the batch Excel's "Failed DWGs" tab. `succeeded = totalSubmitted - failedDrawings`.
**Why:** The state machine's `BatchComplete` Pass state strips the Aggregation Lambda's rich return payload. Modifying the Pass state requires AWS Console coordination and still leaves the app coupled to SFN output shape. Reading the Excel uses data the app already has after download and works regardless of how the Pass state evolves. The "N succeeded, M failed" status appears after Excel save rather than before — preliminary "Completed in {elapsed} — downloading results..." status bridges the gap.
**Date:** April 2026

#### Size Cells Carry 1 to 7 X-Separated Numeric Values
**Rule:** A `size` value is either a single numeric quantity (whole number, fraction, mixed number, or decimal) OR two-or-more numeric quantities joined by `X`/`x` separators. The model captures every value present in the cell — multi-value sizes appear on tees, crosses, multi-branch fittings, and custom shop-fab items, with up to seven dimensions per cell. Each segment between separators must independently form a valid single numeric quantity. Trailing-X detection iterates across continuation lines until the result no longer ends with `X`.
**Why:** The MCAA ratesheet keys concatenate up to seven sizes per row, and real BOMs contain 3-axis tees (`4X3X2`), crosses (`4X3X4X2`), and custom multi-branch fittings. The earlier "exactly two values" cap silently truncated these to two, masking dropped dimensions in downstream rate lookups. Capturing the full size string is the correct primitive; downstream consumers (C# normalization, the future MCAA rate lookup) decide which dimensions matter for their purpose.
**Date:** May 9, 2026

#### Size and Quantity Are Returned Raw-as-Printed; Conversion Is Centralised
**Rule:** Claude returns `size` and `quantity` exactly as printed on the drawing — fractions like `"3/4"` and `"1-1/2"`, reducing strings like `"6X4"`, length strings like `"27'10\""`, `"5.5 ft"`. The aggregation Lambda converts size to decimal at the Excel write boundary. The C# app's `NormalizeMaterialQuantities()` (called once from `GenerateLaborAndSummary` immediately after `WriteMaterialShopField`) converts quantity strings to decimals and writes them back to the Material tab. Size normalisation moved into C# as Step A1 (`NormalizeMaterialSizes` → `WriteMaterialSizes`), mirroring the quantity pattern. Downstream consumers read normalised doubles. Failures log `SIZE_NORMALIZATION_FAILED` / `QUANTITY_PARSE_FAILED` and leave the raw text in the cell for manual correction.
**Why:** Vision models echo printed text more accurately than they convert it. The earlier prompt asked Claude to convert; results were silently wrong on uncommon unit forms. Centralising conversion makes parse failures visible at one well-known point per value type rather than hidden in a multi-layer silent-fallback chain. Size normalisation is in C# (rather than Lambda) because tweaking a regex in C# ships in the next app release and applies to every Recalc Excel pass on existing takeoffs; tweaking it in Lambda requires an AWS deploy and only helps new takeoffs.
**Date:** April 2026

#### Malformed-Size Rows Are Surfaced to a Dedicated Tab, Not Silently Priced at Zero
**Rule:** When a material row's `Size` contains an unbalanced "X" — i.e., contains `x`/`X` and `FittingMakeupService.ParseDualSize` returns null — `ExplodeMaterialRow` adds the row to `_malformedSizes` and returns immediately, skipping all labor explosion (no fab row, no connection rows, no THRD companions). A "Malformed Sizes" worksheet is written from that collection with Drawing Number, Item ID, Component, Size, Quantity, Connection Type, Raw Description, and the fixed Reason `"Size contains unbalanced 'X' — labor generation skipped. Verify against drawing."`. The Summary tab carries a "Malformed Sizes" count line.
**Why:** A real bug had the model emit `Size = "0.5x"` for a TEE outlet — `ParseDualSize` returned null, the fallback `GetDouble(mat, "Size")` parsed `"0.5x"` as 0, and six connection labor rows were silently emitted at size 0 with no rate. The C# regex genuinely cannot infer the missing portion (only the drawing knows the outlet size); silently picking either side is worse than skipping. The new prompt's size-validity contract should prevent the model from ever emitting unbalanced X again, but defense-in-depth catches future slips at the boundary.
**Date:** April 2026

#### `connection_size` Field Does Not Exist
**Rule:** `connection_size` is not in the extraction prompt JSON schema, the aggregation Lambda's row-build code, the Lambda column mapping list, or the Labor tab's `explicitColumns`. The Summary tab's "CONNECTIONS BY SIZE" section groups by `Size` (per-row connection size).
**Why:** `connection_size` was a phantom field — the model emitted it, the Lambda passed it through, the Material/Labor tabs displayed it, but no C# code computed labor or makeup from it. The single remaining consumer was a Summary grouping that bucketed reducer SCRDs incorrectly (a `2x0.5` REDT's run-side and outlet-side rows both grouped into bucket `"2x0.5"`). Switching the grouping to `Size` fixes that bucketing and saves prompt tokens, JSON bytes, and a column reviewers had to scroll past.
**Date:** April 2026

#### Unmatched Material Returns Null, Not Default-CS
**Rule:** When Claude can't match a BOM description's material to the Material Reference Table, `matl_grp` and `matl_grp_desc` return as `null`. The C# app's `CorrectFsMaterial` step still inherits pipe material for FS rows on the same drawing. Non-FS rows with unmatched materials surface as empty cells on the Material tab for manual review.
**Why:** A CS default masked two problems. FS items whose descriptions don't mention material are always overwritten app-side anyway — the CS guess did no work. Non-FS items with exotic materials silently flowed into rate calculations with the wrong multiplier. Returning null surfaces these cases for human review.
**Date:** April 2026

#### Labor-Tab Description Excludes Pipe Spec
**Rule:** The Labor tab's `Description` column for connection rows is built from `"{size} IN - {thickness} - {class} - {material} - {connType}"`. Pipe spec is not injected. For BU rows the trailing `{connType}` segment is the literal string `"HALF BU ONE FLANGE ONLY"` instead of `"BU"` so reviewers can see at a glance that each row prices one flange, not a full joint.
**Why:** The prior `FindPipeSpec` heuristic latched onto the first title-block field whose key contained "spec" — fragile on drawings with multiple spec fields (Pipe / Coating / Insulation), silently broken on drawings labelled "Pipe Schedule" instead. Every title-block field already flows to Labor as a dedicated trailing column, so injecting a best-guess spec into Description added no information — only confusion when wrong. The explicit BU suffix exists because the per-flange convention (one bolt-up joint produces two labor rows) is easy to miss when scanning the Labor tab and an obvious caption prevents double-counting on visual reviews.
**Date:** May 2026

#### BU Labor Rows Are Per-Flange: Quantity 0.5, Full Joint Rate
**Rule:** Each bolt-up joint produces two BU labor rows (one per flange). Each row carries `Quantity = 0.5` and the full per-joint rate from the rate sheet — `ApplyRates` does NOT halve the rate for BU. Per-row `BudgetMHs = mhu × mults × 0.5`; per-joint total across the two rows equals one full per-joint rate. CUT/BEV companion adds are zero for BU (neither branch in `ApplyRates` applies). The invariant `RateSheet × Quantity = BudgetMHs` holds for BU rows the same as for every other connection type.
**Why:** The earlier convention used `Quantity = 1` with the rate halved inside `ApplyRates` (`mhu /= 2`), which produced identical totals but broke the `RateSheet × Quantity = BudgetMHs` audit invariant — the rate column on a BU row no longer matched the rate sheet, confusing review. Shifting the halving to the Quantity side keeps the audit columns honest: a reviewer can multiply RateSheet × Quantity and reach BudgetMHs on every row type without special-casing BU. The Summary tab's BU counts (`buCount = Math.Ceiling(buRows / 2.0)`) are unaffected — they count rows, not quantities.
**Date:** May 26, 2026

#### Flagged Tab Is Minimal; Material Is Authoritative
**Rule:** The Flagged tab carries 13 columns (drawing number, item ID, size, description, component, connection qty/type, material group + desc, thickness, class rating, confidence, flag reason) and no override columns. It does NOT mirror Material — `connection_size`, `quantity`, `commodity_code`, `length`, `shop_field`, and all `tb_*` title-block columns are intentionally omitted. Reviewers match by `(Drawing Number, Item ID)` back to Material to see context or apply corrections.
**Why:** Flagged is a worklist, not an editor. Earlier override columns were decorative — full repo grep found zero C# consumers — and wiring them up would duplicate Material editing. The Material tab is the single source of truth for BOM data; "Recalc Excel" regenerates Labor and Summary from whatever is on Material.
**Date:** April 2026

#### Send Missed to Admin Defaults Unchecked, Doesn't Persist
**Rule:** "Send Missed Makeups and Rates to Admin" defaults to unchecked on every tab load. There is no saved preference.
**Why:** Persisted as checked, users were unintentionally emailing admins on every batch. Default-off makes the email an intentional opt-in each time.
**Date:** April 2026

#### Material-Tab ShopField: Authored Entirely by C#, Lives at Column 5
**Rule:** The C# post-processor is the sole writer of the `ShopField` column on the Material tab. `AssignMaterialShopField` seeds every row to `1` (Shop) in-memory, then flips qualifying rows to `2` (Field): items with `BU`/`SCRD`-only connection types, components that are inherently field work (`FS`, `BOLT`, `GSKT`, `WAS`, `INST`, `GAUGE`), and items with zero connections or empty connection type. PIPE always stays Shop. Mixed connection types (e.g., `BW, SCRD`) stay Shop. `WriteMaterialShopField` removes any pre-existing ShopField column wherever it appears in the Lambda-produced workbook (defensive — the aggregation Lambda no longer emits one) and inserts a fresh column at position 5, between `Size` and `Raw Description`.
**Why:** ShopField is a deterministic data lookup based on connection types and component code — there's no perception involved, so AI shouldn't author it. The aggregation Lambda previously fabricated a `shop_field=1` value that C# overwrote on every cell anyway: pure dead weight in the JSON shape and the deploy surface. Material-tab ShopField drives the Summary tab's Shop/Field bin counts on Material rows. Fixing the column at position 5 keeps it adjacent to other component metadata for reviewer scanning.
**Date:** May 9, 2026

#### Labor-Tab ShopField: Single Uniform Pass (Cross-Mode)
**Rule:** All labor row `ShopField` values are written by a single uniform pass (Step B1) immediately after `GenerateLaborRows` returns. Rule applies to both Summit and MCAA: `BW` and `SW` → 1 (shop) in both modes; `PIPE` and `TUBE` → 1 (shop) under Summit only; everything else → 2 (field). All prior scattered ShopField allocations are removed (`(BU || SCRD) ? 2 : 1` in `CreateLaborRow`, `FLGB || FLGLJ → 2` in the fab loop, `spl["ShopField"] = 2` in `GenerateSplRows`, inheritance from material rows). `"ShopField"` is in `ExcludeFromLabor` so labor builders no longer copy the AI-emitted material value. The Material-tab correction passes (`AssignMaterialShopField`, `WriteMaterialShopField`) remain — they drive the Summary tab's Shop/Field bin counts on Material rows.
**Why:** The previous scattered logic produced a mix of 1s and 2s on labor rows that didn't align with how Shop vs Field labor should bin. A single uniform pass is trivial to audit, change, or extend. Numeric MH totals are unaffected — `ShopField` doesn't influence rate lookup; only Shop-vs-Field bin counts shift.
**Date:** May 5, 2026

#### TUBE Is a Footage Component (Like PIPE)
**Rule:** `TUBE` is in the `FootageComponents` HashSet alongside `PIPE`. Both produce one labor row per BOM row with `Quantity = ParseExactQuantity(mat)` (preserving footage), not the per-quantity explosion fittings/flanges get. Description suffix swaps dynamically (`" - Fab Pipe Handling"` for PIPE, `" - Fab Tube Handling"` for TUBE). Both are excluded from the No Conns diagnostic tab via `!FootageComponents.Contains(component)` rather than hardcoded `!= "PIPE"`.
**Why:** Tube quantity in the BOM is footage (linear feet), not item count — same as pipe. Exploding a 100-ft tube run into 100 labor rows of `Quantity=1` was systematically wrong. Tube doesn't list its own connections (joints are tracked at the fitting side), so it shouldn't appear in No Conns either.
**Date:** April 30, 2026

#### FLGB and FLGLJ Generate Per-Item Install (Fab) Labor Rows
**Rule:** Blind flanges (`FLGB`) and lap-joint flanges (`FLGLJ`) produce one fab/install labor row per physical item with `ShopField=2` (field-installed). Connection (BU) explosion behaviour is unchanged — partner-flange BU rows still count the bolt-up labor. `ExcludeFromMakeupLookup` still excludes them from spool-makeup length calculations (a separate concern: pipe-spool fab length doesn't depend on whether the flange end is a blind or a slip-on).
**Why:** Per-item handling labor (rigging, hauling, positioning the disc) is separate from BU bolt-up labor. Producing zero labor rows for hundreds or thousands of FLGB/FLGLJ BOM items systematically under-counted MH on every project with these components.
**Date:** April 29, 2026

#### FS Material Group Is Auto-Corrected From Pipe Material
**Rule:** Field support (FS) `Matl_Grp` values are auto-corrected to match the pipe of the same size on the same drawing. When multiple pipe materials exist, the non-CS value wins.
**Why:** AI defaults FS to CS (carbon steel) because material isn't in the FS description. Most FS supports stainless or alloy lines on real projects — picking non-CS when ambiguous matches reality.
**Date:** April 2026

#### MakeupEquiv Uses a Two-Pass Lookup
**Rule:** Spool-makeup lookup uses `MakeupEquiv` (e.g., `ADPT→FLG`, `FLGR→FLG`) with two passes: direct match first, then equivalent-component match.
**Why:** Performance — most lookups succeed on direct match. Two-pass also makes the lookup hierarchy explicit and predictable.
**Date:** March 2026

#### ROC Splits Read Raw Takeoff Data, Not Mapped Activity Properties
**Rule:** `ApplyROCSplitsAsync` reads `ShopField` and `Component` directly from the raw takeoff Excel row data, not from mapped Activity object properties (`UDF1`/`UDF6`). Both numeric (`"1"`/`"2"`) and text (`"Shop"`/`"Field"`) values are accepted.
**Why:** Users configure column mappings in their import profile — `ShopField` might map to `UDF1`, `ShopField`, or be unmapped entirely. Hardcoding to `activity.UDF1` made ROC splits silently fail if the mapping didn't match. Reading from raw data makes the feature work regardless of mapping configuration.
**Date:** April 2026

#### Lambda Accepts Only the Config-Based Event Shape
**Rule:** The extraction Lambda's `lambda_handler` requires `config_path` in the event and raises `ValueError` if missing. The legacy direct-image event shape (`s3_path` or `bucket`+`key` for pre-cropped images) does not exist.
**Why:** The legacy path predated the config-based cropping system and was never called from the C# app — `TakeoffService.StartBatchAsync` always sends the new format. Removing it deleted ~140 lines of unexercised code and closed a non-batch zero-BOM silent-success edge case for free.
**Date:** April 2026

### Summit Rate Mode

#### Summit `BudgetMHs` Formula: RateSheet × RollupMult × MatlMult × Quantity
**Rule:** Under Summit, `BudgetMHs = (mhu × RollupMult × MatlMult + CutAdd + BevelAdd) × Quantity`. Audit columns (`RateSheet`, `RollupMult`, `MatlMult`, `CutAdd`, `BevelAdd`) appear in the workbook for verification. FS rows neutralise `RollupMult` and `MatlMult` to 1.0 (see below).
**Why:** Simple multiplication is transparent and matches industry conventions. An earlier `max(RollupMult, MatlMult)` formulation double-counted the rollup multiplier when it exceeded the material multiplier.
**Date:** March 2026

#### Summit Rate Lookup: 4-Step Fallback Chain
**Rule:** Rate lookup tries thickness as-is → toggle leading "S" → class rating → size-only. The OLW/SW class-rating tier system (40/S40/STD/2000 equivalence groups) does not exist.
**Why:** The tier system was complex and the S-toggle handles the same edge cases more simply. Dual-size parsing for all components further reduced misses.
**Date:** March 2026

#### Summit Rate Sheet Keys Match Component Names Directly
**Rule:** All 56 EstGrp keys in `Resources/RateSheet.json` are short component names. `INST`, `PIPE`, `SPL`, `BEV` match rate keys directly. Only valve types (`VBL`/`VGT` → `VLV`) and fittings (`90L`/`TEE` → `FTG`) need the `ResolveEstGrp` mapping dictionary.
**Why:** Removes indirection. The `DirectMatchComponents` set was redundant when `ResolveEstGrp` can use the component name as the key for the common case.
**Date:** March 2026

#### Summit FS Rows Neutralise RollupMult and MatlMult to 1.0
**Rule:** In `TakeoffPostProcessor.ApplyRates`, when `component == "FS"`, both `RollupMult` and `MatlMult` are set to 1.0 in the audit columns and the BudgetMHs computation. Effective formula for FS rows: `BudgetMHs = mhu × Quantity`.
**Why:** Published Summit FS rates in `Resources/RateSheet.json` already have rollup and material-group multipliers baked into the rate value. The general-purpose pass was applying them again, double-counting on every FS row. Setting audit columns to 1.0 (rather than skipping silently) makes it visually obvious in the workbook that no multiplier was applied.
**Date:** May 5, 2026

#### Summit Folds CUT and BEV Into Connection Rows
**Rule:** Under Summit, BW connection rates include CUT+BEV add-ons (via `CutAdd`); SW and SCRD include CUT. `CUT` and `BEV` do not generate separate labor rows in Summit mode.
**Why:** Cut and bevel are always performed with the connection. Folding them in matches how labor is performed and prevents row explosion in the Labor tab.
**Date:** March 2026

### MCAA Rate Mode

#### MCAA-vs-Summit Divergence Is Gated by an In-App Toggle
**Rule:** Summit-vs-MCAA divergence at takeoff time is gated by a user-selectable rate mode (`Takeoff.RateMode` UserSetting). The toggle is a radio control on `TakeoffView` between the Action Buttons row and the Options checkboxes row.
**Why:** A toggle is needed long-term anyway — Summit ratesheet is supposed to remain selectable for 6–12 months after MCAA parity is proven across 5 real projects, then sunset. Toggle puts both code paths on a single trunk and gates the divergence at runtime, instead of maintaining a parallel branch with constant merge work and divergent docs.
**Date:** May 5, 2026

#### MCAA Mode Is Restricted to a Hardcoded Username Allowlist
**Rule:** `Takeoff.RateMode = "MCAA"` is restricted to usernames in the `McaaAllowedUsers` HashSet (`"steve"`, `"steve.amalfitano"`, case-insensitive). Even an admin not in the allowlist cannot select MCAA. The dialog forces the setting back to Summit on load if a non-allowlisted user has somehow set it (export/import, registry edit). After GA the gate moves to admin-only or is removed entirely.
**Why:** Tightest possible lock during the partial-implementation window. Half-finished MCAA logic on main means selecting MCAA could ship inflated/wrong numbers. An admin-table check would still let any admin flip it. The HashSet form (vs single string) covers both username spellings the same person logs in under and future-proofs adding a second tester during Phase 3.
**Date:** May 5, 2026

#### MCAA Mode Diverges From Summit at Four Points
**Rule:** `TakeoffPostProcessor` mode-conditional gating covers:
1. **BOLT/GSKT/WAS aggregated hardware row** — Summit creates one row per material item; MCAA skips (`return result;` early).
2. **SPL spool-handling row generation** — Summit emits via `GenerateSplRows(materialRows)`; MCAA skips the entire call.
3. **BW/SW companion CUT row** — Summit skips; MCAA emits one CUT row per BW or SW connection (size, thickness, material, class inherited; ShopField=1; Quantity=1; description ends in `CUT`).
4. **`ApplyRates` modifier neutralisation** — Under MCAA, `RollupMult = 1.0`, `MatlMult = 1.0`, `cutAdd = 0`, `bevAdd = 0` for all components, so `BudgetMHs = mhu × Quantity`. Audit columns honestly show 1.0/1.0/0/0.
**Why:** SPL is a Summit-specific concept (MCAA prices spool handling differently or not at all). MCAA does not apply Summit's rate modifiers because cut/bevel/rollup get priced as separate joint or prep-op rate rows under MCAA's pricing model. Standalone CUT rows let MCAA price cut labor row-by-row instead of as a connection-rate add-on. Audit columns kept under MCAA so reviewers verify at a glance that no multiplier was applied.
**Date:** May 5, 2026

#### MCAA Phase 1 Still Reads Summit's Embedded Ratesheet
**Rule:** Both Summit and MCAA modes call `RateSheetService.FindRate(...)` against the embedded `Resources/RateSheet.json` for the rate value. The `MCAARateSheetService` swap is the last code change planned before MCAA parity testing across 5 real projects.
**Why:** Phase 1 is structural-only: toggle, gating, audit columns, workbook-mode marker. Pulling in actual MCAA rate values requires the producer-side abbreviation review to complete and the xlsx → SQLite exporter to ship — both blocked on user-driven work. Keeping the rate source unchanged in Phase 1 ships the toggle apparatus tested against known-good rates, isolating variables. When `MCAARateSheetService` exists, the swap is a single conditional at the top of `ApplyRates()`.
**Date:** May 5, 2026

#### Active Rate Mode Is Recorded in the Summary Tab's First Data Row
**Rule:** The rate mode used to generate a takeoff workbook writes as the first data row of the Summary tab: `Rate Mode: Summit` or `Rate Mode: MCAA` (column 2 bolded). It is NOT written as an Excel file-level custom property.
**Why:** A Summary-tab cell is visible the moment the tab is opened — no drilling into File > Info > Properties. File-level custom properties are also fragile, lost when re-saved through tools that don't preserve them. The cell-based marker survives PDF export, screenshot, and print.
**Date:** May 5, 2026

### MCAA Ratesheet (Storage / Producer)

#### MCAA Ratesheet Ships as Local SQLite, Refreshed by the Auto-Updater
**Rule:** The MCAA WebLEM ratesheet ships as a local SQLite file in `%LocalAppData%\VANTAGE` alongside the user-data SQLite. The auto-updater refreshes it independently of full VANTAGE releases when WebLEM updates upstream. It is not embedded as a JSON resource and not hosted in Azure SQL.
**Why:** Three constraints. (1) Must work offline — a takeoff can't depend on Azure round-trips per line for hundreds of facet lookups per project. (2) Must support quarterly rate updates without releasing all of VANTAGE — WebLEM cadence and VANTAGE cadence are decoupled. (3) Size is significant — ~25 MB working xlsx for 174K rates. Embedded JSON fails all three (assembly bloat, locked release cadence, large embedded resource). Azure-hosted SQL fails (1) and adds latency on every facet lookup. Local SQLite hits all three.
**Date:** April 2026

#### `MCAARateSheetService` Runs Alongside `RateSheetService`, Not Refactored Into It
**Rule:** `Services/AI/MCAARateSheetService.cs` runs alongside the existing `Services/AI/RateSheetService.cs`. Each owns its own data source (RateSheet owns embedded `Resources/RateSheet.json`; MCAARateSheet owns local SQLite). Callers pick which one based on the takeoff's rate mode. There is no internal-dispatch refactor inside `RateSheetService`.
**Why:** Embedded `RateSheetService` is on a 6–12 month sunset path (deletion target after parity testing). Refactoring it now to dispatch between Summit-embedded and MCAA-SQLite is throwaway work that gets deleted with the deprecation. Two side-by-side services keeps each read-only and bounded; when MCAA wins parity, both `RateSheetService` and `Resources/RateSheet.json` delete cleanly.
**Date:** April 2026

#### CompRefTable Holds BOM Items Only; Actions/Connections Are MCAA-Only
**Rule:** The takeoff AI's vocabulary source is `CompRefTable.xlsx` — drawing-item → abbreviation. Items the AI emits (everything that appears on a piping BOM: pipe, fittings, valves, flanges, branch connections) live in CompRefTable. Actions and connections (welds, bolts, cuts, bevels, hydrotest, threading) do NOT live in CompRefTable — they live only in the MCAA ratesheet, derived at labor-creation time from BOM connection data. New abbreviations the MCAA ratesheet introduces that aren't in CompRefTable (Cross, Lateral, Tangential Nozzle, Weld Neck variants) are tracked in a separate "MCAA-only abbreviations" list — the AI doesn't auto-emit them.
**Why:** CompRefTable's job is to drive AI drawing-item recognition. Polluting it with action/connection codes (BW, SW, THRD, BEV, CUT, HYDRO) bloats the prompt and confuses the recognition task — those concepts don't appear as items on drawing BOMs. A separate post-processing step handles "BOM has SCRD on CS pipe → emit a THRD labor row" and queries the ratesheet directly using the action's abbreviation.
**Date:** April 2026

#### MCAA Schema Is Bounded by AI Takeoff's Input Scope
**Rule:** All MCAA schema and lookup design choices must fit what AI Takeoff actually produces. AI Takeoff extracts only two surfaces from each drawing PDF: the BOM table and the title block. There is no line-drawing scan and no line list anywhere in the pipeline. Material lives in the BOM description. Component code, body style, size, connection type, schedule, class — anything VANTAGE wants for rate lookup must be extractable from the BOM description plus the title block.
**Why:** Designing for an extraction surface that doesn't exist would invalidate every other MCAA decision. Recording this as a bound (not just a fact) so future MCAA work doesn't reason from a phantom line list. CompRefTable is also injected into every extraction prompt — token cost scales with its row count, which separately bounds how big CompRefTable can get and rules out one-key-per-rate-row schemes that would balloon it.
**Date:** April 2026

#### SR / LR Modifiers Fold Into the Component Code
**Rule:** Short Radius (`SR`) and Long Radius (`LR`) for elbows are folded into the component code itself: `EL90SR`, `EL90LR`. Any other modifier the abbreviation can absorb without exploding the keyspace is treated the same.
**Why:** One less property the AI extracts per item — simpler prompt, fewer independent extraction failures. SR/LR is bounded (two values), so absorbing them into the component code doesn't blow up CompRefTable. General rule: low-cardinality modifiers ride in the abbreviation; reserve `body_style` for ones it can't.
**Date:** April 2026

#### CONCENTRIC/ECCENTRIC Reducer Lookup Falls Back to CONCENTRIC
**Rule:** Body-style-agnostic reducer rates store `body_style = CONCENTRIC`. Lookup tries the AI-extracted body_style first; if no match, retries as `CONCENTRIC`. Body-style-specific rows store their actual body style as expected.
**Why:** The AI extracts CONC or ECC for reducers. Storing "either" rows as CONCENTRIC + fallback keeps the AI's body-style enum to two values and avoids row duplication or an EITHER sentinel + OR-clause SQL. Trade-off accepted: a missing ECCENTRIC-specific row falls through to CONCENTRIC silently — possible silent miscategorisation if SQLite is incomplete or wrong, vs. a returned-null miss that would surface as a "missed rate" warning. The PRD's pivot escape hatch covers this case if it bites in practice.
**Date:** April 2026

#### Property-Completeness Is Verified by Synthetic-Key Uniqueness
**Rule:** When the rate workbook is filled out, the producer concatenates `component` + all property columns into a synthetic key per row and checks for duplicates. Every row in the rate sheet represents a unique rated item or action; duplicate synthetic keys prove a property is missing — the colliding rows are the same rated thing as the schema currently sees it. Iterate (add the missing property column, refill it) until all synthetic keys are unique.
**Why:** Falsifiable test for property completeness. Without this check, "do we have all the properties we need?" is judgment-based and unreliable at 174K rows. The synthetic-key check turns it into a one-line SQL/pandas `groupby` query.
**Date:** April 2026

#### Lookup Key Uses Stored Column Order, Not a Sort
**Rule:** `lookup_key` concatenates its segments (component, reducing, material, `Merged_Props`, connection_qty, connection_type, pressure, class, schedule, weight_class, length, sizes) in fixed column order via a plain `TEXTJOIN`, blanks skipped. Sizes and connection tokens are emitted in **stored order** — the rate-sheet row's stored order is canonical, and the C# takeoff side must emit each item's (size, connection) pairs in that same order for a byte-identical match. `Merged_Props` is the only sorted segment (its properties sort A→Z). Reducing flanges store `BU` first because `BU` pairs with the larger size (`size_1`).
**Why:** An earlier contract sorted the (size, connection) pairs size-descending with a connection-A→Z tie-break. Sorting canonicalized away order that carries meaning (run vs. branch, large vs. small end), collapsing genuinely-different items into ~1,400 false-duplicate keys (a `2×1` reducer keyed identically to `1×2`). Not sorting preserves those distinctions, and both sides staying in stored order still yields identical keys.
**Date:** 2026-07-10

#### Flanges Carry Their Connection in the Key; Flange Manhours Are Handling-Only
**Rule:** Every flange row (`NewComp = FLG`, with the flange type carried as a property — `WN`, `SO`, `LJ`, `BLND`, …) carries `connection_qty`/`connection_type` in its key exactly like other fittings (WN → `BW,BU`, blind → `1 BU`, reducers keyed BU-first). The stored flange manhours are **handling-only**; at takeoff C# synthesizes a separate bolt-up row and a separate weld/joint row per flange type, each with its own key and rate.
**Why:** C# composes every takeoff key with one uniform rule ("include the connection"). Blank-connection flanges could never match a key built that way and would force an unmaintainable per-component exception list. Putting the connection in the flange key is for identity/matching; keeping the flange rate handling-only avoids double-counting the separately-synthesized bolt-up and weld.
**Date:** 2026-07-10

#### The Rate Sheet Is C#-Only; the AI Uses a Separate Component Reference Table
**Rule:** The FinalMerged rate sheet (→ SQLite) serves C# only: `lookup_key → manhours`. The AI never reads it. Component identification and connection synthesis are driven by a separate MCAA **Component Reference Table** keyed per component/subtype, carrying only connection quantity and connection types — no manhours, class, schedule, pressure, or properties.
**Why:** Everything except the component identity and its connection profile is a per-item value the AI extracts from the BOM, so it does not belong in a component-level reference. Splitting the two artifacts keeps the rate sheet a pure C# lookup and gives the AI a small, bounded catalog (which also bounds extraction-prompt token cost).
**Date:** 2026-07-10

#### SkySkraper Producer Is an External Synology Drive Folder
**Rule:** The Python pipeline that scrapes MCAA WebLEM ("SkySkraper") lives at `C:\Users\Steve.Amalfitano\source\repos\PrinceCorwin\SkySkraper\SynologyDrive` — outside the VANTAGE Git repository. Synced across machines via Synology Drive. Has its own `CLAUDE.md`, `AGENTS.md`, and Codex's working journal at `Plans/cdx_Project_Status.md`. The canonical PRD lives in **this** repo at `Plans/MCAA_Ratesheet_Plan.md`; status and completed work track in this repo's `Plans/Project_Status.md` and `Plans/Completed_Work.md`. Files prefixed `cdx_` belong to Codex; everything else is Claude's. Either side reads either's files; neither modifies the other's. VANTAGE's `CLAUDE.md` carries an explicit "never git-track or move SkySkraper" guard.
**Why:** The producer ships ~115 MB of cached HTML, 25 MB of working xlsx, two ~80 MB SQLite databases, and per-leaf XLSX/CSV outputs — none of which belongs in git history. Synology Drive provides multi-machine sync without polluting git. Quarterly cadence means long pauses between active development; the producer doesn't need VANTAGE's commit discipline. Codex's involvement adds another reason: Codex sessions don't have access to VANTAGE's git context.
**Date:** April 2026

#### SkySkraper Cache Layout: Per-Section Sibling Folders
**Rule:** SkySkraper's `raw_cache/` is organised one folder per WebLEM top-level section, sibling-style: `html_piping_systems/`, `html_instrumentation/`, `html_hvac_equipment/`, `html_plumbing_equipment/`, `html_plumbing_fixtures/`, `html_hangers_sleeves_inserts/`, `html_treatment_plant_equipment/`, `html_refrigeration_equipment/`, `html_miscellaneous_labor_operations/`, `html_excavation_backfill/`, plus `html_component/` (PIPING SYSTEMS Component-mode HTML, Codex-owned). Each folder has a sibling `index_<section>.json`. `discovery/leaves.json` remains PIPING SYSTEMS only; new sections get sibling `discovery/leaves_<section>.json` files filtered from `dom_leaves.json` by `breadcrumb[0]`. `dom_leaves.json` is unchanged and remains the authoritative DOM extract for all 1,926 leaves.
**Why:** Codex established the sibling pattern with `html_component/` + `index_component.json`. Nested `html/<section>/` would diverge from that and force every existing script that reads `html_component/` directly to learn a new layout. Sibling lets future sections plug in by pure additive change — add a `RAW_HTML_<SECTION>_DIR` constant, an `index_<section>.json` sidecar, a row in `fetch.py:SECTIONS`, and a leaves file. Backward compatibility preserved via `config.RAW_HTML_DIR` aliasing the new piping path.
**Date:** April 30, 2026

---

## Admin Tools

### Bulk Restore/Purge Uses Temp Table + SqlBulkCopy + INNER JOIN
**Rule:** `BtnRestore_Click` and `BtnPurge_Click` in `Views/DeletedRecordsView.xaml.cs` create a session-scoped `#RestoreBatch` / `#PurgeBatch` temp table, bulk-copy UniqueIDs in via `SqlBulkCopy`, and run a single `UPDATE ... INNER JOIN` (or `DELETE ... INNER JOIN`). Same pattern as `Utilities/SyncManager.cs`. No chunked loops; no transaction wrapping multiple statements.
**Why:** Earlier `WHERE UniqueID IN (@uid0..@uid999)` chunked design hit SQL Server's 2100-parameter ceiling, produced terrible query plans (large IN lists become OR-trees with unreliable plan reuse), and held write locks across all batches in one transaction blocking concurrent user sync. SqlBulkCopy + INNER JOIN is one round-trip with a clean index-seek plan. A 5,783-record restore that used to take minutes now runs in seconds.
**Date:** April 2026

### Deleted Records Export Fetches Complete Records on Click, Not at Refresh
**Rule:** The Deleted Records grid loads only 15 columns from `VMS_Activities` for fast scrolling/filtering. Clicking **EXPORT TO EXCEL** triggers a separate Azure fetch — temp-table + SqlBulkCopy + INNER JOIN against UniqueIDs of the rows visible after grid filters — to pull `SELECT a.* FROM VMS_Activities a INNER JOIN #ExportBatch s ON a.UniqueID = s.UniqueID WHERE a.IsDeleted = 1`. Results map to full `Activity` objects and export in NewVantage format.
**Why:** Exporting only the 15 grid-loaded columns left `Notes` and most other Activity fields blank in the workbook. Loading the full row at refresh time hung against a real deleted-records set (5,000+ rows × wide columns × Azure latency). Refresh stays at original speed; export does the heavy lift only for filtered rows. NewVantage matches what the rest of the app exports today (Progress's Export Activities, etc.); Legacy was a leftover default from when the dialog was first written.
**Date:** 2026-05-09

### Deleted-Records Grid Is Served by a Covering Filtered Index; Deleted Rows Stay in VMS_Activities as Tombstones
**Rule:** Soft-deleted activities remain in `VMS_Activities` with `IsDeleted = 1` — there is no separate deleted-records table. They double as sync tombstones: each client's delta pull (`SyncManager.PullRecordsAsync`, `WHERE SyncVersion > @v`) picks up the `IsDeleted = 1` row and removes it from the local SQLite cache. `DatabaseSetup.EnsureAzureIndexes` maintains `IX_VMS_Activities_Deleted_Grid` — a filtered (`WHERE IsDeleted = 1`) covering index keyed `(ProjectID, UpdatedUtcDate)` that INCLUDEs the 15 columns `DeletedRecordsView`'s grid loads — so both the DISTINCT-projects load and the per-project Refresh run as pure index seeks with no key lookups. It supersedes the older narrow `IX_VMS_Activities_Deleted_ProjectID` (ProjectID-only), which is dropped.
**Why:** Moving deleted rows to a separate table would break deletion propagation — clients learn of a deletion only by pulling the tombstone in their SyncVersion delta, so retiring it early would leave the row in their cache forever. The narrow index left the grid's 15-column Refresh doing a key lookup per deleted row (slow for projects with many deletions); the covering index drops a busy-project Refresh to ~3 logical reads. Bloat from accumulated tombstones is addressed by retention purge of old ones (the existing Purge button), not by relocating live tombstones.
**Date:** 2026-06-27

### Snapshot Modify Is Sync-Inert
**Rule:** `ModifySnapshotDialog` writes edits to Azure `VMS_ProgressSnapshots` (via `ScheduleRepository.UpdateSnapshotFullAsync`) and best-effort to the local 12-column `ProgressSnapshots` mirror. It explicitly excludes `SyncVersion`, `LocalDirty`, `AzureUploadUtcDate`, and `ActivityID` from the UPDATE, and never touches `Activities` / `VMS_Activities`. A subsequent sync push reports zero records for a Modify-only session.
**Why:** Revert-to-Snapshot already exists for "restore my world to that week's state" — it overwrites local Activities and sets `LocalDirty = 1`, pushing snapshot values to Azure on next sync and destroying current live work. Modify covers a different need: "I submitted 50% last week but should have said 60% — fix the snapshot without disturbing my current 75%." Conflating the flows would make Modify destructive by default; keeping them separate (same data-loading pattern, opposite write invariants) preserves Revert's semantics and gives users a non-destructive correction path. Bonus: invisible to sync, so multi-user concurrency is not a concern.
**Date:** 2026-04-24

### Modify-Save Detects Externally-Regenerated Snapshots via 0-Rows-Affected
**Rule:** `ScheduleRepository.UpdateSnapshotsBatchAsync` returns a `Dictionary<string, int>` keyed by `UniqueID`, populated from the `OUTPUT inserted.UniqueID` clause of the single `UPDATE … FROM #SnapBatch` statement. UniqueIDs absent from the dictionary were not in `VMS_ProgressSnapshots` for the target `WeekEndDate` at UPDATE time — `ModifySnapshotDialog.BtnSave_Click` collects them as `zeroAffected` and surfaces "Snapshot was regenerated externally (most likely by Submit Week)" when every dirty row is in that list, or a partial-success message listing the missing UniqueIDs when only some are. The legacy per-row `UpdateSnapshotFullAsync` remains in the repository for other callers but is no longer used by the modify-save flow.
**Why:** No row versioning on `VMS_ProgressSnapshots` today. Two concurrent flows (Modify open + Submit Week for the same week) can produce lost updates in Modify's favor on the old rows. Submit Week's path is `DELETE` scoped to `(AssignedTo, ProjectID, WeekEndDate)` followed by bulk `INSERT` from live Activities — UniqueIDs may match, but values shift. Affected-rows detection is the cheapest signal that the snapshot was regenerated under us. The batched UPDATE's `OUTPUT inserted.UniqueID` returns the same information as N per-row `ExecuteNonQuery` return codes did, but in one server round-trip. Adding a `SyncVersion`-style column is a schema change on a shared table with millions of rows; cross-dialog mutex via `LongRunningOps` would change `LongRunningOps` from app-close guard to inter-dialog mutex. The output-clause check is minimum-complexity defense.
**Date:** 2026-06-16

### Snapshot Editable-Columns Mirror Progress Editable Baseline + Dates
**Rule:** `Utilities/SnapshotEditableColumns.NonEditable` lists: `UniqueID`, `AzureUploadUtcDate`, `UpdatedBy`, `UpdatedUtcDate`, `CreatedBy`, `ProgDate`, `PrevEarnMHs`, `EarnedMHsRoc`, `PlanStart`, `PlanFin`. Every other `SnapshotData` property is editable — including the 10 required-metadata fields, which pass the non-empty check at Save. Mirrors `ImportTakeoffDialog.ExcludedColumns` (Progress-view editable baseline) with one intentional difference: `ActStart` and `ActFin` are editable here.
**Why:** "Everything editable in Progress view" was the spec. ActStart/ActFin are excluded from `ImportTakeoffDialog` because takeoff imports shouldn't set them, not because they're inherently read-only. The Schedule module's detail grid edits the same fields today. Modify-snapshot's correction use case often needs date corrections, so re-admitting them is right. PlanStart/PlanFin stay non-editable because they're P6-driven and there's no "correct the plan date in the snapshot" workflow.
**Date:** 2026-04-24

### Manage My Snapshots Allows Admins and Managers to Modify Any User's Snapshot
**Rule:** Admins and managers can open `ModifySnapshotDialog` on any user's snapshot from `ManageSnapshotsDialog`. Elevation is determined by `AzureDbManager.IsUserAdmin(currentUser) || AzureDbManager.IsUserManager(currentUser)` at the dialog's `Loaded` event. Delete and Revert from this dialog stay owner-only regardless of elevation. The `ModifySnapshotDialog` constructor separates two usernames: `_editorUsername` (whoever clicked Save → written to `UpdatedBy` on each modified row) and `_week.Username` (the snapshot owner → used as the `AssignedTo` filter when loading rows from `VMS_ProgressSnapshots`). The title bar prepends the owner's username when editing a foreign snapshot.
**Why:** Production users had snapshots that needed admin correction (typos, bad metadata, etc.) but the existing flow required handing the user a corrected SQL script. Modify writes are row-keyed on `(UniqueID, WeekEndDate)` with no `AssignedTo` filter in the UPDATE itself, so cross-user edits work correctly at the SQL level — gating was the only blocker. Delete from this dialog stays owner-only because the admin "Manage Snapshots" dialog already does cross-user delete with bulk-selection ergonomics. Revert stays owner-only because the local Activities table on the admin's machine doesn't contain other users' rows, so the round-trip can't complete; if cross-user Revert is ever needed, it'll have to write directly to Azure `VMS_Activities` and is a separate design.
**Date:** 2026-06-16

### Snapshot Save Validation Skips Fields the User Didn't Change
**Rule:** `ModifySnapshotDialog.BtnSave_Click` compares each dirty row against its `_originals` clone before running `ActivityValidator.Validate`. If `PercentEntry`, `ActStart`, and `ActFin` are all unchanged from load, the date/% validator is skipped for that row. Same gating for `ActivityRequiredMetadata.Fields` — a blank required field is only flagged when its current value differs from the original. Pre-existing bad data on unrelated columns therefore cannot block an unrelated edit (Find/Replace on UOM, Description edits, etc.). Originals are captured per row in `LoadSnapshotRowsAsync` via `CloneSnapshot`.
**Why:** The validator was originally written to gate net-new edits in ProgressView's cell-edit path; applying it to every dirty row at snapshot save time meant legacy snapshots with violations (rows that predated the validator, or came in via P6 / takeoff import which don't run the validator) refused to save UOM-only or Description-only changes. The user shouldn't be forced to fix dates they didn't touch in order to save a UOM correction. Skipping unchanged fields preserves the validator's intent (catch new violations) without punishing the user for old ones.
**Date:** 2026-06-16

### Snapshot Save Uses One Batched Azure UPDATE, Not One Per Row
**Rule:** `ScheduleRepository.UpdateSnapshotsBatchAsync` is the production path for `ModifySnapshotDialog.BtnSave_Click`. It opens one Azure SQL connection, creates a `#SnapBatch` temp table via `SELECT TOP 0 [columns] INTO #SnapBatch FROM VMS_ProgressSnapshots` (clones the schema), `SqlBulkCopy`s the dirty rows into it, then runs a single `UPDATE a SET … FROM VMS_ProgressSnapshots a INNER JOIN #SnapBatch b ON a.UniqueID = b.UniqueID WHERE a.WeekEndDate = @WeekEndDate` with an `OUTPUT inserted.UniqueID` clause for the externally-regenerated detection. Local SQLite mirror runs in one transaction with one prepared command. The legacy per-row `UpdateSnapshotFullAsync` stays in `ScheduleRepository` for any caller that needs it but is no longer used by the modify-save flow.
**Why:** Per-row save with its own connection + UPDATE made a 34K-row Find/Replace save take ~45 minutes — every row was a fresh connection handshake plus one Azure round-trip. Connection pooling didn't help meaningfully because each `using` block returned the connection to the pool and the next iteration grabbed a fresh one. Temp-table + bulk-copy + single UPDATE drops it to two Azure round-trips total (DDL + bulk + UPDATE). Same `(UniqueID, WeekEndDate)` row identity, same OUTPUT-based externally-regenerated detection, all-or-nothing semantics replace partial-save (which the user has no way to inspect or recover from anyway).
**Date:** 2026-06-16

### Tools → Validate My Records Auto-Marks Offenders Dirty; Admin → Audit All Records Is Read-Only
**Rule:** Two parallel record-quality tools:
1. **Tools → Validate My Records** scans the current user's `Activities` (`WHERE AssignedTo = @user`), runs `ActivityValidator.Validate` and `ActivityRequiredMetadata.Fields` per row, and **automatically marks every offending `UniqueID` `LocalDirty = 1`** in a batched `IN (...)` transaction. Audit fields (`UpdatedBy`, `UpdatedUtcDate`) are deliberately untouched. The offenders surface through ProgressView's existing dirty-row highlight and metadata-error badge.
2. **Admin → Audit All Records** is gated to `IsAdmin`, scans `VMS_Activities` (`WHERE IsDeleted = 0 WITH (NOLOCK)`) or local `Activities` per the admin's `AuditScope` choice, and is **read-only — no `LocalDirty` mutation, no Azure writes.** Both tools share the same `ValidationIssue` model and the same Excel export shape. Both have a "Show in ProgressView" button that switches to ProgressView and applies `FilterByValidationIssuesAsync(uniqueIds)` so the user sees only the offenders.

The audit tool requires a project selection in a pre-step dialog (`AuditProjectSelectionDialog`) with an Excel-style header tri-state checkbox and an Azure-vs-Local source radio. Local mode lists only projects that have at least one matching local `Activity` so empty selections aren't noise.
**Why:** The same validators were only running at cell-edit boundaries in ProgressView (and Modify Snapshot's edit path), so legacy data and anything that bypassed the validators (P6 import, takeoff import, sync pull, plugin writes, direct SQL) sat unflagged until it hit Submit Week's required-metadata gate. A standalone user-facing scan that auto-flips `LocalDirty` lets the user fix everything they own through the normal dirty-row workflow without us having to integrate the validators into every import path. The admin audit is read-only because (a) the admin's local DB may not contain other users' rows so a cross-user `LocalDirty` flip would be a no-op or worse, and (b) the goal of the admin tool is observability ("who has problems on which projects") rather than action — fix-by-user-action stays the user-tool path. Both tools share `ValidationIssue` so future rule additions land in one model.
**Date:** 2026-06-16

### `ColumnSizer="SizeToHeader"` on 77-Column Editable Snapshot Grid
**Rule:** `ModifySnapshotDialog`'s `SfDataGrid` uses `ColumnSizer="SizeToHeader"` rather than `ColumnSizer="Auto"` for its ~77 generated columns. Pattern applies to any future generated-column grid with a wide schema.
**Why:** `ColumnSizer="Auto"` measures every visible cell across every row for each column on first render — at 77 columns × hundreds of rows that's O(rows × columns) of UI-thread measurement work, took 30+ seconds and looked frozen. `SizeToHeader` sizes by header text only, near-instant. Columns are still user-resizable.
**Date:** 2026-04-24

### Snapshot Dialogs Are Modeless and Stay Open During Long Ops
**Rule:** Both `ManageSnapshotsDialog` (user) and `AdminSnapshotsDialog` use `Show()`, not `ShowDialog()`. They stay open with their own spinner during long deletes/uploads; the user can drag aside and keep working in MainWindow. `DialogResult = true/false` patterns are replaced with a public `NeedsRefresh` property the caller reads in the `Closed` event. Re-entrancy guards focus the existing instance instead of opening a second.
**Why:** Initial plan was to extract delete logic into services and show a non-modal status toast pinned to MainWindow. Simpler approach won: keep the dialog intact with its existing spinner, just remove modality. Four lines per dialog instead of a service refactor.
**Date:** April 2026

### Submit Week Snapshot Is Frozen at SELECT, Not at Click
**Rule:** When Submit Week runs, the Progress grid is locked (`sfActivities.IsEnabled = false`) the instant the busy dialog appears, then unlocked via `Dispatcher.InvokeAsync` the instant the local SELECT into the in-memory DataTable completes inside the background task. The SELECT also runs ahead of the Azure DELETE step so the grid re-enables faster on the overwrite path.
**Why:** Earlier versions kept the grid live throughout the async submit, so edits made after clicking Submit could or could not end up in the snapshot depending on micro-timing. Now the snapshot boundary is explicit: everything up to the SELECT is captured; everything after is the NEXT week's progress. Users stay productive during the slow Azure writes that happen after the snapshot is already frozen in memory.
**Date:** April 2026

### ProgressLog `UserID` Concatenates Uploader and AssignedTo
**Rule:** Admin upload to `VANTAGE_global_ProgressLog` writes `UserID = "uploader|assignedto"` (pipe-separated) — e.g. `"steve|Grant.Gilbert"`. Concatenation happens in the SQL expression `@userId + '|' + ISNULL([AssignedTo], '')` inside the `INSERT ... SELECT FROM VMS_ProgressSnapshots`. Wrapped in `LEFT(CAST(... AS NVARCHAR(MAX)), maxLen)` using the column's max length from `INFORMATION_SCHEMA`. Pipe is chosen because it cannot appear in a Windows username. Legacy rows keep their old `"steve"`-style UserID and are distinguishable from new rows by the absence of `|`.
**Why:** Before this change, two users' snapshots uploaded at the same Timestamp + ProjectID produced indistinguishable ProgressLog rows. `ALTER TABLE` to add a dedicated `AssignedTo` column was rejected for the moment because `ProgressLog` has 14.9M rows and schema changes need DBA coordination. Fixing `VMS_ColumnMappings` to route `AssignedTo` to its own column would have needed the ALTER anyway. The concat packs both pieces of identity into the existing column at zero I/O cost.
**Date:** April 2026

### Submit Week Dedup Check Runs After the DELETE
**Rule:** Submit Week order inside the background task: SELECT local → unlock grid → DELETE old Azure snapshots (if overwriting) → dedup check (existing UniqueIDs for the week) → bulk copy.
**Why:** Dedup needs to skip records already submitted by *other* users for the same week. Checking before the DELETE would match the user's own prior snapshots and incorrectly flag every row as duplicate. Running after the DELETE leaves only other-user entries — exactly the conflict set to report. DELETE scope is narrow (`AssignedTo + ProjectID + WeekEndDate`), so no risk of removing other users' data.
**Date:** April 2026

### ProgressLog Upload Uses Tiered Timeouts, Not Unlimited
**Rule:** `AdminSnapshotsDialog.xaml.cs` uses tiered `CommandTimeout` ceilings sized per operation: 3600s on large `INSERT`/`UPDATE`/`DELETE` paths (main `INSERT ... SELECT`, `UPDATE VMS_Activities` that fires `TR_VMS_Activities_SyncVersion`, per-group DELETE, full-table DELETE), 120s on the snapshot-groups aggregate query, 60s on the single-row tracking INSERT. This is the OPPOSITE choice from the Sync flow's unlimited timeouts.
**Why:** `CommandTimeout = 0` (infinite) wedged the UI forever when a TCP socket dropped mid-operation — the ADO.NET client has no way to know the response isn't coming. The timeout is a dead-socket guard so the UI recovers, not an abort of legitimate work. 3600s is well above the legitimate max (150K-row uploads routinely exceed 10 minutes). Compare with sync flow: sync is foreground and any partial-progress is recoverable on retry, and the user is staring at the dialog the whole time. ProgressLog upload runs behind `SfBusyIndicator` for a long time — a dead socket is less tolerable here because the user has no signal anything is wrong.
**Date:** April 2026

### ProgressLog Upload Writes One Row Per Batch, Not Per RespParty
**Rule:** Upload batches in `ProgressLog` are grouped by `(Username, ProjectID, WeekEndDate, UploadUtcDate)`, one row per batch. The `RespParty` column in `VMS_ProgressLogUploads` is retained in the schema but written as `""` on new rows; the reader (`ManageProgressLogDialog` grid, REFRESH path, DELETE path) ignores it.
**Why:** Snapshots are always created as a unit across all RespParty values. Per-RespParty fanning produced N rows per batch with no information gain — the reader didn't use the breakdown, and the extra `SELECT ... GROUP BY RespParty` round-trip plus N INSERTs cost time per upload. Column retention vs `DROP COLUMN`: legacy data lives in the column and dropping it requires DBA coordination on a 14.9M-row adjacent table; an empty-string column on new rows is harmless and keeps this a pure code change.
**Date:** April 2026

### VANTAGE_global_ProgressLog Is a Clustered, Compressed Table With an Archive Companion
**Rule:** `VANTAGE_global_ProgressLog` is stored as a `PAGE`-compressed clustered index `CIX_ProgressLog` keyed `(Tag_ProjectID, [Timestamp])` — not a heap. Retired/finished projects are archived out to a companion table `VANTAGE_global_ProgressLog_archive` (identical schema) via copy → verify-count → batched-delete, keeping the live table lean. Archiving is currently performed admin-side with backend SQL; the planned "Archive a Project" admin dialog will wrap the same move.
**Why:** As a heap the table grew to ~16 GB with fragmentation and no useful clustering, dragging reads and storage on a database shared with REQit and other apps. Clustering on `(Tag_ProjectID, [Timestamp])` matches how the delete/read queries filter; PAGE compression plus archiving retired projects cut the footprint from ~16 GB to ~4.7 GB. The HEAP→clustered rebuild needs ~2× the table size in scratch space, so it waited on the 2026-06-24 Azure storage-quota increase and ran online (`ONLINE = ON, MAXDOP = 1`) to keep app writes flowing; Azure's `LOG_RATE_GOVERNOR` (service-tier log-write cap) sets the rebuild's pace. Routine uploads re-fragment the clustered index over time, so periodic `ALTER INDEX ... REORGANIZE/REBUILD` is the maintenance lever, and temporarily scaling up the service tier during a heavy rebuild raises the log-rate ceiling.
**Date:** 2026-06-27

---

## Settings Menu

*(No Settings-menu-specific rules currently. Reset User Settings registry rules live in Foundation; Manage UDF Names lives in Progress Module. Add entries here when a Settings-menu-only rule emerges.)*

---

## Plugins

### Plugins Inject Tools-Menu Items via `IPluginHost`
**Rule:** Plugins implement `IVantagePlugin` and inject Tools-menu items via `host.AddToolsMenuItem()`. Each plugin has its own assembly loaded at startup. The auto-update service compares installed plugin versions against `plugins-index.json` on the feed and pulls updated builds — but it does NOT install plugins that aren't already on the local machine; first-install must come from the Plugin Manager UI.
**Why:** Plugins create their own UI dynamically without the main app needing to know about them. Auto-update covers the 90% case (newer version of a plugin a user already runs) without the safety risk of silently installing plugins users never asked for.
**Date:** March 2026

### PTP Plugin Matches on UDF2, Not Description
**Rule:** The PTP TFS MECH Updater matches existing Activities by `UDF2` (containing the CWP value), not by Description pattern.
**Why:** Users were editing the Description field after import, breaking update-vs-create detection and producing duplicate rows. UDF2 is a stable identifier not typically modified.
**Date:** March 2026

### CONST Plugin: Spools Missing From the Report Are Zeroed Out, Not Force-Completed
**Rule:** When a Piece Mark previously imported is no longer in a new weekly Constellation report, the CONST plugin sets `PercentEntry = 0`, clears `ActStart` and `ActFin`, and tags Notes with `"DELETED"`. The user is responsible for actually deleting the row via VANTAGE's normal delete flow. The import-summary popup lists up to 20 affected Piece Marks (full list goes to the log).
**Why:** Force-completing missing spools (`PercentEntry = 100`, synthesised `ActFin = today`) poisoned `SchedActNO` rollups: any SchedActNO containing a cancelled spool got falsely advanced ActStart and inflated earned-value contribution. The plugin doesn't have authority to delete rows on the user's behalf (ownership semantics, sync coordination, undo paths all live in the main app's delete flow), but it does have authority to make the row inert so it can't pollute schedule reporting.
**Date:** 2026-05-09

### CONST Plugin: New Records Insert With `PhaseCategory = "PIPF"`
**Rule:** New CONST records are inserted with `PhaseCategory = "PIPF"`. PhaseCategory is in the insert-only group (not in the UPDATE statement), so existing rows from prior imports keep their original value until manually corrected.
**Why:** `"PIP"` was incorrect for the project's phase-category scheme.
**Date:** 2026-05-09

### CONST Plugin: Module's Trailing "PP" Is Stripped Before Storage
**Rule:** `Module` values from the Constellation report ending in `"PP"` (case insensitive) have the suffix stripped before flowing into `UDF2` and `WorkPackage` (both columns receive the cleaned value). Insert-only field, so existing rows keep their `PP`-suffixed values until re-imported.
**Why:** Module names carry a vendor-internal `"PP"` tag (e.g. `TFS00D002YSPP`) that isn't meaningful in VANTAGE's grouping/filtering context.
**Date:** 2026-05-09

### CONST Plugin: Both Dates Are Rejected When RLS-to-Fab > Final Shipment
**Rule:** When both `RLS to Fab date` and `Final Shipment` are populated in the report and fab-start is strictly after ship date, the plugin writes empty strings for both `ActStart` and `ActFin`. The `+20` shipment-percent boost still triggers from `Final Shipment.HasValue` regardless — only the date writes are suppressed.
**Why:** The data is contradictory at the source (a spool can't start fabrication after it shipped). Letting the dates through would create a Finish-before-Start violation in VANTAGE with no clear pointer to *why*. Rejecting both surfaces the issue as a metadata error on the row, which is a clearly visible cue for the user to take the data quality issue back to Constellation. Falling back to `Final Shipment` for both dates would silently mask the underlying report-quality problem.
**Date:** 2026-05-09
