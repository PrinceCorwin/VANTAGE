using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VANTAGE.Models.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // Orchestrates the progress sheet scan workflow using AWS Textract
    public class ProgressScanService : IDisposable
    {
        private readonly TextractService _textractService;
        private bool _disposed;

        public ProgressScanService()
        {
            _textractService = new TextractService();
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

        // Process a single page from a PDF by converting to image
        private async Task ProcessPdfPageAsync(
            string pdfPath,
            int pageIndex,
            string fileName,
            ScanBatchResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                // Convert PDF page to PNG image using PdfiumViewer
                // This removes any OCR text layer and forces pure visual analysis
                var imageBytes = PdfToImageConverter.ConvertPageToImage(pdfPath, pageIndex);

                if (imageBytes == null)
                {
                    result.FailedPages++;
                    result.Errors.Add($"{fileName} page {pageIndex + 1}: Failed to convert to image");
                    return;
                }

                // Preprocess image for better OCR (grayscale + contrast enhancement)
                var preprocessed = ImagePreprocessor.PreprocessForOcr(imageBytes);

                AppLogger.Info($"Converted PDF page {pageIndex + 1} to image: {imageBytes.Length} bytes, preprocessed: {preprocessed.Length} bytes",
                    "ProgressScanService.ProcessPdfPageAsync");

                // Extract data from image using Textract
                var extractions = await _textractService.AnalyzeImageAsync(
                    preprocessed, cancellationToken);

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

                // Preprocess image for better OCR (grayscale + contrast enhancement)
                var preprocessed = ImagePreprocessor.PreprocessForOcr(imageBytes);

                // Extract data from image using Textract
                var extractions = await _textractService.AnalyzeImageAsync(
                    preprocessed, cancellationToken);

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

        public void Dispose()
        {
            if (!_disposed)
            {
                _textractService?.Dispose();
                _disposed = true;
            }
        }
    }
}
