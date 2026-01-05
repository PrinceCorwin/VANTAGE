using System;
using System.Drawing;
using System.Text.Json;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Services.PdfRenderers
{
    // Renderer for Form type templates (header + sections with items in table format)
    public class FormRenderer : BaseRenderer
    {
        private const float DefaultRowHeight = 22f;
        private const float SectionHeaderHeight = 20f;

        public override PdfDocument Render(string structureJson, TokenContext context, string? logoPath = null)
        {
            var document = CreateDocument();

            try
            {
                var structure = JsonSerializer.Deserialize<FormStructure>(structureJson);
                if (structure == null)
                {
                    AppLogger.Warning("Failed to parse FormStructure JSON", "FormRenderer.Render");
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

                // Draw sections and their items
                foreach (var section in structure.Sections)
                {
                    // Check if we need a new page
                    float sectionHeight = SectionHeaderHeight + (section.Items.Count * rowHeight);
                    if (y + sectionHeight > PageHeight - MarginBottom - 50f)
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
                    }

                    // Draw section header
                    y = DrawSectionHeader(graphics, section.Name, y);

                    // Draw items in this section
                    foreach (var item in section.Items)
                    {
                        y = DrawDataRow(graphics, structure, item, y, rowHeight);
                    }
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
                AppLogger.Error(ex, "FormRenderer.Render");
            }

            return document;
        }

        // Draw column headers row
        private float DrawColumnHeaders(PdfGraphics graphics, FormStructure structure, float y)
        {
            float headerHeight = 18f;
            float x = MarginLeft;

            // Draw header background
            graphics.DrawRectangle(LightGrayBrush, new RectangleF(MarginLeft, y, ContentWidth, headerHeight));

            // Draw each column header
            foreach (var column in structure.Columns)
            {
                float colWidth = ContentWidth * (column.WidthPercent / 100f);

                // Draw border
                graphics.DrawRectangle(NormalPen, new RectangleF(x, y, colWidth, headerHeight));

                // Draw text (centered)
                var textSize = HeaderFont.MeasureString(column.Name);
                float textX = x + (colWidth - textSize.Width) / 2;
                float textY = y + (headerHeight - textSize.Height) / 2;
                graphics.DrawString(column.Name, HeaderFont, BlackBrush, new PointF(textX, textY));

                x += colWidth;
            }

            return y + headerHeight;
        }

        // Draw section header (spans all columns)
        private float DrawSectionHeader(PdfGraphics graphics, string sectionName, float y)
        {
            // Draw background
            graphics.DrawRectangle(SectionGrayBrush, new RectangleF(MarginLeft, y, ContentWidth, SectionHeaderHeight));

            // Draw borders (top and bottom only)
            graphics.DrawLine(NormalPen, MarginLeft, y, MarginLeft + ContentWidth, y);
            graphics.DrawLine(NormalPen, MarginLeft, y + SectionHeaderHeight, MarginLeft + ContentWidth, y + SectionHeaderHeight);

            // Draw text
            float textY = y + (SectionHeaderHeight - HeaderFont.Size) / 2;
            graphics.DrawString(sectionName, HeaderFont, BlackBrush, new PointF(MarginLeft + 5f, textY));

            return y + SectionHeaderHeight;
        }

        // Draw a data row
        private float DrawDataRow(PdfGraphics graphics, FormStructure structure, string itemText, float y, float rowHeight)
        {
            float x = MarginLeft;
            bool isFirstColumn = true;

            foreach (var column in structure.Columns)
            {
                float colWidth = ContentWidth * (column.WidthPercent / 100f);

                // Draw cell border
                graphics.DrawRectangle(ThinPen, new RectangleF(x, y, colWidth, rowHeight));

                // First column gets the item text, other columns are empty (for user fill-in)
                if (isFirstColumn && !string.IsNullOrEmpty(itemText))
                {
                    float textY = y + (rowHeight - BodyFont.Size) / 2;
                    graphics.DrawString(itemText, BodyFont, BlackBrush, new PointF(x + 5f, textY));
                    isFirstColumn = false;
                }

                x += colWidth;
                isFirstColumn = false;
            }

            return y + rowHeight;
        }
    }
}
