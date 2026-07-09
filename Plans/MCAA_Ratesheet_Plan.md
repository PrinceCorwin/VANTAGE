## MCAA Ratesheet Plan

**Owner:** Steve Amalfitano
**Status:** In progress
**Producer project (external, NOT in this repo):** `C:\Users\Steve.Amalfitano\source\repos\PrinceCorwin\SkySkraper\SynologyDrive`
**Working copies safe to edit:** the `SkySkraper\output\` folder at the **repo root** (non-synced) — NOT the `SkySkraper\SynologyDrive\` tree. Never edit or run automation against anything under `SynologyDrive\`: the Synology Drive client and Excel collide there and cause save / sharing-violation failures. The user places the workbooks that need editing in the root `output\` copy.
**Phase 1 (toggle infrastructure) shipped 2026-05-05** — UserSetting `Takeoff.RateMode`, radio group on `Views/TakeoffView.xaml` (gated to allowlisted users for MCAA), mode-conditional gating of four behaviors in `TakeoffPostProcessor` (BOLT/GSKT/WAS skip, SPL skip, CUT companion, modifier neutralization), uniform ShopField rule, mode written to Summary tab. Phase 2 = the work in this plan.

---

## High-level plan

1. **Fork AI Takeoff for MCAA.** Three new files — MCAA prompt, MCAA extraction Lambda, MCAA aggregation Lambda — created by **copying the current Summit files as the starting point**, then modifying only the OUTPUT side (extracted properties, ref vocabularies, JSON schema). Summit's three files stay frozen. The AI-facing reference tables (set and format still OPEN — see the OPEN section) would be net-new MCAA artifacts, not copies of Summit's.
   - **MCAA file locations** (under `%USERPROFILE%\Documents\<prefix>\SynologyDrive\Conversion\` where `<prefix>` is `WorkFromNAS` on the work PC and `SummitFiles` on the personal PC; originally copied 2026-05-10; relocated from Google Drive to NAS sync 2026-05-17):
     - `mcaa-takeoff-poc\extraction_prompt.txt` (copy of Summit `summit-takeoff-poc\extraction_prompt.txt`)
     - `mcaa-takeoff-poc\lambda_function.py` (copy of Summit `summit-takeoff-poc\lambda_function.py`)
     - `mcaa-aggregate-deploy\lambda_function.py` (copy of Summit `aggregate-deploy\lambda_function.py`)

2. **AI-facing reference vocabularies for the MCAA prompt — UNDECIDED.** The table set, decomposition, and format are open; see the OPEN section below. Size is a direct BOM-string capture — no ref sheet.

3. **MCAA AI extracts per BOM item:** the component, the main material, the sizes, and every other attribute as a **property** (see the LOCKED "Extracted-values model" section). Connections are extracted **per-end**, one per size, kept paired with the sizes. The reference tables and matching mechanism that drive this extraction are still OPEN (see the OPEN section).

4. **MCAA aggregation Lambda** emits those columns in the Excel output.

5. **C# composes the lookup key** per the LOCKED "Extracted-values model & key composition" section — canonical (size, connection) ordering (size-descending, connection A→Z on ties), blanks skipped, applied identically to the rate-sheet's stored keys.

6. **C# does exact key match** against the MCAA rate sheet (local SQLite shipped with VANTAGE). Misses go to a missed-rates tab; fallback ladder tuned in testing.

7. **C# routes by rate mode at takeoff time** — MCAA Lambda ARN when MCAA selected, Summit ARN otherwise.

8. **Action/connection labor rows** (welds, cuts, bevels, hydrotest) are C#-synthesized from BOM connections, inheriting parent-item properties, using the same key recipe.

**Cross-cutting integration contract:** the MCAA rate sheet (built by SkySkraper) must use byte-identical key composition with what C# composes.

**Cross-cutting input-side invariant (Summit ↔ MCAA):** BOM detection, title-block detection, drawn-boxes region handling, and all input-side extraction language MUST stay functionally identical between the two prompts. Existing user-defined drawn-boxes configs must continue to work under MCAA mode without re-configuration. Only the OUTPUT side (extracted properties, ref vocabularies, JSON schema) diverges.

---

## Extracted-values model & key composition (LOCKED 2026-07-09)

**Model.** Extracted values fall into three buckets: (1) three distinct top-level fields — the **component** abbreviation (`NewComp`), the **sizes**, and the **main material** (`NewMaterial`); (2) attributes that occupy their own **dedicated key segments** — `Reducing`, `connection_qty`, `connection_type`, `pressure_rating`, `class_rating`, `schedule`, `weight_class`, `length` (see the key below); (3) everything else — free **properties**, collected, sorted alphabetically, and pipe-joined into a single `Merged_Props` value.

**Lookup key.** One flat pipe-delimited string, blanks skipped:

```
NewComp | Reducing | NewMaterial | Merged_Props | connection_qty | connection_type
        | pressure_rating | class_rating | schedule | weight_class | length | size_1 … size_7
