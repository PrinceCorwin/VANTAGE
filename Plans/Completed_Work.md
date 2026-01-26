# MILESTONE - Completed Work

This document tracks completed features and fixes. Items are moved here from Project_Status.md after user confirmation.

---

### January 25, 2026
- **Progress Grid - Grouping Feature:**
  - Added AllowGrouping and ShowGroupDropArea to ProgressView grid
  - Users can drag column headers to Group Drop Area to group rows by value
  - Multi-level grouping supported (drag multiple columns)
  - Expand/collapse groups with arrow icons
  - Right-click menu: "Freeze Columns to Here" and "Unfreeze All Columns"
  - Frozen column count persists in UserSettings
  - Documentation added to Help manual

- **Progress Grid - Visual Improvements:**
  - Fixed inconsistent cell border widths across column types
  - Added explicit BorderThickness (0.5px) via SfDataGrid.Resources style
  - Added cell padding (4px left/right) to all 92 columns for better readability
  - Added UseLayoutRounding and SnapsToDevicePixels for pixel-perfect rendering

- **Progress Grid - Performance Optimizations:**
  - Added UseDrawing="Default" for faster cell rendering (GDI+ vs TextBlock)
  - Added ColumnSizer optimization: auto-size on first load, then disable for performance

- **Progress Grid - Table Summary Row:**
  - Added summary row at bottom of grid showing Sum totals for numeric columns
  - Columns: Quantity, EarnQtyEntry, BudgetMHs, EarnMHsCalc, ClientBudget
  - Auto-updates when values change or filters applied (uses Syncfusion LiveDataUpdateMode)
  - Summary row stays frozen when scrolling vertically

- **Progress Grid - Multi-Cell Copy (Ctrl+C):**
  - Select multiple cells with Ctrl+Click or Shift+Click, press Ctrl+C
  - Copies in Excel-compatible format (tab-separated columns, newline-separated rows)
  - Pastes correctly into Excel cells
  - Uses PreviewKeyDown intercept to capture Ctrl+C before edit control

- **Progress Grid - Multi-Cell Paste (Ctrl+V):**
  - Single cell + multi-row clipboard: pastes downward from selected cell
  - Multi-cell selection: pastes to leftmost column only
  - Multi-column clipboard: uses first column only
  - Validates: editable column, user ownership, valid value types
  - Auto-dates for PercentEntry (SchStart/SchFinish auto-set)
  - Type conversion with clear error messages for mismatches
  - Marks LocalDirty, saves to database, refreshes grid

- **Progress Grid - Column Header Copy Options:**
  - Right-click any column header for new copy options
  - "Copy Column w/ Header" - copies header + all visible row values
  - "Copy Column w/o Header" - copies only visible row values
  - Works on read-only columns (Find & Replace hidden for read-only, copy options visible)

- **Help Manual - Copying Data Documentation:**
  - Added Ctrl+C to keyboard shortcuts table
  - Added new "Copying Data" section with multi-cell copy, column copy, and row copy docs
  - Updated Table of Contents

- **AI Progress Scan - OCR Improvements and UI Enhancements:**
  - Removed grayscale preprocessing (images are already B&W)
  - Changed default contrast from 1.3 to 1.2, slider range 1.0-2.0
  - Moved contrast slider from upload panel to results panel only (with Rescan button)
  - Added "00" → "100" OCR heuristic to handle missed leading 1 in handwritten entries
  - Added column header filtering (AllowFiltering) to results grid
  - Added BudgetMHs column to results grid (shows "NOT FOUND" if ActivityID not matched)
  - Added Select All button, renamed buttons to "Select Ready" and "Clear"
  - Removed Raw debug column
  - Widened dialog (1050x600), increased column widths for Cur %, New %, Conf
  - Added column resizing (AllowResizingColumns)
  - Added persistence for dialog size and column widths to UserSettings

