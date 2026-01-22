using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VANTAGE.Models.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // Orchestrates the progress sheet scan workflow
    public class ProgressScanService
    {
        private readonly ClaudeVisionService _visionService;

        public ProgressScanService()
        {
            _visionService = new ClaudeVisionService();
        }

        // Process multiple files (PDFs and/or images) and extract progress data
        public async Task<ScanBatchResult> ProcessFilesAsync(
            List<string> filePaths,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ScanBatchResult();

            // First, calculate total pages across all files
            var pageInfos = new List<(string FilePath, int PageCount, bool IsPdf)>();
            foreach (var filePath in filePaths)
            {
                if (PdfToImageConverter.IsPdfFile(filePath))
                {
                    int pageCount = PdfToImageConverter.GetPageCount(filePath);
                    pageInfos.Add((filePath, pageCount, true));
                    result.TotalPages += pageCount;
                }
                else if (PdfToImageConverter.IsImageFile(filePath))
                {
                    pageInfos.Add((filePath, 1, false));
                    result.TotalPages += 1;
                }
                else
                {
                    AppLogger.Warning($"Unsupported file type: {filePath}", "ProgressScanService.ProcessFilesAsync");
                }
            }

            AppLogger.Info($"Starting progress scan: {filePaths.Count} files, {result.TotalPages} total pages",
                "ProgressScanService.ProcessFilesAsync", App.CurrentUser?.Username);

            int currentPage = 0;

            // Process each file
            foreach (var (filePath, pageCount, isPdf) in pageInfos)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    AppLogger.Info("Scan cancelled by user", "ProgressScanService.ProcessFilesAsync");
                    break;
                }

                string fileName = Path.GetFileName(filePath);

                if (isPdf)
                {
                    // Process each page of the PDF
                    for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        currentPage++;
                        progress?.Report(new ScanProgress
                        {
                            CurrentPage = currentPage,
                            TotalPages = result.TotalPages,
                            ExtractedCount = result.Extractions.Count,
                            CurrentFile = $"{fileName} (page {pageIndex + 1})"
                        });

                        await ProcessPdfPageAsync(filePath, pageIndex, fileName, result, cancellationToken);
                    }
                }
                else
                {
                    // Process single image file
                    currentPage++;
                    progress?.Report(new ScanProgress
                    {
                        CurrentPage = currentPage,
                        TotalPages = result.TotalPages,
                        ExtractedCount = result.Extractions.Count,
                        CurrentFile = fileName
                    });

                    await ProcessImageFileAsync(filePath, fileName, result, cancellationToken);
                }
            }

            // Final progress report
            progress?.Report(new ScanProgress
            {
                CurrentPage = result.TotalPages,
                TotalPages = result.TotalPages,
                ExtractedCount = result.Extractions.Count,
                CurrentFile = "Complete"
            });

            AppLogger.Info($"Scan complete: {result.Summary}",
                "ProgressScanService.ProcessFilesAsync", App.CurrentUser?.Username);

            return result;
        }

        // Process a single page from a PDF
        private async Task ProcessPdfPageAsync(
            string pdfPath,
            int pageIndex,
            string fileName,
            ScanBatchResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                // Send PDF directly to Claude API (not converted to PNG)
                // Claude can process PDFs natively with better quality
                var pdfBytes = await File.ReadAllBytesAsync(pdfPath, cancellationToken);

                AppLogger.Info($"Sending PDF directly to API: {pdfBytes.Length} bytes",
                    "ProgressScanService.ProcessPdfPageAsync");

                // Extract data from PDF directly
                var extractions = await _visionService.ExtractFromImageAsync(
                    pdfBytes, "application/pdf", cancellationToken);

                if (extractions.Count > 0)
                {
                    result.Extractions.AddRange(extractions);
                }
                result.SuccessfulPages++;
            }
            catch (Exception ex)
            {
                result.FailedPages++;
                result.Errors.Add($"{fileName} page {pageIndex + 1}: {ex.Message}");
                AppLogger.Error(ex, "ProgressScanService.ProcessPdfPageAsync");
            }
        }

        // Process a single image file (PNG, JPG, etc.)
        private async Task ProcessImageFileAsync(
            string imagePath,
            string fileName,
            ScanBatchResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                // Read image file
                var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
                var mediaType = PdfToImageConverter.GetMediaType(imagePath);

                // Extract data from image
                var extractions = await _visionService.ExtractFromImageAsync(
                    imageBytes, mediaType, cancellationToken);

                if (extractions.Count > 0)
                {
                    result.Extractions.AddRange(extractions);
                }
                result.SuccessfulPages++;
            }
            catch (Exception ex)
            {
                result.FailedPages++;
                result.Errors.Add($"{fileName}: {ex.Message}");
                AppLogger.Error(ex, "ProgressScanService.ProcessImageFileAsync");
            }
        }

        // Calculate total page count for a list of files (for UI display before processing)
        public static int CalculateTotalPages(List<string> filePaths)
        {
            int total = 0;
            foreach (var filePath in filePaths)
            {
                if (PdfToImageConverter.IsPdfFile(filePath))
                {
                    total += PdfToImageConverter.GetPageCount(filePath);
                }
                else if (PdfToImageConverter.IsImageFile(filePath))
                {
                    total += 1;
                }
            }
            return total;
        }
    }
}
