using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using VANTAGE.Models;
using VANTAGE.Models.ProgressBook;
using VANTAGE.Utilities;

namespace VANTAGE.Services.ProgressBook
{
    // PDF generator for Progress Books with zone-based layout
    public class ProgressBookPdfGenerator
    {
        // Page dimensions (landscape orientation)
        private float _pageWidth;
        private float _pageHeight;
        private const float MarginLeft = 36f;   // 0.5 inch
        private const float MarginRight = 36f;
        private const float MarginTop = 36f;
        private const float MarginBottom = 36f;
        private float _contentWidth;

        // Zone 3 column names - data columns auto-fit, entry boxes are fixed
        private static readonly string[] Zone3DataColumns = { "REM QTY", "REM MH", "CUR QTY", "CUR %" };
        private static readonly (string Name, float WidthPts)[] Zone3EntryColumns = new[]
        {
            ("DONE", 30f),      // Small checkbox
            ("QTY", 55f),       // Entry box for handwriting
            ("% ENTRY", 55f)    // Entry box for handwriting
        };

        // Padding for cell content (space between text and grid lines)
        private const float ColumnPadding = 4f;

        // Row heights
        private const float HeaderRowHeight = 14f; // Smaller for 5pt column headers
        private const float GroupHeaderHeight = 18f;
        private float _dataRowHeight = 16f;
        private float _lineHeight = 10f; // Height of one line of text

        // Colors
        private static readonly PdfColor LightGray = new PdfColor(240, 240, 240);
        private static readonly PdfColor MediumGray = new PdfColor(200, 200, 200);
        private static readonly PdfColor HeaderGray = new PdfColor(220, 220, 220);
        private static readonly PdfColor GroupHeaderColor = new PdfColor(230, 230, 230);
        private static readonly PdfColor Black = new PdfColor(0, 0, 0);
        private static readonly PdfColor White = new PdfColor(255, 255, 255);

        // Brushes
        private static readonly PdfBrush BlackBrush = new PdfSolidBrush(Black);
        private static readonly PdfBrush WhiteBrush = new PdfSolidBrush(White);
        private static readonly PdfBrush LightGrayBrush = new PdfSolidBrush(LightGray);
        private static readonly PdfBrush HeaderGrayBrush = new PdfSolidBrush(HeaderGray);
        private static readonly PdfBrush GroupHeaderBrush = new PdfSolidBrush(GroupHeaderColor);

        // Pens
        private static readonly PdfPen ThinPen = new PdfPen(Black, 0.5f);
        private static readonly PdfPen NormalPen = new PdfPen(Black, 1f);
        private static readonly PdfPen ThickPen = new PdfPen(Black, 1.5f);

        // Fonts (set based on configuration)
        private PdfFont _headerFont = null!;
        private PdfFont _dataFont = null!;
        private PdfFont _descFont = null!;
        private PdfFont _smallFont = null!;
        private PdfFont _titleFont = null!;
        private PdfFont _groupFont = null!;

        // Configuration
        private ProgressBookConfiguration _config = null!;
        private string _bookName = string.Empty;
        private string _projectId = string.Empty;
        private string _projectDescription = string.Empty;

        // Calculated column widths (in points)
        private float _zone2Width;
        private List<(string FieldName, float Width)> _zone2ColumnWidths = new();
        private List<(string Name, float Width)> _zone3ColumnWidths = new();

        // Activities for measuring content widths
        private List<Activity> _activities = new();

        // Page tracking
        private int _pageNumber;
        private int _totalPages;

        // Active group headers for page continuation
        private List<(string FieldName, string Value, int Level)> _activeGroups = new();

        // Default logo path
        private const string DefaultLogoPath = "pack://application:,,,/Images/SummitLogoNoText.jpg";

        // Generate a progress book PDF
        public PdfDocument Generate(
            ProgressBookConfiguration config,
            List<Activity> activities,
            string bookName,
            string projectId,
            string projectDescription = "")
        {
            _config = config;
            _activities = activities;
            _bookName = bookName;
            _projectId = projectId;
            _projectDescription = projectDescription;

            InitializePageDimensions();
            InitializeFonts();
            CalculateAutoFitColumnWidths();

            var document = CreateDocument();

            if (activities.Count == 0)
            {
                // Generate empty page with header (page numbers in header now)
                var page = document.Pages.Add();
                RenderPageHeader(page, 1, 1);
                RenderColumnHeaders(page, GetHeaderY());
                return document;
            }

            // Group and sort activities
            var groupedData = GroupAndSortActivities(activities);

            // Calculate total pages for header display
            _totalPages = EstimatePageCount(groupedData);
            _pageNumber = 1; // Start at page 1

            // Render pages
            RenderPages(document, groupedData);

            return document;
        }

