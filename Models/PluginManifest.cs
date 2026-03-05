namespace VANTAGE.Models
{
    // Plugin types: "action" (creates UI, user-triggered), "extension" (passive features like themes)
    public class PluginManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string PluginType { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AssemblyFile { get; set; } = string.Empty;
        public string EntryType { get; set; } = string.Empty;
        public string MinAppVersion { get; set; } = string.Empty;
        public string MaxAppVersion { get; set; } = string.Empty;
    }
}
