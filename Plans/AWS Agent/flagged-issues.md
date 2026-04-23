# Flagged Issues — Consolidated List

Derived from `prompt-lambda-status.md`. Covers the extraction prompt, extraction Lambda, and aggregation Lambda. Ordered by priority within each tier.

Last updated: 2026-04-22

---

## TIER 1 — Fix (real risk or active drift)

### 1. MODEL_ID mismatch between Lambda and status doc
**Where:** extraction Lambda (§2.1); project status doc
**What:** Lambda ships `us.anthropic.claude-sonnet-4-5-20250929-v1:0` (Sonnet 4.5). Status doc + context notes say `us.anthropic.claude-sonnet-4-20250514-v1:0`.
**Risk:** One is stale. If the 4.5 upgrade was deliberate, the status doc is wrong; if not, the Lambda is running on a model we didn't intend to use.
**Action:** Check CodeSha256 history or deployment logs to determine which is current truth. Update the other.

### 2. Prompt contradicts itself on `size` / `connection_size` format
**Where:** `extraction_prompt.txt` (§1.9)
**What:** Body text says "EXACTLY as printed" with fraction preservation (`3/4`, `1-1/2`, `6X4`). Output-schema example block at the bottom says `"decimal format (e.g. 0.5, 1.5, 6, 6x4)"`.
**Risk:** Model may follow the schema example on some drawings and the body rule on others. Inconsistent extraction output will confuse the aggregation Lambda's `normalize_size` (it can handle both, but it shouldn't have to).
**Action:** Update the schema example block to match the "as printed" rule. Two lines to edit.

### 3. `raw_description` (Material) vs `description` (Flagged) — column key inconsistency
**Where:** aggregation Lambda (§3.16)
**What:** Same data, two different column names across the Material and Flagged tabs. C# consumer must know both.
**Risk:** Silent breakage if anyone renames one without the other. Future developer confusion.
**Action:** Standardize on one name in the Lambda (probably `raw_description` to match Material, since Material is the primary tab). Update C# side to match.

### 4. `normalize_size` joins reducers with lowercase `x`
**Where:** aggregation Lambda (§3.8)
**What:** Prompt output uses uppercase `X` (`6X4`, `3/4X1/2`). `normalize_size` joins normalized parts with lowercase `x` (`0.75x0.5`).
**Risk:** If C# parses reducers with case-sensitive `Split('X')`, reducer sizes will fail to parse after aggregation.
**Action:** Either change the join to uppercase `X` in `normalize_size`, or confirm C# uses `StringComparison.OrdinalIgnoreCase` / case-insensitive split. Prefer the uppercase fix — it's one character.

### 5. `clean_quantity` silent string fallback
**Where:** aggregation Lambda (§3.8, §3.16)
**What:** When quantity can't be parsed as a number, function returns the original value (possibly a string) instead of flagging. That string lands in the Excel Quantity cell.
**Risk:** Silent data quality failure. A malformed quantity flows downstream into labor/ROC math as a string and either errors out or gets coerced badly.
**Action:** Decide on the contract. Options: (a) return `None` and flag the row in a warning log; (b) surface the raw string to the Flag column for manual review; (c) raise. Current behavior is the worst of all worlds — silent + typed-wrong.

### 6. Dead local variables in `build_material_rows`
**Where:** aggregation Lambda (§3.9, §3.16)
**What:** Locals `matl_grp`, `class_rating`, `length` assigned but unused (row dict reads `item.get(...)` directly). `component` assigned twice. Stale comment describing a concatenated description format that's never built.
**Risk:** Low functional risk, but actively confuses any future edit to this function. The "Format: size IN - component - thickness - class - pipe spec - material - length" comment is especially misleading — it suggests logic that isn't there.
**Action:** Drop the unused locals, drop the duplicate assignment, drop or implement the concatenated description comment. ~10 lines to clean.

### 7. Status doc says "2 tabs" — Lambda outputs 3
**Where:** project knowledge status doc; aggregation Lambda docstring (§3.16)
**What:** Status doc in project knowledge says "Lambda outputs only Material + Flagged tabs." Actual Lambda produces Material, Flagged, and Failed DWGs.
**Risk:** New contributors or future-you reads the stale doc and designs against a wrong assumption.
**Action:** Update the status doc (and `mass_takeoff_implementation_plan` if it says the same) to reflect 3 tabs.

---

## TIER 2 — Cleanup (dead weight, minor inconsistencies)

### 8. Legacy flow still present in extraction Lambda
**Where:** extraction Lambda (§2.20)
**What:** `parse_input`, `load_drawing_as_image`, `convert_pdf_to_png`, `call_bedrock`, plus the entire `else` branch in the handler handle a legacy direct-image input format.
**Risk:** Dead code if all production traffic goes through `config_path` (which per context is true). Adds surface area for bugs and increases package size.
**Action:** Confirm no external caller uses the legacy path (Step Functions, test harnesses, any manual invocation). If none, delete the legacy branch and its helpers.

### 9. Zero-BOM guard is batch-only
**Where:** extraction Lambda (§2.14, §2.20)
**What:** Zero-BOM failure marker writes only when `batch_id` is set. Non-batch zero-BOM runs silently write a "success" JSON with an empty `bom_items` array.
**Risk:** A manual test/invoke that hits a misaligned config or rev-bubble drawing with no rev items looks like success — no surface signal. Easy to miss.
**Action:** Extend the check to non-batch mode (log warning at minimum, or return a non-success status in the payload).

