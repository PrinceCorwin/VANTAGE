# AWS Agent — Change Log

Tracks every edit made to the files in `Plans/AWS Agent/`:

- `extraction_prompt.txt` — system prompt for the Bedrock extraction model
- `extraction_lambda_function.py` — per-drawing extraction Lambda
- `aggregation_lambda_function.py` — batch aggregation + Excel generation Lambda

Entries are append-only. Newest at top. Each entry records: date, file(s) touched, what changed, and why.

---

## 2026-04-22

### `extraction_prompt.txt` — align OUTPUT FORMAT schema with extraction instructions

- **What:** Updated the `size` (line 145) and `connection_size` (line 151) example values in the `## OUTPUT FORMAT` JSON schema. Old examples showed decimal format (`0.5`, `1.5`, `6`, `6x4`); new examples show raw fractional format (`"3/4"`, `"1-1/2"`, `"6x4"`).
- **Why:** The prompt body (line 28, line 98) instructs Claude to return sizes *exactly as printed on the drawing*, with fractions preserved. The schema examples contradicted that by showing decimal values, which pulled the model in two directions. The fractional behavior is the intended one because the aggregation lambda's `normalize_size()` does the fraction-to-decimal conversion downstream. Only the schema examples were out of date.

### `aggregation_lambda_function.py` — harden `normalize_single_size()` against input drift

- **What:** Added pre-normalization in `normalize_single_size()` to handle variations that previously passed through unchanged:
  - Unicode fraction characters (`½`, `¼`, `¾`, `⅛`, `⅜`, `⅝`, `⅞`) → converted to ASCII `1/2`, `1/4`, etc.
  - En-dash (`–`) and em-dash (`—`) → converted to ASCII hyphen.
  - Spaces around hyphens (`1 - 1/2`, `1- 1/2`) → collapsed to `1-1/2`.
  - Multiple whitespace runs (`1  1/2`) → collapsed to single space.
  - Trailing `'` / `"` marks → stripped.
  Also added a `logger.warning("SIZE_NORMALIZATION_FAILED: ...")` when a size cannot be converted, so any future drift in Claude's output shows up in CloudWatch instead of silently becoming `0` in the C# app.
- **Why:** The existing regex matched `1-1/2` and `1 1/2` but failed silently on common edge-case variants. When normalization failed, the app's `FittingMakeupService.GetDouble()` returned `0`, which propagated into pipe matching, rate lookups, and connection explosion without any signal that anything was wrong.

### No C# changes — post-processing contract preserved

- Verified that `FittingMakeupService.GetDouble` / `ParseDualSize` consume decimal strings only. Because `normalize_size()` still produces decimals at the Excel write boundary, the C# side sees the same contract it always has. The lambda changes reduce silent-zero cases but do not alter the shape or type of the Excel output.

### `extraction_prompt.txt` — return null for unmatched material instead of defaulting to CS

- **What:** In the Material Group Matching Rule (line 107), changed the fallback from "default to matl_grp: 'CS' and matl_grp_desc: 'CARBON STL'" to "return matl_grp: null and matl_grp_desc: null". Schema entries at the bottom of the prompt updated to `"string or null"` to reflect that nulls are now valid. Also appended "Return null if no match is found" to the short field descriptions at the top of the file (lines 44-45) so the field list, the detailed rule, and the schema all agree.
- **Why:** The old CS default hid two problems at once. (1) For FS items whose descriptions don't mention material, the app's `CorrectFsMaterial` was always going to overwrite whatever the Lambda returned anyway — so the CS guess did no work. (2) For non-FS items whose material was exotic or non-standard (outside the reference table), Claude would silently return CS and the app would never correct it — the row would flow into rate calculations with a wrong material multiplier, visible to no one. Returning null puts "I don't know" in front of the reviewer as an empty cell on the Material tab for non-FS items, while FS items still get inferred from the pipe on the same drawing as before.

### `extraction_prompt.txt` — clarify that `ELB` in the olet list means elbolet, not elbow

- **What:** Added a parenthetical to the olet rule (line 95): `ELB — note: ELB here means ELBOLET / ELBOWLET, not a standard elbow`.
- **Why:** `ELB` is the standard industry abbreviation for elbow (a pipe bend), not an olet. But in Summit's CompRefTable the `ELB` component code is used for elbolet / elbowlet / anvilet branch-outlet fittings. The prompt's olet list was correct per the reference table, but confusing to anyone (human or future reviewer) who reads `ELB` with the conventional meaning. One-line clarification prevents the question from coming up again.

### `aggregation_lambda_function.py` — remove unused override columns from Flagged tab

