# MILESTONE

Vantage: MILESTONE is a comprehensive construction project management and progress tracking application built with WPF (Windows Presentation Foundation) and .NET 8. Designed for field engineers and project managers in industrial construction (pharmaceutical, microchip, data center facilities), MILESTONE provides powerful tools for tracking construction activities, managing schedules with P6 Primavera integration, generating work package documents, and enabling multi-user collaboration. The application features a modern Syncfusion FluentDark interface, bidirectional cloud synchronization, and is architected for upcoming AI-powered analytics.

---

## ✨ Current Features

### 📊 Progress Tracking Module
- Real-time activity monitoring with Syncfusion SfDataGrid (90+ columns, 200k+ record virtualization)
- Earned value management with automatic calculations (EarnMHs, PercentComplete, EarnedQty)
- Bidirectional updates between percent complete and earned quantities
- Advanced filtering: user-defined multi-condition filters (AND/OR logic), saved filter presets
- Multiple named grid layouts (save up to 5, apply/rename/delete, reset to defaults)
- Excel import/export with legacy OldVantage compatibility
- Prorate BudgetMHs across filtered activities with proportional distribution
- Activity assignment with email notifications
- "Today" filter for three-week lookahead activities

### 📅 Schedule Module
- **P6 Primavera Integration:** Import/export schedules, bidirectional sync
- **Discrepancy Detection:** Filter by Actual Start, Actual Finish, MHs, or % Complete variances
- **MS Rollups:** Automatic MIN(start), MAX(finish), weighted % average calculations
- **Three-Week Lookahead (3WLA):** Forecasting and schedule tracking
- **Visual Variance Highlighting:** Yellow highlighting when P6 progress exceeds MILESTONE
- Master/detail grid layout with Clear Filters functionality
- Missed reason tracking for schedule accountability

### 📦 Work Package Module *(In Development)*
- PDF generation for construction work packages (replaces legacy MS Access VBA system)
- Four template types: Cover (image), List (TOC), Form (checklist), Grid (empty rows)
- Token-based dynamic content binding ({WorkPackage}, {ProjectName}, {PrintedDate}, etc.)
- Customizable templates with clone-to-edit workflow
- Built-in templates: Cover Sheet, TOC, Checklist, Punchlist, Signoff Sheet, DWG Log
- Syncfusion PDF rendering with multi-form merge into single package
- Live PDF preview panel

### 🔄 Synchronization System
- **Hybrid Architecture:** Local SQLite for offline work + Azure SQL Server as central authority
- **SyncVersion-Based Tracking:** Reliable multi-user sync without clock drift issues
- **Conflict Resolution:** Ownership-based editing, Azure always wins on conflicts
- **Performance:** 5k records sync in ~6 seconds
- Soft delete propagation with restore capability

### 👥 User Management & Administration
- Windows authentication integration
- Role-based access control with admin privileges (Azure Admins table)
- User-specific settings and personalized grid layouts
- Admin tools: Manage users, projects, snapshots, deleted records
- Feedback Board with Ideas/Bug Reports and admin moderation

### 🎨 Modern User Interface
- Syncfusion FluentDark themed interface
- Responsive toolbar-based module navigation
- Context-aware tooltips throughout all views and dialogs
- Real-time status bar with user info and system status
- Chromeless window design

### 🛠️ Tools & Utilities
- Export logs with optional email attachment
- Export/Import UserSettings for PC migration
- Clear Local Activities / Clear Local Schedule tools
- Cell copy/paste (Ctrl+C/Ctrl+V) in grids

---

## 🚀 Planned Features

### 🤖 AI Integration *(High Priority)*
Five AI-powered features planned using Anthropic Claude API:

| Feature | Purpose |
|---------|---------|
| **AI Error Assistant** | Translate technical errors into plain English with suggested fixes |
| **AI Description Analysis** | Standardize activity descriptions, detect anomalies |
| **Metadata Consistency Analysis** | Flag outliers, suggest corrections, ensure data quality |
| **AI MissedReason Assistant** | Standardize missed reason entries with category suggestions |
| **AI Schedule Analysis** | Flag relationship violations, sequence errors, progress anomalies |

*Expected daily cost at full usage: $0.20-0.50/day*

