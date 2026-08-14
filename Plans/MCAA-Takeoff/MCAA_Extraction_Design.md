# MCAA Takeoff — Extraction Design, Investigation & Cost Options

**Status:** Design of record after the 2026-08-13 investigation session. Captures what we tested, what we ruled out, the measured costs, and the recommended path. Supersedes earlier drafts that assumed an Anthropic API account.

---

## The core problem
The MCAA takeoff must produce, per drawing: the BOM properties (component / material / sizes / properties per MCAA vocab, for the rate key) **and** an accurate **connection count** (welds + bolt-ups). Two facts frame everything:
- **Reading connections from the BOM alone is structurally wrong** — it **misses pipe-to-pipe welds** and **double-counts fitting-to-fitting welds**. Not a viable base. (This is why the current Summit BOM scan, while cheap, is not accurate on connections.)
- **Reading them from the drawing** is the only correct source, but the drawing is **always a PDF** (no CAD/PCF — client drawings are PDFs, period), so it must be read visually.

## What we ruled out (with evidence)
- **BOM-only** → structurally wrong (above).
- **CAD / PCF parse** → OFF THE TABLE. Client drawings are always PDFs; structured source data does not exist. Do not revisit.
- **Pure computer-vision (count the weld dots in Python)** → **DEAD, proven this session.** Blob detection on the 5 test drawings found 2,800–5,100 ink marks per sheet and, after filtering, landed at 0 or 3–5× the true weld count — an algorithm cannot tell a weld dot from a dimension arrowhead, a node balloon, a support mark, or a period in the text. And even a perfect dot count can't assign **size/type/material** or compute **stock-length adds**, because those require reading the BOM + drawing. CV is at best a sanity-check, not a solution.
- **Conclusion (settled):** the takeoff **must be an LLM reading the PDF (drawing + BOM) and reasoning.** There is no cheaper mechanism that does the real job. The only remaining decisions are cost vs. accuracy and delivery mode.

## What we validated (with evidence)
Agentic per-drawing takeoff (render → crop/zoom into each node → read symbols → cross-check BOM), Opus 4.8, no answers given to the agents:

**Original 5 regression drawings (known answers):** 4/5 exact — N2-176304 (7✓), STORMP (29✓, the phantom-weld trap the one-shot fails), N2-107418 (9✓, the swage BW/SW), PWP (11✓); MEOH 13 vs 12 — drawn welds perfect, +1 only on stock-length arithmetic the agent self-flagged. Fix stock-in-code → effectively 5/5.

**3 unseen drawings from a different client (ADA/Intel, no answer key):** one-shot medium and agentic agreed within ±1 on all three; every disagreement was a **scope call** (does a thread-o-let header weld count; who owns an off-sheet tie-in), not a symbol misread. Both caught the one socket weld present.

**Key finding — the one-shot generalizes better than first feared on carbon-steel / butt-weld-dominant drawings** (bare BW dots read fine even downscaled). The resolution weakness (unreadable socket ticks) is **real but drawing-type-specific** — it bites on socket-weld-heavy small-bore stainless, not big CS water headers. So: **drawing mix determines how much accuracy the expensive agentic zoom actually buys.**

**Correction to bake into the prompt:** the "X-through-dot" symbols on the ADA drawings are **field welds and DO count** (both agentic and one-shot wrongly excluded them as level/support markers). This is a legend/prompt gap, fixable, and it argues for a **comprehensive connection-symbol legend** (see below).

## Measured costs (real, from this session)
- **Summit today (measured from CloudWatch):** ~30,900 input / ~2,200 output tokens per drawing, Sonnet 4.6, on-demand → **~$251 / 2,000.**
- **Agentic exploratory:** 82k–173k tokens/drawing (avg ~127k) → ~$1/drawing → **~$2,000–2,600 / 2,000** unoptimized. Multi-turn → **cannot** use batch.
- **One-shot Opus medium thinking:** ~18k input (prototype) / ~6.6k output → ~$500–730/2,000 on-demand. Production input (legend+BOM+refs) est. ~40k.
- **Model pricing:** Opus 4.8 $5/$25 per 1M in/out (on-demand); **Bedrock Batch Inference = 50% off** (verified on AWS pricing page). Sonnet 4.6 $3/$15.

