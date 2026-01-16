using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Services.PdfRenderers
{
    // Renderer for Cover type templates (header + centered image + optional footer)
    public class CoverRenderer : BaseRenderer
    {
        private const string DefaultCoverImagePath = "pack://application:,,,/Images/CoverPic.png";

        public override PdfDocument Render(string structureJson, TokenContext context, string? logoPath = null)
        {
            var document = CreateDocument();

            try
            {
                var structure = JsonSerializer.Deserialize<CoverStructure>(structureJson);
                if (structure == null)
                {
                    AppLogger.Warning("Failed to parse CoverStructure JSON", "CoverRenderer.Render");
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

                y += 20f; // Add some spacing after header

                // Load and draw the cover image
                PdfImage? coverImage = LoadCoverImage(structure.ImagePath);
                if (coverImage != null)
                {
                    // Calculate image dimensions based on imageWidthPercent
                    float maxWidth = ContentWidth * (structure.ImageWidthPercent / 100f);
                    float maxHeight = PageHeight - y - MarginBottom - footerReserve - 20f; // Leave room for footer

                    // Scale to fit while maintaining aspect ratio
                    float scale = Math.Min(maxWidth / coverImage.Width, maxHeight / coverImage.Height);
                    float drawWidth = coverImage.Width * scale;
                    float drawHeight = coverImage.Height * scale;

                    // Center horizontally
                    float imageX = MarginLeft + (ContentWidth - drawWidth) / 2;

                    // Center vertically in remaining space (above footer)
                    float remainingHeight = PageHeight - y - MarginBottom - footerReserve;
                    float imageY = y + (remainingHeight - drawHeight) / 2;

                    graphics.DrawImage(coverImage, imageX, imageY, drawWidth, drawHeight);
                }

                // Render footer if present
                if (resolvedFooter != null)
                {
                    RenderFooter(page, resolvedFooter);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "CoverRenderer.Render");
            }

            return document;
        }

        // Load cover image from path or use default
        private PdfImage? LoadCoverImage(string? imagePath)
        {
            try
            {
                // If path is null or empty, use default cover image
                if (string.IsNullOrEmpty(imagePath))
                {
                    var uri = new Uri(DefaultCoverImagePath);
                    var streamInfo = Application.GetResourceStream(uri);
                    if (streamInfo?.Stream != null)
                    {
                        return new PdfBitmap(streamInfo.Stream);
                    }
                    return null;
                }

                // Try to load from file path
                if (File.Exists(imagePath))
                {
                    return new PdfBitmap(imagePath);
                }

                // If custom path doesn't exist, fall back to default
                AppLogger.Warning($"Cover image not found at {imagePath}, using default", "CoverRenderer.LoadCoverImage");
                var defaultUri = new Uri(DefaultCoverImagePath);
                var defaultStreamInfo = Application.GetResourceStream(defaultUri);
                if (defaultStreamInfo?.Stream != null)
                {
                    return new PdfBitmap(defaultStreamInfo.Stream);
                }

                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, $"CoverRenderer.LoadCoverImage({imagePath})");
                return null;
            }
        }
    }
}
