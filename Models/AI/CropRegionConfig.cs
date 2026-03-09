using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VANTAGE.Models.AI
{
    // Crop region config for AI Takeoff — defines BOM and title block locations on drawings
    public class CropRegionConfig
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonPropertyName("client_name")]
        public string ClientName { get; set; } = string.Empty;

        [JsonPropertyName("project_name")]
        public string ProjectName { get; set; } = string.Empty;

        [JsonPropertyName("bom_regions")]
        public List<CropRegion> BomRegions { get; set; } = new();

        // New format: multiple title block regions (e.g., PIPE INFO section + Project info section)
        [JsonPropertyName("title_block_regions")]
        public List<CropRegion> TitleBlockRegions { get; set; } = new();

        // Backward compat: reads old single-region format, writes to TitleBlockRegions list
        [JsonPropertyName("title_block_region")]
        public CropRegion? TitleBlockRegion
        {
            get => null; // Always use TitleBlockRegions for reading
            set
            {
                // When deserializing old config with single region, add it to the list
                if (value != null && TitleBlockRegions.Count == 0)
                    TitleBlockRegions.Add(value);
            }
        }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("created_by")]
        public string CreatedBy { get; set; } = string.Empty;
    }

    // A single rectangular crop region defined as percentages of drawing dimensions
    public class CropRegion
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("x_pct")]
        public double XPct { get; set; }

        [JsonPropertyName("y_pct")]
        public double YPct { get; set; }

        [JsonPropertyName("width_pct")]
        public double WidthPct { get; set; }

        [JsonPropertyName("height_pct")]
        public double HeightPct { get; set; }
    }
}
