using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Services.PdfRenderers
{
    // Renderer for Drawings type templates (drawing images from local folder or Procore)
    public class DrawingsRenderer : BaseRenderer
    {
        private const float CaptionHeight = 16f;
        private const float ImagePadding = 10f;

        public override PdfDocument Render(string structureJson, TokenContext context, string? logoPath = null)
        {
            var document = CreateDocument();

            try
            {
                var structure = JsonSerializer.Deserialize<DrawingsStructure>(structureJson);
                if (structure == null)
                {
                    AppLogger.Warning("Failed to parse DrawingsStructure JSON", "DrawingsRenderer.Render");
                    return document;
                }

                // Load drawing files based on source
                List<string> drawingFiles = LoadDrawingFiles(structure, context);

                if (drawingFiles.Count == 0)
                {
                    // Render single page with "No drawings found" message
                    var page = document.Pages.Add();
                    string title = TokenResolver.Resolve(structure.Title, context);
                    float y = RenderHeader(page, context, title, logoPath);
                    y += 30f;
                    page.Graphics.DrawString("No drawings found", BodyFont, BlackBrush, new PointF(MarginLeft, y));
                    return document;
                }

                // Render drawings based on imagesPerPage setting
                switch (structure.ImagesPerPage)
                {
                    case 1:
                        RenderOnePerPage(document, structure, context, drawingFiles, logoPath);
                        break;
                    case 2:
                        RenderTwoPerPage(document, structure, context, drawingFiles, logoPath);
                        break;
                    case 4:
                        RenderFourPerPage(document, structure, context, drawingFiles, logoPath);
                        break;
                    default:
                        RenderOnePerPage(document, structure, context, drawingFiles, logoPath);
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DrawingsRenderer.Render");
            }

            return document;
        }

        // Load drawing files from local folder
        private List<string> LoadDrawingFiles(DrawingsStructure structure, TokenContext context)
        {
            var files = new List<string>();

            if (structure.Source == "Local" && !string.IsNullOrEmpty(structure.FolderPath))
            {
                // Resolve tokens in folder path
                string resolvedPath = TokenResolver.Resolve(structure.FolderPath, context);

                if (Directory.Exists(resolvedPath))
                {
                    // Parse file extensions
                    var extensions = structure.FileExtensions.Split(',')
                        .Select(e => e.Trim().TrimStart('*'))
                        .ToArray();

                    // Get all matching files
                    foreach (var ext in extensions)
                    {
                        files.AddRange(Directory.GetFiles(resolvedPath, "*" + ext, SearchOption.TopDirectoryOnly));
                    }

                    // Sort by filename
                    files = files.OrderBy(f => Path.GetFileName(f)).ToList();
                }
                else
                {
                    AppLogger.Warning($"Drawings folder not found: {resolvedPath}", "DrawingsRenderer.LoadDrawingFiles");
                }
            }
            else if (structure.Source == "Procore")
            {
                // Procore integration - to be implemented later
                AppLogger.Info("Procore drawings source not yet implemented", "DrawingsRenderer.LoadDrawingFiles");
            }

            return files;
        }

        // Render one drawing per page
        private void RenderOnePerPage(PdfDocument document, DrawingsStructure structure, TokenContext context,
            List<string> drawingFiles, string? logoPath)
        {
            foreach (var filePath in drawingFiles)
            {
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                // Render header
                string title = TokenResolver.Resolve(structure.Title, context);
                float y = RenderHeader(page, context, title, logoPath);
                y += ImagePadding;

                // Calculate available space for image
                float captionSpace = structure.ShowCaptions ? CaptionHeight : 0;
                float availableWidth = ContentWidth;
                float availableHeight = PageHeight - y - MarginBottom - captionSpace - ImagePadding;

                // Load and draw the image
                DrawImageInArea(graphics, filePath, MarginLeft, y, availableWidth, availableHeight,
                    structure.ShowCaptions);

                // Render footer if present
                if (!string.IsNullOrEmpty(structure.FooterText))
                {
                    RenderFooter(page, TokenResolver.Resolve(structure.FooterText, context));
                }
            }
        }

        // Render two drawings per page (stacked vertically)
        private void RenderTwoPerPage(PdfDocument document, DrawingsStructure structure, TokenContext context,
            List<string> drawingFiles, string? logoPath)
        {
            for (int i = 0; i < drawingFiles.Count; i += 2)
            {
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                // Render header
                string title = TokenResolver.Resolve(structure.Title, context);
                float y = RenderHeader(page, context, title, logoPath);
                y += ImagePadding;

                // Calculate available space for each image
                float captionSpace = structure.ShowCaptions ? CaptionHeight : 0;
                float totalAvailableHeight = PageHeight - y - MarginBottom - ImagePadding;
                float imageAreaHeight = (totalAvailableHeight - ImagePadding) / 2 - captionSpace;

                // Draw first image
                DrawImageInArea(graphics, drawingFiles[i], MarginLeft, y, ContentWidth, imageAreaHeight,
                    structure.ShowCaptions);

                // Draw second image if exists
                if (i + 1 < drawingFiles.Count)
                {
                    float y2 = y + imageAreaHeight + captionSpace + ImagePadding;
                    DrawImageInArea(graphics, drawingFiles[i + 1], MarginLeft, y2, ContentWidth, imageAreaHeight,
                        structure.ShowCaptions);
                }

                // Render footer if present
                if (!string.IsNullOrEmpty(structure.FooterText))
                {
                    RenderFooter(page, TokenResolver.Resolve(structure.FooterText, context));
                }
            }
        }

        // Render four drawings per page (2x2 grid)
        private void RenderFourPerPage(PdfDocument document, DrawingsStructure structure, TokenContext context,
            List<string> drawingFiles, string? logoPath)
        {
            for (int i = 0; i < drawingFiles.Count; i += 4)
            {
                var page = document.Pages.Add();
                var graphics = page.Graphics;

                // Render header
                string title = TokenResolver.Resolve(structure.Title, context);
                float y = RenderHeader(page, context, title, logoPath);
                y += ImagePadding;

                // Calculate available space for each image
                float captionSpace = structure.ShowCaptions ? CaptionHeight : 0;
                float totalAvailableHeight = PageHeight - y - MarginBottom - ImagePadding;
                float imageAreaHeight = (totalAvailableHeight - ImagePadding) / 2 - captionSpace;
                float imageAreaWidth = (ContentWidth - ImagePadding) / 2;

                // Draw images in 2x2 grid
                float[] xPositions = { MarginLeft, MarginLeft + imageAreaWidth + ImagePadding };
                float[] yPositions = { y, y + imageAreaHeight + captionSpace + ImagePadding };

                for (int j = 0; j < 4 && i + j < drawingFiles.Count; j++)
                {
                    int col = j % 2;
                    int row = j / 2;
                    DrawImageInArea(graphics, drawingFiles[i + j], xPositions[col], yPositions[row],
                        imageAreaWidth, imageAreaHeight, structure.ShowCaptions);
                }

                // Render footer if present
                if (!string.IsNullOrEmpty(structure.FooterText))
                {
                    RenderFooter(page, TokenResolver.Resolve(structure.FooterText, context));
                }
            }
        }

        // Draw an image scaled to fit within the specified area, with optional caption
        private void DrawImageInArea(PdfGraphics graphics, string filePath, float x, float y,
            float maxWidth, float maxHeight, bool showCaption)
        {
            try
            {
                PdfImage? image = LoadDrawingImage(filePath);
                if (image == null)
                {
                    // Draw placeholder for missing image
                    graphics.DrawRectangle(ThinPen, new RectangleF(x, y, maxWidth, maxHeight));
                    graphics.DrawString($"[Could not load: {Path.GetFileName(filePath)}]",
                        SmallFont, BlackBrush, new PointF(x + 5, y + 5));
                    return;
                }

                // Calculate scaled dimensions to fit while maintaining aspect ratio
                float scale = Math.Min(maxWidth / image.Width, maxHeight / image.Height);
                float drawWidth = image.Width * scale;
                float drawHeight = image.Height * scale;

                // Center the image in the available space
                float imageX = x + (maxWidth - drawWidth) / 2;
                float imageY = y + (maxHeight - drawHeight) / 2;

                // Draw border around image area
                graphics.DrawRectangle(ThinPen, new RectangleF(imageX - 1, imageY - 1, drawWidth + 2, drawHeight + 2));

                // Draw the image
                graphics.DrawImage(image, imageX, imageY, drawWidth, drawHeight);

                // Draw caption if enabled
                if (showCaption)
                {
                    string caption = Path.GetFileName(filePath);
                    float captionY = y + maxHeight + 2;

                    // Truncate caption if too long
                    var captionSize = SmallFont.MeasureString(caption);
                    if (captionSize.Width > maxWidth)
                    {
                        while (captionSize.Width > maxWidth - 10 && caption.Length > 10)
                        {
                            caption = caption.Substring(0, caption.Length - 4) + "...";
                            captionSize = SmallFont.MeasureString(caption);
                        }
                    }

                    // Center caption under image
                    float captionX = x + (maxWidth - captionSize.Width) / 2;
                    graphics.DrawString(caption, SmallFont, BlackBrush, new PointF(captionX, captionY));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, $"DrawingsRenderer.DrawImageInArea({filePath})");
                graphics.DrawString($"[Error: {Path.GetFileName(filePath)}]", SmallFont, BlackBrush, new PointF(x + 5, y + 5));
            }
        }

        // Load a drawing image from file
        private PdfImage? LoadDrawingImage(string filePath)
        {
            try
            {
                string extension = Path.GetExtension(filePath).ToLowerInvariant();

                // For PDF files, we'd need to extract pages as images - skip for now
                if (extension == ".pdf")
                {
                    AppLogger.Warning($"PDF drawing files not yet supported: {filePath}", "DrawingsRenderer.LoadDrawingImage");
                    return null;
                }

                // Load image file
                if (File.Exists(filePath))
                {
                    return new PdfBitmap(filePath);
                }

                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, $"DrawingsRenderer.LoadDrawingImage({filePath})");
                return null;
            }
        }
    }
}
