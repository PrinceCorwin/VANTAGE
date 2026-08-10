# MCAA-Codex Greenfield Takeoff Design

**Status:** Greenfield design draft. This is intentionally written from first principles and should be reviewed before using previous-version prompt/Lambda files as compatibility references.
**Owner:** Steve Amalfitano / Codex
**Created:** 2026-08-09
**Folder:** `Plans/codex_takeoff/`

---

## Objective

Given one or more piping isometric drawings, produce the most accurate practical **Material** and **Labor** takeoff for MCAA pricing, with enough evidence and review data for Steve to understand and correct questionable rows.

The AI does not price work. The AI reads drawings and extracts facts. VANTAGE C# validates those facts, applies the MCAA reference contract, composes keys, looks up manhours, and writes final output.

---

## Design Principle

Treat the system like a compiler pipeline:

1. **Inputs** are drawings, crop regions, optional specs, and MCAA reference data.
2. **AI extraction** converts visual/text evidence into structured candidate facts.
3. **C# semantic validation** normalizes and accepts/rejects those facts.
4. **C# compilation** produces final MCAA `Material` and `Labor` rows.
5. **Review output** exposes every uncertainty, conflict, and rejected candidate.

This gives the model room to read messy drawings while keeping pricing deterministic.

---

## Current Required Inputs

### Drawing package

- Original piping isometric PDF.
- One drawing per PDF remains preferred unless VANTAGE later adds page splitting.

### Config regions

The future config should contain:

- **BOM regions:** one or more boxes.
- **Title block regions:** one or more boxes.
- **Drawing body region:** exactly one box.

BOM/title regions can remain multi-box because drawings split these areas in different ways. Drawing body should be one region so the model has a coherent view of piping geometry and symbols.

### MCAA reference data

The C# side needs a reference database derived from the completed MCAA rate-sheet export. It should be versioned independently from the app and should match the current key recipe in `Plans/MCAA_Key_Composition.md`.

### Optional future project profile

A project profile can include:

- uploaded specs
- pipe-class / line-class tables
- project-specific material aliases
- project-specific component aliases
- stock-length rules
- client-specific drawing conventions
- known exceptions

Specs are valuable, but they should be converted into structured rules first. Do not send hundreds of raw spec pages to the model on every drawing unless there is no better choice.

---

## Source Priority

Use context-based inference, but make the source explicit.

Priority for accepting a field:

1. Explicit BOM row text.
2. Explicit title-block / line data.
3. Drawing-body symbol or geometry.
4. Structured project spec/profile rule.
5. MCAA reference default.
6. Unknown / needs review.

If sources conflict, preserve the conflict and surface it in review output. A spec rule can fill blanks, but it should not silently override clear drawing/BOM evidence.

Example:

- BOM says `BALL VALVE 1000# CWP BW`.
- AI may return `class_rating = 1000`, source `bom`, confidence `high`.

Example:

- BOM says `PIPE`, title block says `PIPE SPEC: 316L SS SCH 10S`.
- AI may return `schedule = 10S`, source `title_block`, confidence `high`.

Example:

- Drawing symbol clearly shows socket ticks, but BOM abbreviation implies beveled.
- AI should report the conflict. C# or review workflow decides whether the drawing symbol, BOM, or project rule wins.

---

## AI Responsibilities

The AI extracts candidate facts with evidence. It should never emit final lookup keys or manhours.

### Per drawing

Return:

- drawing identity
- title-block fields
- raw BOM rows
- interpreted material item candidates
- drawing-body connection observations
- candidate properties
- uncertainties/conflicts
- usage metadata if available

### Per BOM/material candidate

Return:

- source BOM item id
- raw size text exactly as printed
- raw description exactly as printed
- raw quantity exactly as printed
- component candidate(s)
- material candidate(s)
- size/end candidates in observed order
- connection candidate(s) in observed order
- dedicated key-segment candidates:
  - `Reducing`
  - `pressure_rating`
  - `class_rating`
  - `schedule`
  - `weight_class`
  - `length`
- candidate properties with evidence
- source for each field: `bom`, `title_block`, `drawing_body`, `project_spec`, `reference_default`, or `unknown`
- confidence per field
- row-level confidence
- review flags

### Per drawing-body observation

Return:

- observation id
- observed symbol type
- likely connection type
- size/material context if visible
- what appears connected
- evidence text
- confidence
- page number
- bounding box or approximate coordinates if possible

