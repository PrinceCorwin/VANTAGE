using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VANTAGE.Models.ProgressBook
{
    // Configuration for a progress book layout, serialized to JSON for storage
    public class ProgressBookConfiguration
    {
        [JsonPropertyName("paperSize")]
        public PaperSize PaperSize { get; set; } = PaperSize.Letter;

        // Font size for data rows (8-14pt)
        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; } = 10;

        // Main grouping field (e.g., PhaseCode, Area)
        [JsonPropertyName("mainGroupField")]
        public string MainGroupField { get; set; } = string.Empty;

        // Sort field within main group
        [JsonPropertyName("mainGroupSortField")]
        public string MainGroupSortField { get; set; } = string.Empty;

        // Optional sub-group levels
        [JsonPropertyName("subGroups")]
        public List<SubGroupConfig> SubGroups { get; set; } = new();

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
                FontSize = 10,
                MainGroupField = "PhaseCode",
                MainGroupSortField = "Description",
                SubGroups = new List<SubGroupConfig>(),
                Columns = new List<ColumnConfig>
                {
                    new ColumnConfig { FieldName = "ROC", Width = 15, DisplayOrder = 0 },
                    new ColumnConfig { FieldName = "Description", Width = 60, DisplayOrder = 1 }
                }
            };
        }
    }
}
