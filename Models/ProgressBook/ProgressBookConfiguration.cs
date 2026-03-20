using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VANTAGE.Models.ProgressBook
{
    // Configuration for a progress book layout, serialized to JSON for storage
    public class ProgressBookConfiguration
    {
        [JsonPropertyName("paperSize")]
        public PaperSize PaperSize { get; set; } = PaperSize.Letter;

        // Font size for data rows (4-10pt)
        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; } = 6;

        // Filter field for selecting which records to include (e.g., WorkPackage, Area)
        [JsonPropertyName("filterField")]
        public string FilterField { get; set; } = string.Empty;

        // Filter value - the selected progress book identifier (appears in PDF header)
        [JsonPropertyName("filterValue")]
        public string FilterValue { get; set; } = string.Empty;

        // Exclude activities with 100% progress
        [JsonPropertyName("excludeCompleted")]
        public bool ExcludeCompleted { get; set; } = false;

        // Include records assigned to other users (not just current user)
        [JsonPropertyName("includeAllUsers")]
        public bool IncludeAllUsers { get; set; } = false;

        // Column to use for value-based exclusion filtering
        [JsonPropertyName("excludeColumn")]
        public string ExcludeColumn { get; set; } = string.Empty;

        // Values to exclude from the selected column (multi-select)
        [JsonPropertyName("excludeValues")]
        public List<string> ExcludeValues { get; set; } = new();

        // Group fields for organizing data (up to 10 levels)
        // Groups are always sorted alphanumerically
        [JsonPropertyName("groups")]
        public List<string> Groups { get; set; } = new();

        // Sort fields for ordering records within groups (up to 10, stacking like Excel)
        // "None" values are skipped during sorting
        [JsonPropertyName("sortFields")]
        public List<string> SortFields { get; set; } = new();

        // Zone 2 columns (flexible middle section)
        // Note: ROC and DESC are always required
        [JsonPropertyName("columns")]
        public List<ColumnConfig> Columns { get; set; } = new();

        // Returns default configuration for new layouts
        public static ProgressBookConfiguration CreateDefault()
        {
            return new ProgressBookConfiguration
            {
                PaperSize = PaperSize.Letter,
                FontSize = 8, // Default to 8pt for better OCR/scan accuracy
                FilterField = "WorkPackage",
                FilterValue = string.Empty,
                Groups = new List<string> { "PhaseCode" },
                SortFields = new List<string> { "ROCStep" },
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { FieldName = "ActivityID", DisplayOrder = 0 }, // Shorter ID for easier scanning
                    new ColumnConfig { FieldName = "ROCStep", DisplayOrder = 1 },
                    new ColumnConfig { FieldName = "Description", DisplayOrder = 2 }
                }
            };
        }
    }

    // Summary data for the progress book cover page
    public class CoverPageData
    {
        // Total Budget MHs for ALL records (including excluded completed)
        public double TotalBudgetMHs { get; set; }

        // Total Earned MHs for ALL records (including excluded completed)
        public double TotalEarnedMHs { get; set; }

        // Percent complete for ALL records (Earned / Budget * 100)
        public double PercentComplete => TotalBudgetMHs > 0 ? (TotalEarnedMHs / TotalBudgetMHs) * 100 : 0;

        // Number of activities included in the book (not counting excluded)
        public int IncludedCount { get; set; }

        // Last sync display text (e.g., "3/20/2026 09:31" or "Never")
        public string LastSyncDisplay { get; set; } = "Never";

        // Last updated display text (max UpdatedUtcDate of included activities)
        public string LastUpdatedDisplay { get; set; } = "N/A";

        // Number of completed activities that were excluded from the book
        public int ExcludedCompletedCount { get; set; }

        // Total Budget MHs of the excluded completed activities
        public double ExcludedCompletedBudgetMHs { get; set; }

        // Total Earned MHs of the excluded completed activities
        public double ExcludedCompletedEarnedMHs { get; set; }
    }
}
