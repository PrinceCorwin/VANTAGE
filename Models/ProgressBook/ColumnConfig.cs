using System.Text.Json.Serialization;

namespace VANTAGE.Models.ProgressBook
{
    // Configuration for a Zone 2 column in the progress book layout
    public class ColumnConfig
    {
        [JsonPropertyName("fieldName")]
        public string FieldName { get; set; } = string.Empty;

        // Width value (1-100) - will be prorated relative to other columns
        [JsonPropertyName("width")]
        public int Width { get; set; } = 10;

        // Display order in the layout (0-based)
        [JsonPropertyName("displayOrder")]
        public int DisplayOrder { get; set; }
    }
}
