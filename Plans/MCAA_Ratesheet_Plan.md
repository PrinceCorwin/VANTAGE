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

> **⚠️ Authoritative recipe now lives in `Plans/MCAA_Key_Composition.md`** — the segment order, Excel formulas, column map, and C# contract are maintained there. Two changes below are SUPERSEDED by that doc: (1) sizes/connections are **not sorted** (stored column order is canonical, per 2026-07-10); (2) **`connection_type` moved to just before the sizes** (2026-08-06), no longer immediately after `connection_qty`. The narrative below is kept for model background; use the key doc for the exact recipe.

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

**Size / connection ordering — STORED ORDER, no sorting (updated 2026-07-10, supersedes the sort rule below).**
1. AI extracts **one connection per size**, kept paired (`connection_i ↔ size_i`) — **no collapsing** identical connections.
2. **Neither side sorts.** Sizes and connection tokens enter the key in **stored column order**; whatever order the rate-sheet row stores them in IS the canonical order, and C# must emit the takeoff item's pairs in that same order to get a byte-identical key.
3. `Merged_Props` is the only sorted segment — its properties sort A→Z (unordered adjectives).
4. Reducing flanges use a specific stored order: `BU` pairs with the larger size, and since `size_1` is the larger, `BU` leads the connection token list (`BU,BW`, `BU,THRD`, …). See the flange normalization notes in "Producer-side to-dos".

**Superseded (do not apply):** an earlier contract sorted the (size, connection) pairs size-descending with a connection-A→Z tie-break, applied identically on both sides. That sort collapsed genuinely-distinct items into ~1,400 false-duplicate keys (a `2×1` reducer keyed identically to `1×2`), so sorting of sizes/connections was **dropped 2026-07-10**.

## Rate sheet structure (FinalMerged workbook → SQLite)

Producer rate sheet: `cdx_rates_review_FinalMerged-*.xlsx`, `Rates` sheet. Column order (by header name):

`rate_id, weblem_data_id, lookup_key, component, NewComp, Reducing, manhours, newManHours, description, method, material, NewMaterial, Merged_Props, Prop1…Prop6, connection_qty, connection_type, pressure_rating, class_rating, schedule, weight_class, length, size_1…size_7, dim_display, …`

- `lookup_key` is a plain **in-order** Excel `TEXTJOIN` of the key segments above — **no sorting** (stored column order is canonical); `Merged_Props` is `TEXTJOIN` of the A→Z-sorted `Prop1…Prop6`. Paste-ready current formulas: `SkySkraper/output/new_key_formulas.txt`.
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

**Working file (as of 2026-07-10): `output/cdx_rates_review_FinalMerged-r3.xlsx`** — r2 was duplicated to r3 and all normalization passes land in r3; r2 is frozen as the pre-normalization snapshot. Per-pass backups go to `output/backups/`.

**Per-end normalization framework (2026-07-10).** Profiling all 116,015 data rows with `connection_qty` as the authoritative end count showed sizes are collapsed as well as connection types. Canonical target: **every qty≥1 row carries exactly qty sizes and qty connection tokens** — pairs per end, so a same-size multi-end item (2"×2" adapter) lists the size once per end (`…|2|2`), never once per distinct value. The AI/C# side must emit the same. Buckets are approved and applied **one at a time** (user direction):

| Bucket | Rows | Shape | Status |
|---|---|---|---|
| A — already per-end | 96,584 (final, after B/C/D/F + flange normalization) — re-verified vs live r3 2026-08-05 | sizes = tokens = qty | final key rebuild only |
| B — type collapsed | 31,183 | all sizes listed, 1 uniform token | ✅ DONE 2026-07-10 |
| C — sizes collapsed | 6,976 | all tokens listed, 1 distinct size listed once | ✅ DONE (re-applied) 2026-08-05 |
| D — both collapsed | 25,051 | 1 size, 1 token, qty > 1 | ✅ DONE 2026-08-05 |
| F — irregular | 216 | counts disagree in other ways | ✅ DONE 2026-08-05 (user-corrected CSV applied) |
| no-joint (incl. PIPE/TUBE blank-qty) | 19,431 (14,071 no-token + 5,360 PIPE/TUBE joint-method rows) | qty 0/blank | untouched — PIPE/TUBE confirm still owed |

