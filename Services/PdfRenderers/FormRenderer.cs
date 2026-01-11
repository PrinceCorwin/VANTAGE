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
        private const float BaseBodyFontSize = 10f;
        private const float BaseHeaderFontSize = 10f;

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

                // Calculate adjusted font sizes
                float fontScale = 1 + structure.FontSizeAdjustPercent / 100f;
                float bodyFontSize = BaseBodyFontSize * fontScale;
                float headerFontSize = BaseHeaderFontSize * fontScale;
                var adjustedBodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, bodyFontSize, PdfFontStyle.Regular);
                var adjustedHeaderFont = new PdfStandardFont(PdfFontFamily.Helvetica, headerFontSize, PdfFontStyle.Bold);

                // Calculate row height - scales with font, then applies increase percent
                float rowHeight = DefaultRowHeight * fontScale * (1 + structure.RowHeightIncreasePercent / 100f);

                // Draw column headers
                y = DrawColumnHeaders(graphics, structure, y, adjustedHeaderFont);

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
                        y = DrawColumnHeaders(graphics, structure, y, adjustedHeaderFont);
                    }

                    // Draw section header
                    y = DrawSectionHeader(graphics, section.Name, y, adjustedHeaderFont);

                    // Draw items in this section
                    foreach (var item in section.Items)
                    {
                        y = DrawDataRow(graphics, structure, item, y, rowHeight, adjustedBodyFont);
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
        private float DrawColumnHeaders(PdfGraphics graphics, FormStructure structure, float y, PdfFont headerFont)
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
                var textSize = headerFont.MeasureString(column.Name);
                float textX = x + (colWidth - textSize.Width) / 2;
                float textY = y + (headerHeight - textSize.Height) / 2;
                graphics.DrawString(column.Name, headerFont, BlackBrush, new PointF(textX, textY));

                x += colWidth;
            }

            return y + headerHeight;
        }

        // Draw section header (spans all columns)
        private float DrawSectionHeader(PdfGraphics graphics, string sectionName, float y, PdfFont headerFont)
        {
            // Draw background
            graphics.DrawRectangle(SectionGrayBrush, new RectangleF(MarginLeft, y, ContentWidth, SectionHeaderHeight));

            // Draw borders (top and bottom only)
            graphics.DrawLine(NormalPen, MarginLeft, y, MarginLeft + ContentWidth, y);
            graphics.DrawLine(NormalPen, MarginLeft, y + SectionHeaderHeight, MarginLeft + ContentWidth, y + SectionHeaderHeight);

            // Draw text
            float textY = y + (SectionHeaderHeight - headerFont.Size) / 2;
            graphics.DrawString(sectionName, headerFont, BlackBrush, new PointF(MarginLeft + 5f, textY));

            return y + SectionHeaderHeight;
        }

        // Draw a data row
        private float DrawDataRow(PdfGraphics graphics, FormStructure structure, string itemText, float y, float rowHeight, PdfFont bodyFont)
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
                    float textY = y + (rowHeight - bodyFont.Size) / 2;
                    graphics.DrawString(itemText, bodyFont, BlackBrush, new PointF(x + 5f, textY));
                    isFirstColumn = false;
                }

                x += colWidth;
                isFirstColumn = false;
            }

            return y + rowHeight;
        }
    }
}
