# MILESTONE - Project Status

**Last Updated:** January 28, 2026

## V1 Testing Scope

### In Scope
| Feature | Status |
|---------|--------|
| Progress Module | Ready for testing |
| Schedule Module | Ready for testing |
| Sync | Complete |
| Admin | Complete |
| Work Package (PDF generation) | Ready for testing (drawings hidden) |
| Help Sidebar | Content writing in progress |
| Progress Book creation | Ready for testing |
| AI Progress Scan | Complete - AWS Textract, 100% accuracy |

### Deferred to Post-V1
| Feature | Reason |
|---------|--------|
| Drawings in Work Packages | Per-WP location architecture needs design |
| AI Features (other than Progress Scan) | Lower priority for V1 |
| Theme Selection | Lower priority |
| Procore Integration | Can develop while users test |

## Module Status

| Module | Status | Notes |
|--------|--------|-------|
| Progress | READY FOR TESTING | Core features complete |
| Schedule | READY FOR TESTING | Core features complete |
| Sync | COMPLETE | Bidirectional sync working |
| Admin | COMPLETE | User/project/snapshot management |
| Work Package | READY FOR TESTING | PDF generation working; Drawings deferred to post-v1 |
| Help Sidebar | IN DEVELOPMENT | Search, action buttons complete; content writing in progress |
| AI Progress Scan | COMPLETE | AWS Textract implementation - 100% accuracy |
| AI Features (other) | NOT STARTED | Error Assistant, Description Analysis, etc. |

## Active Development

### Progress Book Module
- Phases 1-6 complete: Data models, repository, layout builder UI, PDF generator, live preview, generate dialog
- PDF features: Auto-fit column widths, description wrapping, project description in header
- Layout features: Separate grouping and sorting, up to 10 levels each, exclude completed option

### AI Progress Scan - AWS Textract Implementation (COMPLETE)

**Current State:**
- Switched from Claude Vision API to AWS Textract for table extraction
- 100% accuracy achieved on PDF and JPEG scans
- Simplified PDF layout: ID first, single % ENTRY column at far right

**PDF Layout:**
```
| ID | [user cols] | MHs | QTY | REM MH | CUR % | % ENTRY |
```
- ID (ActivityID) as first column - protected from accidental marks
- Data columns: MHs (BudgetMHs), QTY (Quantity), REM MH, CUR %
- % ENTRY box at far right - natural stopping point for field hands
- Writing "100" = done (no checkbox needed)

**Key Files:**
- `Services/AI/TextractService.cs` - AWS Textract API wrapper
- `Services/AI/PdfToImageConverter.cs` - PDF to image conversion (Syncfusion)
- `Services/AI/ProgressScanService.cs` - Scan orchestration
- `Services/ProgressBook/ProgressBookPdfGenerator.cs` - PDF generation

**Enhancements:**
- Image preprocessing with contrast adjustment (slider in results dialog, default 1.2)
- OCR heuristic: "00" auto-converts to "100" (handles missed leading 1)
- Results grid: column filtering, BudgetMHs column, Select All/Select Ready/Clear buttons

### Work Package Module
- Template editors testing
- PDF preview testing

### Help Sidebar
- Write Progress Books content
- Write Administration content
- Write Reference content
- Capture screenshots (45+ total)

## Feature Backlog

### High Priority
- Find-Replace in Schedule Detail Grid

### Medium Priority
- **DISCUSS:** Add PlanStart and PlanFinish fields to Activities (for baseline schedule comparison?)
- Table Summary V2: Settings dialog to choose which columns to summarize and aggregate types (Sum/Avg/Count)
- User-editable header template for WP (allow customizing header layout)
- Import/Export WP templates to JSON

### V2 Data Model
- Add ClientEarnedEquivQty column to Activities table, Azure VMS_Activities, and ColumnMappings (maps to OldVantage `VAL_Client_Earned_EQ-QTY`) - currently ignored during import

### AI Features (see InCode_AI_Plan.md)
| Feature | Status |
|---------|--------|
| AI Progress Scan | Complete - AWS Textract, 100% accuracy |
| AI Scan pre-filter | Not Started - Local image analysis to detect marks before calling Claude API (skip blank pages, reduce API cost) |
| ClaudeApiService infrastructure | Complete (via Progress Scan) |
| AI Error Assistant | Not Started |
| AI Description Analysis | Not Started |
| Metadata Consistency Analysis | Not Started |
| AI MissedReason Assistant | Not Started |
| AI Schedule Analysis | Deferred |

