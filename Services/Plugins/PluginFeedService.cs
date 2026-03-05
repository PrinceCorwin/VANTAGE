using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Services.Plugins
{
    public static class PluginFeedService
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task<List<PluginFeedItem>> GetAvailablePluginsAsync()
        {
            try
            {
                string indexUrl = CredentialService.PluginsIndexUrl;
                if (string.IsNullOrWhiteSpace(indexUrl))
                {
                    AppLogger.Warning("Plugins.IndexUrl is not configured", "PluginFeedService.GetAvailablePluginsAsync");
                    return new List<PluginFeedItem>();
                }

                string json = await _httpClient.GetStringAsync(indexUrl);
                var index = JsonSerializer.Deserialize<PluginFeedIndex>(json, _jsonOptions);
                if (index?.Plugins == null)
                {
                    return new List<PluginFeedItem>();
                }

                return index.Plugins
                    .Where(p => !string.IsNullOrWhiteSpace(p.Id))
                    .OrderBy(p => p.Project)
                    .ThenBy(p => p.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PluginFeedService.GetAvailablePluginsAsync");
                return new List<PluginFeedItem>();
            }
        }
    }
}
