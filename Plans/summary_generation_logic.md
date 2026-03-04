# Summary Generation Logic (C# App)

Generate summary statistics from Material and Labor tabs.

---

## From Material Tab

```csharp
// Drawing counts
total_drawings = Material.Select(r => r.DrawingNumber).Distinct().Count();
drawing_numbers = Material.Select(r => r.DrawingNumber).Distinct().OrderBy(d => d).ToList();

// Item counts
total_bom_items = Material.Count();
shop_items = Material.Count(r => r.ShopField == 1);
field_items = Material.Count(r => r.ShopField == 2);

// Confidence/flagged counts
flagged_count = Material.Count(r => r.Confidence?.ToLower() is "low" or "medium");
low_confidence_count = Material.Count(r => r.Confidence?.ToLower() == "low");
medium_confidence_count = Material.Count(r => r.Confidence?.ToLower() == "medium");

// Components breakdown
components_by_type = Material
    .GroupBy(r => r.Component)
    .ToDictionary(g => g.Key, g => g.Count());
```

---

## From Labor Tab (after app generates it)

```csharp
// Total connections
total_connections = Labor.Count();

// Connections by type (BW, SW, BU, THRD, OLW, GRV)
connections_by_type = Labor
    .GroupBy(r => r.ConnectionType)
    .ToDictionary(g => g.Key, g => g.Count());

// Connections by size (1, 2, 6, 6x1, etc.)
connections_by_size = Labor
    .GroupBy(r => r.ConnectionSize)
    .ToDictionary(g => g.Key, g => g.Count());

// Connections by drawing
connections_by_drawing = Labor
    .GroupBy(r => r.DrawingNumber)
    .ToDictionary(g => g.Key, g => g.Count());
```

---

## Example Output

Given 1 drawing with 14 BOM items generating 12 Labor rows:

```
total_drawings: 1
drawing_numbers: ["LP1Y-APL(100)-034001-02"]
total_bom_items: 14
shop_items: 8
field_items: 6
flagged_count: 0
low_confidence_count: 0
medium_confidence_count: 0
components_by_type: {ELL: 1, PIPE: 1, PIPET: 1, STUB: 2, LAPFLG: 2, GSKT: 1, BOLT: 2, VLV: 1, BLFLG: 1, FS: 2}

total_connections: 12
connections_by_type: {BW: 6, OLW: 1, BU: 5}
connections_by_size: {6: 5, 1: 7}
connections_by_drawing: {"LP1Y-APL(100)-034001-02": 12}
```

---

## Notes

- Generate summary AFTER Labor tab is created
- Material stats don't require Labor tab
- Labor stats require Labor tab to exist
- Sort size numerically when displaying (handle "6x1" reducing sizes)