The drawing-body pass does not replace BOM extraction. It validates or supplements material rows and labor-generation assumptions.

---

## C# Responsibilities

C# is the authority for:

- alias normalization
- accepted component/material/connection/property selection
- invalid property rejection
- property ordering
- `Merged_Props`
- `lookup_key`
- rate lookup
- manhour calculation
- final `Material` tab
- final `Labor` tab
- review tabs

The AI should be thought of as a witness. C# is the judge and compiler.

---

## Key Composition Boundary

`Plans/MCAA_Key_Composition.md` is canonical.

C# must implement:

- Key segment order:
  `NewComp`, `Reducing`, `NewMaterial`, `Merged_Props`, `pressure_rating`, `class_rating`, `schedule`, `weight_class`, `length`, `connection_type`, `size_1` through `size_7`
- `connection_qty` retained for validation/reference only, not keyed.
- `connection_type` immediately before sizes.
- Sizes and connection tokens stay in stored/canonical order.
- `Merged_Props` is the only sorted segment.
- Accepted properties sort A-to-Z before being pipe-joined into `Merged_Props`.
- Blank segments are skipped.
- `length` uses uppercase `FT` / `IN` units.

AI output must therefore provide candidate fields and evidence, not the key.

---

## MCAA Reference Database

Use SQLite unless a later reason forces something heavier.

The DB should support both Lambda/Codex prompting and C# validation. It should be generated from the completed MCAA rate-sheet export plus curated alias tables.

### Core tables

`rates`

- `weblem_data_id`
- `lookup_key`
- `new_comp`
- `new_material`
- `merged_props`
- dedicated key fields
- connection fields
- size fields
- `manhours`
- source metadata

`components`

- `component_code`
- display name
- aliases
- category
- notes

`materials`

- `material_code`
- display name
- aliases
- family/group

`connections`

- connection code
- aliases
- description
- whether it is a weld/joint/bolt-up/thread/prep operation

`properties`

- property code
- display name
- aliases
- notes

`component_material_properties`

- component code
- material code or wildcard
- allowed property code
- source count / provenance

`component_connection_profiles`

- component code
- subtype/property when needed
- expected connection count
- expected connection tokens
- size role rules
- notes

`property_aliases`

- raw text pattern or phrase
- normalized property code
- confidence/default priority

`spec_rules` later

- project id
- line class / pipe spec
- inferred material/schedule/class/etc.
- rule source
- precedence

### Why this DB exists

The model should not search the entire MCAA universe for every item. The DB lets the pipeline hand the model a compact relevant vocabulary and lets C# reject properties that do not belong to a component/material combination.

---

## Property Extraction Strategy

Use a hybrid approach.

The model may report every property-like candidate it sees, but C# decides what becomes a key input.

For each candidate property, AI returns:

- raw text
- normalized guess
- source
- evidence
- confidence

C# then:

1. maps aliases to MCAA property codes
2. filters against allowed properties for the accepted component/material
3. accepts valid properties
4. writes invalid/extraneous candidates to review
5. fills `Prop1..Prop6`
6. sorts accepted props A-to-Z into `Merged_Props`

Rejected properties should not silently disappear during early development. They are useful feedback for fixing prompts, aliases, or the rate-sheet reference tables.

---

## Component and Material Selection

The model should be allowed to return candidates rather than a single forced answer.

Example:

```json
{
  "component_candidates": [
    {
      "code": "FLG",
      "confidence": "high",
      "evidence": "description contains FLANGE WN"
    }
  ],
  "material_candidates": [
    {
      "code": "SS",
      "confidence": "high",
      "evidence": "description contains 316L SS"
    }
  ]
}
```

C# can accept the highest-confidence candidate when it passes reference validation, or flag multiple plausible candidates for review.

---

## Connection and Size Order

Stored/canonical order matters for MCAA keys. Do not sort sizes or connection tokens.

The AI should return observed order plus reasoning:

```json
{
  "connections": [
    {
      "position": 1,
      "connection_type": "BW",
      "size": "2",
      "source": "bom",
      "evidence": "2X1 reducer, larger end"
    },
    {
      "position": 2,
      "connection_type": "SW",
      "size": "1",
      "source": "drawing_body",
      "evidence": "small end lands on socket-weld elbow"
    }
  ]
}
```

C# can then write `connection_type` and `size_1..size_7` in the accepted canonical order.