### 10. `load_batch_extraction_keys` has no error handling
**Where:** aggregation Lambda (§3.16)
**What:** `load_failure_markers` is wrapped in a try/except that returns `[]` on listing failure. `load_batch_extraction_keys` is not — a list error raises straight up.
**Risk:** Inconsistent defensive posture. In practice the listing rarely fails, but if it does, a partial-failure batch fails entirely instead of surfacing what it could.
**Action:** Mirror the defensive pattern. Log and return `[]` on list failure; downstream code already handles empty-extractions-plus-failures.

### 11. `extraction_notes` truncation inconsistency
**Where:** extraction Lambda (§2.20)
**What:** Zero-BOM failure path truncates `extraction_notes` to 500 chars. Generic exception path writes full `str(e)` with no truncation.
**Risk:** Low — exception messages are usually short. But the asymmetry is a trap if someone adds a verbose exception message later.
**Action:** Apply the 500-char cap consistently, or document why the two paths differ.

### 12. Flagged tab missing columns vs Material
**Where:** aggregation Lambda (§3.10, §3.16)
**What:** Flagged omits `connection_size`, `quantity`, `commodity_code`, `length`, `shop_field`, and all `tb_*` fields.
**Risk:** User reviewing a flagged item doesn't see all context. When C# re-applies overrides to Material, it has to match by `(drawing_number, item_id)` — a data contract that's implicit.
**Action:** Decide whether Flagged should be a full copy of the Material row (plus override columns) or a minimal review view. Document the contract either way. Current state is in between.

### 13. Illegible quantity raw-string fallback in prompt
**Where:** `extraction_prompt.txt` (§1.9)
**What:** Prompt instructs model to return a raw string in the `quantity` field when illegible, with `confidence: "low"`. Schema declares quantity as a number.
**Risk:** Downstream JSON consumers that trust the schema may break. The aggregation Lambda's `clean_quantity` happens to tolerate it (see issue #5), but that's circumstantial.
**Action:** Align. Either drop the raw-string fallback (return `null` + flag), or update the schema to explicitly allow `number | string`.

---

## TIER 3 — Awareness (known behavior, design tradeoffs, very minor)

### 14. Consensus backfill is exact-string, unweighted
**Where:** aggregation Lambda (§3.5, §3.16)
**What:** Groups items by exact raw description (case + whitespace sensitive). Votes are unweighted — a low-confidence rating counts the same as a high-confidence one.
**Implication:** OCR variants never merge. In small batches, a hallucinated rating can outvote a correct one.
**Action (if ever):** Normalize descriptions before grouping (lowercase + collapse whitespace), weight votes by confidence. Not worth it unless you see real conflicts in production logs.

### 15. `bom_row_count` vs `bom_items` length — no cross-check
**Where:** extraction Lambda (§2.20)
**What:** Both values come from the model. Zero-BOM guard uses `bom_row_count` only; they could disagree silently.
**Action (if ever):** Trust `len(bom_items)` as the ground truth; log a warning on disagreement.

### 16. "Image N: {label}" text block per image
**Where:** extraction Lambda (§2.20)
**What:** Small token tax per image in the multi-image Bedrock call.
**Action (if ever):** A/B with the labels removed. If no accuracy drop, strip them.

### 17. No app-level retry around `bedrock.converse`
**Where:** extraction Lambda (§2.20)
**What:** Only botocore adaptive retry (max_attempts=10) handles throttling/5xx.
**Action (if ever):** Add application-level retry with backoff on transient errors if batch reliability ever becomes an issue at higher concurrency.

### 18. `render_pdf_page` memory guard assumes RGB
**Where:** extraction Lambda (§2.20)
**What:** `width * height * 3` underestimates RGBA. Currently safe because `alpha=False` is set.
**Action (if ever):** If alpha is ever enabled, switch to `width * height * 4` or derive from `pix.n`.

### 19. Excel column width sampled from first 100 rows only
**Where:** aggregation Lambda (§3.16)
**What:** Wide values in rows 101+ don't widen their column.
**Action:** Not worth changing unless users complain.

### 20. `batch_id` not sanitized before S3 key construction
**Where:** aggregation Lambda (§3.16)
**What:** `batch_id` flows directly into S3 prefixes. Internal tool, low risk, but noted.
**Action:** Not worth changing while the caller is controlled (Vantage app).

### 21. Hardcoded color constants inside `generate_excel`
**Where:** aggregation Lambda (§3.16)
**What:** Header fill `DAEEF3`, override fill `FFFFCC`, border style all hardcoded inside the function.
**Action:** Hoist to module-level constants only if you start adding more tabs/styles.

---

## Suggested fix order

If you want a short plan:
1. Issues **1** and **2** first — they're both quick and both affect the contract the extraction stage commits to. 15 minutes total.
2. Issues **3** and **4** together — they're related column-naming + format-normalization work in the aggregation Lambda and C# consumer.
3. Issue **5** next — it's a data quality bug pretending to be working code.
4. Issues **6** and **7** as a cleanup pass — dead code + doc drift. Satisfying to close out.
5. Tier 2 in whatever order surfaces naturally during feature work.
6. Tier 3 only if triggered by a real symptom.
