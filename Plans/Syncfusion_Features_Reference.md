# Syncfusion WPF Features Reference

Research compiled for MILESTONE project - January 25, 2026

---

## SfDataGrid Features

### Data & Performance

- **Data Virtualization** - Load millions of records with consistent performance
- **Row/Column Virtualization** - Only renders visible cells
- **Incremental Loading** - ISupportIncrementalLoading for on-demand data
- **LiveDataUpdateMode** - Real-time sorting/filtering/grouping during updates
- **PLINQ Processing** - Parallel processing for data operations
- **UseDrawing=Default** - Draw cells instead of TextBlock for 4K display performance
- **ScrollMode.Async** - Smooth scrolling for large datasets

### Filtering

- **Excel-like Filter UI** - Checkbox and advanced filtering
- **Filter Row** - Persistent filter row at grid top
- **Programmatic Filtering** - Via View.Filter delegate
- **FilterMode Options** - CheckboxFilter, AdvancedFilter, Both
- **CanGenerateUniqueItems** - Performance optimization for filter popups

### Grouping & Summaries

- **Multi-level Grouping** - Unlimited columns
- **Table Summary** - Top/bottom grid totals
- **Group Summary** - Aggregates per group
- **Caption Summary** - Summary in group header
- **Custom Aggregates** - User-defined calculations (weighted progress)
- **OnDemand Summary Calculation** - Calculate only when expanded
- **LiveDataUpdateMode.AllowSummaryUpdate** - Real-time summary recalculation

### Column Types

- GridTextColumn
- GridNumericColumn
- GridDateTimeColumn
- GridComboBoxColumn
- GridCheckBoxColumn
- GridTemplateColumn (custom templates)
- GridUnboundColumn (calculated fields)
- GridMaskColumn (input masking)
- GridHyperlinkColumn
- GridMultiColumnDropDownColumn

### Editing Features

- **IEditableObject Support** - Rollback on ESC key
- **CurrentCellValidating** - Cell-level validation
- **RowValidating** - Row-level validation
- **IDataErrorInfo/INotifyDataErrorInfo** - Model-based validation with error indicators
- **AddNewRow** - Built-in add row functionality
- **EditTrigger Options** - OnTap/OnDoubleTab

### Selection & Navigation

- Row/Cell selection modes
- Checkbox selection for bulk operations
- Excel-like navigation (arrow keys, tab)
- CurrentCell programmatic management

### Visual Features

- **Conditional Styling** - StyleSelector, Converters, DataTriggers
- **Stacked Headers** - Multi-row headers for grouping related columns
- **Merged Cells** - Span adjacent cells via QueryCoveredRange event
- **Frozen Columns/Rows** - Lock at edges (top/bottom/left/right)
- **Auto Row Height** - Fit content dynamically
- **Column Resizing/Reordering** - User customization
- **Column Chooser** - Show/hide columns UI

### Export & Print

- **ExportToExcel** - Full XlsIO integration with formatting
- **ExportToPdf** - Complete PDF export with customization
- **ExportToCSV** - Simple data exchange
- **Printing** - Direct print with options
- **RepeatHeaders** - Headers on each page
- Export preserves summaries, groups, stacked headers

### Serialization

- Serialize/Deserialize grid state to XML
- Preserves: Column order, width, visibility, filter/sort/group state
- Per-user layout persistence

### Row Drag & Drop

- **AllowDraggingRows** - Built-in row reordering
- Between grids drag support
- Custom drop handling via DragOver/Drop events
- Multiple row selection drag

---

## Charts (55+ Types)

### Most Relevant for MILESTONE

- **Column Chart** - Daily/weekly activity counts
- **Stacked Column** - Progress by area over time
- **Line Chart** - Trend analysis, S-curves
- **Spline Chart** - Smooth trend lines
- **Area Chart** - Cumulative progress
- **Pie/Doughnut** - Distribution by system/area
- **Bar Chart** - Comparison views
- **FastLineSeries** - Real-time high-frequency data (100k+ points)
- **Bubble Chart** - Multi-dimensional data
- **Funnel Chart** - Workflow stages
- **Waterfall Chart** - Progress changes

