# Labor Tab Generation Logic (C# App)

Generate Labor rows from Material tab data. Each Labor row = 1 connection.

## Input
Material tab from downloaded Excel (all BOM items)

## Output
Labor tab with one row per connection, ready for rate application

---

## Algorithm

```
FOR EACH materialRow in MaterialTab:

    // 1. Skip zero-connection items
    IF materialRow.ConnectionQty == 0 OR materialRow.ConnectionQty == null:
        CONTINUE

    // 2. VLV THRD/BU deduction
    IF materialRow.Component.ToUpper() == "VLV":
        connectionTypes = materialRow.ConnectionType.Split(',').Select(t => t.Trim())
        excludedCount = connectionTypes.Count(t => t.ToUpper() == "BU" OR t.ToUpper() == "THRD")
        keptTypes = connectionTypes.Where(t => t.ToUpper() != "BU" AND t.ToUpper() != "THRD")
        
        IF keptTypes.Count() == 0:
            CONTINUE  // All connections excluded
        
        connectionQty = materialRow.ConnectionQty - excludedCount
        connectionTypeList = keptTypes.ToList()
    ELSE:
        connectionQty = materialRow.ConnectionQty
        connectionTypeList = materialRow.ConnectionType.Split(',').Select(t => t.Trim()).ToList()

    // 3. Parse quantity (handle "41.3'" format)
    quantityStr = materialRow.Quantity.ToString().Replace("'", "").Replace("\"", "").Trim()
    quantity = (int)Math.Floor(double.Parse(quantityStr))
    IF quantity < 1: quantity = 1

    // 4. Distribute connections across types
    IF connectionTypeList.Count > 1 AND connectionQty >= connectionTypeList.Count:
        connectionsPerType = connectionQty / connectionTypeList.Count
        connectionList = new List<string>()
        FOREACH type in connectionTypeList:
            FOR i = 1 TO connectionsPerType:
                connectionList.Add(type)
        remainder = connectionQty % connectionTypeList.Count
        FOR i = 0 TO remainder - 1:
            connectionList.Add(connectionTypeList[i])
    ELSE:
        connectionList = Enumerable.Repeat(connectionTypeList[0], connectionQty).ToList()

    // 5. ShopField logic (per connection type, not per item)
    // Determined in step 7 for each individual connection

    // 6. Get PipeSpec from title block columns
    pipeSpec = materialRow.PIPE_SCHEDULE ?? materialRow.tb_Pipe_Spec ?? ""

    // 7. Explode: quantity × connections
    FOR instance = 1 TO quantity:
        FOREACH connType in connectionList:
            
            // ShopField per connection: BU = Field (2), else Shop (1)
            shopField = connType.ToUpper() == "BU" ? 2 : 1
            
            // Build concatenated description
            descParts = new List<string>()
            IF !string.IsNullOrEmpty(materialRow.Size):
                descParts.Add($"{materialRow.Size} IN")
            IF !string.IsNullOrEmpty(materialRow.Thickness):
                descParts.Add(materialRow.Thickness)
            IF !string.IsNullOrEmpty(pipeSpec):
                descParts.Add(pipeSpec)
            IF !string.IsNullOrEmpty(materialRow.Material):
                descParts.Add(materialRow.Material)
            IF !string.IsNullOrEmpty(materialRow.CommodityCode):
                descParts.Add(materialRow.CommodityCode)
            IF !string.IsNullOrEmpty(connType):
                descParts.Add(connType)
            
            concatDescription = string.Join(" - ", descParts)
            
            // Create Labor row
            laborRow = new LaborRow {
                DrawingNumber = materialRow.DrawingNumber,
                ItemId = materialRow.ItemId,
                Component = materialRow.Component,
                Size = materialRow.Size,
                ConnectionSize = materialRow.ConnectionSize ?? materialRow.Size,
                ConnectionType = connType,  // Single type, not comma-separated
                Thickness = materialRow.Thickness,
                ClassRating = materialRow.ClassRating,
                Material = materialRow.Material,
                CommodityCode = materialRow.CommodityCode,
                Description = concatDescription,
                RawDescription = materialRow.Description,
                Quantity = 1,  // Always 1
                ShopField = shopField,
                Confidence = materialRow.Confidence,
                Flag = materialRow.Flag,
                BudgetMHs = null,  // App fills this later
                // Copy all title block fields from material row
            }
            
            LaborTab.Add(laborRow)
```

