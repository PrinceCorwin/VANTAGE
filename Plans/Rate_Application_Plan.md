## Context

The takeoff post-processor generates a Labor tab with BudgetMHs always null. We need to apply labor rates from the company rate sheet (universal, embedded as resource) to calculate manhours for each labor row. The rate sheet has 6,600+ entries keyed by composite key `GRP_SIZE_RTG` (e.g., `BW-20:STD`, `BU-8:150`).

## Decisions

- **Rate sheet is universal** — same rates for all projects. Embedded as JSON resource.
- **Rate sheet stays untouched** — we use a mapping dictionary in code to translate our components to rate sheet EST_GRP keys. When they drop an updated rate sheet, we just re-convert to JSON.
- **Lookup order: Thickness first, then Class Rating** — Try building the key with Thickness as SCH_RTG. If no match found, retry using Class Rating. This covers items that may have one or the other (or both) from the drawing.
- **WT entries** in rate sheet are alternate keys for the same sizes (e.g., `.250" WT` instead of `STD`). Our schedule translation handles standard cases; WT values would only match if Claude extracts them that way.
- **Post-construction rates (XRAY, STRESS, FLUSH, COAT & WRAP)** — Not in scope now. These are not extracted from drawings; users will add manually later.
- **HEAT, HOSE, DPAN, F8B** — Need CompRefTable edits to give them connection types so they generate labor rows. Connection types TBD (user investigating).
- **CVLV** — Skipped for now. Control valves will map to VLV like other valves. Possible future add to distinguish them with a separate CompRefTable entry.
- **FS** maps to SUPPT for now. May break into support subtypes (SHOE, SPRING SUPPT, etc.) later.

## Component → Rate Sheet Mapping

### Generated Components (connection/fabrication rows)

| Our Component | Rate EST_GRP | Key Pattern |
|---|---|---|
| BW | BW | `BW-{size}:{sch}` |
| SW | SW | `SW-{size}:{sch}` |
| BU | BU | `BU-{size}:{class}` |
| THRD | THRD | `THRD-{size}:{sch}` |
| OLW | OLET WLD | `OLET WLD-{size}:{class}` |
| GRV | GRV | `GRV-{size}:{class}` |
| CUT | CUT | `CUT-{size}:{sch}` |
| BEV | BEVEL | `BEVEL-{size}:{sch}` |
| FSH | PIPE | `PIPE-{size}:{sch}` |
| FRH | SPOOL | `SPOOL-{size}:{sch}` |
| BOLT | HARDWARE | `HARDWARE-{size}` |
| WAS | HARDWARE | `HARDWARE-{size}` |
| GSKT | GSKT | `GSKT-{size}` |

### Fab Records — Fittings (→ FTG)

All keyed by size only: `FTG-{size}`. Olets (WOL, SOL, TOL, ELB, LOL) use **branch/smaller** size.

45L, 90L, 90LSR, ADPT, CAP, COV, CPLG, ELB, FLG, FLGA, FLGB, FLGLJ, FLGO, FO, LOL, NIP, PIPET, PLG, REDC, REDE, REDT, SOL, STR, STUB, TEE, TOL, TRAP, UN, WOL

### Fab Records — Special Mappings

| Our Component | Rate EST_GRP | Key Pattern |
|---|---|---|
| SWG | SWAGE CONC | `SWAGE CONC-{size}` |
| SAFSHW | SHOWER | `SHOWER-{size}` |
| ACT | OPERATOR | `OPERATOR-{size}` |
| FS | SUPPT | `SUPPT-{size}` |
| TUBE | TUBING | `TUBING-{size}` |

### Valves (→ VLV)

All keyed by size + class rating: `VLV-{size}:{class}`

VBF, VBFL, VBFO, VBL, VCK, VGL, VGT, VND, VPL, VPRV, VPSV, VRLF, VSOL, VSPL, VSW, VVNT, VYG

### Instruments (→ INSTRUM)

| Our Component | Rate EST_GRP | Key Pattern |
|---|---|---|
| INST | INSTRUM | `INSTRUM-{size}:{class}` |

### No Rate (not labor items — pending review)

| Component | Status |
|---|---|
| PIPE | Transformed into FSH/FRH, not rated directly |
| HEAT | Needs CompRefTable update — connection type TBD |
| HOSE | Needs CompRefTable update — connection type TBD |
| DPAN | Needs CompRefTable update — connection type TBD |
| F8B | Needs CompRefTable update — connection type TBD |

### Schedule Translation (Thickness → SCH_RTG)

- `STD` → `STD`, `XS` → `XS`, `XXS` → `XXS` (direct)
- Numeric schedules: prefix with "S" → `40`→`S40`, `80`→`S80`, `160`→`S160`
- Decimal wall thickness (e.g., `.250" WT`) — pass through as-is if extracted that way

### Rate Lookup Logic

