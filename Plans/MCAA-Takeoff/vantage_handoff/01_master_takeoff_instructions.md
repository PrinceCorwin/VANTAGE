# PIPING ISOMETRIC WELD & CONNECTION TAKEOFF — MASTER WORKING INSTRUCTIONS

> Single source of truth for the automated weld/connection takeoff. This
> supersedes the original ChatGPT-authored prompt: it preserves that content and
> integrates the refinements established during the first drawing-review session.
> Changes from the original are listed in the **Revision Log** at the bottom.

---

## PURPOSE

Analyze piping isometric construction drawings and produce an accurate
connection/weld takeoff by physical connection location, connection type,
nominal size, and material. The drawing is the primary source for WHERE
connections exist. The Bill of Materials (BOM) is a required cross-check for
WHAT each connected item is, its actual size(s), material, and end-connection
type.

## CORE PRINCIPLE

**Count physical connection locations, not fitting ends.**

Do not estimate weld quantity by multiplying fitting quantities by the number of
fitting ends. That method double-counts fitting-to-fitting joints and misses
pipe-to-pipe welds. Trace the actual isometric and count each physical
connection node once.

## SCOPE — ROUND 1 (what the AI agent returns)

The round-1 deliverable is only: **per drawing, the count of weld connections with
their type (BW / SW), nominal size, and material.** That is what drives the charge.
Explicitly OUT of scope for now:

- **Shop vs field weld.** Do not classify or split shop/field. The field re-decides
  weld locations and marks up the drawings, so the original shop/field designation
  is not authoritative. Assume every weld is field for costing; a later pass
  (possibly feeding marked-up drawings to the AI) may assign the final shop/field
  split. Ignore the dot-vs-X/○/FFW *location* axis — read only butt-vs-socket.
- **Bolt-ups, grooved, and threaded connections** — deferred until BW/SW counting
  is proven against the regression drawings.

## CRITICAL: READ SYMBOLS, NOT WELD NUMBERS  *(governing rule)*

Weld ID balloons (numbered hexagons) are a **QC artifact applied late**. Drawings
are frequently received for takeoff **before** welds are numbered. Therefore:

- The takeoff MUST be derived entirely from **connection symbols + routed
  geometry + BOM**. Never depend on weld numbers being present.
- Do **not** use the balloon count as the weld count, and never assume a
  one-to-one relationship between balloons and joints.
- When balloons happen to exist, use them only as an after-the-fact cross-check.
- Validation test: assume the drawing has **zero** weld numbers and confirm the
  takeoff is still fully derivable from symbols and geometry. If it is not, the
  read is not finished.

---

## DEFINITIONS / CONNECTION TYPES

- **BW** = Butt Weld — plain (shop-)weld dot at a connection.
- **SW** = Socket Weld — weld dot carrying short socket tick marks (fillet/socket
  indicator). See the Symbol Legend for the full description and the "SW = shop
  weld" abbreviation clash.
- **BU** = Bolt Up — `BU-####` tag, `F/G/B` flags, or a flange face. **Not a
  weld.** Excluded from BW/SW totals unless bolt-ups are specifically requested.

These graphical conventions are strong clues but are not infallible; draftsmen
draw inconsistently. Always cross-check the BOM and the connected components
before finalizing a connection type.

## SYMBOL LEGEND (visual reference)

Validated against reference crops from the B1LG isometrics
(`Plans/vantage_handoff/BwSymbol*.png`, `SwSymbols90toPipeAnd90toSwage.png`) and
against standard piping-isometric drafting convention.

**First, what the dot means.** On an isometric a **solid filled dot is a weld-joint
marker**, and specifically a **shop weld** (welded in the fab shop). The general
notes on these drawings confirm it: "ALL WELDS ARE INDICATED AS SHOP WELDS." A
**field weld (FW)** is instead an **X or an open circle**; a **field-fit weld
(FFW)** is an **X with "FFW"/"F.F." text** (pipe left long for a field cut). Shop
vs field is a *location* attribute and is **orthogonal to butt-vs-socket** — they
are two independent axes; do not conflate them. The dot alone proves a weld
exists, not whether it is butt or socket.