_Live re-verification against `output/cdx_rates_review_FinalMerged-r3.xlsx` (2026-08-05, after F): **A=96,584, C=0, D=0, F=0, no-joint=19,431 — sums to 116,015.** Key column is 100% static values (expected). All joint-bearing rows now carry `connection_qty` sizes AND `connection_qty` connection tokens, one per end. Sample verified keys: `TEE|CI|DWV|SAN|SPIG|5|GSKT,GSKT,THRD,THRD,THRD|SERV|4|4|4|2|2`, `WYE|RED|PVC|SDR35|SPIG|3|GSKT,SOLVCEM,SOLVCEM|6|6|4`._

- [x] **Cut-row material expansion + TUBE-as-property — DONE 2026-07-09.** See the "Cut-row handling" section above.
- [x] **Bucket B expansion — DONE 2026-07-10.** 31,183 rows: single uniform token replicated to qty (`BW` → `BW,BW`), `lookup_key` rebuilt from segment columns. **Verify-first mechanic:** a row was only touched if the key recomposed from its columns byte-matched the stored key — 0 mismatches across all 31k, which also validated the LOCKED key recipe sheet-wide. Post-write re-profile: B=0, bucket A grew exactly +31,183. Backup: `output/backups/FinalMerged-r3_BACKUP_before_connexpand.xlsx`. Mechanics: Excel COM via PowerShell — closed-file guard, whole-column array read/write, save; workbook must be closed in Excel during the run.
- [x] **Bucket C expansion — RE-APPLIED (final) 2026-08-05.** 6,976 rows: the single size replicated to qty copies (`2` w/ `SOLVCEM,GSKT` → sizes `2|2`, key `…|SOLVCEM,GSKT|2|2`) — the pass that bakes in the per-end duplicate-size rule. Verify-first mechanic (only touch a row whose key recomposes byte-for-byte from its current columns): 0 key mismatches, 0 shape anomalies. Post-write re-profile: C=0, bucket A grew exactly +6,976. Written via **openpyxl** (not COM) — safe round-trip confirmed (1 structured table, 7 sheets, no charts/pivots/images/VBA). Backup: `output/backups/FinalMerged-r3_BACKUP_before_bucketC.xlsx`. (Earlier 2026-07-10 pass had been reverted to `before_sizeexpand`; this re-application supersedes it.)
- [ ] **⚠️ Flange normalization is NOT actually complete — FINISH IMMEDIATELY (flagged 2026-08-06).** Despite the "DONE 2026-07-10" write-up below, Steve found **a large number of flange (`FLG`) rows with incorrect / missing properties** — the `Prop*`/`Merged_Props` (flange subtype: WN, slip-on, blind, threaded, etc.) are not set correctly on many rows. This matters because properties feed the key, so those flanges will mis-key or miss on lookup. Steve's convention is he only marks flanges "done" once they're actually correct, so the presence of unfinished ones means the flange pass was left incomplete. **Action: go back through the FLG rows, set the correct properties on every one, then re-verify.** Until then, treat the flange connection/size normalization below as PARTIAL, not done — and the final key rebuild must NOT be run over flanges until their properties are corrected (it would freeze wrong keys).
- [~] **Flange connection + size normalization — connection/size portion applied 2026-07-10, but PROPERTIES INCOMPLETE (see the immediate to-do above).** Flanges (`NewComp = FLG`, ~25,937 rows) carried no `connection_qty`/`connection_type`, which broke the uniform key rule (every other fitting keys on its connection). Assigned connections from properties + `raw_header_path` (WN→`BW,BU`, slip-on/plate→`SOWLD,BU`, threaded/companion/union→`THRD,BU`, blind/Van Stone/glass/back-up→`1 BU`, solder/socket-fusion/epoxy/lokring/mechanical/electro-fusion per the `x <method>` in the header path; BU always last) — 20,441 rows, 0 unknown. Then broke out sizes per connection: single-size replicated (`6`→`6|6`); reducers (2 sizes, `size_1` the larger in 100% of 15,797) keyed **BU-first** so `BU` pairs with the larger size; 38 qty-1/2-size specials (blind-with-FPT-tap, socket-reducing-epoxy, reducing back-up-solder) promoted to qty 2 with the joint included — 23,595 rows. Every flange now has `connection_qty == n_sizes`; keys regenerated. Flange manhours are handling-only (C# synthesizes separate bolt-up + weld/joint rows). Backups: `before_flangeconn`, `before_sizebreakout`. The flange-type → connection-profile map is the seed of the AI **Component Reference Table**. Open: confirm scraped flange manhours are handling-only (double-count guard); slip-on modeled as single `SOWLD` + BU (physically 2 fillet welds — revisit).
- [x] **Bucket D expansion — DONE 2026-08-05.** 25,051 rows: replicated both the size and the token to qty (2" BW×BW item stored as one `2` + one `BW` → `2|2` + `BW,BW`). Same verify-first mechanic (0 mismatches); openpyxl write. Post-write re-profile: D=0, bucket A grew exactly +25,051. Backup: `output/backups/FinalMerged-r3_BACKUP_before_bucketD.xlsx`.
- [x] **Bucket F — 216 irregular rows — DONE 2026-08-05.** Handed the user a stable-keyed worklist (`output/irregular_rows_review.csv`, keyed by `rate_id` + `weblem_data_id`, NOT excel_row); the user corrected every qty (qty = n_sizes) and Steve's connection-vocab rules were applied (see auto-memory `project_mcaa_conn_vocab_rules`): **Spigot→`GSKT`** (a spigot is a fitting/end, not a conn type), **all threaded (FPT/MPT/Male)→`THRD`**, unnamed tap ends→`THRD`; plus per-description maps for cleanout/solder/solvent-cement mixed ends and a handful of rate-specific oddballs (LEAD/GSKT lead-joint hub fittings). Applied via `write_bucketF.py`. **Rows matched on `weblem_data_id` — the first run aborted (correctly, no write) on duplicate `rate_id`s (WALLBRK 2310/2312/2317/2330, REPAD 9350–9365), which is how we learned `rate_id` is NOT unique (auto-memory `project_ratesheet_rateid_not_unique`).** Post-write re-profile: F=0, bucket A grew exactly +216. Backup: `output/backups/FinalMerged-r3_BACKUP_before_bucketF.xlsx`.
- [x] **`length` column unit normalization — DONE 2026-08-06.** The `length` segment feeds a dedicated key slot, so unit inconsistency = silent misses. Profiling r3: 105,269 blank, 8,076 already unit-suffixed (`6IN`, `20FT`, …), **2,670 bare numerals** with no unit. Every bare-numeral row was **PIPE** (0 non-PIPE), so all 2,670 → **`FT`** appended (uppercase, matching the existing `20FT`/`6IN` convention; pipe joint-length rates are priced in feet — `20`/`21`/`40` are single/double random lengths, `1`–`19` per-foot rows). Verify-first mechanic (only touch a row whose key recomposes byte-for-byte from its columns): **2,670/2,670 matched, 0 mismatches**; `length` + `lookup_key` rewritten via openpyxl. Post-write: **0 bare numerals remain; 0 new key mismatches** (full-sheet recompose identical pre/post). C# must emit the same `FT`/`IN` convention in the `length` segment. Backup: `output/backups/FinalMerged-r3_BACKUP_before_lengthunits.xlsx`. The unit-suffixed `IN` values are other components (nipples etc.) that already carried units — untouched.
- [x] **PIPE/TUBE connection quantities — CONFIRMED blank (rule set 2026-08-06).** Exec rule: **all PIPE and TUBE rows carry NO connection quantity.** Profiling r3: all 5,801 PIPE + 250 TUBE rows already have blank `connection_qty` — nothing to change; the rule is satisfied. (The single joint-method token in `connection_type` — `BU`/`BW`/`BFUS` — is a *separate* column and those tokens remain; clearing them is a distinct decision not yet made.)
- [ ] **Full stored-order key rebuild (final pass — mechanical).** With sorting dropped (2026-07-10), the final pass is a straight rebuild of every key from its segment columns in **stored order** — no re-sort. Catches any untouched bucket-A reducer still carrying an old sorted key. (The ~1,400 rows that motivated the old re-sort were the false-duplicate symptom of sorting; not sorting resolves them.) ⚠️ **Rebuild keys IN EXCEL, never via an openpyxl static recompose.** `NewMaterial` (and possibly other segment cells) are live `=XLOOKUP` formulas; openpyxl can't evaluate formulas, so a Python recompose reads them as blank and drops tokens like the `CHRMOLY` in `TIG|CHRMOLY|2.25C1M|<size>`. (A 2026-08-06 openpyxl pass flagged 148 such `TIG`/WAM "mismatches" — that was a false alarm from the empty formula cache, NOT bad data; the stored keys were correct.) The safe rebuild is Steve's Excel workflow: repaste the TEXTJOIN formulas, let Excel compute (XLOOKUP resolves), then paste-as-values.
- [ ] **`xlsx → SQLite` exporter → `output/cdx_weblem_rates.db` (mechanical).** ⚠️ MUST key rows on `weblem_data_id`, NOT `rate_id` (rate_id has duplicates).

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
