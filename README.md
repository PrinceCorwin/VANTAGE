# VANTAGE: Milestone

Construction progress tracking and project management application built with WPF and .NET 8. Designed for field engineers and project managers in industrial construction (pharmaceutical, microchip, data center facilities). Replaces the legacy MS Access/VBA system (OldVantage) used by Summit Industrial.

VANTAGE: Milestone provides activity tracking, P6 Primavera schedule integration, AI-powered drawing takeoff, AI-assisted progress scanning, PDF work package generation, a plugin extensibility model, and multi-user cloud synchronization.

---

## Current Features

### Progress Tracking Module
- Activity monitoring with Syncfusion SfDataGrid (90+ columns, designed for 100k+ row datasets)
- Earned value management with automatic calculations (EarnMHs, PercentComplete, EarnedQty)
- Bidirectional updates between percent complete and earned quantities
- User-defined multi-condition filters (AND/OR logic) with saved presets
- Multiple named grid layouts (save up to 5, apply/rename/delete, reset to defaults)
- Find & Replace with batch database writes
- Excel import/export with OldVantage compatibility
- Prorate BudgetMHs across filtered activities with proportional distribution
- Activity assignment with email notifications via Azure Communication Services
- DIY summary panel with weighted progress calculations
- Row Actions sidebar dropdown (Select All, Delete, Copy, Duplicate, Add Blank Row, Export Selected) preserving multi-row selection

### Schedule Module
- **P6 Primavera Integration:** Import current schedule dates, export 3WLA updates
- **Discrepancy Detection:** Filter by % Complete, 3WLA Finish, 3WLA Start, Actual Finish, Actual Start, or MHs variances between P6 and VANTAGE
- **MS Rollups:** Automatic MIN(start), MAX(finish), weighted % average from detail ProgressSnapshots
- **Three-Week Lookahead (3WLA):** Forecast dates for activities starting/finishing within 21 days
- **Schedule Reports (3WLA / 6WLA / 9WLA):** Excel export with mismatch highlighting and AssignedTo audit column
- **Required Fields Tracking:** Count and filter for missing MissedReasons and 3WLA dates
- **Visual Variance Highlighting:** Red/yellow conditional formatting for missed deadlines and variances
- Master/detail grid layout with editable detail ProgressSnapshots
- Schedule Change Log for tracking detail grid edits

### AI Takeoff Module
- AI-powered piping takeoff extraction from PDF/image drawing sets (Claude Sonnet 4.6 via AWS Bedrock)
- Lambda-based parallel drawing processing with S3-backed batch storage and per-batch download
- BOM extraction, connection inference, and dual-size component matching (TEE, REDT, REDC, SWG, etc.)
- Automatic labor row generation with rate-sheet lookup (size + thickness + class fallback chain)
- Per-project rate overrides with `Manage Project Rates` dialog and Excel upload
- ROC (Rule of Credit) splits via `Manage ROC Rates` dialog with applicable-component checklist
- Fitting makeup lookup with olet support and equivalence mappings
- Field/Shop classification (ShopField post-processing) with mixed-connection-type rules
- Audit columns (RateSheet, RollupMult, MatlMult, CutAdd, BevelAdd) for user verification
- Diagnostic worksheets (Missed Makeups, Missed Rates, No Conns, Malformed Sizes, Failed DWGs) with reason classification
- Recalc Excel and re-download support that preserves diagnostic tabs

### AI Progress Scan
- AWS Textract-based table extraction from scanned progress sheets
- High accuracy on both PDF and JPEG scans
- Simplified scan layout: ID first, single % ENTRY column at far right
- Image preprocessing with adjustable contrast (slider, default 1.2)
- OCR heuristic: "00" auto-converts to "100" for missed leading digits
- Results grid with filtering, Select All / Select Ready / Clear buttons

