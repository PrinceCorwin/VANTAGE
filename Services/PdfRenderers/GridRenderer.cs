using System;
using System.Drawing;
using System.Text.Json;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Services.PdfRenderers
{
    // Renderer for Grid type templates (header + column headers + N empty rows)
    public class GridRenderer : BaseRenderer
    {
        private const float DefaultRowHeight = 22f;
        private const float HeaderHeight = 18f;

        public override PdfDocument Render(string structureJson, TokenContext context, string? logoPath = null)
        {
            var document = CreateDocument();

            try
            {
                var structure = JsonSerializer.Deserialize<GridStructure>(structureJson);
                if (structure == null)
                {
                    AppLogger.Warning("Failed to parse GridStructure JSON", "GridRenderer.Render");
                    return document;
                }

                var page = document.Pages.Add();
                var graphics = page.Graphics;

                // Resolve footer text once for consistent measurement and rendering
                string? resolvedFooter = string.IsNullOrEmpty(structure.FooterText)
                    ? null
                    : TokenResolver.Resolve(structure.FooterText, context);
                float footerReserve = GetFooterReservedHeight(resolvedFooter);

                // Render header
                string title = TokenResolver.Resolve(structure.Title, context);
                float y = RenderHeader(page, context, title, logoPath);

                y += 10f;

                // Calculate adjusted font size (uses template's base font size)
                float fontScale = 1 + structure.FontSizeAdjustPercent / 100f;
                float headerFontSize = structure.BaseHeaderFontSize * fontScale;
                var adjustedHeaderFont = new PdfStandardFont(PdfFontFamily.Helvetica, headerFontSize, PdfFontStyle.Bold);

                // Calculate row height - scales proportionally with font size ratio
                float fontRatio = structure.BaseHeaderFontSize / 9f;  // Ratio vs default 9pt
                float rowHeight = DefaultRowHeight * fontRatio * fontScale * (1 + structure.RowHeightIncreasePercent / 100f);

                // Draw column headers
                y = DrawColumnHeaders(graphics, structure, y, adjustedHeaderFont);

                // Calculate how many rows fit on a page (use dynamic footer reserve)
                float availableHeight = PageHeight - MarginBottom - footerReserve - y;
                int rowsPerPage = (int)(availableHeight / rowHeight);
                int rowsDrawn = 0;

                // Draw empty rows
                for (int row = 0; row < structure.RowCount; row++)
                {
                    // Check if we need a new page
                    if (rowsDrawn >= rowsPerPage)
                    {
                        // Render footer on current page if exists
                        if (resolvedFooter != null)
                        {
                            RenderFooter(page, resolvedFooter);
                        }

                        page = document.Pages.Add();
                        graphics = page.Graphics;
                        y = MarginTop;

                        // Redraw column headers on new page
                        y = DrawColumnHeaders(graphics, structure, y, adjustedHeaderFont);

                        // Recalculate rows per page
                        availableHeight = PageHeight - MarginBottom - footerReserve - y;
                        rowsPerPage = (int)(availableHeight / rowHeight);
                        rowsDrawn = 0;
                    }

                    // Draw empty row
                    y = DrawEmptyRow(graphics, structure, y, rowHeight);
                    rowsDrawn++;
                }

                // Render footer if present
                if (resolvedFooter != null)
                {
                    RenderFooter(page, resolvedFooter);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "GridRenderer.Render");
            }

            return document;
        }

        // Draw column headers row
        private float DrawColumnHeaders(PdfGraphics graphics, GridStructure structure, float y, PdfFont headerFont)
        {
            float x = MarginLeft;

            // Draw header background
            graphics.DrawRectangle(LightGrayBrush, new RectangleF(MarginLeft, y, ContentWidth, HeaderHeight));

            // Draw each column header
            foreach (var column in structure.Columns)
            {
                float colWidth = ContentWidth * (column.WidthPercent / 100f);

                // Draw border
                graphics.DrawRectangle(NormalPen, new RectangleF(x, y, colWidth, HeaderHeight));

                // Draw text (centered)
                var textSize = headerFont.MeasureString(column.Name);
                float textX = x + (colWidth - textSize.Width) / 2;
                float textY = y + (HeaderHeight - textSize.Height) / 2;

                // If text is too wide, left-align with padding
                if (textSize.Width > colWidth - 4)
                {
                    textX = x + 2f;
                }

                graphics.DrawString(column.Name, headerFont, BlackBrush, new PointF(textX, textY));

                x += colWidth;
            }

            return y + HeaderHeight;
        }

        // Draw an empty data row
        private float DrawEmptyRow(PdfGraphics graphics, GridStructure structure, float y, float rowHeight)
        {
            float x = MarginLeft;

            foreach (var column in structure.Columns)
            {
                float colWidth = ContentWidth * (column.WidthPercent / 100f);

                // Draw cell border (cells are empty for user fill-in)
                graphics.DrawRectangle(ThinPen, new RectangleF(x, y, colWidth, rowHeight));

                x += colWidth;
            }

            return y + rowHeight;
        }
    }
}
