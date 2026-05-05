# AI Takeoff — Rate Mode Selector (Summit / MCAA) PRD

**Status:** Proposed 2026-05-05. Replaces the short-lived `feature/mcaa-takeoff` branching strategy.
**Priority:** High — unblocks publishing while MCAA work continues.
**Related:** `Plans/MCAA_Ratesheet_Plan.md` (the larger MCAA integration PRD).

## Problem

The MCAA ratesheet integration requires takeoff post-processing logic that conflicts with Summit pricing:

- **CUT companion rows** on every BW/SW connection — needed for MCAA per-joint cut labor; double-counts under Summit (where `CutAdd` is already folded into the BW/SW row).
- **Skip BOLT/GSKT/WAS labor rows** — MCAA captures bolt-up labor in the joint rate; Summit prices these as separate hardware lines.
- **Different rate-lookup service** — MCAA queries a local SQLite produced by SkySkraper; Summit reads `Resources/RateSheet.json`. (Future, larger work.)

A short-lived feature branch (`feature/mcaa-takeoff`) was created to isolate the in-progress MCAA changes so main could continue to publish. That works, but adds cognitive overhead (branch juggling, partial-state docs, merge timing) for what is structurally a long-lived behavioral fork. The toggle is needed long-term anyway — Summit ratesheet is supposed to remain selectable until MCAA parity is proven across 5 real projects, then sunset over 6–12 months.

## Goal

Single `main` branch. A user-selected rate mode (Summit / MCAA) gates the divergent post-processing behaviors and (eventually) routes to the appropriate rate-lookup service. Default = Summit. Mode is locked to a single user (Steve) until MCAA implementation is complete and parity-tested.

## Out of Scope

- The MCAA rate-lookup service itself (`MCAARateSheetService`) — covered by `Plans/MCAA_Ratesheet_Plan.md`. This PRD only adds the mode-selection scaffolding and gates the Summit/MCAA-divergent post-processing behaviors that are already implemented on the (soon-deleted) feature branch.
- Per-project rate-mode override. Mode is a user-level setting, not project-scoped. (Reconsider once MCAA goes GA.)
- UI to compare Summit vs MCAA results side-by-side. Comparison happens manually during parity testing.

## Functional Requirements

