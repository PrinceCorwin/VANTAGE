## Context

The takeoff post-processor generates a Labor tab with BudgetMHs always null. We need to apply labor rates from the company rate sheet (universal, embedded as resource) to calculate manhours for each labor row. The rate sheet has 6,600+ entries keyed by composite key `GRP_SIZE_RTG` (e.g., `BW-20:STD`, `BU-8:150`).

## Component → Rate Sheet Mapping

| Our Component | Rate EST_GRP | SCH_RTG Source | Key Pattern |
|---|---|---|---|
| FSH (shop pipe) | SPOOL | Thickness | `SPOOL-{size}:{sch}` |
| FRH (field pipe) | PIPE | Thickness | `PIPE-{size}:{sch}` |
| BW | BW | Thickness | `BW-{size}:{sch}` |
| SW | SW | Thickness | `SW-{size}:{sch}` |
| OLW | OLET WLD | Class Rating | `OLET WLD-{size}:{class}` |
| BEV | BEVEL | Thickness | `BEVEL-{size}:{sch}` |
| CUT | CUT | Thickness | `CUT-{size}:{sch}` |
| BU | BU | Class Rating | `BU-{size}:{class}` |
| THRD | THRD | Thickness | `THRD-{size}:{sch}` |
| GSKT | GSKT | (none) | `GSKT-{size}` |
| BOLT | HARDWARE | (none) | `HARDWARE-{size}` |
| 90L/45L/TEE/CAP/etc | FTG | (none) | `FTG-{size}` |
| FLG/FLGB | FTG | (none) | `FTG-{size}` |
| WOL/SOL/TOL/etc (fab) | FTG | (none) | `FTG-{branch size}` |
| VBFL/VBL/valves | VLV | Class Rating | `VLV-{size}:{class}` |
| INST | INSTRUM | Class Rating | `INSTRUM-{size}:{class}` |

### Schedule Translation (Thickness → SCH_RTG)

- `STD` → `STD`, `XS` → `XS`, `XXS` → `XXS` (direct)
- Numeric schedules: prefix with "S" → `40`→`S40`, `80`→`S80`, `160`→`S160`

### Size for Dual-Size Components

- Olet fab records (WOL, SOL, etc.): use **branch/smaller** size for FTG lookup
- Connection rows already have the correct single size from explosion logic

## TODO: Deep-Dive Gap Analysis

Before implementing, need to thoroughly compare our labor output against the rate sheet to find all gaps:

- Verify every distinct Component value we generate has a valid mapping
- Check that all Thickness values we produce have corresponding SCH_RTG entries in the rate sheet
- Check that all Class Rating values we produce exist in the rate sheet
- Verify size ranges — do we generate any sizes that aren't in the rate sheet?
- Identify any rate sheet EST_GRP groups we might need but aren't mapped (e.g., FFW, STRESS, COAT & WRAP)
- Confirm GSKT and HARDWARE key patterns work (TYPE-SIZE only, no rating)
- Test VLV and INSTRUM lookups — do our valve/instrument records always have Class Rating populated?
- Review edge cases: empty Thickness, empty Class Rating, unusual sizes

## Implementation (after gap analysis)

### Step 1: Convert rate sheet Excel → JSON

- Parse `Plans/rateSheet.xlsx` into a JSON array with fields: `Key`, `EstGrp`, `Size`, `SchRtg`, `Unit`, `FldMhu`
- Save as `Resources/RateSheet.json`
- Add `<EmbeddedResource Include="Resources\RateSheet.json" />` to `VANTAGE.csproj`

### Step 2: Create `Services/AI/RateSheetService.cs`

- Lazy-load embedded JSON (same pattern as `FittingMakeupService`)
- Dictionary keyed by `GRP_SIZE_RTG` string → `double FldMhu`
- Public methods:
  - `LookupRate(string grpSizeRtg)` → `double?` — direct key lookup
  - `BuildLookupKey(string component, string size, string? thickness, string? classRating)` → `string` — maps our component to EST_GRP, determines SCH_RTG source, builds the composite key
- Component mapping dictionary inside the service

### Step 3: Apply rates in `TakeoffPostProcessor.cs`

- New method: `ApplyRates(List<Dictionary<string, object?>> laborRows)`
- Called in `GenerateLaborAndSummary()` after `GenerateLaborRows()` and before `WriteLaborTab()`
- For each labor row:
  1. Build lookup key from Component, Size, Thickness, Class Rating
  2. Look up FLD_MHU from rate sheet
  3. Set `BudgetMHs = Quantity × FLD_MHU` (or leave null if no match)
- Track missed rates (rows with no match) for diagnostics

### Step 4: Add "Missed Rates" tab

- Similar to existing Missed Makeups tab
- Columns: Drawing Number, Component, Size, Thickness, Class Rating, Lookup Key, Description
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
