# Help Documentation Content Plan

## Target Audience

**Primary:** Field Engineers (FEs)

**Writing Guidelines:**
- Clear, direct language
- No high-level project management jargon
- Step-by-step instructions with specific button/menu references
- Screenshots showing exact locations of controls
- Task-focused organization ("How do I..." approach)

---

## Document Structure

### 1. Getting Started

#### 1.1 What is MILESTONE?
- Brief description: construction activity tracking and work package generation
- Replaces paper-based and legacy systems
- Works offline in the field, syncs when connected

#### 1.2 Logging In
- Username and password entry
- First-time login
- What to do if login fails

#### 1.3 The Main Interface
- Screenshot with callouts:
  - Navigation menu (left side)
  - Toolbar (top)
  - Settings menu (hamburger icon)
  - Help menu (? icon)
  - Status bar (if applicable)
- How to switch between modules

#### 1.4 Offline vs. Online
- How to tell which mode you're in
- What works offline
- When and how to sync

---

### 2. Progress Module

#### 2.1 Overview
- What this module does
- When to use it (daily progress entry, reviewing assigned work)

#### 2.2 Toolbar Buttons
- Document each button left to right:
  - Import Excel
  - Export Excel
  - Sync
  - Filter/Search controls
  - Any other toolbar items

#### 2.3 Menu Items
- File menu options (if applicable)
- Edit menu options
- View menu options
- Tools menu options

#### 2.4 The Activity Grid
- Column explanations (reference MILESTONE_Column_Reference.md)
- Sorting columns
- Filtering data
- Selecting rows (single, multiple, all)
- Right-click context menu options

#### 2.5 Common Tasks

**Importing Activity Data**
- Step-by-step: File → Import or toolbar button
- Expected Excel format
- What happens after import
- Troubleshooting import errors

**Assigning Activities to Users**
- Selecting activities
- Using AssignTo feature
- Bulk assignment

**Updating Progress**
- Editing quantity/percentage fields
- What triggers a sync
- Validation rules

**Filtering Your Work**
- Using quick filters
- Advanced filter options
- Saving filter presets (if available)

**Searching for Activities**
- Search box usage
- What fields are searched

**Exporting Data**
- Export to Excel
- What's included in export

#### 2.6 Syncing Progress Data
- When to sync
- Manual sync button
- What happens during sync
- Conflict resolution

---

### 3. Schedule Module

#### 3.1 Overview
- What this module does
- Connection to P6 Primavera
- Three-week lookahead concept

#### 3.2 Toolbar Buttons
- Document each button

#### 3.3 Menu Items
- Document each menu and its options

#### 3.4 The Schedule Grid
- Column explanations
- Date columns and formatting
- Status indicators

#### 3.5 Common Tasks

**Viewing the Three-Week Lookahead**
- How the view is organized
- Navigating weeks

**Marking Activities Complete**
- Updating actual dates
- Progress percentage

**Recording Missed Reasons**
- When to use missed reasons
- Selecting from reason list
- Adding notes

**Filtering Schedule Views**
- By date range
- By status
- By assignment

**Syncing with P6**
- Import from P6
- Export to P6
- Sync conflicts

---

### 4. Work Packages

#### 4.1 Overview
- What is a work package
- What documents are included
- When to create work packages

#### 4.2 Toolbar Buttons
- Document each button

#### 4.3 Menu Items
- Document each menu and its options

#### 4.4 Common Tasks

**Creating a New Work Package**
- Step-by-step process
- Naming conventions

**Selecting Activities**
- How to pick which activities to include
- Filtering to find activities

**Selecting Forms**
- Available form types
- Customizing form selection

**Adding Drawings (DWG Log)**
- Procore integration (when available)
- Manual drawing entry
- Drawing list management

**Generating the PDF**
- Generate button
- Preview options
- Save location

**Using Templates**
- What templates are available
- Applying a template
- Creating custom templates (if available)

---

### 5. Progress Books

#### 5.1 Overview
- What is a progress book
- Purpose: printed checklists for field crews
- How field crews use them (check off completed items)
- Turning in progress books to FEs for data entry

#### 5.2 Toolbar Buttons
- Document each button

#### 5.3 Menu Items
- Document each menu and its options

#### 5.4 Understanding Progress Book Columns
- Budget
- Percent Complete
- Quantity
- ROC (Rate of Completion)
- DwgNO (Drawing Number)
- PhaseCategory
- Other included columns

#### 5.5 Common Tasks

