# MCAA Rate-Sheet Lookup-Key Composition

**Status:** Canonical. Single source of truth for how the MCAA rate-sheet lookup key is composed — on BOTH sides of the integration contract: the SkySkraper Excel producer (`cdx_rates_review_FinalMerged-r3.xlsx`) and the VANTAGE C# consumer (takeoff key composer). This supersedes `SkySkraper/output/new_key_formulas.txt` (a non-versioned scratch note) and the ordering in the "Extracted-values model & key composition" section of `MCAA_Ratesheet_Plan.md`. When the recipe changes, change it HERE.

**Change log**
- **2026-08-13** — Workbook column changes (formulas + map rewritten): `newManHours` (was col H) **deleted** — every column right of H shifted left by one. `weblem_data_id` (was col B) **repurposed in place** to a `DUPES` helper — a `COUNTIF` over `lookup_key` (C) used to find duplicate keys; the scraped `weblem_data_id` value is no longer stored. Conditional formatting in column C (the old duplicate highlighter) **discontinued** — the `DUPES` column replaces it. Net column count 57 → 56.
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

Header is row 1; formulas go in **row 2** and fill down. Compute `Merged_Props` (L) FIRST — `lookup_key` references it.

**Merged_Props — cell `L2`:**
```
=TEXTJOIN("|",TRUE,SORT(FILTER(M2:R2,M2:R2<>"",""),1,1,TRUE))
```

**lookup_key — cell `C2`:**
```
=TEXTJOIN("|",TRUE,E2,F2,K2,L2,U2,V2,W2,X2,Y2,T2,Z2,AA2,AB2,AC2,AD2,AE2,AF2)
```

`connection_qty` (S) is intentionally NOT referenced. The formula order (`…Y2,T2,Z2…`) differs from physical column order because `connection_type` (T) is emitted just before the sizes.

### Column map (current as of r3, 2026-08-13)
⚠️ **Resolve columns by header NAME, not letter** — letters drift as columns are inserted/deleted. The letters below are current for `cdx_rates_review_FinalMerged-r3.xlsx`; re-verify against row 1 before pasting into any future version.

| Cell | Header |
|---|---|
| B | DUPES *(COUNTIF dup-key helper; was weblem_data_id)* |
| C | lookup_key |
| E | NewComp |
| F | Reducing |
| G | manhours |
| K | NewMaterial *(its own `=XLOOKUP`)* |
| L | Merged_Props |
| M–R | Prop1 … Prop6 |
| S | connection_qty |
| T | connection_type |
| U | pressure_rating |
| V | class_rating |
| W | schedule |
| X | weight_class |
| Y | length |
| Z–AF | size_1 … size_7 |

### No-sort Merged_Props variant
If props should ever stay in column order instead of A→Z: `=TEXTJOIN("|",TRUE,M2,N2,O2,P2,Q2,R2)`. The current locked choice is **A→Z**.

### DUPES helper — cell `B2`
Duplicate-key check (replaces the discontinued column-C conditional formatting). Returns the occurrence count of each `lookup_key`; anything > 1 is a duplicate:
```
=COUNTIF($C$2:$C$134418,C2)
```
Boolean flag variant: `=COUNTIF($C$2:$C$134418,C2)>1`. Bound the range to the data (not whole-column `$C:$C`) so it stays fast at scale. Column C did not move, so this formula is unaffected by the H-column deletion.

---

## Formula vs. static — important operational note

`NewMaterial` (K) is a live `=XLOOKUP` formula. `lookup_key` (C) and `Merged_Props` (L) are **derived** columns built from the TEXTJOIN formulas above.

**Accepted working practice: C and L are kept as pasted VALUES, not live formulas.** At ~134k rows the live TEXTJOIN/array formulas make the sheet hang, so the workflow when the recipe or any segment data changes is: repaste the formulas into `L2` then `C2` → fill down → let Excel finish computing → **paste-as-values** to freeze. The frozen values are Excel-computed, so they are correct by construction.

Consequences to remember:
- Static keys in this sheet are EXPECTED — do not "restore" them to live formulas; that regresses performance.
- Because C/L are static, an openpyxl `data_only=True` read returns them correctly. (The trap earlier was reading a formula column whose cache was empty — that only affects live-formula columns like K/NewMaterial, not these frozen ones.)
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
1. Repaste `L2` then `C2` in the FinalMerged workbook and let Excel recompute (this rebuilds every stored key in the new order).
2. Regenerate the `xlsx → SQLite` export (`cdx_weblem_rates.db`): ship **two columns only** — `lookup_key` (PRIMARY KEY — the key IS the row identity) and `manhours`. Everything else in the workbook is key-building scaffold and is NOT exported. (`weblem_data_id` no longer exists as a column, and `rate_id` was never unique — neither is needed.) ⚠️ `lookup_key` must be unique for the PK to hold — verify the `DUPES` column shows no values > 1 before exporting. (The 4 duplicate rows found 2026-08-13, including the two conflicting `PLG|DI|AWWA|C153…` keys, were removed — leaving 134,417 unique-key data rows.)
3. Update the VANTAGE C# key composer to match, if the order/rules changed.
