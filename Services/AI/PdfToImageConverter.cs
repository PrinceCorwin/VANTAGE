using System;
using System.Collections.Generic;
using System.IO;
using Syncfusion.Windows.PdfViewer;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // Converts PDF pages to PNG images for Claude Vision API processing
    public static class PdfToImageConverter
    {
        // Get the number of pages in a PDF file
        public static int GetPageCount(string pdfPath)
        {
            try
            {
                using var converter = new Syncfusion.PdfToImageConverter.PdfToImageConverter();
                using var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
                converter.Load(fileStream);
                return converter.PageCount;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PdfToImageConverter.GetPageCount");
                return 0;
            }
        }

        // Convert a single page to a PNG image (returns byte array)
        // pageIndex is 0-based, higher DPI improves AI handwriting recognition
        public static byte[]? ConvertPageToImage(string pdfPath, int pageIndex, int dpi = 300)
        {
            try
            {
                using var converter = new Syncfusion.PdfToImageConverter.PdfToImageConverter();
                using var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
                converter.Load(fileStream);

                AppLogger.Info($"PDF loaded: {converter.PageCount} pages", "PdfToImageConverter.ConvertPageToImage");

                if (pageIndex < 0 || pageIndex >= converter.PageCount)
                {
                    AppLogger.Warning($"Invalid page index {pageIndex} for PDF with {converter.PageCount} pages",
                        "PdfToImageConverter.ConvertPageToImage");
                    return null;
                }

                // Convert page to image
                using var imageStream = converter.Convert(pageIndex, false, false);

                if (imageStream == null)
                {
                    AppLogger.Error("Convert returned null stream", "PdfToImageConverter.ConvertPageToImage");
                    return null;
                }

                // Reset stream position if seekable
                if (imageStream.CanSeek)
                    imageStream.Position = 0;

                using var ms = new MemoryStream();
                imageStream.CopyTo(ms);

                if (ms.Length == 0)
                {
                    AppLogger.Error("Converted image has 0 bytes", "PdfToImageConverter.ConvertPageToImage");
                    return null;
                }

                AppLogger.Info($"Converted PDF page {pageIndex + 1} to image: {ms.Length} bytes",
                    "PdfToImageConverter.ConvertPageToImage");

                return ms.ToArray();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PdfToImageConverter.ConvertPageToImage");
                return null;
            }
        }

        // Convert all pages to PNG images
        public static List<byte[]> ConvertAllPages(string pdfPath, int dpi = 300)
        {
            var results = new List<byte[]>();

            try
            {
                using var converter = new Syncfusion.PdfToImageConverter.PdfToImageConverter();
                using var fileStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
                converter.Load(fileStream);

                for (int i = 0; i < converter.PageCount; i++)
                {
                    using var imageStream = converter.Convert(i, false, false);
                    if (imageStream != null)
                    {
                        if (imageStream.CanSeek)
                            imageStream.Position = 0;

                        using var ms = new MemoryStream();
                        imageStream.CopyTo(ms);
                        if (ms.Length > 0)
                            results.Add(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PdfToImageConverter.ConvertAllPages");
            }

            return results;
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
