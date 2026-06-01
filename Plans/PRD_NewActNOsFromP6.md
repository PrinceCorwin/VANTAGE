# PRD: New ActNOs from P6 Import

**Status:** Planning / discussion in progress
**Owner:** Steve
**Started:** 2026-06-01

## Goal

When a user imports a P6 schedule file, detect any SchedActNOs in the P6 file that don't already exist as records on the user's side. Show a dialog listing those missing ActNOs and let the user pick which ones to create. For each selection, create a record in the local Activities table (marked `LocalDirty = 1` for sync) AND in the snapshot for the week being imported.

Stub records get "X" placeholders for the required-metadata text fields the user must fill in later. Some fields come straight from P6:

| Field | P6 source |
|---|---|
| SchedActNO | `task_code` |
| Description | `task_name` |
| BudgetMHs | `target_work_qty` |
| PercentEntry | `complete_pct` |
| ActStart | `act_start_date` (if %>0) |
| ActFin | `act_end_date` (if %=100) |

## Where this hooks into the existing flow

`MainWindow.ImportP6File_Click` (around line 695):

1. File picker -> P6ImportDialog (user picks WeekEndDate + ProjectIDs)
2. `ScheduleExcelImporter.ImportFromP6Async` wipes Schedule table and re-inserts P6 rows
3. `ScheduleRepository.RefillLocalSnapshotsForWeekAsync` pulls Azure snapshots for the week into local `ProgressSnapshots` mirror
4. **[NEW]** Detect P6 SchedActNOs not in Activities, show dialog, let user create stubs
5. "Import Complete" message
6. Schedule view refresh

The new step has to happen BEFORE the Schedule view refresh so the new rows show up immediately.

## Decisions (settled)

- **Project assignment:** one ProjectID per batch. Dialog has a single project picker (from the user's `SelectedProjectIDs` chosen in P6ImportDialog). All new records get that ProjectID.
- **AssignedTo:** the user creating the records (creator owns them; can be reassigned later).
- **Snapshot ProgDate:** use the existing ProgDate for that week+project's snapshot group, so new rows join the existing submission rather than creating a sibling.
- **ActStart fallback:** if P6 reports PercentEntry > 0 but has no `act_start_date`, set ActStart = WeekEndDate. Shouldn't fire in practice (P6 doesn't normally produce that combination) but covers the edge case defensively. Snapshots can't carry validation-invalid rows.
- **Required metadata placeholders:** "X" for WorkPackage, PhaseCode, CompType, PhaseCategory, ROCStep, RespParty. ProjectID comes from the picker. SchedActNO and Description come from P6. (Nine required fields total per `ActivityRequiredMetadata.Fields`.)
- **Duplicate-import behavior:** non-issue. Once stubs exist, the next P6 import's detection query won't flag the same SchedActNOs because they'll be in the comparison set.
- **EarnMHs concern:** withdrawn. EarnMHs is calculated as PercentEntry / 100 × BudgetMHs — works fine regardless of ROCStep value. ROC is a separate optional weighted-progress feature; "X" in ROCStep just means the optional ROC lookup won't match anything, which is harmless.
- **Snapshot modification concern:** withdrawn. The Schedule module's detail table already edits snapshots routinely, so adding new rows to a snapshot fits the existing workflow.
- **MaxLength on Description:** truncate `task_name` to the column width when inserting.

## Open questions (need answers next session)

### Q1 — Cancellation semantics
If the user cancels the New-ActNOs dialog without creating any, the P6 import is already done. Two options:

- **(a) One-shot:** cancel means "ignore for this import." User must re-run P6 import to revisit.
- **(b) Reusable:** add a button to the Schedule view ("Create Missing Activities") that re-runs the detection anytime, so the user can come back to it without re-importing.

(a) is simpler; (b) is more forgiving.

### Q2 — Comparison target: snapshot vs. Activities
Steve originally said "not already in the snapshot being compared." But comparing against the snapshot can produce false positives: if an Activity already exists in the local Activities table but hasn't been submitted to any snapshot yet, P6 references it, and a snapshot-only check would flag it as "missing" — leading to a duplicate Activity being created.

