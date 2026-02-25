# VANTAGE: Milestone

Construction progress tracking and project management application built with WPF and .NET 8. Designed for field engineers and project managers in industrial construction (pharmaceutical, microchip, data center facilities). Replaces the legacy MS Access/VBA system (OldVantage) used by Summit Industrial.

VANTAGE: Milestone provides activity tracking, P6 Primavera schedule integration, AI-powered progress scanning, PDF work package generation, and multi-user cloud synchronization.

---

## Current Features

### Progress Tracking Module
- Activity monitoring with Syncfusion SfDataGrid (90+ columns, 200k+ record virtualization)
- Earned value management with automatic calculations (EarnMHs, PercentComplete, EarnedQty)
- Bidirectional updates between percent complete and earned quantities
- User-defined multi-condition filters (AND/OR logic) with saved presets
- Multiple named grid layouts (save up to 5, apply/rename/delete, reset to defaults)
- Find & Replace with batch database writes
- Excel import/export with OldVantage compatibility
- Prorate BudgetMHs across filtered activities with proportional distribution
- Activity assignment with email notifications via Azure Communication Services
- DIY summary panel with weighted progress calculations

### Schedule Module
- **P6 Primavera Integration:** Import current schedule dates, export 3WLA updates
- **Discrepancy Detection:** Filter by % Complete, 3WLA Finish, 3WLA Start, Actual Finish, Actual Start, or MHs variances between P6 and VANTAGE
- **MS Rollups:** Automatic MIN(start), MAX(finish), weighted % average from detail ProgressSnapshots
- **Three-Week Lookahead (3WLA):** Forecast dates for activities starting/finishing within 21 days
- **3WLA Excel Report:** Export with mismatch highlighting (Actuals, MHs, % Complete, date changes)
- **Required Fields Tracking:** Count and filter for missing MissedReasons and 3WLA dates
- **Visual Variance Highlighting:** Red/yellow conditional formatting for missed deadlines and variances
- Master/detail grid layout with editable detail ProgressSnapshots
- Schedule Change Log for tracking detail grid edits

### Progress Books
- Custom PDF report generation from ProgressSnapshot data
- Layout builder with separate grouping (up to 10 levels) and sorting (up to 10 levels)
- Auto-fit column widths, description wrapping, project description in header
- Exclude completed activities option
- Live preview during configuration

### AI Progress Scan
- AWS Textract-based table extraction from scanned progress sheets
- 100% accuracy on both PDF and JPEG scans
- Simplified scan layout: ID first, single % ENTRY column at far right
- Image preprocessing with adjustable contrast (slider, default 1.2)
- OCR heuristic: "00" auto-converts to "100" for missed leading digits
- Results grid with filtering, Select All/Select Ready/Clear buttons

### Work Package Module
- PDF generation for construction work packages
- Five template types: Cover (image), List (TOC), Form (checklist/signoff), Grid (punchlist), Drawings
- Token-based dynamic content binding ({WorkPackage}, {ProjectName}, {PrintedDate}, etc.)
- Customizable templates with clone-to-edit workflow
- Built-in templates: Cover Sheet, TOC, Checklist, Punchlist, Signoff Sheet, DWG Log
- Multi-form merge into single PDF package
- Live PDF preview panel
- Drawings deferred to post-V1 (per-WP location architecture needs design)

### Synchronization
- **Hybrid Architecture:** Local SQLite for offline work + Azure SQL Server as central authority
- **SyncVersion-Based Tracking:** Monotonic integers, no clock drift issues
- **Conflict Resolution:** Ownership-based editing, Azure always wins
- **Performance:** 5k records sync in ~6 seconds via SqlBulkCopy
- Soft delete propagation with restore capability
- My Records Only mode (toggle on/off, full re-pull on disable)

### Administration
- Windows authentication integration
- Role-based access control with admin privileges (Azure Admins table)
- Manage Users, Projects, Snapshots, Deleted Records dialogs
- Snapshot management: delete multiple weeks, revert with/without backup, grouped by Project + WeekEnd + Submission
- Feedback Board with Ideas/Bug Reports and admin moderation
- Activity Import with auto-detection of Legacy/Milestone format

### Help Sidebar
- Integrated help panel accessible via F1 or Settings menu
- WebView2 rendering with virtual host mapping
- 8 sections: Getting Started, Main Interface, Progress, Schedule, Progress Books, Work Packages, Administration, Reference
- 20 screenshots with context-specific navigation