### Progress Books
- Custom PDF report generation from ProgressSnapshot data
- Layout builder with separate grouping (up to 10 levels) and sorting (up to 10 levels)
- Auto-fit column widths, description wrapping, project description in header
- Exclude completed activities option
- Live preview during configuration

### Work Package Module
- PDF generation for construction work packages
- Five template types: Cover (image), List (TOC), Form (checklist/signoff), Grid (punchlist), Drawings
- Token-based dynamic content binding ({WorkPackage}, {ProjectName}, {PrintedDate}, etc.)
- Customizable templates with clone-to-edit workflow
- Built-in templates: Cover Sheet, TOC, Checklist, Punchlist, Signoff Sheet, DWG Log
- Multi-form merge into single PDF package
- Live PDF preview panel
- Drawings deferred to post-V1 (per-WP location architecture needs design)

### Plugin System
- In-app `Plugin Manager` dialog for installing, updating, and removing optional plugins
- Catalog-driven plugin feed with auto-update support
- `IVantagePlugin` / `IPluginHost` contract for third-party or internal extensions
- Plugin manifest with version, dependency, and signature metadata

### Synchronization
- **Hybrid Architecture:** Local SQLite for offline work + Azure SQL Server as central authority
- **SyncVersion-Based Tracking:** Monotonic integers, no clock drift issues
- **Conflict Resolution:** Ownership-based editing, Azure always wins
- **Performance:** SqlBulkCopy on push, prepared statements on pull (5k records ~6 seconds)
- Soft delete propagation with restore capability
- My Records Only mode (toggle on/off, full re-pull on disable)

### Administration
- Windows authentication integration
- Role-based access control with admin privileges (Azure `Admins` table)
- Manage Users, Projects, Snapshots, Project Rates, ROC Rates dialogs
- Snapshot management: delete multiple weeks, revert with/without backup, grouped by Project + WeekEnd + Submission
- Admin snapshot ops: delete all, upload to ProgressLog
- Deleted Records view with bulk Restore / Purge
- Feedback Board with Ideas / Bug Reports and admin moderation
- Activity Import with auto-detection of Legacy/Milestone format
- Submit Week metadata gate (blocks submission when required fields are missing on the selected project)

### Help Sidebar
- Integrated help panel accessible via F1 or Settings menu
- WebView2 rendering with virtual host mapping
- Context-specific navigation with screenshots throughout

### Multi-Theme System
- Four themes: Dark (default), Light, Orchid, Dark Forest
- Live switching without restart via `ThemeManager`
- Syncfusion `SfSkinManager` integration across all dialogs/views
- Architecture supports adding new themes (see `Themes/THEME_GUIDE.md`)

### Tools & Utilities
- Export logs with optional email attachment
- Export/Import UserSettings for PC migration
- Clear Local Activities / Clear Local Schedule
- Cell copy/paste (Ctrl+C/Ctrl+V) in grids
- Auto-update mechanism with branded installer

---

## In Development / Planned

### Analysis Module (In Progress)
- 3x1+3 grid layout with chart filters panel and persistent filter state
- Dynamic chart sections with selectable visual type, X axis, Y axis
- Pie / doughnut labels and legends
- Summary grid with independent filters
- Excel export

### Procore Integration (In Development)
- OAuth 2.0 authentication with token management (auth dialog and services in place)
- Production and sandbox environment support
- Targeted use: fetch construction drawings for the Work Package DWG Log

### AI Features (Post-V1)
| Feature | Purpose |
|---------|---------|
| **AI Error Assistant** | Translate technical errors into plain English with suggested fixes |
| **AI Description Analysis** | Standardize activity descriptions, detect anomalies |
| **Metadata Consistency Analysis** | Flag outliers, suggest corrections, ensure data quality |
| **AI MissedReason Assistant** | Standardize missed reason entries with category suggestions |
| **AI Schedule Analysis** | Flag relationship violations, sequence errors, progress anomalies |
| **AI Sidebar Chat** | Conversational interface with tool-based data access |

