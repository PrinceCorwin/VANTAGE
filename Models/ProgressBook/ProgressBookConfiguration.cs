using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VANTAGE.Models.ProgressBook
{
    // Configuration for a progress book layout, serialized to JSON for storage
    public class ProgressBookConfiguration
    {
        // Bumped from 1 -> 2 in the 2026-06 columns refactor that promoted
        // MHs / QTY / REM MH / CUR % / % ENTRY to first-class user columns.
        // Layouts saved before this bump won't have the field — System.Text.Json
        // leaves the property at its default-of-default (0 for int), so the
        // migration check in ProgressBooksView.LoadLayoutConfigurationAsync
        // sees `< 2` and runs. CRITICAL: do NOT initialize this property to
        // CurrentSchemaVersion — that would make legacy JSON look already-current
        // and migration would never fire. New configs stamp it explicitly in
        // CreateDefault().
        public const int CurrentSchemaVersion = 2;

        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

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

        // Default field set for new layouts. Order matches the legacy 3-zone render
        // order (ID, ROC, DESC, then the four data columns, with the % entry box on
        // the far right — the field hand's natural stopping point). Labels and source
        // kinds come from ProgressBookColumnCatalog so this list stays purely
        // structural.
        private static readonly string[] DefaultColumnFieldNames =
        {
            "ActivityID",
            "ROCStep",
            "Description",
            "BudgetMHs",
            "Quantity",
            ProgressBookColumnCatalog.RemainingMHsFieldName,
            "PercentEntry",
            ProgressBookColumnCatalog.EntryBoxFieldName,
        };

        // Returns default configuration for new layouts.
        public static ProgressBookConfiguration CreateDefault()
        {
            var config = new ProgressBookConfiguration
            {
                SchemaVersion = CurrentSchemaVersion,
                PaperSize = PaperSize.Letter,
                FontSize = 8, // Default to 8pt for better OCR/scan accuracy
                FilterField = "WorkPackage",
                FilterValue = string.Empty,
                Groups = new List<string> { "PhaseCode" },
                SortFields = new List<string> { "ROCStep" },
            };

            int order = 0;
            foreach (var fieldName in DefaultColumnFieldNames)
            {
                config.Columns.Add(new ColumnConfig
                {
                    FieldName = fieldName,
                    DisplayOrder = order++,
                    SourceKind = ProgressBookColumnCatalog.GetSourceKind(fieldName),
                    DisplayHeader = ProgressBookColumnCatalog.GetDisplayHeader(fieldName),
                });
            }

            return config;
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