### Multi-Theme System
- Three themes: Dark (default), Light, Orchid
- Live switching without restart via ThemeManager
- Syncfusion SfSkinManager integration across all dialogs/views
- Architecture supports adding new themes (see Themes/THEME_GUIDE.md)

### Tools & Utilities
- Export logs with optional email attachment
- Export/Import UserSettings for PC migration
- Clear Local Activities / Clear Local Schedule
- Cell copy/paste (Ctrl+C/Ctrl+V) in grids
- Auto-update mechanism with branded installer

---

## Planned Features

### AI Features (Post-V1)
| Feature | Purpose |
|---------|---------|
| **AI Error Assistant** | Translate technical errors into plain English with suggested fixes |
| **AI Description Analysis** | Standardize activity descriptions, detect anomalies |
| **Metadata Consistency Analysis** | Flag outliers, suggest corrections, ensure data quality |
| **AI MissedReason Assistant** | Standardize missed reason entries with category suggestions |
| **AI Schedule Analysis** | Flag relationship violations, sequence errors, progress anomalies |
| **AI Sidebar Chat** | Conversational interface with tool-based data access |

### Procore Integration (Post-V1)
- OAuth 2.0 authentication with token management
- Fetch construction drawings for Work Package DWG Log
- Production and sandbox environment support

### Dashboard Module (Post-V1)
- Column/stacked charts for activity completion trends
- S-curve for planned vs actual progress
- Pie/doughnut charts for distribution by WorkPackage, PhaseCode, RespParty
- Radial gauge for overall project % complete

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| **Framework** | .NET 8.0 (Windows) |
| **UI** | WPF with Syncfusion |
| **Themes** | FluentDark, FluentLight (Dark, Light, Orchid) |
| **Local Database** | SQLite |
| **Cloud Database** | Azure SQL Server |
| **Architecture** | MVVM with async/await |
| **Excel** | ClosedXML |
| **PDF** | Syncfusion.Pdf |
| **AI/OCR** | AWS Textract |
| **Email** | Azure Communication Services |
| **Help** | WebView2 |

---

## Requirements

### For Users
- Windows 10/11
- Azure SQL Server access (for multi-user sync)

### For Development
- Visual Studio 2022
- Syncfusion License (community or commercial)
- AWS credentials (for AI Progress Scan)

---

## Getting Started

1. Clone the repository
2. Open `VANTAGE.sln` in Visual Studio 2022
3. Restore NuGet packages
4. Configure `Credentials.cs` (gitignored) with Azure and AWS connection strings
5. Build and run
6. On first run, the application will:
   - Check Azure connectivity
   - Initialize the local SQLite database
   - Mirror reference tables from Azure (Users, Projects, ColumnMappings)
   - Validate user authorization via Windows authentication

---

## Project Structure

```
VANTAGE/
├── Data/                 # Repositories and database access
│   ├── ActivityRepository.cs
│   ├── ScheduleRepository.cs
│   ├── TemplateRepository.cs
│   └── AzureDbManager.cs
├── Dialogs/              # Modal dialogs (20+)
├── Help/                 # manual.html + screenshots
├── Models/               # Data models
├── Plans/                # Development documentation
├── Services/
│   ├── AI/              # TextractService, ProgressScanService
│   ├── ProgressBook/    # Progress Book PDF generation
│   └── PdfRenderers/    # Work Package PDF renderers
├── Themes/              # DarkTheme.xaml, LightTheme.xaml, OrchidTheme.xaml
├── Utilities/           # Helpers, exporters, importers
├── ViewModels/          # MVVM view models
├── Views/               # Main module views
├── VANTAGE.Installer/   # Branded installer
└── VANTAGE.Updater/     # Auto-update mechanism
```

---

## Module Status

| Module | Status | Notes |
|--------|--------|-------|
| **Progress** | Ready for Testing | Core features complete |
| **Schedule** | Ready for Testing | P6 import/export, 3WLA, discrepancies |
| **Sync** | Complete | Bidirectional Azure sync |
| **Admin** | Complete | Users, projects, snapshots, feedback |
| **Work Packages** | Ready for Testing | PDF generation; drawings deferred |
| **Progress Books** | Ready for Testing | Custom PDF reports with layout builder |
| **AI Progress Scan** | Complete | AWS Textract, 100% accuracy |
| **Help Sidebar** | Complete | 8-section manual with screenshots |
| **Themes** | Complete | Dark, Light, Orchid |
| **Installer/Updater** | Complete | Auto-update validated |