## The delivery-mode decision (settled 2026-08-13)
- **On-demand** = predictable, prompt turnaround, full price. Current Summit mode.
- **Bedrock Batch Inference** = bundle all requests into one S3 file, submit one async job, results back **within ~24 h** (unpredictable exact time), **50% off**. Verified real on the AWS pricing page; works with Claude models. This is a *different mechanism* from the current pipeline, which calls Bedrock **live, one drawing at a time** (why Summit is NOT getting the discount today — "batch of drawings" in the app is orchestration of many real-time calls, not Batch Inference).
- **Decision: batch is acceptable** — 24 h turnaround is fine for the cost cut, and it lets us run a *more powerful* single pass (high effort, full legend) at half price. **No Anthropic API account needed** — everything runs on the existing AWS/Bedrock account.

## Cost options (per 2,000 drawings — input tokens estimated pending a measured extractor)

| # | Approach | Timing | Accuracy | ~Cost / 2,000 |
|---|---|---|---|---|
| 0 | Summit today (Sonnet BOM scan, on-demand) | predictable | structurally wrong on connections | ~$251 |
| 1 | Single-pass Opus, medium, on-demand | predictable | good; weaker on SW-dense | ~$730 |
| 2 | Single-pass Opus, medium, batch | ≤24 h | same as #1 | ~$365 |
| **3** | **Single-pass Opus, HIGH effort + full legend, batch (RECOMMENDED)** | ≤24 h | best single-pass | **~$575** |
| 4 | Agentic crop-zoom (real-time, cannot batch) | predictable | most accurate + auditable | ~$1,200–2,000 |

## Recommended design
**Single-pass, legend-guided extraction — Opus 4.8, high effort — submitted through Bedrock Batch Inference**, on the existing AWS account.

Per drawing (one request):
1. Render PDF to high-DPI; provide the drawing + a **comprehensive connection-symbol legend** (all weld types shop/field · BW/SW · threaded · grooved · flanged · mechanical · olet family — fixing "X = field weld = counts") + the MCAA vocab reference.
2. Model returns structured JSON: `title_block`, `bom_items[component/material/sizes/properties]`, `connections[type + size + material, welds & bolt-ups]`.
3. Code computes stock-length adds deterministically (ceil(run/stock)−1; SS 20 ft, CS 40 ft).
4. VANTAGE C# (largely built): compose the MCAA key → look up man-hours in `Resources/cdx_weblem_rates.db` → generate the MCAA labor + review tabs.

Cost lever without touching timing: **prompt caching** (real-time, caches the fixed prompt+legend) — supported on Bedrock; trims input cost, no effect on turnaround.

## Build steps (next session)
1. **Build the comprehensive connection-symbol legend** (structured + reference image), serving both weld counting and the MCAA connection-type key.
2. **Build ONE single-pass extractor** (render + legend + BOM + drawing → JSON) and measure real tokens/accuracy on the 5 known-answer drawings. ← first concrete step.
3. **Confirm Bedrock Batch Inference accepts the multimodal image + thinking request format** (batch has format rules — read `Plans/claude-code-aws-deployment-guide.md`).
4. **Wire the batch orchestrator** (build input file → submit job → collect S3 results) + the C# key/rate/labor side.
5. **Validate on a batch of unseen drawings with ground truth** (Steve provides) — especially socket-weld-dense stainless — to decide whether single-pass suffices or an agentic escalation is needed for hard cases.

## Open items / caveats
- Input-token estimates need a real measured extractor before quoting a firm price.
- Single-pass has no agentic zoom → SW-dense small-bore accuracy must be validated; a **hybrid** (single-pass default + agentic escalation on flagged/low-confidence drawings, on-demand) remains a fallback.
- Legend must include the field-weld (X) correction and be validated against multiple clients' drafting conventions; also read each sheet's own legend when present.