### 🏗️ Procore Integration *(Pending)*
- OAuth 2.0 authentication with token management
- Fetch construction drawings for DWG Log forms
- Production and sandbox environment support

### 📈 Analysis & Reporting *(Future)*
- Interactive dashboards with real-time visualization
- Earned Value Analysis (EVA) with SPI/CPI metrics
- Progress S-curves and trend analysis
- Resource utilization and productivity metrics
- Export visualizations to PDF and image formats

### 📱 Enhanced Collaboration *(Future Consideration)*
- Potential Blazor migration for cloud-first architecture
- Mobile capabilities for field updates
- Real-time collaborative editing

---

## 🛠️ Technology Stack

| Component | Technology |
|-----------|------------|
| **Framework** | .NET 8.0 (Windows) |
| **UI** | WPF with Syncfusion 31.2.12 (FluentDark theme) |
| **Local Database** | SQLite |
| **Cloud Database** | Azure SQL Server (mile-wip-server-stecor / MILESTONE-WIP-DB) |
| **Architecture** | MVVM pattern with async/await |
| **Data Export** | ClosedXML for Excel, Syncfusion.Pdf for PDF generation |
| **Cloud Services** | Azure SQL, planned Azure Blob for PDF archival |
| **AI Services** | Anthropic Claude API *(planned)* |

---

## 📋 Requirements

- Windows 10/11
- .NET 8.0 Runtime
- Visual Studio 2022 (for development)
- Syncfusion License (community or commercial)
- Azure SQL Server access (for multi-user sync)

---

## 🚦 Getting Started

1. Clone the repository
2. Open `MILESTONE.sln` in Visual Studio 2022
3. Restore NuGet packages
4. Configure Azure connection string in `AzureDbManager.cs`
5. Build and run the solution
6. On first run, the application will:
   - Check Azure connectivity
   - Initialize the local SQLite database
   - Validate user authorization against Users table
   - Set up user profile with Windows authentication

---

## 📂 Project Structure

```
MILESTONE/
├── Models/              # Data models (Activity, User, Project, Templates)
├── Views/               # XAML views and code-behind
├── ViewModels/          # MVVM view models
├── Data/                # Repositories and data access
│   ├── ActivityRepository.cs
│   ├── ScheduleRepository.cs
│   ├── TemplateRepository.cs
│   └── AzureDbManager.cs
├── Services/            # Business logic services
│   ├── SyncManager.cs
│   ├── TokenResolver.cs
│   └── WorkPackageGenerator.cs
├── Utilities/           # Helper classes
│   ├── AppLogger.cs
│   ├── ColumnMapper.cs
│   ├── FilterBuilder.cs
│   └── UserHelper.cs
├── Controls/            # Custom WPF controls
├── Themes/              # Syncfusion FluentDark theme resources
├── PDF/                 # PDF renderers (Cover, List, Form, Grid)
└── Plans/               # Development documentation
```

---

## 🎯 Key Modules

| Module | Status | Description |
|--------|--------|-------------|
| **PROGRESS** | ✅ Ready for Testing | Activity tracking, earned value, Excel import/export |
| **SCHEDULE** | ✅ Ready for Testing | P6 integration, 3WLA, discrepancy detection |
| **SYNC** | ✅ Complete | Bidirectional Azure synchronization |
| **ADMIN** | ✅ Complete | User, project, snapshot management |
| **WORK PKGS** | 🔄 In Development | PDF work package generation |
| **AI** | 📋 Planned | Claude API integration for analytics |

---

## ⚡ Performance Targets

| Metric | Target |
|--------|--------|
| Sync 5k records | ~6 seconds |
| Grid virtualization | 200k+ records |
| MS rollup calculation | <3 seconds for 200 activities |

---

## 📝 Development Notes

- **One change at a time:** Test before proceeding
- **No quick fixes:** Proper architectural solutions preferred
- **Nullable reference types:** Enabled throughout project
- **Logging:** AppLogger with Info/Error/Warning levels
- **Local database:** Can be deleted and re-synced from Azure at any time

---

## 📜 License

*License information to be added*

---

## 👥 Contributing

*Contribution guidelines to be added*

---

## 📧 Contact

*Contact information to be added*