> ⚠️ **Abbreviation clash:** on iso legends "SW" usually means **shop weld**. In
> THIS takeoff "SW" ALWAYS means **socket weld**. The agent must classify from the
> drawn mark, never from the letters "SW" appearing on the sheet.

**Butt vs socket — the type we actually count:**

- **BW (butt weld) — a plain weld dot** with nothing but the pipe lines touching
  it. The bare shop-weld dot is the entire butt-weld signature. It reads the same
  for pipe-to-pipe, pipe-to-fitting, fitting-to-fitting, and the header-to-branch
  (olet) attachment dot.
- **SW (socket weld) — a weld dot carrying short socket tick marks.** Two stubby
  line segments (~1 dot-diameter long), set slightly OFF the pipe axis on opposing
  sides of the dot, so the dot looks like it grew small "wings" / a half-bracket.
  A socket weld **is a fillet weld**; these ticks are the drawing's fillet/socket
  indicator (the single-line-iso degradation of the ASME fillet-weld symbol). On a
  swage, each socket node carries that tick beside its dot, with the swage body
  drawn as a thin hollow parallelogram between the two dots.
- **BU (bolt-up)** — `BU-####` tag, `F/G/B` flags, a drawn flange face, or two
  short parallel lines (`||`) across the pipe. Not a weld; excluded. (Deferred —
  get BW/SW right first.)

**Scale warning.** The socket ticks are only a few pixels wide at full-sheet
resolution, and some CAD isos degrade a socket weld to a bare point entirely. A
downscaled overview collapses SW into a plain dot — i.e. reads every socket weld
as a butt weld. Socket-vs-butt classification MUST be done from a high-DPI crop
zoomed on each node, never from the whole-sheet overview; when the ticks cannot be
resolved, fall to BOM reconciliation rather than defaulting to BW.

## SYMBOL vs BOM RECONCILIATION  (never rely on symbols alone)

Symbols locate and classify; the BOM must still confirm. Every found connection
is cross-checked against its BOM item so the two agree on end type. Do not
finalize a type from the symbol alone, and do not finalize it from the BOM alone.

Precedence when they conflict:

1. **A clearly legible drawn symbol holds the greatest weight for connection
   type.** If the symbol plainly shows SW (dot + ticks) or BW (bare dot), that is
   the type — even when a BOM end abbreviation implies otherwise. BOM end-prep
   abbreviations (`BBE`, `PBE`, `TBE`) describe the raw fitting as purchased, not
   the joint as installed.
2. **Use BOM reasoning to resolve an unclear or unreadable symbol, not to
   override a clear one.** The swage default (large BW / small SW), the
   mating-fitting rule, and reducing-fitting sizes all apply when the symbol
   cannot be read confidently.
3. **Flag as uncertain only when the symbol is illegible AND BOM reasoning cannot
   settle it** — never force a guess.

Worked example — the swage that set this rule:
- N2-107418-01: a `1 x 1/2` swage with BOM callout `BBE` (which implies BW both
  ends). But a swage exists to make a BW↔SW transition, the small end lands on a
  SW elbow, AND the drawn symbol shows SW ticks. Symbol + reasoning + mating
  fitting all agree → **small end = 1/2" SW.** `BBE` loses; it described the
  fitting, not the installed joint.
- Counter-case: if that same BOM said `BBE` but the drawing had actually shown a
  **plain BW dot** on the small end, the symbol wins the other way →
  **1/2" BW.** The legible symbol is decisive in both directions.

## SOURCE PRIORITY

1. **Drawing geometry / symbols** — the physical connection locations and how the
   piping is connected. Primary.
2. **BOM** — validates fitting identity, size, reductions, material, end type.
   Resolves ambiguity in what a symbol appears to show.
3. **Dimensions** — pipe segment lengths; drive stock-length additions.
4. **Line / title data** — service, nominal line size, material system.

Never use the BOM alone to generate the weld count. The BOM is supporting
evidence, not the counting engine.

---

## STEP-BY-STEP ANALYSIS METHOD

1. Identify drawing number, line/service, material, and main nominal size(s).
2. Locate **every** physical connection symbol/node on the isometric by tracing
   the routed geometry — every run end-to-end and every branch to its terminus.
   Do this from symbols alone; ignore any weld numbers.