```

- `Merged_Props` = the item's properties, sorted A→Z, pipe-joined (each prop is its own pipe token).
- Blanks are **skipped** (`TEXTJOIN(…, TRUE, …)` semantics) — no `NONE`/placeholder sentinel.
- `connection_type` is the only field with an internal delimiter: multiple ends are **comma**-joined within its single segment (`THRD,COMP`); every other boundary is a pipe.
- Real examples: `BW|CS|WRAP|STD|0.5`, `ADPT|ABS|DWV|SPIG|2|SOLVCEM|1.5|2`.

**Size / connection ordering (applied identically on both sides).**
1. AI extracts **one connection per size**, kept paired (`connection_i ↔ size_i`), in any order — **no collapsing** identical connections.
2. C# (and the rate-sheet build) sorts the **(size, connection) pairs by size descending**; pairs stay intact.
3. On equal sizes, break ties by **connection A→Z**.
4. Emit sizes and connections in that sorted order into the key.

The ordering lives in **C#** at takeoff time (the AI is relieved of it — it only has to keep the pairs aligned) and is baked identically into the rate-sheet's stored keys. That two-sided identical sort is what satisfies the byte-identical key-composition contract.

## Rate sheet structure (FinalMerged workbook → SQLite)

Producer rate sheet: `cdx_rates_review_FinalMerged-*.xlsx`, `Rates` sheet. Column order (by header name):

`rate_id, weblem_data_id, lookup_key, component, NewComp, Reducing, manhours, newManHours, description, method, material, NewMaterial, Merged_Props, Prop1…Prop6, connection_qty, connection_type, pressure_rating, class_rating, schedule, weight_class, length, size_1…size_7, dim_display, …`

- `lookup_key` is an Excel `TEXTJOIN` formula concatenating the key segments above; `Merged_Props` is `TEXTJOIN` of the sorted `Prop1…Prop6`.
- ⚠️ **Row-1 header names are authoritative.** The Proj-Summary / session-handoff sheet's column-*letter* references are stale (columns were inserted/deleted between sessions) — always resolve columns by header name, never by letter.
- The forthcoming SkySkraper `xlsx → SQLite` exporter ships this as the local rate DB VANTAGE consumes.

## Cut-row handling — material → cut-category (DONE 2026-07-09)

MCAA's cut table doesn't key by specific material like the rest of the site — it groups everything into **five broad category-materials**: `ALLOY` (all metal alloys), `PLAS` (plastics), `IRON`, `TUBE`, `CONC`. That conflates material with piping style, and a real takeoff item carries a *specific* material — so a cut key composed with the item's real material (e.g. `CUT|CS|0.5`) wouldn't match the only stored row (`CUT|ALLOY|0.5`).

**Decision:** expand each category's cut rows out to its specific member materials, **baked into FinalMerged** (not generated later by the exporter — one consistent source of truth, no mixed formats). C# then composes the cut key with the item's real material and matches directly — no MCAA-specific relabel logic. This **supersedes** the earlier "C# relabels the cut material to `alloy`" plan.

**Material → cut-category map:**
- **ALLOY:** CS, SS, CHRMOLY, HAST, NKALLOY, ALUM, BB, CORGALV, CU
- **PLAS:** ABS, CPVC, HDPE, PEX, PLYETH, PLYPROP, PVC, PVDF, FBGLS
- **IRON:** CI, DI, MI
- **CONC:** VCLAY
- **Skip (no cut row):** GLASS, DIELEC

**TUBE is a property, not a material.** The TUBE cut rate is genuinely different (≈40–65% of the alloy rate), so the TUBE rows are duplicated to **all alloy metals** carrying a `TUBE` property → `CUT|<metal>|TUBE|<size>`. C# adds the `TUBE` property to the cut key when the item being cut is tubing. `CORSS` (corrugated stainless tubing) resolves to `SS` + `TUBE`.

**Applied 2026-07-09:** 1,107 rows appended to FinalMerged — ALLOY 261 (29 sizes × 9 metals), TUBE 405 (45 × 9, +TUBE prop), PLAS 351 (39 × 9), IRON 63 (21 × 3), CONC 27 (27 × 1). Same rate carried into each copy; keys reuse the source key's exact size token. `rate_id` filled for all 1,268 cut rows that lacked one (161 existing category rows + 1,107 new), continuing from the prior max (61,452 → 62,720) — existing IDs untouched, **not** a full-sheet renumber. Originals (the 5 category cut rows) kept.

## Deployment — where the rate sheet lives (decided 2026-07-09)

The MCAA rate sheet ships as a **downloaded SQLite reference file**, saved to the local app-data folder alongside the cache DB, logs, and other reference files — **not hard-coded / bundled** in the app. A small version manifest (version + URL) is checked on startup and the DB is pulled if newer, mirroring VANTAGE's existing app-updater manifest and `plugins-index.json` patterns.

Why: the sheet is already ~116k rows and expected to at least double. Downloading it (a) keeps size out of the installer and (b) **decouples rate-sheet updates from app releases** — publish a new DB, users pull it without a VANTAGE update. FinalMerged (`.xlsx`) stays the producer artifact; the `xlsx → SQLite` exporter produces the shipped DB.

## Producer-side to-dos (rate-sheet normalization)

- [x] **Cut-row material expansion + TUBE-as-property — DONE 2026-07-09.** See the "Cut-row handling" section above.

- [ ] **Expand collapsed connection types.** Go through FinalMerged and extrapolate per-end connection types for multi-size items whose ends are all the same type — where a uniform connection was listed once, expand to one connection per size (`BW` → `BW,BW`) so every end is represented. (Today ~31k multi-size rows carry fewer connection tokens than sizes because identical ends were collapsed.)
- [ ] **Re-sort the key to canonical order.** After expansion, rebuild the key so each row's (size, connection) pairs are ordered **size-descending, connection A→Z on ties** — fixing the 682 small-first rows and the fixed-connection-order rows in the same pass.

## OPEN — AI-facing reference tables (undecided, active discussion)

The set of reference/vocabulary tables sent to the AI, and their format, is **not decided** — it's part of the ongoing Phase-2 discussion. Treat nothing here as canon yet:

- The workbook's current vocab sheets (`conTypes`, `Matl Grade`, `bodyType`) are **provisional, not canonical**. New vocabulary lists will be regenerated from the current **unique values in FinalMerged**.
- **Candidate decomposition (earlier sketch, NOT settled — including whether it's even the right split):** four per-property tables so the AI makes several small discriminations instead of one Cartesian one — `CompRefTable` (components), `ConnRefTable` (connection abbreviations), `MatRefTable` (material + grade combined; plain vs. sanitary-food-grade as separate rows so the AI picks the most specific), `BodyTypeRefTable` (body type, kept separate to avoid a Cartesian explosion). Superseded within this idea: the old "ConnRefTable stores a verbatim connection *pattern*, qty implicit from its length" — connections are now per-end and `connection_qty` is tracked (see LOCKED).
- Also open: how the AI discriminates the large **property bag** (there's no property vocabulary among the four tables yet).
- **Property-search scoping — leaning component + material.** Measured against FinalMerged: 129 total distinct properties → ~16 median per material → **~2 median per (component, material)**. Scoping the AI's property search by component + material collapses the list to ~2, far more reliable than sifting all 129. Snapshot table built: `Plans/MCAA_Property_Applicability.xlsx` (574 combos + a by-material sheet; regenerate as FinalMerged grows). Whether/how it's fed to the AI (one master table sliced per item; two-stage vs single-pass extraction) stays part of this OPEN question.
- `connection_qty` **stays — do not drop it.** Even though it's derivable once connections are listed per-end, it's kept at minimum as a backup/validation count, and is a candidate to live in the AI reference table: once the AI identifies a component, `connection_qty` tells it how many connections to look for and it returns one type per end. Whether we use it that way is open.

**Guardrail:** don't delete or strip source data (columns, values) before the design is settled — keep everything until we've decided what each field is for.

Do not act on any ref-table specifics until this section is resolved.

---

## Development sequence

1. **Pick a small reference drawing** — ~10–20 BOM items, all everyday CS/SS work, exercising a spread of properties at least once (multiple connection types, multi-size items, varied schedules/geometry). Outliers (sanitation / food-quality grades, 9-chrome, Hastelloy, belled end, etc.) are deferred to ongoing rate-sheet expansion — not part of the initial reference slice.
2. **Curate the rate sheet rows and ref vocabularies** to cover every item in the reference drawing. Use the (forthcoming) SkySkraper xlsx → SQLite exporter on just the subset of rate-sheet rows the drawing needs — sample DB is production-shaped from day one.
3. **Iterate the prompt and Lambdas** until the reference drawing extracts cleanly and every item finds a rate via exact-key lookup in the sample DB.
4. **Then scale** to bigger and more varied drawings, growing the rate sheet and ref vocabularies as new items appear. Once 90%+ CS/SS coverage is locked in, that's already close to production-level; outliers continue as ongoing WIP.

**Realistic scope — full automation is NOT a goal.** User interaction will always be needed for items that don't fit the MCAA rate sheet (owner-supplied heaters, instrumentation, etc.). Intended workflow for these:
- AI emits a best-effort row
- User opens the Material tab, picks a similar item the rate sheet does cover, fills in the corrected component / material / etc.
- User clicks Recalc Excel; the lookup re-runs against the corrected row

---

## Deferred details — bring up for alignment when the relevant section is being detailed

Granular items raised during high-level planning that get punted until the section that owns them is being designed. Bring each one up at the noted point — don't act on them silently.

- **Letter-case normalization in the property vocabularies.** Sample values mix all-caps (`BELL`, `DWV`, `COMPAIR`, `VACC`) with title case (`Soft`). Pick one casing rule, apply during rate-sheet cleanup, have C# normalize the same way before key composition. Otherwise `Soft` vs `SOFT` is a silent miss. — Bring up when designing the C# key composer or when finalizing any MCAA ref vocabulary.
- **Bare-numeral property values (`45`, `90`).** Confirm during rate-sheet cleanup that no MCAA component code is the bare numeral standalone (so namespaces stay disjoint). — Bring up when scrubbing the MCAA component vocabulary.
- **Fallback ladder design for missed lookups.** Order in which properties get dropped from the key when an exact match fails (analogous to Summit's "thickness as-is → toggle leading S → class rating → size-only" ladder). Tune in testing once we see what actually misses. — Bring up after the first MCAA-mode takeoff is run against the rate sheet.
- **Missed-rate triage workflow.** Operational path when a key misses — manual entry, suggested coarser fallback, rate-sheet update. — Bring up when designing the missed-rates tab UX.
- **Guiding the AI to detect and extract properties.** The core extraction challenge: getting the AI to recognize whether a BOM item carries any properties and pull them out correctly. Properties are noisy — often buried in long descriptions or unstated — so expect a higher miss rate early. Folds in: (a) **most-specific value wins** — when several property values could match, the AI must pick the most specific (e.g. sanitary-food-grade vs. plain stainless), not settle for the generic just because its words appear; (b) **disjoint abbreviation namespace** — property abbreviations must not collide with connection-type abbreviations (a token like `BW` can't be ambiguous). — Bring up when iterating the prompt against the reference drawing and finalizing the property vocabulary.
- **Connection-type abbreviation vocabulary contract.** Producer rate sheet `connection_type` column has to match byte-for-byte what C# writes into the key. Same shape of risk as the component-abbreviation contract. — Bring up when finalizing the MCAA `connection_type` vocabulary on the producer side.
- **Allow AI to emit blanks on component/material when uncertain?** Previously attempted — AI over-used blanks as an easy escape hatch. There may be a middle ground (low-confidence flag, threshold-gated blanks, "force-pick from CompRefTable" rule, etc.). — Bring up when designing the missed-rate triage workflow / Material-tab user-correction UX.
- **Learned-corrections DB for items with missing/wrong AI-extracted properties.** Local SQLite table keyed by commodity/item code (primary, when present) and exact normalized BOM description (fallback). After takeoff returns, C# applies saved corrections to a row's properties BEFORE the rate-lookup key is composed, so the user doesn't re-correct the same items every project. Compounds value over time. Doubles as a feedback signal — frequent identical corrections suggest the rate sheet needs an alias or the prompt needs tuning. Open design questions: (a) per-project vs global scope, (b) auto-save every Material-tab edit vs explicit "save correction" button, (c) audit column in Excel marking corrected-from-DB vs AI-extracted rows so debugging stays transparent. Matching strictness is the central design risk — too loose = silent misapplication to a different item; too strict = rarely fires and user re-corrects forever. — Bring up after the missed-rate triage workflow is in place and Material-tab corrections start happening at volume.

---

Detail gets added section by section as each step is worked.
