# Product Requirements Document: AI Progress Scan (AWS Textract)

## Document Info
- **Version:** 2.1
- **Last Updated:** January 2025
- **Status:** Ready for Implementation
- **Previous Version:** PRD_ProgressBook_AIScan.md (Claude Vision API)
- **Change Note:** v2.1 - Switched from UniqueID to ActivityID for OCR matching (integer is more reliable than 19-char alphanumeric)

---

## Table of Contents
1. [Overview](#overview)
2. [Why Switch from Claude Vision to AWS Textract](#why-switch-from-claude-vision-to-aws-textract)
3. [System Architecture](#system-architecture)
4. [Data Models](#data-models)
5. [Service Layer](#service-layer)
6. [User Interface](#user-interface)
7. [Database Integration](#database-integration)
8. [Implementation Phases](#implementation-phases)
9. [Technical Specifications](#technical-specifications)
10. [Testing Strategy](#testing-strategy)
11. [Cost Analysis](#cost-analysis)
12. [Risks and Mitigations](#risks-and-mitigations)

---

## Overview

### Purpose
Extract handwritten progress entries from scanned/photographed Progress Book sheets and automatically update MILESTONE activity records. This feature completes the Progress Book workflow: generate → print → mark in field → scan → apply updates.

### Context Within MILESTONE
- **Progress Module:** Complete and tested - handles activity tracking, assignments, filtering, sync
- **Progress Book Generation:** Complete - generates PDF progress books with UniqueID, configurable columns, entry boxes (DONE checkbox, QTY box, % box)
- **AI Progress Scan:** This feature - extracts handwritten values from completed sheets

### User Workflow
```
1. Field Engineer generates Progress Book PDF (existing feature)
2. Progress Book is printed and distributed to field workers
3. Field workers mark DONE checkboxes, enter QTY or % values by hand
4. FE scans/photographs completed sheets (phone camera, scanner)
5. FE uploads images to MILESTONE → AI Progress Scan
6. AWS Textract extracts table structure and handwritten values
7. System matches extracted rows to Activities by ActivityID (integer)
8. FE reviews extractions in approval dialog (edit/approve/reject)
9. Approved entries update local database (PercentEntry, EarnQtyEntry)
10. Changes sync to Central via existing sync mechanism
```

### Key Data Flow
```
Scanned Image → AWS Textract → Extraction Results → ActivityID Matching → Review UI → Database Update
```

---

## Why Switch from Claude Vision to AWS Textract

### Problems with Claude Vision Implementation
1. **Inconsistent accuracy** - Same images produce different results between API calls
2. **Poor UniqueID extraction** - 19-character alphanumeric IDs frequently corrupted (solved by switching to ActivityID)
3. **Table structure confusion** - Difficulty maintaining row alignment across columns
4. **No spatial coordinates** - Cannot validate which column a value belongs to
5. **Prompt sensitivity** - Minor prompt changes cause major accuracy swings

### Why ActivityID Instead of UniqueID
- **ActivityID:** Integer (e.g., 11967) - 5-6 digits, numbers only
- **UniqueID:** Alphanumeric (e.g., i251009101621125ano) - 19 characters, mixed letters/numbers
- OCR accuracy for pure numbers is significantly higher than alphanumeric strings
- Shorter values = fewer characters to get wrong
- Progress Book PDF generator updated to show ActivityID in first column

### AWS Textract Advantages
1. **Purpose-built for tables** - TABLES feature type specifically designed for grid extraction
2. **Bounding box geometry** - Each cell returns precise x,y coordinates for validation
3. **Handwriting detection** - FORMS feature type trained on handwritten content
4. **Confidence scores per cell** - Individual confidence for each extracted value
5. **Consistent results** - Deterministic extraction, same image = same output
6. **Multi-page documents** - Native support for multi-page PDFs
7. **Proven accuracy** - Used by major enterprises for document processing

### Trade-offs
| Factor | Claude Vision | AWS Textract |
|--------|---------------|--------------|
| Cost | ~$0.003/page (1024x1024) | ~$0.015/page (tables+forms) |
| Accuracy (tables) | Variable, 60-85% | Consistent, 85-95% |
| Handwriting | Fair | Good |
| Integration complexity | Simple HTTP | AWS SDK setup |
| Spatial data | None | Full bounding boxes |

**Decision:** Higher accuracy justifies 5x cost increase. A failed extraction costs more in user time than the API fee.

---

## System Architecture

### Component Diagram
```
┌─────────────────────────────────────────────────────────────────────┐
│                        MILESTONE WPF Application                     │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌─────────────┐    ┌──────────────────┐    ┌───────────────────┐  │
│  │ ProgressView │───►│ ProgressScanDialog│───►│ ScanReviewDialog  │  │
│  │  (button)    │    │ (upload images)   │    │ (approve/reject)  │  │
│  └─────────────┘    └────────┬───────────┘    └─────────┬─────────┘  │
│                              │                          │            │
│                              ▼                          ▼            │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │                     Services Layer                             │  │
│  │  ┌──────────────────┐  ┌─────────────────┐  ┌──────────────┐  │  │
│  │  │ProgressScanService│  │ TextractService │  │ MatchService │  │  │
│  │  │  (orchestrator)   │  │ (AWS API calls) │  │ (DB lookup)  │  │  │
│  │  └──────────────────┘  └─────────────────┘  └──────────────┘  │  │
│  └───────────────────────────────────────────────────────────────┘  │
│                              │                          │            │
│                              ▼                          ▼            │
│              ┌───────────────────┐         ┌─────────────────────┐  │
│              │    AWS Textract    │         │   Local SQLite DB   │  │
│              │  (cloud service)   │         │   (Activities)      │  │
│              └───────────────────┘         └─────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

### File Structure
```
MILESTONE/
├── Models/
│   └── AI/
│       ├── TextractExtractionResult.cs    // Raw Textract response mapping
│       ├── ProgressExtraction.cs          // Parsed extraction per row
│       └── ScanReviewItem.cs              // UI binding model (existing)
│
├── Services/
│   └── AI/
│       ├── TextractService.cs             // AWS Textract API wrapper
│       ├── TextractParser.cs              // Parse Textract blocks → rows
│       ├── ProgressScanService.cs         // Orchestration (modify existing)
│       ├── ScanMatchingService.cs         // UniqueID lookup (existing)
│       ├── ScanValidationService.cs       // Validation rules (existing)
│       └── PdfToImageConverter.cs         // PDF page extraction (existing)
│
├── ViewModels/
│   ├── ProgressScanViewModel.cs           // Upload dialog (modify existing)
│   └── ProgressScanReviewViewModel.cs     // Review dialog (existing)
│
└── Views/
    ├── ProgressScanDialog.xaml            // Upload UI (modify existing)
    └── ProgressScanReviewDialog.xaml      // Review UI (existing)
```

---

## Data Models

### TextractExtractionResult.cs
Maps AWS Textract response structure for table extraction.

```csharp
// Raw cell data from Textract TABLE analysis
public class TextractCell
{
    public int RowIndex { get; set; }
    public int ColumnIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public BoundingBox? Geometry { get; set; }
    public bool IsHeader { get; set; }
}

// Bounding box for spatial validation
public class BoundingBox
{
    public float Left { get; set; }
    public float Top { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}

// Complete extraction result for one page
public class TextractPageResult
{
    public int PageNumber { get; set; }
    public List<TextractCell> Cells { get; set; } = new();
    public int TableRowCount { get; set; }
    public int TableColumnCount { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### ProgressExtraction.cs
Parsed result aligned to Progress Book column structure.

```csharp
// Single row extraction mapped to Progress Book layout
public class ProgressExtraction
{
    // From ActivityID column (Zone 2, first column) - INTEGER
    public int? ActivityId { get; set; }
    public float ActivityIdConfidence { get; set; }
    
    // From entry columns (Zone 3)
    public bool? Done { get; set; }           // DONE checkbox
    public float DoneConfidence { get; set; }
    
    public decimal? Qty { get; set; }         // QTY entry box
    public float QtyConfidence { get; set; }
    
    public decimal? Pct { get; set; }         // % entry box
    public float PctConfidence { get; set; }
    
    // Metadata
    public int PageNumber { get; set; }
    public int RowNumber { get; set; }
    public string RawActivityIdText { get; set; } = string.Empty;  // For debugging
    
    // Computed
    public float OverallConfidence => 
        (ActivityIdConfidence + Math.Max(Math.Max(DoneConfidence, QtyConfidence), PctConfidence)) / 2;
    
    public bool HasAnyEntry => Done.HasValue || Qty.HasValue || Pct.HasValue;
    
    public bool HasValidActivityId => ActivityId.HasValue && ActivityId.Value > 0;
}
```

### ScanReviewItem.cs (Existing - Minimal Changes)
Keep existing structure, just update source property names if needed.

```csharp
// UI model for review grid - binds extraction to database record
public class ScanReviewItem : INotifyPropertyChanged
{
    // From extraction
    public ProgressExtraction RawExtraction { get; set; } = null!;
    
    // From database match
    public Activity? MatchedActivity { get; set; }
    public decimal? CurrentQty { get; set; }      // Activity.EarnQtyEntry
    public decimal? CurrentPercent { get; set; }  // Activity.PercentEntry
    public string? Description { get; set; }      // Activity.Description
    
    // User-editable values
    public decimal? NewQty { get; set; }
    public decimal? NewPercent { get; set; }
    public bool IsSelected { get; set; }
    
    // Status
    public ScanMatchStatus Status { get; set; }
    public string? StatusMessage { get; set; }
    
    // Display helpers
    public int? ActivityId => RawExtraction.ActivityId;
    public int Confidence => (int)(RawExtraction.OverallConfidence * 100);
    
    public event PropertyChangedEventHandler? PropertyChanged;
}

public enum ScanMatchStatus
{
    Ready,       // Matched, ready to apply
    Warning,     // Matched but validation warning (e.g., % decrease)
    NotFound,    // ActivityID not in database
    InvalidId,   // Could not parse ActivityID as integer
    LowConfidence, // Confidence below threshold
    Error        // Processing error
}
```

---

## Service Layer

### TextractService.cs
AWS Textract API wrapper with retry logic and error handling.

```csharp
// AWS Textract integration service
public class TextractService : IDisposable
{
    private readonly AmazonTextractClient _client;
    private bool _disposed;
    
    // Configuration
    public const int MaxRetries = 3;
    public const int RetryDelayMs = 1000;
    public const float MinConfidenceThreshold = 0.70f;
    
    public TextractService()
    {
        // Credentials from Credentials.cs or environment
        var config = new AmazonTextractConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(
                Credentials.AwsRegion ?? "us-east-1")
        };
        
        _client = new AmazonTextractClient(
            Credentials.AwsAccessKey,
            Credentials.AwsSecretKey,
            config);
    }
    
    // Analyze single image for tables and forms
    public async Task<TextractPageResult> AnalyzeImageAsync(
        byte[] imageBytes, 
        int pageNumber,
        CancellationToken cancellationToken = default)
    {
        // Implementation with retry logic
    }
    
    // Analyze multi-page PDF (uses async job API)
    public async Task<List<TextractPageResult>> AnalyzePdfAsync(
        byte[] pdfBytes,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Uses StartDocumentAnalysis + GetDocumentAnalysis polling
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _client?.Dispose();
            _disposed = true;
        }
    }
}
```

### TextractParser.cs
Parse Textract blocks into structured row data aligned with Progress Book layout.

```csharp
// Parses raw Textract output into ProgressExtraction rows
public class TextractParser
{
    // Progress Book Zone 3 column headers (for identification)
    private static readonly string[] EntryColumnHeaders = 
        { "DONE", "QTY", "%" };
    
    // Parse Textract cells into extraction rows
    public List<ProgressExtraction> ParseTableResults(
        TextractPageResult page,
        int pageNumber)
    {
        var extractions = new List<ProgressExtraction>();
        
        // Group cells by row
        var rowGroups = page.Cells
            .Where(c => !c.IsHeader)
            .GroupBy(c => c.RowIndex)
            .OrderBy(g => g.Key);
        
        // Identify column indices from header row
        var columnMap = IdentifyColumns(page.Cells.Where(c => c.IsHeader));
        
        foreach (var row in rowGroups)
        {
            var extraction = ParseRow(row.ToList(), columnMap, pageNumber);
            if (extraction.HasAnyEntry && extraction.HasValidActivityId)
            {
                extractions.Add(extraction);
            }
        }
        
        return extractions;
    }
    
    // Identify which column index maps to which field
    private Dictionary<string, int> IdentifyColumns(
        IEnumerable<TextractCell> headerCells)
    {
        // Match "ActivityID" or "ID" column (first column with numeric content)
        // Match "DONE", "QTY", "%" columns by header text
    }
    
    // Parse single row into ProgressExtraction
    private ProgressExtraction ParseRow(
        List<TextractCell> cells,
        Dictionary<string, int> columnMap,
        int pageNumber)
    {
        // Extract ActivityID from first column (integer)
        // Extract Done/Qty/Pct from entry columns
        // Handle handwritten value parsing (checkmarks, numbers)
    }
    
    // Parse ActivityID (integer only)
    private (int? value, float confidence, string rawText) ParseActivityId(TextractCell cell)
    {
        var text = cell.Text.Trim();
        
        if (string.IsNullOrEmpty(text))
            return (null, 0f, text);
        
        // Remove any non-digit characters (common OCR artifacts)
        var digitsOnly = new string(text.Where(char.IsDigit).ToArray());
        
        if (string.IsNullOrEmpty(digitsOnly))
            return (null, 0f, text);
        
        if (int.TryParse(digitsOnly, out var activityId) && activityId > 0)
        {
            // Confidence penalty if we had to clean the text
            var confidence = text == digitsOnly ? cell.Confidence : cell.Confidence * 0.9f;
            return (activityId, confidence, text);
        }
        
        return (null, 0f, text);
    }
    
    // Parse handwritten checkbox (checkmark, X, filled box)
    private (bool? value, float confidence) ParseCheckbox(TextractCell cell)
    {
        var text = cell.Text.Trim().ToUpperInvariant();
        
        // Common checkbox markers
        if (string.IsNullOrEmpty(text))
            return (null, 1.0f);
            
        if (text is "X" or "✓" or "✔" or "V" or "YES" or "Y" or "1")
            return (true, cell.Confidence);
            
        if (text is "O" or "N" or "NO" or "0")
            return (false, cell.Confidence);
            
        // Ambiguous - return true with lower confidence
        return (true, cell.Confidence * 0.7f);
    }
    
    // Parse handwritten number (qty or percentage)
    private (decimal? value, float confidence) ParseNumber(TextractCell cell)
    {
        var text = cell.Text.Trim();
        
        if (string.IsNullOrEmpty(text))
            return (null, 1.0f);
            
        // Remove % symbol if present
        text = text.TrimEnd('%');
        
        // Handle common OCR errors
        text = text.Replace("O", "0")  // O vs 0
                   .Replace("l", "1")  // l vs 1
                   .Replace("I", "1"); // I vs 1
        
        if (decimal.TryParse(text, out var value))
        {
            // Validate percentage range
            if (value < 0 || value > 100)
                return (null, cell.Confidence * 0.5f);
                
            return (value, cell.Confidence);
        }
        
        return (null, 0f);
    }
}
```

### ProgressScanService.cs (Modify Existing)
Orchestrates the scan workflow - replace Claude Vision with Textract.

```csharp
// Orchestrates progress scan workflow
public class ProgressScanService : IDisposable
{
    private readonly TextractService _textract;
    private readonly TextractParser _parser;
    private readonly ScanMatchingService _matcher;
    private readonly ScanValidationService _validator;
    
    public ProgressScanService()
    {
        _textract = new TextractService();
        _parser = new TextractParser();
        _matcher = new ScanMatchingService();
        _validator = new ScanValidationService();
    }
    
    // Process uploaded images/PDF
    public async Task<List<ScanReviewItem>> ProcessUploadAsync(
        string[] filePaths,
        IProgress<(int current, int total, string status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var allExtractions = new List<ProgressExtraction>();
        int fileIndex = 0;
        
        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fileName = Path.GetFileName(filePath);
            progress?.Report((fileIndex, filePaths.Length, $"Processing {fileName}..."));
            
            try
            {
                var extractions = await ProcessFileAsync(filePath, cancellationToken);
                allExtractions.AddRange(extractions);
                
                AppLogger.Info(
                    $"Extracted {extractions.Count} entries from {fileName}", 
                    "ProgressScanService.ProcessUpload", 
                    App.CurrentUser?.Username);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressScanService.ProcessFile");
                // Continue with other files
            }
            
            fileIndex++;
        }
        
        // Match to database and validate
        progress?.Report((fileIndex, filePaths.Length, "Matching to database..."));
        var reviewItems = await MatchAndValidateAsync(allExtractions);
        
        AppLogger.Info(
            $"Scan complete: {reviewItems.Count} items, " +
            $"{reviewItems.Count(r => r.Status == ScanMatchStatus.Ready)} ready, " +
            $"{reviewItems.Count(r => r.Status == ScanMatchStatus.NotFound)} not found",
            "ProgressScanService.ProcessUpload",
            App.CurrentUser?.Username);
        
        return reviewItems;
    }
    
    // Process single file (image or PDF)
    private async Task<List<ProgressExtraction>> ProcessFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        if (extension == ".pdf")
        {
            return await ProcessPdfAsync(bytes, cancellationToken);
        }
        else
        {
            // Single image
            var pageResult = await _textract.AnalyzeImageAsync(bytes, 1, cancellationToken);
            return _parser.ParseTableResults(pageResult, 1);
        }
    }
    
    // Process multi-page PDF
    private async Task<List<ProgressExtraction>> ProcessPdfAsync(
        byte[] pdfBytes,
        CancellationToken cancellationToken)
    {
        var extractions = new List<ProgressExtraction>();
        var pageResults = await _textract.AnalyzePdfAsync(pdfBytes, null, cancellationToken);
        
        foreach (var page in pageResults)
        {
            var pageExtractions = _parser.ParseTableResults(page, page.PageNumber);
            extractions.AddRange(pageExtractions);
        }
        
        return extractions;
    }
    
    // Match extractions to database records
    private async Task<List<ScanReviewItem>> MatchAndValidateAsync(
        List<ProgressExtraction> extractions)
    {
        var reviewItems = new List<ScanReviewItem>();
        
        foreach (var extraction in extractions)
        {
            var item = new ScanReviewItem { RawExtraction = extraction };
            
            // Validate ActivityID was parsed
            if (!extraction.HasValidActivityId)
            {
                item.Status = ScanMatchStatus.InvalidId;
                item.StatusMessage = $"Could not parse ActivityID: '{extraction.RawActivityIdText}'";
                reviewItems.Add(item);
                continue;
            }
            
            // Look up by ActivityID (integer)
            var activity = await _matcher.FindActivityByIdAsync(extraction.ActivityId!.Value);
            
            if (activity == null)
            {
                item.Status = ScanMatchStatus.NotFound;
                item.StatusMessage = $"ActivityID {extraction.ActivityId} not found in database";
            }
            else
            {
                item.MatchedActivity = activity;
                item.CurrentQty = activity.EarnQtyEntry;
                item.CurrentPercent = activity.PercentEntry;
                item.Description = activity.Description;
                
                // Set proposed new values
                item.NewQty = extraction.Qty ?? item.CurrentQty;
                item.NewPercent = extraction.Pct ?? item.CurrentPercent;
                
                // Handle DONE checkbox → 100%
                if (extraction.Done == true && !extraction.Pct.HasValue)
                {
                    item.NewPercent = 100;
                }
                
                // Validate
                var validation = _validator.Validate(item);
                item.Status = validation.Status;
                item.StatusMessage = validation.Message;
            }
            
            // Mark low confidence
            if (extraction.OverallConfidence < TextractService.MinConfidenceThreshold)
            {
                item.Status = ScanMatchStatus.LowConfidence;
                item.StatusMessage = $"Low confidence ({extraction.OverallConfidence:P0})";
            }
            
            reviewItems.Add(item);
        }
        
        return reviewItems;
    }
    
    public void Dispose()
    {
        _textract?.Dispose();
    }
}
```

---

## User Interface

### ProgressScanDialog.xaml
Minimal changes - same upload flow, just different backend service.

**Key Elements:**
- Drag-drop zone for files (PDF, PNG, JPG)
- File list showing queued uploads
- Progress bar during Textract processing
- Cancel button for long operations
- Status text showing extraction progress

### ProgressScanReviewDialog.xaml (Existing)
No changes needed - same review/approve/reject workflow.

**Grid Columns:**
| Column | Binding | Editable | Notes |
|--------|---------|----------|-------|
| ☑ Select | IsSelected | Yes | Checkbox for bulk actions |
| ActivityID | ActivityId | No | From extraction (integer) |
| Description | Description | No | From matched Activity |
| Current % | CurrentPercent | No | Existing value |
| New % | NewPercent | Yes | Proposed value |
| Current QTY | CurrentQty | No | Existing value |
| New QTY | NewQty | Yes | Proposed value |
| Confidence | Confidence | No | Color-coded (green/yellow/red) |
| Status | Status | No | Ready/Warning/NotFound/InvalidId/Error |

**Confidence Color Coding:**
- Green (≥90%): High confidence
- Yellow (70-89%): Medium confidence, review recommended
- Red (<70%): Low confidence, manual verification required

---

## Database Integration

### ActivityID Matching
ActivityID is the primary key for matching extractions to database records. Using integer instead of UniqueID for better OCR accuracy.

```sql
-- ScanMatchingService query
SELECT * FROM Activities 
WHERE ActivityID = @activityId 
  AND ProjectID = @projectId 
  AND IsDeleted = 0
```

### Applying Updates
Updates use existing Activity update pattern with LocalDirty flag.

```csharp
// Apply approved scan results
public async Task<int> ApplyUpdatesAsync(
    IEnumerable<ScanReviewItem> approvedItems,
    string username)
{
    int updateCount = 0;
    
    using var connection = DatabaseSetup.GetConnection();
    await connection.OpenAsync();
    using var transaction = connection.BeginTransaction();
    
    try
    {
        foreach (var item in approvedItems.Where(i => i.IsSelected && i.Status == ScanMatchStatus.Ready))
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                UPDATE Activities SET
                    PercentEntry = @percent,
                    EarnQtyEntry = @qty,
                    LocalDirty = 1,
                    UpdatedBy = @username,
                    UpdatedUtcDate = @utcNow
                WHERE ActivityID = @activityId";
            
            cmd.Parameters.AddWithValue("@percent", item.NewPercent ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@qty", item.NewQty ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@utcNow", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@activityId", item.ActivityId!.Value);
            
            updateCount += await cmd.ExecuteNonQueryAsync();
        }
        
        transaction.Commit();
        
        AppLogger.Info(
            $"Applied scan updates: {updateCount} records",
            "ProgressScanService.ApplyUpdates",
            username);
        
        return updateCount;
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

---

## Implementation Phases

### Phase 1: AWS Infrastructure (1-2 days)
**Files:** Credentials.cs, Services/AI/TextractService.cs

**Tasks:**
1. Add AWS credentials to Credentials.cs (AccessKey, SecretKey, Region)
2. Add AWSSDK.Textract NuGet package
3. Implement TextractService with single-image analysis
4. Test API connectivity with sample image
5. Implement retry logic and error handling

**Validation:** Successfully analyze test image via API

### Phase 2: Parser Implementation (2-3 days)
**Files:** Services/AI/TextractParser.cs, Models/AI/TextractExtractionResult.cs

**Tasks:**
1. Create data models for Textract response
2. Implement column identification from headers
3. Implement row parsing with UniqueID extraction
4. Implement checkbox parsing (checkmarks, X marks)
5. Implement number parsing with OCR error correction
6. Test with real Progress Book scans

**Validation:** Parser correctly extracts UniqueID and values from test images

### Phase 3: Service Integration (1-2 days)
**Files:** Services/AI/ProgressScanService.cs

**Tasks:**
1. Replace Claude Vision calls with Textract calls
2. Wire up TextractParser for result processing
3. Maintain existing matching and validation logic
4. Test end-to-end with single image
5. Implement multi-page PDF support

**Validation:** Full extraction pipeline works for single and multi-page documents

### Phase 4: UI Updates (1 day)
**Files:** ViewModels/ProgressScanViewModel.cs, Views/ProgressScanDialog.xaml

**Tasks:**
1. Update progress reporting for Textract async jobs
2. Update error messages for AWS-specific errors
3. Test drag-drop and file selection
4. Test cancel functionality during processing

**Validation:** UI correctly shows progress and handles errors

### Phase 5: Testing & Refinement (2-3 days)
**Tasks:**
1. Test with variety of handwriting styles
2. Test with low-quality scans (phone photos)
3. Tune confidence thresholds
4. Add column position validation using bounding boxes
5. Document edge cases and handling

**Validation:** Accuracy meets 85%+ target on representative sample

---

## Technical Specifications

### NuGet Dependencies
```xml
<PackageReference Include="AWSSDK.Textract" Version="3.7.x" />
```

### AWS Permissions Required
```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "textract:AnalyzeDocument",
                "textract:StartDocumentAnalysis",
                "textract:GetDocumentAnalysis"
            ],
            "Resource": "*"
        }
    ]
}
```

### Supported File Formats
- **Images:** PNG, JPG, JPEG (single page)
- **Documents:** PDF (multi-page, uses async API)

### Image Requirements
- **Minimum resolution:** 150 DPI recommended
- **Maximum file size:** 10 MB per image, 500 MB per PDF
- **Orientation:** Auto-detected, but upright preferred

### API Rate Limits
- **Synchronous (AnalyzeDocument):** 1 request/second default
- **Asynchronous (StartDocumentAnalysis):** 20 concurrent jobs

### Logging Requirements
```csharp
// Scan started
AppLogger.Info($"Progress scan started: {fileCount} files", 
    "ProgressScanService.StartScan", username);

// Textract API call
AppLogger.Info($"Textract analyzing page {pageNum}: {confidence:P0} avg confidence",
    "TextractService.AnalyzeImage", username);

// Extraction complete
AppLogger.Info($"Extracted {entryCount} entries from {pageCount} pages",
    "ProgressScanService.Complete", username);

// Updates applied
AppLogger.Info($"Applied scan updates: {updateCount} records",
    "ProgressScanService.ApplyUpdates", username);

// Errors
AppLogger.Error(ex, "TextractService.AnalyzeImage");
```

---

## Testing Strategy

### Unit Tests
1. **TextractParser tests** - Parse sample Textract JSON responses
2. **Checkbox parsing** - Various checkmark styles (X, ✓, filled)
3. **Number parsing** - Common OCR errors (O/0, l/1)
4. **Confidence calculation** - Weighted average logic

### Integration Tests
1. **End-to-end extraction** - Real images through full pipeline
2. **Multi-page PDF** - Document with 5+ pages
3. **Error handling** - Invalid credentials, rate limits, network failures

### Manual Test Cases
| Test Case | Input | Expected Result |
|-----------|-------|-----------------|
| Clean handwriting | Clear numbers | High confidence (>90%) |
| Messy handwriting | Scribbled numbers | Medium confidence (70-90%) |
| Phone photo | Slightly skewed | Successful extraction |
| Low light photo | Dark/blurry | Warning or low confidence |
| Empty sheet | No handwriting | Zero extractions |
| Partial entries | Some rows marked | Only marked rows extracted |

### Accuracy Targets
- **ActivityID extraction:** 98%+ accuracy (integer-only, much easier than alphanumeric)
- **Checkbox detection:** 90%+ accuracy
- **Number extraction:** 85%+ accuracy
- **Overall row extraction:** 90%+ usable (high + medium confidence)

---

## Cost Analysis

### AWS Textract Pricing (us-east-1)
- **Tables:** $0.015 per page
- **Forms (handwriting):** $0.050 per page
- **Combined (TABLES + FORMS):** ~$0.015 per page (tables include form detection)

### Estimated Monthly Cost

| Usage Level | Pages/Month | Monthly Cost |
|-------------|-------------|--------------|
| Light (1 FE) | 200 pages | $3.00 |
| Medium (5 FEs) | 1,000 pages | $15.00 |
| Heavy (10 FEs) | 2,500 pages | $37.50 |

### Cost Controls
1. **User awareness** - Show estimated page count before processing
2. **Admin dashboard** - Track API usage per user/project (future)
3. **Daily limits** - Optional cap on pages per user (future)

---

## Risks and Mitigations

### Risk: Poor Handwriting Accuracy
**Likelihood:** Medium  
**Impact:** High - defeats purpose of feature  
**Mitigation:**
- Confidence thresholds flag uncertain extractions
- Review UI allows manual correction
- Document recommended writing practices for field workers

### Risk: ActivityID Extraction Errors
**Likelihood:** Low (mitigated by using integer instead of alphanumeric)  
**Impact:** High - wrong activity updated  
**Mitigation:**
- ActivityID is integer-only (5-6 digits) - much easier to OCR than 19-char UniqueID
- Strict confidence threshold for ActivityID (90%+)
- Bounding box validation ensures correct column
- Review UI shows ActivityID for verification
- No auto-apply without user confirmation

### Risk: AWS Service Issues
**Likelihood:** Low  
**Impact:** Medium - temporary feature unavailability  
**Mitigation:**
- Retry logic with exponential backoff
- Clear error messages for users
- Graceful degradation (manual entry fallback)

### Risk: Cost Overruns
**Likelihood:** Low  
**Impact:** Low - modest per-page cost  
**Mitigation:**
- Usage logging for monitoring
- Optional daily limits
- User sees page count before processing

### Risk: Progress Book Format Changes
**Likelihood:** Medium  
**Impact:** Medium - parser may fail  
**Mitigation:**
- Column identification by header text, not position
- Flexible parser handles minor layout variations
- Version Progress Book layouts with AI compatibility flag

---

## Appendix: Progress Book Layout Reference

### Zone Structure (from ProgressBookPdfGenerator)
```
┌─────────────────────────────────────────────────────────────────────────┐
│ Zone 1: ActivityID (always first, integer, 5-6 digits)                  │
│ Zone 2: User-configured columns (Description, Area, Tag, etc.)          │
│ Zone 3: Fixed entry columns (REM QTY, REM MH, CUR QTY, CUR %, DONE,    │
│         QTY entry box, % entry box)                                     │
└─────────────────────────────────────────────────────────────────────────┘
```

### Column Header Identification
The parser identifies columns by matching header text:
- **ActivityID:** First column, or header contains "ActivityID" or "ID" (integer values)
- **DONE:** Header is "DONE" (checkbox column)
- **QTY:** Header is "QTY" followed by entry box
- **%:** Header is "%" or "% ENTRY" (percentage entry column)

### Entry Box Recognition
Entry boxes appear as empty rectangles with borders:
- **Checkbox (DONE):** Small square, ~30pt width
- **QTY box:** Medium rectangle, ~55pt width
- **% box:** Medium rectangle, ~55pt width

Textract detects these as CELL boundaries and extracts any content inside.

### ActivityID vs UniqueID
| Property | ActivityID | UniqueID |
|----------|------------|----------|
| Type | Integer | String |
| Example | 11967 | i251009101621125ano |
| Length | 5-6 digits | 19 characters |
| Characters | 0-9 only | a-z, 0-9 |
| OCR Accuracy | ~98% | ~70-85% |
| Used for | AI Scan matching | Sync operations |

---

## Appendix: Migration from Claude Vision

### Files to Modify
1. **Delete:** `Services/AI/ClaudeVisionService.cs`
2. **Delete:** `Services/AI/ClaudeApiConfig.cs`
3. **Modify:** `Services/AI/ProgressScanService.cs` - replace Claude calls with Textract
4. **Modify:** `Models/AI/ScanExtractionResult.cs` - rename to ProgressExtraction, use ActivityId (int)
5. **Modify:** `Models/AI/ScanReviewItem.cs` - update ActivityId binding
6. **Modify:** `Services/ProgressBook/ProgressBookPdfGenerator.cs` - ensure ActivityID is in first column

### Credential Changes
Remove from Credentials.cs:
```csharp
// Remove
public static string ClaudeApiKey => "...";
public static string ClaudeApiEndpoint => "...";
public static string ClaudeModel => "...";
```

Add to Credentials.cs:
```csharp
// Add
public static string AwsAccessKey => "...";
public static string AwsSecretKey => "...";
public static string AwsRegion => "us-east-1";
```

### Progress Book PDF Changes
Ensure the PDF generator places ActivityID (not UniqueID) in the first column (Zone 1). This change should already be in place per your update.

---

**END OF DOCUMENT**
