# Deferred Issues — Claude Code Todo List

Backlog of small issues surfaced during the 2026-04-22 Lambda review. None are blocking. Grouped by file. Within each group, ordered roughly by effort (quick wins first).

---

## `aggregation_lambda_function.py`

### 1. Join reducer sizes with uppercase `X` instead of lowercase `x`
**Where:** `normalize_size()`, the `"x".join(normalize_single_size(p.strip()) for p in parts)` line.
**What:** Change `"x".join(...)` to `"X".join(...)`.
**Why:** Drawings use uppercase `X`. Prompt instructs Claude to output uppercase `X`. Extraction JSON is uppercase. The Lambda is the only place rewriting to lowercase. C# consumers are already case-insensitive (verified by audit), so no behavior change — purely a convention fix so the Excel reviewer sees the same format the drawing shows.
**Effort:** One character. Add a CHANGES.md entry.

### 2. Drop unused `count` in `consensus_backfill_class_rating`
**Where:** `winner, count = counter.most_common(1)[0]`.
**What:** Rename `count` to `_`.
**Why:** `count` is never read. Lint noise.
**Effort:** Trivial.

### 3. Standardize None handling in `build_material_rows`
**Where:** The inner `for item in bom_items` loop that builds the row dict.
**What:** Some fields use `item.get("x") or ""` (turns None into `""`), others use bare `item.get("x")` (keeps None). Pick one and apply consistently.
**Why:** Both render as empty Excel cells so no visible difference, but anyone reading the row dict downstream sees two different "missing" representations. Current inconsistency: `commodity_code`, `component`, `size`, `thickness`, `conn_type` use `or ""`; `class_rating`, `length`, `matl_grp`, `matl_grp_desc` use bare `.get()`.
**Effort:** ~10 minutes. Recommend bare `.get()` everywhere — None is the honest signal.

### 4. Consensus backfill improvements (only if real conflicts show up in logs)
**Where:** `consensus_backfill_class_rating()`.
**What:** Two possible improvements:
- Normalize descriptions before grouping (lowercase + collapse whitespace) so OCR/typo variants merge.
- Weight votes by confidence so a low-confidence hallucinated rating can't outvote a single high-confidence one in small batches.
**Why:** Current behavior is exact-string, unweighted. Fine in practice; not worth touching unless CloudWatch starts showing `CLASS_RATING_CONFLICT` warnings with surprising winners.
**Effort:** Medium. Defer until triggered by a real symptom.

### 5. Hoist styling constants out of `generate_excel`
**Where:** `generate_excel()` function body.
**What:** `DAEEF3` header fill, border style, freeze pane position hardcoded inside the function.
**Why:** Only worth doing if you start adding more tabs/styles that share these values. Not worth doing pre-emptively.
**Effort:** Small. Defer until needed.

---

## `extraction_lambda_function.py`

### 6. Cross-check `bom_row_count` against `len(bom_items)`
**Where:** Zero-BOM guard at the end of the main success path.
**What:** Both values come from the model and could disagree silently. Trust `len(bom_items)` as ground truth; log a warning if `bom_row_count` disagrees.
**Why:** Low risk today but cheap insurance against a future model output quirk.
**Effort:** ~5 minutes.

### 7. Drop the "Image N: {label}" text blocks in `call_bedrock_multi_image`
**Where:** The loop that builds `content_blocks`.
**What:** Currently prepends a `{"text": f"Image {i+1}: {label}"}` block before every image. That's a token tax per image in every call.
**Why:** Probably not improving accuracy given the trailing instruction already describes the image set. A/B it — if no accuracy drop, strip them.
**Effort:** Small to test, trivial to remove.

### 8. App-level retry around `bedrock.converse`
**Where:** `call_bedrock_multi_image()`.
**What:** Currently only botocore adaptive retries (max_attempts=10) handle throttling/5xx. No application-level retry with backoff on transient errors.
**Why:** Only worth adding if batch reliability ever becomes a problem at higher concurrency (e.g., after the Bedrock quota increase lands). Not needed at MaxConcurrency=40 under current quota.
**Effort:** Medium. Defer until it's a real problem.

### 9. `render_pdf_page` memory guard assumes RGB
**Where:** `render_pdf_page()`, the `estimated_bytes = pix.width * pix.height * 3` line.
**What:** Hardcoded `* 3` for RGB. Currently safe because `alpha=False` is set.
**Why:** Would under-estimate if alpha is ever enabled. Could switch to `pix.n` (channels) to be future-proof.
**Effort:** Trivial. Do it next time you touch this function.

---

## `extraction_prompt.txt`

### 10. Tighten raw-string fallback language for illegible quantities
**Where:** Body text under the `quantity` field description.
**What:** Currently instructs the model to return raw text as a fallback when illegible. Now that quantity handling moved app-side, the fallback is more tolerable — but the wording could be clearer that any raw-text return should also set `confidence: "low"` and include a `flag`.
**Why:** Minor — current wording already covers this, but could be more explicit.
**Effort:** Small wordsmith pass.

---

## Cross-cutting / process

### 11. Decide whether Flagged tab should mirror Material tab
**Where:** `build_flagged_rows` in aggregation Lambda.
**What:** Flagged omits `connection_size`, `quantity`, `commodity_code`, `length`, `shop_field`, and all `tb_*` fields. A user reviewing a flagged item doesn't see full context. Two options:
- Make Flagged a full mirror of Material plus the flag reason column.
- Leave Flagged as a minimal "rows to look at" view and document that C# matches back by `(drawing_number, item_id)` to get the full row from Material.
**Why:** Current state is in between — feels like it should be one or the other. Document the contract either way.
**Effort:** Low if "keep minimal" (just document). Medium if "expand to mirror Material" (row builder + write tab).

### 12. PEP 8 blank lines between top-level functions
**Where:** Both Lambda files.
**What:** Some top-level function definitions have only one blank line between them instead of the PEP 8–preferred two. Pre-existing.
**Why:** Pure cosmetics. Run `black` or `ruff format` on both files next time you're editing them.
**Effort:** Trivial with a formatter.

### 13. Move `from botocore.config import Config` to the top of `extraction_lambda_function.py`
**Where:** Line 12, mid-module after `s3` client creation.
**What:** Move to the top with the other imports.
**Why:** PEP 8. Pre-existing. Cosmetic.
**Effort:** Trivial.

---

## Not on this list (intentional)

The following are known behaviors / design tradeoffs that surfaced during review but are **not** worth tracking as todos:
- Consensus backfill grouping by exact description string — safer than fuzzy matching; leave it alone unless real conflicts emerge.
- Excel column width sampled from first 100 rows only — rarely matters; leave it.
- `batch_id` not sanitized before S3 key construction — internal tool, controlled caller, fine.
- CHANGES.md entry ordering quirk on the zero-BOM message change — purely historical, no code effect.

---

## Suggested triage

- **Next time you touch the aggregation Lambda:** items 1, 2, 3. All trivial, all in one file.
- **Next time you touch the extraction Lambda:** items 6, 9, 13. Same pattern.
- **Before the Bedrock quota increase lands:** consider item 8 (app-level retry) in case concurrency climbs.
- **After seeing real CloudWatch data from production batches:** revisit items 4 and 7 based on what the logs actually show.
- **Items 5, 10, 11, 12:** pick up opportunistically or ignore.
