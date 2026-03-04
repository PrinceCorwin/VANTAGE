# Plan: Labor & Summary Tab Generation

## Context
AWS takeoff lambda now outputs only Material + Flagged tabs. We need to generate the Labor tab (one row per connection, exploded from Material) and Summary tab (stats from both) in the C# app after download.

## Files to Create
- `Services/AI/TakeoffPostProcessor.cs` — static class, all logic here

## Files to Modify
- `Views/TakeoffView.xaml.cs` — call post-processor after download in `BtnDownload_Click`

## Implementation

### TakeoffPostProcessor.cs

**Step 1: Read Material tab**
- Open workbook with ClosedXML
- Read header row dynamically (column name → index map)
- Read all data rows into list of dictionaries

**Step 2: Generate Labor rows** (per `Plans/labor_generation_logic.md`)
- For each material row:
  - Skip if connection_qty == 0 or null
  - VLV rule: exclude THRD and BU connections, adjust connection count
  - Parse quantity (handle `41.3'` format — strip quotes, floor to int, min 1)
  - Distribute connections across types (divide evenly, remainder to first types)
  - Explode: quantity × connections = labor rows, each with qty=1
  - ShopField per connection: BU=2 (Field), all others=1 (Shop)
  - Build concat description: `{size} IN - {thickness} - {pipeSpec} - {material} - {commodityCode} - {connType}`
  - RawDescription = original Material.Description
  - ConnectionSize defaults to Material.Size if missing
  - Copy all other columns from material row dynamically (title block fields vary by config — any column not in the explicit exclude list gets carried over)
  - Exclude from Labor: connection_qty, length, quantity (material-only fields)
  - Add BudgetMHs column (null/empty for now)

**Step 3: Write Labor tab**
- Create new worksheet "Labor"
- Write header row with defined column order
- Write all generated labor rows

**Step 4: Generate Summary tab** (per `Plans/summary_generation_logic.md`)
- From Material: total_drawings, total_bom_items, shop/field counts, flagged counts, components by type
- From Labor: total_connections, connections by type, by size, by drawing
- Write as labeled rows (not a data grid — more of a report layout)

**Step 5: Save workbook**

### TakeoffView.xaml.cs changes
- After `DownloadExcelAsync` completes, call `TakeoffPostProcessor.GenerateLaborAndSummary(dialog.FileName)` on background thread
- Add `using System.Threading.Tasks` if missing
- Update status: "Generating labor records..." → "Downloaded and processed: {path}"

## Labor Tab Columns (in order)
| Column | Source |
|--------|--------|
| Drawing Number | Material.drawing_number |
| Item ID | Material.item_id |
| Component | Material.component |
| Size | Material.size |
| Connection Size | Material.connection_size ?? Material.size |
| Connection Type | Single type (exploded) |
| Thickness | Material.thickness |
| Class Rating | Material.class_rating |
| Material | Material.material |
| Commodity Code | Material.commodity_code |
| Description | Concatenated (built) |
| Raw Description | Material.description |
| Quantity | Always 1 |
| ShopField | 1=Shop, 2=Field |
| Confidence | Material.confidence |
| Flag | Material.flag |
| BudgetMHs | empty |
| [Title block fields] | Copied dynamically from material row |

## Key Edge Cases
- VLV with mixed connection types (e.g., "BW, THRD") — keep BW, drop THRD
- VLV with only BU/THRD — skip entirely (0 labor rows)
- Quantity format `41.3'` — strip trailing quote, parse as double, floor to int
- Missing connection_size — fall back to size
- Title block columns vary by config — copy dynamically (any column not in the explicit exclude list)

## Verification
1. Run a batch with the updated lambda (Material + Flagged only output)
2. Download the Excel — post-processor should run automatically
3. Check Labor tab: correct row count (qty × connections), VLV exclusions, ShopField values, descriptions
4. Check Summary tab: stats match manual count
5. Verify Material and Flagged tabs are untouched

## Future Steps (not in this plan)
- Fab records: Cut (1 per weld), Bevel (2 per BW), FSH, FRH — added to Labor tab after this works
- FittingMakeup SQLite table for FRH qty adjustment
- ROC splits on FSH/FRH
- Rate sheet upload + BudgetMHs calculation
- Non-pipe shop records
