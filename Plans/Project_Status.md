# MILESTONE - Project Status

**Last Updated:** January 31, 2026

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

### Help Sidebar — In Progress
Active work on `Help/manual.html`. Current state of each section:

**Completed sections (content written, screenshots placeholders converted to `<img>` tags):**
- 1. Getting Started
- 2. Main Interface
- 3. Progress Module (toolbar split into 3 section screenshots, sidebar filters documented, filter manager documented, SCAN button added)
- 4. Schedule Module (filter buttons, discrepancies, 3WLA, missed reasons all documented)

**Needs work:**
- 5. Progress Books — **Needs thorough redo.** Some instructions are vague or incorrect (e.g., references to wrong toolbar locations, workflow steps may not match current UI). Review every instruction against the actual app behavior. The SCAN button is on the Progress toolbar, not Progress Books toolbar.
- 6. Work Packages — Content written and detailed (tokens, form types, templates). Removed `wp-full-view.png` since Generate tab is the default view. Looks complete pending screenshot review.
- 7. Administration — Content needs writing
- 8. Reference — Content needs writing
- Troubleshooting section — Still says "Content coming soon..."

**Screenshots status:**
- 15 screenshots saved to `Help/` folder (set Build Action to Content / Copy if newer in VS Properties for each)
- Screenshots not yet captured: `wp-templates-tab.png`, `wp-form-cover.png`, `wp-form-list.png`, `wp-form-form.png`, `wp-form-grid.png`
- WebView2 uses virtual host mapping (`https://help.local/manual.html`) for image loading — see `SidePanelView.xaml.cs`
- VS sometimes re-adds PNGs as `<Resource Include>` — always set to Content / Copy if newer

**Other manual changes made this session:**
- Renamed "Help / AI Sidebar" → "Help Sidebar" everywhere (menu items and manual)
- Removed glossary section and TOC entry
- Added note about accessing Help Sidebar via F1 or settings menu
- Added note about active filters showing highlighted border
- Updated Today filter description (SchStart/SchFinish == today)
- Updated Budget dropdown description
- Updated Discrepancies description (VANTAGE vs P6 values)
- Swapped Progress Books (now section 5) and Work Packages (now section 6)

## Feature Backlog

### High Priority
- V1 production packaging (self-contained publish, initial ZIP distribution, credentials strategy)
- Set up GitHub Release with update manifest for auto-update testing
- Clear Filters button: remove persistent highlighted border; only show it when any filter is active (sidebar filters, column header filters, or Scan dialog Apply); remove border after clicking Clear Filters

### Medium Priority
- **DISCUSS:** Add PlanStart and PlanFinish fields to Activities (for baseline schedule comparison?)
- Table Summary V2: Settings dialog to choose which columns to summarize and aggregate types (Sum/Avg/Count)
- User-editable header template for WP (allow customizing header layout)
- Import/Export WP templates to JSON

### V2 Data Model
- Add ClientEarnedEquivQty column to Activities table, Azure VMS_Activities, and ColumnMappings (maps to OldVantage `VAL_Client_Earned_EQ-QTY`) - currently ignored during import

### V2 Architecture Revisit
- **Schedule CellStyle DataTrigger binding approach** -- The Schedule master grid uses `CellStyle` with `DataTrigger` bindings on 8 columns (MissedStartReason, MissedFinishReason, 3WLA Start/Finish, MS Start/Finish, MS %/MHs) to conditionally color cells red/yellow. These bindings reference bool properties on `ScheduleMasterRow` (e.g., `IsMissedStartReasonRequired`, `HasStartVariance`). Syncfusion's SfDataGrid cell recycling temporarily sets the GridCell DataContext to `ScheduleViewModel` instead of the row data, causing WPF Error 40 binding failures. Current fix: 8 dummy `=> false` properties on `ScheduleViewModel` (line ~30) so the binding resolves without error during the transient state. Proper fix would be replacing the simple `{Binding Path=PropName}` DataTrigger bindings with `MultiBinding` + `IMultiValueConverter` that type-checks the DataContext, preserving PropertyChanged reactivity. See `ScheduleView.xaml` lines 83-145 (styles) and `ScheduleViewModel.cs` dummy properties block.

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
- Find-Replace in Schedule Detail Grid - deferred to V2; may need redesign of main/detail grid interaction
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
