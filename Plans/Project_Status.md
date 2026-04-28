# MILESTONE - Project Status

**Last Updated:** April 27, 2026

## Deferred to Post-V1
| Feature | Reason |
|---------|--------|
| Drawings in Work Packages | Per-WP location architecture needs design |
| AI Features (other than Progress Scan) | Lower priority for V1 |

## In Progress / Not Started
| Module | Status | Notes |
|--------|--------|-------|
| Analysis | IN PROGRESS | 3x1+3 grid layout, chart filters panel with persistence and Reset button, dynamic chart sections with selectable visual type/X axis/Y axis, pie/doughnut labels and legends, summary grid with independent filters, Excel export |
| Procore | IN PROGRESS | OAuth + auth dialog + service layer scaffolded; targeted at WP DWG Log fetch |
| AI Features (other) | NOT STARTED | Error Assistant, Description Analysis, etc. |

## Active Development

### Work Package Module
- Template editors testing
- PDF preview testing

### AI Takeoff — Multi-Drawing Documents
- **TODO:** Support drawing documents (PDFs) that contain multiple drawings per document (multiple pages). Currently each uploaded PDF is treated as a single drawing. Need to handle cases where one PDF contains several pages, each representing a different drawing.

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
- **Sync `claude-skills` repo on work machine.** Once the new global `firstSetup` skill is committed and pushed from `~/.claude/skills/` here, the work machine needs to pull `PrinceCorwin/claude-skills` to receive it. Same drill any time a global skill is added/edited on one machine — pull on the others to stay in sync.
- **Workstation cleanup: delete VANTAGE skills from global `~/.claude/skills/` on any remaining machines.** On 2026-04-21, `finisher`, `publisher`, `create-theme`, and `speedup` were moved from `~/.claude/skills/` to project-local `.claude/skills/` in this repo. Machines cleaned so far: 2026-04-21 machine (original move), 2026-04-23 this machine (pulled `claude-skills` fast-forward, no untracked `publisher/` existed, Step 3.7 UserSettings Registry Sync ported from the global `finisher/skill.md` to project-local before discard). On each remaining machine: pull the `claude-skills` repo (deletes the three tracked ones automatically) and manually delete the untracked `publisher/` folder from `~/.claude/skills/`. Then pull this repo to pick up the project-local copies.
- **Azure performance fixes (ProgressLog + Submit Week) — remaining off-hours work.** (1) `CommandTimeout = 0` on ProgressLog upload — **DONE 2026-04-21** (tiered: 3600s for big INSERT/UPDATE/DELETE, 120s for aggregates, 60s for single-row inserts; in `AdminSnapshotsDialog.xaml.cs`). (2) `ExportedBy` INCLUDE on `IX_ProgressSnapshots_Group_Lookup` — **DONE 2026-04-21** via SSMS. (3) **Blocked on Azure DB quota:** `VANTAGE_global_ProgressLog` clustered index — attempted 2026-04-21, failed at 13% because the table alone is 15.9 GB in a 30 GB DB (shared with pts_*, deliveryPlanner_* apps) and a HEAP→clustered rebuild needs ~2x the table size in scratch space. Paused operation was aborted cleanly, no data loss. **Revisit after Azure DB quota is increased** (or after archiving old ProgressLog rows to shrink the table). Fallback if we need the scan speedup sooner: add a nonclustered index on `[Timestamp]` instead (~300–500 MB, fits in current headroom) — script ready. (4) `VMS_ProgressSnapshots` clustered PK rebuild — **DONE 2026-04-21** (was 58.7% fragmented / 765 MB; rebuilt ONLINE with FILLFACTOR=90, now 0.01% / 402 MB).
- **Mobile/iOS Version (iPad)** — Execs want iPad app for field supes to submit progress. Needs architecture discussion: native iOS, cross-platform framework, web app, API design, offline sync, etc.
- **Takeoff Post-Processing Pipeline** — All operate on the downloaded Excel, no AWS changes needed. See `summit-takeoff-integration-guide.md` for details.
  1. Fabrication item generation — Connection rows, BOM fab records, PIPE/SPL records, ROCStep column complete. CUT/BEV no longer separate rows — their rates are folded into BW/SW/THRD connection rows. GSKT/BOLT excluded from labor. FLGLJ excluded from makeup. Fitting makeup lookup complete with olet support (WOL/SOL/TOL/ELB/LOL/NOL), class as string, Thickness fallback for olets. RED/SWG fallback to smaller pipe if unclaimed by larger. Missed Makeups tab has Reason column (No Makeup Found / Unclaimed). No Conns tab shows material items with no connections. **Dual-size matching:** All components try `ParseDualSize()` first; TEE/REDT match either size, others match larger size only. **STR makeup:** Strainers lookup as TEE using larger size, 2x multiplier (drain is not a pipe connection). **ShopField post-processing:** Lambda sets all material rows to 1 (Shop); post-processor corrects to Field (2) for: BU/SCRD-only connection types, FS/BOLT/GSKT/WAS/INST/GAUGE components, and items with no connections. PIPE stays Shop. Mixed connection types (e.g., BW+SCRD) stay Shop. Written back to Material worksheet.
  2. **Rate application** — Core implementation complete. Rate sheet keys shortened to match component names directly. Per-project rate overrides with management dialog, upload from Excel, RateSource column. Admin email notification for missed data. Simplified rate lookup: thickness as-is → toggle leading S → class rating → size-only. Dual-size parsing for all components (not just olets). Missed rates tab shows both thickness and class keys attempted. Material multipliers (MatlMult) and rollup multipliers (RollupMult) applied to labor MHs. BudgetMHs = (RateSheet × RollupMult × max(RollupMult, MatlMult) + CutAdd + BevelAdd) × Qty. Audit columns (RateSheet, RollupMult, MatlMult, CutAdd, BevelAdd) in Excel for user verification. **FS commodity code support:** Field supports (FS) now copy Commodity Code to Class Rating for rate lookup, enabling specialized rates like `SPT-4:A1234` with fallback to `SPT-4`. **THRD labor generation:** Every SCRD connection now generates a companion THRD labor row for threading labor. SCRD consolidated to 11 size-only entries, THRD has 44 entries (33 schedule-specific + 11 fallbacks).
  3. **ROC splits** — VMS_ROCRates table complete (Components column added for applicable component checklist). ROC Set Manager redesigned with view/edit modes, applicable components checklist panel, accessible via Tools menu. ROC set dropdown in ImportTakeoffDialog. Split logic implemented in import pipeline: matches rows by component (applicable list) + ShopField, original row gets first matching step, clones for remaining steps, BudgetMHs distributed by percentage. SPL rows set to ShopField=2 (Field) during takeoff post-processing. SCRD labor rows set to ShopField=2 (Field) during labor generation.
