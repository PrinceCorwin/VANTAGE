# PRD: Table Summary Row for Numerical Columns

## Document Info

| Field | Value |
|-------|-------|
| Project | MILESTONE |
| Feature | Table Summary Row |
| Author | Claude AI |
| Date | January 25, 2026 |
| Status | Draft |

---

## Overview

Add a table summary row at the bottom of SfDataGrid instances displaying Sum totals for all numerical columns. This provides users with at-a-glance totals without manual calculation or Excel export.

## Problem Statement

Field engineers working with large datasets (thousands to 200k+ records) need quick visibility into totals for numerical data like quantities, completed counts, and progress percentages. Currently, users must either manually calculate or export to Excel to see column totals.

## Scope

### Grids to Implement

- Progress Module: `ProgressDataGrid`
- Schedule Module: `ScheduleDataGrid`
- Any future grids with numerical data

### In Scope

- Automatic Sum totals for all numerical columns
- Dynamic recalculation on filter/data changes
- Reusable helper for consistent implementation

### Out of Scope

- User-selectable aggregate types (Sum/Avg/Count)
- Custom aggregates (weighted averages)
- Group summaries
- Caption summaries
- Summary row at top position
- Persisting summary preferences per user

---

## Requirements

### Functional Requirements

#### FR-1: Summary Row Position

- Display at bottom of grid (below all data rows)
- Remains visible when scrolling (frozen at bottom)
- Respects current filter state (totals reflect filtered data only)

#### FR-2: Numerical Column Detection

- Automatically identify columns bound to numeric types:
  - `int`, `int?`
  - `long`, `long?`
  - `double`, `double?`
  - `decimal`, `decimal?`
  - `float`, `float?`
- Skip non-numeric columns (string, DateTime, bool, etc.)
- Skip calculated/unbound columns unless explicitly numeric

#### FR-3: Aggregation

- Use `Sum` for all numerical columns
- Use `DoubleAggregate` as SummaryType for flexibility
- Summary recalculates automatically when:
  - Data changes (add/edit/delete)
  - Filter applied/removed
  - Data refreshed from sync

#### FR-4: Performance

- Use `CalculationMode.OnDemand` for optimal performance with large datasets
- Target: No perceptible lag with 200k records

### Column-Specific Formatting

| Column Type | Format Code | Example Output |
|-------------|-------------|----------------|
| Integer (Quantity, Completed) | `N0` | 12,345 |
| Decimal (Hours, Weight) | `N2` | 1,234.56 |
| Percentage (PercentComplete) | `P1` | 85.6% |
| Currency (if applicable) | `C0` | $50,000 |

### Visual Requirements

- Summary row uses Syncfusion default styling (visually distinct from data rows)
- No prefix text required (column header identifies the data)
- Empty cell displayed for non-numeric columns in summary row

---

## Technical Specification

### Architecture

Create a reusable static helper class that can configure any SfDataGrid with table summaries based on the bound model type.

### Implementation Pattern

```csharp
// Usage in View code-behind
public partial class ProgressView : UserControl
{
    public ProgressView()
    {
        InitializeComponent();
        Loaded += (s, e) => GridSummaryHelper.AddTableSummary(ProgressDataGrid, typeof(Activity));
    }
}
```

### Core Components

#### GridSummaryHelper.cs (NEW)

Static helper class with the following responsibilities:

1. **Numeric Type Detection**
   - Maintain HashSet of supported numeric types
   - Use reflection to identify numeric properties on model

2. **Format Selection**
   - Determine appropriate format string based on:
     - Property type (int vs double)
     - Property name convention (contains "Percent")

3. **Summary Row Configuration**
   - Build `GridTableSummaryRow` with `Position.Bottom`
   - Set `ShowSummaryInRow = false` for column-based display
   - Create `GridSummaryColumn` for each numeric property

### Numeric Type Detection

```csharp
private static readonly HashSet<Type> NumericTypes = new()
{
    typeof(int), typeof(int?),
    typeof(long), typeof(long?),
    typeof(double), typeof(double?),
    typeof(decimal), typeof(decimal?),
    typeof(float), typeof(float?)
};

private static bool IsNumericType(Type type)
{
    var underlying = Nullable.GetUnderlyingType(type) ?? type;
    return NumericTypes.Contains(type) || NumericTypes.Contains(underlying);
}
```

### Format Selection Logic

