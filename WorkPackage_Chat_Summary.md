# Chat Summary: Work Package Module - UI Planning & Phase 1

**Date:** January 4, 2026

---

## What Was Accomplished

### UI Design Finalized

Designed complete UI layout for the Work Package module:

**Main Layout:**
- Split view with 60/40 default ratio (draggable GridSplitter, saves to UserSettings)
- Left panel: Tabbed interface (Generate, WP Templates, Form Templates)
- Right panel: PDF Preview using SfPdfViewer
- On-demand preview (user clicks Refresh Preview button)

**Three Tabs:**
1. **Generate Tab** (90% daily use) - Project/WP selection, PKG Manager, Scheduler, WP Name Pattern, Logo, Output folder, Generate button
2. **WP Templates Tab** - Dropdown with "+ Add New" first option, Name field, Clone/Delete buttons, Expiration Days, Forms list with reorder/remove controls
3. **Form Templates Tab** - Dropdown with "+ Add New" first option, Name field, Type display (read-only), Clone/Delete buttons, type-specific editor

**Type Selection Dialog:**
When creating new Form Template, dialog appears: "Select template type: [Cover] [List] [Form] [Grid]"

### Four Template Types Defined

| Type | Purpose | Editor Controls |
|------|---------|-----------------|
| **Cover** | Header + single image + footer | Title, Image path/browse, Image width %, Footer |
| **List** | Header + text items + footer | Title, Items list (drag to reorder), Footer |
| **Form** | Header + sections/items + columns + footer | Title, Columns with width %, Row height increase %, Sections with items, Footer |
| **Grid** | Header + column headers + N rows + footer | Title, Row count, Row height increase %, Columns with width %, Footer |

### JSON Structure Refinements

**Column Width Percentages:**
- User-configurable per column
- Must sum to 100% (validation/auto-normalize)
- First column in Form type is "ITEM" (populated with section item text)

**Row Height:**
- Default height ~0.3"
- `rowHeightIncreasePercent` field allows user to increase all rows uniformly

**Punchlist:**
- Changed from 44 rows to 22 rows
- If more needed, add another instance of the form

**Cover Sheet:**
- New template type (Cover)
- Default image: `images/CoverPic.png` (embedded as app resource)
- `imageWidthPercent` controls image size (default 80%)

### PDF Rendering Styles Documented

All hardcoded (not user-configurable except where noted):
- Title Bar: 14pt Bold, centered, 2pt borders top and bottom
- Column Headers: 10pt Bold, light gray background (#E0E0E0), 1pt borders all sides
- Section Headers: 10pt Bold, light gray background (#D0D0D0), borders above/below only
- Data Rows: 10pt Regular, 0.5pt borders, default ~0.3" height
- Footer: 9pt Regular

### Phase 1: Database Implementation

**Azure SQL Server:**
```sql
ALTER TABLE Projects ADD Phone NVARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE Projects ADD Fax NVARCHAR(50) NOT NULL DEFAULT '';
```

**Local SQLite (DatabaseSetup.cs):**

Added to Projects CREATE TABLE:
```sql
Phone TEXT NOT NULL DEFAULT '',
Fax TEXT NOT NULL DEFAULT '',
```

Added new tables:
```sql
CREATE TABLE IF NOT EXISTS FormTemplates (
    TemplateID TEXT PRIMARY KEY,
    TemplateName TEXT NOT NULL,
    TemplateType TEXT NOT NULL,
    StructureJson TEXT NOT NULL,
    IsBuiltIn INTEGER NOT NULL DEFAULT 0,
    CreatedBy TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS WPTemplates (
    WPTemplateID TEXT PRIMARY KEY,
    WPTemplateName TEXT NOT NULL,
    FormsJson TEXT NOT NULL,
    DefaultSettings TEXT NOT NULL,
    IsBuiltIn INTEGER NOT NULL DEFAULT 0,
    CreatedBy TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL
);
```

Added indexes:
```sql
CREATE INDEX IF NOT EXISTS idx_formtemplate_name ON FormTemplates(TemplateName);
CREATE INDEX IF NOT EXISTS idx_formtemplate_type ON FormTemplates(TemplateType);
CREATE INDEX IF NOT EXISTS idx_wptemplate_name ON WPTemplates(WPTemplateName);
```

---

## Key Decisions Made

1. **Four template types** - Cover, List, Form, Grid
2. **60/40 split layout** - Editor tabs left, preview right, draggable splitter
3. **On-demand preview** - Refresh Preview button (not real-time)
4. **Type selection dialog** - When creating new Form Template
5. **Metadata per generation** - Project, PKG Manager, Scheduler selected each time (not stored in templates)
6. **Column width percentages** - User-configurable, must sum to 100%
7. **22 rows for Punchlist** - Add another form instance if more needed
8. **Default cover image** - images/CoverPic.png

---

## Next Steps (Phase 2+)

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Database tables | ✅ COMPLETE |
| 2 | Models - FormTemplate, WPTemplate classes + JSON deserialization | NEXT |
| 3 | Seed Built-Ins - Insert 5 default form templates + 1 WP template | |
| 4 | Token Resolver - Service to replace {tokens} with values | |
| 5 | PDF Renderers - CoverRenderer, ListRenderer, FormRenderer, GridRenderer | |
| 6 | WorkPackageView Shell - Split layout, tabs, preview panel | |
| 7 | Generate Tab UI | |
| 8 | WP Templates Tab UI | |
| 9 | Form Templates Tab UI | |
| 10 | Preview Integration - SfPdfViewer | |
| 11 | Import/Export - JSON file handling | |
| 12 | DWG Log / Drawings - Deferred (Procore research) | |

---

## Files to Update in Project Knowledge

- `WorkPackage_Module_Plan.md` - Updated with all UI layouts, four template types, JSON structures with column widths
- `WorkPackage_Chat_Summary.md` - This file (replaces previous summary)
