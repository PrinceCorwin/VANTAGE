using System;
using System.IO;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // Helper for file type detection and media types for Claude Vision API
    public static class PdfToImageConverter
    {
        // Get the number of pages in a PDF file
        // Returns 1 as default since Claude processes the entire PDF
        public static int GetPageCount(string pdfPath)
        {
            // PDF is sent directly to Claude API which handles multi-page PDFs
            // Return 1 for UI display purposes
            return 1;
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