- **What:** Deleted the four override columns from the Flagged tab — `Override Component`, `Override Conn Qty`, `Override Conn Type`, `Override Notes` — along with the yellow-highlight `PatternFill` and the `if key.startswith("override_")` branches that styled them. Both `build_flagged_rows` (the four empty `override_*` keys) and `write_flagged_tab` (the four column tuples + fill logic) are simplified.
- **Why:** The columns were write-only decoration. A full repo grep confirmed nothing in the C# app reads them back — the app's only Flagged-tab touchpoints are a tab reorder and a Summary item count that pulls from the Material tab's `Confidence` column, not from Flagged. If a reviewer filled in an "override" value, nothing happened. A user wanting to correct a flagged item edits the Material tab directly, which is also where all the row context lives; the Flagged tab's purpose is "here are the rows to look at," and the `Drawing Number` + `Item ID` columns already let the reviewer jump to the corresponding Material row.

### `extraction_lambda_function.py` — cap exception-path failure marker at 500 chars

- **What:** In the outer exception handler, the failure marker's `error` field is now truncated to 500 characters (`error_message[:500]`). Matches the truncation already applied to the zero-BOM failure path's `extraction_notes`.
- **Why:** A verbose exception (long stack trace, huge AWS error body) would land as one giant cell on the Failed DWGs tab, making the tab unreadable. The full error still goes to CloudWatch via `logger.error(..., exc_info=True)` and to the Step Functions return payload, so no debugging context is lost. Only the human-readable Excel cell gets capped.

### `aggregation_lambda_function.py` — add try/except around S3 list in `load_batch_extraction_keys`

- **What:** Wrapped the `paginator.paginate(...)` loop in `load_batch_extraction_keys` in try/except. On list failure, logs `Could not list extractions at {prefix}: {e}` and returns whatever keys were collected so far (typically `[]`). Matches the defensive pattern already used by `load_failure_markers`.
- **Why:** The two functions do essentially the same work (list JSON files under a batch prefix) but only one had error handling. A transient S3 listing hiccup would crash the entire aggregation instead of producing a partial Excel with what it could read. Consistency + graceful degradation.

### `extraction_lambda_function.py` — delete legacy direct-image path

- **What:** Removed the pre-config-era direct-image calling convention from the Lambda. Specifically:
  - `lambda_handler` flattened. The old `if config_path: ... else: legacy ...` branch is gone; the handler now validates `config_path` up front and raises `ValueError` if missing, then runs the active config-based crop/extract flow at one indent level less.
  - Deleted `call_bedrock(full_prompt, image_bytes, media_type)` — the single-image Bedrock call used only by the legacy path. The multi-image variant `call_bedrock_multi_image` remains the sole entry point.
  - Deleted `parse_input(event)` (parsed `s3_path` or `bucket`+`key`), `load_drawing_as_image(bucket, key)`, `convert_pdf_to_png(pdf_bytes)`, and `convert_tiff_to_png(tiff_bytes)` — all only called from the removed legacy path.
  - Removed the `# Legacy helper functions` section header.
  - Total: ~140 lines gone. Python AST check passed.
- **Why:** The legacy path predates the current config-based cropping system. It accepted pre-cropped images and ran Bedrock on them directly — useful when an operator manually cropped the BOM outside AWS. Since `TakeoffService.StartBatchAsync` was wired up, every Step Functions execution sends the new-format payload (`{config_path, bucket, drawing_keys, rev_bubble_only}`). Full repo audit confirmed nothing else invokes the Lambda: no `AmazonLambdaClient` in the C# app, no references to `s3_path` anywhere outside the Lambda itself, no test harnesses or scripts using the legacy shape. Deleting removes ~140 lines of surface area that can't be exercised and also resolves `flagged-issues.md` #9 (non-batch zero-BOM silent success) for free, since the silent-success case only existed because the legacy path set `batch_id = None`.

### `aggregation_lambda_function.py` — remove dead locals and stale comments from `build_material_rows`

- **What:** In the inner `for item in bom_items` loop: removed three unused local assignments (`matl_grp`, `class_rating`, `length`), removed a duplicate `component = item.get("component") or ""` line, and removed two misleading comments (`# Build concatenated description` and `# Format: size IN - component - thickness - class - pipe spec - material - length`). ~8 lines gone. Zero functional change — the row dict already reads `item.get("matl_grp")`, `item.get("class_rating")`, and `item.get("length")` directly, so the locals were never consumed.
- **Why:** The function doesn't build any concatenated description (that logic lives in the C# `BuildConcatDescription`, which we already simplified during the `FindPipeSpec` cleanup). The stale comments told a future reader to expect concatenation logic that isn't there. The unused locals were noise that obscured which values actually flow into the row.

### Quantity handling — move conversion out of the Lambda and into the app (prompt + Lambda + C#)

