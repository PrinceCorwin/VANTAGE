# VANTAGE

VANTAGE is a comprehensive construction project management and progress tracking application built with WPF (Windows Presentation Foundation) and .NET 8. Designed for engineering and construction professionals, VANTAGE provides powerful tools for monitoring project activities, tracking earned value metrics, managing schedules, and generating insightful reports. The application features a modern Material Design interface with customizable themes, advanced filtering capabilities, and robust data management through both local SQLite and Azure cloud storage options.

## ✨ Current Features

📊 **Progress Tracking Module**
- Real-time activity monitoring with customizable data grids
- Earned value management with automatic calculations (EarnMHs, PercentComplete, EarnedQty)
- Bidirectional updates between percent complete and earned quantities
- Advanced filtering and sorting capabilities with saved filter presets
- Column visibility customization and user preference management
- Pagination support for handling large datasets efficiently

🎨 **Modern User Interface**
- Material Design themed interface with dark/light mode support
- Responsive and intuitive navigation with toolbar-based module switching
- Context-aware tooltips and user-friendly controls
- Real-time status bar updates showing current user and system status

💾 **Data Management**
- Local SQLite database for offline work and data persistence
- Excel import/export functionality (replace or combine modes)
- Azure Table Storage integration for cloud synchronization
- Comprehensive activity schema with 80+ customizable fields
- User settings and preferences storage

👥 **User Management & Security**
- Windows authentication integration
- Role-based access control with admin privileges
- User-specific settings and personalized views
- Activity assignment and ownership tracking

📋 **Reporting Capabilities**
- Multiple pre-configured report templates (10 report slots)
- Export capabilities for sharing and archival
- Custom report generation framework (expandable)

## 🚀 Future Features

📅 **P6 Schedule Interface**
- Direct integration with Primavera P6 schedules
- Bidirectional synchronization between VANTAGE and P6
- Schedule activity linking and cross-referencing
- Critical path analysis and schedule variance reporting
- Import/export of P6 XER files
- Real-time schedule updates and conflict resolution

📈 **Analysis and Reports with Graphical Statistics**
- Interactive dashboards with real-time data visualization
- Customizable charts and graphs (bar, line, pie, Gantt)
- Earned Value Analysis (EVA) with SPI/CPI metrics
- Progress S-curves and trend analysis
- Resource utilization and productivity metrics
- Heat maps for activity status and bottleneck identification
- Comparative analysis across projects and time periods
- Automated report generation with graphical insights
- Export visualizations to PDF, PowerPoint, and image formats

🔄 **Enhanced Cloud Capabilities**
- Multi-user collaboration with real-time updates
- Conflict resolution for concurrent edits
- Mobile companion app for field updates
- Offline sync with intelligent merge strategies

🤖 **Advanced Analytics**
- Predictive analytics for project completion forecasting
- AI-powered recommendations for schedule optimization
- Anomaly detection for progress discrepancies
- Historical trend analysis and benchmarking

## 🛠️ Technology Stack

- **Framework:** .NET 8.0 (Windows)
- **UI:** WPF with Material Design themes
- **Database:** SQLite (local) / Azure Table Storage (cloud)
- **Architecture:** MVVM pattern with CommunityToolkit.Mvvm
- **Data Export:** ClosedXML for Excel operations
- **Cloud Services:** Azure.Data.Tables, Azure.Identity

## 📋 Requirements

- Windows 10/11
- .NET 8.0 Runtime
- Visual Studio 2022 (for development)

## 🚦 Getting Started

1. Clone the repository
2. Open `VANTAGE.sln` in Visual Studio 2022
3. Restore NuGet packages
4. Build and run the solution
5. On first run, the application will:
   - Initialize the local SQLite database
   - Create default app settings
   - Set up your user profile with Windows authentication

## 📂 Project Structure

- **Models/** - Data models (Activity, User, Settings)
- **Views/** - XAML views and code-behind
- **ViewModels/** - MVVM view models
- **Data/** - Database repositories and data access
- **Utilities/** - Helper classes (FilterBuilder, ColumnMapper, ThemeManager, AdminHelper)
- **Controls/** - Custom WPF controls
- **Themes/** - Material Design theme resources

## 🎯 Key Modules

- **PROGRESS** - Main activity tracking and progress entry module
- **SCHEDULE** - Schedule management (upcoming)
- **REPORTS** - Report generation and export
- **ANALYSIS** - Data analysis and visualization (upcoming)
- **ADMIN** - Administrative functions and user management

## 📝 License

*License information to be added*

## 👥 Contributing

*Contribution guidelines to be added*

## 📧 Contact

*Contact information to be added Soon*
