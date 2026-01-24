using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using VANTAGE.Models.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // Service for extracting progress data from images using Claude Vision API with Tool Use
    public class ClaudeVisionService
    {
        private readonly HttpClient _httpClient;

        // System prompt for the extraction task - single % entry column with adjacent ID
        private const string SystemPrompt = @"You are analyzing a scanned construction progress sheet. Find handwritten percentage values.

DOCUMENT STRUCTURE:
- Each data row has a % ENTRY box followed by an ID number printed IMMEDIATELY TO THE RIGHT
- The two rightmost columns are: % ENTRY | ID
- The % ENTRY column has a white box for handwritten percentages

YOUR TASK:
For each row with handwriting in the % ENTRY box:
1. Read the handwritten percentage (0-100)
2. Read the ID number IMMEDIATELY TO THE RIGHT of that entry box
3. Report both values

THE ID IS RIGHT NEXT TO THE ENTRY:
- Look directly RIGHT of the handwritten entry
- The ID is the printed number in the column immediately after the % ENTRY box
- IDs are numeric (e.g., 1139574)
- Read the ID exactly as printed";

        // Tool definition for reporting progress entries - simplified for single % column
        private static readonly object ProgressEntryTool = new
        {
            name = "report_progress_entry",
            description = "Report a handwritten percentage entry. Call once for each row that has a number written in the % ENTRY box.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    activity_id = new
                    {
                        type = "string",
                        description = "The ID printed immediately RIGHT of the entry box. Read exactly as printed."
                    },
                    percent = new
                    {
                        type = "integer",
                        description = "The percentage value written in the entry box (0-100). 100 means complete."
                    },
                    confidence = new
                    {
                        type = "integer",
                        description = "Confidence 0-100. Lower if handwriting is unclear."
                    },
                    observation = new
                    {
                        type = "string",
                        description = "What you see: e.g., '100 written in % box', '50 in entry area', 'faint 75'"
                    }
                },
                required = new[] { "activity_id", "percent", "confidence", "observation" }
            }
        };

        public ClaudeVisionService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", ClaudeApiConfig.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", ClaudeApiConfig.ApiVersion);
        }

        // Extract progress entries from an image using tool calling
        public async Task<List<ScanExtractionResult>> ExtractFromImageAsync(
            byte[] imageData,
            string mediaType,
            CancellationToken cancellationToken = default)
        {
            string base64Image = Convert.ToBase64String(imageData);
            return await ExtractFromBase64Async(base64Image, mediaType, cancellationToken);
        }

        // Extract progress entries using tool calling for structured output
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
                    var result = await SendToolRequestAsync(base64Image, mediaType, cancellationToken);
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

        // Send request with tool use for structured extraction
        private async Task<List<ScanExtractionResult>?> SendToolRequestAsync(
            string base64Data,
            string mediaType,
            CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                model = ClaudeApiConfig.Model,
                max_tokens = ClaudeApiConfig.MaxTokens,
                system = SystemPrompt,
                tools = new[] { ProgressEntryTool },
                tool_choice = new { type = "any" },
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
                                    data = base64Data
                                }
                            },
                            new
                            {
                                type = "text",
                                text = "Find handwritten percentages in the '% ENTRY' boxes (white boxes with '%' label). For each entry, read the ID printed immediately to the RIGHT of that entry box. Report the ID and percentage."
                            }
                        }
                    }
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var jsonContent = JsonSerializer.Serialize(requestBody, jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            AppLogger.Info($"Sending tool use request to Claude API", "ClaudeVisionService.SendToolRequestAsync");

            var response = await _httpClient.PostAsync(ClaudeApiConfig.Endpoint, content, cancellationToken);

            // Handle rate limiting
            if ((int)response.StatusCode == 429)
            {
                AppLogger.Warning("Rate limited by API, waiting before retry",
                    "ClaudeVisionService.SendToolRequestAsync");
                await Task.Delay(ClaudeApiConfig.RateLimitDelayMs, cancellationToken);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                AppLogger.Error($"API error {response.StatusCode}: {responseJson}",
                    "ClaudeVisionService.SendToolRequestAsync");
                return null;
            }

            return ParseToolResponse(responseJson);
        }

        // Parse tool use response and extract results from tool calls
        private List<ScanExtractionResult> ParseToolResponse(string responseJson)
        {
            var results = new List<ScanExtractionResult>();

            try
            {
                AppLogger.Info($"Parsing tool response ({responseJson.Length} chars)",
                    "ClaudeVisionService.ParseToolResponse");

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("content", out var contentArray))
                {
                    AppLogger.Warning("No content in response", "ClaudeVisionService.ParseToolResponse");
                    return results;
                }

                // Iterate through content blocks looking for tool_use
                foreach (var contentBlock in contentArray.EnumerateArray())
                {
                    if (!contentBlock.TryGetProperty("type", out var typeElement))
                        continue;

                    var blockType = typeElement.GetString();

                    if (blockType == "tool_use")
                    {
                        var extraction = ParseToolUseBlock(contentBlock);
                        if (extraction != null)
                        {
                            results.Add(extraction);
                            AppLogger.Info($"Tool call: ID={extraction.UniqueId}, Pct={extraction.Pct}, Done={extraction.Done}",
                                "ClaudeVisionService.ParseToolResponse");
                        }
                    }
                }

                AppLogger.Info($"Extracted {results.Count} entries from tool calls",
                    "ClaudeVisionService.ParseToolResponse");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ClaudeVisionService.ParseToolResponse");
            }

            return results;
        }

        // Parse a single tool_use block into a ScanExtractionResult
        // Simplified for single % column - just activity_id and percent
        private ScanExtractionResult? ParseToolUseBlock(JsonElement toolUseBlock)
        {
            try
            {
                if (!toolUseBlock.TryGetProperty("input", out var input))
                    return null;

                var activityId = input.TryGetProperty("activity_id", out var idEl) ? idEl.GetString() : null;
                var percent = input.TryGetProperty("percent", out var pctEl) ? pctEl.GetInt32() : (int?)null;
                var confidence = input.TryGetProperty("confidence", out var confEl) ? confEl.GetInt32() : 0;
                var observation = input.TryGetProperty("observation", out var obsEl) ? obsEl.GetString() : null;

                if (string.IsNullOrEmpty(activityId) || percent == null)
                    return null;

                var result = new ScanExtractionResult
                {
                    UniqueId = activityId,
                    Pct = percent.Value,
                    Done = percent.Value >= 100,  // 100 or more = done
                    Confidence = confidence,
                    Raw = observation
                };

                AppLogger.Info($"Extracted: ID={activityId}, Percent={percent}, Confidence={confidence}",
                    "ClaudeVisionService.ParseToolUseBlock");

                return result;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ClaudeVisionService.ParseToolUseBlock");
                return null;
            }
        }

        private static bool IsRetryable(HttpRequestException ex)
        {
            return ex.StatusCode == null ||
                   (int)ex.StatusCode.Value >= 500 ||
                   (int)ex.StatusCode.Value == 429;
        }
    }
}