        // Initialize page dimensions based on paper size
        private void InitializePageDimensions()
        {
            if (_config.PaperSize == PaperSize.Tabloid)
            {
                // Tabloid landscape: 17" x 11"
                _pageWidth = 17f * 72f;  // 1224 points
                _pageHeight = 11f * 72f; // 792 points
            }
            else
            {
                // Letter landscape: 11" x 8.5"
                _pageWidth = 11f * 72f;  // 792 points
                _pageHeight = 8.5f * 72f; // 612 points
            }
            _contentWidth = _pageWidth - MarginLeft - MarginRight;
        }

        // Initialize fonts based on configuration
        // Page header and column header fonts are static, data fonts use config.FontSize
        private void InitializeFonts()
        {
            int dataFontSize = _config.FontSize;
            int descFontSize = Math.Max(dataFontSize - 1, 4);

            // Static fonts - not affected by font size slider
            _titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
            _smallFont = new PdfStandardFont(PdfFontFamily.Helvetica, 8, PdfFontStyle.Regular);
            _headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 5, PdfFontStyle.Bold); // Column headers fixed at 5pt
            _groupFont = new PdfStandardFont(PdfFontFamily.Helvetica, dataFontSize, PdfFontStyle.Bold);

            // Data fonts based on configuration
            _dataFont = new PdfStandardFont(PdfFontFamily.Helvetica, dataFontSize, PdfFontStyle.Regular);
            _descFont = new PdfStandardFont(PdfFontFamily.Helvetica, descFontSize, PdfFontStyle.Regular);

            // Adjust row height and line height based on data font size
            _lineHeight = descFontSize + 2;
            _dataRowHeight = dataFontSize + 8; // More padding for readability
        }

        // Calculate auto-fit column widths based on actual data content
        // ALL columns except entry boxes (DONE, QTY, % ENTRY) fit to content
        // Description gets remaining space
        private void CalculateAutoFitColumnWidths()
        {
            // Calculate Zone 3 widths - data columns auto-fit, entry boxes are fixed
            _zone3ColumnWidths.Clear();
            float zone3DataWidth = 0;
            float zone3EntryWidth = 0;

            // Measure Zone 3 data columns from actual data
            foreach (var colName in Zone3DataColumns)
            {
                float width = MeasureZone3Column(colName);
                _zone3ColumnWidths.Add((colName, width));
                zone3DataWidth += width;
            }

            // Add fixed entry columns
            foreach (var col in Zone3EntryColumns)
            {
                _zone3ColumnWidths.Add((col.Name, col.WidthPts));
                zone3EntryWidth += col.WidthPts;
            }

            float totalZone3Width = zone3DataWidth + zone3EntryWidth;

            // Zone 2 width = total width minus Zone 3
            _zone2Width = _contentWidth - totalZone3Width;

            // Calculate Zone 2 column widths by measuring actual content
            _zone2ColumnWidths.Clear();
            var columns = _config.Columns.OrderBy(c => c.DisplayOrder).ToList();

            // Measure each column (except Description which gets remainder)
            float totalMeasured = 0;
            int descIndex = -1;
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (col.FieldName.Equals("Description", StringComparison.OrdinalIgnoreCase))
                {
                    descIndex = i;
                    _zone2ColumnWidths.Add((col.FieldName, 0)); // Placeholder, calculated later
                }
                else
                {
                    float width = MeasureColumnWidth(col.FieldName);
                    _zone2ColumnWidths.Add((col.FieldName, width));
                    totalMeasured += width;
                }
            }

