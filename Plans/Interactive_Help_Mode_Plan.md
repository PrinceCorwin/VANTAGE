# Help Sidebar Enhancement Plan

## Overview

Two enhancements to the Help sidebar:

1. **Search Field** — Text search within help documentation with find-next navigation
2. **Interactive Help Mode** — Click UI controls to navigate to their documentation

---

## Feature 1: Help Search Field

### User Flow

```
1. User opens Help sidebar (F1)
2. Search field visible below tabs, above content
3. User types search term (e.g., "sync")
4. WebView2 highlights all matches in yellow
5. First match scrolls into view
6. User clicks ▲/▼ buttons to navigate between matches
7. Match counter shows "3 of 12"
8. User clears search or types new term
9. Highlights clear, new search begins
```

### UI Design

```
┌─────────────────────────────────────────────────┐
│  [Help] [AI Assistant]           [?] [✕]        │
├─────────────────────────────────────────────────┤
│  Progress Module                                │
├─────────────────────────────────────────────────┤
│  [🔍 Search help...         ] [▲] [▼]  3 of 12  │
├─────────────────────────────────────────────────┤
│                                                 │
│            Help Content (WebView2)              │
│            with highlighted matches             │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Technical Approach

WebView2 has built-in find functionality:

```csharp
// Start search
await webViewHelp.CoreWebView2.FindController.StartFindAsync(
    searchText, 
    new CoreWebView2FindOptions());

// Navigate matches
webViewHelp.CoreWebView2.FindController.FindNext();
webViewHelp.CoreWebView2.FindController.FindPrevious();

// Stop search (clears highlights)
webViewHelp.CoreWebView2.FindController.StopFind();
```

### Search Behavior

| Action | Result |
|--------|--------|
| Type in search box | Auto-search after 300ms debounce |
| Press Enter | Find next match |
| Press Shift+Enter | Find previous match |
| Click ▲ | Find previous match |
| Click ▼ | Find next match |
| Clear search box | Clear all highlights |
| Switch modules | Clear search, navigate to module section |

---

## Feature 2: Interactive Help Mode

### User Flow

```
1. User opens Help sidebar (F1)
2. Sidebar shows help content, app works normally
3. User clicks "Interactive Mode" toggle button in sidebar
4. Light blue overlay appears over ContentArea
5. Text displays: "INTERACTIVE MODE ENABLED"
6. Cursor changes to help cursor (?) when over mapped controls
7. User clicks a control (e.g., SYNC button)
8. Help scrolls to #progress-sync-button section
9. User can click more controls to explore
10. User clicks toggle again or presses Esc to exit
11. Overlay disappears, normal operation resumes
```

### UI Design

#### Sidebar Toggle Button

```
┌─────────────────────────────────────────────────┐
│  [Help] [AI Assistant]           [?] [✕]        │
│                                   │             │
│                      Interactive Mode toggle    │
└─────────────────────────────────────────────────┘
```

Button states:
- Off: Outline style, "?" icon or cursor icon
- On: Filled accent color, indicating active

#### Overlay Appearance

```
┌─────────────────────────────────────────────────┐
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │
│ ░░░░░  INTERACTIVE MODE ENABLED  ░░░░░░░░░░░░░ │
│ ░░░░░  Click any control for help ░░░░░░░░░░░░ │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │
└─────────────────────────────────────────────────┘

Overlay color: #0078D4 (AccentColor) at 10-15% opacity
Text: Centered, white with dark shadow for readability
```

#### Cursor Behavior

| Hover Target | Cursor |
|--------------|--------|
| Mapped control (has HelpTopic) | Help cursor (?) |
| Unmapped control | Default arrow |
| Overlay background | Default arrow |

---

## Technical Architecture

### Search Field Components

| Component | Purpose |
|-----------|---------|
| Search TextBox | User input field |
| Navigation buttons | ▲/▼ for prev/next match |
| Match counter | "X of Y" display |
| Debounce timer | Delay search until typing stops |
| CoreWebView2.FindController | Built-in WebView2 search API |

### Interactive Mode Components

| Component | Purpose |
|-----------|---------|
| `HelpMapping` attached property | Stores help anchor ID on controls |
| `InteractiveHelpOverlay` UserControl | Transparent overlay with hit testing |
| `SidePanelViewModel.IsInteractiveMode` | Toggle state property |
| `MainWindow` overlay hosting | Shows/hides overlay based on mode |

### Attached Property: HelpMapping.Topic

```csharp
public static class HelpMapping
{
    public static readonly DependencyProperty TopicProperty =
        DependencyProperty.RegisterAttached(
            "Topic",
            typeof(string),
            typeof(HelpMapping),
            new PropertyMetadata(null));

