# Lessons Learned / Decision Log

## Source scope

This log captures only what was established in the referenced conversation. It should not be expanded into unsupported project standards unless new drawings, legends, specs, or user corrections confirm those rules.

## Core decisions

1. The takeoff must be drawing-first. The physical connection marker/node on the isometric is the primary counted object.
2. The BOM is a validation and classification source, not the primary source of weld quantity.
3. Count each physical joint once, including fitting-to-fitting joints.
4. Count pipe-to-pipe welds when the drawing shows a weld symbol on straight pipe, even if the BOM has no fitting at that location.
5. For this exercise, BU means bolt-up and is ignored unless the user explicitly asks for bolt-up counts.
6. BW means butt weld. SW means socket weld.
7. Dot-only symbols usually indicate BW. Dot-with-fork/socket symbols usually indicate SW.
8. The drawing symbol is important, but drafting may be imperfect. Always cross-check with the BOM and fitting end descriptions.
9. Reducing fittings must be identified from the BOM because iso linework may not visibly show the size change.
10. Straight-run stock-length welds are added after the drawn connection count.

## Corrected mistakes and resulting lessons

### Mistake: Treating the BOM as the source of quantity

The user already had a BOM-based AI agent, but it was inaccurate because it doubled fitting-to-fitting welds and missed pipe-to-pipe welds.

Decision: Count actual drawing connection nodes first. Use the BOM only to validate fitting type, end prep, size, material, and reductions.

### Mistake: Double-counting fitting-to-fitting joints

Counting fitting ends separately can count one physical weld twice when two fittings touch.

Decision: A connection between two fittings is one joint. It must appear once in the connection-level audit.

### Mistake: Missing pipe-to-pipe welds

Some welds appear in straight pipe runs and have no corresponding BOM fitting.

Decision: If the drawing shows a weld symbol on straight pipe, count it even without a BOM fitting.

### Mistake: Misreading BW, SW, and BU symbols

The assistant initially asked for a legend and then incorrectly treated connection 01 in the first sample as BU. The user clarified:

- BW = butt weld.
- SW = socket weld.
- BU = bolt-up.
- Dot only generally means BW.
- Dot with fork/socket marks generally means SW.
- Connection 01 was a BW dot at the stub end, not BU.
- BU connections are ignored for this exercise.

Decision: Use dot-only versus dot-with-fork as the working symbol rule, then check against BOM/end type.

### Mistake: Misidentifying connection 07 on the first sample

The assistant first called connection 07 pipe-to-pipe. The user corrected it: connection 07 was a 1/2 inch socket-weld 90 elbow to pipe.

Decision: Inspect the geometry around each callout, not just the dot location. A curved elbow shape changes what is connected.

### Mistake: Misreading swage behavior

The first sample had a 1 inch x 1/2 inch swage/reducer. The user corrected connections 05 and 06:

- Larger 1 inch side: BW.
- Smaller 1/2 inch side: SW.

Decision: Evaluate swages/reducers end by end. Size and weld type may change across the fitting.

### Decision: Validated first sample rollup

After corrections, the first sample drawing was read as:

| Size | Type | Material | Qty |
|---:|---|---|---:|
| 1 inch | BW | 316/316L SS | 5 |
| 1/2 inch | SW | 316/316L SS | 4 |

BU was ignored.

### Mistake: Initial TFA size split was wrong

For LP1Y-TFA-176305-01, the assistant initially reported the wrong size split, even though the total remained 14. The intermediate correction treated a 2 x 3/4 weldolet as one 2 inch weld plus one 3/4 inch weld, and treated a 3/4 x 1/2 reducing tee as two 3/4 inch welds plus one 1/2 inch weld.

Then the user corrected the weldolet/olet rule again.

Final TFA decision:

| Size | Type | Material | Qty |
|---:|---|---|---:|
| 2 inch | BW | Hastelloy | 2 |
| 3/4 inch | BW | Hastelloy | 8 |
| 1/2 inch | BW | Hastelloy | 4 |

Total: 14 BW.

### Decision: Final weldolet / olet sizing rule

For weldolets and olet-type fittings, a BOM callout such as 2 x 3/4 means 2 inch header pipe with a 3/4 inch branch/olet. The header pipe size is not automatically the weld size.

Decision:

- Header-side connection is usually a BW at the branch/olet size.
- Branch-side connection is usually the branch/olet size.
- Branch-side type depends on fitting type: weldolet usually BW, sockolet SW, threadolet threaded.
- A 2 x 3/4 weldolet normally gives two 3/4 inch welds, not one 2 inch and one 3/4 inch.
- A 24 x 2 sockolet normally gives one 2 inch BW to the 24 inch header and one 2 inch SW to the 2 inch branch.
- Use a larger/header-size weld only if the drawing or BOM specifically marks an unusual reducing branch configuration.