3. Assign each physical connection a unique record. Count it once only.
4. Trace what component or pipe exists on each side of that connection.
5. Read the weld symbol: plain dot → BW; dot with fork/socket ticks → SW.
6. Cross-check the BOM for the connected fitting/component: end type (BW, SW,
   threaded, flanged), whether reducing, actual end sizes, material.
7. If drawing symbol and BOM disagree on connection type, apply SYMBOL vs BOM
   RECONCILIATION: a clearly legible symbol wins; use BOM reasoning only to
   resolve an unclear symbol; flag as uncertain only if the symbol is illegible
   and BOM reasoning cannot settle it.
8. After all drawn connection nodes are classified, inspect every continuous
   straight pipe segment between connections for stock-length requirements.
9. Add required stock-length welds/couplings.
10. Summarize by connection type + size + material.

---

## CRITICAL COUNTING RULES

- Count each physical joint once.
- A fitting-to-fitting weld is ONE weld (e.g., elbow-to-valve, reducer-to-valve),
  not one per fitting.
- A pipe-to-fitting weld is ONE weld.
- A pipe-to-pipe weld on a straight run is ONE weld even with no fitting in the
  BOM.
- Do not infer weld count from BOM fitting quantities.

### No phantom pipe-to-pipe weld at a branch node

A filled dot on a run **at a branch location** is the branch fitting's
header/attachment weld (weldolet, sockolet, pipet, etc.) at **branch size**.
Count it once as the header weld. Do **not** also count a pipe-to-pipe weld at
that same spot. A genuine pipe-to-pipe / random-length weld exists **only** where
a joint sits on an otherwise-continuous straight run with **no fitting and no
branch** at that point.

---

## SIZE DETERMINATION RULES

Do not assume a connection stays the line/header size because the linework looks
continuous. Almost any fitting can be reducing; the BOM may be the only place a
size change appears. Determine the actual weld size at each specific fitting end.

Examples:
- A 3/4 x 1/2 reducing tee: two 3/4 connections and one 1/2 connection.
- A 1 x 1/2 reducer/swage: one 1" end and one 1/2" end; classify weld type per
  end (see Swages/Reducers).
- Do not treat the first number in a branch-fitting callout as a weld size (see
  Branch/Olet rules).

## BRANCH CONNECTION / OLET / PIPET RULES

Branch fittings must always be cross-checked against the BOM.

- A `header x branch` callout (e.g., `2 x 3/4`) means a branch fitting on a
  2" header serving a 3/4" branch. The first number is the **header** size; it
  is NOT a 2" weld at the olet-to-header connection.
- Header-side attachment weld = **BW at the BRANCH size**.
- Branch-side connection is also at branch size; its type follows the fitting:
  - weldolet → BW branch side
  - sockolet / SW pipet → SW branch side
  - threadolet → threaded branch side
- A typical `2 x 3/4` SW pipet contributes **three welds**: one 3/4" BW at the
  header + one 3/4" SW pipet-outlet-to-nipple + one 3/4" SW nipple-to-SW-flange.
- Only assign a header-size weld to the branch attachment if the drawing/BOM
  specifically shows an unusual reducing branch (uncommon).

## REDUCING FITTINGS

Always inspect the BOM for reducing tees, concentric/eccentric reducers, swages,
reducing elbows, reducing branch fittings, and reducing valves. The isometric
line may not show the size change. Assign each weld by the actual fitting-end
size at that node.

## SWAGES vs REDUCERS  *(refined)*

A swage exists specifically to make a **BW ↔ SW transition**.

- **Default swage: large end BW, small end SW** — this default applies when the
  symbol is unclear. A **legible drawn symbol overrides the default** (see SYMBOL
  vs BOM RECONCILIATION). Classify each end by the component it lands on in the
  drawing — not by the `BBE`/`PBE`/`TBE` abbreviation, which describes the raw
  fitting, not the installed joint.
- If both ends were genuinely BW, a **concentric/eccentric reducer** would have
  been specified. A swage in the BOM is itself a signal that one end is socket.
- A **reducer** (`A403-W` conc/ecc): BW both ends — large-size BW + small-size
  BW.

