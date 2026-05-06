# AI Takeoff — Rate Mode Selector (Summit / MCAA) PRD

**Status:** Phase 1 shipped 2026-05-05. Replaces the short-lived `feature/mcaa-takeoff` branching strategy.
**Next milestone:** Phase 2 — `MCAARateSheetService` rate-lookup swap. Both modes currently use `RateSheetService`; the swap is the last code change before MCAA parity testing. See `Plans/MCAA_Ratesheet_Plan.md`.
**Related:** `Plans/MCAA_Ratesheet_Plan.md` (the larger MCAA integration PRD), `Plans/Project_Status.md` (Phase 1 entry under Active Development), `Plans/Completed_Work.md` 2026-05-05 entry (full session context for how we got here).

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

1. **Mode selector on `TakeoffView`.** Radio button group between the Action Buttons row (Process / Cancel / Previous Batches / Recalc Excel) and the Options checkboxes row, with two options: `Summit Rates` (default) and `MCAA Rates`. Visible to all users; the MCAA option is disabled (greyed out with tooltip) for everyone except the gated user.
2. **Access control.** Only the gated user (initially: hardcoded to Steve's username — see Open Questions) can select MCAA. Even if a non-gated user has `Takeoff.RateMode = MCAA` in their UserSettings (e.g., via export/import), the dialog forces it back to Summit on load.
3. **Persistence.** New UserSetting `Takeoff.RateMode` (string, "Summit" | "MCAA"). Survives app restart. Recorded into the takeoff batch metadata so re-runs and downstream Excel processing know which mode produced the workbook.
4. **Behavior gating in `TakeoffPostProcessor` (mode-conditional).** Four divergence points read the static `_currentRateMode`:
   - **BOLT/GSKT/WAS labor row.** Summit: create the aggregated hardware labor row (priced via `RateSheetService`). MCAA: return early — no labor row, Material tab unchanged.
   - **SPL (spool handling) row generation.** Summit: emit one SPL row per PIPE item with computed makeup quantity. MCAA: skip the entire `GenerateSplRows` call — no spool rows.
   - **CUT companion row on BW/SW.** Summit: skip. MCAA: emit one CUT row per BW or SW connection (Quantity=1, same size/thickness/material/class as parent; ShopField is set later by the uniform pass).
   - **Modifier neutralization in `ApplyRates`.** Summit: apply `RollupMult`, `MatlMult`, plus `CutAdd`/`BevelAdd` add-ons on BW/SW/SCRD. MCAA: force `RollupMult = 1`, `MatlMult = 1`, `CutAdd = 0`, `BevelAdd = 0` for all components, so `BudgetMHs = mhu × qty` everywhere. Audit columns honestly show 1.0/1.0/0/0 so reviewers can verify at a glance that no multiplier was applied.
5. **Uniform `ShopField` rule (cross-mode).** A single pass after `GenerateLaborRows` is the only writer of `ShopField` on labor rows: BW and SW → 1 (shop) in both modes; PIPE and TUBE → 1 (shop) under Summit only; everything else → 2 (field). Replaces the prior scattered allocations in `CreateLaborRow`, the FLGB/FLGLJ fab branch, the SPL row creation, and material-row inheritance — all of which were removed. `ShopField` was added to `ExcludeFromLabor` so labor rows no longer inherit the AI-emitted material value. Note this diverges Summit behavior from v26.2.17 for OLW, THRD, hardware, and inherited fab rows (some were 1, now uniformly 2 except BW/SW/PIPE/TUBE).
6. **Rate-lookup service routing.** Initially: both modes route to `RateSheetService` against the embedded `Resources/RateSheet.json`. Once `MCAARateSheetService` exists (Phase 2), swap the lookup at the top of `ApplyRates()` based on the active mode. This is intentionally the last code change before parity testing.
7. **Mode visibility in output.** The Summary tab's first data row is `Rate Mode: Summit` or `Rate Mode: MCAA` (bolded). Lets the user verify at-a-glance that a workbook was produced with the expected mode. File-level custom properties were rejected — too easy to lose on re-save.

## Design Decisions

- **Hardcoded username gate, not admin-table check.** Tightest possible lock: even an admin can't flip to MCAA. Only the named user can. Prevents accidental enablement during the half-finished MCAA implementation window. Revisit when MCAA is GA — then move the gate to admin-only or remove it entirely.
- **No build-time flag.** Both code paths are always present and always compile. Avoids `#if MCAA` ceremony and keeps CI simple.
- **Mode is per-user, not per-project.** A user works in one mode at a time. If two users on the same project disagreed, that's a coordination problem solved by access control during the rollout window.
- **Mode is captured in the workbook.** Lets a future user (or you) tell at a glance whether a saved Excel came from Summit or MCAA pricing without inferring from the labor row counts.

## Non-Goals

- Switching rate mode mid-takeoff. Mode is captured at takeoff-start and used throughout that batch.
- Migrating existing batches between modes. A workbook generated under Summit stays a Summit workbook; re-running under MCAA produces a separate workbook.

## Implementation Phases

**Phase 1 — Toggle infrastructure (shipped 2026-05-05).**
1. ✅ `Takeoff.RateMode` listed on the deny-list in `UserSettingsRegistry.cs` (matches the `Takeoff.LastConfigKey` precedent — has its own UI control, default hardcoded at read site).
2. ✅ Radio group on `Views/TakeoffView.xaml` between the Action Buttons row and the Options checkboxes row. `LoadRateModeSetting` reads the saved value, disables `rbMcaaRates` for non-allowed users (case-insensitive allowlist `{ steve, steve.amalfitano }`), and force-resets stored `MCAA` to `Summit` if a non-allowed user has it stored. Parse-order safe: `IsChecked` is set programmatically under a guard, not via XAML attribute.
3. ✅ Plumbed via a `RateMode` parameter on `GenerateLaborAndSummary` → captured into a static `_currentRateMode` field at the top of the run. Both call sites in `TakeoffView.xaml.cs` (post-batch download, Recalc Excel) read `rbMcaaRates.IsChecked` at click time and pass through.
4. ✅ Mode-conditional gating at the four divergence points described in FR #4.
5. ✅ Cross-mode uniform `ShopField` rule (FR #5) — single post-`GenerateLaborRows` pass, scattered allocations removed.
6. ✅ Active mode written to the Summary tab's first data row (FR #7).
7. ⏳ Summit-mode parity check vs `v26.2.17`. Initial visual sanity confirmed; full numeric parity sign-off pending. Note ShopField parity is intentionally relaxed per FR #5 — Summit OLW/THRD/hardware ShopField values now follow the uniform rule, not the v26.2.17 pattern.

**Phase 2 — MCAA rate service (separate PRD: `MCAA_Ratesheet_Plan.md`).** Build `MCAARateSheetService`, route `ApplyRates()` based on mode, ship the SQLite ratesheet.

**Phase 3 — Parity testing across 5 real projects.** Compare Summit vs MCAA totals, document deltas, get sign-off.

**Phase 4 — Sunset.** Once parity is signed off, remove the gate so all users can pick MCAA. Eventually default to MCAA. Eventually remove Summit code paths entirely.

## Migration of the Feature Branch (closed)

The `feature/mcaa-takeoff` branch's two code changes (TUBE/CUT companion rows from `907b591`, BOLT/GSKT/WAS skip from `b6aa2d2`) were cherry-picked onto main on 2026-05-05 and are now gated behind `RateMode == MCAA`. The brief window of unreleasability is closed — Summit-mode runs no longer hit the MCAA-prep code paths.

The `backup/pre-split-2026-05-05` tag stays in place permanently as a recovery anchor pointing at the pre-split tip.

## Verification

- **Summit-mode parity vs v26.2.17:** With Summit selected, Labor sheet has BOLT/GSKT/WAS aggregated rows, SPL rows, no standalone CUT companions on BW/SW, BW rows priced as `(joint + cut + bevel) × qty`, SW/SCRD rows priced as `(joint + cut) × qty`, FS rows priced as `mhu × qty`. **ShopField parity is intentionally relaxed** — OLW/THRD/hardware/inherited fab rows now follow the uniform pass (FR #5). PIPE and TUBE remain ShopField=1 under Summit.
- **MCAA-mode behavior:** With MCAA selected (gated user only), Labor sheet has no BOLT/GSKT/WAS rows, no SPL rows, every BW/SW row has a companion CUT row, all rows priced as `mhu × qty` with audit columns showing 1.0/1.0/0/0. ShopField follows the uniform rule (BW/SW → 1, everything else → 2 — including CUT companions).
- **Lock effectiveness:** Sign in as a non-gated user and verify the MCAA radio is disabled with tooltip. Try to flip the UserSetting via export/import; verify the dialog forces it back on load.
- **Mode persistence:** Change mode, close app, reopen — selection survives.
- **Mode in workbook:** Summary tab's first data row reads `Rate Mode: Summit` or `Rate Mode: MCAA` (bolded). ✅

## Risks

- **Half-finished MCAA logic on main.** MCAA-only code paths must compile and not crash even when not selected. Less forgiving than a branch where intermediate broken states are fine. Discipline-based, not enforced. Currently mitigated by gating MCAA selection to two named usernames.
- **UserSetting tampering.** Export/import or registry edit could in theory bypass the username gate at the storage layer. The dialog-load force-reset is the second line of defense. Still, treat the gate as soft — defense in depth, not a security boundary.
- **ShopField divergence from v26.2.17.** The uniform Step B1 rule changes Summit-mode ShopField values for OLW/THRD/hardware/inherited fab rows. Numeric MH totals are unaffected (ShopField doesn't influence rate lookup), but downstream consumers that filter on Shop vs Field will see different counts. Acceptable per user direction; flagged here for traceability.

*(Closed risk — kept for history: "Window of unreleasability" between cherry-picking the branch's commits and gating them. Mitigated by completing FR #4 + FR #5 before Phase 1 closure.)*

## Resolutions (2026-05-05)

1. **Username gate.** Allowed users for MCAA: `steve` or `steve.amalfitano`, case-insensitive. Source: `App.CurrentUser?.Username` (established pattern). Implemented as a small allowlist so a second user can be added later without code changes to the gate's call sites.
2. **Workbook output.** Summary tab cell. Labeled row near the top of the Summary tab so the active mode is visible the moment the tab is opened. (File-level custom properties rejected — too easy to lose on re-save.)
3. **Placement.** `Views/TakeoffView.xaml`, radio group between the Action Buttons row and the Options checkboxes row. (Originally drafted as `ImportTakeoffDialog`, which is the wrong file — that's the Progress-module import dialog, not the AI Takeoff entry point.)
4. **Previous Batches column — no.** Re-download and Recalc Excel both regenerate labor rows under whichever mode is currently selected on `TakeoffView`, not the mode used originally. The original mode is irrelevant at re-download time, so a column would be misleading.
