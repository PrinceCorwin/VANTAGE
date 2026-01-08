# Sidebar Help Tab Status

## Last Updated: January 7, 2026

---

## Summary

| Phase | Status | Progress |
|-------|--------|----------|
| Sidebar Infrastructure | Complete | 100% |
| Help Content Writing | In Progress | 10% |
| Screenshots | Not Started | 0% |
| PDF Export | Not Started | 0% |

---

## Phase 1: Sidebar Infrastructure

### Completed Items

| Date | Item | Notes |
|------|------|-------|
| 2026-01-07 | WebView2 NuGet package | Added Microsoft.Web.WebView2 |
| 2026-01-07 | IHelpAware interface | `Interfaces/IHelpAware.cs` - provides HelpAnchor and ModuleDisplayName |
| 2026-01-07 | SidePanelViewModel | `ViewModels/SidePanelViewModel.cs` - manages panel state, persistence |
| 2026-01-07 | SidePanelView | `Views/SidePanelView.xaml/.cs` - WebView2 + tab UI |
| 2026-01-07 | MainWindow split layout | Grid with ContentArea, GridSplitter, SidePanel |
| 2026-01-07 | Help menu + popup | ? button with "Help / AI Sidebar" and "About MILESTONE" |
| 2026-01-07 | Keyboard shortcuts | F1 opens sidebar, Esc closes |
| 2026-01-07 | Splitter persistence | Width saved to UserSettings on drag complete |
| 2026-01-07 | Context-aware navigation | Sidebar updates when switching modules |
| 2026-01-07 | IHelpAware on ProgressViewModel | HelpAnchor: "progress-module" |
| 2026-01-07 | IHelpAware on ScheduleViewModel | HelpAnchor: "schedule-module" |
| 2026-01-07 | IHelpAware on WorkPackageView | HelpAnchor: "work-packages" |
| 2026-01-07 | ProgressBooksView placeholder | `Views/ProgressBooksView.xaml/.cs` with IHelpAware |
| 2026-01-07 | Help manual HTML | `Help/manual.html` - FluentDark styling, all anchor sections |

### Files Created

- `Interfaces/IHelpAware.cs`
- `ViewModels/SidePanelViewModel.cs`
- `Views/SidePanelView.xaml`
- `Views/SidePanelView.xaml.cs`
- `Views/ProgressBooksView.xaml`
- `Views/ProgressBooksView.xaml.cs`
- `Help/manual.html`

### Files Modified

- `VANTAGE.csproj` - Added WebView2 package, Help file copy directive
- `MainWindow.xaml` - Added Help button/popup, split layout for sidebar
- `MainWindow.xaml.cs` - Added sidebar initialization, toggle, context updates
- `ViewModels/ProgressViewModel.cs` - Added IHelpAware implementation
- `ViewModels/ScheduleViewModel.cs` - Added IHelpAware implementation
- `Views/WorkPackageView.xaml.cs` - Added IHelpAware implementation

---

## Phase 2: Help Content Writing

### Completed Items

| Date | Item | Notes |
|------|------|-------|
| 2026-01-07 | HTML structure | All 7 sections with anchors |
| 2026-01-07 | FluentDark CSS styling | Matches app theme |
| 2026-01-07 | Table of contents | Clickable navigation |
| 2026-01-07 | Placeholder boxes | For future screenshots |

### Remaining Items

| Section | Status | Notes |
|---------|--------|-------|
| Getting Started | Placeholder | Write actual content |
| Progress Module | Placeholder | Write actual content |
| Schedule Module | Placeholder | Write actual content |
| Work Packages | Placeholder | Write actual content |
| Progress Books | Placeholder | Write actual content |
| Administration | Placeholder | Write actual content |
| Reference | Placeholder | Write actual content |

---

## Phase 3: Screenshots

### Status: Not Started

Screenshots needed (45+ total):

**Getting Started (4)**
- Login screen
- Main interface overview
- Navigation menu
- Sync status indicator

**Progress Module (11)**
- Full view, toolbar, menus (File/Edit/View)
- Import dialog, grid columns, context menu
- Filter panel, assign dialog, sync button

**Schedule Module (6)**
- Full view, toolbar, menus
- Lookahead view, missed reason, P6 sync

**Work Packages (9)**
- Full view, toolbar, menus
- Create dialog, activity selection, form selection
- DWG log, generate PDF, templates

**Progress Books (8)**
- Full view, toolbar, menus
- Create dialog, grouping, columns
- Print preview, sample output

**Administration (5)**
- User management, add user
- Project setup, sync status, logs

**Reference (1)**
- Sample error dialog

---

## Phase 4: PDF Export

### Status: Not Started

| Item | Status | Notes |
|------|--------|-------|
| PDF generation method | Not Started | Pandoc CLI or alternative |
| "Download PDF" menu item | Not Started | Add to Help menu |
| Test downloadable manual | Not Started | Verify formatting |

---

## Known Issues

*None currently*

---

## Notes & Decisions

| Date | Decision |
|------|----------|
| 2026-01-07 | Sidebar uses single implementation in MainWindow (not per-module) |
| 2026-01-07 | WorkPackageView implements IHelpAware directly (no ViewModel yet) |
| 2026-01-07 | Screenshots will be captured with ShareX |

---

## Content Status Tracker

| Section | Draft | Screenshots | Review | Final |
|---------|-------|-------------|--------|-------|
| 1. Getting Started | [ ] | [ ] | [ ] | [ ] |
| 2. Progress Module | [ ] | [ ] | [ ] | [ ] |
| 3. Schedule Module | [ ] | [ ] | [ ] | [ ] |
| 4. Work Packages | [ ] | [ ] | [ ] | [ ] |
| 5. Progress Books | [ ] | [ ] | [ ] | [ ] |
| 6. Administration | [ ] | [ ] | [ ] | [ ] |
| 7. Reference | [ ] | [ ] | [ ] | [ ] |
