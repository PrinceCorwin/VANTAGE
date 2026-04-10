# MILESTONE - Project Status

**Last Updated:** April 10, 2026

## Deferred to Post-V1
| Feature | Reason |
|---------|--------|
| Drawings in Work Packages | Per-WP location architecture needs design |
| AI Features (other than Progress Scan) | Lower priority for V1 |
| Procore Integration | Can develop while users test |

## In Progress / Not Started
| Module | Status | Notes |
|--------|--------|-------|
| Analysis | IN PROGRESS | 4x1+3 grid layout, chart filters panel, dynamic chart sections with selectable visual type/X axis/Y axis, summary grid with independent filters, Excel export |
| AI Features (other) | NOT STARTED | Error Assistant, Description Analysis, etc. |

## Active Development

### Work Package Module
- Template editors testing
- PDF preview testing

### AI Takeoff — Multi-Drawing Documents
- **TODO:** Support drawing documents (PDFs) that contain multiple drawings per document (multiple pages). Currently each uploaded PDF is treated as a single drawing. Need to handle cases where one PDF contains several pages, each representing a different drawing.

### AI Takeoff — Second Size in Extraction Output
- **TODO:** Add a "Second Size" field to the AI extraction output for dual-size components (e.g., 6x4 reducers, tees). Currently only one Size field is extracted, requiring post-processing to parse dual sizes like "6x4". Having the Lambda/Claude output both sizes directly would eliminate the need for `ParseDualSize()` logic in `TakeoffPostProcessor`.

## Tutorial Videos

Series of short tutorial videos for end users. Each item below needs a plan and script developed in its corresponding folder under `Plans/Tutorials/`.

- [ ] **Intro Video** (`Plans/Tutorials/Intro/`) — Installation, updates, plugins, and the other MainWindow menu items (Tools, Settings, Help, status bar, etc.)
- [ ] **Progress Module Video** (`Plans/Tutorials/Progress/`) — Grid basics, sorting/grouping/filtering, editing, copy/paste, find/replace, sync, snapshots
- [ ] **Schedule Module Video** (`Plans/Tutorials/Schedule/`) — Master/detail layout, P6 import/export, editing, quick filters, UDF mapping, change log
- [ ] **Work Packages Video** (`Plans/Tutorials/WorkPackages/`) — Templates (WP and Form), tokens, generate tab, PDF output
- [ ] **Progress Books Video** (`Plans/Tutorials/ProgressBooks/`) — Layout configuration, column selection, generation, AI Progress Scan
- [ ] **Analyse Module Video** (`Plans/Tutorials/Analyse/`) — Summary grid, group by, user/project filters, metrics, conditional coloring
- [ ] **Takeoffs Module Video** (`Plans/Tutorials/Takeoffs/`) — AI-powered piping takeoff extraction, configs, regions, batches, downloads
- [ ] **Admin Video** (`Plans/Tutorials/Admin/`) — Edit Users, Edit Projects, Manage Snapshots, Progress Log, Project Rates, S3 Drawings (admin-only features)

## Feature Backlog

