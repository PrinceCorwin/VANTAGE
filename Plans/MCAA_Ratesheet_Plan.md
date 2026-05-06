# MCAA Ratesheet Plan

**Owner:** Steve Amalfitano
**Status:** In progress — manual abbreviation review ongoing in the producer project (SkySkraper)
**Producer project folder (external, NOT in this repo):** `C:\Users\Steve.Amalfitano\source\repos\PrinceCorwin\SkySkraper\SynologyDrive`
**See also:** `Plans/Takeoff_Rate_Mode_PRD.md` — the in-app rate-mode toggle (Summit / MCAA) that gates which lookup service this PRD's `MCAARateSheetService` plugs into.

---

## Purpose

Replace VANTAGE's embedded `Resources/RateSheet.json` (Summit's hand-curated labor rates) with industry-standard MCAA WebLEM rates as the lookup source for piping takeoff items.

The producer is a standalone Python pipeline that scrapes WebLEM and produces a queryable database. The consumer is VANTAGE — specifically the takeoff-side service that turns BOM items + connections into priced labor rows.

For this initial cutover, MCAA appears as a new option on the existing per-takeoff-line rate-source dropdown alongside Summit's embedded rates. Once parity-tested across multiple real projects, the embedded Summit rates are removed and MCAA becomes the sole source.

## Project locations