> Worked example: a `1 x 1/2` swage with a `BBE` callout whose small end lands on
> a 1/2" SW ell → **1" BW** large end, **1/2" SW** small end. Not 1/2" BW.

## VALVES, FLANGES, STUB ENDS, NIPPLES, OTHER COMPONENTS

Use BOM descriptions as an end-type check:
- `BW` in a valve description → butt-weld ends (e.g., `BALL ... BW ...` = 2 BW).
- `SW` in an elbow/union/flange/valve → socket-weld ends.
- A **stub end** is welded to the pipe (1 BW at pipe size); the lap-joint flange
  over it is a bolt-up, not a weld.
- **WN flange**: 1 BW at the neck; the flange face is a bolt-up.
- BBE/PBE/TOE and similar end-prep abbreviations are supporting evidence only;
  confirm the actual drawn connection before converting to a weld.

## BOLT-UP CONNECTIONS

`BU` = bolt-up. Excluded from BW/SW totals unless the task specifically asks for
bolt-ups.

## MATERIAL RULES

Determine material from line data + BOM. Report at a useful construction level
(e.g., 316/316L SS, Carbon Steel / CS, Hastelloy C-22, Hastelloy C-276). Do not
assume a grade the source does not establish.

**Material is per BOM item, NOT per drawing.** A single drawing routinely carries
multiple materials across its line items. Do NOT read one material and apply it to
the whole sheet, and do NOT "find the first material and assume the rest." Resolve
each connection's material from its own component/BOM line. (This governs stock-length
choice too — SS vs CS stock length follows the individual run's material.)

---

## STOCK-LENGTH / RANDOM PIPE WELD RULES

After counting all drawn welds, inspect straight-pipe dimensions between physical
connection points.

- Stock lengths: **Stainless steel (SS) = 20 ft**, **Carbon steel (CS) = 40 ft**.
- For a continuous straight segment longer than the applicable stock length:

  `additional joints = ceil(segment length / stock length) - 1`

  - 35 ft SS → 1 added joint; 45 ft SS → 2; 25'-7 1/2" SS → 1; 65 ft CS → 1.
- Added-joint type matches the segment/end condition:
  - BW piping → add same-size/material **BW**, no coupling.
  - SW piping → add the SW joint **plus one same-size/material coupling per added
    joint**.
- If material is neither SS nor CS and no stock length is given, do not invent
  one — ask or flag.
- Stock-length welds are **additional** to the symbols already drawn.

---

## QUALITY CONTROL / RECONCILIATION  *(with weld numbers ignored)*

Before finalizing:
- Walk the entire routed line end-to-end and every branch to its terminus;
  confirm each connection node counted once and only once, from symbols alone.
- Reconcile both directions: every BOM fitting has its joints located on the
  drawing; every located joint maps to a component or a legitimate pipe-to-pipe
  weld.
- No BOM fitting end double-counted because two fittings meet at one joint.
- No pipe-to-pipe weld missed on straight pipe; no phantom pipe-to-pipe weld
  invented at a branch node.
- Every reducing fitting, swage, and branch re-checked against the BOM.
- BW/SW type confirmed from symbol AND BOM/component end type.
- Material confirmed per connection group.
- Straight-run dimensions checked against stock-length rules.
- Totals reconcile to (physical drawn nodes) + (explicit stock-length joints).

## UNCERTAINTY RULE

Do not force a guess when symbol, geometry, and BOM cannot be reconciled. Report,
e.g.: "Connection at [location]: likely 3/4 SW, but drawn symbol and BOM end
geometry conflict — review required." A transparent uncertain item beats a
confident wrong count.

---

## WORKING OUTPUT (audit trail)

Build a connection-level table before summarizing:

`Connection ID | Type | Size | Material | Upstream Item | Downstream Item | Drawing Evidence | BOM Evidence | Confidence`

(Connection ID is an internal index — NOT a drawing weld number.) This table is
the source of the final totals.

## FINAL RESPONSE FORMAT

Concise unless a detailed audit is requested. Example:

```
DRAWING: ABC-123  (Material: 316/316L SS)
- 8 x 2" BW
- 4 x 3/4" SW
- +1 x 2" BW (added for >20 ft straight SS run)
- +0 couplings
Total welds: 13
```