- **Progress Grid - Global Search:**
  - Added pill-shaped search box in toolbar (left of REFRESH button)
  - Searches across commonly-used columns: ActivityID, Description, WorkPackage, PhaseCode, CompType, Area, RespParty, AssignedTo, Notes, TagNO, UniqueID, DwgNO, LineNumber, SchedActNO
  - Case-insensitive, filters on each keystroke
  - X button clears search
  - Combines with existing filters (Today, User Defined, column filters)
  - Clear Filters button also clears search text

---

### January 24, 2026
- **EarnQtyEntry Recalculation Bug Fixed:**
  - Added `RecalculateDerivedFields(changedField)` method to Activity.cs
  - Find/Replace now triggers recalculation after programmatic property changes
  - Progress summary panel updates after Find/Replace completes
  - Fixes: PercentEntry ↔ EarnQtyEntry sync, Quantity changes, BudgetMHs changes

- **AI Progress Scan - Image Preprocessing for OCR:**
  - Created ImagePreprocessor.cs with grayscale conversion and 30% contrast enhancement
  - Integrated preprocessing into ProgressScanService before Textract analysis
  - Fixes handwritten "100" being misread as "0" by improving image clarity

- **AI Progress Scan - Legacy Code Cleanup:**
  - Removed Done checkbox concept (legacy - now using % entry for completion)
  - Removed ExtractedDone, ExtractedQty, CurrentQty, NewQty from ScanReviewItem
  - Removed Done and Qty from ScanExtractionResult
  - Cleaned up TextractService to not set Done field

- **AI Progress Scan - Review Grid Fix (Proper Syncfusion Implementation):**
  - Changed to GridCheckBoxColumn (Syncfusion native) instead of template with WPF CheckBox
  - Set EditTrigger="OnTap" for single-click editing of all cells
  - Set SelectionMode="Single" with SelectionUnit="Cell" for proper cell interaction
  - Both checkboxes and New % cells now editable with single click
  - CurrentCellEndEdit event updates selection count

- **Progress Grid - Added ActivityID Column:**
  - Added ActivityID as visible column in ProgressView.xaml (after UniqueID)
  - Used for AI scan matching (shorter than UniqueID, easier for OCR)

- **AI Progress Scan - AWS Textract Implementation (100% accuracy achieved):**
  - Switched from Claude Vision API to AWS Textract for table extraction
  - Textract provides proper table structure with row/column indices and bounding boxes
  - Created TextractService.cs - AWS API wrapper with retry logic
  - Updated ProgressScanService.cs to use Textract instead of Claude Vision
  - Removed ClaudeVisionService.cs and ClaudeApiConfig.cs
  - PDF layout redesigned:
    - ID (ActivityID) moved to first column (Zone 1) - protected from accidental marks
    - Data columns: MHs (BudgetMHs), QTY (Quantity), REM MH, CUR % (removed REM QTY, CUR QTY)
    - Single % ENTRY box at far right - natural stopping point for field hands
    - Writing "100" = done (eliminated checkbox entirely)
  - Testing: 100% accuracy on 2 PDF scans and 1 JPEG scan
  - Added CLAUDE.md instruction: never modify Credentials.cs without explicit permission

