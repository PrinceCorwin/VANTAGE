# MILESTONE - Project Status

**Last Updated:** January 17, 2026

## Module Status

| Module | Status | Notes |
|--------|--------|-------|
| Progress | READY FOR TESTING | Core features complete |
| Schedule | READY FOR TESTING | Core features complete |
| Sync | COMPLETE | Bidirectional sync working |
| Admin | COMPLETE | User/project/snapshot management |
| Work Package | IN DEVELOPMENT | PDF generation working; Drawings editor and template editors need testing |
| Help Sidebar | IN DEVELOPMENT | Infrastructure complete; search implemented; content writing in progress |
| AI Features | NOT STARTED | Requires ClaudeApiService infrastructure first |

## Active Development

### Work Package Module
- Drawings - Fix preview display
- Drawings - Fix layout/orientation for 11x17 drawings
- Drawings - Implement Procore fetch
- **DISCUSS:** Drawings fetch architecture - consider AI-assisted matching (many factors: DwgNO formats, revisions, sheet numbers, naming conventions). May warrant separate Drawings Manager module/dialog where drawings are fetched/organized independently, then WP module simply pulls from that cache.

### Help Sidebar
- Write Getting Started content
- Write Progress Module content
- Write Schedule Module content
- Write Work Packages content
- Write Progress Books content
- Write Administration content
- Write Reference content
- Capture screenshots (45+ total)
- Implement PDF export

## Feature Backlog

### High Priority
- Progress Book creation
- Theme selection by user - save preference, apply on startup
- Add Offline Indicator in status bar - clickable to retry connection

### Medium Priority
- **DISCUSS:** Add PlanStart and PlanFinish fields to Activities (for baseline schedule comparison?)
- Schedule module: Check if user can apply detail grid edits to live activities - explore adding this option if not available
- Shift+Scroll horizontal scrolling (see ShiftScroll_Horizontal_Implementation_Plan.md)
- User-editable header template for WP (allow customizing header layout)
- Import/Export WP templates to JSON

### Infrastructure / Azure Migration
- Execute VMS_ table creation script on company Azure (see MILESTONE_Azure_Migration_Plan.md)
- Legacy Azure Table Save - Admin dialog to upload project snapshots to company Azure dbo_VANTAGE_global_ProgressLog (UPSERT, schema mapping TBD)

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

### Procore Integration (see Procore_Plan.md)
- Procore Drawings integration for WP module

### Shelved
- Find-Replace in Schedule Detail Grid
- Disable Tooltips setting (see DisableTooltips_Plan.md)
- Interactive Help Mode - click UI controls to navigate to documentation (see Sidebar_Help_Plan.md)

## Known Issues

1. **Drawings preview not displaying** - Fetched drawings show in generated PDF but not in previewer
2. **Drawings layout/orientation** - 11x17 drawings may need rotation or scaling adjustment

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