- **Producer (data pipeline):** SkySkraper folder above. Synology Drive synced; **NOT** in this Git repo. Has its own `CLAUDE.md` (Python conventions, scrape rules), `AGENTS.md` (Codex conventions), and `Plans/` (Codex's working journal `cdx_Project_Status.md`).
- **Consumer (this repo):** VANTAGE — implements the local SQLite query layer, the new labor-creation service, and the integration with the takeoff post-processing pipeline.

This document is the canonical PRD. Project status and completed work for the MCAA effort live in this repo's `Plans/Project_Status.md` and `Plans/Completed_Work.md`. The producer project's own status docs (Claude-side) have been retired in favor of these; Codex retains its own working journal in the SkySkraper folder.

## Current state of the producer

- WebLEM scrape complete: full corpus (1,926 leaves across all 10 top-level sections) cached as HTML in `raw_cache/`, organized one folder per section (`html_piping_systems/` 1,671, `html_hvac_equipment/` 80, `html_plumbing_equipment/` 47, `html_hangers_sleeves_inserts/` 38, `html_treatment_plant_equipment/` 36, `html_refrigeration_equipment/` 19, `html_miscellaneous_labor_operations/` 13, `html_instrumentation/` 11, `html_excavation_backfill/` 9, `html_plumbing_fixtures/` 2). Quarterly refresh cadence; WebLEM updates rarely.
- Extraction working: Codex's `cdx_build_rates_review.py` produces `output/cdx_rates_review.xlsx` with 174,175 rate rows.
- **Canonical workbook for the abbreviation review:** `output/cdx_rates_review_WORKING.xlsx`. The user maintains a `_WORKING` copy alongside the script-generated `cdx_rates_review.xlsx` so script regenerations don't overwrite manual edits.
- ~101K rows have abbreviations carried forward from earlier vocab work; ~73K rows still need user-driven `newComp` (lookup component) assignments.
- Two stale SQLite databases (`weblem.db` and `weblem_v2.db`) in `output/` are preserved for reference. The production SQLite will be derived from `cdx_rates_review_WORKING.xlsx` after the abbreviation review is complete.

## Producer architecture (summary)

Four-stage Python pipeline. Detail lives in the SkySkraper folder; outline:

1. **Discover** — DOM-extract leaf list from WebLEM via JS snippet → `dom_leaves.json` (1,926 entries) → filtered into per-section `discovery/leaves_<section>.json` files (PIPING SYSTEMS in the original `discovery/leaves.json`, others in sibling files)
2. **Fetch** — HTML cache via `?mode=wam` (1,926 leaves total, one folder per section) + `?mode=component` (66 PIPING SYSTEMS leaves where WAM is empty)
3. **Survey** — schema discovery (matrix, keydata, flat) and vocabulary scan (currently piping-only; non-piping sections need their own survey pass)
4. **Build** — extract HTML → xlsx review surface; eventually, xlsx → SQLite

Per-leaf workbooks for human review at `output/cdx_workbooks/` (1,671 PIPING SYSTEMS XLSX so far; non-piping section workbooks generate when those parsers come online), plus the master `cdx_rates_review.xlsx` / `cdx_rates_review_WORKING.xlsx`.

## Vocabulary architecture

Three vocabulary sources interact:

- **`CompRefTable.xlsx`** — Summit's BOM-item descriptions. Drives the takeoff AI agent's drawing-item → abbreviation assignment.
- **VANTAGE's `Resources/RateSheet.json`** — current embedded ratesheet. Will be deprecated.
- **MCAA Ratesheet (this project's output)** — built fresh from WebLEM's actual property space.

**CompRefTable bridge rules:**

- Items that exist in MCAA AND are emitted by the takeoff AI agent go in CompRefTable. After the abbreviation review is locked, CompRefTable gets updated with any new item codes MCAA introduces.
- Actions and connections (welds, bolts, cuts, bevels, hydrotest, etc.) go ONLY in MCAA. The takeoff AI does not extract these from drawings; their labor rows are derived from BOM connections at labor-creation time.
- New MCAA-only abbreviations not in CompRefTable are tracked separately so CompRefTable stays scoped to its primary job (drawing → abbreviation).

## Schema decisions (locked)

- **Two-column component identity during review:** `component` (verbatim source from WebLEM) + `newComp` (user-assigned lookup abbreviation). After the abbreviation review is complete, `newComp` values replace `component` values; the verbatim `component` is no longer needed for production lookup.
- **Material columns:** `material`, `material_grade`, `material_category` (separate column for the WebLEM Cut-Table category map: Alloy / Plastic / Tubing / Iron / Concrete).
- **Drop CDX provenance columns** (`raw_header_path`, `raw_column_headers`, `component_source`, `component_confidence`, etc.) from the final SQLite. They stay in the xlsx review surface for debugging but don't ship.
- **Defer `lookup_key` string design.** VANTAGE will query by facet columns (`WHERE component='ELB' AND material='CS' AND size_1=0.5`), not by composed string keys. This removes multi-port encoding complexity from the critical path. Adding a string key later is straightforward if a downstream consumer ever needs one.
- **Final column-set walkthrough is TBD.** When we build the exporter, walk 10–15 representative rows (CS elbow, Chrome Moly flange, Cut Table row, hydrotest row, multi-port manifold, etc.) and lock the column set against real data, not against abstract preferences.
- **WAM vs Component:** scrape rule is XOR per leaf — WAM if non-empty, else Component. The 3 Repad leaves (Reinforcing Saddle pages) emit both methods side-by-side in the v2 builder; verify whether they produce facet-colliding rows that need DB-build-time disambiguation. No VANTAGE-side rule needed otherwise.

## AI extraction → lookup contract

The schema is sized to fit what AI Takeoff actually produces, not a hypothetical extraction surface.

- **AI Takeoff input scope (locked).** AI Takeoff extracts only two surfaces from each drawing PDF: the BOM table and the title block. There is no line drawing scan and no line list. Material almost always lives in the BOM description. This bounds every other choice in this section.
- **Component-level identity.** AI returns a basic component code (`EL90`, `ADPTRED`, `TEE`, ...) plus a small set of property values extracted from the BOM description. The `(component, properties...)` tuple uniquely identifies an MCAA rate row once the property set is complete. Two viable implementations of the lookup against this tuple — see next bullet.
- **Lookup implementation — decision deferred until the rate sheet is complete.** Both options keep CompRefTable bounded (it still enumerates components only, not rate rows). Choice between them is about how VANTAGE looks up the rate once the AI returns its `(component, properties)` tuple.
  - **Option A — facet-column query.** SQLite stores each property as a separate indexed column. Lookup is `WHERE component = ? AND body_style = ? AND material = ? AND size_1 = ? ...`. Native SQL handles NULL/optional properties and fallback rules (OR clauses). Adding a property later is `ALTER TABLE`. Easy to debug.
  - **Option B — concatenated key.** Producer concatenates `component` + properties in canonical order into a single indexed key column. VANTAGE composes the same key from AI extraction and looks up by string equality. Same artifact serves the producer-side uniqueness check (see "Property-completeness check" below) and production lookup. Catch: producer and consumer must agree byte-perfectly on canonicalization (NULL slots, size formatting, casing); fallback rules require rebuilding the key and retrying instead of one OR clause.
  - **Cheap hedge:** SQLite can carry both — facet columns AND a concatenated key column, both indexed. Pick either as primary lookup; switch later without a schema change. Final call after the rate sheet is filled out and we see how often Option B's catches actually bite.
- **Why not row-level unique keys (with size/material baked in).** Different from Option B — this would be one CompRefTable entry per MCAA rate row, e.g. `EL90-CS-2X1-SCH40`. Rejected because CompRefTable would have to enumerate every variant, and CompRefTable is injected into every extraction prompt. Token cost scales with its row count, so a thousands-of-rows CompRefTable is infeasible regardless of any accuracy benefit. Component-level identity (above) keeps CompRefTable bounded.
- **Radius modifiers ride on the component code.** `SR` (short radius) and `LR` (long radius) are folded into the component code (`EL90SR`, `EL90LR`) rather than extracted as a separate body-style value. One less decision for the AI per item, and matches how `newComp` is already being assigned in the rate-table review. Apply the same approach to any other modifier the abbreviation can carry without exploding the keyspace.
- **Body style is a new extracted property** for modifiers that can't ride on the component code. Anticipated values include `MALE`, `FEMALE`, `FEMALE_BRANCH`, `CONCENTRIC`, `ECCENTRIC`, ... — final list determined by what the MCAA rate table actually distinguishes. A body-style reference table will be added to the extraction prompt alongside CompRefTable.
- **Concentric/eccentric reducers — storage + fallback rule.** Some MCAA reducer rates are body-style-agnostic ("either"); some are CONCENTRIC-specific; some are ECCENTRIC-specific. **Storage:** body-style-agnostic rows are stored with `body_style = CONCENTRIC`. **Lookup:** try the AI-extracted value first; if no match, retry as `CONCENTRIC`. Keeps the AI's body-style enum small and avoids duplicating "either" rows in SQLite.
- **Final property list is gated on the rate-table review.** The property columns the rate table actually needs to disambiguate same-component rows are the ones VANTAGE will query by. Schema lock waits for the abbreviation review to settle so we know that set against real data, not abstract preferences.
- **Property-completeness check via synthetic key uniqueness.** When the rate workbook is filled out, concatenate `component` + all property columns into a synthetic key per row and look for duplicates. Every row in the rate sheet represents a unique rated item or action, so any duplicate key proves a property is missing — the colliding rows are the same rated thing as the schema currently sees it. Iterate (add the missing property column, refill it) until all synthetic keys are unique; at that point the property set is complete. AI extraction is then tuned to produce exactly that property set. The same key-construction logic could double as the production lookup mechanism (Option B above) — final call after the rate sheet is done.
- **Pivot escape hatch.** If body-style extraction proves unreliable in practice, or if a property cluster turns out to require row-level keys after all, this section gets revisited — not the whole approach.

## VANTAGE integration plan

- **Storage:** local SQLite shipped with the VANTAGE installer, kept in AppData alongside the existing local DB. Auto-updater can refresh it independently of full VANTAGE releases. Not embedded as a JSON resource. Not Azure-hosted (must work offline).
- **Service:** new `MCAARateSheetService` sibling class. **No refactor of the existing `RateSheetService.cs`.** The embedded service is being deprecated; refactoring it to dispatch to MCAA is throwaway work. Side-by-side services until parity is proven, then `RateSheetService` and its embedded JSON are deleted.
- **UI:** the existing per-takeoff-line rate-source dropdown gains "MCAA" as an option alongside "Summit." Project-level default with per-line override.
- **Labor creation service (new):** the current labor-creation flow handles BOM items only. MCAA-driven labor includes actions/connections (welds, cuts, bevels, hydrotest, etc.) that need a different generation pattern — those are derived from BOM connections, not extracted from drawings. Likely a dedicated `MCAALaborService` paired with `MCAARateSheetService`. Scope this when the abbreviation review is done and the SQLite exporter is in place.

## Done conditions

- **Abbreviation review:** user-driven. The user signals when `newComp` is complete; at that point the `newComp` values replace `component` values throughout `cdx_rates_review_WORKING.xlsx` and the xlsx → SQLite exporter is built.
- **MCAA cutover:** parity-test against 5 real recent projects. Expect 60–80% line match within 10% manhours — divergence is normal because Summit's embedded rates and MCAA were built for different reasons. Significant gaps are the design conversation, not a regression.
- **Sunset of embedded Summit rates:** target 6–12 months after MCAA goes live, contingent on parity criteria being met (no escalations attributable to rate gaps, MCAA covers all in-use takeoff codes).

## Open todos

1. **Reducer `body_type` populated for all rows** (producer-side, during abbreviation review). Ensure every Reducer rate has a `body_type` value so all reducers can be labeled `RED` (concentric/eccentric agnostic) rather than split into `REDCON` (concentric) and `REDECC` (eccentric). Some MCAA rates apply to either type — distinct codes would force false splits where a single rate must serve both shapes.
2. **Final column-set walkthrough** — when locking the schema for the production SQLite, validate against 10–15 representative rows.
3. **xlsx → SQLite exporter** — single script, idempotent, produces `weblem_rates.db` from the locked workbook with CHECK constraints on required fields and indexes on `(newComp, material, size_1)` and `(method, leaf_id)`.
4. **MCAA labor creation service** (VANTAGE-side) — design and build after the SQLite exporter is in place.
5. **`MCAARateSheetService`** (VANTAGE-side) — sibling to existing `RateSheetService`, queries the local SQLite by facet columns.
6. **Parity-test plan** — pick 5 real recent projects, re-price under both rate sheets, document divergence cases.

## Collaboration model

- **Claude (in this repo)** drives schema lock, exporter, and VANTAGE integration. Has full Vantage code context, which is the missing piece previous sessions lacked.
- **Codex (in the SkySkraper folder)** continues extraction and normalization work it has been doing well. Operates under the SkySkraper folder's `AGENTS.md`.
- **File ownership rule:** files starting with `cdx_` are Codex's; everything else in the SkySkraper folder is Claude's. Either side can read either's files; neither modifies the other's. If one needs to modify the other's content, it copies to its own version first.
- **Folder reorganization:** the SkySkraper folder was cleaned up on 2026-04-28 (cruft and stale audit outputs removed; backup workbooks consolidated under `output/backups/`). Further reorganization (deeper Claude/Codex/shared split) deferred.

## Reference

- **Producer folder:** `C:\Users\Steve.Amalfitano\source\repos\PrinceCorwin\SkySkraper\SynologyDrive`
- **Codex's working journal (in producer folder):** `Plans/cdx_Project_Status.md`
- **Producer architecture (deep detail):** the original `SkySkraper_MCAA_Ratesheet_Plan.md` in the producer folder has been retired in favor of this PRD; consult Codex's journal for ongoing extraction notes.
