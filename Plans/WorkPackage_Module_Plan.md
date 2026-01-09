# Work Package Module - Architecture

## Overview
Module for generating construction work package PDFs. Replaces legacy MS Access VBA system. Work packages are multi-PDF documents containing standardized forms. Users can customize templates per project.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        WorkPackageView                               │
│  ┌──────────────────────────┬──────────────────────────────────┐   │
│  │  TabControl (60%)        │  Preview Panel (40%)              │   │
│  │  ├─ Generate Tab         │  ├─ Preview context label         │   │
│  │  ├─ WP Templates Tab     │  ├─ PdfViewerControl              │   │
│  │  └─ Form Templates Tab   │  └─ Refresh Preview button        │   │
│  └──────────────────────────┴──────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    ▼               ▼               ▼
            TemplateRepository  TokenResolver  WorkPackageGenerator
                    │                               │
                    ▼                               ▼
            SQLite Tables              PDF Renderers (Syncfusion)
         (FormTemplates,               ├─ BaseRenderer
          WPTemplates)                 ├─ CoverRenderer
                                       ├─ ListRenderer
                                       ├─ FormRenderer
                                       └─ GridRenderer
```

## Data Model

### FormTemplates Table (Local SQLite)
```sql
CREATE TABLE FormTemplates (
    TemplateID TEXT PRIMARY KEY,
    TemplateName TEXT NOT NULL,
    TemplateType TEXT NOT NULL,     -- Cover, List, Form, Grid
    StructureJson TEXT NOT NULL,
    IsBuiltIn INTEGER DEFAULT 0,
    CreatedBy TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL
);
```

### WPTemplates Table (Local SQLite)
```sql
CREATE TABLE WPTemplates (
    WPTemplateID TEXT PRIMARY KEY,
    WPTemplateName TEXT NOT NULL,
    FormsJson TEXT NOT NULL,        -- Ordered list of FormTemplateIDs
    DefaultSettings TEXT NOT NULL,   -- JSON with expirationDays
    IsBuiltIn INTEGER DEFAULT 0,
    CreatedBy TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL
);
```

## Template Types

| Type | Description | Examples |
|------|-------------|----------|
| **Cover** | Header + single large image + optional footer | Cover Sheet |
| **List** | Header + text items + optional footer | TOC |
| **Form** | Header + sections with items + columns + footer | Checklist, Signoff |
| **Grid** | Header + column headers + N empty rows + footer | Punchlist, DWG Log |

## JSON Structures

### Cover Type
```json
{
  "title": "WORK PACKAGE COVER SHEET",
  "imagePath": null,
  "imageWidthPercent": 80,
  "footerText": null
}
```

### List Type
```json
{
  "title": "WORK PACKAGE TABLE OF CONTENTS",
  "items": [
    "WP DOC EXPIRATION DATE: {ExpirationDate}",
    "PRINTED: {PrintedDate}",
    "1    WP Coversheet",
    "2    WP Checklist"
  ],
  "footerText": null
}
```

### Form Type
```json
{
  "title": "WORK PACKAGE CHECKLIST",
  "columns": [
    {"name": "ITEM", "widthPercent": 50},
    {"name": "DATE", "widthPercent": 12},
    {"name": "SIGN", "widthPercent": 15},
    {"name": "COMMENTS", "widthPercent": 23}
  ],
  "rowHeightIncreasePercent": 0,
  "sections": [
    {
      "name": "6 WEEK ASSEMBLY",
      "items": ["Documents Assembled", "Work Package Assembled"]
    }
  ],
  "footerText": null
}
```

### Grid Type
```json
{
  "title": "WORK PACKAGE PUNCHLIST",
  "columns": [
    {"name": "PL NO", "widthPercent": 6},
    {"name": "TAG/LINE/CABLE", "widthPercent": 12},
    {"name": "PL ITEM DESCRIPTION", "widthPercent": 30}
  ],
  "rowCount": 22,
  "rowHeightIncreasePercent": 0,
  "footerText": null
}
```

### WP Template Settings
```json
{
  "expirationDays": 14,
  "forms": [
    {"formTemplateId": "builtin-cover-sheet"},
    {"formTemplateId": "builtin-toc"},
    {"formTemplateId": "builtin-checklist"}
  ]
}
```

## Token System

| Token | Resolution |
|-------|------------|
| `{PrintedDate}` | DateTime.Now |
| `{ExpirationDate}` | DateTime.Now.AddDays(expirationDays) |
| `{WPName}` | User's WP Name pattern resolved |
| `{SchedActNO}` | DISTINCT SchedActNO from Activities for WP |
| `{PhaseCode}` | DISTINCT PhaseCode from Activities for WP |
| `{PKGManager}` | Selected from UI dropdown |
| `{Scheduler}` | Selected from UI dropdown |
| `{ProjectName}` | From Projects table |
| `{WorkPackage}` | Selected WP value |
| `{UDF2}`, `{Area}`, etc. | First distinct value from Activities |

## Built-in Templates

### Form Templates (6)
1. **Cover Sheet** (Cover) - Summit image
2. **TOC** (List) - Info block + contents
3. **Checklist** (Form) - 3 sections, DATE/SIGN/COMMENTS columns
4. **Punchlist** (Grid) - 10 columns, 22 rows
5. **Signoff Sheet** (Form) - 2 sections + footer
6. **DWG Log** (Grid) - Placeholder

### WP Templates (1)
1. **Summit Standard WP** - All 6 forms in order

## Output Structure
```
OutputFolder/
└── {ProjectID}/
    └── {WorkPackage}/
        ├── 1_Cover_Sheet.pdf
        ├── 2_TOC.pdf
        ├── ...
        └── {WorkPackage}-WorkPackage.pdf (merged)