```csharp
private static string GetSummaryFormat(Type propertyType, string propertyName)
{
    // Percentage columns (by naming convention)
    if (propertyName.Contains("Percent", StringComparison.OrdinalIgnoreCase) ||
        propertyName.EndsWith("Pct", StringComparison.OrdinalIgnoreCase))
    {
        return "{Sum:P1}";
    }
    
    var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
    
    // Decimal/floating-point types
    if (underlying == typeof(double) || 
        underlying == typeof(decimal) || 
        underlying == typeof(float))
    {
        return "{Sum:N2}";
    }
    
    // Integer types (int, long)
    return "{Sum:N0}";
}
```

### Summary Row Builder

```csharp
public static void AddTableSummary(SfDataGrid grid, Type modelType)
{
    var summaryRow = new GridTableSummaryRow()
    {
        Position = TableSummaryRowPosition.Bottom,
        ShowSummaryInRow = false,
        SummaryColumns = new ObservableCollection<ISummaryColumn>()
    };

    var numericProperties = modelType.GetProperties()
        .Where(p => IsNumericType(p.PropertyType))
        .ToList();

    foreach (var prop in numericProperties)
    {
        summaryRow.SummaryColumns.Add(new GridSummaryColumn()
        {
            Name = $"{prop.Name}Sum",
            MappingName = prop.Name,
            SummaryType = SummaryType.DoubleAggregate,
            Format = GetSummaryFormat(prop.PropertyType, prop.Name)
        });
    }

    grid.TableSummaryRows.Clear();
    grid.TableSummaryRows.Add(summaryRow);
}
```

---

## Files to Create/Modify

| File | Action | Description |
|------|--------|-------------|
| `Helpers/GridSummaryHelper.cs` | CREATE | Reusable summary configuration helper |
| `Views/ProgressView.xaml.cs` | MODIFY | Call helper after grid initialization |
| `Views/ScheduleView.xaml.cs` | MODIFY | Call helper after grid initialization |

---

## Testing

### Unit Tests

| Test Case | Expected Result |
|-----------|-----------------|
| Integer property detected | Returns true from IsNumericType |
| Nullable int detected | Returns true from IsNumericType |
| String property rejected | Returns false from IsNumericType |
| DateTime property rejected | Returns false from IsNumericType |
| PercentComplete formatting | Returns "{Sum:P1}" |
| Quantity formatting | Returns "{Sum:N0}" |
| Hours (double) formatting | Returns "{Sum:N2}" |

### Integration Tests

| Test Case | Expected Result |
|-----------|-----------------|
| Summary row visible | Row appears at grid bottom |
| Totals correct | Sum matches manual calculation |
| Filter applied | Totals reflect filtered subset only |
| Inline edit | Totals update immediately |
| Record deleted | Totals update immediately |
| Sync refresh | Totals recalculate with new data |
| 200k records | Summary calculates within 500ms |

### Manual Testing Checklist

- [ ] Totals display correctly for all numeric columns
- [ ] Totals update when filter applied
- [ ] Totals update after inline edit
- [ ] Totals update after bulk edit
- [ ] Totals update after record deletion
- [ ] Totals update after sync (new/modified records)
- [ ] Non-numeric columns show empty in summary row
- [ ] Percentage columns display as percentages (85.6%), not decimals (0.856)
- [ ] Large dataset (200k records) - verify no UI freeze
- [ ] Summary row remains frozen when scrolling vertically

---

## Rollout Plan

### Phase 1: Core Implementation

1. Create `GridSummaryHelper.cs`
2. Add unit tests for type detection and formatting
3. Implement in Progress module
4. Verify with test data

### Phase 2: Expansion

1. Implement in Schedule module
2. Document for future grid implementations

---

## Success Metrics

| Metric | Target |
|--------|--------|
| Summary calculation time (200k records) | < 500ms |
| User feedback | Positive reception in testing |
| Code reusability | Single helper works for all grids |

---

## Dependencies

- Syncfusion.SfGrid.WPF (version 31.2.12)
- Syncfusion.Data (for ISummaryColumn, SummaryType)

---

## Appendix

### Syncfusion Summary Types Reference

| SummaryType | Available Functions |
|-------------|---------------------|
| CountAggregate | Count |
| Int32Aggregate | Count, Max, Min, Average, Sum |
| DoubleAggregate | Count, Max, Min, Average, Sum |
| Custom | User-defined via ISummaryAggregate |

### Format String Reference

| Specifier | Description | Example |
|-----------|-------------|---------|
| N0 | Number, no decimals | 12,345 |
| N2 | Number, 2 decimals | 12,345.67 |
| P1 | Percent, 1 decimal | 85.6% |
| C0 | Currency, no decimals | $50,000 |
| C2 | Currency, 2 decimals | $50,000.00 |