1. Build key using **Thickness** as SCH_RTG: `{EST_GRP}-{size}:{thickness}`
2. If no match, retry using **Class Rating**: `{EST_GRP}-{size}:{class}`
3. If still no match, try size-only key: `{EST_GRP}-{size}` (for FTG, GSKT, HARDWARE, etc.)
4. If no match after all attempts, add to Missed Rates tab

## Rate Sheet Groups NOT in CompRefTable

### Likely to appear on piping ISOs — need CompRefTable entries

| Rate EST_GRP | What It Is | Priority |
|---|---|---|
| CVLV | Control valve (has actuator/positioner, different from standard valves) | High |
| ORIFICE | Orifice plate/assembly (distinct from orifice flange FLGO) | High |
| GAUGE | Pressure/temperature gauge | High |
| GAUGE GLASS | Level gauge glass | Medium |
| METER | Flow meter | Medium |
| METER RUN | Meter run assembly (straight pipe run for flow measurement) | Medium |
| PROBE | Thermowell/probe | Medium |
| XMTR | Transmitter (pressure, temperature, flow) | Medium |
| SADDLE | Pipe saddle/support fitting | Medium |
| RED FLG | Reducing flange (different bore than pipe size) | Medium |
| SCRD MU | Screwed/threaded makeup (threaded fitting makeup labor) | Low |
| VENT/DRAIN | Vent or drain assembly | Low |
| SHOE | Pipe shoe (may split from FS later) | Low |
| SPRING SUPPT | Spring hanger/support | Low |
| ANCHOR | Pipe anchor | Low |
| CLAMP | Pipe clamp | Low |

### Post-construction / non-BOM activities — not in scope now

Users will add these manually. No CompRefTable entry needed.

COAT & WRAP FTG, COAT & WRAP WLD, FFW, FW, FLUSH, STRESS, XRAY, QC-TEST, PENETRATION, SPEC

### Out of scope — equipment/structural, not piping

ANALYZER, EYEWASH, EYEWASH STATION, EYEWASH/SHOWER, INSTRUM STAND, JET PUMP, STRUT - HDC, SWAY SUPPT, T-SUPPT, VLV OPERATOR

### Already mapped from our components

SWAGE CONC (←SWG), SHOWER (←SAFSHW), OPERATOR (←ACT), SUPPT (←FS), TUBING (←TUBE)

## Implementation

### Step 1: Convert rate sheet Excel → JSON

- Parse `Plans/rateSheet.xlsx` into a JSON array with fields: `Key`, `EstGrp`, `Size`, `SchRtg`, `Unit`, `FldMhu`
- Save as `Resources/RateSheet.json`
- Add `<EmbeddedResource Include="Resources\RateSheet.json" />` to `VANTAGE.csproj`

### Step 2: Create `Services/AI/RateSheetService.cs`

- Lazy-load embedded JSON (same pattern as `FittingMakeupService`)
- Dictionary keyed by `GRP_SIZE_RTG` string → `double FldMhu`
- Component-to-EST_GRP mapping dictionary
- Public methods:
  - `LookupRate(string grpSizeRtg)` → `double?` — direct key lookup
  - `BuildLookupKey(string component, string size, string? thickness, string? classRating)` → `string` — maps component to EST_GRP, tries Thickness then Class Rating, builds composite key
- Fallback chain: Thickness key → Class Rating key → size-only key → null (missed)

### Step 3: Apply rates in `TakeoffPostProcessor.cs`

- New method: `ApplyRates(List<Dictionary<string, object?>> laborRows)`
- Called in `GenerateLaborAndSummary()` after `GenerateLaborRows()` and before `WriteLaborTab()`
- For each labor row:
  1. Build lookup key from Component, Size, Thickness, Class Rating
  2. Look up FLD_MHU using fallback chain
  3. Set `BudgetMHs = Quantity × FLD_MHU` (or leave null if no match)
- Track missed rates (rows with no match) for diagnostics

### Step 4: Add "Missed Rates" tab

- Similar to existing Missed Makeups tab
- Columns: Drawing Number, Component, Size, Thickness, Class Rating, Lookup Key Attempted, Description
- Only written if there are missed lookups
- Add to tab reorder list

## Files to Create/Modify

- **New:** `Resources/RateSheet.json` — embedded rate data
- **New:** `Services/AI/RateSheetService.cs` — rate lookup service
- **Modify:** `Services/AI/TakeoffPostProcessor.cs` — call ApplyRates, write Missed Rates tab
- **Modify:** `VANTAGE.csproj` — add EmbeddedResource entry

## Verification

1. Build project (`dotnet build`)
2. User runs a takeoff processing job
3. Check Labor tab — BudgetMHs column should be populated
4. Check Missed Rates tab — review any unmatched rows and refine mappings
5. Spot-check a few rates against the rate sheet Excel manually
