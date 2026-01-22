using System.Text.Json.Serialization;

namespace VANTAGE.Models.AI
{
    // Represents a single extracted entry from Claude Vision API response
    public class ScanExtractionResult
    {
        [JsonPropertyName("uniqueId")]
        public string UniqueId { get; set; } = null!;

        [JsonPropertyName("done")]
        public bool? Done { get; set; }

        [JsonPropertyName("qty")]
        public decimal? Qty { get; set; }

        [JsonPropertyName("pct")]
        public decimal? Pct { get; set; }

        [JsonPropertyName("confidence")]
        public int Confidence { get; set; }

        [JsonPropertyName("raw")]
        public string? Raw { get; set; }
    }
}
