# Product Requirements Document: Progress Book Module & AI Progress Scan

## Document Info
- **Version:** 1.0
- **Last Updated:** January 2025
- **Status:** Ready for Implementation

---

## Table of Contents
1. [Overview](#overview)
2. [Progress Book Module](#progress-book-module)
3. [AI Progress Scan Feature](#ai-progress-scan-feature)
4. [Data Models](#data-models)
5. [File Structure](#file-structure)
6. [Implementation Phases](#implementation-phases)

---

## Overview

### Purpose
Enable field engineers to print customizable progress books for field workers, then scan completed books with handwritten progress entries using AI vision to automatically update records in MILESTONE.

### Goals
1. Replace manual data entry from paper progress sheets
2. Reduce transcription errors
3. Save field engineer time
4. Maintain audit trail of progress updates

### User Workflow
```
1. FE designs progress book layout (columns, grouping, sorting)
2. FE generates PDF progress book for specific records
3. Field workers mark progress on printed sheets (checkbox, qty, or %)
4. FE scans/photographs completed sheets
5. AI extracts handwritten values matched to UniqueIDs
6. FE reviews extracted data in approval screen
7. FE approves/edits/rejects entries
8. System batch updates approved records
```

---

## Progress Book Module

### Navigation
- New navigation button: "Progress Books" (button already exists)
- Opens ProgressBookView as primary view

### Layout Builder UI

#### Main Layout Builder Screen
```
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Progress Book Layout Builder                                    [Save] [Delete] │
├─────────────────────────────────────────────────────────────────────────────────┤
│ Layout Name: [________________________]   Paper Size: (•) Letter ( ) Tabloid   │
│                                                                                 │
│ Font Size: [====●=====] 10pt            (DESC will render at 9pt)              │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│ GROUPING & SORTING                                                              │
│ ┌─────────────────────────────────────────────────────────────────────────────┐ │
│ │ Main Group By: [▼ PhaseCode        ]    Sort By: [▼ Description    ]       │ │
│ │                                                                             │ │
│ │ Sub-Groups:                                                        [+ Add] │ │
│ │   1. [▼ Size              ]    Sort By: [▼ ROC             ]    [Remove]   │ │
│ │   2. [▼ ROC               ]    Sort By: [▼ UniqueID        ]    [Remove]   │ │
│ └─────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                 │
│ COLUMNS (Zone 2 - Flexible)                                                     │
│ ┌─────────────────────────────────────────────────────────────────────────────┐ │
│ │ Drag to reorder │ Column          │ Width (1-100) │ Actions                 │ │
│ │ ≡               │ ROC *           │ [  15  ]      │ (required)              │ │
│ │ ≡               │ DESC *          │ [  60  ]      │ (required)              │ │
│ │ ≡               │ CompType        │ [  10  ]      │ [Remove]                │ │
│ │ ≡               │ PhaseCategory   │ [  15  ]      │ [Remove]                │ │
│ │                                                                   [+ Add]   │ │
│ └─────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                 │
│ * Required columns cannot be removed                                            │
│                                                                                 │
├─────────────────────────────────────────────────────────────────────────────────┤
│ PREVIEW                                                          [↻ Refresh]   │
│ ┌─────────────────────────────────────────────────────────────────────────────┐ │
│ │                                                                             │ │
│ │  (PDF preview of first page with sample data)                              │ │
│ │                                                                             │ │
│ └─────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                 │
│                                              [Generate Progress Book]           │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Column Specifications

#### Zone 1 (Locked Left)
| Column | Width | Editable | Notes |
|--------|-------|----------|-------|
| UniqueID | Fixed 5% | No | Always first column |

#### Zone 2 (Flexible Middle)
| Column | Required | Default Width | Min Width | Notes |
|--------|----------|---------------|-----------|-------|
| ROC | Yes | 15 | 5 | Moveable, resizable |
| DESC | Yes | 60 | 20 | Moveable, resizable, font -1pt |
| *User columns* | No | 10 | 5 | Moveable, resizable, deletable |

**Width Calculation:**
- User enters values 1-100 for each Zone 2 column
- System calculates prorated percentages: `columnWidth = (userValue / sumOfAllValues) * availableZone2Width`
- Available Zone 2 width = 100% - Zone1Width - Zone3Width

#### Zone 3 (Locked Right)
| Column | Width | Format | Notes |
|--------|-------|--------|-------|
| REM QTY | Fixed 6% | Decimal | Remaining quantity |
| REM MH | Fixed 6% | Decimal | Remaining manhours |
| CUR QTY | Fixed 6% | Decimal | Current completed quantity |
| CUR % | Fixed 7% | "XX.XX%" | Current percent complete |
| DONE | Fixed 4% | ☐ checkbox | Empty checkbox for field use |
| QTY Entry | Fixed 8% | [      ] | Boxed area for handwriting |
| % Entry | Fixed 8% | [      ] | Boxed area for handwriting |

**Zone 3 Total: ~45% of page width**

### Font Size Slider

| Property | Value |
|----------|-------|
| Minimum | 8pt |
| Maximum | 14pt |
| Default | 10pt |
| Step | 1pt |
| DESC adjustment | Always (selected - 1)pt |

Display format: "Font Size: [====●=====] 10pt (DESC will render at 9pt)"

### Grouping Configuration

#### Main Group Field
- Dropdown populated from full Activities field list
- Common fields shown first with star icon (★):
  - ★ PhaseCode
  - ★ Area
  - ★ UDF2
  - ★ Tag
  - (separator line)
  - All other fields alphabetically
- Does NOT need to be included in Zone 2 columns
- Required - user must select a main group

#### Sub-Groups
- Dropdown limited to columns included in Zone 2
- User can add multiple sub-groups (no limit, but practical limit ~3-4)
- Each sub-group has its own "Sort By" dropdown
- Sort By dropdown limited to Zone 2 columns
- Sub-groups are optional

### PDF Generation

#### Header Section (Every Page)
```
┌─────────────────────────────────────────────────────────────────────────────────┐
│ [SUMMIT LOGO]  │  {ProjectID}  │  {ProjectDescription}  │  Progress Book - {Name}│
└─────────────────────────────────────────────────────────────────────────────────┘
```

- Summit logo: Load from embedded resource or configured path
- ProjectID: From current project context
- ProjectDescription: From current project context
- Name: Progress Book name entered by user when generating

#### Column Header Row
```
┌────────┬─────────────────────────────┬───────┬───────┬───────┬───────┬────┬───────┬───────┐
│UniqueID│ [Zone 2 columns as ordered] │REM QTY│REM MH │CUR QTY│CUR %  │DONE│QTY    │ %     │
└────────┴─────────────────────────────┴───────┴───────┴───────┴───────┴────┴───────┴───────┘
```

#### Group Headers with Summaries
```
┌─────────────────────────────────────────────────────────────────────────────────┐
│ ▼ {GroupFieldName}: {GroupValue}          Rem: {X} QTY, {Y} MH │ Tot: {A} QTY, {B} MH │
└─────────────────────────────────────────────────────────────────────────────────┘
```

- Indent sub-groups visually (add left padding per level)
- Calculate summaries by aggregating child records:
  - Remaining QTY: Sum of RemainingQty for all records in group
  - Remaining MH: Sum of RemainingMH for all records in group
  - Total QTY: Sum of TotalQty for all records in group
  - Total MH: Sum of TotalMH for all records in group

#### Data Rows
```
│ 1594   │ 1.REC │ 0.75IN SCH-S40 SM...     │  0.7  │  0.02 │  0.0  │ 0.00% │  ☐   │ [     ] │ [     ] │
```

- Alternating row shading (light gray / white)
- DESC column truncated with ellipsis if too long
- Entry boxes rendered as visible bordered rectangles

#### Page Break Rules
1. Check if next group header + at least 1 data row fits on current page
2. If NO: Force page break
3. On new page: Reprint all active group headers with "(continued)" suffix
4. Example: "▼ AREA: 51.CUB.100.1 (continued)"

#### Footer Section (Every Page)
```
                                    Page {N} of {Total}
```

- Centered at bottom
- Include generation timestamp in small print (optional)

### Paper Size Options

| Size | Dimensions | Orientation |
|------|------------|-------------|
| Letter | 8.5" x 11" | Landscape (11" x 8.5") |
| Tabloid | 11" x 17" | Landscape (17" x 11") |

### Saved Layouts

#### Storage
- SQLite table: `ProgressBookLayouts`
- JSON column for flexible column/grouping configuration

#### Layout Operations
- Save: Create new or update existing layout
- Load: Populate builder from saved layout
- Delete: Remove layout (with confirmation)
- Duplicate: Copy existing layout with new name

### Generate Progress Book Dialog

When user clicks "Generate Progress Book":

```
┌─────────────────────────────────────────────────────────────────┐
│ Generate Progress Book                                      [X] │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│ Progress Book Name: [CB102 Pipe Area A___________]             │
│                                                                 │
│ Include Records:                                                │
│   ( ) All records assigned to me                                │
│   (•) Current filter from Progress View                         │
│   ( ) Select specific Progress Book: [▼ Select...        ]     │
│                                                                 │
│ Record Count: 1,247 records                                     │
│ Estimated Pages: 44                                             │
│                                                                 │
│                              [Cancel]  [Generate PDF]           │
└─────────────────────────────────────────────────────────────────┘
```

---

## AI Progress Scan Feature

### Entry Point
- Button in Progress View toolbar: "Scan Progress" or icon with tooltip
- Opens file picker, then processing workflow

### Scanning Workflow

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Upload    │────▶│   Process   │────▶│   Review    │────▶│   Apply     │
│   Files     │     │   with AI   │     │   Results   │     │   Updates   │
└─────────────┘     └─────────────┘     └─────────────┘     └─────────────┘
```

### Step 1: Upload Files

**Supported Formats:**
- PDF (multi-page)
- PNG
- JPG / JPEG

**UI:**
```
┌─────────────────────────────────────────────────────────────────┐
│ Scan Progress Sheets                                        [X] │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                                                         │   │
│  │     Drag and drop files here                           │   │
│  │           or                                            │   │
│  │     [Browse Files]                                      │   │
│  │                                                         │   │
│  │     Supported: PDF, PNG, JPG                           │   │
│  │                                                         │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  Selected Files:                                                │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ ✓ progress_book_cb102.pdf (44 pages)          [Remove] │   │
│  │ ✓ extra_sheet.jpg                             [Remove] │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  Total Pages: 45                                                │
│  Estimated Cost: ~$0.45 - $1.35                                │
│                                                                 │
│                              [Cancel]  [Start Processing]       │
└─────────────────────────────────────────────────────────────────┘
```

### Step 2: AI Processing

**Processing Dialog:**
```
┌─────────────────────────────────────────────────────────────────┐
│ Processing Scanned Sheets                                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  [████████████░░░░░░░░░░░░░░░░░░] 35%                          │
│                                                                 │
│  Processing page 16 of 45...                                    │
│                                                                 │
│  Extracted: 127 entries so far                                  │
│                                                                 │
│                                            [Cancel]             │
└─────────────────────────────────────────────────────────────────┘
```

#### Claude Vision API Integration

**API Endpoint:** `https://api.anthropic.com/v1/messages`

**Model:** `claude-sonnet-4-20250514` (or latest vision-capable model)

**Request Structure:**
```json
{
  "model": "claude-sonnet-4-20250514",
  "max_tokens": 4096,
  "messages": [
    {
      "role": "user",
      "content": [
        {
          "type": "image",
          "source": {
            "type": "base64",
            "media_type": "image/png",
            "data": "{base64_encoded_image}"
          }
        },
        {
          "type": "text",
          "text": "{extraction_prompt}"
        }
      ]
    }
  ]
}
```

**Extraction Prompt:**
```
Analyze this construction progress sheet image. Extract all rows that contain handwritten entries in the DONE checkbox, QTY box, or % box.

Document Structure:
- UniqueID column is on the far left (numeric, 1-5 digits)
- DONE column has an empty checkbox (☐) - look for checkmarks, X marks, or filled boxes
- QTY column has a boxed area [...] for handwritten quantity values
- % column has a boxed area [...] for handwritten percentage values
- Only data rows have UniqueIDs - ignore header rows and group summary rows

For each row with ANY handwritten entry, return:
- uniqueId: The exact numeric UniqueID from the leftmost column (CRITICAL - must be precise)
- done: true if checkbox is marked (checkmark, X, filled), false if empty, null if unclear
- qty: The handwritten quantity value as a number (null if empty or illegible)
- pct: The handwritten percentage value as a number WITHOUT % symbol (null if empty or illegible)
- confidence: Your confidence in this extraction (0-100)
- raw: Exactly what you see written (for verification)

Return ONLY a JSON array, no other text:
[
  {"uniqueId": 1594, "done": true, "qty": null, "pct": null, "confidence": 98, "raw": "checkmark"},
  {"uniqueId": 1556, "done": false, "qty": 2.5, "pct": null, "confidence": 85, "raw": "2.5"},
  {"uniqueId": 1621, "done": false, "qty": null, "pct": 50, "confidence": 92, "raw": "50"}
]

Rules:
- ONLY include rows where you see handwriting or marks in DONE, QTY, or % areas
- Skip rows with no handwritten entries
- If you see a number near the % box, treat it as percentage
- If you see a number near the QTY box, treat it as quantity
- If "50%" is written, extract pct as 50 (not 50%)
- UniqueID must be extracted EXACTLY - this is the database key
- If UniqueID is unclear, set confidence below 50
- For ambiguous entries, lower confidence and describe in raw field
```

**Batch Processing:**
- Process 1 page per API call (most reliable, simplest error handling)
- For 45 pages: 45 API calls (~$0.90-1.35 total cost)
- Parse JSON response from each call
- Aggregate all extracted entries
- Show progress: "Processing page X of Y"

**Error Handling:**
- API timeout: Retry up to 3 times with exponential backoff
- Invalid JSON response: Log raw response, skip page, continue
- Rate limit: Queue and retry after delay
- Network failure: Show error, allow retry

### Step 3: Matching & Validation

**Matching Logic:**
```csharp
foreach extracted entry:
    1. Query local Activities table by UniqueID (current user's records only)
    2. If found:
        - Create ScanReviewItem with matched record
        - Run validation rules
    3. If not found:
        - Create ScanReviewItem with NotFound status
```

**Validation Rules:**

| Rule | Condition | Severity | Message |
|------|-----------|----------|---------|
| Not Found | UniqueID not in user's records | Error | "Record not found in your assignments" |
| Low Confidence | confidence < 70 | Warning | "Low confidence extraction - verify values" |
| Progress Decrease | extracted % < current % | Warning | "New % is less than current %" |
| QTY Exceeds Total | extracted qty > total qty | Warning | "Entered QTY exceeds total quantity" |
| Invalid Percent | extracted % > 100 | Error | "Percentage cannot exceed 100%" |
| Both QTY and PCT | both values provided | Info | "Both QTY and % provided - system will use %" |

### Step 4: Review Screen

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│ Review Scanned Progress                                                         [X] │
├─────────────────────────────────────────────────────────────────────────────────────┤
│ Summary: 127 extracted │ 119 matched │ 8 not found │ 23 warnings                    │
│                                                                                     │
│ Filter: [All ▼]  [✓ High Confidence]  [⚠ Warnings]  [✗ Not Found]                  │
│                                                                                     │
│ ┌─────────────────────────────────────────────────────────────────────────────────┐ │
│ │☑│UID  │ Description              │Cur QTY│Cur % │New QTY│New % │Conf│ Status   │ │
│ ├─┼─────┼──────────────────────────┼───────┼──────┼───────┼──────┼────┼──────────┤ │
│ │☑│ 1594│ 0.75IN SCH-S40 SM SCH... │  0.0  │ 0.00%│       │  50  │ 95 │ ✓ Ready  │ │
│ │☑│ 1556│ 0.75IN SCH-S40 SM SCH... │  0.0  │ 0.00%│  2.5  │      │ 85 │ ✓ Ready  │ │
│ │☐│ 1621│ 0.75IN SCH-S80 SM SCH... │  0.5  │25.00%│       │  20  │ 88 │ ⚠ % decreased │
│ │☐│ 9999│ (not found)              │   -   │   -  │       │  75  │ 72 │ ✗ Not found │
│ │☑│ 1507│ 0.75IN SCH-S80 SM SCH... │  0.0  │ 0.00%│       │      │ 98 │ ✓ DONE   │ │
│ └─────────────────────────────────────────────────────────────────────────────────┘ │
│                                                                                     │
│ Confidence Legend: 🟢 90-100  🟡 70-89  🔴 Below 70                                 │
│                                                                                     │
│ Selected: 96 of 127                                                                 │
│                                                                                     │
│ [Select All Matched]  [Clear Selection]          [Cancel]  [Apply Selected (96)]   │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

**Grid Columns:**

| Column | Type | Editable | Notes |
|--------|------|----------|-------|
| Checkbox | CheckBox | Yes | Include in batch update |
| UniqueID | Text | No | From extraction |
| Description | Text | No | From matched DB record (truncated) |
| Current QTY | Number | No | From DB record |
| Current % | Percent | No | From DB record, format "XX.XX%" |
| New QTY | Number | Yes | From extraction, user can edit |
| New % | Number | Yes | From extraction, user can edit |
| Confidence | Number | No | Color-coded: Green/Yellow/Red |
| Status | Text | No | Ready / Warning message / Error |

**Grid Behaviors:**
- Sort by clicking column headers
- Default sort: Status (errors first), then UniqueID
- Double-click row to see full details including raw extraction text
- Rows with errors (Not Found, Invalid) are unchecked by default
- Rows with warnings are unchecked by default
- Rows with confidence >= 90 and no warnings are checked by default
- User can manually check/uncheck any row

**Bulk Actions:**
- Select All Matched: Check all rows with status "Ready" or warnings (exclude Not Found)
- Clear Selection: Uncheck all rows
- Apply Selected: Process checked rows

**Editing:**
- User can edit New QTY and New % columns directly in grid
- Editing clears any validation warnings for that row
- If user enters both QTY and %, show info tooltip "% will be used"

### Step 5: Apply Updates

**Processing Logic:**
```
For each selected row:
    1. Determine update type:
        - If DONE checked: Set percent to 100%
        - Else if New % provided: Use New %
        - Else if New QTY provided: Calculate % from QTY
    
    2. Call ProgressRepository.UpdateProgress(uniqueId, newPercent)
    
    3. Track success/failure count
```

**Confirmation Dialog:**
```
┌─────────────────────────────────────────────────────────────────┐
│ Confirm Progress Update                                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│ You are about to update 96 records.                            │
│                                                                 │
│ This action will:                                               │
│ • Update progress percentages for selected records             │
│ • Mark records as modified for next sync                       │
│                                                                 │
│ This cannot be undone.                                          │
│                                                                 │
│                              [Cancel]  [Apply Updates]          │
└─────────────────────────────────────────────────────────────────┘
```

**Post-Update:**
- Show success summary: "Updated 96 records successfully"
- If any failures: List failed UniqueIDs with reasons
- Log operation: `AppLogger.Info($"Progress scan applied: {count} records updated from {pageCount} scanned pages", "ProgressScanService.ApplyUpdates", username)`
- Refresh Progress View grid
- Close scan dialog

---

## Data Models

### ProgressBookLayout (Database Entity)

```csharp
public class ProgressBookLayout
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string ProjectId { get; set; } = null!;
    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string ConfigurationJson { get; set; } = null!;  // Serialized ProgressBookConfiguration
}
```

### ProgressBookConfiguration (Serialized to JSON)

```csharp
public class ProgressBookConfiguration
{
    public PaperSize PaperSize { get; set; } = PaperSize.Letter;
    public int FontSize { get; set; } = 10;  // 8-14
    public string MainGroupField { get; set; } = null!;
    public string MainGroupSortField { get; set; } = null!;
    public List<SubGroupConfig> SubGroups { get; set; } = new();
    public List<ColumnConfig> Columns { get; set; } = new();  // Zone 2 columns only
}

public class SubGroupConfig
{
    public string GroupField { get; set; } = null!;
    public string SortField { get; set; } = null!;
}

public class ColumnConfig
{
    public string FieldName { get; set; } = null!;
    public int Width { get; set; }  // 1-100
    public int DisplayOrder { get; set; }
}

public enum PaperSize
{
    Letter,
    Tabloid
}
```

### ScanExtractionResult (From Claude API)

```csharp
public class ScanExtractionResult
{
    public int UniqueId { get; set; }
    public bool? Done { get; set; }
    public decimal? Qty { get; set; }
    public decimal? Pct { get; set; }
    public int Confidence { get; set; }
    public string? Raw { get; set; }
}
```

### ScanReviewItem (For Review Grid)

```csharp
public class ScanReviewItem : INotifyPropertyChanged
{
    // From extraction
    public int ExtractedUniqueId { get; set; }
    public bool? ExtractedDone { get; set; }
    public decimal? ExtractedQty { get; set; }
    public decimal? ExtractedPct { get; set; }
    public int Confidence { get; set; }
    public string? RawExtraction { get; set; }
    
    // From database match
    public Activity? MatchedRecord { get; set; }
    public decimal? CurrentQty { get; set; }
    public decimal? CurrentPercent { get; set; }
    public string? Description { get; set; }
    
    // User editable
    public decimal? NewQty { get; set; }
    public decimal? NewPercent { get; set; }
    public bool IsSelected { get; set; }
    
    // Validation
    public ScanMatchStatus Status { get; set; }
    public string? ValidationMessage { get; set; }
    
    public event PropertyChangedEventHandler? PropertyChanged;
}

public enum ScanMatchStatus
{
    Ready,
    Warning,
    NotFound,
    Error
}
```

---

## File Structure

```
MILESTONE/
├── Models/
│   ├── ProgressBook/
│   │   ├── ProgressBookLayout.cs
│   │   ├── ProgressBookConfiguration.cs
│   │   ├── ColumnConfig.cs
│   │   ├── SubGroupConfig.cs
│   │   └── PaperSize.cs
│   └── AI/
│       ├── ScanExtractionResult.cs
│       └── ScanReviewItem.cs
│
├── Services/
│   ├── ProgressBook/
│   │   ├── ProgressBookService.cs          // Orchestrates layout and generation
│   │   ├── ProgressBookLayoutRepository.cs  // CRUD for layouts
│   │   └── ProgressBookPdfGenerator.cs      // PDF generation
│   └── AI/
│       ├── ClaudeApiConfig.cs               // API key, endpoint configuration
│       ├── ClaudeVisionService.cs           // API communication
│       ├── ProgressScanService.cs           // Orchestrates scan workflow
│       ├── ScanMatchingService.cs           // Matches extractions to DB
│       ├── ScanValidationService.cs         // Validation rules
│       └── PdfToImageConverter.cs           // PDF page extraction
│
├── ViewModels/
│   ├── ProgressBookLayoutViewModel.cs       // Layout builder
│   ├── GenerateProgressBookViewModel.cs     // Generation dialog
│   ├── ProgressScanViewModel.cs             // Upload dialog
│   └── ProgressScanReviewViewModel.cs       // Review dialog
│
└── Views/
    ├── ProgressBookLayoutView.xaml          // Layout builder
    ├── GenerateProgressBookDialog.xaml      // Generation dialog
    ├── ProgressScanDialog.xaml              // Upload dialog
    └── ProgressScanReviewDialog.xaml        // Review dialog
```

---

## Implementation Phases

### Phase 1: Data Models & Repository
**Files:** Models/ProgressBook/*, Services/ProgressBook/ProgressBookLayoutRepository.cs
**Tasks:**
1. Create all ProgressBook model classes
2. Create database table for ProgressBookLayouts
3. Implement CRUD operations in repository
4. Unit test repository operations

### Phase 2: Layout Builder UI
**Files:** ViewModels/ProgressBookLayoutViewModel.cs, Views/ProgressBookLayoutView.xaml
**Tasks:**
1. Create layout builder view with all controls
2. Implement column add/remove/reorder
3. Implement grouping configuration
4. Implement font size slider
5. Implement save/load/delete layout operations
6. Wire up navigation button

### Phase 3: PDF Generation
**Files:** Services/ProgressBook/ProgressBookPdfGenerator.cs, ProgressBookService.cs
**Tasks:**
1. Implement PDF generation with QuestPDF or similar library
2. Handle header with logo
3. Handle column layout with zone calculations
4. Handle grouping with headers and summaries
5. Handle page breaks with continued headers
6. Handle page numbers
7. Implement preview functionality
8. Create generation dialog

### Phase 4: AI Infrastructure
**Files:** Models/AI/*, Services/AI/ClaudeApiConfig.cs, ClaudeVisionService.cs
**Tasks:**
1. Create AI model classes
2. Implement Claude API configuration (secure key storage)
3. Implement ClaudeVisionService with image upload
4. Implement JSON response parsing
5. Implement error handling and retry logic
6. Test with sample images

### Phase 5: Scan Upload & Processing
**Files:** Services/AI/ProgressScanService.cs, PdfToImageConverter.cs, ViewModels/ProgressScanViewModel.cs, Views/ProgressScanDialog.xaml
**Tasks:**
1. Create upload dialog with drag-drop
2. Implement PDF to image conversion
3. Implement batch processing with progress
4. Wire up to ClaudeVisionService
5. Aggregate results from multiple pages

### Phase 6: Matching & Validation
**Files:** Services/AI/ScanMatchingService.cs, ScanValidationService.cs
**Tasks:**
1. Implement UniqueID matching against local database
2. Implement all validation rules
3. Create ScanReviewItems with proper status

### Phase 7: Review UI & Apply
**Files:** ViewModels/ProgressScanReviewViewModel.cs, Views/ProgressScanReviewDialog.xaml
**Tasks:**
1. Create review dialog with SfDataGrid
2. Implement filtering (All/Warnings/Not Found)
3. Implement confidence color coding
4. Implement editable New QTY/New % columns
5. Implement bulk selection actions
6. Implement Apply with confirmation
7. Implement batch update to database
8. Add logging for audit trail

---

## Technical Notes

### Implementation Decisions (January 2025)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| PDF Library | Syncfusion.Pdf.WPF | Consistent with existing WorkPackage module |
| Layout Storage | Local SQLite only | No Azure sync needed for layouts |
| Zone Widths | 5% / 45% / 50% | Accepted as specified |
| Record Ownership | Skip unassigned | Only process records assigned to current user |
| PDF to Image | PdfiumViewer.Native.x86_64.v8-xfa | Proven library for Windows |
| API Batch Size | 1 page per call | Most reliable, simplest error handling |
| Usage Limits | GlobalSettings Azure table | App-wide configurable limits |
| DONE Checkbox | Always 100% | Marking DONE sets progress to 100% |

### PDF Library
Use **Syncfusion.Pdf.WPF** (already in project):
- Consistent with existing WorkPackage PDF generation
- Extend BaseRenderer pattern from `Services/PdfRenderers/`
- Already licensed and tested

### PDF to Image Conversion
Use **PdfiumViewer.Native.x86_64.v8-xfa**:
- Extract pages as images at 200 DPI
- Windows native binaries
- Well-tested library

### Claude API Key Storage
- Store in `Credentials.cs` alongside other API credentials:
```csharp
public static string ClaudeApiKey => "sk-ant-xxxxxxxxxxxxx";
public static string ClaudeApiEndpoint => "https://api.anthropic.com/v1/messages";
public static string ClaudeModel => "claude-sonnet-4-20250514";
```
- `ClaudeApiConfig.cs` reads from `Credentials.cs` at runtime

### Cost Monitoring & Limits
- Create `GlobalSettings` Azure table for app-wide limits:
  - `ClaudeApi_DailyLimit` (pages per day)
  - `ClaudeApi_MonthlyLimit` (pages per month)
- Track usage per user in local UserSettings
- Log all API calls for billing reconciliation
- Estimated cost: ~$0.02-0.03 per page

### Logging Requirements
All operations must use AppLogger:
```csharp
// Scan started
AppLogger.Info($"Progress scan started: {pageCount} pages", "ProgressScanService.StartScan", username);

// Scan completed
AppLogger.Info($"Progress scan extracted: {entryCount} entries from {pageCount} pages", "ProgressScanService.CompleteScan", username);

// Updates applied
AppLogger.Info($"Progress scan applied: {updatedCount} records updated", "ProgressScanService.ApplyUpdates", username);

// Errors
AppLogger.Error(ex, "ClaudeVisionService.ExtractFromImage");
```

---

## Appendix: Field List for Main Group

Common fields (show at top with ★):
- PhaseCode
- Area
- UDF2
- Tag
- Commodity
- System

Full field list (alphabetical after common):
- ActivityID
- Area
- Catg_ComponentType
- Commodity
- Comp
- Description
- DrawingNumber
- P6ActivityID
- PhaseCategory
- PhaseCode
- ROC
- Size
- System
- Tag
- TestPackage
- UDF1
- UDF2
- UDF3
- UDF4
- UDF5
- WBS

(Exact list should match available columns in Activities table)
