# MILESTONE - Project Status

**Last Updated:** March 13, 2026

## V1 Testing Scope

### In Scope
| Feature | Status |
|---------|--------|
| Progress Module | Ready for testing |
| Schedule Module | Ready for testing |
| Sync | Complete |
| Admin | Complete |
| Work Package (PDF generation) | Ready for testing (drawings hidden) |
| Help Sidebar | Complete for V1 |
| Progress Book creation | Ready for testing |
| AI Progress Scan | Complete - AWS Textract, 100% accuracy |

### Deferred to Post-V1
| Feature | Reason |
|---------|--------|
| Drawings in Work Packages | Per-WP location architecture needs design |
| AI Features (other than Progress Scan) | Lower priority for V1 |
| Procore Integration | Can develop while users test |

## Module Status

| Module | Status | Notes |
|--------|--------|-------|
| Progress | READY FOR TESTING | Core features complete |
| Schedule | READY FOR TESTING | Core features complete |
| Analysis | IN PROGRESS | 4x2 grid layout with summary metrics, Excel export |
| Sync | COMPLETE | Bidirectional sync working |
| Admin | COMPLETE | User/project/snapshot management |
| Work Package | READY FOR TESTING | PDF generation working; Drawings deferred to post-v1 |
| Help Sidebar | COMPLETE | All V1 sections written; Troubleshooting deferred to post-V1 |
| AI Progress Scan | COMPLETE | AWS Textract implementation - 100% accuracy |
| AI Takeoff | READY FOR TESTING | Auto-download on completion, Previous Batches dropdown for re-downloading past results. Metadata (username, config, drawing count) stored in S3. Multi title block regions supported (see Active Development section). |
| AI Features (other) | NOT STARTED | Error Assistant, Description Analysis, etc. |

## Active Development

### Logging & Snapshot Retention - Codex
- App startup log maintenance now purges AppLogger file/database logs older than 15 days. - Codex
- Schedule change log JSON files now purge entries older than 15 days at startup. - Codex
- VMS_ProgressSnapshots retention remains global at submit-time purge for any rows older than 28 days. - Codex

### Plugin System (Complete)
- Plugin Manager dialog in top-right settings menu (`⋮`) with Installed and Available tabs.
- Feed-based discovery from `VANTAGE-Plugins` repo (`plugins-index.json`), install via GitHub release assets.
- Startup auto-update: installed plugins checked against feed, newer versions installed automatically.
- Plugin execution framework: `IVantagePlugin` interface, `IPluginHost` for app capabilities, `PluginLoaderService` loads assemblies at startup.
- Plugins can add menu items to Tools menu dynamically via `host.AddToolsMenuItem()`.
- `IPluginHost` includes `RefreshProgressViewAsync()` for plugins that modify activity data.
- First plugin published: `ptp-tfs-mech-updater` v1.0.1 (PTP vendor shipping report importer for TFS Mechanical, ROCStep 4.SHP).
- **`const-tfs-mech-updater` (WIP):** Scaffold complete — identical structure to PTP plugin with ROCStep 4.SHP, description prefix `FABRICATION - 4.SHP CONST `. Pending: column mappings from first CONST vendor report. Not yet in plugins-index.json or published as a release.

### Multi-Theme System

**Current state:** Dark, Light, Orchid, and Dark Forest themes with live switching (no restart). 103 keys per theme. Full token reference in `Themes/THEME_GUIDE.md`.

**Architecture:** DynamicResource bindings throughout, `ThemeManager.ApplyTheme()` swaps dictionaries at runtime, fires `ThemeChanged` event. Views with Syncfusion grids re-apply `SfSkinManager.SetTheme()` on their grid controls via the event. See THEME_GUIDE.md for full details on creating new themes and technical constraints.

**Theme Generator:** `Scripts/Generate-Theme.ps1` generates a complete theme XAML from 4 hex colors (Primary, Accent, Secondary, Surface) + dark/light base. Claude Code skill `/create-theme` automates the full workflow. Status button colors are hardcoded per base type (dark/light) to stay consistent. Independent highlight keys (`ScanButtonForeground`, `SummaryBudgetForeground`, `SummaryEarnedForeground`, `SummaryPercentForeground`, `SidebarButtonHoverBorder`, `SidebarButtonHoverBackground`) allow per-theme tuning without affecting other themes.

### Progress Book Module
- Phases 1-6 complete: Data models, repository, layout builder UI, PDF generator, live preview, generate dialog
- PDF features: Auto-fit column widths, description wrapping, project description in header
- Layout features: Separate grouping and sorting, up to 10 levels each, exclude completed option

### AI Progress Scan (COMPLETE)
- AWS Textract for table extraction, 100% accuracy on PDF and JPEG scans
- PDF layout: `| ID | [user cols] | MHs | QTY | REM MH | CUR % | % ENTRY |`
- Image preprocessing with contrast adjustment (slider, default 1.2)
- OCR heuristic: "00" auto-converts to "100" (handles missed leading 1)
- Key files: `TextractService.cs`, `PdfToImageConverter.cs`, `ProgressScanService.cs`, `ProgressBookPdfGenerator.cs`

### Work Package Module
- Template editors testing
- PDF preview testing

