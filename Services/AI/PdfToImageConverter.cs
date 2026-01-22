using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PdfiumViewer;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // Converts PDF pages to PNG images for Claude Vision API processing
    public static class PdfToImageConverter
    {
        private const int DefaultDpi = 200;

        // Get the number of pages in a PDF file
        public static int GetPageCount(string pdfPath)
        {
            try
            {
                using var document = PdfDocument.Load(pdfPath);
                return document.PageCount;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PdfToImageConverter.GetPageCount");
                return 0;
            }
        }

        // Convert a single page to a PNG image (returns byte array)
        // pageIndex is 0-based
        public static byte[]? ConvertPageToImage(string pdfPath, int pageIndex, int dpi = DefaultDpi)
        {
            try
            {
                using var document = PdfDocument.Load(pdfPath);

                if (pageIndex < 0 || pageIndex >= document.PageCount)
                {
                    AppLogger.Warning($"Invalid page index {pageIndex} for PDF with {document.PageCount} pages",
                        "PdfToImageConverter.ConvertPageToImage");
                    return null;
                }

                // Get page size and calculate image dimensions
                var pageSize = document.PageSizes[pageIndex];
                int width = (int)(pageSize.Width * dpi / 72.0);
                int height = (int)(pageSize.Height * dpi / 72.0);

                // Render the page to an image
                using var image = document.Render(pageIndex, width, height, dpi, dpi, false);

                // Convert to PNG byte array
                using var ms = new MemoryStream();
                image.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PdfToImageConverter.ConvertPageToImage");
                return null;
            }
        }

        // Convert all pages to PNG images
        public static List<byte[]> ConvertAllPages(string pdfPath, int dpi = DefaultDpi)
        {
            var results = new List<byte[]>();

            try
            {
                using var document = PdfDocument.Load(pdfPath);

                for (int i = 0; i < document.PageCount; i++)
                {
                    var imageBytes = ConvertPageToImageInternal(document, i, dpi);
                    if (imageBytes != null)
                    {
                        results.Add(imageBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PdfToImageConverter.ConvertAllPages");
            }

            return results;
        }

        // Internal method to convert a page using an already-opened document
        private static byte[]? ConvertPageToImageInternal(PdfDocument document, int pageIndex, int dpi)
        {
            try
            {
                var pageSize = document.PageSizes[pageIndex];
                int width = (int)(pageSize.Width * dpi / 72.0);
                int height = (int)(pageSize.Height * dpi / 72.0);

                using var image = document.Render(pageIndex, width, height, dpi, dpi, false);

                using var ms = new MemoryStream();
                image.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to convert page {pageIndex}: {ex.Message}",
                    "PdfToImageConverter.ConvertPageToImageInternal");
                return null;
            }
        }

        // Get the media type string for a file based on extension
        public static string GetMediaType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/png"  // Default for PDF-converted images
            };
        }

        // Check if a file is a supported image format (not PDF)
        public static bool IsImageFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp";
        }

        // Check if a file is a PDF
        public static bool IsPdfFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".pdf";
        }
    }
}
