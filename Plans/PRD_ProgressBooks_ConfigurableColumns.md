# PRD: Progress Books — Fully Configurable Columns

**Status:** SHIPPED 2026-06-05 (column refactor + central `ProgressBookColumnCatalog`).
**Owner:** Steve
**Created:** 2026-06-03
**Revised:** 2026-06-04 — `% ENTRY` retained as the sole un-removable column; columns proportionally widen to fill row when `Description` is absent.
**Shipped:** 2026-06-05 — central column catalog, save-bug fix, spinner on layout switch. See `Plans/Completed_Work.md` entries for 2026-06-04 and 2026-06-05.

## Goal

Shrink the "required column" concept from four columns (ActivityID / UniqueID / ROCStep / Description) to one (`% ENTRY`). Every other column — ActivityID, ROCStep, Description, MHs, QTY, REM MH, CUR % — becomes default-included but user-removable. `% ENTRY` stays locked because the handwritten progress entry IS the point of the book — a Progress Book without an entry column isn't a Progress Book. Field crews can shape the rest however they want on paper. The Cover Sheet (Page 1) totals must remain correct regardless of which body columns are present.

## Current state (what blocks this)

- `Views/ProgressBooksView.xaml.cs:310` hard-codes `isRequired` for ActivityID / UniqueID / ROCStep / Description — drives the `*` suffix and prevents removal.
- `Views/ProgressBooksView.xaml.cs:632` renders `{FieldName} *` for required columns in the column list UI.
- `Views/ProgressBooksView.xaml.cs:191-192` filters required fields out of the Add dropdown so the user can't re-add them.
- `Models/ProgressBook/ProgressBookConfiguration.cs:56-73` seeds the default `Columns` list with only ActivityID, ROCStep, Description. MHs / QTY / REM MH / CUR % / % ENTRY are not in the column list today — they're injected by the PDF generator at render time.
- `Services/ProgressBook/ProgressBookPdfGenerator.cs:32-36` defines `Zone3DataColumns = { "MHs", "QTY", "REM MH", "CUR %" }` and `Zone3EntryColumns = { ("% ENTRY", 50f) }`, rendered unconditionally.
- PDF body uses a fixed 3-zone layout in `RenderDataRow` (lines 937-1008): Zone 1 = ActivityID, Zone 2 = configured user columns, Zone 3 = the four data columns + the entry box.

## Settled decisions

1. **All 8 columns become first-class `ColumnConfig` entries** with a `SourceKind` (`Direct` / `Computed` / `EntryBox`). The default-included set is `ActivityID`, `ROCStep`, `Description`, `MHs`, `QTY`, `REM MH`, `CUR %`, `% ENTRY`, in that order. All removable, all reorderable.

   | Column | SourceKind | Source |
   |---|---|---|
   | ActivityID | Direct | `activity.ActivityID` |
   | ROCStep | Direct | `activity.ROCStep` |
   | Description | Direct | `activity.Description` (stretch-fill semantics retained when present) |
   | MHs | Direct | `activity.BudgetMHs` |
   | QTY | Direct | `activity.Quantity` |
   | REM MH | Computed | `activity.BudgetMHs - activity.EarnMHsCalc` |
   | CUR % | Direct | `activity.PercentEntry` |
   | % ENTRY | EntryBox | (writable field — always renders just `%`) |