**Creating a Progress Book**
- Step-by-step process
- Naming the book

**Selecting Groupings**
- By Area
- By Module
- By Drawing
- Combining multiple groupings

**Customizing Columns**
- Which columns to include
- Column order

**Generating / Printing**
- Print preview
- Print settings
- Export to PDF

**Entering Progress from Completed Books**
- Reading field crew markups
- Entering data into Progress Module

#### 5.6 Future: Digital Progress Books (placeholder)
- Mobile app usage
- Real-time sync from field

---

### 6. Administration

#### 6.1 Overview
- Who needs this section (admins, lead FEs)
- Access requirements

#### 6.2 User Management
- Adding users
- Editing user permissions
- Deactivating users
- Password resets

#### 6.3 Project Setup
- Creating a new project
- Project settings
- Connecting to P6

#### 6.4 Database Operations
- Manual sync
- Database status
- Troubleshooting sync issues

#### 6.5 Logs
- Where to find logs
- What logs contain
- Sending logs for support

---

### 7. Reference

#### 7.1 Keyboard Shortcuts
- Table format: Shortcut | Action | Where it works

#### 7.2 Glossary
- Activity
- Area
- AssignTo
- Budget
- DwgNO
- FE (Field Engineer)
- Lookahead
- Module
- P6 / Primavera
- PhaseCategory
- Progress Book
- Quantity
- ROC
- Sync
- Work Package
- (Add more as needed)

#### 7.3 Troubleshooting / FAQ

**Login Issues**
- "Invalid credentials" error
- Account locked

**Sync Issues**
- Sync button grayed out
- Sync takes too long
- Sync conflict messages

**Import/Export Issues**
- Excel import fails
- Data missing after import

**Performance Issues**
- App running slow
- Grid not loading

**Error Messages**
- Common error messages and what they mean
- When to contact support

#### 7.4 Getting Help
- Using the Help sidebar
- Contacting support
- Reporting bugs

---

## Screenshot Requirements

Each section should include screenshots showing:
- Toolbar with buttons labeled
- Menu dropdowns expanded
- Dialog boxes for key operations
- Grid views with sample data (anonymized)
- Before/after for multi-step processes

**Screenshot naming convention:** `[section]-[description].png`

**Annotation style guide:**
- Red circles/rectangles for highlighting areas
- Numbered callouts (1, 2, 3) with corresponding legend
- Arrows pointing to specific buttons/fields
- Brief text labels where needed
- Blur or redact any sensitive/real project data

---

## Visual Assets Checklist

This is the running list of all screenshots and visuals needed. Updated as content is written.

### Section 1: Getting Started
| ID | Filename | Description | Annotations Needed | Status |
|----|----------|-------------|-------------------|--------|
| 1.1 | `start-login-screen.png` | Login dialog | Callouts: (1) Username field, (2) Password field, (3) Login button | [ ] |
| 1.2 | `start-main-interface.png` | MainWindow after login | Callouts: (1) Navigation menu, (2) Toolbar, (3) Help menu, (4) Settings menu, (5) Main content area | [ ] |
| 1.3 | `start-navigation-menu.png` | Left nav expanded | Callouts: (1) Progress, (2) Schedule, (3) Work Packages, (4) Progress Books, (5) Admin (if visible) | [ ] |
| 1.4 | `start-sync-status.png` | Status indicator showing online/offline | Arrow pointing to indicator, label showing both states | [ ] |

### Section 2: Progress Module
| ID | Filename | Description | Annotations Needed | Status |
|----|----------|-------------|-------------------|--------|
| 2.1 | `progress-full-view.png` | Progress module main view | Overview shot, no annotations | [ ] |
| 2.2 | `progress-toolbar.png` | Toolbar closeup | Numbered callouts for each button (list TBD after toolbar review) | [ ] |
| 2.3 | `progress-menu-file.png` | File menu expanded | Callouts for each menu item | [ ] |
| 2.4 | `progress-menu-edit.png` | Edit menu expanded | Callouts for each menu item | [ ] |
| 2.5 | `progress-menu-view.png` | View menu expanded | Callouts for each menu item | [ ] |
| 2.6 | `progress-import-dialog.png` | Excel import dialog | Callouts: file selection, options, import button | [ ] |
| 2.7 | `progress-grid-columns.png` | Grid header row | Callouts for key columns | [ ] |
| 2.8 | `progress-context-menu.png` | Right-click menu on grid | Callouts for each option | [ ] |
| 2.9 | `progress-filter-panel.png` | Filter/search controls | Callouts for filter fields and buttons | [ ] |
| 2.10 | `progress-assign-dialog.png` | AssignTo dialog/dropdown | Show selection process | [ ] |
| 2.11 | `progress-sync-button.png` | Sync in progress | Show sync indicator/progress | [ ] |

