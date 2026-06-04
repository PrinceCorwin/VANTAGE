# VANTAGE: Milestone - Completed Work

This document tracks completed features and fixes. Items are moved here from Project_Status.md after user confirmation.

---

## Unreleased

### June 4, 2026 (Progress Books — Configurable Columns Refactor — IMPLEMENTATION COMPLETE, PENDING USER TESTING)

**Status flag.** Code is committed and builds clean, but the user had to leave before verifying the end-to-end PDF render. The view-side changes were partially tested mid-session and a duplicate-render bug was discovered; that bug is the symptom that drove finishing the PDF generator refactor (Step 3) in this commit. Re-test items live in `Plans/Project_Status.md` under "Progress Books — fully configurable columns". Do not consider this entry final until the test items pass.

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
