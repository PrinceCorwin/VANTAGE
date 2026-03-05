using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Services.Plugins
{
    public class InstalledPluginInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string PluginType { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AssemblyFile { get; set; } = string.Empty;
        public string EntryType { get; set; } = string.Empty;
        public string PluginDirectory { get; set; } = string.Empty;
        public string ManifestPath { get; set; } = string.Empty;
    }

    public static class PluginCatalogService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static string GetPluginsRootPath()
        {
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localApp, "VANTAGE", "Plugins");
        }

        public static List<InstalledPluginInfo> GetInstalledPlugins()
        {
            var plugins = new List<InstalledPluginInfo>();

            try
            {
                var rootPath = GetPluginsRootPath();
                Directory.CreateDirectory(rootPath);

                var manifestFiles = Directory.GetFiles(rootPath, "plugin.json", SearchOption.AllDirectories);
                foreach (var manifestPath in manifestFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(manifestPath);
                        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions);
                        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
                        {
                            continue;
                        }

                        plugins.Add(new InstalledPluginInfo
                        {
                            Id = manifest.Id,
                            Name = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name,
                            Version = manifest.Version,
                            PluginType = manifest.PluginType,
                            Project = manifest.Project,
                            Description = manifest.Description,
                            AssemblyFile = manifest.AssemblyFile,
                            EntryType = manifest.EntryType,
                            ManifestPath = manifestPath,
                            PluginDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty
                        });
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error(ex, "PluginCatalogService.GetInstalledPlugins.ReadManifest");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PluginCatalogService.GetInstalledPlugins");
            }

            return plugins
                .OrderBy(p => p.Project)
                .ThenBy(p => p.Name)
                .ToList();
        }
    }
}
