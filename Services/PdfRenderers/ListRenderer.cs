using System;
using System.Drawing;
using System.Text.Json;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Services.PdfRenderers
{
    // Renderer for List type templates (header + text items + optional footer)
    public class ListRenderer : BaseRenderer
    {
        private const float DefaultLineHeight = 18f;
        private const float BaseBodyFontSize = 10f;

        public override PdfDocument Render(string structureJson, TokenContext context, string? logoPath = null)
        {
            var document = CreateDocument();

            try
            {
                var structure = JsonSerializer.Deserialize<ListStructure>(structureJson);
                if (structure == null)
                {
                    AppLogger.Warning("Failed to parse ListStructure JSON", "ListRenderer.Render");
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

                y += 15f; // Add spacing after header

                // Calculate adjusted font size and line height
                float fontScale = 1 + structure.FontSizeAdjustPercent / 100f;
                float bodyFontSize = BaseBodyFontSize * fontScale;
                float lineHeight = DefaultLineHeight * fontScale;
                var adjustedBodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, bodyFontSize, PdfFontStyle.Regular);

                // Render each item
                foreach (var item in structure.Items)
                {
                    // Check if we need a new page (use dynamic footer reserve)
                    if (y > PageHeight - MarginBottom - footerReserve)
                    {
                        // Render footer on current page if exists
                        if (resolvedFooter != null)
                        {
                            RenderFooter(page, resolvedFooter);
                        }

                        page = document.Pages.Add();
                        graphics = page.Graphics;
                        y = MarginTop;
                    }

                    // Empty string creates a blank line (vertical spacing)
                    if (string.IsNullOrEmpty(item))
                    {
                        y += lineHeight / 2;
                        continue;
                    }

                    // "---" creates a horizontal line separator
                    if (item == "---")
                    {
                        y += 4f;
                        var pen = new PdfPen(PdfBrushes.Gray, 0.5f);
                        float lineWidth = PageWidth - MarginLeft - MarginRight;
                        graphics.DrawLine(pen, new PointF(MarginLeft, y), new PointF(MarginLeft + lineWidth, y));
                        y += 8f;
                        continue;
                    }

                    // Resolve tokens in the item text
                    string resolvedText = TokenResolver.Resolve(item, context);

                    // Draw the text
                    graphics.DrawString(resolvedText, adjustedBodyFont, BlackBrush, new PointF(MarginLeft, y));
                    y += lineHeight;
                }

                // Render footer if present
                if (resolvedFooter != null)
                {
                    RenderFooter(page, resolvedFooter);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ListRenderer.Render");
            }

            return document;
        }
    }
}