### AI Sidebar Chat (see Sidebar_AI_Assistant_Plan.md)
| Phase | Status |
|-------|--------|
| Chat UI | Not Started |
| Conversation Management | Not Started |
| Tool Definitions | Not Started |
| Tool Execution | Not Started |

### Post-V1: Drawings Architecture
- Design per-WP drawing location system (options: token paths, per-WP config, Drawings Manager)
- Fix preview display
- Fix layout/orientation for 11x17 drawings
- Implement Procore fetch
- Consider AI-assisted drawing matching (DwgNO formats, revisions, sheet numbers)

**Code disabled for v1 (re-enable when drawings architecture is ready):**
| File | What to re-enable |
|------|-------------------|
| `WorkPackageView.xaml` | Remove `Visibility="Collapsed"` from Drawings section Border (~line 238) |
| `WorkPackageView.xaml.cs` | Remove `.Where(t => t.TemplateType != TemplateTypes.Drawings)` from `PopulateAddFormMenu()` |
| `WorkPackageView.xaml.cs` | Remove filter in `ApplyWPFormsListFilter()` method |
| `WorkPackageView.xaml.cs` | Remove early return in `BuildDrawingsEditor()` and `#pragma warning` directives |
| `DrawingsRenderer.cs` | Remove early return in `Render()` method and `#pragma warning` directives |

### Syncfusion Features to Evaluate

**Dashboard Module (Post-V1):**
| Feature | Description |
|---------|-------------|
| Column/Stacked Chart | Daily/weekly activity completion counts, productivity trends |
| S-Curve (Line/Area Chart) | Planned vs actual progress over time |
| Pie/Doughnut Chart | Distribution by WorkPackage, PhaseCode, or RespParty |
| Gantt Chart | Visual schedule with dependencies (complements P6 import) |
| Radial Gauge | Dashboard widget for overall project % complete |
| Bullet Graph | Performance vs target KPIs per work package |

**V2:**
| Feature | Description |
|---------|-------------|
| Docking Manager | Visual Studio-like docking for flexible panel/toolbar layouts |

**Schedule Module:**
| Feature | Description |
|---------|-------------|
| TreeGrid (SfTreeGrid) | Hierarchical WBS display with parent/child relationships |
| Critical Path Highlighting | Auto-highlight critical path activities (P6 provides float data) |

**Evaluated and Removed:**
- Column Chooser - current checkbox popup is more intuitive
- Stacked Headers - adds complexity without benefit
- Custom Aggregates - Summary Panel already shows weighted progress
- Row Drag & Drop - conflicts with P6 sync and data model
- Checkbox Selection - Ctrl+Click multi-select is sufficient
- Export/Print - Syncfusion printing issues; use Progress Books instead

### Shelved
- Offline Indicator in status bar - clickable to retry connection
- Disable Tooltips setting (see DisableTooltips_Plan.md)
- Interactive Help Mode - click UI controls to navigate to documentation (see Sidebar_Help_Plan.md)

## Known Issues

### AI Progress Scan - Accuracy Issues (RESOLVED)
- ~~PDF scans less accurate than JPEG scans~~ Fixed with AWS Textract
- ~~Checkmarks sometimes missed~~ Removed checkbox, use % entry instead
- Now achieving 100% accuracy on both PDF and JPEG scans

## Test Scenarios Validated

- Import -> Edit -> Sync -> Pull cycle
- Multi-user ownership conflicts
- Deletion propagation
- Metadata validation blocking
- Offline mode with retry dialog
- P6 Import/Export cycle
- Schedule filters and conditional formatting
- Detail grid editing with rollup recalculation
- Email notifications
- Admin dialogs (Users, Projects, Snapshots)
- UserSettings export/import with immediate reload
- Log export to file and email with attachment
- User-defined filters create/edit/delete and apply
- Grid layouts save/apply/rename/delete and reset to default
- Prorate MHs with various operation/preserve combinations
- Discrepancy dropdown filter
- My Records Only sync (toggle on/off, full re-pull on disable)
- Work Package PDF generation and preview
- Manage Snapshots: delete multiple weeks, revert to single week with/without backup
- Schedule Change Log: log detail grid edits, view/apply to Activities, duplicate handling
- Activity Import: auto-detects Legacy/Milestone format, date/percent cleanup, strict percent conversion