### Section 3: Schedule Module
| ID | Filename | Description | Annotations Needed | Status |
|----|----------|-------------|-------------------|--------|
| 3.1 | `schedule-full-view.png` | Schedule module main view | Overview shot, no annotations | [ ] |
| 3.2 | `schedule-toolbar.png` | Toolbar closeup | Numbered callouts for each button | [ ] |
| 3.3 | `schedule-menu-items.png` | Menu dropdowns | Callouts for each menu item | [ ] |
| 3.4 | `schedule-lookahead-view.png` | Three-week lookahead display | Callouts: week columns, status indicators | [ ] |
| 3.5 | `schedule-missed-reason.png` | Missed reason selection | Show dropdown/dialog | [ ] |
| 3.6 | `schedule-p6-sync.png` | P6 sync dialog/status | Callouts for sync options | [ ] |

### Section 4: Work Packages
| ID | Filename | Description | Annotations Needed | Status |
|----|----------|-------------|-------------------|--------|
| 4.1 | `wp-full-view.png` | Work Packages main view | Overview shot | [ ] |
| 4.2 | `wp-toolbar.png` | Toolbar closeup | Numbered callouts for each button | [ ] |
| 4.3 | `wp-menu-items.png` | Menu dropdowns | Callouts for each menu item | [ ] |
| 4.4 | `wp-create-new.png` | New work package dialog | Callouts for name, options | [ ] |
| 4.5 | `wp-activity-selection.png` | Activity picker | Show selection UI | [ ] |
| 4.6 | `wp-form-selection.png` | Form type selection | Callouts for available forms | [ ] |
| 4.7 | `wp-dwg-log.png` | DWG Log interface | Callouts for drawing fields | [ ] |
| 4.8 | `wp-generate-pdf.png` | Generate button and preview | Show output options | [ ] |
| 4.9 | `wp-template-selection.png` | Template picker (if applicable) | Callouts for template options | [ ] |

### Section 5: Progress Books
| ID | Filename | Description | Annotations Needed | Status |
|----|----------|-------------|-------------------|--------|
| 5.1 | `pb-full-view.png` | Progress Books main view | Overview shot | [ ] |
| 5.2 | `pb-toolbar.png` | Toolbar closeup | Numbered callouts for each button | [ ] |
| 5.3 | `pb-menu-items.png` | Menu dropdowns | Callouts for each menu item | [ ] |
| 5.4 | `pb-create-new.png` | New progress book dialog | Callouts for options | [ ] |
| 5.5 | `pb-grouping-selection.png` | Grouping picker (Area/Module/Drawing) | Show selection UI | [ ] |
| 5.6 | `pb-column-selection.png` | Column customization | Callouts for available columns | [ ] |
| 5.7 | `pb-print-preview.png` | Print preview | Show what output looks like | [ ] |
| 5.8 | `pb-sample-output.png` | Actual printed/PDF progress book | Callouts for column meanings | [ ] |

### Section 6: Administration
| ID | Filename | Description | Annotations Needed | Status |
|----|----------|-------------|-------------------|--------|
| 6.1 | `admin-user-management.png` | User list/management screen | Callouts for add/edit/delete | [ ] |
| 6.2 | `admin-add-user.png` | Add user dialog | Callouts for fields | [ ] |
| 6.3 | `admin-project-setup.png` | Project settings | Callouts for key settings | [ ] |
| 6.4 | `admin-sync-status.png` | Database sync status | Callouts for status indicators | [ ] |
| 6.5 | `admin-logs-location.png` | Where to find logs | File path or UI location | [ ] |

### Section 7: Reference
| ID | Filename | Description | Annotations Needed | Status |
|----|----------|-------------|-------------------|--------|
| 7.1 | `ref-error-example.png` | Sample error dialog | Example of error message | [ ] |

---

**Total Visual Assets: 45 (estimated, will update as content develops)**

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

---

## Notes

- Menu items and toolbar buttons need verification against actual UI
- Some features may be admin-only; note access requirements
- Procore integration section pending completion of that feature
- Digital progress books section is placeholder for future mobile functionality