1. **Mode selector in `ImportTakeoffDialog`.** Radio button group (or labeled dropdown) with two options: `Summit Rates` (default) and `MCAA Rates`. Visible to all users; the MCAA option is disabled (greyed out with tooltip) for everyone except the gated user.
2. **Access control.** Only the gated user (initially: hardcoded to Steve's username — see Open Questions) can select MCAA. Even if a non-gated user has `Takeoff.RateMode = MCAA` in their UserSettings (e.g., via export/import), the dialog forces it back to Summit on load.
3. **Persistence.** New UserSetting `Takeoff.RateMode` (string, "Summit" | "MCAA"). Survives app restart. Recorded into the takeoff batch metadata so re-runs and downstream Excel processing know which mode produced the workbook.
4. **Behavior gating in `TakeoffPostProcessor`.** Two surgical conditionals:
   - **BOLT/GSKT/WAS labor row.** Summit: create row (priced via `RateSheetService`). MCAA: return early — no labor row, Material tab unchanged.
   - **CUT companion row on BW/SW.** Summit: skip. MCAA: emit one CUT row per connection (Quantity=1, ShopField=1, same size/thickness/material/class as parent).
5. **Rate-lookup service routing.** Initially: both modes route to `RateSheetService`. (MCAA path will be unreachable in practice because MCAA mode is gated to one user who isn't ready to use it yet.) Once `MCAARateSheetService` exists, swap the lookup at the top of `ApplyRates()` based on the active mode.
6. **Mode visibility in output.** The takeoff Excel includes the mode used to generate it (e.g., a `RateMode` field on the Summary tab or in the file metadata). Lets the user verify at-a-glance that a workbook was produced with the expected mode.

## Design Decisions

- **Hardcoded username gate, not admin-table check.** Tightest possible lock: even an admin can't flip to MCAA. Only the named user can. Prevents accidental enablement during the half-finished MCAA implementation window. Revisit when MCAA is GA — then move the gate to admin-only or remove it entirely.
- **No build-time flag.** Both code paths are always present and always compile. Avoids `#if MCAA` ceremony and keeps CI simple.
- **Mode is per-user, not per-project.** A user works in one mode at a time. If two users on the same project disagreed, that's a coordination problem solved by access control during the rollout window.
- **Mode is captured in the workbook.** Lets a future user (or you) tell at a glance whether a saved Excel came from Summit or MCAA pricing without inferring from the labor row counts.

## Non-Goals

- Switching rate mode mid-takeoff. Mode is captured at takeoff-start and used throughout that batch.
- Migrating existing batches between modes. A workbook generated under Summit stays a Summit workbook; re-running under MCAA produces a separate workbook.

## Implementation Phases

**Phase 1 — Toggle infrastructure (this PRD).**
1. Add `Takeoff.RateMode` UserSetting with default `"Summit"`. Register in `UserSettingsRegistry.cs`.
2. Add the radio group to `ImportTakeoffDialog.xaml`. Wire to UserSettings, with the username-gated lock on MCAA selection.
3. Plumb the selected mode from `ImportTakeoffDialog` → `TakeoffSession` → `TakeoffPostProcessor`. Pass as a parameter or store on `TakeoffSession` for the post-processor to read.
4. Gate the BOLT/GSKT/WAS skip and the CUT companion row generation behind the mode flag in `TakeoffPostProcessor`.
5. Verify Summit-mode parity: run a takeoff with Summit selected and confirm the output matches the v26.2.17 reference (no double-counted CUT, hardware rows present and priced).

**Phase 2 — MCAA rate service (separate PRD: `MCAA_Ratesheet_Plan.md`).** Build `MCAARateSheetService`, route `ApplyRates()` based on mode, ship the SQLite ratesheet.

**Phase 3 — Parity testing across 5 real projects.** Compare Summit vs MCAA totals, document deltas, get sign-off.

**Phase 4 — Sunset.** Once parity is signed off, remove the gate so all users can pick MCAA. Eventually default to MCAA. Eventually remove Summit code paths entirely.

## Migration of the Feature Branch

The `feature/mcaa-takeoff` branch's two code changes (TUBE/CUT companion rows from `907b591`, BOLT/GSKT/WAS skip from `b6aa2d2`) come back onto main as part of this PRD's Phase 1. Until Phase 1 is complete, those behaviors will be active unconditionally on main — which means **main is not publishable during the Phase 1 window**. Plan to do Phase 1 in a single sitting.

The `backup/pre-split-2026-05-05` tag stays in place permanently as a recovery anchor.

## Verification

- **Summit-mode parity:** Same project run with Summit selected pre-and-post Phase 1 produces identical Labor sheet (row count, BudgetMHs, BOLT/GSKT/WAS rows present, no CUT companions, FS rows multiplied by 1.0 only).
- **MCAA-mode behavior:** With MCAA selected (gated user only), Labor sheet has no BOLT/GSKT/WAS rows, every BW/SW row has a companion CUT row, FS multipliers neutralized.
- **Lock effectiveness:** Sign in as a non-gated user and verify the MCAA radio is disabled with tooltip. Try to flip the UserSetting via export/import; verify the dialog forces it back on load.
- **Mode persistence:** Change mode, close app, reopen — selection survives.
- **Mode in workbook:** Generated Excel reports the mode used.

## Risks

- **Window of unreleasability.** Between cherry-picking the branch's commits onto main and finishing Phase 1, main has the broken double-counting CUT behavior unconditionally. Don't publish during that window. Single-session implementation reduces exposure.
- **Half-finished MCAA logic on main.** Future MCAA-only code paths must still compile and not crash even when not selected. Less forgiving than a branch where intermediate broken states are fine. Discipline-based, not enforced.
- **UserSetting tampering.** Export/import or registry edit could in theory bypass the username gate at the storage layer. The dialog-load force-reset is the second line of defense. Still, treat the gate as soft — defense in depth, not a security boundary.

## Open Questions

1. **What username goes in the gate?** Steve's Vantage username, presumably. Confirm the exact string and where to source it from (`App.CurrentUser?.Username`?).
2. **Where does the `RateMode` field live in the workbook?** Summary tab as a labeled cell? File-level custom property? Both?
3. **Does the mode dropdown belong in `ImportTakeoffDialog` or earlier (e.g., on the Takeoffs tab itself)?** Dialog feels right since mode is per-batch, but tab-level would be more visible.
4. **Should the takeoff Previous Batches list show the mode column?** Helps avoid re-running the wrong mode on a re-download.