### Mobile / iOS (Post-V1)
- iPad app for field supervisors to submit progress; architecture TBD (native iOS, cross-platform, or web)

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| **Framework** | .NET 8.0 (Windows) |
| **UI** | WPF with Syncfusion |
| **Themes** | FluentDark / FluentLight base (Dark, Light, Orchid, Dark Forest) |
| **Local Database** | SQLite |
| **Cloud Database** | Azure SQL Server |
| **Architecture** | MVVM with async/await |
| **Excel** | ClosedXML |
| **PDF** | Syncfusion.Pdf |
| **AI — Takeoff** | AWS Bedrock (Claude Sonnet 4.6) + AWS Lambda + S3 |
| **AI — Progress Scan** | AWS Textract |
| **Email** | Azure Communication Services |
| **Help** | WebView2 |
| **Plugins** | In-process `IVantagePlugin` contract with catalog feed |

---

## Requirements

### For Users
- Windows 10/11
- Azure SQL Server access (for multi-user sync)

### For Development
- Visual Studio 2022
- Syncfusion License (community or commercial)
- AWS credentials (for AI Progress Scan and AI Takeoff)

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
├── Assets/               # Images, icons, sidebar artwork
├── Converters/           # WPF value converters
├── Data/                 # Repositories and database access
│   ├── ActivityRepository.cs
│   ├── ScheduleRepository.cs
│   ├── TemplateRepository.cs
│   └── AzureDbManager.cs
├── Diagnostics/          # Logging, error capture
├── Dialogs/              # Modal dialogs (90+)
├── Help/                 # manual.html + screenshots
├── Models/               # Data models (Activity, Schedule, Plugin manifests, etc.)
├── Plans/                # Development documentation, PRDs, archives
├── Resources/            # Shared XAML resources
├── Scripts/              # Build/publish/utility scripts
├── Services/
│   ├── AI/               # TextractService, ProgressScanService, TakeoffService, RateSheetService, FittingMakeupService
│   ├── PdfRenderers/     # Work Package PDF renderers
│   ├── Plugins/          # Plugin loader, catalog, install, auto-update
│   ├── Procore/          # Procore API + auth services
│   └── ProgressBook/     # Progress Book PDF generation
├── Styles/               # Shared XAML styles
├── Themes/               # DarkTheme, LightTheme, OrchidTheme, DarkForestTheme
├── Utilities/            # Helpers, exporters, importers, validators
├── ViewModels/           # MVVM view models
├── Views/                # Module views (Progress, Schedule, Takeoff, WorkPackage, ProgressBooks, Analysis, DeletedRecords, SidePanel)
├── VANTAGE.Installer/    # Branded installer
└── VANTAGE.Updater/      # Auto-update mechanism
```

---

## Module Status

| Module | Status | Notes |
|--------|--------|-------|
| **Progress** | Production | Core features complete, in active use |
| **Schedule** | Production | P6 import/export, 3WLA, discrepancies, change log |
| **Sync** | Production | Bidirectional Azure sync |
| **Admin** | Production | Users, projects, snapshots, rates, deleted records, feedback |
| **Work Packages** | Production | PDF generation; drawings deferred |
| **Progress Books** | Production | Custom PDF reports with layout builder |
| **AI Progress Scan** | Production | AWS Textract |
| **AI Takeoff** | Production | Bedrock + Lambda + S3 pipeline with rate / ROC / makeup post-processing |
| **Plugins** | Production | Plugin Manager, catalog feed, auto-update |
| **Help Sidebar** | Production | Multi-section manual with screenshots |
| **Themes** | Production | Dark, Light, Orchid, Dark Forest |
| **Installer / Updater** | Production | Auto-update validated |
| **Analysis** | In Development | Charts, summary grid, Excel export |
| **Procore** | In Development | OAuth + service layer scaffolded |
