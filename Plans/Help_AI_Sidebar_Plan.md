# Help / AI Sidebar Implementation Plan

## Overview

A collapsible right-side panel in MainWindow that provides context-aware help documentation and AI assistant functionality. The panel uses a tabbed interface allowing users to switch between Help and AI Assistant while maintaining their current work view.

## Architecture Decision

**Approach:** Single sidebar in MainWindow (Option A)

**Rationale:**
- Zero redundant code across modules
- Panel state persists across module navigation
- WebView2 instance reused (better memory, no reload flicker)
- AI conversation persists when switching modules
- Single point of maintenance

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

### 2. SidePanel UserControl

Location: `Views/SidePanelView.xaml` + `ViewModels/SidePanelViewModel.cs`

**Features:**
- Tabbed interface (Help | AI Assistant)
- WebView2 for HTML help content display
- AI chat interface (future implementation)
- Collapse/expand toggle
- Resizable via GridSplitter

### 3. MainWindow Modifications

- Split layout: Left (content) | GridSplitter | Right (sidebar)
- Help menu added to toolbar (before hamburger menu)
- Context-aware sidebar activation

## UI Layout

```
┌─────────────────────────────────────────────────────────────────┐
│ Toolbar                                    [?] Help  [☰] Menu   │
├────────────────────────────────────┬────────┬───────────────────┤
│                                    │ ║ ║ ║  │ [Help] [AI]       │
│                                    │ ║ ║ ║  │                   │
│     Current Module View            │ ║ ║ ║  │   Sidebar         │
│     (Progress/Schedule/WP/etc)     │ ║ ║ ║  │   Content         │
│                                    │ ║ ║ ║  │                   │
│                                    │ ║ ║ ║  │                   │
│                                    │ ║ ║ ║  │                   │
└────────────────────────────────────┴────────┴───────────────────┘
                                     GridSplitter
```

## Help Menu Structure

```
Help [?]
├── Help / AI Sidebar     → Opens sidebar, navigates to context-appropriate section
├── ─────────────────
├── (Future items)
└── About MILESTONE
```

## Help Documentation

### Source Format
- Markdown files in `Documentation/Help/` folder
- Version controlled, easy to edit

### In-App Display
- Convert Markdown to HTML at build time (or embed pre-built HTML)
- Single HTML file with CSS styling
- Anchor IDs for each section (e.g., `#progress-module`)
- WebView2 displays HTML, navigates to anchor based on context

### Downloadable Format
- PDF generated from same Markdown source
- Available via Help menu: "Download User Manual (PDF)"

## Help Content Structure

```
Help/
├── index.md              → Table of contents, getting started
├── progress-module.md    → Progress tracking documentation
├── schedule-module.md    → Schedule/lookahead documentation  
├── work-packages.md      → Work package generation documentation
├── progress-books.md     → Progress books documentation
├── admin-tools.md        → Admin functionality (if applicable)
└── assets/
    └── images/           → Screenshots, diagrams
```

## Implementation Steps

### Phase 1: Infrastructure
1. [ ] Add WebView2 NuGet package
2. [ ] Create `IHelpAware` interface
3. [ ] Implement `IHelpAware` on all module ViewModels
4. [ ] Create `SidePanelViewModel`
5. [ ] Create `SidePanelView` UserControl

### Phase 2: MainWindow Integration
6. [ ] Modify MainWindow.xaml for split layout
7. [ ] Add GridSplitter with proper styling
8. [ ] Add Help menu to toolbar
9. [ ] Wire up sidebar toggle command
10. [ ] Implement context-aware navigation

### Phase 3: Help Content
11. [ ] Create Markdown help content structure
12. [ ] Build HTML conversion pipeline (or manual conversion)
13. [ ] Style HTML for FluentDark theme compatibility
14. [ ] Test in-app navigation with anchors

### Phase 4: PDF Export
15. [ ] Implement PDF generation (Pandoc or similar)
16. [ ] Add "Download PDF" menu item
17. [ ] Test downloadable manual

### Phase 5: AI Assistant (Future)
18. [ ] Design AI chat interface
19. [ ] Implement Anthropic API integration
20. [ ] Context-aware prompting based on current module
21. [ ] Conversation persistence

## Technical Details

### WebView2
- NuGet: `Microsoft.Web.WebView2`
- Requires WebView2 Runtime (auto-installs or bundle)
- Navigate to local HTML: `webView.Source = new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Help", "manual.html#anchor"))`

### Sidebar State Persistence
- Store in user preferences JSON:
  - `SidebarOpen`: bool
  - `SidebarWidth`: double
  - `ActiveTab`: string ("Help" | "AI")

### GridSplitter Behavior
- Minimum sidebar width: 300px
- Maximum sidebar width: 50% of window
- Default width: 400px
- Collapsed state: 0px (hidden)

## Dependencies

| Package | Purpose |
|---------|---------|
| Microsoft.Web.WebView2 | HTML help display |
| (Optional) Markdig | Markdown to HTML conversion |
| (Optional) Pandoc CLI | PDF generation |

## Design Considerations

### FluentDark Theme Compatibility
- Help HTML must use dark theme styling
- Match Syncfusion FluentDark colors
- WebView2 background should match app background

### Accessibility
- Keyboard navigation support
- Screen reader friendly HTML structure
- Proper heading hierarchy in documentation

### Performance
- WebView2 initialized once, reused
- HTML loaded once, anchor navigation only
- Lazy load AI tab until first access

## Future Enhancements

- Search within help documentation
- Bookmark/favorite sections
- Print directly from Help panel
- AI assistant with module-specific context injection
- AI-suggested help topics based on user actions
