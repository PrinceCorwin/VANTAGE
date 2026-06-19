# VANTAGE: Milestone - Completed Work

This document tracks completed features and fixes. Items are moved here from Project_Status.md after user confirmation.

---

## Unreleased

_(none — most recent work is shipped)_

---

## v26.2.23 — Released 2026-06-19

### June 18, 2026 (AI Takeoff Lambdas — Flagged Tab Column-Key Consistency + Honest Direct-Invoke Zero-BOM Failures)

**`summit-takeoff-aggregate` (zip deploy).** `build_flagged_rows` now emits the parts-line under the key `raw_description` instead of `description`, and the Flagged tab's column header reads "Raw Description" matching the Material tab. Old SHA `TMDJU7Phs26W30w82NTeiGn19iBfw6Az7B+Ak+TSE6c=` → new SHA `QD2UNgGaqoPtvB96MET/lo08lWWwe5Bpx7iR/VpsB0g=`. Removes the silent data-contract trap where a future global-rename in either Lambda could have left half of the columns reading the wrong source key. Source reads (`item.get("description")` from the per-drawing extraction JSON) are untouched — extraction Lambda's output contract is unchanged. Verified C# Recalc (`Services/AI/TakeoffPostProcessor.cs`) reads only the Material tab, so old Excels remain fully Recalc-compatible — the Flagged tab is a human-review surface only.

