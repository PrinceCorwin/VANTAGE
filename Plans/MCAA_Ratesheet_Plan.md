## MCAA Ratesheet Plan

**Owner:** Steve Amalfitano
**Status:** In progress
**Producer project (external, NOT in this repo):** `C:\Users\Steve.Amalfitano\source\repos\PrinceCorwin\SkySkraper\SynologyDrive`
**Phase 1 (toggle infrastructure) shipped 2026-05-05** — UserSetting `Takeoff.RateMode`, radio group on `Views/TakeoffView.xaml` (gated to allowlisted users for MCAA), mode-conditional gating of four behaviors in `TakeoffPostProcessor` (BOLT/GSKT/WAS skip, SPL skip, CUT companion, modifier neutralization), uniform ShopField rule, mode written to Summary tab. Phase 2 = the work in this plan.

---

## High-level plan

1. **Fork AI Takeoff for MCAA.** Three new files — MCAA prompt, MCAA extraction Lambda, MCAA aggregation Lambda — created by **copying the current Summit files as the starting point**, then modifying only the OUTPUT side (extracted properties, ref vocabularies, JSON schema). Summit's three files stay frozen. Ref sheets themselves (`mcaa-CompRefTable`, `mcaa-ConnRefTable`, `mcaa-MatRefTable`, `mcaa-BodyTypeRefTable`) are net-new MCAA artifacts, not copies.
   - **MCAA file locations** (under `%USERPROFILE%\Documents\WorkFromNAS\SynologyDrive\Conversion\`, originally copied 2026-05-10; relocated from Google Drive to NAS sync 2026-05-17):
     - `mcaa-takeoff-poc\extraction_prompt.txt` (copy of Summit `summit-takeoff-poc\extraction_prompt.txt`)
     - `mcaa-takeoff-poc\lambda_function.py` (copy of Summit `summit-takeoff-poc\lambda_function.py`)
     - `mcaa-aggregate-deploy\lambda_function.py` (copy of Summit `aggregate-deploy\lambda_function.py`)

2. **Per-property AI-facing reference sheets in S3** for the MCAA prompt. Four focused tables so the AI does multiple cleaner discriminations against small vocabularies instead of one combinatorial decision against a Cartesian product:
   - **mcaa-CompRefTable** — components only (Description + AlternateDescriptions + Component). ConnType dropped from the Summit seed; it now lives in mcaa-ConnRefTable.
   - **mcaa-ConnRefTable** — connection patterns (Description + AlternateDescriptions + ConnType). ConnType is a comma-separated pattern stored verbatim: `"BW"`, `"BW,BW"`, `"BW,BW,BW"`, `"BW,PRESSFIT,BW"`, `"BW,BW,SCRD"` for a BW-run SCRD-branch tee, etc. C# parses the comma list to generate one labor row per element; ConnectionQty is implicit from list length.
   - **mcaa-MatRefTable** — material + grade combined into single rows (Description + AlternateDescriptions + Material + Grade). Plain `STAINLESS STEEL` and `SANITARY FOOD GRADE STAINLESS STEEL` are SEPARATE rows; AI picks the more specific match.
   - **mcaa-BodyTypeRefTable** — body type only (Description + AlternateDescriptions + BodyType). Kept separate from MatRefTable because body type is orthogonal to material/grade — merging would re-introduce a Cartesian explosion.

   Size is a direct BOM-string capture — no ref sheet.

3. **MCAA AI extracts per BOM item:** component, material, matl_grade, body_type, size_string, connection_type. Material and matl_grade come from a single match against mcaa-MatRefTable (the matched row supplies both values). connection_type is the comma-separated pattern verbatim from the matched mcaa-ConnRefTable row; C# parses the list at labor-generation time to produce one labor row per element. JSON schema in Lambda output keeps these as separate fields.

4. **MCAA aggregation Lambda** emits those columns in the Excel output.

5. **C# composes the lookup key** per labor row from those columns in the mapped order; size normalized in code.

6. **C# does exact key match** against the MCAA rate sheet (local SQLite shipped with VANTAGE). Misses go to a missed-rates tab; fallback ladder tuned in testing.

7. **C# routes by rate mode at takeoff time** — MCAA Lambda ARN when MCAA selected, Summit ARN otherwise.

8. **Action/connection labor rows** (welds, cuts, bevels, hydrotest) are C#-synthesized from BOM connections, inheriting parent-item properties, using the same key recipe.

**Cross-cutting integration contract:** the MCAA rate sheet (built by SkySkraper) must use byte-identical key composition with what C# composes.

**Cross-cutting input-side invariant (Summit ↔ MCAA):** BOM detection, title-block detection, drawn-boxes region handling, and all input-side extraction language MUST stay functionally identical between the two prompts. Existing user-defined drawn-boxes configs must continue to work under MCAA mode without re-configuration. Only the OUTPUT side (extracted properties, ref vocabularies, JSON schema) diverges.

---

## Development sequence

1. **Pick a small reference drawing** — ~10–20 BOM items, all everyday CS/SS work, exercising each extracted property at least once (matl_grade, body_type, multiple connection types, multi-size items). Outliers (sanitation / food-quality grades, 9-chrome, Hastelloy, belled end, etc.) are deferred to ongoing rate-sheet expansion — not part of the initial reference slice.
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

- **Letter-case normalization in body_type (and other) vocabularies.** Sample values mix all-caps (`BELL`, `DWV`, `COMPAIR`, `VACC`) with title case (`Soft`). Pick one casing rule, apply during rate-sheet cleanup, have C# normalize the same way before key composition. Otherwise `Soft` vs `SOFT` is a silent miss. — Bring up when designing the C# key composer or when finalizing any MCAA ref vocabulary.
- **Bare-numeral body types (`45`, `90`).** Confirm during rate-sheet cleanup that no MCAA component code is the bare numeral standalone (so namespaces stay disjoint). — Bring up when scrubbing the MCAA component vocabulary.
- **Empty-field representation in the composed key.** matl_grade absent on plain CS pipe, body_type unknown on a generic reducer — what value goes in the key? Empty string vs literal `NONE` vs collapsed slot. Pick one rule, bake into both the rate-sheet key column and C#'s composer. — Bring up when designing the C# key composer.
- **Fallback ladder design for missed lookups.** Order in which properties get dropped from the key when an exact match fails (analogous to Summit's "thickness as-is → toggle leading S → class rating → size-only" ladder). Tune in testing once we see what actually misses. — Bring up after the first MCAA-mode takeoff is run against the rate sheet.
- **Missed-rate triage workflow.** Operational path when a key misses — manual entry, suggested coarser fallback, rate-sheet update. — Bring up when designing the missed-rates tab UX.
- **AI accuracy on matl_grade and body_type.** These fields are inherently noisy (grade hides in long descriptions; reducers often don't state body type in the BOM). Plan for higher initial miss rate during early rollout. — Bring up when designing missed-rate triage and during the first parity tests.
- **Connection-type abbreviation vocabulary contract.** Producer rate sheet `connection_type` column has to match byte-for-byte what C# writes into the key. Same shape of risk as the component-abbreviation contract. — Bring up when finalizing the MCAA `connection_type` vocabulary on the producer side.
- **Disambiguation between connection_type and body_type abbreviations.** User decision: solve at the data layer by making namespaces disjoint (body_type vocabulary will not reuse connection-type abbreviations like `BW`). — Bring up when the body_type vocabulary is being finalized on the producer side.
- **Allow AI to emit blanks on component/material when uncertain?** Previously attempted — AI over-used blanks as an easy escape hatch. There may be a middle ground (low-confidence flag, threshold-gated blanks, "force-pick from CompRefTable" rule, etc.). — Bring up when designing the missed-rate triage workflow / Material-tab user-correction UX.
- **"Most-specific match wins" rule for combined mcaa-MatRefTable.** Without an explicit instruction, AI could match generic `STAINLESS STEEL` to a sanitary-food-grade BOM line because the words "STAINLESS STEEL" appear in both. Either lock this in the prompt as an explicit rule or rely on rich AlternateDescriptions to nudge the AI toward the more specific row (or both). — Bring up when iterating the prompt against the reference drawing.
- **Learned-corrections DB for items with missing/wrong AI-extracted properties.** Local SQLite table keyed by commodity/item code (primary, when present) and exact normalized BOM description (fallback). After takeoff returns, C# applies saved corrections to a row's properties BEFORE the rate-lookup key is composed, so the user doesn't re-correct the same items every project. Compounds value over time. Doubles as a feedback signal — frequent identical corrections suggest the rate sheet needs an alias or the prompt needs tuning. Open design questions: (a) per-project vs global scope, (b) auto-save every Material-tab edit vs explicit "save correction" button, (c) audit column in Excel marking corrected-from-DB vs AI-extracted rows so debugging stays transparent. Matching strictness is the central design risk — too loose = silent misapplication to a different item; too strict = rarely fires and user re-corrects forever. — Bring up after the missed-rate triage workflow is in place and Material-tab corrections start happening at volume.

---

Detail gets added section by section as each step is worked.
