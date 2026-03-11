using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // Post-processes takeoff Excel output: generates Labor tab (exploded connections)
    // and Summary tab (stats) from the Material tab produced by AWS lambda.
    public static class TakeoffPostProcessor
    {
        // Accumulates missed fitting lookups during FRH generation (reset each run)
        private static List<MissedMakeup> _missedMakeups = new();

        // Columns to exclude from Labor tab (material-only fields)
        private static readonly HashSet<string> ExcludeFromLabor = new(StringComparer.OrdinalIgnoreCase)
        {
            "connection_qty",
            "Connection Qty",
            "Connection Type",
            "connection_type",
            "Raw Description",
            "raw_description",
            "length",
            "Item ID"
        };

        // Components to exclude from fitting makeup lookup (non-weldable items)
        // Components to exclude from fitting makeup lookup (non-weldable or not tracked)
        private static readonly HashSet<string> ExcludeFromMakeupLookup = new(StringComparer.OrdinalIgnoreCase)
        {
            "FS", "GSKT", "BOLT", "WAS", "HEAT", "HOSE", "INST", "PLG", "SAFSHW", "F8B", "DPAN", "ACT", "FLGB", "PIPET"
        };

        // Main entry point - processes the downloaded Excel file in place
        public static void GenerateLaborAndSummary(string excelPath)
        {
            try
            {
                using var workbook = new XLWorkbook(excelPath);

                // Reset missed makeups for this run
                _missedMakeups = new List<MissedMakeup>();

                // Step A: Read Material tab
                var materialRows = ReadMaterialTab(workbook);
                AppLogger.Info($"Read {materialRows.Count} material rows", "TakeoffPostProcessor");

                // Step B: Generate Labor rows
                var laborRows = GenerateLaborRows(materialRows);
                AppLogger.Info($"Generated {laborRows.Count} labor rows", "TakeoffPostProcessor");

                // Step C: Write Labor tab
                WriteLaborTab(workbook, laborRows);

                // Step D & E: Generate and write Summary tab
                WriteSummaryTab(workbook, materialRows, laborRows);

                // Step F: Write Missed Makeups tab (only if there are missed lookups)
                if (_missedMakeups.Count > 0)
                {
                    WriteMissedMakeupsTab(workbook);
                    AppLogger.Info($"Logged {_missedMakeups.Count} missed fitting makeups", "TakeoffPostProcessor");
                }

                // Step G: Reorder tabs
                ReorderTabs(workbook);

                // Step G: Save
                workbook.Save();
                AppLogger.Info($"Saved workbook with Labor and Summary tabs", "TakeoffPostProcessor");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TakeoffPostProcessor.GenerateLaborAndSummary");
                throw;
            }
        }

        // Reorder tabs: Summary, Material, Labor, Flagged
        private static void ReorderTabs(XLWorkbook workbook)
        {
            var desiredOrder = new[] { "Summary", "Material", "Labor", "Flagged", "Missed Makeups" };
            int position = 1;

            foreach (var name in desiredOrder)
            {
                if (workbook.TryGetWorksheet(name, out var ws))
                {
                    ws.Position = position++;
                }
            }
        }

        // Step A: Read Material tab into list of dictionaries (column name -> value)
        private static List<Dictionary<string, object?>> ReadMaterialTab(XLWorkbook workbook)
        {
            var rows = new List<Dictionary<string, object?>>();

            if (!workbook.TryGetWorksheet("Material", out var ws))
                throw new InvalidOperationException("Material tab not found in workbook");

            var usedRange = ws.RangeUsed();
            if (usedRange == null) return rows;

            int lastRow = usedRange.LastRow().RowNumber();
            int lastCol = usedRange.LastColumn().ColumnNumber();

            // Build column name map from header row
            var columnMap = new Dictionary<int, string>();
            for (int col = 1; col <= lastCol; col++)
            {
                string header = ws.Cell(1, col).GetString().Trim();
                if (!string.IsNullOrEmpty(header))
                    columnMap[col] = header;
            }

            // Read data rows
            for (int row = 2; row <= lastRow; row++)
            {
                var rowData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (col, name) in columnMap)
                {
                    var cell = ws.Cell(row, col);
                    if (cell.IsEmpty())
                        rowData[name] = null;
                    else if (cell.Value.IsNumber)
                        rowData[name] = cell.Value.GetNumber();
                    else
                        rowData[name] = cell.GetString();
                }
                rows.Add(rowData);
            }

            return rows;
        }

        // Step B: Generate Labor rows (one per connection, exploded) + FRH records
        private static List<Dictionary<string, object?>> GenerateLaborRows(
            List<Dictionary<string, object?>> materialRows)
        {
            var laborRows = new List<Dictionary<string, object?>>();

            foreach (var mat in materialRows)
            {
                var rows = ExplodeMaterialRow(mat);
                laborRows.AddRange(rows);
            }

            // Generate FRH (Field Handling) records for PIPE items with fitting makeup
            var frhRows = GenerateFrhRows(materialRows);
            laborRows.AddRange(frhRows);

            return laborRows;
        }

        // Generate FRH records by computing fitting makeup for each pipe
        private static List<Dictionary<string, object?>> GenerateFrhRows(
            List<Dictionary<string, object?>> materialRows)
        {
            var frhRows = new List<Dictionary<string, object?>>();

            // Group material rows by Drawing Number
            var byDrawing = materialRows.GroupBy(r => GetString(r, "Drawing Number"));

            foreach (var drawingGroup in byDrawing)
            {
                var rows = drawingGroup.ToList();
                var pipeRows = rows.Where(r => GetString(r, "Component").Equals("PIPE", StringComparison.OrdinalIgnoreCase)).ToList();
                var fittingRows = rows.Where(r => !GetString(r, "Component").Equals("PIPE", StringComparison.OrdinalIgnoreCase)).ToList();

                AppLogger.Info($"Drawing '{drawingGroup.Key}': {pipeRows.Count} pipes, {fittingRows.Count} fittings", "TakeoffPostProcessor.FRH");

                // Track which fittings get claimed by at least one pipe
                var claimedFittings = new HashSet<Dictionary<string, object?>>();

                foreach (var pipe in pipeRows)
                {
                    double pipeSize = FittingMakeupService.GetDouble(pipe, "Size");
                    string pipeMaterial = GetString(pipe, "Material");
                    string? pipeClass = GetNullableString(pipe, "Class Rating");

                    AppLogger.Info($"  PIPE size={pipeSize} material='{pipeMaterial}' class={pipeClass}", "TakeoffPostProcessor.FRH");

                    // Find fittings on same drawing with matching size AND material
                    var matchingFittings = new List<Dictionary<string, object?>>();
                    foreach (var fitting in fittingRows)
                    {
                        string fittingComponent = GetString(fitting, "Component").ToUpper();

                        // Skip non-weldable components
                        if (ExcludeFromMakeupLookup.Contains(fittingComponent))
                            continue;

                        string fittingMaterial = GetString(fitting, "Material");

                        // Material must match
                        if (!fittingMaterial.Equals(pipeMaterial, StringComparison.OrdinalIgnoreCase))
                        {
                            AppLogger.Info($"    SKIP {fittingComponent}: material mismatch pipe='{pipeMaterial}' fitting='{fittingMaterial}'", "TakeoffPostProcessor.FRH");
                            continue;
                        }

                        // Size matching logic varies by component type
                        if (FittingMakeupService.IsOlet(fittingComponent))
                        {
                            // Olet BOM has dual size in Size field (e.g., "6x1") — parse smaller
                            string sizeStr = GetString(fitting, "Size");
                            double? smallSize = FittingMakeupService.ParseOletSmallSize(sizeStr);
                            if (smallSize == null)
                            {
                                // Single size — try direct match
                                double fittingSize = FittingMakeupService.GetDouble(fitting, "Size");
                                if (Math.Abs(fittingSize - pipeSize) < 0.001)
                                {
                                    matchingFittings.Add(fitting);
                                    claimedFittings.Add(fitting);
                                }
                            }
                            else if (Math.Abs(smallSize.Value - pipeSize) < 0.001)
                            {
                                matchingFittings.Add(fitting);
                                claimedFittings.Add(fitting);
                            }
                        }
                        else if (fittingComponent == "REDT")
                        {
                            // REDT: parse dual size (e.g., "4x2"), match if pipe equals either size
                            string sizeStr = GetString(fitting, "Size");
                            var parsed = FittingMakeupService.ParseDualSize(sizeStr);
                            if (parsed != null &&
                                (Math.Abs(parsed.Value.Larger - pipeSize) < 0.001 ||
                                 Math.Abs(parsed.Value.Smaller - pipeSize) < 0.001))
                            {
                                matchingFittings.Add(fitting);
                                claimedFittings.Add(fitting);
                            }
                        }
                        else if (fittingComponent == "REDC" || fittingComponent == "REDE" || fittingComponent == "SWG")
                        {
                            // Reducing fittings: parse dual size, only match on larger size
                            string sizeStr = GetString(fitting, "Size");
                            var parsed = FittingMakeupService.ParseDualSize(sizeStr);
                            if (parsed != null && Math.Abs(parsed.Value.Larger - pipeSize) < 0.001)
                            {
                                matchingFittings.Add(fitting);
                                claimedFittings.Add(fitting);
                            }
                        }
                        else
                        {
                            // Standard fitting — size must match pipe size
                            double fittingSize = FittingMakeupService.GetDouble(fitting, "Size");
                            if (Math.Abs(fittingSize - pipeSize) < 0.001)
                            {
                                matchingFittings.Add(fitting);
                                claimedFittings.Add(fitting);
                            }
                        }
                    }

                    AppLogger.Info($"  Matched {matchingFittings.Count} fittings to pipe size={pipeSize}", "TakeoffPostProcessor.FRH");

                    // Calculate fitting makeup
                    var (totalMakeupInches, missed) = FittingMakeupService.CalculateFittingMakeupForPipe(
                        pipeSize, pipeClass, matchingFittings);

                    AppLogger.Info($"  Makeup: {totalMakeupInches} inches, {missed.Count} missed", "TakeoffPostProcessor.FRH");

                    // Collect missed makeups for the tab
                    _missedMakeups.AddRange(missed);

                    // Get pipe length in feet
                    double pipeLengthFeet = FittingMakeupService.ParsePipeLengthFeet(pipe);

                    // FRH Quantity = pipe length (ft) + (total makeup inches / 12)
                    double frhQuantity = pipeLengthFeet + (totalMakeupInches / 12.0);
                    if (frhQuantity <= 0) continue;

                    // Create FRH row (same structure as FSH)
                    var frh = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (key, value) in pipe)
                    {
                        if (!ExcludeFromLabor.Contains(key))
                            frh[key] = value;
                    }
                    frh["Component"] = "FRH";
                    frh["Quantity"] = Math.Round(frhQuantity, 3);
                    frh["Description"] = GetString(pipe, "Raw Description");
                    frh["BudgetMHs"] = null;

                    frhRows.Add(frh);
                }

                // Log fittings on this drawing that weren't claimed by any pipe (excluding non-weldable)
                foreach (var fitting in fittingRows)
                {
                    if (claimedFittings.Contains(fitting)) continue;

                    string component = GetString(fitting, "Component").ToUpper();
                    if (ExcludeFromMakeupLookup.Contains(component)) continue;

                    _missedMakeups.Add(new MissedMakeup
                    {
                        DrawingNumber = GetString(fitting, "Drawing Number"),
                        Component = component,
                        Size = GetString(fitting, "Size"),
                        ConnectionType = GetString(fitting, "Connection Type"),
                        ClassRating = GetString(fitting, "Class Rating"),
                        Description = GetString(fitting, "Raw Description")
                    });
                }
            }

            return frhRows;
        }

        // Helper: Get nullable double from row
        private static double? GetNullableDouble(Dictionary<string, object?> row, string key)
        {
            if (row.TryGetValue(key, out var val) && val != null)
            {
                if (val is double d) return d;
                string s = val.ToString()?.Trim() ?? "";
                s = s.TrimEnd('\'', '"');
                if (double.TryParse(s, out double parsed)) return parsed;
            }
            return null;
        }

        // Explode a single material row into labor rows
        private static List<Dictionary<string, object?>> ExplodeMaterialRow(
            Dictionary<string, object?> mat)
        {
            var result = new List<Dictionary<string, object?>>();

            string component = GetString(mat, "Component").ToUpper();
            int quantity = ParseQuantity(mat);
            string sizeStr = GetString(mat, "Size");
            string thickness = GetString(mat, "Thickness");
            string pipeSpec = FindPipeSpec(mat);
            string material = GetString(mat, "Material");
            string commodityCode = GetString(mat, "Commodity Code");
            string rawDesc = GetString(mat, "Raw Description");

            // Parse dual size if present (e.g., "4x2" → larger=4, smaller=2)
            var dualSize = FittingMakeupService.ParseDualSize(sizeStr);
            double largerSize = dualSize?.Larger ?? FittingMakeupService.GetDouble(mat, "Size");
            double smallerSize = dualSize?.Smaller ?? largerSize;
            bool isDualSize = dualSize != null;

            // Non-PIPE items: add fab record with original component and raw description
            if (!component.Equals("PIPE", StringComparison.OrdinalIgnoreCase))
            {
                for (int q = 0; q < quantity; q++)
                {
                    var fab = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (key, value) in mat)
                    {
                        if (!ExcludeFromLabor.Contains(key))
                            fab[key] = value;
                    }
                    fab["Quantity"] = 1;
                    fab["Description"] = string.IsNullOrEmpty(commodityCode)
                        ? rawDesc
                        : $"{rawDesc} - {commodityCode}";
                    fab["BudgetMHs"] = null;
                    result.Add(fab);
                }
            }
            else
            {
                // PIPE items: one FSH (fab shop handling) record per BOM item
                var fsh = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in mat)
                {
                    if (!ExcludeFromLabor.Contains(key))
                        fsh[key] = value;
                }
                fsh["Component"] = "FSH";
                fsh["Quantity"] = 1;
                fsh["Description"] = rawDesc;
                fsh["BudgetMHs"] = null;
                result.Add(fsh);
            }

            // Connection explosion (all items with connections)
            int connQty = GetInt(mat, "Connection Qty");
            if (connQty <= 0) return result;

            string connTypes = GetString(mat, "Connection Type");
            if (string.IsNullOrWhiteSpace(connTypes)) return result;

            // Parse connection types (may be comma-separated like "BW, THRD")
            var types = connTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim().ToUpper())
                                 .ToList();

            // Build list of (connectionType, size) pairs based on component type
            var connectionPairs = BuildConnectionPairs(component, types, connQty, largerSize, smallerSize, isDualSize);

            // Explode: quantity × connections
            for (int q = 0; q < quantity; q++)
            {
                foreach (var (connType, connSize) in connectionPairs)
                {
                    // Skip NIP connections
                    if (connType == "NIP") continue;

                    var labor = CreateLaborRow(mat, connType, connSize, thickness, pipeSpec, material);
                    result.Add(labor);

                    // Fabrication records (not for olets)
                    if (!FittingMakeupService.IsOlet(component))
                    {
                        // CUT: one per BW, SW, or THRD connection
                        if (connType == "BW" || connType == "SW" || connType == "THRD")
                        {
                            var cut = CreateFabRow(mat, "CUT", connSize, thickness, pipeSpec, material);
                            result.Add(cut);
                        }

                        // BEV: two per BW connection
                        if (connType == "BW")
                        {
                            for (int b = 0; b < 2; b++)
                            {
                                var bev = CreateFabRow(mat, "BEV", connSize, thickness, pipeSpec, material);
                                result.Add(bev);
                            }
                        }
                    }
                }
            }

            return result;
        }

        // Build list of (connectionType, size) pairs based on component type
        private static List<(string Type, double Size)> BuildConnectionPairs(
            string component, List<string> types, int connQty, double largerSize, double smallerSize, bool isDualSize)
        {
            var pairs = new List<(string, double)>();
            if (types.Count == 0 || connQty <= 0) return pairs;

            // Olets: use smaller size only, OLW + other type (both at smaller size)
            if (FittingMakeupService.IsOlet(component))
            {
                // Always add OLW for the weld to header
                pairs.Add(("OLW", smallerSize));
                // Add the other connection type if present and not OLW
                foreach (var t in types)
                {
                    if (t != "OLW")
                    {
                        pairs.Add((t, smallerSize));
                        break;
                    }
                }
                return pairs;
            }

            // FLG types: BU + other type (typically BW)
            if (component == "FLG" || component == "FLGB" || component == "FLGO")
            {
                // Always add BU for the bolt-up
                pairs.Add(("BU", largerSize));
                // Add the other connection type if present and not BU
                foreach (var t in types)
                {
                    if (t != "BU")
                    {
                        pairs.Add((t, largerSize));
                        break;
                    }
                }
                return pairs;
            }

            // TEE with dual size: 2 connections for larger (run faces), 1 for smaller (outlet)
            if (component == "TEE" && isDualSize)
            {
                string runType = types.Count > 0 ? types[0] : "BW";
                string outletType = types.Count > 1 ? types[1] : runType;
                pairs.Add((runType, largerSize));
                pairs.Add((runType, largerSize));
                pairs.Add((outletType, smallerSize));
                return pairs;
            }

            // Dual-size fittings (REDT, REDC, REDE, SWG, etc.): 1st type at larger, 2nd type at smaller
            if (isDualSize)
            {
                string largerType = types.Count > 0 ? types[0] : "BW";
                string smallerType = types.Count > 1 ? types[1] : largerType;
                pairs.Add((largerType, largerSize));
                pairs.Add((smallerType, smallerSize));
                return pairs;
            }

            // Single-size fittings: distribute connQty across types
            // If only one type and connQty > 1, repeat that type
            if (types.Count == 1)
            {
                for (int i = 0; i < connQty; i++)
                    pairs.Add((types[0], largerSize));
            }
            else
            {
                // Multiple types: assign in order, cycle if connQty > types.Count
                for (int i = 0; i < connQty; i++)
                    pairs.Add((types[i % types.Count], largerSize));
            }
            return pairs;
        }

        // Create a labor row for a connection
        private static Dictionary<string, object?> CreateLaborRow(
            Dictionary<string, object?> mat, string connType, double size,
            string thickness, string pipeSpec, string material)
        {
            var labor = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in mat)
            {
                if (!ExcludeFromLabor.Contains(key))
                    labor[key] = value;
            }

            string sizeStr = size.ToString("0.###");
            labor["Component"] = connType;
            labor["Size"] = sizeStr;
            labor["Quantity"] = 1;
            labor["ShopField"] = connType == "BU" ? 2 : 1;
            labor["Description"] = BuildConcatDescription(sizeStr, thickness, pipeSpec, material, connType);
            labor["BudgetMHs"] = null;

            return labor;
        }

        // Create a fabrication row (CUT or BEV)
        private static Dictionary<string, object?> CreateFabRow(
            Dictionary<string, object?> mat, string fabType, double size,
            string thickness, string pipeSpec, string material)
        {
            var fab = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in mat)
            {
                if (!ExcludeFromLabor.Contains(key))
                    fab[key] = value;
            }

            string sizeStr = size.ToString("0.###");
            fab["Component"] = fabType;
            fab["Size"] = sizeStr;
            fab["Quantity"] = 1;
            fab["ShopField"] = 1;
            fab["Description"] = BuildFabDescription(sizeStr, thickness, pipeSpec, material, fabType == "BEV" ? "BEVEL" : fabType);
            fab["BudgetMHs"] = null;

            return fab;
        }

        // Parse quantity - handles formats like "2", "41.3'", etc.
        private static int ParseQuantity(Dictionary<string, object?> mat)
        {
            var qtyVal = mat.GetValueOrDefault("Quantity");
            if (qtyVal == null) return 1;

            string qtyStr = qtyVal.ToString()?.Trim() ?? "1";

            // Strip trailing quote (feet marker)
            qtyStr = qtyStr.TrimEnd('\'', '"');

            if (double.TryParse(qtyStr, out double d))
                return Math.Max(1, (int)Math.Floor(d));

            return 1;
        }

        // Find pipe spec from title block fields (various naming conventions)
        private static string FindPipeSpec(Dictionary<string, object?> mat)
        {
            string[] possibleNames = { "Pipe Spec", "PIPE SPEC", "Piping Spec", "PipeSpec" };
            foreach (var name in possibleNames)
            {
                if (mat.TryGetValue(name, out var val) && val != null)
                {
                    string s = val.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            return "";
        }

        // Build description for fabrication items (CUT/BEVEL)
        private static string BuildFabDescription(
            string size, string thickness, string pipeSpec, string material, string fabType)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(size)) parts.Add($"{size} IN");
            if (!string.IsNullOrEmpty(thickness)) parts.Add(thickness);
            if (!string.IsNullOrEmpty(pipeSpec)) parts.Add(pipeSpec);
            if (!string.IsNullOrEmpty(material)) parts.Add(material);
            parts.Add(fabType);

            return string.Join(" - ", parts);
        }

        // Build concatenated description for connection rows
        private static string BuildConcatDescription(
            string size, string thickness, string pipeSpec,
            string material, string connType)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(size)) parts.Add($"{size} IN");
            if (!string.IsNullOrEmpty(thickness)) parts.Add(thickness);
            if (!string.IsNullOrEmpty(pipeSpec)) parts.Add(pipeSpec);
            if (!string.IsNullOrEmpty(material)) parts.Add(material);
            if (!string.IsNullOrEmpty(connType)) parts.Add(connType);

            return string.Join(" - ", parts);
        }

        // Step C: Write Labor tab
        private static void WriteLaborTab(XLWorkbook workbook, List<Dictionary<string, object?>> laborRows)
        {
            // Remove existing Labor tab if present
            if (workbook.TryGetWorksheet("Labor", out _))
                workbook.Worksheets.Delete("Labor");

            var ws = workbook.Worksheets.Add("Labor");

            if (laborRows.Count == 0)
            {
                ws.Cell(1, 1).Value = "No labor rows generated";
                return;
            }

            // Define column order (explicit columns first, then dynamic title block fields)
            var explicitColumns = new List<string>
            {
                "Drawing Number", "Component", "Size", "Connection Size",
                "Thickness", "Class Rating", "Material",
                "Commodity Code", "Description", "Quantity",
                "ShopField", "ROCStep", "Confidence", "Flag", "BudgetMHs"
            };

            // Find any additional columns (title block fields)
            var allKeys = laborRows.SelectMany(r => r.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var additionalColumns = allKeys
                .Where(k => !explicitColumns.Contains(k, StringComparer.OrdinalIgnoreCase))
                .OrderBy(k => k)
                .ToList();

            var columns = explicitColumns.Concat(additionalColumns).ToList();

            // Write header
            for (int i = 0; i < columns.Count; i++)
                ws.Cell(1, i + 1).Value = columns[i];

            // Style header
            var headerRange = ws.Range(1, 1, 1, columns.Count);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#DAEEF3");

            // Write data
            for (int row = 0; row < laborRows.Count; row++)
            {
                var data = laborRows[row];
                for (int col = 0; col < columns.Count; col++)
                {
                    var value = data.GetValueOrDefault(columns[col]);
                    if (value != null)
                        ws.Cell(row + 2, col + 1).Value = XLCellValue.FromObject(value);
                }
            }

            // Auto-fit columns
            ws.Columns().AdjustToContents(1, 100);
            ws.SheetView.FreezeRows(1);
        }

        // Step D & E: Generate and write Summary tab
        private static void WriteSummaryTab(
            XLWorkbook workbook,
            List<Dictionary<string, object?>> materialRows,
            List<Dictionary<string, object?>> laborRows)
        {
            // Remove existing Summary tab if present
            if (workbook.TryGetWorksheet("Summary", out _))
                workbook.Worksheets.Delete("Summary");

            var ws = workbook.Worksheets.Add("Summary");
            var headerFill = XLColor.FromHtml("#C6EFCE");

            int row = 1;

            // Section: TAKEOFF SUMMARY
            WriteSectionHeader(ws, row++, "TAKEOFF SUMMARY", headerFill);

            var drawingNumbers = materialRows.Select(r => GetString(r, "Drawing Number")).Distinct().ToList();
            int shopCount = materialRows.Count(r => GetInt(r, "ShopField") == 1);
            int fieldCount = materialRows.Count(r => GetInt(r, "ShopField") == 2);
            int lowConf = materialRows.Count(r => GetString(r, "Confidence").Equals("low", StringComparison.OrdinalIgnoreCase));
            int medConf = materialRows.Count(r => GetString(r, "Confidence").Equals("medium", StringComparison.OrdinalIgnoreCase));

            WriteSummaryRow(ws, row++, "Total Drawings", drawingNumbers.Count);
            WriteSummaryRow(ws, row++, "Total BOM Items", materialRows.Count);
            WriteSummaryRow(ws, row++, "Total Connections", laborRows.Count);
            WriteSummaryRow(ws, row++, "Shop Items", shopCount);
            WriteSummaryRow(ws, row++, "Field Items", fieldCount);
            WriteSummaryRow(ws, row++, "Flagged Items", lowConf + medConf);
            WriteSummaryRow(ws, row++, "Low Confidence", lowConf);
            WriteSummaryRow(ws, row++, "Medium Confidence", medConf);
            row++;

            // Section: CONNECTIONS BY TYPE
            WriteSectionHeader(ws, row, "CONNECTIONS BY TYPE", headerFill);
            ws.Cell(row, 2).Value = "Count";
            ws.Cell(row, 2).Style.Fill.BackgroundColor = headerFill;
            ws.Cell(row, 2).Style.Font.Bold = true;
            row++;

            var byType = laborRows.GroupBy(r => GetString(r, "Component"))
                                  .OrderBy(g => g.Key);
            foreach (var g in byType)
                WriteSummaryRow(ws, row++, g.Key, g.Count());
            row++;

            // Section: CONNECTIONS BY SIZE
            WriteSectionHeader(ws, row, "CONNECTIONS BY SIZE", headerFill);
            ws.Cell(row, 2).Value = "Count";
            ws.Cell(row, 2).Style.Fill.BackgroundColor = headerFill;
            ws.Cell(row, 2).Style.Font.Bold = true;
            row++;

            var bySize = laborRows.GroupBy(r => GetString(r, "Connection Size"))
                                  .OrderBy(g => ParseSizeForSort(g.Key));
            foreach (var g in bySize)
                WriteSummaryRow(ws, row++, g.Key, g.Count());
            row++;

            // Section: COMPONENTS BY TYPE
            WriteSectionHeader(ws, row, "COMPONENTS BY TYPE", headerFill);
            ws.Cell(row, 2).Value = "Count";
            ws.Cell(row, 2).Style.Fill.BackgroundColor = headerFill;
            ws.Cell(row, 2).Style.Font.Bold = true;
            row++;

            var byComp = materialRows.GroupBy(r => GetString(r, "Component"))
                                     .OrderBy(g => g.Key);
            foreach (var g in byComp)
                WriteSummaryRow(ws, row++, g.Key, g.Count());
            row++;

            // Section: CONNECTIONS BY DRAWING
            WriteSectionHeader(ws, row, "CONNECTIONS BY DRAWING", headerFill);
            ws.Cell(row, 2).Value = "Connections";
            ws.Cell(row, 2).Style.Fill.BackgroundColor = headerFill;
            ws.Cell(row, 2).Style.Font.Bold = true;
            row++;

            var byDrawing = laborRows.GroupBy(r => GetString(r, "Drawing Number"))
                                     .OrderBy(g => g.Key);
            foreach (var g in byDrawing)
                WriteSummaryRow(ws, row++, g.Key, g.Count());

            ws.Column(1).AdjustToContents();
            ws.Column(2).AdjustToContents();
        }

        // Write a section header with green background
        private static void WriteSectionHeader(IXLWorksheet ws, int row, string text, XLColor fill)
        {
            ws.Cell(row, 1).Value = text;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = fill;
            ws.Cell(row, 1).Style.Font.Bold = true;
        }

        // Write a label/value row
        private static void WriteSummaryRow(IXLWorksheet ws, int row, string label, int value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
        }

        // Write Missed Makeups tab for fittings not found in the lookup table
        private static void WriteMissedMakeupsTab(XLWorkbook workbook)
        {
            if (workbook.TryGetWorksheet("Missed Makeups", out _))
                workbook.Worksheets.Delete("Missed Makeups");

            var ws = workbook.Worksheets.Add("Missed Makeups");

            var columns = new[] { "Drawing Number", "Component", "Size", "Connection Type", "Class Rating", "LookupKey", "Description" };

            // Header
            for (int i = 0; i < columns.Length; i++)
                ws.Cell(1, i + 1).Value = columns[i];

            var headerRange = ws.Range(1, 1, 1, columns.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FDE9D9");

            // Data rows
            for (int i = 0; i < _missedMakeups.Count; i++)
            {
                var m = _missedMakeups[i];
                int row = i + 2;
                ws.Cell(row, 1).Value = m.DrawingNumber;
                ws.Cell(row, 2).Value = m.Component;
                ws.Cell(row, 3).Value = m.Size;
                ws.Cell(row, 4).Value = m.ConnectionType;
                ws.Cell(row, 5).Value = m.ClassRating;
                ws.Cell(row, 6).Value = m.LookupKey;
                ws.Cell(row, 7).Value = m.Description;
            }

            ws.Columns().AdjustToContents(1, 100);
            ws.SheetView.FreezeRows(1);
        }

        // Helper: Get string value from row
        private static string GetString(Dictionary<string, object?> row, string key)
        {
            if (row.TryGetValue(key, out var val) && val != null)
                return val.ToString()?.Trim() ?? "";
            return "";
        }

        // Helper: Get nullable string value from row
        private static string? GetNullableString(Dictionary<string, object?> row, string key)
        {
            if (row.TryGetValue(key, out var val) && val != null)
            {
                string s = val.ToString()?.Trim() ?? "";
                return string.IsNullOrEmpty(s) ? null : s;
            }
            return null;
        }

        // Helper: Get int value from row
        private static int GetInt(Dictionary<string, object?> row, string key)
        {
            if (row.TryGetValue(key, out var val) && val != null)
            {
                if (val is int i) return i;
                if (val is double d) return (int)d;
                if (int.TryParse(val.ToString(), out int parsed)) return parsed;
            }
            return 0;
        }

        // Helper: Parse size string for sorting (e.g., "2" -> 2.0, "1/2" -> 0.5)
        private static double ParseSizeForSort(string size)
        {
            if (string.IsNullOrEmpty(size)) return 0;

            // Handle fractions like "1/2", "3/4"
            if (size.Contains('/'))
            {
                var parts = size.Split('/');
                if (parts.Length == 2 &&
                    double.TryParse(parts[0], out double num) &&
                    double.TryParse(parts[1], out double den) &&
                    den != 0)
                {
                    return num / den;
                }
            }

            if (double.TryParse(size, out double d))
                return d;

            return 0;
        }
    }
}
