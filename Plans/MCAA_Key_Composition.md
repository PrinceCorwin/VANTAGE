# MCAA Rate-Sheet Lookup-Key Composition

**Status:** Canonical. Single source of truth for how the MCAA rate-sheet lookup key is composed — on BOTH sides of the integration contract: the SkySkraper Excel producer (`cdx_rates_review_FinalMerged-r3.xlsx`) and the VANTAGE C# consumer (takeoff key composer). This supersedes `SkySkraper/output/new_key_formulas.txt` (a non-versioned scratch note) and the ordering in the "Extracted-values model & key composition" section of `MCAA_Ratesheet_Plan.md`. When the recipe changes, change it HERE.

**Change log**
- **2026-08-06** — `connection_qty` **removed from the key** — redundant now that `connection_type` lists one token per end (qty == token count on all 96,584 keyed rows; 0 rows where qty carried info the tokens didn't). The **column is retained** for validation / AI-ref use; it is simply not part of the key.
- **2026-08-06** — `connection_type` moved to **just before the size columns** (was immediately after `connection_qty`).
- **2026-07-10** — Sorting of sizes / connection tokens dropped; stored column order is canonical.

---

## The lookup key

One flat, pipe-delimited string. Blanks are **skipped** — Excel `TEXTJOIN(…, TRUE, …)` semantics, no `NONE`/placeholder sentinel. Segment order:

1. NewComp
2. Reducing
3. NewMaterial
4. Merged_Props
5. pressure_rating
6. class_rating
7. schedule
8. weight_class
9. length
10. **connection_type**  ← just before the sizes (moved here 2026-08-06)
11. size_1 … size_7

`connection_qty` is **not** in the key (removed 2026-08-06 — redundant with the per-end `connection_type` token list). The column still exists in the sheet; it is just not keyed.

### Rules
- **No sorting** of sizes or connection tokens — stored column order IS canonical. C# must emit them in that same order.
- **Merged_Props** is the only sorted segment: `Prop1..Prop6`, blanks dropped, sorted A→Z, pipe-joined (each prop is its own token).
- **connection_type** is the only segment with an internal delimiter: multiple ends are **comma**-joined in stored order (e.g. `BW,BW`, `THRD,COMP`) and used as-is, never re-sorted. Every other boundary is a pipe.
- One connection per size, kept paired (`connection_i` ↔ `size_i`), no collapsing of identical connections.
- **length** carries an explicit unit (`FT` / `IN`, uppercase). C# must emit the same convention. (Bare-numeral PIPE lengths were unit-normalized to `FT` on 2026-08-06.)

---

## Excel formulas (producer side)

Header is row 1; formulas go in **row 2** and fill down. Compute `Merged_Props` (M) FIRST — `lookup_key` references it.

**Merged_Props — cell `M2`:**
```
=TEXTJOIN("|",TRUE,SORT(FILTER(N2:S2,N2:S2<>"",""),1,1,TRUE))
```

**lookup_key — cell `C2`:**
```
=TEXTJOIN("|",TRUE,E2,F2,L2,M2,V2,W2,X2,Y2,Z2,U2,AA2,AB2,AC2,AD2,AE2,AF2,AG2)
```

`connection_qty` (T) is intentionally NOT referenced. The formula order (`…Z2,U2,AA2…`) differs from physical column order because `connection_type` (U) is emitted just before the sizes.

### Column map (current as of r3, 2026-08-06)
⚠️ **Resolve columns by header NAME, not letter** — letters drift as columns are inserted/deleted. The letters below are current for `cdx_rates_review_FinalMerged-r3.xlsx`; re-verify against row 1 before pasting into any future version.

| Cell | Header |
|---|---|
| C | lookup_key |
| E | NewComp |
| F | Reducing |
| L | NewMaterial *(its own `=XLOOKUP`)* |
| M | Merged_Props |
| N–S | Prop1 … Prop6 |
| T | connection_qty |
| U | connection_type |
| V | pressure_rating |
| W | class_rating |
| X | schedule |
| Y | weight_class |
| Z | length |
| AA–AG | size_1 … size_7 |

### No-sort Merged_Props variant
If props should ever stay in column order instead of A→Z: `=TEXTJOIN("|",TRUE,N2,O2,P2,Q2,R2,S2)`. The current locked choice is **A→Z**.

---

## Formula vs. static — important operational note

`NewMaterial` (L) is a live `=XLOOKUP` formula. `lookup_key` (C) and `Merged_Props` (M) are **derived** columns built from the TEXTJOIN formulas above.

**Accepted working practice: C and M are kept as pasted VALUES, not live formulas.** At ~116k rows the live TEXTJOIN/array formulas make the sheet hang, so the workflow when the recipe or any segment data changes is: repaste the formulas into `M2` then `C2` → fill down → let Excel finish computing → **paste-as-values** to freeze. The frozen values are Excel-computed, so they are correct by construction.

Consequences to remember:
- Static keys in this sheet are EXPECTED — do not "restore" them to live formulas; that regresses performance.
- Because C/M are static, an openpyxl `data_only=True` read returns them correctly. (The trap earlier this session was reading a formula column whose cache was empty — that only affects live-formula columns like L, not these frozen ones.)
- A script that recomposes keys programmatically MUST use the exact recipe in this doc (same order, blank-skip, comma-join, A→Z props) so its output matches what the Excel formula would produce.

---

## C# contract (consumer side)

VANTAGE's takeoff key composer must produce a **byte-identical** string:
- Same segment order (including `connection_type` immediately before the sizes).
- Same blank-skipping (omit empty segments, no placeholder).
- Same comma-join, stored order, for multi-end `connection_type`.
- Same `FT` / `IN` uppercase unit convention in `length`.
- `Merged_Props` sorted A→Z.

Any drift = silent lookup misses against the rate DB.

---

## Downstream reminders (do after any recipe change)
1. Repaste `M2` then `C2` in the FinalMerged workbook and let Excel recompute (this rebuilds every stored key in the new order).
2. Regenerate the `xlsx → SQLite` export (`cdx_weblem_rates.db`), keyed on `weblem_data_id` (NOT `rate_id` — it has duplicates).
3. Update the VANTAGE C# key composer to match, if the order/rules changed.