2. **Shrink `isRequired` to just `% ENTRY`.** The flag and the `*` suffix render stay, but the only column carrying them is `% ENTRY`. ActivityID / UniqueID / ROCStep / Description lose their required status entirely. The Add-dropdown filter continues to hide `% ENTRY` only (since it's already always present). Any other column is removable and re-addable from the Add dropdown.

3. **Collapse the 3-zone PDF layout into one ordered list** driven by `_config.Columns`. Width semantics:
   - When `Description` is present in the list (the expected case — field crews need it to know what's being progressed), every other column auto-fits its content and `Description` absorbs the remaining row width.
   - When `Description` is absent, every column auto-fits its content first, then the remaining width is proportionally distributed across all columns so the row still fills the page edge-to-edge (no dead space on the right).

   `CalculateAutoFitColumnWidths()` and `RenderDataRow()` iterate the single ordered list instead of zones.

4. **`% ENTRY` simplification.** Drop the `WhiteBrush` fill rectangle and the `isComplete` branch entirely. `% ENTRY` is always present (un-removable), and every row renders just the `%` character — no fill, no separate box, no completion check. The pre-existing thin cell border from the row-grid framework (the standard column separator and row outline) remains intact — that's not part of the entry-box code path.

   Rationale: the current white-fill is half-dead. On white-background rows it's white-on-white and visually invisible; on the alternating light-gray rows it's a faint cue at best. The `%` character is the only consistently visible signal across all rows today. The `CUR %` column already conveys completion status; field crews can figure out where to write without the suppressed-on-complete-row cue.

5. **Cover Sheet stays independent — verify, don''t break.** `Dialogs/GenerateProgressBookDialog.xaml.cs:108-114` already computes `TotalBudgetMHs` and `TotalEarnedMHs` directly from `allActivities`, decoupled from `_config.Columns`. That path stays untouched. Add an inline comment near that block marking the cover-sheet independence contract, and validate with a one-shot test: remove MHs from the body columns, generate the book, confirm cover-page totals still render correctly.

6. **Backward-compat: auto-migrate older saved layouts silently.** Older `ProgressBookLayouts.ConfigurationJson` rows have `Columns` = [ActivityID, ROCStep, Description] only. On layout load (`ProgressBooksView.LoadLayoutConfigurationAsync`): if `Columns` is missing any of the 5 newly-explicit columns (MHs / QTY / REM MH / CUR % / % ENTRY) AND a new `SchemaVersion` field is absent or < 2, append the missing columns at the end in the legacy order (MHs, QTY, REM MH, CUR %, % ENTRY) so the rendered book is visually identical to what it was before. Stamp `SchemaVersion = 2` on the next save. No user prompt; the migration is silent. Note: this also guarantees `% ENTRY` lands in every migrated layout, satisfying the new "always present" contract without a separate check.

7. **No reset button.** The "Default Layout" entry in the layouts dropdown already serves as the reset path. No new UI affordance.

## Files to touch

| File | Change |
|---|---|
| `Models/ProgressBook/ColumnConfig.cs` | Add `SourceKind` enum + property; optional `DisplayHeader` for columns whose header differs from underlying field name (MHs, REM MH, CUR %). |
| `Models/ProgressBook/ProgressBookConfiguration.cs` | Add `SchemaVersion` (int, defaults to 2 for new configs). Update default `Columns` to the 8-entry list. |
| `Views/ProgressBooksView.xaml.cs` | Narrow the `isRequired` check at `LoadLayoutConfigurationAsync` (~line 310 before the recent Loaded refactor) and at the default-columns block in `LoadDefaultConfigurationAsync` (~line 245) so only `% ENTRY` qualifies. Narrow the Add-dropdown exclusion in `RefreshAddColumnDropdown` (~line 191) to hide `% ENTRY` only. Extend `_allFields` to include the 5 promoted columns. Add migration in `LoadLayoutConfigurationAsync` so older saved layouts that omit `% ENTRY` get it appended on load (treat missing `% ENTRY` as a schema-v1 layout). |
| `Services/ProgressBook/ProgressBookPdfGenerator.cs` | Remove `Zone3DataColumns` / `Zone3EntryColumns` constants. Replace 3-zone rendering in `CalculateAutoFitColumnWidths` and `RenderDataRow` with a single ordered iteration over `_config.Columns`. Add the Description-absent proportional-fill branch in `CalculateAutoFitColumnWidths`. Delete `DrawEntryBox` entirely (its only caller goes away — the `% ENTRY` cell just renders a bold `%` glyph via `DrawLeftText` with a bolder font). |
| `Dialogs/GenerateProgressBookDialog.xaml.cs` | No code change — add inline comment near lines 108-114 marking the cover-sheet independence contract. |

## Out of scope

- Changing the Cover Sheet layout itself.
- Adding new fields to the available-fields registry beyond the 5 being promoted (the 140+ Activity fields already available are unchanged).
- AI Progress Scan changes.

## Open / deferred

- None. All decisions settled in the 2026-06-03 design conversation.

## Notes from the design conversation

- The existing `DrawEntryBox` white-fill is effectively half-dead code (white-on-white on the alternating-row white background). Removing it is a code-quality win alongside the behavior simplification.
- Field crew already orients to `CUR %` for completion status, so the per-row "skip the box on 100% rows" cue isn''t load-bearing.
- The cover sheet was discovered during exploration to be already decoupled from body columns — the decoupling work is documentation + a sanity test, not code refactoring.