- **AWS Agent awareness items — revisit after the 434-drawing takeoff.** Deferred from the 2026-04-22 prompt/Lambda review. Three reference docs to review when picking these up: `Plans/AWS Agent/CHANGES.md` (the review's closed items), `Plans/AWS Agent/flagged-issues.md` (the original Tier 3 list summarized below), and `Plans/AWS Agent/deferred-issues-todo.md` (a follow-up backlog with additional small items — trivial cleanups, PEP-8 tidying, and triage suggestions — grouped per file). None of these are currently broken; they are defensive improvements and design refinements. Visit soon.
  1. **`class_rating` consensus backfill is exact-string, unweighted** — groups by raw description (case + whitespace sensitive), counts low-confidence votes the same as high-confidence. OCR variants never merge. Consider normalizing descriptions before grouping and weighting votes by confidence.
  2. **`bom_row_count` vs `len(bom_items)` no cross-check** — both come from the model and can silently disagree. Zero-BOM guard uses `bom_row_count` only. Trust `len(bom_items)` as ground truth and log a warning on mismatch.
  3. **Per-image `"Image N: {label}"` text block** — small token tax on every multi-image Bedrock call. Consider A/B-testing with the labels removed; strip if no accuracy drop.
  4. **No app-level retry around `bedrock.converse`** — only botocore adaptive retry (max_attempts=10) handles throttling/5xx. Add application-level retry with backoff if batch reliability ever becomes an issue at higher concurrency.
  5. **`render_pdf_page` memory guard assumes RGB** — `width * height * 3` under-counts RGBA. Currently safe because `alpha=False` is set. If alpha is ever enabled, switch to `width * height * 4` or derive from `pix.n`.
  6. **Excel column width sampled from first 100 rows only** — wide values in rows 101+ don't widen their column. Not worth changing unless users complain.
  7. **`batch_id` not sanitized before S3 key construction** — flows directly into S3 prefixes. Internal tool, controlled caller (Vantage app), low risk.
  8. **Hardcoded color constants inside `generate_excel`** — header fill `DAEEF3`, border style, etc. Hoist to module-level constants if more tabs/styles get added.


### Medium Priority
- **Help manual screenshots audit** — Review all sections of `Help/manual.html`, update outdated screenshots to reflect current UI, and add missing screenshots for features that have none (e.g., ActNO Split Ownership Check dialog, Sync Incomplete warning, any other recently added dialogs or UI changes).
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
- AI Sidebar Chat (see Sidebar_AI_Assistant_Plan.md) - conversational AI tab in the sidebar; UI scaffolding removed April 2026, plan doc retained for possible future revival

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