- **What:** Three-file coordinated change that makes quantity follow the same pattern as size — Claude returns raw-as-printed, downstream app handles conversion.
  - **`extraction_prompt.txt`** (body, lines 30-43; OUTPUT FORMAT schema line 147): prompt now instructs Claude to echo the drawing's quantity verbatim with unit markers preserved (`"27'10\""`, `"10 FT 2 IN"`, `"5.5 ft"`, `"41.3'"`, `"6\""`, plain integers for counts). No more arithmetic or unit conversion by the model. Schema updated to show raw examples.
  - **`aggregation_lambda_function.py`** (`clean_quantity`): reduced to a pass-through. Removed the feet+inches regex and apostrophe-stripping logic. The raw Claude value flows straight to the Excel Material tab — either a number cell or a string cell depending on what Claude returned.
  - **`TakeoffPostProcessor.cs`**: added three methods and wired them into `GenerateLaborAndSummary` right after `WriteMaterialShopField`:
    - `ConvertQuantityToDecimal(object?)` — handles numbers, feet+inches (`27'10"`, `27'-10"`, `27' 10"`, `27'`), inches-only (`6"`), decimals with trailing prime/quote, and textual unit markers (`ft` / `FT` / `in` / `IN`). Returns `double?` so callers can detect failure.
    - `NormalizeMaterialQuantities(materialRows)` — one-time in-place pass. On success writes the decimal back to `row["Quantity"]`; on failure logs `QUANTITY_PARSE_FAILED` with drawing/item context and leaves the raw value so a human reviewer can spot the bad cell in Excel and rerun.
    - `WriteMaterialQuantities(workbook, materialRows)` — mirror of `WriteMaterialShopField`; writes the converted decimal back to the Excel Material tab so the saved workbook shows clean numbers. Skips cells whose in-memory value is still a string (conversion failed).
  - **`TakeoffPostProcessor.cs`** (`ParseQuantity`, `ParseExactQuantity`) and **`FittingMakeupService.cs`** (`ParsePipeLengthFeet`): simplified to read the already-normalized double from the row dict. Each keeps a string/TryParse safety-net branch for the rare failed-normalization row, but the regex-heavy feet+inches logic is gone from these consumers.
- **Why:** The previous design had three independent layers of silent fallback (Claude's raw-string escape hatch, Lambda's `clean_quantity` pass-through on parse failure, C# consumers defaulting to `1` or `0`). A mangled quantity could slip all the way through to Labor/Summary math without any user-visible signal. The new design establishes the decimal value once, in one place, before any consumer reads it — SPL, PIPE fab, connection explosion, and `ApplyRates` all see the same normalized number, and failures surface on the Material tab as the raw Claude text (which the reviewer fixes and reruns). `dotnet build` passed with 0 errors.
- **Files touched:** `extraction_prompt.txt`, `aggregation_lambda_function.py`, `Services/AI/TakeoffPostProcessor.cs`, `Services/AI/FittingMakeupService.cs`.

### `TakeoffPostProcessor.cs` — remove pipe-spec injection from Labor Description + delete unreachable fab-row code

- **What:** App-side change (paired with this AWS-Agent review thread):
  - Removed the `pipeSpec` piece from the concatenated `Description` on connection-row labor entries (BW/SW/BU/SCRD/OLW/THRD). Description now reads `"2 IN - 40 - 150 - CS - BW"` instead of `"2 IN - 40 - 150 - CS150 - CS - BW"`.
  - Deleted the `FindPipeSpec()` method and all call sites / parameter references.
  - Deleted the unreachable methods `CreateFabRow()` and `BuildFabDescription()` (never called — stubs left over from an earlier plan to emit CUT/BEV as separate labor rows; that logic now lives inside `ApplyRates()`).
- **Why:** `FindPipeSpec` picked the first title-block field whose key contained the substring "spec", which grabbed the wrong field on drawings with multiple spec fields (Pipe Spec / Coating Spec / Insulation Spec) and silently failed on drawings that label the field something else entirely (e.g., "Pipe Schedule"). The user already sees every title-block field as a dedicated trailing column on the Labor tab, so injecting a best-guess spec into the Description provided no information that wasn't already there — only confusion when the guess was wrong. Removing the heuristic lets the user read the true spec from its own column instead. `dotnet build` passed with 0 errors.
- **Files touched:** `Services/AI/TakeoffPostProcessor.cs` (not in `Plans/AWS Agent/`, but logged here because it is part of the same review thread).

### `extraction_lambda_function.py` — differentiate zero-BOM failure messages by batch type

- **What:** In the zero-BOM-item failure path, the error message now varies by batch type:
  - Rev-bubble run: `"No revision-bubbled items found on drawing"` (expected — drawing had no revisions).
  - Regular run: `"No items found in BOM"` (may indicate a real problem — misaligned crop, unreadable BOM).
  Claude's `extraction_notes` is still appended when present. Also hoisted the `rev_bubble_only = event.get("rev_bubble_only", False)` default up to before the `if config_path / else` branch so it is guaranteed to be in scope at the failure point on both code paths.
- **Why:** The zero-BOM-as-failure behavior is intentional — it prevents drawings from silently disappearing and documents them on the Failed DWGs tab. But the previous generic `"Zero BOM items extracted"` message couldn't distinguish a legitimately empty rev-bubble drawing from an actual extraction problem. In a rev-bubble batch, many drawings legitimately have no revisions; in a regular batch, zero items signals something worth investigating. Different wording lets the user tell these apart at a glance.

---