- **AI Progress Scan - Architecture changes for accuracy (earlier):**
  - Switched to Claude Opus 4.5 model (`claude-opus-4-5-20251101`) for better vision accuracy
  - Implemented Tool Use (function calling) for structured output consistency
    - Defined `report_progress_entry` tool with strict schema
    - Eliminates JSON parsing variability - same results on repeated scans
  - Fixed PDF-to-image conversion:
    - Removed PdfiumViewer (incompatible with .NET 8, caused `FPDF_Release` entry point error)
    - Added Syncfusion.PdfToImageConverter.WPF package
    - PDF pages now convert to images before sending to API
  - Removed color fills from entry boxes (colors weren't helping AI accuracy):
    - All entry boxes now white background
    - AI relies on text labels instead of colors
  - Added text labels to all entry columns for AI identification:
    - DONE column: "C:" label (C = Complete)
    - QTY column: "Qty:" label
    - % ENTRY column: "%:" label
  - Updated AI prompts to focus on reading text labels, not colors
  - **Status:** Accuracy still inconsistent between PDF and JPEG scans - testing continues

---

### January 22, 2026
- **AI Progress Scan - Major accuracy improvements:**
  - Changed Progress Book format from UniqueID to ActivityID (shorter, easier to OCR)
  - Added color-coded entry fields for better AI column recognition:
    - DONE checkbox: Light green (230, 255, 230)
    - QTY entry: Light blue (230, 240, 255)
    - % ENTRY: Light yellow (255, 255, 230)
  - Entry fields only render for incomplete items (CUR % < 100) - reduces visual noise
  - Default font size increased from 6pt to 8pt with warning below 7pt
  - Updated AI extraction prompt to reference color-coded columns explicitly
  - Updated matching logic to use ActivityID (int) instead of UniqueID (string)
  - Removed PdfiumViewer dependency - PDFs now sent directly to Claude API
    - Claude handles multi-page PDFs natively with better quality
    - Removed PdfiumViewer and PdfiumViewer.Native.x86_64.v8-xfa packages
    - Simplified PdfToImageConverter.cs to just file type detection
  - Added font size warning display in Progress Books view
  - Testing confirmed: 7 extracted, 7 matched, accurate QTY vs % column distinction

### January 21, 2026
- **AI Progress Scan feature (Phases 4-5):**
  - Created Claude Vision API infrastructure in Services/AI/:
    - ClaudeApiConfig.cs - API configuration (key, version, endpoints)
    - ClaudeVisionService.cs - Image analysis with retry logic and rate limiting
    - PdfToImageConverter.cs - PDF-to-PNG conversion using PdfiumViewer at 200 DPI
    - ProgressScanService.cs - Orchestrates scan workflow with progress reporting
  - Created AI models in Models/AI/:
    - ScanExtractionResult.cs - JSON response model for Claude API
    - ScanReviewItem.cs - Bindable review grid item with INotifyPropertyChanged
    - ScanProgress.cs - Progress tracking with ScanBatchResult
  - Created ProgressScanDialog with 3-step workflow:
    - Step 1: Drag-drop or browse for PDF/PNG/JPG files
    - Step 2: Processing with progress bar and cancel support
    - Step 3: Review grid with checkbox selection, filtering, editable New %/QTY columns
  - Added SCAN button to ProgressView toolbar
  - Added PdfiumViewer and PdfiumViewer.Native.x86_64.v8-xfa NuGet packages

- **Progress Book - Exclude Completed Activities:**
  - Added checkbox to filter section in ProgressBooksView.xaml
  - Added ExcludeCompleted property to ProgressBookConfiguration model
  - Updated preview and generate queries to filter out 100% progress activities
  - Persists with layout save/load

- **Progress Book - UI cleanup:**
  - Removed Layout Zones section from ProgressBooksView (kept Save button)
  - Removed UpdateZone2Summary method and all calls to it

- **Progress Book PDF - Header redesign and fixes:**
  - New page header layout: Logo (half size) + project info on left, book title centered, date + page number on right
  - Removed footer - page numbers now in header (more vertical space for data)
  - Column headers fixed at 5pt font (not affected by slider)
  - Column padding halved from 8pt to 4pt
  - Header row height reduced from 20pt to 14pt
  - Fixed page numbering off-by-one error (was showing "Page 4 of 3")
  - Rewrote EstimatePageCount to simulate actual rendering logic for accurate page totals

### January 20, 2026
- **Progress Book PDF Generator - Auto-fit and layout improvements:**
  - Replaced percentage-based column widths with auto-fit based on actual content
  - Zone 2 columns (UniqueID, ROC, DESC, user columns) measure content to determine width
  - Zone 3 data columns (REM QTY, REM MH, CUR QTY, CUR %) also auto-fit
  - Only entry boxes (DONE, QTY, % ENTRY) have fixed widths
  - Description column wraps long lines (row height increases as needed)
  - Added project description to page header from Projects table (e.g., "24.005 - Fluor Lilly Near Site OSM Modules")
  - Page header fonts are static 12pt (not affected by font slider)
  - Column/group headers match font slider setting
  - Font slider range changed to 4-10pt with default 6pt
  - Increased cell padding for better readability
  - UniqueID moved to Zone 2 columns (user can reorder but not delete)
  - Separated Groups from Sorts: groups auto-sort alphanumerically, sorts stack like Excel
  - Up to 10 groups and 10 sort levels allowed
  - Removed SubGroupConfig.cs (no longer needed)
  - Added GetProjectDescription() to ProjectCache for header lookup

- **Progress Book Layout Builder - Style consistency update:**
  - Added Syncfusion SfSkinManager with FluentDark theme for proper control theming
  - Added RoundedButtonStyle and PrimaryButtonStyle matching WorkPackageView exactly
  - Updated GENERATE, Save, Refresh Preview buttons to use proper styles
  - Removed explicit Height from ComboBoxes (Syncfusion theme handles sizing)
  - Added Foreground to RadioButtons for visibility on dark background
  - GridSplitter persists position to UserSettings

### January 19, 2026
- **Progress Book Module - Phases 1-3 complete:**
  - Phase 1: Created data models in `Models/ProgressBook/`:
    - PaperSize.cs (Letter/Tabloid enum)
    - ColumnConfig.cs (Zone 2 column configuration)
    - SubGroupConfig.cs (sub-group level config)
    - ProgressBookConfiguration.cs (full layout config, serialized to JSON)
    - ProgressBookLayout.cs (database entity)
  - Phase 1: Added ProgressBookLayouts table to DatabaseSetup.cs with indexes
  - Phase 2: Created ProgressBookLayoutRepository.cs with full CRUD operations
  - Phase 3: Built Layout Builder UI in ProgressBooksView.xaml:
    - Layout name input and saved layouts dropdown
    - Paper size radio buttons (Letter/Tabloid landscape)
    - Font size slider (8-14pt)
    - Main group dropdown with starred common fields
    - Sub-groups section with add/remove
    - Zone 2 columns list with width inputs and remove buttons
    - Zone summary panel and preview placeholder
  - Phase 3: Created SelectFieldDialog.xaml for adding columns
  - Updated PRD with implementation decisions (Syncfusion PDF, 1 page/call, GlobalSettings limits)


- Help Manual - Work Packages section written (manual.html):
  - Added 7 subsections with TOC links: Overview, Layout, Generate Tab, WP Templates Tab, Form Templates Tab, Token System, Previewing Templates
  - Documented Generate tab: all settings (Project, Work Packages, WP Template, PKG Manager, Scheduler, WP Name Pattern, Logo, Output Folder), workflow steps, output structure
  - Documented WP Templates tab: template controls, forms list management, creation workflow
  - Documented Form Templates tab: all 5 form types (Cover, List, Form, Grid, Drawings) with settings tables
  - Documented complete token system: date/user tokens, work package tokens, project tokens, activity tokens (including UDF1-10)
  - Added notes and warnings for key concepts (WP Name Pattern usage, Grid vs Form differences, sample data in preview)
- Help Sidebar - Action buttons implemented:
  - Back to Top: scrolls WebView2 to top via JavaScript (window.scrollTo)
  - Print PDF: saves help content as PDF via WebView2.PrintToPdfAsync with SaveFileDialog
  - View in Browser: opens manual.html in default browser via Process.Start with UseShellExecute
- Help Sidebar - Search field improvements:
  - Added clear button (✕) that appears when text is present
  - Added italic "Search..." placeholder when field is empty
  - Clear button clears search and refocuses input field

### January 18, 2026
- **Auto-detecting Activity Import** - Consolidated Legacy and NewVantage imports into single smart import:
  - Added DetectFormat() method that identifies format by column headers (UDFNineteen/Val_Perc_Complete = Legacy, UniqueID/PercentEntry = NewVantage)
  - Removed ambiguous threshold-based percent detection (was using 1.5 threshold which caused edge cases)
  - Legacy format: ALWAYS multiply percent by 100 (strict conversion)
  - NewVantage format: ALWAYS use percent as-is (strict conversion)
  - Clear error messages if format cannot be determined or is mixed
  - Removed Legacy Import buttons from UI (auto-detect handles both formats)
  - Kept Legacy Export buttons for OldVantage system compatibility
  - Updated Import Activities tooltips to indicate auto-detection
- **Activity Import date/percent cleanup** - During import, cleans up date/percent inconsistencies:
  - PercentEntry = 0 → clears both SchStart and SchFinish
  - PercentEntry > 0 with no SchStart → sets SchStart to today
  - SchStart in future → clamps to today
  - PercentEntry < 100 → clears SchFinish
  - PercentEntry = 100 with no SchFinish → sets SchFinish to today
  - SchFinish in future → clamps to today
- **Export progress indicator** - Shows animated progress bar in bottom-right during Activity exports (both regular and Legacy)

### January 17, 2026
- **Schedule Change Log feature** - Apply Schedule detail grid edits to live Activities:
  - New ScheduleChangeLogEntry model and ScheduleChangeLogger utility class
  - ScheduleChangeLogDialog accessible via Tools → Schedule Change Log
  - Logs edits to PercentEntry, BudgetMHs, SchStart, SchFinish in detail grid
  - Dialog shows WeekEndDate, UniqueID, Description, Field, Old/New values with checkboxes
  - Smart duplicate handling: only applies most recent change per UniqueID+Field
  - Progress view auto-refreshes after applying changes
  - Log files stored in %LocalAppData%\VANTAGE\Logs\ScheduleChanges\
  - Help sidebar documentation added under Schedule Module section
- **Auto-purge for all log files** - Cleans up logs older than 30 days on startup:
  - AppLogger: Purges both physical log files (app-yyyyMMdd.log) and database Logs table entries
  - ScheduleChangeLogger: Purges schedule change JSON files
  - Uses filename date parsing (not file system dates) for reliable age detection
- **UDF18 renamed to RespParty (Responsible Party)** throughout the application:
  - Models, database layer, views, dialogs, import/export, documentation
  - Grid column now displays as "Resp Party" with required field styling
  - Legacy imports still work via ColumnMappings table
- Work Package template editors tested and validated:
  - Cover editor - editing, saving, preview
  - List editor - item add/remove/reorder, saving, preview
  - Grid editor - column add/remove/reorder, row count, saving, preview
  - Form editor - sections/items/columns, saving, preview
  - Type selection dialog - creating new templates of each type
- Summary stats column selector:
  - Added clickable column name in ProgressView summary stats (replaces static "Budget:" label)
  - Dropdown arrow indicator with context menu showing available numerical columns
  - Available columns: BudgetMHs, ClientBudget, Quantity, BudgetHoursGroup, BudgetHoursROC, ROCBudgetQTY, ClientEquivQty, BaseUnit
  - Earned calculation dynamically uses selected column: `selectedColumn * PercentEntry / 100`
  - Selection persists to UserSettings across sessions
  - Underline on hover indicates interactivity

### January 16, 2026
- Legacy Import/Export format support:
  - Added ExportFormat enum (Legacy, NewVantage) to ExcelExporter
  - Default format is now NewVantage (property names as headers, percentages as 0-100)
  - Legacy format uses OldVantage column names and 0-1 percentage decimals
  - Added Legacy menu items to File menu (Import Replace, Import Combine, Export, Template)
  - Toggle Legacy I/O Menu button in Settings popup (saves visibility state to UserSettings)
  - Legacy items hidden by default, appear at bottom of File menu when enabled
  - Updated ExcelImporter with format-aware column mapping and percentage conversion
  - File names include "_Legacy" suffix for Legacy format exports
- User Access Request feature:
  - Created AccessRequestDialog with username (read-only), full name, and email fields
  - Added GetAdminEmailsAsync to AzureDbManager (joins Admins with Users to get emails)
  - Added SendAccessRequestEmailAsync to EmailService (styled HTML email to all admins)
  - Modified App.xaml.cs access denied flow to offer "Request Access" option
  - Handles offline scenario with appropriate message
- Form Template Editor - Column delete prorate fix:
  - Grid and Form editors now prorate remaining column widths to 100% after column deletion
  - Matches existing behavior for add/edit operations
  - Affects Checklist, Punchlist, Signoff, Drawing Log templates
- Form Template Editor - Reset Defaults button:
  - Added Reset Defaults button for user-created templates (Cover, List, Grid, Form types)
  - Button hidden for built-in templates and Drawings placeholder
  - Resets template StructureJson to match a selected built-in template of same type
  - For types with multiple built-ins (Grid, Form), shows ResetTemplateDialog with ComboBox
  - For types with single built-in (Cover, List), resets directly with confirmation
  - Added GetBuiltInFormTemplatesByTypeAsync to TemplateRepository
  - Created Dialogs/ResetTemplateDialog.xaml for built-in selection
- Azure Migration Plan updated:
  - Marked Phase 2 (C# code changes) and Phase 3 (testing) as DEFERRED
  - Added Rollback Strategy section
  - Added Future Feature: Legacy Azure Table Save (admin uploads snapshots to dbo_VANTAGE_global_ProgressLog)
- Work Package PDF - Dynamic footer height:
  - Footer now auto-sizes based on content (was hardcoded 25pt, too small for long text)
  - Added MeasureFooterHeight() and GetFooterReservedHeight() to BaseRenderer
  - All renderers (Form, Grid, Cover, List) now use dynamic footer measurement
  - Page break logic accounts for actual footer size
  - Fixes Signoff Sheet footer text being cut off
- Work Package PDF - Punchlist base font size:
  - Added BaseHeaderFontSize property to GridStructure (default 9pt)
  - Punchlist template uses 6.3pt base (30% smaller) to fit many columns
  - Slider adjustment still works on top of reduced base size
  - Grid editor preserves BaseHeaderFontSize on save/load
- Form template names updated: Removed "WORK PACKAGE" prefix from all default templates (now just "Cover Sheet", "Punchlist", etc.)
- Help Sidebar - Search functionality:
  - Added search field below context header with ˄/˅ navigation buttons and match counter
  - Uses WebView2 Find API (CoreWebView2.Find) with SuppressDefaultFindDialog
  - Highlights all matches in yellow, scrolls first match into view
  - 300ms debounce on search input
  - Enter/Shift+Enter keyboard shortcuts for next/previous
  - Search clears when navigating to different module
  - Match counter shows "3 of 12" format
- Help Manual - Content writing (manual.html):
  - Restructured to 8 sections (added Main Interface as Section 2)
  - Rewrote Getting Started: What is MILESTONE, Before You Begin (admin setup, no login, first sync)
  - Wrote Main Interface: Layout, Navigation, Menus (all items listed), Status Bar, Help Sidebar, Shortcuts
  - Wrote comprehensive Progress Module (10 subsections): workflow, toolbar, filters, editing, metadata, sync
  - Wrote comprehensive Schedule Module (13 subsections): 3WLA, missed reasons, P6 import/export, discrepancies
  - Added nested TOC with anchor links to all subsections
  - Styled TOC (larger main sections, indented subsections)
  - Added screenshot placeholders throughout
- Documentation cleanup:
  - Merged Interactive_Help_Mode_Plan.md into Sidebar_Help_Plan.md
  - Deleted separate Interactive_Help_Mode_Plan.md, Sidebar_Help_Status.md
  - Help navigation simplified: always opens to top of document (removed anchor navigation)
  - Removed IHelpAware interface and all implementations (no longer needed)
  - Deleted Interfaces/IHelpAware.cs and empty Interfaces folder

### January 11, 2026
- Work Package Module - Drawings integration (Phase 1):
  - Added Drawings section to Generate tab with Local Folder / Procore source selector
  - DwgNO grid auto-populates from selected work packages (queries distinct DwgNO values)
  - Local folder fetch with smart matching: full DwgNO → fallback to last two segments (e.g., "017004-01")
  - Copies all matching revisions of each drawing
  - Captures "Not in DB" files (PDFs in folder not matching any DwgNO)
  - Renamed "Drawings - Template" to "Drawings - Placeholder" (marks position in WP)
  - Hidden Drawings from Form Templates edit dropdown (configured via Generate tab now)
  - DrawingsRenderer merges fetched PDFs into work package at placeholder position
  - Fixed "Cannot access closed file" error (keep loaded PDFs alive until final save)
- Work Package Module - Generate tab improvements:
  - Expanded "+ Field" dropdown with all Activity fields
  - Priority fields at top: Area, CompType, PhaseCategory, PhaseCode, SchedActNO, SystemNO, UDF2, WorkPackage
  - Separator line between priority and remaining fields
  - All fields alphabetically sorted within their sections
  - WP Name Pattern now persists (saves on focus lost, restores on view load)
- Work Package Module - Grid editor improvements:
  - Added Edit button (✎) for columns with edit panel (name + width fields)
  - Edit panel with Save/Cancel buttons, Enter/Escape keyboard support
- Work Package Module - Column width prorate fix (Grid and Form editors):
  - Fixed prorate algorithm: edited/added column keeps its input value
  - Other columns scale proportionally to fill remaining space (100 - fixedValue)
  - Prevents negative values and ensures columns always sum to 100%

### January 10, 2026
- Work Package Module - Form (Checklist) editor improvements:
  - Added Edit buttons (✎) for columns, sections, and section items
  - Edit panels with Save/Cancel buttons, Enter/Escape keyboard support
  - Auto-prorate column widths when adding new column (keeps total at 100%)
- Work Package Module - Font Size Adjust slider:
  - Added to Form, Grid, and List (TOC) editors
  - Range: -30% to +50% adjustment
  - Row height/line height scales proportionally with font size
  - Footer text remains at base font size for consistency
- Work Package Module - List (TOC) editor improvements:
  - Added "+ Add Item" dropdown with predefined items (WP Doc Expiration Date, Printed Date, WP Name, Schedule Activity No, Phase Code)
  - Added Blank Line and Line Separator options to dropdown
  - Blank lines display as italic dimmed "blank line" in editor
  - Line separators display as italic dimmed "line separator" and render as horizontal line in PDF
  - Reworked Edit/Add workflow: Edit button shows edit field (in-place update), Add New shows separate add field
  - Both panels hidden by default, appear only when triggered
- Work Package Module - Template management improvements:
  - Created TemplateNameDialog for clone/save-as-new operations with duplicate name validation
  - Clone now saves immediately after naming (no need to click Save)
  - Save on built-in templates prompts for new name via dialog
  - Added delete confirmation dialog for form templates (matches WP templates)
  - Built-in form templates now display in logical order: Cover, TOC, Checklist, Punchlist, Signoff, Drawing Log, Drawings
- Theme resources refactoring:
  - Added 20+ new color resources to DarkTheme.xaml (action buttons, overlays, errors, warnings, UI elements)
  - Created ThemeHelper utility class for code-behind theme resource access
  - Updated 15 XAML files to use theme resources instead of hard-coded hex colors
  - Updated 4 code-behind files to use ThemeHelper (MainWindow, AdminSnapshotsDialog, ProgressView, P6ImportDialog)
  - Widened DeletedRecordsView sidebar from 170px to 190px for header text
- Bulk percent update performance optimization:
  - Added BulkUpdatePercentAsync to ActivityRepository for single-transaction batch updates
  - Batches updates in groups of 500 to avoid SQLite's 999 parameter limit
  - Replaced per-record database updates with bulk operation (40k records now updates in seconds vs minutes)
  - Added chunked enumeration for large selections (>5000 records) with Task.Delay yields to keep UI responsive
  - Filters selected records to user's records only without freezing UI
- UserSettings refactored to remove UserID:
  - Removed UserID column from UserSettings table (single user per machine)
  - Simplified all SettingsManager methods to not require userId parameter
  - Removed App.CurrentUserID usage from all settings calls
  - Updated ThemeManager to use parameter-less signatures
- Settings export now excludes LastSyncUtcDate:
  - Ensures full sync on new machines when importing settings
  - New machine gets fresh sync instead of potentially stale timestamp
- UI improvements:
  - Moved Import/Export Settings from File menu to Settings popup (hamburger menu)
  - Settings grouped between Feedback Board and About MILESTONE
  - Replaced WPF ProgressBar with Syncfusion SfLinearProgressBar (indeterminate marquee animation) in ProgressView, MainWindow overlay, and SyncDialog overlay
  - Custom Percent Buttons now show full blocking overlay during bulk updates
  - Fixed MainWindow reference issue (use Application.Current.Windows.OfType instead of Application.Current.MainWindow which returns VS design adorner)

### January 9, 2026
- 3 Decimal Place Precision enforcement:
  - Created NumericHelper.RoundToPlaces() centralized rounding utility
  - Excel import: rounds all double values on read
  - Model setters: rounds BudgetMHs, Quantity, EarnQtyEntry, PercentEntry
  - Grid edit: auto-rounds on cell exit before saving
  - Excel export: rounds all double values on write
  - Database save: defensive rounding in ActivityRepository
  - Future: admin-configurable decimal places (low priority)
- Revert to Snapshot feature:
  - Renamed DeleteSnapshotsDialog to ManageSnapshotsDialog
  - Added "Revert To Selected" button (enabled for single selection only)
  - Warning dialog with backup option before reverting
  - Backup creates snapshot with today's date
  - Pre-sync ensures pending changes are saved before revert
  - Ownership validation skips records now owned by others
  - Created SkippedRecordsDialog to show records that couldn't be restored
  - Restores all fields except UniqueID, AssignedTo, and calculated fields
  - Records marked LocalDirty=1 for user-controlled sync

### January 8, 2026
- File organization cleanup:
  - Moved 5 dialogs to Dialogs/ folder with namespace updates
  - Renamed 8 files to PascalCase (preserved git history)
  - Moved ScheduleProjectMapping from Utilities to Models
  - Created WorkPackageViewModel (partial extraction)
  - Extracted UserItem, ProjectItem to Models/
  - Extracted ColumnDisplayConverter to Converters/
- Reorganized MainWindow menus: File menu grouped with separators, removed Reports/Analysis menus, cleaned up placeholders
- Moved Help/AI Sidebar to Tools menu, About to hamburger menu
- Added separators to Admin and Tools menus
- Consolidated status docs into single Project_Status.md

### January 7-8, 2026
- Added "My Records Only" checkbox to SYNC dialog
- Help Sidebar infrastructure complete (WebView2, IHelpAware, context-aware navigation, F1 shortcut)

### January 6-7, 2026
- Work Package PDF generation working with proper page sizing
- Form template editors implemented (Cover, List, Grid, Form types)
- Preview uses actual UI selections for token resolution
