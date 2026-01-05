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

                // Render header
                string title = TokenResolver.Resolve(structure.Title, context);
                float y = RenderHeader(page, context, title, logoPath);

                y += 10f;

                // Calculate row height with increase percent
                float rowHeight = DefaultRowHeight * (1 + structure.RowHeightIncreasePercent / 100f);

                // Draw column headers
                y = DrawColumnHeaders(graphics, structure, y);

                // Calculate how many rows fit on a page
                float availableHeight = PageHeight - MarginBottom - 50f - y;
                int rowsPerPage = (int)(availableHeight / rowHeight);
                int rowsDrawn = 0;

                // Draw empty rows
                for (int row = 0; row < structure.RowCount; row++)
                {
                    // Check if we need a new page
                    if (rowsDrawn >= rowsPerPage)
                    {
                        // Render footer on current page if exists
                        if (!string.IsNullOrEmpty(structure.FooterText))
                        {
                            RenderFooter(page, TokenResolver.Resolve(structure.FooterText, context));
                        }

                        page = document.Pages.Add();
                        graphics = page.Graphics;
                        y = MarginTop;

                        // Redraw column headers on new page
                        y = DrawColumnHeaders(graphics, structure, y);

                        // Recalculate rows per page
                        availableHeight = PageHeight - MarginBottom - 50f - y;
                        rowsPerPage = (int)(availableHeight / rowHeight);
                        rowsDrawn = 0;
                    }

                    // Draw empty row
                    y = DrawEmptyRow(graphics, structure, y, rowHeight);
                    rowsDrawn++;
                }

                // Render footer if present
                if (!string.IsNullOrEmpty(structure.FooterText))
                {
                    string footerText = TokenResolver.Resolve(structure.FooterText, context);
                    RenderFooter(page, footerText);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "GridRenderer.Render");
            }

            return document;
        }

        // Draw column headers row
        private float DrawColumnHeaders(PdfGraphics graphics, GridStructure structure, float y)
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
                var textSize = SmallBoldFont.MeasureString(column.Name);
                float textX = x + (colWidth - textSize.Width) / 2;
                float textY = y + (HeaderHeight - textSize.Height) / 2;

                // If text is too wide, left-align with padding
                if (textSize.Width > colWidth - 4)
                {
                    textX = x + 2f;
                }

                graphics.DrawString(column.Name, SmallBoldFont, BlackBrush, new PointF(textX, textY));

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
