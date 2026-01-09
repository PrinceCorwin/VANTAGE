# Sidebar Help Tab Implementation Plan

## Overview

The Help tab in the collapsible right-side panel provides context-aware help documentation. Uses WebView2 to display styled HTML content that matches the FluentDark theme.

---

## Architecture

**Approach:** Single sidebar in MainWindow

**Rationale:**
- Zero redundant code across modules
- Panel state persists across module navigation
- WebView2 instance reused (better memory, no reload flicker)
- Single point of maintenance

---

## Components

### 1. IHelpAware Interface

Location: `Interfaces/IHelpAware.cs`

```csharp
public interface IHelpAware
{
    string HelpAnchor { get; }       // e.g., "progress-module"
    string ModuleDisplayName { get; } // e.g., "Progress Module"
}
```

Each module ViewModel implements this interface to provide context.

### 2. SidePanel UserControl (Help Tab)

Location: `Views/SidePanelView.xaml` + `ViewModels/SidePanelViewModel.cs`

**Features:**
- WebView2 for HTML help content display
- Collapse/expand toggle
- Resizable via GridSplitter
- Context-aware anchor navigation

### 3. MainWindow Integration

- Split layout: Left (content) | GridSplitter | Right (sidebar)
- Help menu added to toolbar (before hamburger menu)
- F1 keyboard shortcut opens sidebar

---

## UI Layout

```
+------------------------------------------------------------------+
| Toolbar                                    [?] Help  [hamburger]  |
+--------------------------------------+--------+------------------+
|                                      |  |||   | [Help] [AI]      |
|                                      |  |||   |                  |
|     Current Module View              |  |||   |   Help Content   |
|     (Progress/Schedule/WP/etc)       |  |||   |   (WebView2)     |
|                                      |  |||   |                  |
|                                      |  |||   |                  |
+--------------------------------------+--------+------------------+
                                       GridSplitter
```

---

## Help Menu Structure

```
Help [?]
+-- Help / AI Sidebar     -> Opens sidebar, navigates to context-appropriate section
+-- -----------------
+-- About MILESTONE
```

---

## Help Documentation

### Source Format
- Single HTML file: `Help/manual.html`
- FluentDark CSS styling embedded
- Anchor IDs for each section (e.g., `#progress-module`)

### In-App Display
- WebView2 displays HTML, navigates to anchor based on context
- Table of contents with clickable navigation
- Placeholder boxes for future screenshots

### Downloadable Format (Future)
- PDF generated from same source
- Available via Help menu: "Download User Manual (PDF)"

---

## Help Content Structure

| Section | Anchor | Description |
|---------|--------|-------------|
| Getting Started | `#getting-started` | Login, main interface, offline/online |
| Progress Module | `#progress-module` | Activity tracking, filtering, sync |
| Schedule Module | `#schedule-module` | Three-week lookahead, P6 integration |
| Work Packages | `#work-packages` | WP creation, forms, drawings, PDF generation |
| Progress Books | `#progress-books` | Printed checklists for field crews |
| Administration | `#administration` | User management, project setup |
| Reference | `#reference` | Keyboard shortcuts, glossary, troubleshooting |

---

## Sidebar State Persistence

Stored in user preferences JSON:
- `SidebarOpen`: bool
- `SidebarWidth`: double
- `ActiveTab`: string ("Help" | "AI")

---

## GridSplitter Behavior

- Minimum sidebar width: 300px
- Maximum sidebar width: 50% of window
- Default width: 400px
- Collapsed state: 0px (hidden)

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| F1 | Open sidebar to Help tab |
| Esc | Close sidebar (when focused) |

---

## Dependencies

| Package | Purpose |
|---------|---------|
| Microsoft.Web.WebView2 | HTML help display |
| (Optional) Pandoc CLI | PDF generation |

---

## Design Considerations

### FluentDark Theme Compatibility
- Help HTML uses dark theme styling
- Matches Syncfusion FluentDark colors
- WebView2 background matches app background

### Accessibility
- Keyboard navigation support
- Screen reader friendly HTML structure
- Proper heading hierarchy in documentation

### Performance
- WebView2 initialized once, reused
- HTML loaded once, anchor navigation only

---

## Help Content Outline

See detailed content structure in sections below:

### Getting Started
- What is MILESTONE?
- Logging In
- The Main Interface (annotated screenshot)
- Offline vs. Online

### Progress Module
- Overview and when to use
- Toolbar buttons documented
- Menu items documented
- Activity grid usage (sorting, filtering, context menu)
- Common tasks: Import, Assign, Update Progress, Filter, Search, Export, Sync

### Schedule Module
- Overview and P6 connection
- Three-week lookahead concept
- Marking activities complete
- Recording missed reasons
- Filtering and syncing with P6

### Work Packages
- What is a work package
- Creating new packages
- Selecting activities and forms
- Adding drawings (DWG Log)
- Generating PDF
- Using templates

### Progress Books
- What is a progress book
- Purpose: printed checklists for field crews
- Creating and customizing
- Entering progress from completed books

### Administration
- User management
- Project setup
- Database operations
- Logs

### Reference
- Keyboard shortcuts table
- Glossary of terms
- Troubleshooting / FAQ
- Getting help

---

## Screenshot Requirements

Each section should include screenshots showing:
- Toolbar with buttons labeled
- Menu dropdowns expanded
- Dialog boxes for key operations
- Grid views with sample data (anonymized)
- Before/after for multi-step processes

**Naming convention:** `[section]-[description].png`

**Annotation style:**
- Red circles/rectangles for highlighting
- Numbered callouts (1, 2, 3) with legend
- Arrows pointing to specific buttons/fields
- Blur or redact sensitive data

**Estimated total:** 45+ screenshots across all sections

---

## Future Enhancements

- Search within help documentation
- Bookmark/favorite sections
- Print directly from Help panel
- AI-suggested help topics based on user actions

---

## Completed Implementation

### Files Created
| File | Purpose |
|------|---------|
| Interfaces/IHelpAware.cs | Interface for module context |
| ViewModels/SidePanelViewModel.cs | Panel state management |
| Views/SidePanelView.xaml | WebView2 + tab UI |
| Views/SidePanelView.xaml.cs | View code-behind |
| Views/ProgressBooksView.xaml(.cs) | Placeholder view with IHelpAware |
| Help/manual.html | FluentDark styled help content |

### Files Modified
| File | Change |
|------|--------|
| VANTAGE.csproj | Added WebView2 package, Help file copy directive |
| MainWindow.xaml | Added Help button/popup, split layout for sidebar |
| MainWindow.xaml.cs | Sidebar initialization, toggle, context updates |
| ViewModels/ProgressViewModel.cs | Added IHelpAware implementation |
| ViewModels/ScheduleViewModel.cs | Added IHelpAware implementation |
| Views/WorkPackageView.xaml.cs | Added IHelpAware implementation |

### January 7, 2026
- Added WebView2 NuGet package
- Created IHelpAware interface with HelpAnchor and ModuleDisplayName
- Created SidePanelViewModel with panel state and persistence
- Created SidePanelView with WebView2 and tab UI
- Added MainWindow split layout (ContentArea, GridSplitter, SidePanel)
- Added Help menu with popup (Help/AI Sidebar, About MILESTONE)
- Implemented F1 keyboard shortcut to open sidebar, Esc to close
- Added splitter width persistence to UserSettings
- Implemented context-aware navigation (updates when switching modules)
- Added IHelpAware to ProgressViewModel, ScheduleViewModel, WorkPackageView
- Created ProgressBooksView placeholder with IHelpAware
- Created Help/manual.html with FluentDark styling and all anchor sections