---

## Examples (from actual extraction data)

| Material Row | Labor Rows Generated |
|--------------|---------------------|
| 1 ELL, 2 BW connections | 2 rows, ShopField=1 |
| 4 ELLs (qty=4), 2 BW connections each | 8 rows (4 × 2), ShopField=1 |
| 1 PIPET, 2 conn "OLW, BW" | 2 rows (1 OLW + 1 BW), ShopField=1 |
| 1 STUB, 1 BW connection | 1 row, ShopField=1 |
| 1 LAPFLG, 1 BU connection | 1 row, ShopField=2 |
| 1 BLFLG, 1 BU connection | 1 row, ShopField=2 |
| 1 VLV, 2 BU connections | 0 rows (BU excluded for VLV) |
| 1 VLV, 2 conn "BW, THRD" | 1 row (THRD excluded, 1 BW kept) |
| 1 VLV, 2 conn "BW, SW" | 2 rows (both welded, kept) |
| 1 PIPE, 0 connections | 0 rows (skipped) |
| 1 GSKT, 0 connections | 0 rows (skipped) |
| 1 BOLT, 0 connections | 0 rows (skipped) |
| 1 FS, 0 connections | 0 rows (skipped) |

---

## Labor Tab Columns

| Column | Source |
|--------|--------|
| Drawing Number | Material.DrawingNumber |
| Item ID | Material.ItemId |
| Component | Material.Component |
| Size | Material.Size |
| Connection Size | Material.ConnectionSize ?? Material.Size |
| Connection Type | Single type (exploded) |
| Thickness | Material.Thickness |
| Class Rating | Material.ClassRating |
| Material | Material.Material |
| Commodity Code | Material.CommodityCode |
| Description | Concatenated (built) |
| Raw Description | Material.Description |
| Quantity | Always 1 |
| ShopField | 1=Shop, 2=Field (BU=Field) |
| Confidence | Material.Confidence |
| Flag | Material.Flag |
| BudgetMHs | null (app fills) |
| [Title Block Fields] | Copied from Material |

---

## Summary Generation Logic

After generating Labor tab, compute summary stats:

```
FROM Material tab:
- total_drawings = Material.Select(r => r.DrawingNumber).Distinct().Count()
- total_bom_items = Material.Count()
- shop_items = Material.Count(r => r.ShopField == 1)
- field_items = Material.Count(r => r.ShopField == 2)
- flagged_count = Material.Count(r => r.Confidence == "low" || r.Confidence == "medium")
- components_by_type = Material.GroupBy(r => r.Component).ToDictionary(g => g.Key, g => g.Count())

FROM Labor tab:
- total_connections = Labor.Count()
- connections_by_type = Labor.GroupBy(r => r.ConnectionType).ToDictionary(g => g.Key, g => g.Count())
- connections_by_size = Labor.GroupBy(r => r.ConnectionSize).ToDictionary(g => g.Key, g => g.Count())
- connections_by_drawing = Labor.GroupBy(r => r.DrawingNumber).ToDictionary(g => g.Key, g => g.Count())
```

---

## Notes

- Zero-connection items (PIPE, GSKT, BOLT, FS, etc.) generate no Labor rows
- VLV exclusion: THRD and BU connections excluded, welded (BW, SW, GRV, OLW) kept
- ShopField is per-connection: BU = Field (2), all others = Shop (1)
- Each Labor row = exactly 1 connection
- Labor tab excludes: Connection Qty, Length (Material-only fields)
- Labor tab adds: BudgetMHs (for rate application)