### High Priority
- **Mobile/iOS Version (iPad)** — Execs want iPad app for field supes to submit progress. Needs architecture discussion: native iOS, cross-platform framework, web app, API design, offline sync, etc.
- **Takeoff Post-Processing Pipeline** — All operate on the downloaded Excel, no AWS changes needed. See `summit-takeoff-integration-guide.md` for details.
  1. Fabrication item generation — Connection rows, BOM fab records, PIPE/SPL records, ROCStep column complete. CUT/BEV no longer separate rows — their rates are folded into BW/SW/THRD connection rows. GSKT/BOLT excluded from labor. FLGLJ excluded from makeup. Fitting makeup lookup complete with olet support (WOL/SOL/TOL/ELB/LOL/NOL), class as string, Thickness fallback for olets. RED/SWG fallback to smaller pipe if unclaimed by larger. Missed Makeups tab has Reason column (No Makeup Found / Unclaimed). No Conns tab shows material items with no connections. **Dual-size matching:** All components try `ParseDualSize()` first; TEE/REDT match either size, others match larger size only. **STR makeup:** Strainers lookup as TEE using larger size, 2x multiplier (drain is not a pipe connection). **ShopField post-processing:** Lambda sets all material rows to 1 (Shop); post-processor corrects to Field (2) for: BU/SCRD-only connection types, FS/BOLT/GSKT/WAS/INST/GAUGE components, and items with no connections. PIPE stays Shop. Mixed connection types (e.g., BW+SCRD) stay Shop. Written back to Material worksheet.
  2. **Rate application** — Core implementation complete. Rate sheet keys shortened to match component names directly. Per-project rate overrides with management dialog, upload from Excel, RateSource column. Admin email notification for missed data. Simplified rate lookup: thickness as-is → toggle leading S → class rating → size-only. Dual-size parsing for all components (not just olets). Missed rates tab shows both thickness and class keys attempted. Material multipliers (MatlMult) and rollup multipliers (RollupMult) applied to labor MHs. BudgetMHs = (RateSheet × RollupMult × max(RollupMult, MatlMult) + CutAdd + BevelAdd) × Qty. Audit columns (RateSheet, RollupMult, MatlMult, CutAdd, BevelAdd) in Excel for user verification. **FS commodity code support:** Field supports (FS) now copy Commodity Code to Class Rating for rate lookup, enabling specialized rates like `SPT-4:A1234` with fallback to `SPT-4`. **THRD labor generation:** Every SCRD connection now generates a companion THRD labor row for threading labor. SCRD consolidated to 11 size-only entries, THRD has 44 entries (33 schedule-specific + 11 fallbacks).
  3. **ROC splits** — VMS_ROCRates table complete (Components column added for applicable component checklist). ROC Set Manager redesigned with view/edit modes, applicable components checklist panel, accessible via Tools menu. ROC set dropdown in ImportTakeoffDialog. Split logic implemented in import pipeline: matches rows by component (applicable list) + ShopField, original row gets first matching step, clones for remaining steps, BudgetMHs distributed by percentage. SPL rows set to ShopField=2 (Field) during takeoff post-processing. SCRD labor rows set to ShopField=2 (Field) during labor generation.
  4. ~~VANTAGE tab~~ — Complete. Column mapping and Rollup Fab Per DWG handled in Import from AI Takeoff dialog.


### Medium Priority
- **Clean up Project_Status.md** — Sweep the doc and move every "Complete" / finished narrative section out to `Completed_Work.md` (or delete if already represented there). Sections to review include Plugin System, Multi-Theme System, Progress Book Module, AI Progress Scan, Work Package Module, Help Sidebar, AI Takeoff Multi Title Block Regions, and any other "Complete"-flagged blocks. Status doc should only contain in-progress work, todos, and the backlog — not narration of finished features (that's what `Completed_Work.md` is for, per CLAUDE.md workflow).
- **MSI/MSIX installer** — Replace custom installer with MSI (WiX Toolset) or MSIX packaging to get genuine Windows install integration. Current custom installer registers via registry but Windows Search won't execute `UninstallString` directly — only MSI and UWP/MSIX apps get direct uninstall from search context menu. Current setup works via Settings > Apps.
- **User-editable header template for WP** — Allow customizing header layout
- **Complete RateEquiv mappings** — Finish adding all component-to-EstGrp mappings in `RateSheetService.cs` (ComponentToEstGrp dictionary). Currently has valve types, fittings, GGLASS, METER, HOSE, HEAT→INST, etc.
- **Unify component reference tables** — Ensure all components are represented across CompRefTable, RateSheet.json, and FittingMakeup.json. Audit for missing entries and add equivalence mappings (MakeupEquiv in FittingMakeupService.cs) where components share identical values. SCRD/FLG (all sizes incl. 3"), wildcard SCRD/CPLG, classless FLG wildcards (BW/SW/SCRD), wildcard SCRD/TEE and SCRD/90L added. GRV→SW→BW makeup fallback chain. GAUGE excluded from makeup lookup.

### V2 Data Model
- Add ClientEarnedEquivQty column to Activities table, Azure VMS_Activities, and ColumnMappings (maps to OldVantage `VAL_Client_Earned_EQ-QTY`) - currently ignored during import

### AI Features (see InCode_AI_Plan.md)
| Feature | Status |
|---------|--------|
| AI Scan pre-filter | Not Started - Local image analysis to detect marks before calling API (skip blank pages, reduce cost) |
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

### Shelved
- Find-Replace in Schedule Detail Grid - deferred to V2; may need redesign of main/detail grid interaction
- Offline Indicator in status bar - clickable to retry connection
- Disable Tooltips setting (see DisableTooltips_Plan.md)
- Interactive Help Mode - click UI controls to navigate to documentation (see Sidebar_Help_Plan.md)

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
- Manage Snapshots: grouping by ProjectID + WeekEndDate + ProgDate, delete spinner
- Schedule Change Log: log detail grid edits, view/apply to Activities, duplicate handling
- Activity Import: auto-detects Legacy/Milestone format, date/percent cleanup, strict percent conversion
- Snapshot save: immediate spinner, date auto-fix, conditional NOT EXISTS, sync optimization, elapsed timer
- GitHub 2FA enabled — verified push access still works