Open design item: define canonical order rules per component family where drawing/BOM order is ambiguous. Flanges and reducers already have known special cases in the MCAA docs.

---

## Drawing Body Use

The drawing body region should be used for:

- validating connection types
- detecting fitting-to-fitting vs pipe-to-fitting joints
- identifying branch/olet behavior
- catching pipe-to-pipe welds
- checking stock-length additions
- resolving swage/reducer end behavior
- finding conflicts between BOM assumptions and drawn symbols

For full MCAA material/labor, the drawing body is supporting evidence and labor topology input. It does not replace BOM row extraction.

---

## Spec/Profile Use

Specs can improve accuracy, but only if converted into structured rules.

Recommended later workflow:

1. User uploads project specs / pipe class docs.
2. A one-time spec ingestion process extracts structured rules.
3. Steve reviews/approves the rules.
4. Takeoff runs use the structured rules as project context.

Do not make v1 dependent on full spec ingestion. Start with BOM/title/drawing-body + MCAA reference DB. Add specs after the base pipeline works.

---

## AI Input Package

For each drawing, send:

- BOM crop image(s)
- title-block crop image(s)
- drawing-body crop image
- OCR/table text when available
- compact component vocabulary
- compact material vocabulary
- compact connection vocabulary
- allowed property hints when a component/material candidate is already likely
- project profile rules if available
- strict JSON schema

The input package should be compact. Do not send the full MCAA rate sheet to the model.

---

## JSON Output Shape

High-level structure:

```json
{
  "schema": "mcaa-codex-extraction-v1",
  "drawing_number": "string",
  "title_block": {},
  "bom_items": [],
  "drawing_observations": [],
  "conflicts": [],
  "extraction_notes": [],
  "model_metadata": {}
}
```

Each `bom_items[]` row should include raw fields, candidates, sources, evidence, and confidence. Exact schema will be drafted after r3 and drawing-body config are ready.

---

## Workbook Output

C# should write or finalize:

- `Summary`
- `Material`
- `Labor`
- `Codex Audit`
- `Rejected Properties`
- `Conflicts`
- `Failed DWGs`
- `Model Metadata`

The `Material` and `Labor` tabs should be import-compatible with VANTAGE workflows. Review tabs can evolve quickly during development.

---

## Review Philosophy

Early versions should over-report review data rather than hide it.

Review surfaces:

- rejected properties
- unrecognized component aliases
- material ambiguity
- connection-order uncertainty
- drawing/body vs BOM conflicts
- spec/profile conflicts
- low-confidence extracted fields
- missed MCAA rates

Once the pipeline is proven, some review tabs can be hidden or collapsed.

---

## Validation Layers

Track failures by layer:

1. **Extraction failure:** AI missed or misread drawing/BOM facts.
2. **Normalization failure:** aliases or formats did not map.
3. **Reference failure:** extracted property/component/material is not allowed by reference data.
4. **Key failure:** C# composed a key that does not exist in the rate DB.
5. **Rate coverage failure:** rate sheet lacks a needed row.
6. **Labor synthesis failure:** material facts were right but generated labor rows were wrong.

This prevents false blame. A wrong MH total may be a rate-sheet gap, not an AI failure.

---

## First Comparison Target

Until r3 and the drawing-body config are ready, exact MH totals are not the first gate.

Initial gate:

- Material row shape is correct.
- Needed MCAA fields are present or transparently flagged.
- Candidate properties are reviewable.
- Accepted/rejected property behavior is explainable.
- C# can compose keys deterministically from accepted facts.

Later gate:

- Exact key hit rate.
- Exact Labor rows.
- Exact MH totals.

---

## Dependencies Before Implementation

Blocked until:

- Steve completes the r3 MCAA rate-sheet/doc state.
- Drawing-body config box exists in the config process.
- We agree on the initial MCAA reference DB tables.
- We agree whether v1 consumes specs or defers specs to v2.

Useful planning work before unblock:

- Draft JSON schema.
- Draft reference DB schema.
- Draft C# compiler boundary.
- Draft review tab definitions.
- Draft test checklist for the five regression drawings plus one real MCAA material/labor drawing set.

---

## Implementation Philosophy

Build outside-in:

1. Prove extraction schema on reference drawings.
2. Build C# validation/key compiler against known sample rows.
3. Add AWS only after the local contracts are stable.
4. Add VANTAGE UI routing after backend output is useful.

This avoids wiring a shiny button to an unstable engine.

