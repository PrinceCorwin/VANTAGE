# MCAA-Codex AI Takeoff Plan

**Status:** Planning only. No VANTAGE app code, AWS resources, prompts, or Lambdas are to be edited until Steve explicitly approves implementation.
**Owner:** Steve Amalfitano / Codex
**Created:** 2026-08-09
**Working folder:** `Plans/codex_takeoff/`

---

## Goal

Build a second, independent MCAA AI Takeoff backend named **MCAA-Codex** so Steve can compare its output against Claude's current MCAA takeoff backend.

The VANTAGE Takeoff UI will eventually expose two MCAA choices:

- **MCAA-Claude** — the current Claude-maintained prompt/Lambda/model path.
- **MCAA-Codex** — a new Codex-maintained prompt/Lambda/model path.

The two paths must run side by side without overwriting each other's prompts, Lambdas, reference files, S3 keys, batch outputs, or deployment artifacts.

---

## Non-Negotiables

- Do not edit VANTAGE app code until Steve explicitly approves implementation.
- Do not modify Claude's current prompt, Lambdas, AWS resources, or deployment path.
- Keep Codex planning and working notes under `Plans/codex_takeoff/`.
- Treat `Plans/vantage_handoff/01_master_takeoff_instructions.md` as the current drawing-first weld-takeoff authority.
- Preserve the existing user-drawn crop-box config workflow unless testing proves it cannot support the Codex path.
- Prefer additive routing and new backend/profile configuration over disrupting existing Summit or MCAA-Claude behavior.
- Every AWS deployment must follow `Plans/claude-code-aws-deployment-guide.md`: capture before state, deploy, verify after state.

---

## Initial Model Choice

Use **OpenAI GPT-5.6 Sol on Amazon Bedrock** for the first MCAA-Codex version.

Rationale:

- It is the highest-reasoning OpenAI model tier currently documented for Amazon Bedrock.
- It supports image input and text output through the Bedrock Responses API.
- It is available in `us-east-1`, which matches the existing VANTAGE takeoff AWS region.
- It gives the comparison real model diversity against Claude's current path.

Fallback model for cost/latency experiments: **GPT-5.6 Terra**. Do not start with Terra; use it only after Sol establishes an accuracy baseline.

Before implementation, verify actual account access and quotas in the Summit AWS account. Documentation availability is not the same as enabled account access.

Reference docs checked during planning:

- `https://aws.amazon.com/about-aws/whats-new/2026/07/openai-gpt-sol-terra/`
- `https://aws.amazon.com/blogs/machine-learning/get-started-with-openai-gpt-5-6-sol-terra-and-luna-on-amazon-bedrock/`
- `https://docs.aws.amazon.com/bedrock/latest/userguide/model-cards-openai.html`

---

## Existing VANTAGE Takeoff Contract

Current app-side flow, observed read-only:

1. `Views/TakeoffView` loads crop configs from S3 and lets the user select drawings.
2. `Dialogs/ConfigCreatorWindow` creates/edits drawn crop boxes and saves `CropRegionConfig` JSON under `clients/{client}/{project}.json`.
3. `Services/AI/TakeoffSession` owns upload -> metadata -> Step Functions start -> polling -> completion.
4. `Services/AI/TakeoffService.UploadDrawingsAsync` uploads PDFs to the drawings bucket under the config-derived prefix.
5. `TakeoffService.WriteMetadataAsync` writes `batches/{batchId}/metadata.json`.
6. `TakeoffService.StartBatchAsync` starts one state machine ARN with input:

```json
{
  "config_path": "clients/client/project.json",
  "bucket": "<drawings-bucket>",
  "drawing_keys": ["client/project/drawing.pdf"],
  "rev_bubble_only": false
}
```

7. `TakeoffSession` polls Step Functions until terminal status.
8. `TakeoffService.DownloadExcelAsync` downloads `batches/{batchId}/output/takeoff_{batchId}.xlsx`.
9. `TakeoffPostProcessor.GenerateLaborAndSummary` reads the Lambda-produced `Material` tab and writes/refreshes `Labor`, `Summary`, missed-rate tabs, etc.
10. `ImportTakeoffDialog` imports from the post-processed `Labor` tab.