**`summit-takeoff-poc` (container deploy).** The zero-BOM guard now fires regardless of whether `batch_id` is set, closing a silent-failure surface in non-batch (direct-invoke) mode. Old SHA `37cb86d67c2c87b6d0dbb087bfba3be08f67530ef0d25616b1463c6b66c49cf2` → new SHA `2a5e412516a89ce44eba3b6c5b319570886cbc0d75255cd73a00f6e5da26be17`. Batch behavior unchanged: marker still lands at `batches/<batch_id>/failures/<drawing>.json` and the response still returns `status: failed`. Non-batch behavior: marker now lands at `failures/<drawing>.json` (parallel to the success path's top-level `extractions/<drawing>.json`) and the response returns `status: failed` instead of a misleading success envelope around an empty `bom_items` array. Future debug invocations against the Lambda CLI can no longer be fooled by zero-item responses.

**Backlog hygiene.** Removed AI Takeoff Lambda issues #1 (column-key inconsistency) and #2 (batch-only zero-BOM guard) from `Plans/Project_Status.md`. Added a TODO under MCAA Ratesheet Integration for the AI Takeoff → MCAA Lambda routing work (deploy MCAA Lambdas, decide S3 bucket strategy, wire RateMode toggle, Step Functions orchestrator) and flagged that the MCAA Lambda source files in `mcaa-takeoff-poc/` and `mcaa-aggregate-deploy/` are scratch placeholders likely to be rewritten when the MCAA data aggregation finalizes — pre-rewrite edits to them are theater.

**MCAA Lambda copies.** Mirror edits applied to `mcaa-aggregate-deploy/lambda_function.py` so the source matches Summit. NOT deployed — no `mcaa-*` Lambda exists in AWS yet (verified against `aws lambda list-functions` in us-east-1).

**Project_Status.md cleanup.** Updated the Azure performance fix item with the Monday 2026-06-22 quota-increase request plan (director regained company Azure access). Added a new High Priority item for migrating the Email Service from Steve's personal Azure ACS resource to the company account once Monday's access is in.

**Key files (Summit AI Takeoff sources, deployed; live under `%USERPROFILE%\Documents\<prefix>\SynologyDrive\Conversion\`):** `aggregate-deploy/lambda_function.py`, `summit-takeoff-poc/lambda_function.py`. MCAA mirrors: `mcaa-aggregate-deploy/lambda_function.py` (deployed = no — no target).

---

### June 18, 2026 (Validation Coverage — Pre-Sync Partial Gate + Submit Week Combined Gate)

**`ActivityValidator.GetAllViolations(activity)` — canonical batch-validation primitive.** New method in `Utilities/ActivityValidator.cs`. Combines required-metadata (the 9 `ActivityRequiredMetadata.Fields`, via cached `PropertyInfo[]`), conditional date-required rules (ActStart needed when `% > 0`, ActFin needed when `% = 100`), and the existing `Validate` date/% rules (future dates, finish-before-start, inverse conditional violations) into one offender-list helper. Returns one string per violation; empty list = row is fully sync-valid. Project-exists is intentionally NOT included — it requires a `Projects` lookup and stays at call sites that own the valid-ProjectID cache. Shared by SyncManager's pre-sync gate and Submit Week's combined gate so both surfaces enforce identical rules.

**SyncManager pre-sync partial gate (Piece #3).** `Utilities/SyncManager.cs` → `PushRecordsAsync` now splits `dirtyRecords` into valid/invalid via `GetAllViolations` immediately after fetching dirty records. Valid rows continue through the existing push pipeline; invalid rows stay `LocalDirty = 1` locally (excluded from push, no DB mutation needed since they're already dirty). New `SyncResult` fields: `ValidationFailedRecords` (one human-readable `"UniqueID xyz: <violation>"` line per violation) and `ValidationFailedUniqueIds` (distinct UniqueID HashSet — use its `.Count` for the row tally so multiple violations on one row don't inflate the count). If every row fails validation, returns early with no Azure work attempted but `TotalRecordsToPush` and the failure lists populated. Partial-sync semantics are safe — push is row-keyed by `UniqueID`, so rows are independent.

**SyncDialog surfaces the validation failures distinctly.** `Dialogs/SyncDialog.xaml.cs` extends the result message with a dedicated block when `pushResult.ValidationFailedUniqueIds.Count > 0`: "Pushed X of N rows. Y row(s) have validation issues and remain marked as unsaved — fix and re-sync." plus a "Use Tools → Validate My Records to review all issues" hint and up to 5 example offender lines. Title flips to "Sync Incomplete" with the warning icon whenever the push had ANY validation failures, ownership conflicts, or an error message — previously only the error-message path triggered Sync Incomplete.

**Submit Week combined validation gate (Piece #4).** `Views/ProgressView.xaml.cs` → `BtnSubmit_Click` Step 3b replaced. The old SQL-only metadata count (`CountMetadataErrorsForProject`) was deleted; the new gate (1) single-shot SQL check that `selectedProject` exists in `Projects` (surfaces a clear "Invalid Project" dialog if not — avoids an offender list of identical rows), (2) iterates `_viewModel.Activities` filtered to `AssignedTo = currentUser` + `ProjectID = selectedProject`, calling `ActivityValidator.GetAllViolations` per row. Collects `(SchedActNO, UniqueID, Violation)` tuples. If any, blocks with a combined "Validation Errors" dialog: distinct-record count, total-violation count, up to 10 sample `"ActNo X: <message>"` lines, and a pointer to Tools → Validate My Records. All-or-nothing — snapshot is a point-in-time copy that must be internally consistent. Date checks run in C# (not SQL) because date columns are TEXT and SQLite's `date()` would let legacy non-standard date strings slip the filter.

**Behavior change worth flagging.** Submit Week now blocks on rules the old SQL gate didn't catch: future ActStart, future ActFin, ActFin earlier than ActStart, ActStart set when `% = 0`, ActFin set when `% < 100`. Users who haven't run Tools → Validate My Records yet may see new "Validation Errors" blocks on first Submit Week. The error dialog points them directly at Validate My Records for a bulk view of every offender.

**Key files:** `Utilities/ActivityValidator.cs` (new `GetAllViolations`, cached `PropertyInfo[]` for required-metadata fields), `Utilities/SyncManager.cs` (pre-sync gate + new `SyncResult.ValidationFailedRecords`/`ValidationFailedUniqueIds`), `Dialogs/SyncDialog.xaml.cs` (validation block in result message, broader "Sync Incomplete" trigger), `Views/ProgressView.xaml.cs` (combined gate, deleted `CountMetadataErrorsForProject`), `Help/manual.html` (partial-sync wording, broader "Submit Blocked by Validation Errors" callout), `Plans/Decisions.md` (updated Submit Week gating decision), `CLAUDE.md` (Edit Validation Rules section documents `GetAllViolations` as canonical primitive).

---

### June 16, 2026 (Snapshots — Modify Performance + Admin/Manager Elevation + Find/Replace; Validate My Records + Audit All Records Tools)

**Manage My Snapshots — Admin/manager elevation on Modify only.** Admins and managers (checked via `IsUserAdmin || IsUserManager` against `App.CurrentUser.Username` at `Loaded`) can open Modify on any user's snapshot. Delete and Revert stay owner-only — admins use the admin Snapshots dialog for cross-user delete, and reverting another user's snapshot from this dialog is intentionally unsupported (local Activities only contains their own rows, so the round-trip wouldn't work). The button row updates in `UpdateSelectionSummary` to enable Modify on a foreign-week selection when elevated; summary text now reads "Modify only — cannot delete/revert" for foreign rows. `ModifySnapshotDialog`'s constructor now separates `_editorUsername` (audit/`UpdatedBy`) from `_week.Username` (the snapshot-owner filter used at Azure load), and the title bar prepends the owner's name when editing a foreign snapshot so the user always knows whose data they're touching.

**Modify Snapshot — Clear Filters button.** Bottom-left button on `ModifySnapshotDialog` calls `sfSnapshot.ClearFilters()` (same pattern as ProgressView / ScheduleView). Saves a couple of clicks when a column filter is hiding rows the user needs to see.

**Modify Snapshot — Find & Replace on column headers.** Right-click any column header in the snapshot grid → "Find & Replace in this column..." Opens new `Dialogs/SnapshotFindReplaceDialog` modeled on the Progress `FindReplaceDialog` UX (Replace ALL cells / Find blanks / Match case / Whole cell / Count / Replace All / Close). Operates purely in memory on the loaded `SnapshotData` rows — mutates via reflection, returns `ChangedUniqueIds` to the caller, which adds them to `_dirtyUniqueIds` and enables Save. No DB writes, no ownership gating, no derived-field math (snapshots are frozen). Per-cell validation deferred to Save so a bulk UOM replace doesn't get blocked on every row by date-rule violations elsewhere on the row.

**Save validation only checks fields the user actually changed.** `BtnSave_Click` now compares each dirty row against its `_originals` clone before validating: if `PercentEntry`, `ActStart`, and `ActFin` are all unchanged from load, the date/% validator is skipped entirely. Same gating for required-metadata blanks — only flag when the field changed. Pre-existing bad data on unrelated columns (legacy snapshots with `ActFin < ActStart`, etc.) no longer blocks unrelated edits like a UOM Find/Replace. Originals are still consulted from the `_originals` dict populated during `LoadSnapshotRowsAsync`.

**Snapshot save: one batch, two Azure round-trips.** New `ScheduleRepository.UpdateSnapshotsBatchAsync(rows, weekEndDate, username, progress)`. Instead of one Azure connection + one UPDATE per dirty row (which made a 34K-row Find/Replace save take ~45 minutes), the batch method: (1) creates a `#SnapBatch` temp table via `SELECT TOP 0 ... INTO`, (2) `SqlBulkCopy`s the editable column values into it, (3) runs a single `UPDATE … FROM` with `OUTPUT inserted.UniqueID`, returning a `Dictionary<string, int>` of UniqueID → rows affected so the dialog still detects externally-regenerated rows. Local SQLite mirror updates in one transaction with one prepared command — sub-second on 34K rows. `BtnSave_Click` calls the batch method, marshals progress text into `txtBusyMessage` via `Dispatcher`, and treats save errors as all-or-nothing (the temp-table approach has transaction-equivalent semantics). The old `UpdateSnapshotFullAsync` per-row method stays in place but is now unused by the snapshot edit flow. Expected speedup is 10–50× for save sets in the hundreds; the 34K-row scenario drops from ~45 min to seconds.

**Tools → Validate My Records (own records).** New `Dialogs/ValidateMyRecordsDialog`. Scans the current user's local `Activities` (`WHERE AssignedTo = @user`), runs `ActivityRequiredMetadata.Fields` (9 required fields) + `ActivityValidator.Validate` (5 date/% rules) per row, and auto-marks every offending `UniqueID` `LocalDirty = 1` in a batched `IN (...)` transaction. Audit fields (`UpdatedBy`, `UpdatedUtcDate`) are deliberately untouched — we're surfacing legacy state, not claiming the user edited it. Results grid: `ProjectID, SchedActNO, Description, UniqueID, Violation` — column headers match Activity field names from elsewhere in the app. **Export Report** writes the full list to an `.xlsx` via ClosedXML (project standard). **Show in ProgressView** closes the dialog and signals MainWindow to switch to ProgressView and apply a `UniqueID IN (...)` filter so the user sees ONLY the offenders, not their other unrelated dirty edits.

**ProgressView — new validation-issues filter.** `Views/ProgressView.xaml.cs` gains a `_validationIssuesFilterActive` flag (alongside the existing `_metadataErrorsFilterActive` / `_scanResultsFilterActive`) and a public `FilterByValidationIssuesAsync(IList<string> uniqueIds)`. Clears existing filters via `ClearFiltersWithoutReload`, applies `_viewModel.ApplyFilter("ValidationIssues", "IN", "UniqueID IN ('a','b',...)")` with single-quote escaping, and explicitly refreshes all four toolbar elements per the Progress View Toolbar State Sync rules in CLAUDE.md: `UpdateRecordCount`, `DebouncedUpdateSummary`, `CalculateMetadataErrorCount`, `UpdateClearFiltersBorder`. `BtnClearFilters_Click` now resets the flag, so Clear Filters drops the validation-issues filter too; `UpdateClearFiltersBorder` checks the flag, so the green border lights up while it's active.

**Admin → Audit All Records (read-only).** New `Dialogs/AuditAllRecordsDialog` + new `Dialogs/AuditProjectSelectionDialog` pre-step. The pre-step lists projects with checkboxes (header tri-state `(Select All)` checkbox cycles like an Excel filter), all checked by default, plus a **Source: Azure (all projects) / Local (synced projects)** radio pair that decides both where the project list comes from AND where the audit scans. Local mode lists only projects that have at least one matching local `Activity` so a project with nothing synced isn't noise. The results dialog accepts `(IReadOnlyList<string> projectFilter, AuditScope scope)`, streams rows via `WITH (NOLOCK)` on Azure or a plain SQLite `SELECT` on Local, validates per row in C# (same `ActivityValidator` + `ActivityRequiredMetadata` rules), and keeps only offenders so memory is bounded by issue count, not table size. Read-only — no LocalDirty mutation, no Azure writes. Grid columns include `AssignedTo` (read-only column not in the user dialog) and the grid has `AllowGrouping` enabled so the admin can drag the AssignedTo header to the group drop area to see per-user issue counts. Window title reflects the scope (`Audit All Records (Admin) — Local` vs `Audit All Records (Admin) — Azure`). Export Report + Show in ProgressView buttons present (admin tip: use Local mode after a project sync so Show in ProgressView surfaces every offender).

**Validation rules covered (canonical inventory).** `ActivityValidator.Validate` (stops at first violation per row): future ActStart, future ActFin, `ActFin < ActStart`, `% = 0 AND ActStart set`, `% < 100 AND ActFin set`. `ActivityRequiredMetadata.Fields`: `ProjectID, WorkPackage, PhaseCode, CompType, PhaseCategory, SchedActNO, Description, ROCStep, RespParty`. Each blank required field becomes its own row in the results grid; the date rules contribute at most one row per record.

**ValidationIssue model.** New `public class ValidationIssue` defined in `ValidateMyRecordsDialog.xaml.cs` carries `AssignedTo, UniqueID, SchedActNO, ProjectID, Description, Violation`. Two constructors — the 5-arg overload (used by Validate My Records) leaves `AssignedTo` empty; the 6-arg overload (used by Audit All Records) populates it. Same type drives both dialogs and both Excel exports.

**Import Takeoff — Keep None handling option.** Adds a fourth radio button to the `ImportTakeoffDialog` Handling section: `Keep None` drops both PIPE and SPL rows from the source. Save/load through `ImportProfile.HandlingMode = "KeepNone"`. The filter logic at `ImportTakeoffDialog.xaml.cs:724-725` works without an explicit branch because `keepPipe` and `keepSpl` are both derived from the existing two radios — when only `rbKeepNone` is checked they both fall false and both row types get dropped naturally. Manual entry added.

**Key files (snapshot dialog work):** `Dialogs/ManageSnapshotsDialog.xaml.cs`, `Dialogs/ModifySnapshotDialog.xaml(.cs)`, `Dialogs/SnapshotFindReplaceDialog.xaml(.cs)` (new), `Data/ScheduleRepository.cs` (new `UpdateSnapshotsBatchAsync`).

**Key files (validation tools):** `Dialogs/ValidateMyRecordsDialog.xaml(.cs)` (new), `Dialogs/AuditAllRecordsDialog.xaml(.cs)` (new), `Dialogs/AuditProjectSelectionDialog.xaml(.cs)` (new — includes `AuditScope` enum and `ProjectChoice` model), `Views/ProgressView.xaml.cs` (`_validationIssuesFilterActive` + `FilterByValidationIssuesAsync` + Clear Filters / UpdateClearFiltersBorder wiring), `MainWindow.xaml`(.cs) (Tools and Admin menu items + click handlers).

**Key files (import takeoff):** `Dialogs/ImportTakeoffDialog.xaml(.cs)`, `Models/ImportProfile.cs`, `Help/manual.html`.

---

### June 5, 2026 (Progress Books — Column Catalog, Save Bug Fix, Spinner on Layout Switch)

**Spinner on layout switch.** Yesterday's commit only blocked the UI during the initial `Loaded` flow. Switching layouts via `cboSavedLayouts` still awaited DB queries with no overlay, so the same kind of mid-load race that wiped FilterField before was still theoretically possible. `CboSavedLayouts_SelectionChanged` now wraps the layout-load body in `leftPanelBusy.IsBusy = true / try / finally false` — the early-return and unsaved-changes dialog stay outside the wrap so the spinner only fires when an actual load is happening.

**Central column-name catalog (`Models/ProgressBook/ProgressBookColumnCatalog.cs`).** New static class — single source of truth for Progress Book column metadata. Maps Activity FieldName (or a synthetic key) to `(ColumnSourceKind, DisplayHeader)`. Used by the View (column list label, Add dropdown, layout migration, BuildCurrentConfiguration round-trip) AND by `ProgressBookPdfGenerator` (header text + value dispatch + alignment). Fields not catalogued fall back to `(Direct, FieldName-as-is)` — no more `.ToUpper()` ugliness like `BUDGETMHS` in PDF headers. Public surface: `Contains(fieldName)`, `GetSourceKind(fieldName)`, `GetDisplayHeader(fieldName)`, plus two const synthetic-key names (`RemainingMHsFieldName`, `EntryBoxFieldName`).

**Catalog entries (user-approved mapping):**

| FieldName | Display label |
|---|---|
| `ActivityID` | `Act ID` |
| `ROCStep` | `ROC` |
| `Description` | `DESC` |
| `PhaseCategory` | `PhaseCat` |
| `SecondDwgNO` | `2ndDwgNo` |
| `SecondActno` | `2ndActNo` |
| `SchedActNO` | `ActNo` |
| `WorkPackage` | `WP` |
| `RespParty` | `RP` |
| `BudgetMHs` | `MHs` |
| `Quantity` | `QTY` |
| `RemainingMHs` *(synthetic Computed)* | `REM MH` |
| `EarnMHsCalc` | `ERN MH` |
| `EarnedQtyCalc` | `ERN QTY` |
| `EarnQtyEntry` | `Qty Entry` |
| `PercentCompleteCalc` | `% Comp` |
| `PercentEntry` | `% Comp` |
| `% ENTRY` *(synthetic EntryBox)* | `% ENTRY` |

`PercentEntry` and `PercentCompleteCalc` intentionally share the `% Comp` label — they're the same value for Progress Book purposes, and showing the duplicate label nudges a user who adds both to delete one. `UniqueID` / `PhaseCode` / `CompType` / `ProjectID` and the rest of the Activity fields are NOT catalogued and render with their FieldName as-is in both UI and PDF (drops the older `UID` / `PHASE` / `COMP` / `PROJ` shortenings).

**`ProgressBookConfiguration.CreateDefault()` reads from the catalog.** The default column list is now a single `DefaultColumnFieldNames` string array; SourceKind and DisplayHeader come from the catalog at construction time. CreateDefault now also stamps `SchemaVersion = CurrentSchemaVersion` explicitly. Default labels are now `Act ID | ROC | DESC | MHs | QTY | REM MH | % Comp | % ENTRY`.

**`ProgressBooksView._columnMeta` removed.** All four call sites (`LoadLayoutConfigurationAsync`, `MigrateConfigurationIfNeeded` legacy-append loop, `MigrateConfigurationIfNeeded` defensive % ENTRY add, `BtnAddColumn_Click`) now consult the catalog directly. The two synthetic-key constants in the View are thin aliases over the catalog's constants for readability at call sites.

**Catalog wins on layout load.** `LoadLayoutConfigurationAsync` now overrides `col.SourceKind` and `col.DisplayHeader` from the catalog whenever `ProgressBookColumnCatalog.Contains(col.FieldName)` is true. Net effect: renaming any short label in the catalog propagates to every saved layout on its next open — no JSON migration needed. Uncatalogued FieldNames still respect whatever the saved JSON had.

**`ProgressBookPdfGenerator.GetColumnDisplayName` collapsed.** The function is now a one-line `return ProgressBookColumnCatalog.GetDisplayHeader(fieldName);`. The previous nine hardcoded mappings (`ROC` / `DESC` / `PHASE` / `CATG` / `COMP` / `WP` / `PROJ` / `UID` / `ID`) and the `.ToUpper()` fallback are gone.

**Save bug — deleted columns reappearing on next load.** `BuildCurrentConfiguration` was creating a fresh `ProgressBookConfiguration` without setting `SchemaVersion`. The property's default is `0` (no initializer, intentional so legacy JSON triggers migration). Result: every Save wrote `"schemaVersion": 0`, and every Load saw `SchemaVersion < 2`, ran the migrator, and re-appended any promoted column the user had intentionally deleted. Fixed by stamping `SchemaVersion = ProgressBookConfiguration.CurrentSchemaVersion` in `BuildCurrentConfiguration`. Going forward, user deletes stick.

**Migration heuristic improvement — auto-heal layouts already corrupted by the save bug.** `MigrateConfigurationIfNeeded` no longer treats `SchemaVersion < 2` as sufficient to declare a layout legacy. New gate: `SchemaVersion < 2` AND `% ENTRY` is absent from `config.Columns`. Since `% ENTRY` is un-removable in any post-refactor save, its presence in the JSON is the cleanest "this layout was already saved under the new schema" signal — even if SchemaVersion was lost. Layouts that had columns falsely re-added by the save bug now load with the user's deletes intact; the next Save bumps SchemaVersion to 2 cleanly.

**Manual.** `Help/manual.html` Columns section updated to reflect the new default labels (`Act ID`, `ROC`, `DESC`, `MHs`, `QTY`, `REM MH`, `% Comp`, `% ENTRY`).

**Key files:** `Models/ProgressBook/ProgressBookColumnCatalog.cs` (new), `Models/ProgressBook/ProgressBookConfiguration.cs` (CreateDefault reads catalog + stamps SchemaVersion), `Views/ProgressBooksView.xaml.cs` (catalog wiring + BuildCurrentConfiguration SchemaVersion stamp + MigrateConfigurationIfNeeded heuristic + spinner on layout switch), `Services/ProgressBook/ProgressBookPdfGenerator.cs` (GetColumnDisplayName collapsed to catalog), `Help/manual.html`.

---

### June 4, 2026 (Progress Books — Configurable Columns Refactor)

User-tested end-to-end on 2026-06-05 (see that day's entry for the catalog follow-on and the SchemaVersion save bug fix that surfaced during testing).

**Schema (Step 1).** `Models/ProgressBook/ProgressBookConfiguration.cs` bumped to `SchemaVersion = 2`. The `SchemaVersion` property has NO initializer on purpose — legacy JSON without the key deserializes to `0` so the migrator fires; `CreateDefault()` stamps `CurrentSchemaVersion` explicitly. `Models/ProgressBook/ColumnConfig.cs` gained `ColumnSourceKind` (`Direct` / `Computed` / `EntryBox`) and an optional `DisplayHeader` so the renderer can dispatch on source semantics and label columns with friendly text (`MHs`, `REM MH`, `CUR %`) without renaming Activity properties. `CreateDefault()` now seeds eight columns in legacy render order: `ActivityID, ROCStep, Description, BudgetMHs (MHs), Quantity (QTY), RemainingMHs (REM MH), PercentEntry (CUR %), % ENTRY`.

**View (Step 2).** `Views/ProgressBooksView.xaml.cs` exposes the new defaults and treats only `% ENTRY` as required. New `_columnMeta` dictionary maps the five promoted columns to their (SourceKind, DisplayHeader) pairs; `BtnAddColumn_Click`, `RefreshColumnsListBox`, `BuildCurrentConfiguration` and `LoadLayoutConfigurationAsync` all consult it so promoted columns round-trip the metadata correctly. `_allFields` extended with synthetic keys `RemainingMHs` and `% ENTRY`. The column-list label prefers `DisplayHeader` (the UI shows "MHs" not "BudgetMHs"). New `MigrateConfigurationIfNeeded` is the silent in-memory migrator: when `SchemaVersion < 2`, appends the five promoted columns to the end of `Columns` in legacy render order, stamps `CurrentSchemaVersion`, and persists on next Save. Also defensively re-adds `% ENTRY` if it ever goes missing.

**PDF renderer (Step 3).** `Services/ProgressBook/ProgressBookPdfGenerator.cs` 3-zone layout is gone. `Zone3DataColumns`, `Zone3EntryColumns`, `_zone1IdWidth`, `_zone2Width`, `_zone2ColumnWidths`, `_zone3ColumnWidths` removed; replaced by a single `_columnWidths : List<(ColumnConfig, float Width)>` populated by `CalculateAutoFitColumnWidths` from `_config.Columns`. New width semantics: every column auto-fits to `max(header, longest-data)`; `EntryBox` columns enforce `MinEntryBoxWidth = 50pt` so there's room to handwrite; if Description is present it absorbs the remainder; otherwise the remainder is proportionally distributed across all columns so the row still fills page edge-to-edge. `RenderColumnHeaders` and `RenderDataRow` now single-iterate the list. New helpers `GetColumnValueText` (dispatch on SourceKind) and `IsNumericColumn` (right-align numerics, left-align text and EntryBox). Headers prefer `ColumnConfig.DisplayHeader`; `GetColumnDisplayName` remains as the fallback for non-promoted columns. `DrawEntryBox` deleted entirely (sole caller was the Zone 3 entry box) — `% ENTRY` cells now render a single bold `%` glyph 3px from the left edge on every row, no white fill and no isComplete suppression. `CalculateRowHeight` reads Description width from the unified `_columnWidths`.

**PRD revision.** `Plans/PRD_ProgressBooks_ConfigurableColumns.md` revised 2026-06-04 to make `% ENTRY` the SOLE un-removable column (the original PRD had all eight removable). Added Description-absent proportional-fill semantics.

**Manual.** `Help/manual.html` Layout and Columns sections updated to describe last-layout auto-restore, the eight-column default, the % ENTRY-only required rule, and the Description-present-vs-absent width behavior.

**Open design question carried forward.** Centralized column-name catalog. Short labels are still in two places (`ProgressBooksView._columnMeta` + `ProgressBookPdfGenerator.GetColumnDisplayName`). User to provide the full FieldName → short-label mapping; we'll consolidate into `Models/ProgressBook/ProgressBookColumnCatalog.cs` that both consumers read, with FieldName-as-is fallback (no more `.ToUpper()`).

**Key files:** `Models/ProgressBook/ColumnConfig.cs`, `Models/ProgressBook/ProgressBookConfiguration.cs`, `Views/ProgressBooksView.xaml`, `Views/ProgressBooksView.xaml.cs`, `Services/ProgressBook/ProgressBookPdfGenerator.cs`, `Plans/PRD_ProgressBooks_ConfigurableColumns.md`, `Help/manual.html`.

---

### June 4, 2026 (Progress Books — Layout Load Race Fixed + Last-Selected Layout Persists)

**Problem 1 — race condition wiped FilterField on layout pick.** `ProgressBooksView_Loaded` ran `LoadSavedLayoutsAsync` (which toggled `_isLoading` to false at the end of its own scope) and then `LoadDefaultConfigurationAsync` (which awaits `LoadFilterValuesAsync` — a DB query that yields the UI thread). During that yield the saved-layouts dropdown was enabled, and a user click on a saved layout hit the `if (_isLoading) return;` short-circuit in `CboSavedLayouts_SelectionChanged`. The dropdown visually showed the user's layout but the layout body never loaded, so the FilterField stayed on the default WorkPackage. Repro: pick a saved layout, FilterField reverts to WorkPackage on every load.

**Problem 2 — no memory of which layout the user had open last session.** Every navigation back into Progress Books reset to "Default Layout".

**Fix 1 — UI block + single targeted load.** Wrapped the left configuration ScrollViewer in `syncfusion:SfBusyIndicator` (`x:Name="leftPanelBusy"`, DualRing animation, AccentColor foreground). `ProgressBooksView_Loaded` now sets `_isLoading = true` AND `leftPanelBusy.IsBusy = true` for the entire load; the saved-layouts dropdown can't be clicked until both clear. The handler also no longer does the racy "default-load then maybe-layout-load" pair — it resolves the persisted last-layout ID, picks the matching item, and dispatches to exactly one of `LoadDefaultConfigurationAsync` or `LoadLayoutConfigurationAsync`. `LoadDefaultConfigurationAsync` and `LoadLayoutConfigurationAsync` now save/restore `_isLoading` (`bool prevLoading = _isLoading; ... finally { _isLoading = prevLoading; }`) so nested calls under an outer guarded scope can't prematurely flip the flag. `LoadSavedLayoutsAsync` was refactored to only refresh the dropdown items — callers manage selection and `_isLoading` themselves. `SelectLayoutInDropdown` also uses save/restore. `BtnSaveLayout_Click` (all three save paths) and `BtnDeleteLayout_Click` wrap their `LoadSavedLayoutsAsync` + selection sequence in `_isLoading = true / try / finally _isLoading = false`.

**Fix 2 — persist last-selected layout to UserSettings.** New `ProgressBook.LastSelectedLayoutId` key. `CboSavedLayouts_SelectionChanged` writes the new ID after a successful load. Save handlers write it after the saved-layout becomes the active selection. `BtnDeleteLayout_Click` writes 0 (Default Layout) since the deleted ID is gone. On `Loaded`, `GetLastSelectedLayoutId` reads the key, resolves it to a dropdown item, and falls back to Default Layout when the ID is missing / unparseable / points at a deleted layout. `UserSettingsRegistry.cs` deny-list comment updated to note this key has its own UI (the layout dropdown) and doesn't belong in the Reset dialog.

**Key files:** `Views/ProgressBooksView.xaml` (Syncfusion namespace + busy indicator wrap), `Views/ProgressBooksView.xaml.cs` (constants, Loaded refactor, save/restore in LoadDefault/LoadLayout/SelectLayoutInDropdown, last-layout-id helpers, save/delete handler wraps), `Utilities/UserSettingsRegistry.cs` (deny-list comment).

---

### June 3, 2026 (AI Takeoff — REDT and REDC Generate the Correct Number of Weld Labor Rows)

**Problem.** `TakeoffPostProcessor.BuildConnectionPairs` had a dedicated dual-size branch for `TEE` that correctly emitted 3 connection pairs (2 run faces + 1 outlet) but routed every other dual-size component through a generic "1 larger + 1 smaller" branch. That undercounted REDT (a reducing tee — same topology as TEE, 3 welds) by one row and REDC (a reducing cross — 4 welds) by two rows. For example, REDT `1x0.5` with `Connection Qty=3, Connection Type=BW` was producing 1×BW@1 + 1×BW@0.5 (2 labor rows) instead of 2×BW@1 + 1×BW@0.5 (3 rows). `Connection Qty` from the BOM was being ignored by that branch entirely.

**Fix in `TakeoffPostProcessor.BuildConnectionPairs`.** Folded REDT into the existing TEE dual-size branch (`(component == "TEE" || component == "REDT") && isDualSize`) so both emit 3 pairs: `runType×largerSize`, `runType×largerSize`, `outletType×smallerSize`. Added a new REDC dual-size branch immediately below that emits 4 pairs: 2× run at larger + 2× outlet at smaller. RED / SWG / REDE are genuine 2-weld reducers, so they still flow through the generic dual-size branch unchanged.

**Fix in `FittingMakeupService.CalculateFittingMakeupForPipe`.** Added a `REDC` branch that mirrors the existing REDT pattern: parses the `4x2`-style size, routes same-size REDC to `CROSS` (4× run makeup) as a safety net, and for true reducing crosses contributes `RunIn * 2` when the calling pipe is the larger run size and `OutletIn * 2` when the calling pipe is the smaller branch size. Without this branch, REDC was hitting the catch-all "single weld" path and contributing only 1× makeup regardless of topology.

**Known data gap.** `Resources/FittingMakeup.json` currently has zero REDC entries (vs. 285 REDT entries). The labor-row count fix above works standalone, but REDC makeup-inches lookups will now land on the Missed Makeups tab with key `BW/REDC/{larger}x{smaller}/...` until rate-sheet entries are added. That's intentionally surfacing the gap rather than the prior behavior of silently treating REDC as a 1-weld fitting.

**Key files:** `Services/AI/TakeoffPostProcessor.cs` (BuildConnectionPairs — REDT/REDC branches), `Services/AI/FittingMakeupService.cs` (CalculateFittingMakeupForPipe — REDC handler).

---

### June 2, 2026 (Analysis Module — Local / Snapshot Source Toggle Backed by `SnapshotAnalysis` Cache)

**Problem.** The Analysis summary grid only aggregated from local `Activities` — the user's live working set. There was no way to run the same Group By / aggregation against a historical Submit Week snapshot, anyone else's snapshot, or multiple snapshots combined.

**Source toggle.** New radio pair in the Analysis toolbar: **Local** (default, current behavior) and **Snapshot**. Defaults to All Users + Local when no saved settings exist (changed from My Records default). Both selections persist immediately via UserSettings — `AnalysisSourceMode` and the existing `AnalysisCurrentUserOnly` — so they survive app restart. Each radio pair has an explicit `GroupName` (`AnalysisUserFilter`, `AnalysisSource`) so the four toolbar radios don't auto-group as one set (WPF default when they share a parent panel).

**Persistent local cache.** New `SnapshotAnalysis` table (schema v13 migration) mirrors the `Activities` schema and lives in the local SQLite. The Analysis grid's Snapshot mode queries this table — same SQL shape as Local mode, just a different `FROM` clause. Sub-second aggregation regardless of source, because both paths read from local SQLite. Aggregation now goes through a single `LoadSummaryFromLocalTable(groupField, sourceTable)` helper with an `Activities` / `SnapshotAnalysis` allowlist.

**Picker dialog (`Dialogs/SelectAnalysisSnapshotsDialog`).** Opens via the toolbar **Select** button (enabled only when Snapshot radio is checked). Uses the same fast `GROUP BY (AssignedTo, ProjectID, WeekEndDate)` query as `ManageSnapshotsDialog` — ProgDate intentionally not in the grouping (scanning ProgDate at production volume times out). Checkbox column + Select All / Select None / Apply / Cancel. Rows are pre-checked from whatever's currently in the local `SnapshotAnalysis` table — selection state lives in the table itself, not a separate persisted list, so it can never drift from the cache.

**Repository (`SnapshotAnalysisRepository.PopulateFromAzureAsync`).** On Apply, wipes the local table and bulk-inserts the rows the user picked from Azure `VMS_ProgressSnapshots`, in a single SQLite transaction with indexes dropped/recreated around the insert (same pattern `RefillLocalSnapshotsForWeekAsync` uses). One Azure round-trip per selection change — busy overlay shown on the summary grid during the pull. Cancelling the dialog does nothing; clicking Apply with no rows checked clears the cache.

**Selection ergonomics.** Clicking the Snapshot radio does NOT auto-open the picker — it just switches the source and re-aggregates from whatever's currently in the cache. Empty cache = empty grid. User clicks Select to populate. Status text reads `none` or `N selected` so the cache state is visible at a glance.

**SQL gotchas fixed along the way.** (a) `RowCount` is a T-SQL reserved word, must be bracketed as `[RowCount]` in the SELECT alias. (b) `ActivityID` doesn't exist on `VMS_ProgressSnapshots` (it's a `VMS_Activities` column only) — dropped from the column list. (c) The `INNER JOIN (VALUES …) snaps(projectId, weekEndDate, assignedTo)` introduces column names that collide with the snapshot columns; every snapshot column in the SELECT must be qualified with `s.` to disambiguate.

**Key files:** `Utilities/SchemaMigrator.cs` (v13 migration creates SnapshotAnalysis table + indexes), `Data/SnapshotAnalysisRepository.cs` (new — `GetCurrentSnapshotKeysAsync`, `PopulateFromAzureAsync`), `Dialogs/SelectAnalysisSnapshotsDialog.xaml`(.cs) (new), `Models/AnalysisSnapshotKey.cs` (new — INPC for the picker grid), `Views/AnalysisView.xaml(.cs)` (radio pair + Select button + busy overlay + source-aware `LoadSummaryData`), `Utilities/SettingsManager.cs` (`GetAnalysisSourceMode`/`SetAnalysisSourceMode`; user-only default flipped to false = All Users), `Utilities/UserSettingsRegistry.cs` (deny-list comment for the new key).

---

### June 1, 2026 (New ActNOs from P6 — Dialog and Stub-Record Creation)

**Problem.** A P6 import wipes and re-populates the local `Schedule` table from the P6 workbook, then refills the local `ProgressSnapshots` mirror from Azure for the imported week. Any `SchedActNO` that P6 references but that the user's snapshot doesn't have was silently invisible — there was no way to tell the field "you need to start tracking these activities" short of manually adding rows in the Progress grid.

**New dialog.** When `MainWindow.ImportP6File_Click` finishes the snapshot refill, it now runs `ScheduleRepository.GetMissingActNOsFromP6Async` against the local DB to find P6 SchedActNOs not present in the just-refilled local `ProgressSnapshots` for the selected ProjectIDs. If any are found, `Dialogs/NewActNOsDialog.xaml` opens with an editable SfDataGrid: checkbox column + SchedActNO (read-only) + Description / BudgetMHs / PercentEntry / ActStart / ActFin (all editable, pre-populated from P6's `task_code`, `task_name`, `target_work_qty`, `complete_pct`, `act_start_date`, `act_end_date`). User picks one ProjectID from a ComboBox (constrained to the ProjectIDs they selected in P6ImportDialog), Select All / Select None toolbar, "X of N selected" counter, busy overlay during the create.

**Stub creation (`ScheduleRepository.CreateStubActivitiesFromP6Async`).** For each selected row, in this order:
1. Insert into Azure `VMS_ProgressSnapshots` (full schema, transactional) so the rows survive the next P6 re-import (which wipes and re-pulls the local mirror from Azure).
2. Insert into local `Activities` (full schema, `LocalDirty = 1`) so the next sync push moves them to Azure `VMS_Activities`.
3. Insert into local `ProgressSnapshots` mirror (lean 12-column rollup) so the Schedule view sees the rows immediately.
4. Both local writes happen in a single SQLite transaction; either both land or neither.

**Required-metadata placeholders.** P6 supplies `SchedActNO` and `Description`. ProjectID comes from the dialog ComboBox. The other six required-metadata fields (`WorkPackage`, `PhaseCode`, `CompType`, `PhaseCategory`, `ROCStep`, `RespParty`) get the literal string `"X"` — non-empty so the sync gate passes, but conspicuous enough that the user knows these need real values before the record is meaningful. Quantity defaults to `0.001` to avoid divide-by-zero in `EarnedQtyCalc`.

**UniqueID formula matches the rest of the app.** Stubs use `$"i{yyMMddHHmmss}{sequence}{last 3 of username lowered}"` — same pattern as Duplicate Row, Add Blank Row, and Excel import. Single timestamp captured for the batch, sequence increments per row.

**ProgDate is `UtcNow`, not the existing submission's ProgDate.** Earlier iteration tried to look up the original Submit Week's ProgDate from Azure so stubs would join the existing submission group in `ManageSnapshotsDialog`. The Azure lookup timed out at 60+ seconds against the user's production-sized snapshot table despite the supporting covering index — query plan / sniffing issue, not worth chasing. New stubs always use `DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")` and appear as a sibling group in ManageSnapshots. Cosmetic-only divergence; data is intact.

**ActStart fallback.** Defensive: if P6 reports `PercentEntry > 0` but `act_start_date` is blank (shouldn't happen in practice), ActStart falls back to the imported WeekEndDate so the snapshot row isn't born violating `ActivityValidator`'s required-metadata rule.

**Progress view refresh.** Progress view is cached (`MainWindow._cachedProgressView`), so its in-memory `Activities` collection is stale until the view is rebuilt or `RefreshData` runs. After stubs are created, `ImportP6File_Click` now calls `NotifyActivitiesModifiedAsync()` — same callback ScheduleView uses on its own edits — which refreshes the cached Progress view in place. New rows show immediately whether the user is in Schedule, Progress, or any other view at import time. Schedule view isn't cached, so it auto-loads fresh on next navigation.

**Cancellation.** Cancelling the dialog without creating any rows is one-shot — the user must re-run P6 import to revisit. No "Create Missing Activities" button was added to Schedule view; deferred unless a need surfaces.

**Comparison target.** Detection compares P6 against the local `ProgressSnapshots` snapshot mirror, not against the local `Activities` table. The Schedule module's premise is "P6 vs. snapshot"; that's the comparison the dialog reflects.

**Key files:** `Dialogs/NewActNOsDialog.xaml`(.cs) (new), `Models/MissingActNOCandidate.cs` (new), `Data/ScheduleRepository.cs` (new `GetMissingActNOsFromP6Async` and `CreateStubActivitiesFromP6Async`), `MainWindow.xaml.cs` (`ImportP6File_Click` wires the detection + dialog + cached Progress view refresh between the snapshot refill and the "Import Complete" message), `Plans/PRD_NewActNOsFromP6.md` (planning doc, retained for reference).

---

### June 1, 2026 (P6 Export — complete_pct Now Two Decimals)

**Schedule → Export To P6 File no longer rounds `complete_pct` to a whole number.** Users reported activities at 99.5–99.9% landing in the exported P6 workbook as `100` while the same row's `act_end_date` was blank — an inconsistency P6 flags because the exporter only writes `act_end_date` when `MS_PercentComplete >= 100`. Root cause: `Utilities/ScheduleExcelExporter.cs:111` called `Math.Round(row.MS_PercentComplete, 0)`, which rounded `99.7` to `100`. Fix: round to 2 decimals and apply `NumberFormat = "0.00"` so Excel/P6 display both decimals even when the trailing one is zero (`25.50`, not `25.5`). Two-line edit in `WriteTaskSheet`.

**3WLA companion file already correct.** Verified `Utilities/ScheduleReportExporter.cs` writes `MS_PercentComplete` and `P6_PercentComplete` (columns 5 and 6) as raw doubles with no rounding, which matches the user feedback that the Schedule Reports file already shows the right decimal precision.

**Key files:** `Utilities/ScheduleExcelExporter.cs`.

---

**Archives:** See Plans/Archives/ for previous months.