    public static string GetTopic(DependencyObject obj) => (string)obj.GetValue(TopicProperty);
    public static void SetTopic(DependencyObject obj, string value) => obj.SetValue(TopicProperty, value);
}
```

Usage in XAML:
```xml
<Button x:Name="btnSync" 
        local:HelpMapping.Topic="progress-sync-button"
        Content="SYNC"/>
```

### Overlay Click Handling

```
User clicks overlay
        ↓
InteractiveHelpOverlay.OnMouseLeftButtonDown
        ↓
Get click position relative to ContentArea
        ↓
VisualTreeHelper.HitTest(ContentArea, clickPoint)
        ↓
Walk visual tree upward from hit element
        ↓
Find first element with HelpMapping.Topic attached property
        ↓
If found: Raise HelpTopicClicked event with topic string
If not found: Ignore click (no action)
        ↓
SidePanelViewModel receives topic
        ↓
Navigate WebView to #topic anchor
```

### State Management

```csharp
// SidePanelViewModel
public bool IsInteractiveMode
{
    get => _isInteractiveMode;
    set
    {
        _isInteractiveMode = value;
        OnPropertyChanged(nameof(IsInteractiveMode));
        // MainWindow listens and shows/hides overlay
    }
}
```

---

## Module-Specific Mapping Scope

### Progress Module

| Control Type | Include | Examples |
|--------------|---------|----------|
| Toolbar buttons | Yes | Refresh, Sync, Export, Import |
| Dropdown menus | Yes | File, Edit, View menus |
| Quick filter buttons | Yes | Complete, Not Complete, My Records |
| Filter panel controls | Yes | Search box, Clear Filters |
| Grid (general) | Yes | Single topic: "progress-grid" |
| Individual grid columns | No | — |
| Column visibility list | Yes | Single topic |

### Schedule Module

| Control Type | Include | Examples |
|--------------|---------|----------|
| Toolbar buttons | Yes | Save, Export, Date picker |
| Dropdown menus | Yes | File, Edit menus |
| Filter controls | Yes | Status filters |
| Grid (general) | Yes | Single topic: "schedule-grid" |
| Individual grid columns | No | — |
| Detail panel | Yes | Single topic |

### Work Package Module

| Control Type | Include | Examples |
|--------------|---------|----------|
| All buttons | Yes | Generate, Browse, Preview |
| All dropdowns | Yes | Template selectors |
| Tab controls | Yes | WP Templates, Form Templates |
| List boxes | Yes | Work package list, Form list |
| Text inputs | Yes | Name pattern, paths |

### Progress Books Module

| Control Type | Include | Examples |
|--------------|---------|----------|
| All buttons | Yes | TBD when module developed |
| All dropdowns | Yes | TBD |
| Grouping selectors | Yes | Area, Module, Drawing |

---

## Help HTML Requirements

Each mapped control needs a corresponding anchor in manual.html:

```html
<!-- Progress Module - Toolbar -->
<h3 id="progress-refresh-button">Refresh Button</h3>
<p>Reloads activity data from the local database...</p>

<h3 id="progress-sync-button">Sync Button</h3>
<p>Uploads your changes and downloads updates from the central database...</p>

<h3 id="progress-export-button">Export Button</h3>
<p>Exports the current filtered view to Excel...</p>