            // Description gets all remaining width
            if (descIndex >= 0)
            {
                float descWidth = _zone2Width - totalMeasured;
                if (descWidth < 50) descWidth = 50; // Absolute minimum for description
                _zone2ColumnWidths[descIndex] = ("Description", descWidth);
            }
        }

        // Measure Zone 3 data column width from actual data values
        private float MeasureZone3Column(string columnName)
        {
            // Measure header with extra padding to prevent truncation
            float headerWidth = _headerFont.MeasureString(columnName).Width + (ColumnPadding * 2);

            // Measure data values
            float maxDataWidth = 0;
            foreach (var activity in _activities)
            {
                string value = columnName switch
                {
                    "REM QTY" => (activity.Quantity - activity.EarnQtyEntry).ToString("N2"),
                    "REM MH" => (activity.BudgetMHs - activity.EarnMHsCalc).ToString("N2"),
                    "CUR QTY" => activity.EarnQtyEntry.ToString("N2"),
                    "CUR %" => activity.PercentEntry.ToString("N2") + "%",
                    _ => ""
                };
                float dataWidth = _dataFont.MeasureString(value).Width + (ColumnPadding * 2);
                if (dataWidth > maxDataWidth)
                    maxDataWidth = dataWidth;
            }

            return Math.Max(headerWidth, maxDataWidth);
        }

        // Measure the required width for a Zone 2 column based on actual data
        private float MeasureColumnWidth(string fieldName)
        {
            // Measure header text with extra padding to prevent truncation
            string header = GetColumnDisplayName(fieldName);
            float headerWidth = _headerFont.MeasureString(header).Width + (ColumnPadding * 2);

            if (_activities.Count == 0)
                return headerWidth;

            // Find longest data value
            float maxDataWidth = 0;
            var font = fieldName.Equals("Description", StringComparison.OrdinalIgnoreCase) ? _descFont : _dataFont;

            foreach (var activity in _activities)
            {
                string? value = GetFieldValue(activity, fieldName);
                if (!string.IsNullOrEmpty(value))
                {
                    float dataWidth = font.MeasureString(value).Width + (ColumnPadding * 2);
                    if (dataWidth > maxDataWidth)
                        maxDataWidth = dataWidth;
                }
            }

            // Return the larger of header or data width
            return Math.Max(headerWidth, maxDataWidth);
        }

        // Create PDF document with correct page settings
        private PdfDocument CreateDocument()
        {
            var document = new PdfDocument();
            document.PageSettings.Size = new SizeF(_pageWidth, _pageHeight);
            document.PageSettings.Margins.All = 0;
            return document;
        }

        // Group and sort activities according to configuration
        // Groups are sorted alphanumerically, activities are sorted by stacking sort fields
        private List<ActivityGroup> GroupAndSortActivities(List<Activity> activities)
        {
            var result = new List<ActivityGroup>();

            // If no groups configured, return ungrouped but sorted
            if (_config.Groups.Count == 0)
            {
                var group = new ActivityGroup
                {
                    FieldName = "All",
                    Value = "All Records",
                    Level = 0,
                    Activities = ApplyStackedSort(activities)
                };
                CalculateGroupSummaries(group);
                result.Add(group);
                return result;
            }

            // Main grouping by first group field
            var mainGroupField = _config.Groups[0];

            var mainGroups = activities
                .GroupBy(a => GetFieldValue(a, mainGroupField) ?? "(Blank)")
                .OrderBy(g => g.Key); // Alphanumeric sort

            foreach (var mainGroup in mainGroups)
            {
                var group = new ActivityGroup
                {
                    FieldName = mainGroupField,
                    Value = mainGroup.Key,
                    Level = 0
                };

                // Apply sub-grouping if more groups configured
                if (_config.Groups.Count > 1)
                {
                    group.SubGroups = CreateSubGroups(mainGroup.ToList(), 1);
                }
                else
                {
                    // Apply stacked sorting to activities
                    group.Activities = ApplyStackedSort(mainGroup.ToList());
                }

                CalculateGroupSummaries(group);
                result.Add(group);
            }

            return result;
        }

        // Recursively create sub-groups using Groups list
        private List<ActivityGroup> CreateSubGroups(List<Activity> activities, int groupIndex)
        {
            if (groupIndex >= _config.Groups.Count)
            {
                return new List<ActivityGroup>();
            }

            var groupField = _config.Groups[groupIndex];
            var result = new List<ActivityGroup>();

            var groups = activities
                .GroupBy(a => GetFieldValue(a, groupField) ?? "(Blank)")
                .OrderBy(g => g.Key); // Alphanumeric sort

            foreach (var group in groups)
            {
                var activityGroup = new ActivityGroup
                {
                    FieldName = groupField,
                    Value = group.Key,
                    Level = groupIndex
                };

                if (groupIndex + 1 < _config.Groups.Count)
                {
                    activityGroup.SubGroups = CreateSubGroups(group.ToList(), groupIndex + 1);
                }
                else
                {
                    // At deepest group level, apply stacked sorting
                    activityGroup.Activities = ApplyStackedSort(group.ToList());
                }

                CalculateGroupSummaries(activityGroup);
                result.Add(activityGroup);
            }

            return result;
        }

        // Apply stacked sorting (like Excel) using SortFields config
        // Each sort field is applied in order; "None" values are skipped
        private List<Activity> ApplyStackedSort(List<Activity> activities)
        {
            if (activities.Count == 0)
                return activities;

            // Get active sort fields (skip "None")
            var sortFields = _config.SortFields
                .Where(f => !string.IsNullOrEmpty(f) && !f.Equals("None", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sortFields.Count == 0)
            {
                // Default sort by Description if no sort fields
                return activities.OrderBy(a => a.Description ?? "").ToList();
            }

            // Apply stacked sort using LINQ
            IOrderedEnumerable<Activity>? orderedActivities = null;

            for (int i = 0; i < sortFields.Count; i++)
            {
                var field = sortFields[i];
                if (i == 0)
                {
                    orderedActivities = activities.OrderBy(a => GetFieldValue(a, field) ?? "");
                }
                else
                {
                    orderedActivities = orderedActivities!.ThenBy(a => GetFieldValue(a, field) ?? "");
                }
            }

            return orderedActivities?.ToList() ?? activities;
        }

        // Calculate summary values for a group
        private void CalculateGroupSummaries(ActivityGroup group)
        {
            var allActivities = GetAllActivitiesInGroup(group);

            group.TotalQty = allActivities.Sum(a => a.Quantity);
            group.TotalMH = allActivities.Sum(a => a.BudgetMHs);
            group.RemainingQty = allActivities.Sum(a => a.Quantity - a.EarnQtyEntry);
            group.RemainingMH = allActivities.Sum(a => a.BudgetMHs - a.EarnMHsCalc);
        }

        // Get all activities in a group (including sub-groups)
        private List<Activity> GetAllActivitiesInGroup(ActivityGroup group)
        {
            var activities = new List<Activity>();
            activities.AddRange(group.Activities);

            foreach (var subGroup in group.SubGroups)
            {
                activities.AddRange(GetAllActivitiesInGroup(subGroup));
            }

            return activities;
        }

        // Calculate exact page count by simulating the rendering logic
        private int EstimatePageCount(List<ActivityGroup> groups)
        {
            float y = GetHeaderY() + HeaderRowHeight; // After page header and column headers
            int pageCount = 1;
            int activeGroupCount = 0;

            foreach (var group in groups)
            {
                SimulateGroup(group, ref y, ref pageCount, ref activeGroupCount);
            }

            return pageCount;
        }

        // Simulate rendering a group to count page breaks
        private void SimulateGroup(ActivityGroup group, ref float y, ref int pageCount, ref int activeGroupCount)
        {
            // Check if we need a new page for group header
            if (y + GroupHeaderHeight + _dataRowHeight > _pageHeight - MarginBottom)
            {
                pageCount++;
                // After new page: header + column headers + continued group headers
                y = GetHeaderY() + HeaderRowHeight + (activeGroupCount * GroupHeaderHeight);
            }

            // Group header
            y += GroupHeaderHeight;
            activeGroupCount++;

            // Activities
            foreach (var activity in group.Activities)
            {
                float rowHeight = CalculateRowHeight(activity);

                if (y + rowHeight > _pageHeight - MarginBottom)
                {
                    pageCount++;
                    // After new page: header + column headers + continued group headers
                    y = GetHeaderY() + HeaderRowHeight + (activeGroupCount * GroupHeaderHeight);
                }

                y += rowHeight;
            }

            // Sub-groups
            foreach (var subGroup in group.SubGroups)
            {
                SimulateGroup(subGroup, ref y, ref pageCount, ref activeGroupCount);
            }

            // Remove this group from count when done
            activeGroupCount--;
        }

        // Get Y position after page header
        private float GetHeaderY()
        {
            return MarginTop + 40; // Smaller header: logo (20) + project text + spacing
        }

        // Render all pages
        private void RenderPages(PdfDocument document, List<ActivityGroup> groups)
        {
            PdfPage? currentPage = null;
            float y = 0;
            int rowIndex = 0;

            _activeGroups.Clear();

            foreach (var group in groups)
            {
                RenderGroup(document, group, ref currentPage, ref y, ref rowIndex);
            }
        }

        // Render a group and its contents
        private void RenderGroup(
            PdfDocument document,
            ActivityGroup group,
            ref PdfPage? currentPage,
            ref float y,
            ref int rowIndex)
        {
            // Check if we need a new page (no footer space needed)
            if (currentPage == null || y + GroupHeaderHeight + _dataRowHeight > _pageHeight - MarginBottom)
            {
                // Increment page number for new pages (after the first)
                if (currentPage != null)
                {
                    _pageNumber++;
                }

                currentPage = document.Pages.Add();
                RenderPageHeader(currentPage, _pageNumber, _totalPages);
                y = GetHeaderY();
                y = RenderColumnHeaders(currentPage, y);

                // Render continued group headers if we're in the middle of groups
                foreach (var activeGroup in _activeGroups)
                {
                    y = RenderGroupHeader(currentPage, activeGroup.FieldName, activeGroup.Value + " (continued)",
                        0, 0, 0, 0, activeGroup.Level, y);
                }
            }

            // Update active groups
            while (_activeGroups.Count > group.Level)
            {
                _activeGroups.RemoveAt(_activeGroups.Count - 1);
            }
            _activeGroups.Add((group.FieldName, group.Value, group.Level));

            // Render group header
            y = RenderGroupHeader(currentPage!, group.FieldName, group.Value,
                group.RemainingQty, group.RemainingMH, group.TotalQty, group.TotalMH, group.Level, y);

            // Render activities in this group
            foreach (var activity in group.Activities)
            {
                // Calculate height needed for this row (may vary due to description wrapping)
                float rowHeight = CalculateRowHeight(activity);

                if (y + rowHeight > _pageHeight - MarginBottom)
                {
                    _pageNumber++;

                    currentPage = document.Pages.Add();
                    RenderPageHeader(currentPage, _pageNumber, _totalPages);
                    y = GetHeaderY();
                    y = RenderColumnHeaders(currentPage, y);

                    // Render continued group headers
                    foreach (var activeGroup in _activeGroups)
                    {
                        y = RenderGroupHeader(currentPage, activeGroup.FieldName, activeGroup.Value + " (continued)",
                            0, 0, 0, 0, activeGroup.Level, y);
                    }
                }

                y = RenderDataRow(currentPage!, activity, rowIndex, y);
                rowIndex++;
            }

            // Render sub-groups
            foreach (var subGroup in group.SubGroups)
            {
                RenderGroup(document, subGroup, ref currentPage, ref y, ref rowIndex);
            }

            // Remove this group from active groups when done
            if (_activeGroups.Count > 0 && _activeGroups[^1].FieldName == group.FieldName)
            {
                _activeGroups.RemoveAt(_activeGroups.Count - 1);
            }
        }

        // Render page header with new layout:
        // LEFT: Logo (half size) with ProjectID - Description underneath
        // CENTER: Progress Book: {value}
        // RIGHT: Date on top, Page x of y underneath
        private void RenderPageHeader(PdfPage page, int pageNum, int totalPages)
        {
            var graphics = page.Graphics;
            float y = MarginTop;

            // LEFT SIDE: Logo (half size) with Project ID - Description underneath
            float logoHeight = 20f;
            float logoWidth = 0f;
            try
            {
                var logo = LoadImage(DefaultLogoPath);
                if (logo != null)
                {
                    logoWidth = logoHeight * (logo.Width / (float)logo.Height);
                    graphics.DrawImage(logo, MarginLeft, y, logoWidth, logoHeight);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressBookPdfGenerator.RenderPageHeader (logo)");
            }

            // Project ID - Description under logo
            string projectText = string.IsNullOrEmpty(_projectDescription)
                ? _projectId
                : $"{_projectId} - {_projectDescription}";
            graphics.DrawString(projectText, _smallFont, BlackBrush, new PointF(MarginLeft, y + logoHeight + 2));

            // CENTER: Progress Book name
            string bookTitle = $"Progress Book: {_bookName}";
            var bookTitleSize = _titleFont.MeasureString(bookTitle);
            float centerX = MarginLeft + (_contentWidth - bookTitleSize.Width) / 2;
            float centerY = y + 6; // Vertically centered in header area
            graphics.DrawString(bookTitle, _titleFont, BlackBrush, new PointF(centerX, centerY));

            // RIGHT SIDE: Date on first line, Page x of y underneath
            string dateText = DateTime.Now.ToString("MM/dd/yyyy");
            var dateSize = _smallFont.MeasureString(dateText);
            graphics.DrawString(dateText, _smallFont, BlackBrush,
                new PointF(MarginLeft + _contentWidth - dateSize.Width, y));

            string pageText = $"Page {pageNum} of {totalPages}";
            var pageSize = _smallFont.MeasureString(pageText);
            graphics.DrawString(pageText, _smallFont, BlackBrush,
                new PointF(MarginLeft + _contentWidth - pageSize.Width, y + 12));
        }

        // Render column headers
        private float RenderColumnHeaders(PdfPage page, float y)
        {
            var graphics = page.Graphics;
            float x = MarginLeft;

            // Header background
            graphics.DrawRectangle(HeaderGrayBrush, new RectangleF(x, y, _contentWidth, HeaderRowHeight));
            graphics.DrawRectangle(NormalPen, new RectangleF(x, y, _contentWidth, HeaderRowHeight));

            // Zone 2: User columns (includes UniqueID)
            foreach (var col in _zone2ColumnWidths)
            {
                string displayName = GetColumnDisplayName(col.FieldName);
                DrawCenteredText(graphics, displayName, _headerFont, x, y, col.Width, HeaderRowHeight);
                graphics.DrawLine(ThinPen, x + col.Width, y, x + col.Width, y + HeaderRowHeight);
                x += col.Width;
            }

            // Zone 3: Fixed columns
            foreach (var col in _zone3ColumnWidths)
            {
                DrawCenteredText(graphics, col.Name, _headerFont, x, y, col.Width, HeaderRowHeight);
                graphics.DrawLine(ThinPen, x + col.Width, y, x + col.Width, y + HeaderRowHeight);
                x += col.Width;
            }

            return y + HeaderRowHeight;
        }

        // Render group header with summaries
        private float RenderGroupHeader(
            PdfPage page,
            string fieldName,
            string value,
            double remQty,
            double remMH,
            double totQty,
            double totMH,
            int level,
            float y)
        {
            var graphics = page.Graphics;
            float indent = level * 20f;
            float x = MarginLeft + indent;
            float width = _contentWidth - indent;

            // Background
            graphics.DrawRectangle(GroupHeaderBrush, new RectangleF(x, y, width, GroupHeaderHeight));
            graphics.DrawRectangle(ThinPen, new RectangleF(x, y, width, GroupHeaderHeight));

            // Group label
            string displayName = GetColumnDisplayName(fieldName);
            string label = $"{displayName}: {value}";
            graphics.DrawString(label, _groupFont, BlackBrush, new PointF(x + 4, y + 3));

            // Summary (only show if values are non-zero)
            if (totQty > 0 || totMH > 0)
            {
                string summary = $"Rem: {remQty:N1} QTY, {remMH:N1} MH | Tot: {totQty:N1} QTY, {totMH:N1} MH";
                var summarySize = _smallFont.MeasureString(summary);
                graphics.DrawString(summary, _smallFont, BlackBrush,
                    new PointF(x + width - summarySize.Width - 4, y + 4));
            }

            return y + GroupHeaderHeight;
        }

        // Render a data row with variable height for description wrapping
        private float RenderDataRow(PdfPage page, Activity activity, int rowIndex, float y)
        {
            var graphics = page.Graphics;
            float x = MarginLeft;

            // Calculate row height based on description content
            float rowHeight = CalculateRowHeight(activity);

            // Alternating row background
            if (rowIndex % 2 == 1)
            {
                graphics.DrawRectangle(LightGrayBrush, new RectangleF(x, y, _contentWidth, rowHeight));
            }

            // Row border
            graphics.DrawRectangle(ThinPen, new RectangleF(x, y, _contentWidth, rowHeight));

            // Zone 2: User columns (includes UniqueID)
            foreach (var col in _zone2ColumnWidths)
            {
                string value = GetFieldValue(activity, col.FieldName) ?? "";

                if (col.FieldName.Equals("Description", StringComparison.OrdinalIgnoreCase))
                {
                    // Description uses wrapping
                    DrawWrappedText(graphics, value, _descFont, x, y, col.Width, rowHeight);
                }
                else
                {
                    // Other columns use single-line left-aligned text
                    DrawLeftText(graphics, value, _dataFont, x, y, col.Width, rowHeight, truncate: false);
                }
                graphics.DrawLine(ThinPen, x + col.Width, y, x + col.Width, y + rowHeight);
                x += col.Width;
            }

            // Zone 3: Fixed columns
            // REM QTY
            double remQty = activity.Quantity - activity.EarnQtyEntry;
            DrawRightText(graphics, remQty.ToString("N2"), _dataFont, x, y, _zone3ColumnWidths[0].Width, rowHeight);
            graphics.DrawLine(ThinPen, x + _zone3ColumnWidths[0].Width, y, x + _zone3ColumnWidths[0].Width, y + rowHeight);
            x += _zone3ColumnWidths[0].Width;

            // REM MH
            double remMH = activity.BudgetMHs - activity.EarnMHsCalc;
            DrawRightText(graphics, remMH.ToString("N2"), _dataFont, x, y, _zone3ColumnWidths[1].Width, rowHeight);
            graphics.DrawLine(ThinPen, x + _zone3ColumnWidths[1].Width, y, x + _zone3ColumnWidths[1].Width, y + rowHeight);
            x += _zone3ColumnWidths[1].Width;

            // CUR QTY
            DrawRightText(graphics, activity.EarnQtyEntry.ToString("N2"), _dataFont, x, y, _zone3ColumnWidths[2].Width, rowHeight);
            graphics.DrawLine(ThinPen, x + _zone3ColumnWidths[2].Width, y, x + _zone3ColumnWidths[2].Width, y + rowHeight);
            x += _zone3ColumnWidths[2].Width;

            // CUR %
            DrawRightText(graphics, activity.PercentEntry.ToString("N2") + "%", _dataFont, x, y, _zone3ColumnWidths[3].Width, rowHeight);
            graphics.DrawLine(ThinPen, x + _zone3ColumnWidths[3].Width, y, x + _zone3ColumnWidths[3].Width, y + rowHeight);
            x += _zone3ColumnWidths[3].Width;

            // DONE checkbox (empty)
            DrawCheckbox(graphics, x, y, _zone3ColumnWidths[4].Width, rowHeight);
            graphics.DrawLine(ThinPen, x + _zone3ColumnWidths[4].Width, y, x + _zone3ColumnWidths[4].Width, y + rowHeight);
            x += _zone3ColumnWidths[4].Width;

            // QTY Entry box (empty)
            DrawEntryBox(graphics, x, y, _zone3ColumnWidths[5].Width, rowHeight);
            graphics.DrawLine(ThinPen, x + _zone3ColumnWidths[5].Width, y, x + _zone3ColumnWidths[5].Width, y + rowHeight);
            x += _zone3ColumnWidths[5].Width;

            // % Entry box (empty)
            DrawEntryBox(graphics, x, y, _zone3ColumnWidths[6].Width, rowHeight);

            return y + rowHeight;
        }

        // Helper: Draw centered text in a cell
        private void DrawCenteredText(PdfGraphics graphics, string text, PdfFont font, float x, float y, float width, float height)
        {
            var size = font.MeasureString(text);
            float padding = ColumnPadding / 2;
            float textX = x + (width - size.Width) / 2;
            float textY = y + (height - size.Height) / 2;

            // Truncate if too wide
            if (size.Width > width - ColumnPadding)
            {
                text = TruncateText(text, font, width - ColumnPadding);
                textX = x + padding;
            }

            graphics.DrawString(text, font, BlackBrush, new PointF(textX, textY));
        }

        // Helper: Draw left-aligned text in a cell
        private void DrawLeftText(PdfGraphics graphics, string text, PdfFont font, float x, float y, float width, float height, bool truncate = false)
        {
            var size = font.MeasureString(text);
            float padding = ColumnPadding / 2;
            float textX = x + padding;
            float textY = y + (height - size.Height) / 2;

            if (truncate && size.Width > width - ColumnPadding)
            {
                text = TruncateText(text, font, width - ColumnPadding);
            }

            graphics.DrawString(text, font, BlackBrush, new PointF(textX, textY));
        }

        // Helper: Draw right-aligned text in a cell
        private void DrawRightText(PdfGraphics graphics, string text, PdfFont font, float x, float y, float width, float height)
        {
            var size = font.MeasureString(text);
            float padding = ColumnPadding / 2;
            float textX = x + width - size.Width - padding;
            float textY = y + (height - size.Height) / 2;

            graphics.DrawString(text, font, BlackBrush, new PointF(textX, textY));
        }

        // Helper: Draw empty checkbox
        private void DrawCheckbox(PdfGraphics graphics, float x, float y, float width, float height)
        {
            float boxSize = Math.Min(height - 4, 12);
            float boxX = x + (width - boxSize) / 2;
            float boxY = y + (height - boxSize) / 2;

            graphics.DrawRectangle(ThinPen, new RectangleF(boxX, boxY, boxSize, boxSize));
        }

        // Helper: Draw entry box for handwriting
        private void DrawEntryBox(PdfGraphics graphics, float x, float y, float width, float height)
        {
            float padding = 2;
            float boxWidth = width - padding * 2;
            float boxHeight = height - padding * 2;
            float boxX = x + padding;
            float boxY = y + padding;

            graphics.DrawRectangle(WhiteBrush, new RectangleF(boxX, boxY, boxWidth, boxHeight));
            graphics.DrawRectangle(ThinPen, new RectangleF(boxX, boxY, boxWidth, boxHeight));
        }

        // Helper: Calculate row height for an activity (considers description wrapping)
        private float CalculateRowHeight(Activity activity)
        {
            // Get description column width
            var descCol = _zone2ColumnWidths.FirstOrDefault(c => c.FieldName.Equals("Description", StringComparison.OrdinalIgnoreCase));
            if (descCol.Width == 0) return _dataRowHeight;

            string desc = GetFieldValue(activity, "Description") ?? "";
            if (string.IsNullOrEmpty(desc)) return _dataRowHeight;

            // Wrap text and count lines
            var lines = WrapText(desc, _descFont, descCol.Width - ColumnPadding);
            if (lines.Count <= 1) return _dataRowHeight;

            // Calculate height needed for multiple lines
            float neededHeight = (lines.Count * _lineHeight) + 4; // 4 for top/bottom padding
            return Math.Max(_dataRowHeight, neededHeight);
        }

        // Helper: Wrap text into multiple lines that fit within maxWidth
        private List<string> WrapText(string text, PdfFont font, float maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            {
                lines.Add(text ?? "");
                return lines;
            }

            // Check if text fits on one line
            if (font.MeasureString(text).Width <= maxWidth)
            {
                lines.Add(text);
                return lines;
            }

            // Split into words and wrap
            var words = text.Split(' ');
            string currentLine = "";

            foreach (var word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                float testWidth = font.MeasureString(testLine).Width;

                if (testWidth > maxWidth)
                {
                    // Current line is full, start new line
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        // Single word is too long, add it anyway
                        lines.Add(word);
                        currentLine = "";
                    }
                }
                else
                {
                    currentLine = testLine;
                }
            }

            // Add the last line
            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        // Helper: Draw multi-line text in a cell (for Description wrapping)
        private void DrawWrappedText(PdfGraphics graphics, string text, PdfFont font, float x, float y, float width, float height)
        {
            var lines = WrapText(text, font, width - ColumnPadding);
            float textY = y + 2; // Top padding

            foreach (var line in lines)
            {
                if (textY + _lineHeight > y + height - 2) break; // Don't overflow cell
                graphics.DrawString(line, font, BlackBrush, new PointF(x + ColumnPadding / 2, textY));
                textY += _lineHeight;
            }
        }

        // Helper: Truncate text with ellipsis
        private string TruncateText(string text, PdfFont font, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return text;

            const string ellipsis = "...";
            var ellipsisWidth = font.MeasureString(ellipsis).Width;

            if (font.MeasureString(text).Width <= maxWidth)
                return text;

            for (int i = text.Length - 1; i > 0; i--)
            {
                string truncated = text.Substring(0, i) + ellipsis;
                if (font.MeasureString(truncated).Width <= maxWidth)
                    return truncated;
            }

            return ellipsis;
        }

        // Helper: Get field value from Activity using reflection
        private string? GetFieldValue(Activity activity, string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return null;

            try
            {
                var prop = typeof(Activity).GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    var value = prop.GetValue(activity);
                    return value?.ToString();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, $"ProgressBookPdfGenerator.GetFieldValue({fieldName})");
            }

            return null;
        }

        // Helper: Get display name for column
        private string GetColumnDisplayName(string fieldName)
        {
            // Map field names to shorter display names where appropriate
            return fieldName switch
            {
                "ROCStep" => "ROC",
                "Description" => "DESC",
                "PhaseCode" => "PHASE",
                "PhaseCategory" => "CATG",
                "CompType" => "COMP",
                "WorkPackage" => "WP",
                "ProjectID" => "PROJ",
                "UniqueID" => "UID",
                _ => fieldName.ToUpper()
            };
        }

        // Helper: Load image from resource or file
        private PdfImage? LoadImage(string imagePath)
        {
            try
            {
                if (imagePath.StartsWith("pack://"))
                {
                    var uri = new Uri(imagePath);
                    var streamInfo = Application.GetResourceStream(uri);
                    if (streamInfo?.Stream != null)
                    {
                        return new PdfBitmap(streamInfo.Stream);
                    }
                }
                else if (File.Exists(imagePath))
                {
                    return new PdfBitmap(imagePath);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, $"ProgressBookPdfGenerator.LoadImage({imagePath})");
            }

            return null;
        }
    }

    // Helper class for grouping activities
    internal class ActivityGroup
    {
        public string FieldName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int Level { get; set; }
        public List<Activity> Activities { get; set; } = new();
        public List<ActivityGroup> SubGroups { get; set; } = new();

        // Summary values
        public double TotalQty { get; set; }
        public double TotalMH { get; set; }
        public double RemainingQty { get; set; }
        public double RemainingMH { get; set; }
    }
}