If requested, also provide the connection-by-connection breakdown.

---

## FAILURE MODES TO AVOID

1. Counting BOM fitting ends and calling that the weld count.
2. Doubling a fitting-to-fitting weld.
3. Missing a pipe-to-pipe weld because there is no fitting in the BOM.
4. Inventing a pipe-to-pipe weld at a branch node (that dot is the olet/pipet
   header weld).
5. Assuming all fittings are same-size.
6. Assuming the isometric line reveals every reduction.
7. Reading a `header x branch` olet callout as a header-size weld.
8. Classifying a swage from its BBE/PBE abbreviation instead of the mating
   fitting (swage = BW large / SW small by default).
9. Blindly trusting the dot/fork convention over a clear BOM end type.
10. Depending on weld numbers — they are often absent; read symbols.
11. Forgetting stock-length welds after the drawn takeoff.
12. Adding an SW stock-length joint without its coupling.
13. Assuming the material system instead of verifying per drawing.

## WORKING PHILOSOPHY

Use drawing geometry to find WHERE the physical joints are. Use the BOM to
understand WHAT is connected and WHAT SIZE/TYPE each end is. Use dimensions and
material rules to determine joints the fabricator must add that are not drawn.
The final answer represents physical construction joints — not a theoretical
count of fitting ends, and not a count of weld balloons.

---

## REVISION LOG (changes from the original ChatGPT prompt)

- **Added governing rule "Read symbols, not weld numbers."** Drawings are
  received before QC numbers welds; the takeoff must stand on symbols + geometry
  + BOM. Balloon counts are cross-check only.
- **Added "No phantom pipe-to-pipe weld at a branch node."** A dot on a run at a
  branch is the olet/pipet header weld (branch size), counted once — not an
  additional pipe weld. (Fix for the STORMP-100412-01 2" BW over-count: 22 → 19.)
- **Refined Swages vs Reducers.** Swage default = BW large / SW small; classify
  by the mating fitting, not the BBE abbreviation. (Fix for N2-107418-01: the
  1x1/2 swage small end is 1/2" SW into a SW ell, not 1/2" BW.)
- **Reinforced olet/pipet = branch-size welds**, and that an SW pipet contributes
  3 welds (1 BW header + 2 SW).
- **Corrected material granularity: material is per BOM item, NOT per drawing.**
  A single drawing routinely carries multiple materials; never read one material
  and apply it sheet-wide, and never assume the first material found covers the
  rest. (Supersedes the earlier "material is per-drawing" wording. N2-176304-01
  being Hastelloy C22 rather than SS is an example of verifying material, not of
  a whole-drawing material.)
- Consolidated QC/failure-mode lists to reflect the above.
- **Locked Round-1 scope:** output = count + type (BW/SW) + size + material per
  drawing. Shop-vs-field explicitly out of scope (assume all field; the field
  re-marks weld locations); bolt-up/grooved/threaded deferred until BW/SW is proven.
- **Added Symbol Legend (visual reference)** validated against reference crops and
  standard iso convention: the filled dot is a shop-weld joint marker; BW = bare
  dot; SW = dot + short socket tick marks (a socket weld is a fillet weld, these
  are its fillet/socket indicator). Documented that shop-vs-field (dot vs X/○/FFW)
  is a *location* axis orthogonal to butt-vs-socket, and flagged the abbreviation
  clash that "SW" on iso legends usually means *shop weld* (read the mark, not the
  letters). Added the scale warning that socket ticks disappear at overview
  resolution — classify from high-DPI per-node crops, and fall to BOM when the
  ticks can't be resolved rather than defaulting to BW.
- **Added "Symbol vs BOM Reconciliation (never rely on symbols alone)."** Every
  connection is cross-checked against its BOM item. Precedence: a clearly legible
  drawn symbol holds greatest weight for type; BOM reasoning (swage default,
  mating fitting) resolves only an unclear symbol; flag uncertain only when the
  symbol is illegible and BOM reasoning cannot settle it. The legible symbol is
  decisive in BOTH directions — a plain BW dot on a BBE-swage small end would make
  it BW. Refined method step 7 and the Swages section accordingly.