Planning implication: MCAA-Codex should preserve this app-visible contract where practical, especially the output workbook location and `Material` / `Labor` tab expectations.

---

## Reuse vs Divergence

### Reuse

- Existing Takeoff screen and selected-file workflow.
- Existing crop-box config format:
  - `client_id`
  - `project_id`
  - `client_name`
  - `project_name`
  - `bom_regions`
  - `title_block_regions`
- Existing S3 config convention under `clients/`.
- Existing batch metadata concept.
- Existing Previous Batches and download flow where possible.
- Existing post-processor and import flow if the Codex workbook can remain compatible.

### Diverge

- Separate prompt files.
- Separate Lambda source folders.
- Separate Step Functions state machine or equivalent backend route.
- Separate model invocation layer using Bedrock OpenAI Responses API.
- Separate output schema version in metadata and workbook.
- Separate diagnostic artifacts for connection-level audit and drawing evidence.

---

## C# Key Ownership

VANTAGE C# owns MCAA key composition and rate lookup. The AI and AWS Lambdas must not compose final `lookup_key` values and must not calculate manhours.

AI/Lambda output should provide extraction facts only:

- component candidates
- material candidates
- sizes in observed/canonical end order
- connection types in observed/canonical end order
- dedicated key-segment facts such as `Reducing`, `pressure_rating`, `class_rating`, `schedule`, `weight_class`, and `length`
- candidate properties with evidence
- confidence/review flags

C# then:

- normalizes component/material/connection/property aliases
- filters candidate properties against the MCAA reference data
- rejects or flags irrelevant properties
- fills `Prop1` through `Prop6`
- sorts only the accepted properties A-to-Z into `Merged_Props`
- preserves stored/canonical order for sizes and connection tokens
- composes the byte-identical `lookup_key`
- looks up MCAA manhours
- writes final `Material`, `Labor`, missed-rate, and review tabs

Canonical key rules from `Plans/MCAA_Key_Composition.md`:

- Segment order: `NewComp`, `Reducing`, `NewMaterial`, `Merged_Props`, `pressure_rating`, `class_rating`, `schedule`, `weight_class`, `length`, `connection_type`, then `size_1` through `size_7`.
- `connection_qty` is retained for validation/reference but is not in the key.
- `connection_type` sits immediately before the sizes.
- Sizes and connection tokens are not sorted. Stored/canonical order carries meaning and must be preserved.
- `Merged_Props` is the only sorted segment.
- `length` must carry uppercase `FT` or `IN` units.

The practical prompt rule: ask the model for candidate properties and evidence, not for keys.

---

## Proposed AWS Resource Shape

Names are placeholders until implementation approval.

### Preferred isolation

- State machine: `mcaa-codex-takeoff-orchestrator`
- Extraction Lambda: `mcaa-codex-takeoff-extract`
- Aggregation Lambda: `mcaa-codex-takeoff-aggregate`
- Prompt/config keys in existing config bucket under a Codex prefix:
  - `codex/extraction_prompt.txt`
  - `codex/connection_schema.json`
  - `codex/reference_symbols/...`
- Batch output either:
  - same processing bucket with `backend=mcaa-codex` metadata and compatible output path, or
  - Codex-prefixed batch keys if VANTAGE routing is updated to know the backend.

### Avoid for v1

- Reusing Claude's prompt key.
- Reusing Claude's Lambda names.
- Mutating the existing Step Functions state machine in place.
- Creating separate crop-box configs that force users to redraw BOM/title-block boxes.

---

## Pipeline Design

### Stage 1: Intake

- Receive same state-machine input shape as current VANTAGE when possible.
- Load `CropRegionConfig` from S3.
- Download source drawing PDF(s).
- Render high-DPI page images.
- Crop BOM and title-block regions using existing percentages.

### Stage 2: Drawing-First Connection Detection

- Analyze the full isometric or tiled high-DPI crops for physical connection symbols/nodes.
- Do not rely on weld numbers.
- Classify BW/SW/BU from symbol evidence.
- Use BOM and title-block crops to validate component identity, size, material, and reductions.
- Flag uncertainty instead of forcing a confident answer.

### Stage 3: Connection-Level Audit

Produce one row per physical connection before any rollup:

| Field | Purpose |
|---|---|
| `drawing_number` | Drawing identifier |
| `connection_id` | Codex internal id, not weld balloon number |
| `connection_type` | BW, SW, BU, threaded, unknown, etc. |
| `include_in_round1` | true only for BW/SW in initial scope |
| `size` | Nominal weld size |
| `material` | Construction material |
| `upstream_item` | Evidence of one side |
| `downstream_item` | Evidence of other side |
| `drawing_evidence` | Symbol/geometry explanation |
| `bom_evidence` | BOM/title block evidence |
| `confidence` | high / medium / low |
| `page` | PDF page |
| `bbox` | Optional drawing coordinate box for future UI review |
| `needs_review` | Boolean |

### Stage 4: Stock-Length Additions

- Apply after drawn-node count.
- SS stock length = 20 ft.
- CS stock length = 40 ft.
- Added BW joints need no coupling.
- Added SW joints need a same-size/material coupling.
- Keep stock additions distinct from drawn nodes in the audit.

### Stage 5: Workbook Output

For compatibility, v1 should still produce `takeoff_{batchId}.xlsx`.

Target tabs:

- `Summary` — C# generated from validated Material/Labor rows.
- `Material` — C# generated from AI/Lambda extraction facts plus MCAA reference validation.
- `Labor` — C# generated from validated Material rows, connection rules, stock-length rules, and MCAA rate lookup.
- `Codex Audit` — connection-level audit with evidence/confidence.
- `Codex Review` — low-confidence and conflict rows.
- `Rejected Properties` — candidate properties observed by the AI but not accepted into the MCAA key inputs.
- `Failed DWGs` — same concept as current aggregation output.

Planning decision: MCAA-Codex should not have the Lambda produce final rated `Labor` rows. The Lambda should produce structured extraction facts. VANTAGE C# should be the compiler that turns those facts into final `Material` and `Labor` tabs. This keeps key composition, property sorting, rate lookup, and manhour calculation in the app where the MCAA contract belongs.

---

## VANTAGE UI Routing Plan

Future UI should not be a simple `Summit/MCAA` binary.

Recommended future shape:

- Keep Summit path intact until retired.
- Replace current MCAA radio with backend-specific options:
  - `MCAA-Claude`
  - `MCAA-Codex`
- Store the selected backend in a new setting, likely separate from rate pricing:
  - `Takeoff.McaaBackend = Claude | Codex`

Reason: "rate mode" and "AI backend" are different concepts. MCAA-Claude and MCAA-Codex are both MCAA pricing/extraction experiments, but they route to different AWS resources and output schemas.

Potential future split:

- `Takeoff.RateMode = Summit | MCAA`
- `Takeoff.McaaBackend = Claude | Codex`

Implementation should avoid breaking existing saved `Takeoff.RateMode` values.

---

## Regression and Comparison Plan

Use the reference drawings under `Plans/vantage_handoff/drawings/` as the first regression set.

Expected round-one results from `Plans/vantage_handoff/00_HANDOFF_README.md`:

| Drawing | Expected result |
|---|---|
| LP1Y-MEOH-111304-21 | 9 x 4" BW including stock additions + 3 x 1" BW = 12 |
| LP1Y-PWP-014002-16 | 11 x 6" BW = 11 |
| LP1Y-STORMP-100412-01 | 1 x 4" BW + 19 x 2" BW + 3 x 3/4" BW + 6 x 3/4" SW = 29 |
| LP1Y-N2-107418-01 | 5 x 1" BW + 4 x 1/2" SW = 9 |
| LP1Y-N2-176304-01 | 4 x 1" BW + 3 x 1/2" BW = 7 |

Comparison dimensions:

- Correct total BW/SW count.
- Correct split by size.
- Correct material.
- No dependence on weld numbers.
- No fitting-end double counting.
- No phantom pipe-to-pipe weld at branch nodes.
- Correct swage large/small end behavior.
- Correct branch/olet size rule.
- Correct stock-length additions.
- Reviewability: can Steve see why each connection was counted?
- Token/cost/latency per drawing.

---

## Prompt Strategy

Use `Plans/vantage_handoff/01_master_takeoff_instructions.md` as the core operating spec, but convert it into a production prompt with:

- Strict JSON output schema.
- Explicit "no weld-number dependency" rule.
- Evidence-required fields.
- Confidence labels.
- "Uncertain instead of invented" rule.
- Few-shot examples from the five regression drawings.
- Separate sections for drawn-node audit and stock-length additions.

The prompt should require the model to return connection-level audit rows first, then rollups.

---

## Lambda Strategy

### Extraction Lambda

Responsibilities:

- Download PDF and config.
- Render full page and crops.
- Build model request payloads.
- Call GPT-5.6 Sol via Bedrock Responses API.
- Validate JSON response against schema.
- Save per-drawing extraction JSON to S3.
- Save debug thumbnails/crops only if enabled.

### Aggregation Lambda

Responsibilities:

- Read all per-drawing JSON outputs.
- Combine connection audits.
- Build an extraction workbook or JSON package for C# consumption.
- Produce `Failed DWGs` for failures.
- Preserve enough intermediate evidence for review and debugging.
- Do not compose final `lookup_key` values.
- Do not calculate manhours.

### Possible later split

If single-call full-drawing reasoning is too expensive or unreliable, split into:

1. Symbol/node candidate detection over high-DPI tiles.
2. Per-candidate classification from local crops.
3. BOM/title-block reconciliation.
4. Aggregation/stock-length pass.

Do not start with that complexity unless the direct approach fails on regression drawings.

---

## Open Questions

1. Should Codex use a separate processing bucket or share the current one with backend-tagged metadata?
2. What exact C# compiler shape should create final MCAA `Material` and `Labor`: extend `TakeoffPostProcessor`, add a side-by-side `McaaCodexPostProcessor`, or build a shared MCAA material/labor service?
3. How much coordinate/bounding-box precision is practical from GPT-5.6 Sol on rendered images?
4. Should low-confidence rows block import, or only surface in a review tab?
5. Should MCAA-Codex use the existing drawing cleanup behavior that deletes uploaded PDFs after completion?
6. Where should model cost/latency metrics be stored: batch metadata, workbook Summary, or both?
7. Should the first implementation be CLI/Lambda-only before touching the VANTAGE UI?

---

## Proposed Implementation Phases

### Phase 0: Planning and Read-Only Inventory

- Finish documenting existing app contracts.
- Read current Claude AWS source files from the NAS path for compatibility only.
- Verify Bedrock GPT-5.6 account access and quotas.
- Decide AWS resource names and isolation strategy.

### Phase 1: Offline Prototype

- Build a local proof harness under Codex working materials, not VANTAGE app code.
- Render the five reference PDFs.
- Call GPT-5.6 Sol on one drawing at a time.
- Iterate prompt/schema until the audit rows are stable.
- Compare against known expected totals.

### Phase 2: AWS Prototype

- Create Codex-specific prompt/config keys.
- Create Codex extraction and aggregation Lambda sources.
- Deploy to separate AWS resources.
- Run the five reference drawings through AWS.
- Verify every deploy with SHA/head-object checks.

### Phase 3: Output Compatibility

- Produce Excel compatible with the current download/import workflow.
- Decide whether to bypass or reuse `TakeoffPostProcessor`.
- Add audit/review tabs.
- Compare output against Claude path.

### Phase 4: VANTAGE UI Integration

- Add backend selection in Takeoff UI.
- Add credential/config support for backend-specific state machine ARNs or routes.
- Preserve existing saved settings.
- Build and wait for Steve's Visual Studio validation.

### Phase 5: Broader Regression

- Run both MCAA-Claude and MCAA-Codex on a larger drawing set.
- Track misses and edge cases under `Plans/codex_takeoff/`.
- Promote any validated shared rules into the authoritative handoff/spec docs only after Steve agrees.

---

## Documentation Rules for Codex Work

Codex-specific planning, scratch notes, schemas, and comparison reports stay under `Plans/codex_takeoff/`.

Shared project docs are updated only when work is validated or a decision becomes authoritative:

- `Plans/Project_Status.md`
- `Plans/Completed_Work.md`
- `Plans/Decisions.md`
- `Help/manual.html` for user-visible behavior

Until Steve confirms testing, keep Codex findings as planning or experiment notes, not project-complete claims.

