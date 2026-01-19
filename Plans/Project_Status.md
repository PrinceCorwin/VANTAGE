# MILESTONE - Project Status

**Last Updated:** January 19, 2026

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
| Progress Book creation | Needs implementation |

### Deferred to Post-V1
| Feature | Reason |
|---------|--------|
| Drawings in Work Packages | Per-WP location architecture needs design |
| AI Features | Requires ClaudeApiService infrastructure |
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
| AI Features | NOT STARTED | Requires ClaudeApiService infrastructure first |

## Active Development

### Work Package Module
- Template editors testing
- PDF preview testing

### Help Sidebar
- Write Work Packages content
- Write Progress Books content
- Write Administration content
- Write Reference content
- Capture screenshots (45+ total)

## Feature Backlog

### High Priority
- Progress Book creation
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
| ClaudeApiService infrastructure | Not Started |
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

None currently blocking v1 testing.

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
