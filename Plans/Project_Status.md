# MILESTONE - Project Status

**Last Updated:** January 24, 2026

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
| AI Progress Scan | In development - accuracy improvements needed |

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
| AI Progress Scan | IN DEVELOPMENT | Accuracy improvements in progress - see Active Development |
| AI Features (other) | NOT STARTED | Error Assistant, Description Analysis, etc. |

## Active Development

### Progress Book Module
- Phases 1-6 complete: Data models, repository, layout builder UI, PDF generator, live preview, generate dialog
- PDF features: Auto-fit column widths, description wrapping, project description in header
- Layout features: Separate grouping and sorting, up to 10 levels each, exclude completed option

### AI Progress Scan - Accuracy Improvements (IN PROGRESS)

**Current State:**
- Using Claude Opus 4.5 model with Tool Use (function calling) for structured output
- PDF-to-image conversion working via Syncfusion.PdfToImageConverter.WPF
- Removed color fills from entry boxes - now relying on text labels only
- Entry boxes have "Qty:" and "%:" labels to help AI distinguish columns
- DONE checkbox has no label currently

**Problem:**
- JPEG scans give better accuracy than PDF scans
- Checkmarks are sometimes missed (small X marks in small boxes)
- Inconsistent detection between PDF and JPEG of same document

**Technical Changes Made:**
- Switched from PdfiumViewer (incompatible with .NET 8) to Syncfusion.PdfToImageConverter.WPF
- Removed all color fills from entry columns (colors weren't helping AI, just humans)
- Added "Qty:" and "%:" labels to entry boxes
- Updated AI prompt to focus on reading text labels, not colors

**Next Steps:**
1. ~~Add "C:" label to DONE checkbox (C = Complete) to help AI identify it~~ DONE
2. Test accuracy with new "C:" label on checkbox
3. Consider requiring image scans (JPEG/PNG) instead of PDF for better accuracy
4. May need to increase checkbox size if still missing marks

**To Test:**
- Generate a new Progress Book PDF (will have C:, Qty:, %: labels in all entry boxes)
- Print it, fill in entries by hand, scan it (try both PDF and JPEG)
- Run AI Progress Scan and compare accuracy

**Key Files:**
- `Services/AI/ClaudeVisionService.cs` - AI prompt and tool definitions
- `Services/AI/PdfToImageConverter.cs` - PDF to image conversion (Syncfusion)
- `Services/AI/ProgressScanService.cs` - Scan orchestration
- `Services/ProgressBook/ProgressBookPdfGenerator.cs` - PDF generation with entry boxes
- `Credentials.cs` - Model set to `claude-opus-4-5-20251101`

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
- Add Offline Indicator in status bar - clickable to retry connection

### Medium Priority
- **DISCUSS:** Add PlanStart and PlanFinish fields to Activities (for baseline schedule comparison?)
- Shift+Scroll horizontal scrolling (see ShiftScroll_Horizontal_Implementation_Plan.md)
- User-editable header template for WP (allow customizing header layout)
- Import/Export WP templates to JSON

### Infrastructure / Azure Migration
- Execute VMS_ table creation script on company Azure (see MILESTONE_Azure_Migration_Plan.md)
- Progress_Log Azure Upload - Admin dialog to append project snapshots to company Azure dbo_VANTAGE_global_ProgressLog (historical by WeekEndDate, schema mapping TBD)

### AI Features (see InCode_AI_Plan.md)
| Feature | Status |
|---------|--------|
| AI Progress Scan | In Progress - Accuracy improvements needed (see Active Development section above) |
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

### Shelved
- Find-Replace in Schedule Detail Grid
- Disable Tooltips setting (see DisableTooltips_Plan.md)
- Interactive Help Mode - click UI controls to navigate to documentation (see Sidebar_Help_Plan.md)

## Known Issues

### AI Progress Scan - Review Grid Selection Bug
- Checkbox selection in review grid is buggy
- Items auto-select unexpectedly, user cannot manually select/unselect rows
- Affects "Apply Selected" functionality - button shows count but selection doesn't work properly
- **TODO:** Fix SfDataGrid checkbox binding in ProgressScanDialog.xaml

### AI Progress Scan - Accuracy Issues
- PDF scans less accurate than JPEG scans (same document)
- Checkmarks sometimes missed (small X marks in small checkbox boxes)
- Consider requiring JPEG/PNG instead of PDF uploads for V1

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