### Decision: Reducing tee rule

A 3/4 x 1/2 reducing tee contributes two 3/4 inch connections and one 1/2 inch connection, assuming the drawing shows all three as welded connections.

Decision: Always check the BOM for reducing tees because linework may not reveal the size change.

### Mistake: STORMP size split was wrong

For LP1Y-STORMP-100412-01, the assistant first gave 2 x 4 inch BW and 21 x 2 inch BW. After BOM reconciliation, this was corrected to:

| Size | Type | Material | Qty |
|---:|---|---|---:|
| 4 inch | BW | 316/316L SS | 1 |
| 2 inch | BW | 316/316L SS | 22 |
| 3/4 inch | SW | 316/316L SS | 6 |

Total: 29 welds = 23 BW + 6 SW.

Note: The later weldolet/olet correction reinforces that branch-like fittings must be checked carefully by fitting type, BOM callout, and symbol. Do not generalize one branch fitting behavior to all branch fittings without source support.

### Decision: N2 drawing after recheck

For LP1Y-N2-176304-01:

| Size | Type | Material | Qty |
|---:|---|---|---:|
| 1 inch | BW | Hastelloy C-22 | 4 |
| 1/2 inch | BW | Hastelloy C-22 | 3 |

Total: 7 BW.

### Decision: PWP drawing after recheck

For LP1Y-PWP-014002-16:

| Size | Type | Material | Qty |
|---:|---|---|---:|
| 6 inch | BW | 316/316L SS | 11 |

Total: 11 BW. No straight segment exceeded the 20 ft stainless stock-length rule in the conversation.

### Decision: MEOH stock-length rule

For LP1Y-MEOH-111304-21, the drawn connections gave:

- 8 x 4 inch BW.
- 2 x 1 inch BW.

The stock-length check added two 4 inch BW pipe-to-pipe welds:

- One between connections 02 and 01 on the 35'-0" straight run.
- One between connections 03 and 07 on the 25'-7 1/2" straight run.

Final MEOH rollup:

| Size | Type | Material | Qty |
|---:|---|---|---:|
| 4 inch | BW | 316/316L SS | 10 |
| 1 inch | BW | 316/316L SS | 2 |

Total: 12 BW. No SW and no added couplings.

### Decision: Stock-length rules

- SS pipe stick length: 20 ft.
- CS pipe stick length: 40 ft.
- Add intermediate welds when a straight segment exceeds the stick length.
- For BW added joints, add BW only.
- For SW added joints, add SW plus same-size/same-material couplings.

### Decision: VANTAGE/agent design guidance

The desired AI agent should return individual detected connections first, not just totals. Each connection should include type, size, material, confidence, evidence, and ideally coordinates/bounding boxes so VANTAGE can show what was counted on the drawing.

Recommended batch inputs:

- Original PDF drawings.
- Connection-symbol examples.
- Counting rules.
- Material rules.
- BOM.
- Drawing legend.
- Line list/spec when needed.
- Validated drawings with known-good answers.

Recommended analysis approach:

- Render high-resolution crops of the original PDF.
- Trace piping and connection nodes.
- Classify BW/SW/BU symbols.
- Determine size and material.
- Cross-check with BOM.
- Deduplicate fitting-to-fitting joints.
- Add stock-length welds.
- Report uncertainty.

## Final example totals captured from the conversation

These are conversation-derived results, not a substitute for re-opening the drawings in a production QA workflow.

| Drawing | Size | Type | Material | Qty |
|---|---:|---|---|---:|
| LP1Y-TFA-176305-01 | 2 inch | BW | Hastelloy | 2 |
| LP1Y-TFA-176305-01 | 3/4 inch | BW | Hastelloy | 8 |
| LP1Y-TFA-176305-01 | 1/2 inch | BW | Hastelloy | 4 |
| LP1Y-N2-176304-01 | 1 inch | BW | Hastelloy C-22 | 4 |
| LP1Y-N2-176304-01 | 1/2 inch | BW | Hastelloy C-22 | 3 |
| LP1Y-PWP-014002-16 | 6 inch | BW | 316/316L SS | 11 |
| LP1Y-STORMP-100412-01 | 4 inch | BW | 316/316L SS | 1 |
| LP1Y-STORMP-100412-01 | 2 inch | BW | 316/316L SS | 22 |
| LP1Y-STORMP-100412-01 | 3/4 inch | SW | 316/316L SS | 6 |
| LP1Y-MEOH-111304-21 | 4 inch | BW | 316/316L SS | 10 |
| LP1Y-MEOH-111304-21 | 1 inch | BW | 316/316L SS | 2 |

Conversation total across these five drawings: 73 welds, consisting of 67 BW and 6 SW.
