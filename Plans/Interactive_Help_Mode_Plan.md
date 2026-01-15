# Help Sidebar Enhancement Plan

## Overview

Two enhancements to the Help sidebar:

1. **Search Field** — Text search within help documentation with find-next navigation ✅ COMPLETE
2. **Interactive Help Mode** — Click UI controls to navigate to their documentation 🔶 SHELVED

---

## Feature 1: Help Search Field ✅ COMPLETE

**Completed:** January 16, 2026

### User Flow

```
1. User opens Help sidebar (F1)
2. Search field visible below tabs, above content
3. User types search term (e.g., "sync")
4. WebView2 highlights all matches in yellow
5. First match scrolls into view
6. User clicks ˄/˅ buttons to navigate between matches
7. Match counter shows "3 of 12"
8. User clears search or types new term
9. Highlights clear, new search begins
```

### UI Design

```
┌─────────────────────────────────────────────────┐
│  [Help] [AI Assistant]                    [✕]   │
├─────────────────────────────────────────────────┤
│  Progress Module                                │
├─────────────────────────────────────────────────┤
│  [Search help...           ] [˄] [˅]  3 of 12   │
├─────────────────────────────────────────────────┤
│                                                 │
│            Help Content (WebView2)              │
│            with highlighted matches             │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Technical Implementation

WebView2 Find API (requires v1.0.3405.78+):

```csharp
// Create find options via factory method
var findOptions = webViewHelp.CoreWebView2.Environment.CreateFindOptions();
findOptions.FindTerm = searchText;
findOptions.SuppressDefaultFindDialog = true;
findOptions.ShouldHighlightAllMatches = true;

// Start search
await webViewHelp.CoreWebView2.Find.StartAsync(findOptions);

// Navigate matches
webViewHelp.CoreWebView2.Find.FindNext();
webViewHelp.CoreWebView2.Find.FindPrevious();

// Stop search (clears highlights)
webViewHelp.CoreWebView2.Find.Stop();

// Get match info via events
webViewHelp.CoreWebView2.Find.MatchCountChanged += ...
webViewHelp.CoreWebView2.Find.ActiveMatchIndexChanged += ...
```

### Search Behavior

| Action | Result |
|--------|--------|
| Type in search box | Auto-search after 300ms debounce |
| Press Enter | Find next match |
| Press Shift+Enter | Find previous match |
| Click ˄ | Find previous match |
| Click ˅ | Find next match |
| Press Escape | Clear search |
| Clear search box | Clear all highlights |
| Switch modules | Clear search, navigate to module section |

### Files Modified

| File | Change |
|------|--------|
| `ViewModels/SidePanelViewModel.cs` | Added SearchText, MatchCount, CurrentMatchIndex properties |
| `Views/SidePanelView.xaml` | Added search row with TextBox, nav buttons, match counter |
| `Views/SidePanelView.xaml.cs` | Wired up Find API, debounce timer, keyboard shortcuts |

---

## Feature 2: Interactive Help Mode 🔶 SHELVED

**Status:** Shelved as of January 16, 2026  
**Reason:** Search functionality and control tooltips provide sufficient help discovery for now  
**Resume:** Can be implemented later if users request click-to-navigate help

### Original Concept

User clicks a toggle button to enter Interactive Mode:
- Light blue overlay appears over ContentArea
- Cursor changes to help cursor (?) over mapped controls
- Clicking a control navigates help to that control's documentation
- `HelpMapping.Topic` attached property marks controls with help anchors

### Shelved Implementation Steps

#### Phase 2: Interactive Mode Infrastructure (SHELVED)
- [ ] Create `HelpMapping` attached property class
- [ ] Create `InteractiveHelpOverlay` UserControl
- [ ] Add `IsInteractiveMode` property to `SidePanelViewModel`
- [ ] Add overlay to MainWindow (hidden by default)
- [ ] Wire up show/hide based on IsInteractiveMode

#### Phase 3: Interactive Mode Hit Testing (SHELVED)
- [ ] Implement overlay mouse click handler
- [ ] Implement VisualTreeHelper hit testing
- [ ] Walk tree to find HelpMapping.Topic
- [ ] Raise event when topic found
- [ ] Connect event to SidePanelViewModel navigation

#### Phase 4: Interactive Mode Visual Polish (SHELVED)
- [ ] Add toggle button to sidebar header
- [ ] Style overlay (blue tint, centered text)
- [ ] Implement cursor change on hover

#### Phases 5-8: Control Mapping (SHELVED)
- [ ] Add HelpMapping.Topic to Progress, Schedule, Work Package, Progress Books controls
- [ ] Update manual.html with control-specific sections

### Files That Would Be Created (if resumed)

| File | Purpose |
|------|---------|
| `Utilities/HelpMapping.cs` | Attached property for help topic |
| `Views/InteractiveHelpOverlay.xaml` | Overlay UserControl |
| `Views/InteractiveHelpOverlay.xaml.cs` | Hit testing and event logic |

---

## Implementation Summary

### Phase 1: Search Field ✅ COMPLETE
1. [x] Add search TextBox to SidePanelView below context header
2. [x] Add ˄/˅ navigation buttons and match counter
3. [x] Implement debounce timer for search input (300ms)
4. [x] Wire up CoreWebView2.Find API for search
5. [x] Implement FindNext/FindPrevious on button clicks
6. [x] Implement Enter/Shift+Enter keyboard shortcuts
7. [x] Clear search when module changes

### Phases 2-8: Interactive Mode 🔶 SHELVED
8-30. [ ] All items shelved — see details above

---

## Notes

- Search field only visible on Help tab, hidden on AI tab
- Search clears when navigating to different module
- Tooltips on nav buttons visible even when disabled (ToolTipService.ShowOnDisabled)
- Uses WebView2's native find with SuppressDefaultFindDialog=true