```

## PDF Rendering

Using Syncfusion.Pdf library:
- `PdfDocument` for creating documents
- `PdfGrid` for tables
- `PdfLayoutResult` for page flow
- `PdfDocumentBase.Merge()` for combining PDFs

### Styling (Hardcoded)
- Title Bar: 14pt Bold, centered, 2pt borders
- Column Headers: 10pt Bold, light gray background (#E0E0E0)
- Section Headers: 10pt Bold, gray background (#D0D0D0), span all columns
- Data Rows: 10pt Regular, 0.5pt borders
- Footer: 9pt Regular, full width

## Key Design Decisions

1. **Four template types** - Cover, List, Form, Grid cover all patterns
2. **Token-based binding** - `{TokenName}` replaced at generation
3. **Local only** - Templates in SQLite, no Azure sync initially
4. **Clone to customize** - Built-in templates cannot be edited directly
5. **On-demand preview** - User clicks Refresh (not real-time)
6. **Column width percentages** - Must sum to 100%
7. **Metadata per generation** - Project, PKG Manager, etc. selected each time

## Deferred Items
- DWG Log integration (pending Procore research)
- External drawings fetch
- Azure sync for templates
- PDF archival to Azure Blob

## Completed Implementation

### Files Created
| File | Purpose |
|------|---------|
| Views/WorkPackageView.xaml | Main view with split layout |
| Views/WorkPackageView.xaml.cs | View code-behind |
| Models/FormTemplate.cs | Form template model + JSON structures |
| Models/WPTemplate.cs | WP template model |
| Data/TemplateRepository.cs | Template CRUD operations |
| Services/TokenResolver.cs | Token replacement service |
| Services/PdfRenderers/BaseRenderer.cs | Shared PDF rendering logic |
| Services/PdfRenderers/CoverRenderer.cs | Cover type renderer |
| Services/PdfRenderers/ListRenderer.cs | List type renderer |
| Services/PdfRenderers/FormRenderer.cs | Form type renderer |
| Services/PdfRenderers/GridRenderer.cs | Grid type renderer |
| Services/PdfRenderers/DrawingsRenderer.cs | Drawings type renderer |
| Services/PdfRenderers/WorkPackageGenerator.cs | PDF generation orchestrator |
| Views/TemplateTypeDialog.xaml(.cs) | Type selection dialog for new templates |

### January 7, 2026
- Logo path field shows "(default)" instead of hardcoded path
- Added persistence for Generate tab selections (Project, WP Template, PKG Manager, Scheduler)
- Added Phone and Fax fields to AdminProjectsDialog
- Redesigned PDF header layout (logo, WP name, project info, phone/fax, title bar, PKG/Scheduler)
- Renamed built-in form templates to include " - Template" suffix
- Added duplicate name validation when saving form templates
- Fixed Form Templates tab bugs (Type selection dialog, editor changes on clone)
- PDF Viewer: default zoom Fit Width, minimum zoom 10%
- Splitter position persists to UserSettings

### January 6, 2026
- Implemented form template editors for Cover, List, Grid, and Form types
- Added ColumnDisplayConverter for column list display formatting
- Added TemplateTypeDialog for creating new templates with type selection
- Fixed PDF page size in MergeDocuments (612x792 = 8.5x11 inches)
- Fixed preview token resolution - body tokens resolve from database
- Preview uses actual UI selections instead of placeholders
- Added Drawings form template type with DrawingsRenderer

### January 4, 2026
- Created WorkPackageView.xaml with 60/40 split layout, tabs, preview panel
- Wired up MainWindow navigation (BtnWorkPackage_Click)
- Applied SfSkinManager FluentDark theme to all controls
- Re-enabled PDF preview with Syncfusion PdfViewerControl
- Verified folder browser and Generate button save PDFs correctly
- Replaced legacy SHBrowseForFolder P/Invoke with modern IFileDialog COM interface
- Created database tables (FormTemplates, WPTemplates) and seeded built-in templates
