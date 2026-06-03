# PRD: Progress Books — Fully Configurable Columns

**Status:** PLANNED (not started)
**Owner:** Steve
**Created:** 2026-06-03

## Goal

Remove the "required column" concept from the Progress Books layout builder. Every column — including ActivityID, ROCStep, Description, MHs, QTY, REM MH, CUR %, and % ENTRY — becomes a default-included, user-removable column. Field crews can shape the book to whatever they actually want on paper. The Cover Sheet (Page 1) totals must remain correct regardless of which body columns are present.

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

2. **Kill the `isRequired` / asterisk concept.** Remove the flag, the `*` suffix render, and the Add-dropdown filter. Any column is removable; the same column can be added back from the Add dropdown if the user later changes their mind.

3. **Collapse the 3-zone PDF layout into one ordered list** driven by `_config.Columns`. `Description`, if present in the list, keeps its stretch-fill semantics so it absorbs the remaining row width. Otherwise every column auto-fits its content. `CalculateAutoFitColumnWidths()` and `RenderDataRow()` iterate the single ordered list instead of zones.

4. **`% ENTRY` simplification.** Drop the `WhiteBrush` fill rectangle and the `isComplete` branch entirely. When the column is present, render only the `%` character on every row — no fill, no separate box, no completion check. The pre-existing thin cell border from the row-grid framework (the standard column separator and row outline) remains intact — that''s not part of the entry-box code path.

   Rationale: the current white-fill is half-dead. On white-background rows it''s white-on-white and visually invisible; on the alternating light-gray rows it''s a faint cue at best. The `%` character is the only consistently visible signal across all rows today. The `CUR %` column already conveys completion status; field crews can figure out where to write without the suppressed-on-complete-row cue.

5. **Cover Sheet stays independent — verify, don''t break.** `Dialogs/GenerateProgressBookDialog.xaml.cs:108-114` already computes `TotalBudgetMHs` and `TotalEarnedMHs` directly from `allActivities`, decoupled from `_config.Columns`. That path stays untouched. Add an inline comment near that block marking the cover-sheet independence contract, and validate with a one-shot test: remove MHs from the body columns, generate the book, confirm cover-page totals still render correctly.

6. **Backward-compat: auto-migrate older saved layouts silently.** Older `ProgressBookLayouts.ConfigurationJson` rows have `Columns` = [ActivityID, ROCStep, Description] only. On layout load (`ProgressBooksView.LoadLayoutConfigurationAsync`, ~line 287): if `Columns` is missing any of the 5 newly-explicit columns (MHs / QTY / REM MH / CUR % / % ENTRY) AND a new `SchemaVersion` field is absent or < 2, append the missing columns at the end in the legacy order so the rendered book is visually identical to what it was before. Stamp `SchemaVersion = 2` on the next save. No user prompt; the migration is silent.

7. **No reset button.** The "Default Layout" entry in the layouts dropdown already serves as the reset path. No new UI affordance.

## Files to touch

| File | Change |
|---|---|
| `Models/ProgressBook/ColumnConfig.cs` | Add `SourceKind` enum + property; optional `DisplayHeader` for columns whose header differs from underlying field name (MHs, REM MH, CUR %). |
| `Models/ProgressBook/ProgressBookConfiguration.cs` | Add `SchemaVersion` (int, defaults to 2 for new configs). Update default `Columns` to the 8-entry list. |
| `Views/ProgressBooksView.xaml.cs` | Remove required-column logic (lines 191-192, 310, 632). Extend available-fields list with the 5 promoted columns. Add migration in `LoadLayoutConfigurationAsync` (~line 287). |
| `Services/ProgressBook/ProgressBookPdfGenerator.cs` | Remove `Zone3DataColumns` / `Zone3EntryColumns` constants. Replace 3-zone rendering in `CalculateAutoFitColumnWidths` and `RenderDataRow` with a single ordered iteration over `_config.Columns`. Strip `DrawEntryBox` white-fill behavior; render `%` directly in the `EntryBox` cell. |
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