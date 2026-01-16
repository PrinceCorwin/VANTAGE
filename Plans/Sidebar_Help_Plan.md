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

| # | Section | Anchor | Description |
|---|---------|--------|-------------|
| 1 | Getting Started | `#getting-started` | What is MILESTONE, prerequisites |
| 2 | Main Interface | `#main-interface` | Layout, navigation, menus, status bar, help sidebar |
| 3 | Progress Module | `#progress-module` | Activity tracking, filtering, editing, sync |
| 4 | Schedule Module | `#schedule-module` | Three-week lookahead, P6 integration, missed reasons |
| 5 | Work Packages | `#work-packages` | WP creation, forms, drawings, PDF generation |
| 6 | Progress Books | `#progress-books` | Printed checklists for field crews |
| 7 | Administration | `#administration` | User management, project setup |
| 8 | Reference | `#reference` | Glossary, troubleshooting |

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

### 1. Getting Started
- What is MILESTONE?
- Before You Begin (admin setup, no login, first sync)

### 2. Main Interface
- Layout (toolbar, content area, status bar)
- Navigation (module buttons)
- Menus (File, Tools, Admin, Settings)
- Status Bar
- Help Sidebar
- Keyboard Shortcuts

### 3. Progress Module ✅ WRITTEN
- Overview, typical workflow, layout
- Toolbar (percent buttons, sync, submit, summary panel)
- Sidebar filters (progress, attribute, user, column header)
- Grid editing (ownership, multi-select, date rules, context menu)
- Required metadata fields (9 fields listed)
- Syncing data (SYNC and SUBMIT WEEK workflows)
- Column layouts
- Keyboard shortcuts

### 4. Schedule Module ✅ WRITTEN (needs refinement)
- Overview with prerequisites note
- Layout (master/detail grids, snapshot data warning)
- Color coding (red = required, yellow = discrepancy)
- Toolbar (filters, action buttons, column header filtering)
- Master grid columns (P6 data, MS rollups, editable fields)
- Detail grid (editable columns, auto-date behavior)
- Three-Week Lookahead (when required, when locked, workflow)
- Missed Reasons (when required, auto-populated, workflow)
- Discrepancies (filter types)
- P6 Import (steps, column mapping, warning)
- P6 Export (steps, exported data)
- Saving changes
- Keyboard shortcuts

### 5. Work Packages
- What is a work package
- Creating new packages
- Selecting activities and forms
- Adding drawings (DWG Log)
- Generating PDF
- Using templates

### 6. Progress Books
- What is a progress book
- Purpose: printed checklists for field crews
- Creating and customizing
- Entering progress from completed books

### 7. Administration
- User management
- Project setup
- Database operations
- Logs

### 8. Reference
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

## Search Functionality ✅ COMPLETE

**Completed:** January 16, 2026

### Features
- Search field below tabs, above content
- WebView2 Find API with yellow highlight on all matches
- ˄/˅ navigation buttons with match counter ("3 of 12")
- 300ms debounce on search input
- Enter/Shift+Enter keyboard shortcuts for next/previous
- Search clears when switching modules

### Technical Implementation
```csharp
// WebView2 Find API (requires v1.0.3405.78+)
var findOptions = webViewHelp.CoreWebView2.Environment.CreateFindOptions();
findOptions.FindTerm = searchText;
findOptions.SuppressDefaultFindDialog = true;
findOptions.ShouldHighlightAllMatches = true;
await webViewHelp.CoreWebView2.Find.StartAsync(findOptions);
```

### Files Modified
| File | Change |
|------|--------|
| `ViewModels/SidePanelViewModel.cs` | Added SearchText, MatchCount, CurrentMatchIndex properties |
| `Views/SidePanelView.xaml` | Added search row with TextBox, nav buttons, match counter |
| `Views/SidePanelView.xaml.cs` | Wired up Find API, debounce timer, keyboard shortcuts |

---

## Interactive Help Mode 🔶 SHELVED

**Status:** Shelved as of January 16, 2026
**Reason:** Search functionality and control tooltips provide sufficient help discovery
**Resume:** Can be implemented later if users request click-to-navigate help

### Original Concept
- Toggle button enters Interactive Mode
- Light blue overlay appears over ContentArea
- Cursor changes to help cursor (?) over mapped controls
- Clicking a control navigates help to that control's documentation
- `HelpMapping.Topic` attached property marks controls with help anchors

### Files That Would Be Created (if resumed)
| File | Purpose |
|------|---------|
| `Utilities/HelpMapping.cs` | Attached property for help topic |
| `Views/InteractiveHelpOverlay.xaml` | Overlay UserControl |
| `Views/InteractiveHelpOverlay.xaml.cs` | Hit testing and event logic |

---

## Future Enhancements

- Bookmark/favorite sections
- Print directly from Help panel
- PDF export of help documentation
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

### January 16, 2026
- Implemented Help search functionality (WebView2 Find API)
- Added search field with ˄/˅ navigation buttons and match counter
- Added 300ms debounce, Enter/Shift+Enter keyboard shortcuts
- Rewrote Getting Started section (What is MILESTONE, Before You Begin)
- Added Main Interface as new Section 2 (Layout, Navigation, Menus, Status Bar, Help Sidebar, Shortcuts)
- Wrote comprehensive Progress Module section (10 subsections with full detail)
- Wrote comprehensive Schedule Module section (13 subsections with full detail)
- Added nested TOC with all subsection links and anchor IDs
- Styled TOC with larger main sections, indented subsections
- Renumbered all sections (now 8 total)

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
