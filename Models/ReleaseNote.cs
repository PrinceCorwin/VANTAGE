using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VANTAGE.Models
{
    // Represents a single version release with its notes
    public class ReleaseNote
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("highlights")]
        public List<string> Highlights { get; set; } = new();
    }

    // Root object for the release notes JSON file
    public class ReleaseNotesData
    {
        [JsonPropertyName("releases")]
        public List<ReleaseNote> Releases { get; set; } = new();
    }
}
