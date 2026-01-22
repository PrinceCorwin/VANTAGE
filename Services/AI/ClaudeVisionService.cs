using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VANTAGE.Models.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // Service for extracting progress data from images using Claude Vision API
    public class ClaudeVisionService
    {
        private readonly HttpClient _httpClient;

        // Extraction prompt for Claude (from PRD)
        private const string ExtractionPrompt = @"Analyze this construction progress sheet image. Extract all rows that contain handwritten entries in the DONE checkbox, QTY box, or % box.

Document Structure:
- UniqueID column is on the far left (alphanumeric, ~19 characters, e.g., i251009101621125ano)
- DONE column has an empty checkbox (☐) - look for checkmarks, X marks, or filled boxes
- QTY column has a boxed area [...] for handwritten quantity values
- % column has a boxed area [...] for handwritten percentage values
- Only data rows have UniqueIDs - ignore header rows and group summary rows

For each row with ANY handwritten entry, return:
- uniqueId: The exact alphanumeric UniqueID from the leftmost column (CRITICAL - must be precise)
- done: true if checkbox is marked (checkmark, X, filled), false if empty, null if unclear
- qty: The handwritten quantity value as a number (null if empty or illegible)
- pct: The handwritten percentage value as a number WITHOUT % symbol (null if empty or illegible)
- confidence: Your confidence in this extraction (0-100)
- raw: Exactly what you see written (for verification)

Return ONLY a JSON array, no other text:
[
  {""uniqueId"": ""i251009101621125ano"", ""done"": true, ""qty"": null, ""pct"": null, ""confidence"": 98, ""raw"": ""checkmark""},
  {""uniqueId"": ""i251009101556089ano"", ""done"": false, ""qty"": 2.5, ""pct"": null, ""confidence"": 85, ""raw"": ""2.5""},
  {""uniqueId"": ""i251009101621098pno"", ""done"": false, ""qty"": null, ""pct"": 50, ""confidence"": 92, ""raw"": ""50""}
]

Rules:
- ONLY include rows where you see handwriting or marks in DONE, QTY, or % areas
- Skip rows with no handwritten entries
- If you see a number near the % box, treat it as percentage
- If you see a number near the QTY box, treat it as quantity
- If ""50%"" is written, extract pct as 50 (not 50%)
- UniqueID must be extracted EXACTLY - this is the database key
- If UniqueID is unclear, set confidence below 50
- For ambiguous entries, lower confidence and describe in raw field";

        public ClaudeVisionService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", ClaudeApiConfig.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", ClaudeApiConfig.ApiVersion);
        }

        // Extract progress entries from an image (byte array)
        // Returns empty list on failure (never throws)
        public async Task<List<ScanExtractionResult>> ExtractFromImageAsync(
            byte[] imageData,
            string mediaType,
            CancellationToken cancellationToken = default)
        {
            string base64Image = Convert.ToBase64String(imageData);
            return await ExtractFromBase64Async(base64Image, mediaType, cancellationToken);
        }

        // Extract progress entries from a base64 encoded image
        // Returns empty list on failure (never throws)
        public async Task<List<ScanExtractionResult>> ExtractFromBase64Async(
            string base64Image,
            string mediaType,
            CancellationToken cancellationToken = default)
        {
            int retryCount = 0;
            int delayMs = ClaudeApiConfig.InitialRetryDelayMs;

            while (retryCount < ClaudeApiConfig.MaxRetries)
            {
                try
                {
                    var result = await SendRequestAsync(base64Image, mediaType, cancellationToken);
                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (HttpRequestException ex) when (IsRetryable(ex))
                {
                    AppLogger.Warning($"API request failed (attempt {retryCount + 1}): {ex.Message}",
                        "ClaudeVisionService.ExtractFromBase64Async");
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    AppLogger.Info("Extraction cancelled by user", "ClaudeVisionService.ExtractFromBase64Async");
                    return new List<ScanExtractionResult>();
                }
                catch (TaskCanceledException)
                {
                    AppLogger.Warning($"API request timed out (attempt {retryCount + 1})",
                        "ClaudeVisionService.ExtractFromBase64Async");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ClaudeVisionService.ExtractFromBase64Async");
                    return new List<ScanExtractionResult>();
                }

                retryCount++;
                if (retryCount < ClaudeApiConfig.MaxRetries)
                {
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, ClaudeApiConfig.MaxRetryDelayMs);
                }
            }

            AppLogger.Error($"API request failed after {ClaudeApiConfig.MaxRetries} attempts",
                "ClaudeVisionService.ExtractFromBase64Async");
            return new List<ScanExtractionResult>();
        }

        // Send the actual API request
        private async Task<List<ScanExtractionResult>?> SendRequestAsync(
            string base64Image,
            string mediaType,
            CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = ClaudeApiConfig.Model,
                max_tokens = ClaudeApiConfig.MaxTokens,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = mediaType,
                                    data = base64Image
                                }
                            },
                            new
                            {
                                type = "text",
                                text = ExtractionPrompt
                            }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(ClaudeApiConfig.Endpoint, content, cancellationToken);

            // Handle rate limiting
            if ((int)response.StatusCode == 429)
            {
                AppLogger.Warning("Rate limited by API, waiting before retry",
                    "ClaudeVisionService.SendRequestAsync");
                await Task.Delay(ClaudeApiConfig.RateLimitDelayMs, cancellationToken);
                return null; // Will trigger retry
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                AppLogger.Error($"API error {response.StatusCode}: {responseJson}",
                    "ClaudeVisionService.SendRequestAsync");
                return null;
            }

            return ParseApiResponse(responseJson);
        }

        // Parse the API response and extract the JSON array from content
        private List<ScanExtractionResult> ParseApiResponse(string responseJson)
        {
            try
            {
                // Parse the Claude API response structure
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                // Navigate to content[0].text
                if (root.TryGetProperty("content", out var contentArray) &&
                    contentArray.GetArrayLength() > 0)
                {
                    var firstContent = contentArray[0];
                    if (firstContent.TryGetProperty("text", out var textElement))
                    {
                        var extractedText = textElement.GetString();
                        if (!string.IsNullOrEmpty(extractedText))
                        {
                            return ParseExtractionJson(extractedText);
                        }
                    }
                }

                AppLogger.Warning("Unexpected API response structure",
                    "ClaudeVisionService.ParseApiResponse");
                return new List<ScanExtractionResult>();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ClaudeVisionService.ParseApiResponse");
                return new List<ScanExtractionResult>();
            }
        }

        // Parse the extraction JSON array from Claude's text response
        private List<ScanExtractionResult> ParseExtractionJson(string text)
        {
            try
            {
                // Claude should return just a JSON array, but clean up any extra text
                var trimmed = text.Trim();

                // Find the JSON array bounds
                int start = trimmed.IndexOf('[');
                int end = trimmed.LastIndexOf(']');

                if (start >= 0 && end > start)
                {
                    var jsonArray = trimmed.Substring(start, end - start + 1);
                    var results = JsonSerializer.Deserialize<List<ScanExtractionResult>>(jsonArray);
                    return results ?? new List<ScanExtractionResult>();
                }

                AppLogger.Warning($"No JSON array found in response: {text.Substring(0, Math.Min(100, text.Length))}",
                    "ClaudeVisionService.ParseExtractionJson");
                return new List<ScanExtractionResult>();
            }
            catch (JsonException ex)
            {
                AppLogger.Error($"Failed to parse extraction JSON: {ex.Message}",
                    "ClaudeVisionService.ParseExtractionJson");
                return new List<ScanExtractionResult>();
            }
        }

        // Determine if an exception is retryable
        private static bool IsRetryable(HttpRequestException ex)
        {
            // Network errors, server errors (5xx) are retryable
            return ex.StatusCode == null ||
                   (int)ex.StatusCode.Value >= 500 ||
                   (int)ex.StatusCode.Value == 429;
        }
    }
}