### Help Sidebar (Complete for V1)
- All 8 sections written (Getting Started, Main Interface, Progress, Schedule, Progress Books, Work Packages, Administration, Reference)
- 20 screenshots, Build Action: Content / Copy if newer
- WebView2 virtual host mapping (`https://help.local/manual.html`) — see `SidePanelView.xaml.cs`
- VS sometimes re-adds PNGs as `<Resource Include>` — always verify Content / Copy if newer
- Troubleshooting section deferred to post-V1

### AI Takeoff Module - Multi Title Block Regions

**Purpose:** Allow users to draw multiple boxes around different sections of a drawing's title block (e.g., PIPE INFO section, Project info section) to exclude noise like logos and revision history. All regions are sent as separate images to Claude, which extracts them into ONE unified `title_block` object.

**Key difference from BOM multi-box:** BOM regions are stitched into one tall image. Title block regions stay as separate images but produce a single combined extraction.

**Files Modified:**

| File | Changes |
|------|---------|
| `Models/AI/CropRegionConfig.cs` | Changed `TitleBlockRegion` (single) to `TitleBlockRegions` (list). Backward compat: `TitleBlockRegion` setter auto-populates list when deserializing old configs. |
| `Dialogs/ConfigCreatorWindow.xaml.cs` | Removed replace logic that limited to one title block. Labels now show "Title Block", "Title Block 2", etc. Save builds list with labels `title_block`, `title_block_2`. Load iterates over `TitleBlockRegions` list. |
| `Plans/AWS Agent/extraction_lambda_function.py` | Checks for `title_block_regions` (list) first, falls back to `title_block_region` (single). Crops each region separately. Labels: "Title block section 1", "Title block section 2". Prompt instructs Claude to combine all sections into single `title_block` object. |

**Config JSON Format:**
```json
{
  "title_block_regions": [
    { "label": "title_block", "x_pct": 75.2, "y_pct": 80.1, "width_pct": 24.5, "height_pct": 19.2 },
    { "label": "title_block_2", "x_pct": 0.5, "y_pct": 85.0, "width_pct": 20.0, "height_pct": 14.5 }
  ]
}
```

**Lambda Deployed:** Updated `extraction_lambda_function.py` deployed to AWS Lambda (March 2026). Testing complete.

## Temporary Restrictions

### AI Takeoff Module (MainWindow.xaml.cs)
**Status:** Restricted to users `steve` and `Steve.Amalfitano` only

**To revert to Estimator role check:**
1. Delete the `IsTakeoffAllowed()` method (~line 340)
2. Line ~326: Change `!IsTakeoffAllowed()` to `!App.CurrentUser.IsEstimator`
3. Line ~1093: Change `(granted && IsTakeoffAllowed())` to just `granted`
4. Remove the `// TEMPORARY` and `// TO REVERT` comments
5. **Add AI Takeoff module to release notes** — When releasing the version that lifts this restriction, add AI Takeoff feature to ReleaseNotes.json highlights

## Feature Backlog

### High Priority
- **Mobile/iOS Version (iPad)** — Execs want iPad app for field supes to submit progress. Needs architecture discussion: native iOS, cross-platform framework, web app, API design, offline sync, etc.
- **Takeoff Post-Processing Pipeline** — All operate on the downloaded Excel, no AWS changes needed. See `summit-takeoff-integration-guide.md` for details.
  1. Fabrication item generation — CUT/BEV rows, connection rows, BOM fab records, PIPE/SPL records, ROCStep column complete. Fitting makeup lookup complete with olet support (WOL/SOL/TOL/ELB/LOL/NOL), class as string, Thickness fallback for olets. Missed Makeups tab has Reason column (No Makeup Found / Unclaimed).
  2. **Rate application** — Core implementation complete. Per-project rate overrides with management dialog, upload from Excel, RateSource column. Admin email notification for missed data. TODO:
     - ~~CVLV (control valve)~~ — Skipped for now, maps to VLV. Possible future add.
     - Determine connection types for HEAT, HOSE, DPAN, F8B so they generate labor rows
     - Add CompRefTable entries for missing rate sheet groups: ORFC, GAUGE, GGLASS, METER, METERR, PROBE, XMTR, SAD, REDF, SCRD, VNTDRN, SHOE, SPRING, ANCH, CLMP
  3. **ROC splits** — VMS_ROCRates table and admin dialog complete. ROC set dropdown on TakeoffView complete. Post-processing logic to apply ROC percentage splits NOT yet implemented.
  4. VANTAGE tab — Column rename for direct import into Activities


### Medium Priority
- **Theme System Refactor** — Phases 1-7 complete. Live switching works, tokens split, guide written. Theme generator script and `/create-theme` skill complete. See `Themes/THEME_GUIDE.md`.
- **Create Activities Feature** — File menu item exists but not implemented. Needs discussion on functionality: possibly AI-assisted (user prompts what they need, records generated), or structured wizard, or template-based. Currently shows "coming soon" placeholder.
- **MSI/MSIX installer** — Replace custom installer with MSI (WiX Toolset) or MSIX packaging to get genuine Windows install integration. Current custom installer registers via registry but Windows Search won't execute `UninstallString` directly — only MSI and UWP/MSIX apps get direct uninstall from search context menu. Current setup works via Settings > Apps.
- **User-editable header template for WP** — Allow customizing header layout

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
