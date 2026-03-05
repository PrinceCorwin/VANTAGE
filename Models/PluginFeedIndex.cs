using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VANTAGE.Models
{
    public class PluginFeedIndex
    {
        [JsonPropertyName("plugins")]
        public List<PluginFeedItem> Plugins { get; set; } = new();
    }

    public class PluginFeedItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("pluginType")]
        public string PluginType { get; set; } = string.Empty;

        [JsonPropertyName("project")]
        public string Project { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("packageUrl")]
        public string PackageUrl { get; set; } = string.Empty;

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;
    }
}