### Chart Features

- Data Labels
- Tooltips
- Trackball (cross-series tracking)
- Zoom/Pan interactive exploration
- Multiple Axes (dual Y-axis support)
- Annotations (target lines, text overlays)
- Legends (customizable positioning)
- Animation (load/update effects)
- Real-time Updates (live data binding)
- 100k+ data points performance

---

## Gantt Chart Control

- MS Project-like interface
- **Task Dependencies** - FS, SS, FF, SF link types
- **XML Import/Export** - MS Project compatibility (P6 data exchange)
- **Resource View** - Resources with inline tasks
- **Critical Path** highlighting
- **Task Drag/Drop** for rescheduling
- **Progress Indicator** - Visual % complete
- **Grid + Chart Split** - Adjustable widths
- **Custom Schedule** - Quarterly, numeric scales
- Filtering/Sorting built-in
- Export to images (JPEG, PNG, BMP)

---

## Scheduler Control

- Day/Week/Month/Timeline views
- Recurring events support
- Resource grouping (resources as rows)
- Drag & drop rescheduling
- Built-in appointment editor
- Reminders functionality
- Context menu for CRUD operations
- Multiple calendar types (Gregorian, Hijri, etc.)

---

## TreeGrid (SfTreeGrid)

- Self-referential data (parent-child via ID)
- Load on demand (expand to load)
- Checkbox selection at node level
- Row drag & drop between parents
- All SfDataGrid features available (filtering, editing, summaries)

---

## Gauges & KPI Controls

- **Radial Gauge** - Circular speedometer for progress %, quality scores
- **Linear Gauge** - Horizontal/vertical progress bars
- **Digital Gauge** - 7-segment display for counts
- **Bullet Graph** - Performance vs. target KPI tracking
- **Step Progress Bar** - Stage-based workflow status
- **OLAP Gauge** - KPI visualization from cubes

---

## Other Useful Controls

- **Docking Manager** - Visual Studio-like docking for flexible layouts
- **Ribbon** - Office-style toolbar
- **PropertyGrid** - Object property editor
- **Diagram** - Flowcharts, process flows
- **PDF Viewer** - View/annotate PDFs (isometric viewing)
- **Spell Checker** - Text validation
- **Kanban** - Card-based workflow visualization
- **Maps/Treemap/Heat Map** - Geographic/hierarchical visualization

---

## Themes & Styling

- 27+ built-in themes (FluentDark currently used, Office, VS styles)
- **Theme Studio** - Customize any theme
- **SfSkinManager** - Runtime theme switching
- **RTL Support** - Right-to-left layouts
- **Localization** - Multi-language support

---

## AI Integration

- AI-ready components with built-in suggestions
- AI Coding Assistant for development
- Predictive Analytics control for ML integration
- **AI AssistView** - Conversational UI component

---

## Performance Quick Wins (Not Yet Implemented)

```csharp
sfDataGrid.EnableDataVirtualization = true;
sfDataGrid.UseDrawing = UseDrawing.Default;     // High-DPI optimization
sfDataGrid.ScrollMode = ScrollMode.Async;        // Smooth scrolling
sfDataGrid.ColumnSizer = GridLengthUnitType.None; // Fastest sizing
sfDataGrid.SummaryCalculationMode = CalculationMode.OnDemandCaptionSummary;
```

---

## Documentation References

- Main docs: https://help.syncfusion.com/wpf/
- NuGet packages: 
  - Syncfusion.SfGrid.WPF
  - Syncfusion.SfChart.WPF
  - Syncfusion.Gantt.WPF
- GitHub demos: https://github.com/syncfusion/wpf-demos
