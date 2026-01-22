using System.Collections.Generic;

namespace VANTAGE.Models.AI
{
    // Progress update during scan processing
    public class ScanProgress
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int ExtractedCount { get; set; }
        public string CurrentFile { get; set; } = string.Empty;

        // Calculated property for progress percentage (0-100)
        public int ProgressPercent => TotalPages > 0 ? (CurrentPage * 100) / TotalPages : 0;
    }

    // Result of processing a batch of scanned files
    public class ScanBatchResult
    {
        public List<ScanExtractionResult> Extractions { get; set; } = new();
        public int TotalPages { get; set; }
        public int SuccessfulPages { get; set; }
        public int FailedPages { get; set; }
        public List<string> Errors { get; set; } = new();  // Page-specific error messages

        // Helper to check if any pages failed
        public bool HasErrors => FailedPages > 0;

        // Summary string for display
        public string Summary => $"Processed {SuccessfulPages} of {TotalPages} pages, extracted {Extractions.Count} entries" +
            (HasErrors ? $" ({FailedPages} pages failed)" : "");
    }
}
