# Vantage — Weld Takeoff Handoff Package

Handoff from a Claude (Cowork) session to Claude Code in the Vantage project.
Purpose: give Claude Code exactly what we intend and everything that happened, so
it can continue building an automated piping-isometric weld/connection takeoff.

## Intent

Replace the current BOM-driven takeoff (an AWS agent on an older model that
counts fitting ends from the BOM) with a method that **reads the isometric line
drawing itself** — counting physical weld joints by connection symbol, size, and
material, and adding stock-length welds the drawing doesn't show. The BOM is a
cross-check for component identity/size/type, never the counting engine.

Non-negotiable principle established this session: **do not rely on weld numbers.**
Drawings are often received for takeoff before QC numbers the welds. The takeoff
must be derivable from connection symbols + geometry + BOM alone.

## What's in this package

| File | What it is |
|---|---|
| `00_HANDOFF_README.md` | This file. |
| `01_master_takeoff_instructions.md` | **The operational spec.** The original ChatGPT working instructions with all session refinements integrated. This is the single source of truth Claude Code should follow. Revision Log at the bottom lists what changed. |
| `02_session_transcript.md` | Full transcript of the session — every user turn (verbatim) and assistant turn (substantive summary), including the two corrections and the results. |

The 5 source drawings are the reference examples the method was validated
against — attach them to the Claude Code project alongside this package.

## Symbol legend (core)

- Filled dot = **BW** (butt weld)
- Filled dot with fork/socket ticks = **SW** (socket weld)
- `BU-####` / flange face / `F,G,B` flags = **bolt-up**, not a weld (excluded)

## Corrected results the method must reproduce

| Drawing | Material | Expected takeoff |
|---|---|---|
| MEOH-111304-21 | 316/316L SS | 9 x 4" BW (incl. +2 stock) + 3 x 1" BW = 12 |
| PWP-014002-16 | 316/316L SS | 11 x 6" BW = 11 |
| STORMP-100412-01 | 316/316L SS | 1 x 4" BW + 19 x 2" BW + 3 x 3/4" BW + 6 x 3/4" SW = 29 |
| N2-107418-01 | 316/316L SS | 5 x 1" BW + 4 x 1/2" SW = 9 |
| N2-176304-01 | Hastelloy C22 | 4 x 1" BW + 3 x 1/2" BW = 7 |

Use these as regression cases: any change to the method must still produce these
numbers.

## Key lessons baked into the spec (why the numbers above are what they are)

1. **Read symbols, not weld numbers** — numbers are a late QC artifact, often
   absent.
2. **No phantom pipe-to-pipe weld at a branch** — a dot on a run at a branch is
   the olet/pipet header weld (branch size), counted once. (STORMP: 22 → 19.)
3. **Swage end-type follows the mating fitting**, not the BBE abbreviation;
   default swage = BW large / SW small. (N2-107418: small end is 1/2" SW.)
4. **Fitting-to-fitting = one weld** (elbow-to-valve, reducer-to-valve).
5. **Olet/pipet header + branch welds are at branch size**; an SW pipet = 3 welds
   (1 BW + 2 SW).
6. **Stock-length additions**: SS 20 ft / CS 40 ft;
   `ceil(run/stock) - 1`; SW adds need a coupling, BW adds do not.
7. **Material is per BOM item** (NOT per drawing) — a single drawing routinely
   carries multiple materials; never read one material and apply it sheet-wide, and
   never assume the first material found covers the rest.

## Suggested next steps for Claude Code

- Treat `01_master_takeoff_instructions.md` as the system/spec prompt.
- Keep a running changelog; append a new rule block each time a drawing exposes
  an edge case (reducing tees, ecc vs conc orientation, mitered ells, RTJ /
  flanged equipment tie-ins, threaded connections, etc.).
- Build the connection-level audit table as the intermediate output, then roll up
  to totals — this is the artifact to diff against the AWS agent.
- Re-run the 5 regression drawings after any spec change.