<!-- Progress Module - Grid -->
<h3 id="progress-grid">Activity Grid</h3>
<p>The main data grid displays all activities matching your current filters...</p>
```

---

## Implementation Steps

### Phase 1: Search Field
1. [ ] Add search TextBox to SidePanelView below context header
2. [ ] Add ▲/▼ navigation buttons and match counter
3. [ ] Implement debounce timer for search input
4. [ ] Wire up CoreWebView2.FindController for search
5. [ ] Implement FindNext/FindPrevious on button clicks
6. [ ] Implement Enter/Shift+Enter keyboard shortcuts
7. [ ] Clear search when module changes

### Phase 2: Interactive Mode Infrastructure
8. [ ] Create `HelpMapping` attached property class
9. [ ] Create `InteractiveHelpOverlay` UserControl
10. [ ] Add `IsInteractiveMode` property to `SidePanelViewModel`
11. [ ] Add overlay to MainWindow (hidden by default)
12. [ ] Wire up show/hide based on IsInteractiveMode

### Phase 3: Interactive Mode Hit Testing
13. [ ] Implement overlay mouse click handler
14. [ ] Implement VisualTreeHelper hit testing
15. [ ] Walk tree to find HelpMapping.Topic
16. [ ] Raise event when topic found
17. [ ] Connect event to SidePanelViewModel navigation

### Phase 4: Interactive Mode Visual Polish
18. [ ] Add toggle button to sidebar header
19. [ ] Style overlay (blue tint, centered text)
20. [ ] Implement cursor change on hover

### Phase 5: Control Mapping - Progress
21. [ ] Add HelpMapping.Topic to Progress toolbar buttons
22. [ ] Add HelpMapping.Topic to Progress filter controls
23. [ ] Add HelpMapping.Topic to Progress grid area
24. [ ] Update manual.html with Progress control sections

### Phase 6: Control Mapping - Schedule
25. [ ] Add HelpMapping.Topic to Schedule controls
26. [ ] Update manual.html with Schedule control sections

### Phase 7: Control Mapping - Work Package
27. [ ] Add HelpMapping.Topic to Work Package controls
28. [ ] Update manual.html with Work Package control sections

### Phase 8: Control Mapping - Progress Books
29. [ ] Add HelpMapping.Topic to Progress Books controls
30. [ ] Update manual.html with Progress Books control sections

---

## Files to Create

| File | Purpose |
|------|---------|
| `Utilities/HelpMapping.cs` | Attached property for help topic |
| `Views/InteractiveHelpOverlay.xaml` | Overlay UserControl |
| `Views/InteractiveHelpOverlay.xaml.cs` | Hit testing and event logic |

## Files to Modify

| File | Change |
|------|--------|
| `ViewModels/SidePanelViewModel.cs` | Add IsInteractiveMode, search text properties |
| `Views/SidePanelView.xaml` | Add search field, toggle button |
| `Views/SidePanelView.xaml.cs` | Wire search to FindController, toggle to ViewModel |
| `MainWindow.xaml` | Add overlay element |
| `MainWindow.xaml.cs` | Show/hide overlay on mode change |
| `Views/ProgressView.xaml` | Add HelpMapping.Topic to controls |
| `Views/ScheduleView.xaml` | Add HelpMapping.Topic to controls |
| `Views/WorkPackageView.xaml` | Add HelpMapping.Topic to controls |
| `Views/ProgressBooksView.xaml` | Add HelpMapping.Topic to controls |
| `Help/manual.html` | Add control-specific sections |

---

## Exit Conditions (Interactive Mode)

Interactive Mode deactivates when:
- User clicks toggle button again
- User presses Escape key
- User closes sidebar entirely
- User switches to AI tab (auto-navigates to #ai-assistant section first)

---

## Decisions Made

| Question | Decision |
|----------|----------|
| AI tab behavior | Switching to AI tab disables Interactive Mode, navigates Help to #ai-assistant |
| Hover highlight | No highlight, just cursor change to help cursor (?) |
| Toggle off behavior | Just remove overlay, don't close sidebar |
| Grid granularity | One help topic per grid (progress-grid, schedule-grid, etc.) |

---

## Notes

- Overlay must be above ContentArea but below sidebar
- Z-order: ContentArea (bottom) → Overlay → GridSplitter → Sidebar (top)
- Overlay does not cover sidebar - user can still scroll help content
- Search field only visible on Help tab, hidden on AI tab
- Search clears when navigating to different module (via Interactive Mode or navigation buttons)
