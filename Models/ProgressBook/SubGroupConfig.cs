using System.Text.Json.Serialization;

namespace VANTAGE.Models.ProgressBook
{
    // Configuration for a sub-group level in the progress book layout
    public class SubGroupConfig
    {
        // Field to group by (must be a Zone 2 column)
        [JsonPropertyName("groupField")]
        public string GroupField { get; set; } = string.Empty;

        // Field to sort within this group
        [JsonPropertyName("sortField")]
        public string SortField { get; set; } = string.Empty;
    }
}
