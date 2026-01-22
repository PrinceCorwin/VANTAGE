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

        // Extraction prompt for Claude - focus on accurate OCR
        private const string ExtractionPrompt = @"This is a construction progress sheet. Extract handwritten progress entries.

COLUMN LAYOUT (left to right on the right side of each row):
- DONE: LIGHT GREEN checkbox - for X marks meaning ""complete""
- QTY: LIGHT BLUE box - for handwritten QUANTITY numbers
- % ENTRY: LIGHT YELLOW box - for handwritten PERCENTAGE numbers

CRITICAL: QTY (blue) and % ENTRY (yellow) are DIFFERENT columns!
- If you see a number in a BLUE box, put it in ""qty""
- If you see a number in a YELLOW box, put it in ""pct""
- Do NOT confuse these two columns!

TASK:
1. Scan each row for handwritten marks in the colored entry boxes
2. For EACH marked row, read the ID from the leftmost ""ID"" column ON THAT SAME ROW
3. Note which colored box contains the mark

Return JSON array:
[{""uniqueId"": ""1139574"", ""done"": true, ""qty"": null, ""pct"": null, ""confidence"": 90, ""raw"": ""X in green DONE box""}]
[{""uniqueId"": ""1139558"", ""done"": null, ""qty"": 82, ""pct"": null, ""confidence"": 85, ""raw"": ""82 in blue QTY box""}]
[{""uniqueId"": ""1139560"", ""done"": null, ""qty"": null, ""pct"": 100, ""confidence"": 90, ""raw"": ""100 in yellow % box""}]

- uniqueId: The 7-digit ID from the leftmost column of THE SAME ROW as the mark
- done: true ONLY if X/checkmark in GREEN box
- qty: number ONLY if written in BLUE box
- pct: number ONLY if written in YELLOW box
- confidence: 0-100
- raw: describe mark AND which color box it's in

Return ONLY the JSON array.";

        public ClaudeVisionService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", ClaudeApiConfig.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", ClaudeApiConfig.ApiVersion);
            // Enable PDF support
            _httpClient.DefaultRequestHeaders.Add("anthropic-beta", "pdfs-2024-09-25");
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
            string base64Data,
            string mediaType,
            CancellationToken cancellationToken)
        {
            // Use "document" type for PDFs, "image" type for images
            var contentType = mediaType == "application/pdf" ? "document" : "image";

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
                                type = contentType,
                                source = new
                                {
                                    type = "base64",
                                    media_type = mediaType,
                                    data = base64Data
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
                // Log the raw response for debugging
                AppLogger.Info($"Claude raw response ({text.Length} chars): {text}",
                    "ClaudeVisionService.ParseExtractionJson");

                // Claude should return just a JSON array, but clean up any extra text
                var trimmed = text.Trim();

                // Find the JSON array bounds
                int start = trimmed.IndexOf('[');
                int end = trimmed.LastIndexOf(']');

                if (start >= 0 && end > start)
                {
                    var jsonArray = trimmed.Substring(start, end - start + 1);
                    var results = JsonSerializer.Deserialize<List<ScanExtractionResult>>(jsonArray);

                    // Log extracted UniqueIDs for debugging
                    if (results != null)
                    {
                        foreach (var r in results)
                        {
                            AppLogger.Info($"Extracted UniqueId: '{r.UniqueId}' (len={r.UniqueId?.Length}), conf={r.Confidence}",
                                "ClaudeVisionService.ParseExtractionJson");
                        }
                    }

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
