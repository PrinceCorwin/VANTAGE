# MCAA Takeoff — Phase 1 Findings (drawing-first weld counting)

**Status:** Prototype proving one-pass, full-page BW/SW counting against the 5 `vantage_handoff` regression drawings. In progress; strong signal.

## Setup
- **Harness (session-local, scratchpad — recreate if needed):** renders each regression PDF full-page to ≤8000px, sends the 4 weld-symbol reference PNGs + the drawing to Bedrock `converse`, using `01_master_takeoff_instructions.md` as the system prompt. Model reports **drawn welds only**; **stock-length adds are computed in code** (`ceil(run_ft / stock_len) − 1`; SS=20ft, CS=40ft; unknown material → flagged, never invented).
- **Model = `us.anthropic.claude-opus-4-8`.** Sonnet 4.6 was tested and **rejected** — it massively over-counts (STORMP 51 vs 29), the "counts fitting ends" failure. Opus respects the drawing-first principle.
- **Extended thinking is the key lever.** Opus 4.8 uses the adaptive form: `additionalModelRequestFields={"thinking":{"type":"adaptive"},"output_config":{"effort":"high"}}`, `maxTokens=24000`, **no `temperature`** (deprecated on 4.8). Cost: ~1 min + ~13–16k output tokens per drawing at high effort.

## Results (total welds vs known answer)
| Drawing | Expected | Opus, no thinking | Opus, high-effort thinking |
|---|---|---|---|
| MEOH-111304-21 | 12 | 10 | **12 ✓** |
| N2-107418-01 | 9 | 9 ✓ | (run in progress) |
| N2-176304-01 | 7 | 8 (also saw 6/7) | (in progress) |
| PWP-014002-16 | 11 | 8 | (in progress) |
| STORMP-100412-01 | 29 | 27 | (in progress) |

## Key findings
1. **Thinking closes most of the gap** — MEOH went off-by-2 → exact, and stock-length runs were read correctly (2, not 1). The "perfect" interactive Cowork result = reasoning + close looking + **2 human corrections** (per `02_session_transcript.md`); we've now reproduced the reasoning half.
2. **Socket-weld detection works** on Opus (the feared part) — SW counts were exact on both drawings that have them in the no-thinking runs (N2-107418: 4×½″; STORMP: 6×¾″).
3. **Stock-length-in-code is the right split** but depends on the model reliably reporting straight-run lengths; thinking improves that reporting.
4. **Residual ±1 jitter** on the drawn count remains — non-determinism (no temp knob on 4.8) + resolution. Full sheet at ≤8000px ≈ **235 DPI**, and the spec says symbols/geometry need high-DPI per-node views.

## Next levers (to close the last ±1 and reach the interactive-quality result)
- **Resolution:** tile the drawing into high-DPI regions (or a full-page routing pass + high-DPI crops on flagged nodes). Untested; most likely cause of the last missed weld.
- **Verify/self-correction pass:** an automated second look that critiques the first count — the stand-in for the human correction turns.
- Determinism handled operationally via the **human Review tab** (model self-flags low-confidence joints), not 3× sampling.

## ⚠️ Throughput / scale constraint (batches up to 2,000 drawings)
- **1 min/drawing at HIGH effort is a ceiling test, NOT the production setting.** The 5-drawings-in-5-8-min figure is from the **sequential** test harness — production fans out across **concurrent** Lambda invocations. Wall-clock ≈ (drawings ÷ concurrency) × latency.
- **Must do before production:**
  1. **Effort sweep** — test `low` / `medium` adaptive effort vs `high` for the accuracy/latency/cost tradeoff. High is almost certainly overkill; find the minimum that holds the count. (Token cost of high effort × 2,000 drawings is the real cost driver.)
  2. **Concurrency** — the MCAA Step Functions stack must run far higher `MaxConcurrency` than Summit's `3`. Real cap becomes the **Bedrock rate quota** (TPM/RPM), which we manage/request-increase.
  3. **Possible tiering** — cheap/fast pass for simple drawings, reserve high effort only for complex/flagged ones.
- Target: a 2,000-drawing batch must complete in a tolerable window (tens of minutes, not hours) at acceptable cost.

## Then (Phase 2+)
Fold the proven weld-count instructions into the combined one-pass MCAA extraction prompt (add BOM material extraction via the 4 ref tables), expand scope to **all connections incl. bolt-ups** (the 5 regression answers are BW/SW-only — need expected BU counts to validate), then the separate MCAA AWS stack and C# rate lookup. See the session plan.
