# MCAA Ratesheet Plan

**Owner:** Steve Amalfitano
**Status:** In progress — manual abbreviation review ongoing in the producer project (SkySkraper)
**Producer project folder (external, NOT in this repo):** `C:\Users\Steve.Amalfitano\source\repos\PrinceCorwin\SkySkraper\SynologyDrive`

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

- WebLEM scrape complete: 1,671 leaves cached as HTML in `raw_cache/`. Quarterly refresh cadence; WebLEM updates rarely.
- Extraction working: Codex's `cdx_build_rates_review.py` produces `output/cdx_rates_review.xlsx` with 174,175 rate rows.
- **Canonical workbook for the abbreviation review:** `output/cdx_rates_review_WORKING.xlsx`. The user maintains a `_WORKING` copy alongside the script-generated `cdx_rates_review.xlsx` so script regenerations don't overwrite manual edits.
- ~101K rows have abbreviations carried forward from earlier vocab work; ~73K rows still need user-driven `newComp` (lookup component) assignments.
- Two stale SQLite databases (`weblem.db` and `weblem_v2.db`) in `output/` are preserved for reference. The production SQLite will be derived from `cdx_rates_review_WORKING.xlsx` after the abbreviation review is complete.

## Producer architecture (summary)

Four-stage Python pipeline. Detail lives in the SkySkraper folder; outline:

1. **Discover** — DOM-extract leaf list from WebLEM via JS snippet → `discovery/leaves.json` (1,671 entries)
2. **Fetch** — HTML cache via `?mode=wam` (1,671 leaves) + `?mode=component` (66 leaves where WAM is empty)
3. **Survey** — schema discovery (matrix, keydata, flat) and vocabulary scan
4. **Build** — extract HTML → xlsx review surface; eventually, xlsx → SQLite

Per-leaf workbooks for human review at `output/cdx_workbooks/` (1,671 XLSX), plus the master `cdx_rates_review.xlsx` / `cdx_rates_review_WORKING.xlsx`.

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

1. **Final column-set walkthrough** — when locking the schema for the production SQLite, validate against 10–15 representative rows.
2. **xlsx → SQLite exporter** — single script, idempotent, produces `weblem_rates.db` from the locked workbook with CHECK constraints on required fields and indexes on `(newComp, material, size_1)` and `(method, leaf_id)`.
3. **MCAA labor creation service** (VANTAGE-side) — design and build after the SQLite exporter is in place.
4. **`MCAARateSheetService`** (VANTAGE-side) — sibling to existing `RateSheetService`, queries the local SQLite by facet columns.
5. **Parity-test plan** — pick 5 real recent projects, re-price under both rate sheets, document divergence cases.

## Collaboration model

- **Claude (in this repo)** drives schema lock, exporter, and VANTAGE integration. Has full Vantage code context, which is the missing piece previous sessions lacked.
- **Codex (in the SkySkraper folder)** continues extraction and normalization work it has been doing well. Operates under the SkySkraper folder's `AGENTS.md`.
- **File ownership rule:** files starting with `cdx_` are Codex's; everything else in the SkySkraper folder is Claude's. Either side can read either's files; neither modifies the other's. If one needs to modify the other's content, it copies to its own version first.
- **Folder reorganization:** the SkySkraper folder was cleaned up on 2026-04-28 (cruft and stale audit outputs removed; backup workbooks consolidated under `output/backups/`). Further reorganization (deeper Claude/Codex/shared split) deferred.

## Reference

- **Producer folder:** `C:\Users\Steve.Amalfitano\source\repos\PrinceCorwin\SkySkraper\SynologyDrive`
- **Codex's working journal (in producer folder):** `Plans/cdx_Project_Status.md`
- **Producer architecture (deep detail):** the original `SkySkraper_MCAA_Ratesheet_Plan.md` in the producer folder has been retired in favor of this PRD; consult Codex's journal for ongoing extraction notes.
