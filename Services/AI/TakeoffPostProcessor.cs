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
        // Accumulates missed fitting lookups during SPL generation (reset each run)
        private static List<MissedMakeup> _missedMakeups = new();

        // Accumulates missed rate lookups during rate application (reset each run)
        private static List<MissedRate> _missedRates = new();

        // Accumulates material items that had no connections to explode (reset each run)
        private static List<Dictionary<string, object?>> _noConns = new();

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

        // Material group multipliers for labor MH calculations
        private static readonly Dictionary<string, double> MaterialMultipliers = new(StringComparer.OrdinalIgnoreCase)
        {
            { "CS", 1.000 }, { "CR1", 1.230 }, { "CR2", 1.310 }, { "CR3", 1.415 },
            { "SS", 1.425 }, { "CU", 1.200 }, { "A333", 1.500 }, { "HAST", 2.000 },
            { "SSX", 1.435 }, { "AL", 1.640 }, { "HDPE", 0.500 }, { "PVC", 0.330 },
            { "FRP", 0.700 }, { "CLINED", 1.050 }, { "RLINED", 1.050 }, { "NVD", 1.050 }
        };

        // Returns the material multiplier for a Matl_Grp, defaults to 1.0 if not found
        private static double GetMaterialMultiplier(string matlGrp)
        {
            return MaterialMultipliers.TryGetValue(matlGrp, out double mult) ? mult : 1.0;
        }

        // Returns the rollup multiplier based on component type
        private static double GetRollupMultiplier(string component)
        {
            if (component.Equals("PIPE", StringComparison.OrdinalIgnoreCase)) return 1.4;
            if (component.Equals("SPL", StringComparison.OrdinalIgnoreCase)) return 1.4;
            if (component.Equals("BW", StringComparison.OrdinalIgnoreCase)) return 1.25;
            if (component.Equals("SW", StringComparison.OrdinalIgnoreCase)) return 1.25;
            if (component.Equals("FW", StringComparison.OrdinalIgnoreCase)) return 1.35;
            return 1.0;
        }

        // Components to exclude from fitting makeup lookup (non-weldable or not tracked)
        private static readonly HashSet<string> ExcludeFromMakeupLookup = new(StringComparer.OrdinalIgnoreCase)
        {
            "FS", "GSKT", "BOLT", "WAS", "HEAT", "HOSE", "INST", "NIP", "PLG", "SAFSHW", "F8B", "DPAN", "ACT", "FLGB", "PIPET", "FLGLJ", "GAUGE"
        };

        // Main entry point - processes the downloaded Excel file in place
        // Returns counts of missed makeups and missed rates for caller use
        // Optional projectRateCache provides per-project rate overrides
        public static (int MissedMakeups, int MissedRates) GenerateLaborAndSummary(
            string excelPath, Dictionary<string, (double MH, string Unit)>? projectRateCache = null,
            List<string>? failedDrawings = null)
        {
            try
            {
                using var workbook = new XLWorkbook(excelPath);

                // Reset missed lookups for this run
                _missedMakeups = new List<MissedMakeup>();
                _missedRates = new List<MissedRate>();
                _noConns = new List<Dictionary<string, object?>>();

                // Step A: Read Material tab
                var materialRows = ReadMaterialTab(workbook);
                AppLogger.Info($"Read {materialRows.Count} material rows", "TakeoffPostProcessor");

                // Step A2: Normalize TEE sizes (convert single sizes like "4" to "4x4")
                NormalizeTeeSizes(materialRows);

                // Step B: Generate Labor rows
                var laborRows = GenerateLaborRows(materialRows);
                AppLogger.Info($"Generated {laborRows.Count} labor rows", "TakeoffPostProcessor");

                // Step C: Apply rates to labor rows (with optional project overrides)
                ApplyRates(laborRows, projectRateCache);

                // Step D: Write Labor tab
                WriteLaborTab(workbook, laborRows);

                // Step E & F: Generate and write Summary tab
                WriteSummaryTab(workbook, materialRows, laborRows);

                // Step G: Write or remove Missed Makeups tab
                if (_missedMakeups.Count > 0)
                {
                    WriteMissedMakeupsTab(workbook);
                    AppLogger.Info($"Logged {_missedMakeups.Count} missed fitting makeups", "TakeoffPostProcessor");
                }
                else if (workbook.TryGetWorksheet("Missed Makeups", out _))
                {
                    workbook.Worksheets.Delete("Missed Makeups");
                }

                // Step H: Write or remove Missed Rates tab
                if (_missedRates.Count > 0)
                {
                    WriteMissedRatesTab(workbook);
                    AppLogger.Info($"Logged {_missedRates.Count} missed rate lookups", "TakeoffPostProcessor");
                }
                else if (workbook.TryGetWorksheet("Missed Rates", out _))
                {
                    workbook.Worksheets.Delete("Missed Rates");
                }

                // Step I: Write or remove No Conns tab
                if (_noConns.Count > 0)
                {
                    WriteNoConnsTab(workbook);
                    AppLogger.Info($"Logged {_noConns.Count} material items with no connections", "TakeoffPostProcessor");
                }
                else if (workbook.TryGetWorksheet("No Conns", out _))
                {
                    workbook.Worksheets.Delete("No Conns");
                }

                // Step J: Write or remove Failed DWGs tab
                if (failedDrawings != null && failedDrawings.Count > 0)
                {
                    WriteFailedDrawingsTab(workbook, failedDrawings);
                    AppLogger.Info($"Logged {failedDrawings.Count} failed drawing(s)", "TakeoffPostProcessor");
                }
                else if (workbook.TryGetWorksheet("Failed DWGs", out _))
                {
                    workbook.Worksheets.Delete("Failed DWGs");
                }

                // Step K: Reorder tabs
                ReorderTabs(workbook);

                // Save
                workbook.Save();
                AppLogger.Info($"Saved workbook with Labor and Summary tabs", "TakeoffPostProcessor");

                return (_missedMakeups.Count, _missedRates.Count);
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
            var desiredOrder = new[] { "Summary", "Material", "Labor", "Flagged", "Failed DWGs", "Missed Makeups", "Missed Rates", "No Conns" };
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

            // Build column name map from header row, keeping only the first occurrence
            // of each column name. BOM columns come before Title Block columns, so this
            // ensures BOM fields (like "Size") aren't overwritten by Title Block fields.
            var columnMap = new Dictionary<int, string>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int col = 1; col <= lastCol; col++)
            {
                string header = ws.Cell(1, col).GetString().Trim();
                if (!string.IsNullOrEmpty(header) && !seenNames.Contains(header))
                {
                    columnMap[col] = header;
                    seenNames.Add(header);
                }
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

        // Step A2: Normalize TEE sizes - convert single sizes (e.g., "4") to dual format (e.g., "4x4")
        // This must run before any makeup or rate lookups since those depend on proper size format
        private static void NormalizeTeeSizes(List<Dictionary<string, object?>> materialRows)
        {
            foreach (var row in materialRows)
            {
                string component = GetString(row, "Component").ToUpper();
                if (component != "TEE") continue;

                string sizeStr = GetString(row, "Size").Trim();
                if (string.IsNullOrEmpty(sizeStr)) continue;

                // Skip if already in AxB format (contains 'x' or 'X')
                if (sizeStr.Contains('x', StringComparison.OrdinalIgnoreCase)) continue;

                // Check if it's a single number
                if (double.TryParse(sizeStr, out double size))
                {
                    // Convert to AxA format
                    string normalized = $"{sizeStr}x{sizeStr}";
                    row["Size"] = normalized;
                }
            }
        }

        // Step B: Generate Labor rows (one per connection, exploded) + SPL records
        private static List<Dictionary<string, object?>> GenerateLaborRows(
            List<Dictionary<string, object?>> materialRows)
        {
            var laborRows = new List<Dictionary<string, object?>>();

            foreach (var mat in materialRows)
            {
                var rows = ExplodeMaterialRow(mat, materialRows);
                laborRows.AddRange(rows);
            }

            // Generate SPL (Spool Handling) records for PIPE items with fitting makeup
            var splRows = GenerateSplRows(materialRows);
            laborRows.AddRange(splRows);

            return laborRows;
        }

        // Generate SPL (Spool) records by computing fitting makeup for each pipe
        private static List<Dictionary<string, object?>> GenerateSplRows(
            List<Dictionary<string, object?>> materialRows)
        {
            var splRows = new List<Dictionary<string, object?>>();

            // Group material rows by Drawing Number
            var byDrawing = materialRows.GroupBy(r => GetString(r, "Drawing Number"));

            foreach (var drawingGroup in byDrawing)
            {
                var rows = drawingGroup.ToList();
                var pipeRows = rows.Where(r => GetString(r, "Component").Equals("PIPE", StringComparison.OrdinalIgnoreCase)).ToList();
                var fittingRows = rows.Where(r => !GetString(r, "Component").Equals("PIPE", StringComparison.OrdinalIgnoreCase)).ToList();

                // Track which fittings get claimed by at least one pipe
                var claimedFittings = new HashSet<Dictionary<string, object?>>();
                // Map each pipe to its SPL row so pass 2 can update quantities
                var pipeToSpl = new Dictionary<Dictionary<string, object?>, Dictionary<string, object?>>();

                foreach (var pipe in pipeRows)
                {
                    double pipeSize = FittingMakeupService.GetDouble(pipe, "Size");
                    string pipeMaterial = GetString(pipe, "Matl_Grp");
                    string? pipeClass = GetNullableString(pipe, "Class Rating");

                    // Find fittings on same drawing with matching size AND material
                    var matchingFittings = new List<Dictionary<string, object?>>();
                    foreach (var fitting in fittingRows)
                    {
                        string fittingComponent = GetString(fitting, "Component").ToUpper();

                        // Skip non-weldable components
                        if (ExcludeFromMakeupLookup.Contains(fittingComponent))
                            continue;

                        string fittingMaterial = GetString(fitting, "Matl_Grp");

                        // Material must match
                        if (!fittingMaterial.Equals(pipeMaterial, StringComparison.OrdinalIgnoreCase))
                            continue;

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
                        else if (fittingComponent == "REDT" || fittingComponent == "TEE")
                        {
                            // REDT/TEE: parse dual size (e.g., "4x2" or "4x4"), match if pipe equals either size
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
                        else if (fittingComponent == "RED" || fittingComponent == "SWG")
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

                    // Calculate fitting makeup
                    var (totalMakeupInches, missed) = FittingMakeupService.CalculateFittingMakeupForPipe(
                        pipeSize, pipeClass, matchingFittings);

                    // Collect missed makeups for the tab
                    _missedMakeups.AddRange(missed);

                    // Get pipe length in feet
                    double pipeLengthFeet = FittingMakeupService.ParsePipeLengthFeet(pipe);

                    // SPL Quantity = pipe length (ft) + (total makeup inches / 12)
                    double splQuantity = pipeLengthFeet + (totalMakeupInches / 12.0);
                    if (splQuantity <= 0) continue;

                    // Create SPL row (spool handling for field-installed fabricated spools)
                    var spl = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (key, value) in pipe)
                    {
                        if (!ExcludeFromLabor.Contains(key))
                            spl[key] = value;
                    }
                    spl["Component"] = "SPL";
                    spl["Quantity"] = Math.Round(splQuantity, 3);
                    spl["Description"] = GetString(pipe, "Raw Description") + " - Spool Handling";
                    spl["BudgetMHs"] = null;

                    splRows.Add(spl);
                    pipeToSpl[pipe] = spl;
                }

                // Pass 2: unclaimed RED/SWG — try matching on smaller size
                foreach (var fitting in fittingRows)
                {
                    if (claimedFittings.Contains(fitting)) continue;
                    string comp = GetString(fitting, "Component").ToUpper();
                    if (comp != "RED" && comp != "SWG") continue;

                    string sizeStr = GetString(fitting, "Size");
                    var parsed = FittingMakeupService.ParseDualSize(sizeStr);
                    if (parsed == null) continue;

                    string fittingMaterial = GetString(fitting, "Matl_Grp");
                    int qty = Math.Max(1, (int)FittingMakeupService.GetDouble(fitting, "Quantity"));

                    foreach (var pipe in pipeRows)
                    {
                        if (!pipeToSpl.ContainsKey(pipe)) continue;
                        double pipeSize = FittingMakeupService.GetDouble(pipe, "Size");
                        string pipeMaterial = GetString(pipe, "Matl_Grp");

                        if (!fittingMaterial.Equals(pipeMaterial, StringComparison.OrdinalIgnoreCase)) continue;
                        if (Math.Abs(parsed.Value.Smaller - pipeSize) > 0.001) continue;

                        // Matched on smaller size — look up makeup by larger size
                        string connTypes = GetString(fitting, "Connection Type");
                        string? classRating = GetNullableString(fitting, "Class Rating");
                        string weldType = connTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim().ToUpper())
                            .FirstOrDefault(t => t != "BU") ?? "BW";
                        var result = FittingMakeupService.LookupMakeup(weldType, comp, parsed.Value.Larger, classRating);

                        if (result != null)
                        {
                            double extraInches = result.Value.RunIn * qty;
                            var spl = pipeToSpl[pipe];
                            double currentQty = FittingMakeupService.GetDouble(spl, "Quantity");
                            spl["Quantity"] = Math.Round(currentQty + (extraInches / 12.0), 3);
                        }
                        else
                        {
                            string lookupKey = $"{weldType}/{comp}/{parsed.Value.Larger}" +
                                (!string.IsNullOrEmpty(classRating) ? $"/Class{classRating}" : "");
                            _missedMakeups.Add(new MissedMakeup
                            {
                                DrawingNumber = GetString(fitting, "Drawing Number"),
                                Component = comp,
                                Size = sizeStr,
                                ConnectionType = connTypes,
                                ClassRating = classRating ?? "",
                                Description = GetString(fitting, "Raw Description"),
                                LookupKey = lookupKey,
                                Reason = "No Makeup Found (fallback to smaller pipe)"
                            });
                        }

                        claimedFittings.Add(fitting);
                        break; // Claimed by first matching pipe
                    }
                }

                // Log fittings on this drawing that weren't claimed by any pipe
                foreach (var fitting in fittingRows)
                {
                    if (claimedFittings.Contains(fitting)) continue;

                    string component = GetString(fitting, "Component").ToUpper();
                    string reason = ExcludeFromMakeupLookup.Contains(component) ? "Excluded" : "Unclaimed";

                    _missedMakeups.Add(new MissedMakeup
                    {
                        DrawingNumber = GetString(fitting, "Drawing Number"),
                        Component = component,
                        Size = GetString(fitting, "Size"),
                        ConnectionType = GetString(fitting, "Connection Type"),
                        ClassRating = GetString(fitting, "Class Rating"),
                        Description = GetString(fitting, "Raw Description"),
                        Reason = reason
                    });
                }
            }

            return splRows;
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
            Dictionary<string, object?> mat,
            List<Dictionary<string, object?>>? allMaterialRows = null)
        {
            var result = new List<Dictionary<string, object?>>();

            string component = GetString(mat, "Component").ToUpper();
            int quantity = ParseQuantity(mat);
            string sizeStr = GetString(mat, "Size");
            string thickness = GetString(mat, "Thickness");
            string pipeSpec = FindPipeSpec(mat);
            string material = GetString(mat, "Matl_Grp");
            string commodityCode = GetString(mat, "Commodity Code");
            string rawDesc = GetString(mat, "Raw Description");

            // Parse dual size if present (e.g., "4x2" → larger=4, smaller=2)
            var dualSize = FittingMakeupService.ParseDualSize(sizeStr);
            double largerSize = dualSize?.Larger ?? FittingMakeupService.GetDouble(mat, "Size");
            double smallerSize = dualSize?.Smaller ?? largerSize;
            bool isDualSize = dualSize != null;

            // Hardware items (BOLT, GSKT, WAS): one aggregated labor row per material row
            // Preserves original quantity for MH calculation (rate × qty)
            if (component == "BOLT" || component == "GSKT" || component == "WAS")
            {
                var hardware = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in mat)
                {
                    if (!ExcludeFromLabor.Contains(key))
                        hardware[key] = value;
                }
                hardware["Quantity"] = quantity;
                hardware["Description"] = string.IsNullOrEmpty(commodityCode)
                    ? rawDesc
                    : $"{rawDesc} - {commodityCode}";
                hardware["BudgetMHs"] = null;
                result.Add(hardware);
                return result;
            }

            // PIPE items: one PIPE fab labor record per BOM item
            if (component.Equals("PIPE", StringComparison.OrdinalIgnoreCase))
            {
                var pipeFab = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in mat)
                {
                    if (!ExcludeFromLabor.Contains(key))
                        pipeFab[key] = value;
                }
                pipeFab["Component"] = "PIPE";
                pipeFab["Quantity"] = ParseExactQuantity(mat);
                pipeFab["Description"] = rawDesc + " - Fab Pipe Handling";
                pipeFab["BudgetMHs"] = null;
                result.Add(pipeFab);
            }
            // FLGLJ and FLGB don't get fab records — labor is covered by their BU connection row
            else if (component != "FLGLJ" && component != "FLGB")
            {
                // Non-PIPE items: add fab record with original component and raw description
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

                    // FS (field supports): use Commodity Code as Class Rating for rate lookup
                    // Allows lookup as SPT-{size}:{commodityCode} with fallback to SPT-{size}
                    if (component == "FS" && !string.IsNullOrEmpty(commodityCode))
                        fab["Class Rating"] = commodityCode;

                    result.Add(fab);
                }
            }
            // else: FLGLJ/FLGB — skip fab record, continue to connection explosion

            // NIP and PLG don't create connection rows — their connections are
            // always to another fitting and get counted from that fitting instead
            if (component == "NIP" || component == "PLG")
            {
                _noConns.Add(mat);
                return result;
            }

            // Connection explosion (all items with connections)
            int connQty = GetInt(mat, "Connection Qty");
            if (connQty <= 0)
            {
                if (!component.Equals("PIPE", StringComparison.OrdinalIgnoreCase))
                    _noConns.Add(mat);
                return result;
            }

            string connTypes = GetString(mat, "Connection Type");
            if (string.IsNullOrWhiteSpace(connTypes))
            {
                if (!component.Equals("PIPE", StringComparison.OrdinalIgnoreCase))
                    _noConns.Add(mat);
                return result;
            }

            // Parse connection types (may be comma-separated like "BW, SCRD")
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
                    // SPT connections don't get labor rows — MHs come from the FS fab record
                    if (connType == "SPT") continue;

                    var labor = CreateLaborRow(mat, connType, connSize, thickness, pipeSpec, material, allMaterialRows);
                    result.Add(labor);

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

            // FLG types with 2 connections: BU (bolt side) + other type (typically BW)
            if (component == "FLG" || component == "FLGO")
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

        // Create a labor row for a connection.
        // All connection rows must have a thickness. If the source item has none,
        // check the matching PIPE entry. If no pipe found, default to "40".
        private static Dictionary<string, object?> CreateLaborRow(
            Dictionary<string, object?> mat, string connType, double size,
            string thickness, string pipeSpec, string material,
            List<Dictionary<string, object?>>? allMaterialRows = null)
        {
            // Ensure thickness is populated
            if (string.IsNullOrWhiteSpace(thickness) && allMaterialRows != null)
            {
                var matchingPipe = FindMatchingPipe(allMaterialRows, mat, size);
                if (matchingPipe != null)
                    thickness = GetString(matchingPipe, "Thickness");
            }
            if (string.IsNullOrWhiteSpace(thickness))
                thickness = "40";

            var labor = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in mat)
            {
                if (!ExcludeFromLabor.Contains(key))
                    labor[key] = value;
            }

            string sizeStr = size.ToString("0.###");
            string classRating = GetString(mat, "Class Rating");
            labor["Component"] = connType;
            labor["Size"] = sizeStr;
            labor["Thickness"] = thickness;
            labor["Quantity"] = 1;
            labor["ShopField"] = connType == "BU" ? 2 : 1;
            string descSuffix = connType == "BU" ? "BU - One Flange" : connType;
            labor["Description"] = BuildConcatDescription(sizeStr, thickness, classRating, pipeSpec, material, descSuffix);
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
            string classRating = GetString(mat, "Class Rating");
            fab["Component"] = fabType;
            fab["Size"] = sizeStr;
            fab["Quantity"] = 1;
            fab["ShopField"] = 1;
            fab["Description"] = BuildFabDescription(sizeStr, thickness, classRating, pipeSpec, material, fabType);
            fab["BudgetMHs"] = null;

            return fab;
        }

        // Find a PIPE material row matching the connection size and material on the same drawing.
        // Used to inherit thickness/class for CUT and BEV records.
        private static Dictionary<string, object?>? FindMatchingPipe(
            List<Dictionary<string, object?>>? allMaterialRows,
            Dictionary<string, object?> fittingRow,
            double connSize)
        {
            if (allMaterialRows == null) return null;

            string drawingNumber = GetString(fittingRow, "Drawing Number");
            string fittingMaterial = GetString(fittingRow, "Matl_Grp");
            string connSizeStr = connSize.ToString("0.###");

            foreach (var row in allMaterialRows)
            {
                string comp = GetString(row, "Component").ToUpper();
                if (comp != "PIPE") continue;

                // Same drawing
                if (!GetString(row, "Drawing Number").Equals(drawingNumber, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Matching size
                string pipeSize = GetString(row, "Size");
                if (!pipeSize.Equals(connSizeStr, StringComparison.OrdinalIgnoreCase)
                    && !pipeSize.Equals(connSize.ToString("0"), StringComparison.OrdinalIgnoreCase))
                    continue;

                // Matching material
                string pipeMaterial = GetString(row, "Matl_Grp");
                if (!pipeMaterial.Equals(fittingMaterial, StringComparison.OrdinalIgnoreCase))
                    continue;

                return row;
            }

            return null;
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

        // Parse quantity preserving decimal value (for PIPE fab labor rows)
        private static double ParseExactQuantity(Dictionary<string, object?> mat)
        {
            var qtyVal = mat.GetValueOrDefault("Quantity");
            if (qtyVal == null) return 1;

            string qtyStr = qtyVal.ToString()?.Trim() ?? "1";
            qtyStr = qtyStr.TrimEnd('\'', '"');

            if (double.TryParse(qtyStr, out double d))
                return Math.Max(0.001, d);

            return 1;
        }

        // Find pipe spec from title block fields (various naming conventions)
        private static string FindPipeSpec(Dictionary<string, object?> mat)
        {
            foreach (var (key, val) in mat)
            {
                if (val == null) continue;
                if (key.Contains("spec", StringComparison.OrdinalIgnoreCase))
                {
                    string s = val.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }
            return "";
        }

        // Build description for fabrication items (CUT/BEVEL)
        private static string BuildFabDescription(
            string size, string thickness, string classRating,
            string pipeSpec, string material, string fabType)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(size)) parts.Add($"{size} IN");
            if (!string.IsNullOrEmpty(thickness)) parts.Add(thickness);
            if (!string.IsNullOrEmpty(classRating)) parts.Add(classRating);
            if (!string.IsNullOrEmpty(pipeSpec)) parts.Add(pipeSpec);
            if (!string.IsNullOrEmpty(material)) parts.Add(material);
            parts.Add(fabType);

            return string.Join(" - ", parts);
        }

        // Build concatenated description for connection rows
        private static string BuildConcatDescription(
            string size, string thickness, string classRating,
            string pipeSpec, string material, string connType)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(size)) parts.Add($"{size} IN");
            if (!string.IsNullOrEmpty(thickness)) parts.Add(thickness);
            if (!string.IsNullOrEmpty(classRating)) parts.Add(classRating);
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
                "Thickness", "Class Rating", "Matl_Grp",
                "Commodity Code", "Description", "Quantity",
                "ShopField", "ROCStep", "Confidence", "Flag", "RateSheet", "RollupMult", "MatlMult", "CutAdd", "BevelAdd", "BudgetMHs", "UOM", "RateSource"
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

            var columns = new[] { "Drawing Number", "Component", "Size", "Connection Type", "Class Rating", "LookupKey", "Reason", "Description" };

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
                ws.Cell(row, 7).Value = m.Reason;
                ws.Cell(row, 8).Value = m.Description;
            }

            ws.Columns().AdjustToContents(1, 100);
            ws.SheetView.FreezeRows(1);
        }

        // Apply rates from rate sheet to labor rows, setting BudgetMHs = Quantity × FLD_MHU
        // Optional projectRateCache provides per-project rate overrides (falls back to default)
        private static void ApplyRates(List<Dictionary<string, object?>> laborRows,
            Dictionary<string, (double MH, string Unit)>? projectRateCache = null)
        {
            int matched = 0;
            foreach (var row in laborRows)
            {
                string component = GetString(row, "Component");
                string size = GetString(row, "Size");
                string? thickness = GetNullableString(row, "Thickness");
                string? classRating = GetNullableString(row, "Class Rating");

                // Default class for rate lookup when none specified
                if (string.IsNullOrWhiteSpace(classRating))
                {
                    string connType = GetString(row, "Connection Type");
                    if (connType.Equals("SW", StringComparison.OrdinalIgnoreCase))
                        classRating = "3000";
                    else if (component.Equals("BU", StringComparison.OrdinalIgnoreCase))
                        classRating = "150";
                }

                var (fldMhu, unit, rateSource, keyAttempted) = RateSheetService.FindRateWithProjectOverride(
                    projectRateCache, component, size, thickness, classRating);

                if (fldMhu.HasValue)
                {
                    // Use double for quantity — PIPE fab rows have feet values < 1
                    double qty = GetNullableDouble(row, "Quantity") ?? 1;
                    double mhu = fldMhu.Value;

                    // BU rows are created for each flanged end, so every bolt-up joint
                    // generates 2 rows. Halve the rate so the total per joint is correct.
                    if (component.Equals("BU", StringComparison.OrdinalIgnoreCase))
                        mhu /= 2;

                    // Rollup and material multipliers
                    double rollupMult = GetRollupMultiplier(component);
                    string matlGrp = GetString(row, "Matl_Grp");
                    double matlMult = GetMaterialMultiplier(matlGrp);
                    row["RateSheet"] = mhu;
                    row["RollupMult"] = rollupMult;
                    row["MatlMult"] = matlMult;

                    // BW: add CUT + BEV rates (unmultiplied). SW/SCRD: add CUT rate only.
                    double cutAdd = 0;
                    double bevAdd = 0;
                    if (component.Equals("BW", StringComparison.OrdinalIgnoreCase))
                    {
                        var cutRate = RateSheetService.FindRate("CUT", size, thickness, classRating);
                        var bevRate = RateSheetService.FindRate("BEV", size, thickness, classRating);
                        if (cutRate.FldMhu.HasValue) cutAdd = cutRate.FldMhu.Value;
                        if (bevRate.FldMhu.HasValue) bevAdd = bevRate.FldMhu.Value;
                    }
                    else if (component.Equals("SW", StringComparison.OrdinalIgnoreCase) ||
                             component.Equals("SCRD", StringComparison.OrdinalIgnoreCase))
                    {
                        var cutRate = RateSheetService.FindRate("CUT", size, thickness, classRating);
                        if (cutRate.FldMhu.HasValue) cutAdd = cutRate.FldMhu.Value;
                    }
                    row["CutAdd"] = cutAdd;
                    row["BevelAdd"] = bevAdd;

                    row["BudgetMHs"] = NumericHelper.RoundToPlaces((mhu * rollupMult * matlMult + cutAdd + bevAdd) * qty);
                    row["UOM"] = unit;
                    row["RateSource"] = rateSource;
                    matched++;
                }
                else
                {
                    _missedRates.Add(new MissedRate
                    {
                        DrawingNumber = GetString(row, "Drawing Number"),
                        Component = component,
                        Size = size,
                        Thickness = thickness ?? "",
                        ClassRating = classRating ?? "",
                        LookupKey = keyAttempted,
                        Description = GetString(row, "Description")
                    });
                }
            }

            AppLogger.Info($"Rate application: {matched}/{laborRows.Count} matched, {_missedRates.Count} missed", "TakeoffPostProcessor");
        }

        // Write Missed Rates tab
        private static void WriteMissedRatesTab(XLWorkbook workbook)
        {
            if (workbook.TryGetWorksheet("Missed Rates", out _))
                workbook.Worksheets.Delete("Missed Rates");

            var ws = workbook.Worksheets.Add("Missed Rates");

            var columns = new[] { "Drawing Number", "Component", "Size", "Thickness", "Class Rating", "LookupKey", "Description" };

            // Header
            for (int i = 0; i < columns.Length; i++)
                ws.Cell(1, i + 1).Value = columns[i];

            var headerRange = ws.Range(1, 1, 1, columns.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FDE9D9");

            // Data rows
            for (int i = 0; i < _missedRates.Count; i++)
            {
                var m = _missedRates[i];
                int row = i + 2;
                ws.Cell(row, 1).Value = m.DrawingNumber;
                ws.Cell(row, 2).Value = m.Component;
                ws.Cell(row, 3).Value = m.Size;
                ws.Cell(row, 4).Value = m.Thickness;
                ws.Cell(row, 5).Value = m.ClassRating;
                ws.Cell(row, 6).Value = m.LookupKey;
                ws.Cell(row, 7).Value = m.Description;
            }

            ws.Columns().AdjustToContents(1, 100);
            ws.SheetView.FreezeRows(1);
        }

        // Write No Conns tab — material items that had no connections to explode
        private static void WriteNoConnsTab(XLWorkbook workbook)
        {
            if (workbook.TryGetWorksheet("No Conns", out _))
                workbook.Worksheets.Delete("No Conns");

            var ws = workbook.Worksheets.Add("No Conns");

            var columns = new[] { "Drawing Number", "Component", "Size", "Quantity", "Thickness", "Class Rating", "Matl_Grp", "Connection Qty", "Connection Type", "Raw Description" };

            // Header
            for (int i = 0; i < columns.Length; i++)
                ws.Cell(1, i + 1).Value = columns[i];

            var headerRange = ws.Range(1, 1, 1, columns.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E2F3");

            // Data rows
            for (int i = 0; i < _noConns.Count; i++)
            {
                var m = _noConns[i];
                int row = i + 2;
                ws.Cell(row, 1).Value = GetString(m, "Drawing Number");
                ws.Cell(row, 2).Value = GetString(m, "Component");
                ws.Cell(row, 3).Value = GetString(m, "Size");
                ws.Cell(row, 4).Value = GetString(m, "Quantity");
                ws.Cell(row, 5).Value = GetString(m, "Thickness");
                ws.Cell(row, 6).Value = GetString(m, "Class Rating");
                ws.Cell(row, 7).Value = GetString(m, "Matl_Grp");
                ws.Cell(row, 8).Value = GetString(m, "Connection Qty");
                ws.Cell(row, 9).Value = GetString(m, "Connection Type");
                ws.Cell(row, 10).Value = GetString(m, "Raw Description");
            }

            ws.Columns().AdjustToContents(1, 100);
            ws.SheetView.FreezeRows(1);
        }

        // Write Failed DWGs tab — drawings that failed AI extraction
        private static void WriteFailedDrawingsTab(XLWorkbook workbook, List<string> failedDrawings)
        {
            if (workbook.TryGetWorksheet("Failed DWGs", out _))
                workbook.Worksheets.Delete("Failed DWGs");

            var ws = workbook.Worksheets.Add("Failed DWGs");

            // Header
            ws.Cell(1, 1).Value = "Drawing File";
            var headerRange = ws.Range(1, 1, 1, 1);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F4CCCC");

            // Data rows
            for (int i = 0; i < failedDrawings.Count; i++)
                ws.Cell(i + 2, 1).Value = failedDrawings[i];

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