The safer comparison is **against the Activities table** (filtered to the selected ProjectIDs), since Activities is the source of truth for what records exist. Snapshots are just weekly captures.

**Need confirmation:** change comparison target from snapshot to Activities?

## Implementation outline (once Q1 + Q2 are answered)

### Detection query
After `RefillLocalSnapshotsForWeekAsync`, query the local DB for Schedule rows whose SchedActNO doesn't appear in Activities (or snapshot, pending Q2) for the selected ProjectIDs. Return SchedActNO, Description, BudgetMHs, PercentEntry, ActStart, ActFin for each missing row.

If the result is empty, skip the dialog entirely.

### New dialog: `NewActNOsDialog`
- Header: intro text + count
- Project picker (required, limited to the user's P6-import-selected projects)
- `SfDataGrid` with checkbox column + editable cells for SchedActNO, Description, BudgetMHs, PercentEntry, ActStart, ActFin
- Buttons: Select All / Select None / Create N Records / Cancel
- User can edit any cell before creating (fix typos, adjust budgets)
- Use `AppMessageBox` for any user-facing dialogs
- Apply SfSkinManager so theme propagates
- Use SfBusyIndicator (DualRing) for the create operation

### Field population per record

Required-metadata fields:
| Field | Value |
|---|---|
| ProjectID | picker value |
| WorkPackage | "X" |
| PhaseCode | "X" |
| CompType | "X" |
| PhaseCategory | "X" |
| SchedActNO | from P6 task_code |
| Description | from P6 task_name (truncated to column width) |
| ROCStep | "X" |
| RespParty | "X" |

Other fields needed for a valid Activity / snapshot row:
- `UniqueID` = `Guid.NewGuid().ToString()`
- `HexNO` = 0
- `AssignedTo` = current user
- `BudgetMHs` = `target_work_qty`
- `Quantity` = 0.001 (model default, avoids divide-by-zero in EarnedQtyCalc)
- `PercentEntry` = `complete_pct` (already converted to 0-100 in the importer)
- `ActStart` = `act_start_date` if PercentEntry > 0, fallback to WeekEndDate if P6 didn't provide it; null otherwise
- `ActFin` = `act_end_date` if PercentEntry = 100; null otherwise
- `WeekEndDate` = the import's WeekEndDate
- `LocalDirty` = 1
- `SyncVersion` = 0
- `CreatedBy` = current user
- `UpdatedBy` = current user
- `UpdatedUtcDate` = `DateTime.UtcNow`

### Writes (transactional)
1. Insert into local `Activities` (LocalDirty=1) — sync push will pick them up
2. Insert into local `ProgressSnapshots` mirror so Schedule view shows them immediately
3. Insert into Azure `VMS_ProgressSnapshots` directly with the existing ProgDate for the week+project
4. Re-run the `ThreeWeekStart/Finish` update on the Schedule table (the same one `ScheduleExcelImporter.ImportToDatabase` does at the tail)
5. Log via `AppLogger.Info` with username

Wrap in a single Azure transaction + local transaction. Rollback both on any failure.

### Post-create
- Schedule view refresh (already happens after the import message — just make sure the new step runs before it)
- "Import Complete" message updated to mention the N stubs created if applicable

## Files that will be touched

- `Utilities/ScheduleExcelImporter.cs` — probably exposes a helper that returns the parsed P6 rows so the dialog has the source data without re-reading the file
- `Dialogs/NewActNOsDialog.xaml` + `.xaml.cs` — new dialog
- `MainWindow.xaml.cs` — wire detection + dialog into `ImportP6File_Click`
- `Repositories/ActivityRepository.cs` — insert helper (probably exists; verify)
- `Repositories/ScheduleRepository.cs` — snapshot insert helper (verify exists)
- Possibly `Views/ScheduleView.xaml(.cs)` — if Q1 = (b), add the "Create Missing Activities" button

## Docs to update at finish

- `Help/manual.html` — new workflow section
- `Plans/Decisions.md` — entry for the placeholder-"X" pattern and the rationale for inserting new rows into existing snapshots
- `Plans/Project_Status.md` / `Plans/Completed_Work.md` via `/finisher`
