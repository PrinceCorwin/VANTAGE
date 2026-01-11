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
        private const float LineHeight = 18f;

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

                // Render header
                string title = TokenResolver.Resolve(structure.Title, context);
                float y = RenderHeader(page, context, title, logoPath);

                y += 15f; // Add spacing after header

                // Render each item
                foreach (var item in structure.Items)
                {
                    // Check if we need a new page
                    if (y > PageHeight - MarginBottom - 50f)
                    {
                        page = document.Pages.Add();
                        graphics = page.Graphics;
                        y = MarginTop;
                    }

                    // Empty string creates a blank line (vertical spacing)
                    if (string.IsNullOrEmpty(item))
                    {
                        y += LineHeight / 2;
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
                    graphics.DrawString(resolvedText, BodyFont, BlackBrush, new PointF(MarginLeft, y));
                    y += LineHeight;
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
                AppLogger.Error(ex, "ListRenderer.Render");
            }

            return document;
        }
    }
}
