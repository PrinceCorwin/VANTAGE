using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Textract;
using Amazon.Textract.Model;
using VANTAGE.Models.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // AWS Textract service for extracting table data from progress sheets
    public class TextractService : IDisposable
    {
        private readonly AmazonTextractClient _client;
        private bool _disposed;

        // Retry configuration
        private const int MaxRetries = 3;
        private const int InitialRetryDelayMs = 1000;
        private const int MaxRetryDelayMs = 10000;

        // Confidence threshold for accepting values
        public const float MinConfidenceThreshold = 70f;

        public TextractService()
        {
            var config = new AmazonTextractConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(CredentialService.AwsRegion)
            };

            _client = new AmazonTextractClient(
                CredentialService.AwsAccessKey,
                CredentialService.AwsSecretKey,
                config);
        }

        // Analyze a single image and extract progress data
        public async Task<List<ScanExtractionResult>> AnalyzeImageAsync(
            byte[] imageBytes,
            CancellationToken cancellationToken = default)
        {
            int retryCount = 0;
            int delayMs = InitialRetryDelayMs;

            while (retryCount < MaxRetries)
            {
                try
                {
                    return await SendAnalyzeRequestAsync(imageBytes, cancellationToken);
                }
                catch (ThrottlingException)
                {
                    AppLogger.Warning($"Textract rate limited (attempt {retryCount + 1})",
                        "TextractService.AnalyzeImageAsync");
                }
                catch (ProvisionedThroughputExceededException)
                {
                    AppLogger.Warning($"Textract throughput exceeded (attempt {retryCount + 1})",
                        "TextractService.AnalyzeImageAsync");
                }
                catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    AppLogger.Info("Analysis cancelled by user", "TextractService.AnalyzeImageAsync");
                    return new List<ScanExtractionResult>();
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TextractService.AnalyzeImageAsync");
                    return new List<ScanExtractionResult>();
                }

                retryCount++;
                if (retryCount < MaxRetries)
                {
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, MaxRetryDelayMs);
                }
            }

            AppLogger.Error($"Textract request failed after {MaxRetries} attempts",
                "TextractService.AnalyzeImageAsync");
            return new List<ScanExtractionResult>();
        }

        // Send the analyze request to Textract
        private async Task<List<ScanExtractionResult>> SendAnalyzeRequestAsync(
            byte[] imageBytes,
            CancellationToken cancellationToken)
        {
            var request = new AnalyzeDocumentRequest
            {
                Document = new Document
                {
                    Bytes = new MemoryStream(imageBytes)
                },
                FeatureTypes = new List<string> { "TABLES", "FORMS" }
            };

            AppLogger.Info($"Sending Textract request ({imageBytes.Length} bytes)",
                "TextractService.SendAnalyzeRequestAsync");

            var response = await _client.AnalyzeDocumentAsync(request, cancellationToken);

            AppLogger.Info($"Textract response: {response.Blocks.Count} blocks",
                "TextractService.SendAnalyzeRequestAsync");

            return ParseTextractResponse(response);
        }

        // Parse Textract response blocks into extraction results
        private List<ScanExtractionResult> ParseTextractResponse(AnalyzeDocumentResponse response)
        {
            var results = new List<ScanExtractionResult>();

            // Build lookup maps for blocks
            var blockMap = response.Blocks.ToDictionary(b => b.Id);
            var tableBlocks = response.Blocks.Where(b => b.BlockType == BlockType.TABLE).ToList();

            if (tableBlocks.Count == 0)
            {
                AppLogger.Warning("No tables found in document", "TextractService.ParseTextractResponse");
                return results;
            }

            AppLogger.Info($"Found {tableBlocks.Count} tables", "TextractService.ParseTextractResponse");

            // Process each table
            foreach (var table in tableBlocks)
            {
                var tableResults = ParseTable(table, blockMap);
                results.AddRange(tableResults);
            }

            AppLogger.Info($"Extracted {results.Count} entries from Textract",
                "TextractService.ParseTextractResponse");

            return results;
        }

        // Parse a single table block into extraction results
        private List<ScanExtractionResult> ParseTable(Block table, Dictionary<string, Block> blockMap)
        {
            var results = new List<ScanExtractionResult>();

            if (table.Relationships == null) return results;

            // Get all cell blocks for this table
            var cellRelation = table.Relationships.FirstOrDefault(r => r.Type == RelationshipType.CHILD);
            if (cellRelation == null) return results;

            // Filter to cells with valid row/column indices
            var cells = cellRelation.Ids
                .Select(id => blockMap.GetValueOrDefault(id))
                .Where(b => b != null && b.BlockType == BlockType.CELL && b.RowIndex.HasValue && b.ColumnIndex.HasValue)
                .Cast<Block>()
                .ToList();

            // Group cells by row
            var rowGroups = cells
                .GroupBy(c => c.RowIndex!.Value)
                .OrderBy(g => g.Key)
                .ToList();

            // Identify column indices from header row (row 1)
            var headerRow = rowGroups.FirstOrDefault(g => g.Key == 1);
            var columnMap = IdentifyColumns(headerRow?.ToList(), blockMap);

            if (!columnMap.ContainsKey("ID") || !columnMap.ContainsKey("% ENTRY"))
            {
                AppLogger.Warning("Could not identify ID or % ENTRY columns",
                    "TextractService.ParseTable");
                return results;
            }

            // Process data rows (skip header)
            foreach (var row in rowGroups.Where(g => g.Key > 1))
            {
                var rowCells = row.ToDictionary(c => c.ColumnIndex!.Value);
                var extraction = ParseDataRow(rowCells, columnMap, blockMap);

                if (extraction != null)
                {
                    results.Add(extraction);
                    AppLogger.Info($"Extracted: ID={extraction.UniqueId}, Pct={extraction.Pct}",
                        "TextractService.ParseTable");
                }
            }

            return results;
        }

        // Identify which columns contain ID and % ENTRY based on headers
        private Dictionary<string, int> IdentifyColumns(List<Block>? headerCells, Dictionary<string, Block> blockMap)
        {
            var columnMap = new Dictionary<string, int>();

            if (headerCells == null) return columnMap;

            foreach (var cell in headerCells)
            {
                if (!cell.ColumnIndex.HasValue) continue;

                string headerText = GetCellText(cell, blockMap).Trim().ToUpperInvariant();
                int colIndex = cell.ColumnIndex.Value;

                // Match column headers
                if (headerText == "ID" || headerText.Contains("ACTIVITYID"))
                {
                    columnMap["ID"] = colIndex;
                }
                else if (headerText.Contains("% ENTRY") || headerText == "%" || headerText == "ENTRY")
                {
                    columnMap["% ENTRY"] = colIndex;
                }
                else if (headerText == "MHS" || headerText.Contains("BUDGET"))
                {
                    columnMap["MHs"] = colIndex;
                }
                else if (headerText == "QTY" || headerText == "QUANTITY")
                {
                    columnMap["QTY"] = colIndex;
                }
                else if (headerText.Contains("REM") && headerText.Contains("MH"))
                {
                    columnMap["REM MH"] = colIndex;
                }
                else if (headerText.Contains("CUR") && headerText.Contains("%"))
                {
                    columnMap["CUR %"] = colIndex;
                }
            }

            AppLogger.Info($"Column map: {string.Join(", ", columnMap.Select(kv => $"{kv.Key}={kv.Value}"))}",
                "TextractService.IdentifyColumns");

            return columnMap;
        }

        // Parse a data row into an extraction result
        private ScanExtractionResult? ParseDataRow(
            Dictionary<int, Block> rowCells,
            Dictionary<string, int> columnMap,
            Dictionary<string, Block> blockMap)
        {
            // Extract ActivityID from ID column
            if (!columnMap.TryGetValue("ID", out int idCol) ||
                !rowCells.TryGetValue(idCol, out var idCell))
            {
                return null;
            }

            string idText = GetCellText(idCell, blockMap).Trim();
            float idConfidence = idCell.Confidence ?? 0f;

            // Parse ActivityID as integer
            string digitsOnly = new string(idText.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(digitsOnly) || !int.TryParse(digitsOnly, out int activityId))
            {
                return null; // Can't parse ID
            }

            // Extract percentage from % ENTRY column
            if (!columnMap.TryGetValue("% ENTRY", out int pctCol) ||
                !rowCells.TryGetValue(pctCol, out var pctCell))
            {
                return null;
            }

            string pctText = GetCellText(pctCell, blockMap).Trim();
            float pctConfidence = pctCell.Confidence ?? 0f;

            // Log raw value before any processing
            AppLogger.Info($"Raw pctText for ActivityID {activityId}: '{pctText}' (confidence: {pctConfidence:F1}%)",
                "TextractService.ParseDataRow");

            // Skip empty entry boxes
            if (string.IsNullOrWhiteSpace(pctText))
            {
                return null;
            }

            // Parse percentage value
            var (pctValue, ocrWarning) = ParsePercentage(pctText);
            if (pctValue == null)
            {
                return null;
            }

            // Calculate overall confidence (average of ID and % confidence)
            float avgConfidence = (idConfidence + pctConfidence) / 2;

            return new ScanExtractionResult
            {
                UniqueId = activityId.ToString(),
                Pct = pctValue,
                Confidence = (int)avgConfidence,
                Raw = $"ID:{idText}, %:{pctText}",
                Warning = ocrWarning
            };
        }

        // Get text content from a cell block
        private string GetCellText(Block cell, Dictionary<string, Block> blockMap)
        {
            if (cell.Relationships == null) return string.Empty;

            var childRelation = cell.Relationships.FirstOrDefault(r => r.Type == RelationshipType.CHILD);
            if (childRelation == null) return string.Empty;

            var textParts = new List<string>();

            foreach (var childId in childRelation.Ids)
            {
                if (blockMap.TryGetValue(childId, out var childBlock))
                {
                    if (childBlock.BlockType == BlockType.WORD)
                    {
                        textParts.Add(childBlock.Text ?? string.Empty);
                    }
                    else if (childBlock.BlockType == BlockType.SELECTION_ELEMENT)
                    {
                        // Handle checkboxes if present
                        if (childBlock.SelectionStatus == SelectionStatus.SELECTED)
                        {
                            textParts.Add("X");
                        }
                    }
                }
            }

            return string.Join(" ", textParts);
        }

        // Parse a percentage value from text with OCR error correction
        // Returns (value, warning) tuple - warning is set when substitution was applied
        private (decimal? Value, string? Warning) ParsePercentage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return (null, null);

            string? warning = null;

            // Remove % symbol and whitespace
            text = text.Replace("%", "").Trim();

            // Common OCR corrections for letters that look like digits
            text = text.Replace("O", "0")   // O vs 0
                       .Replace("o", "0")
                       .Replace("l", "1")   // l vs 1
                       .Replace("I", "1")   // I vs 1
                       .Replace("S", "5")   // S vs 5
                       .Replace("B", "8");  // B vs 8

            // Extract digits and decimal point only
            var cleaned = new string(text.Where(c => char.IsDigit(c) || c == '.').ToArray());

            // Heuristic: lone "0" is likely "10" with a missed leading 1
            // Field workers leave blank for 0%, they don't write "0"
            if (cleaned == "0")
            {
                AppLogger.Info("ParsePercentage: converted '0' to '10' (likely missed leading 1)",
                    "TextractService.ParsePercentage");
                cleaned = "10";
                warning = "OCR read '0' as '10' - verify";
            }

            // Heuristic: "00" is likely "100" with a missed leading 1
            // Nobody writes "00%" - they'd write "0" for zero percent
            if (cleaned == "00")
            {
                AppLogger.Info("ParsePercentage: converted '00' to '100' (likely missed leading 1)",
                    "TextractService.ParsePercentage");
                cleaned = "100";
                warning = "OCR read '00' as '100' - verify";
            }

            if (decimal.TryParse(cleaned, out decimal value))
            {
                // Validate range (0-100)
                if (value >= 0 && value <= 100)
                {
                    return (value, warning);
                }
                AppLogger.Warning($"ParsePercentage: value {value} outside 0-100 range",
                    "TextractService.ParsePercentage");
            }

            return (null, null);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _client?.Dispose();
                _disposed = true;
            }
        }
    }
}
