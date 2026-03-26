using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace VANTAGE.Services.AI
{
    // Loads fitting makeup reference data and computes makeup lengths for pipe SPL (spool) records
    public static class FittingMakeupService
    {
        private static List<FittingMakeupEntry>? _entries;

        // Olet component types — always looked up as "SOL" with connection type "OLW"
        private static readonly HashSet<string> OletComponents = new(StringComparer.OrdinalIgnoreCase)
        {
            "SOL", "WOL", "TOL", "ELB", "LOL", "NOL"
        };

        // Components that share identical makeup values with another component
        private static readonly Dictionary<string, string> MakeupEquiv = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ADPT", "FLG" },
            { "FLGR", "FLG" }
        };

        // Lazy-load entries from embedded resource
        private static List<FittingMakeupEntry> LoadEntries()
        {
            if (_entries != null) return _entries;

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("VANTAGE.Resources.FittingMakeup.json");
            if (stream == null)
                throw new InvalidOperationException("Embedded resource VANTAGE.Resources.FittingMakeup.json not found");

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            _entries = JsonConvert.DeserializeObject<List<FittingMakeupEntry>>(json)
                ?? throw new InvalidOperationException("Failed to deserialize FittingMakeup.json");

            return _entries;
        }

        // Connection type fallback chain: GRV → SW → BW
        private static readonly string[][] ConnTypeFallbacks = new[]
        {
            new[] { "GRV", "SW", "BW" }
        };

        // Look up a fitting makeup entry. Returns (Makeup_Run_In, Makeup_Outlet_In) or null if not found.
        // Two-pass lookup: exact class match first, then fallback to wildcard (null class) entries.
        // Falls back to equivalent component (e.g., FLGR → FLG) only if direct lookup fails.
        // GRV connections fall back to SW, then BW if not found.
        public static (double RunIn, double? OutletIn)? LookupMakeup(
            string connType, string component, double runSize, string? classRating, double? outletSize = null)
        {
            // Try direct component lookup first
            var result = LookupMakeupCore(connType, component, runSize, classRating, outletSize);
            if (result != null) return result;

            // Fall back to equivalent component if one exists
            if (MakeupEquiv.TryGetValue(component, out string? equivComponent))
            {
                result = LookupMakeupCore(connType, equivComponent, runSize, classRating, outletSize);
                if (result != null) return result;
            }

            // Connection type fallback chain (e.g., GRV → SW → BW)
            foreach (var chain in ConnTypeFallbacks)
            {
                if (!connType.Equals(chain[0], StringComparison.OrdinalIgnoreCase)) continue;

                for (int i = 1; i < chain.Length; i++)
                {
                    result = LookupMakeupCore(chain[i], component, runSize, classRating, outletSize);
                    if (result != null) return result;

                    if (MakeupEquiv.TryGetValue(component, out string? equivComp))
                    {
                        result = LookupMakeupCore(chain[i], equivComp, runSize, classRating, outletSize);
                        if (result != null) return result;
                    }
                }
            }

            return null;
        }

        // Core lookup logic
        private static (double RunIn, double? OutletIn)? LookupMakeupCore(
            string connType, string component, double runSize, string? classRating, double? outletSize)
        {
            var entries = LoadEntries();

            // Filter to entries matching connection type, component, size, and outlet
            var candidates = entries.Where(e =>
                e.Connection_Type.Equals(connType, StringComparison.OrdinalIgnoreCase) &&
                e.Component.Equals(component, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(e.Run_Size - runSize) < 0.001 &&
                OutletMatches(e.Outlet_Size, outletSize)).ToList();

            // Pass 1: Try exact class match
            var match = candidates.FirstOrDefault(e => ClassMatchesExact(e.Class, classRating));

            // Pass 2: Fall back to wildcard entries (no class specified)
            match ??= candidates.FirstOrDefault(e => string.IsNullOrEmpty(e.Class));

            if (match == null) return null;
            return (match.Makeup_Run_In, match.Makeup_Outlet_In);
        }

        // Exact class match - both must have a value and match
        private static bool ClassMatchesExact(string? entryClass, string? lookupClass)
        {
            if (string.IsNullOrEmpty(entryClass) || string.IsNullOrEmpty(lookupClass)) return false;
            return entryClass.Equals(lookupClass, StringComparison.OrdinalIgnoreCase);
        }

        // If checking outlet size, both must match. If entry has no outlet, lookup must also have none.
        private static bool OutletMatches(double? entryOutlet, double? lookupOutlet)
        {
            if (entryOutlet == null && lookupOutlet == null) return true;
            if (entryOutlet == null || lookupOutlet == null) return false;
            return Math.Abs(entryOutlet.Value - lookupOutlet.Value) < 0.001;
        }

        // Calculate total fitting makeup inches for a pipe, given its size and same-drawing/same-size fittings
        // Returns (totalMakeupInches, missedFittings)
        public static (double TotalMakeupInches, List<MissedMakeup> Missed) CalculateFittingMakeupForPipe(
            double pipeSize, string? pipeClass, List<Dictionary<string, object?>> fittings)
        {
            double totalInches = 0;
            var missed = new List<MissedMakeup>();

            foreach (var fitting in fittings)
            {
                string component = GetString(fitting, "Component").ToUpper();
                string connTypes = GetString(fitting, "Connection Type");
                string? classRating = GetNullableString(fitting, "Class Rating");
                int qty = GetBomQuantity(fitting);

                // Skip BU-only fittings (flanged items with no weld connection to pipe)
                if (IsBoltUpOnly(connTypes))
                    continue;

                double? contribution = null;
                string lookupKey = "";

                if (OletComponents.Contains(component))
                {
                    // Olets: use actual component name and non-OLW connection type
                    // Fall back to Thickness if Class Rating is empty
                    string? oletClass = classRating;
                    if (string.IsNullOrEmpty(oletClass))
                        oletClass = GetNullableString(fitting, "Thickness");
                    var (runIn, key) = LookupOlet(component, connTypes, pipeSize, oletClass);
                    contribution = runIn;
                    lookupKey = key;
                }
                else if (component == "TEE")
                {
                    // TEE: 3x Makeup_Run_In
                    string weldType = ExtractWeldableType(connTypes);
                    var result = LookupMakeup(weldType, "TEE", pipeSize, classRating, pipeSize);
                    lookupKey = $"{weldType}/TEE/{pipeSize}" + (!string.IsNullOrEmpty(classRating) ? $"/Class{classRating}" : "");
                    if (result != null)
                        contribution = result.Value.RunIn * 3;
                }
                else if (component == "CROSS")
                {
                    // CROSS: 4x Makeup_Run_In
                    string weldType = ExtractWeldableType(connTypes);
                    var result = LookupMakeup(weldType, "CROSS", pipeSize, classRating, pipeSize);
                    lookupKey = $"{weldType}/CROSS/{pipeSize}" + (!string.IsNullOrEmpty(classRating) ? $"/Class{classRating}" : "");
                    if (result != null)
                        contribution = result.Value.RunIn * 4;
                }
                else if (component == "REDT")
                {
                    // REDT: parse dual size from Size field (e.g., "4x2")
                    string sizeStr = GetString(fitting, "Size");
                    var parsed = ParseDualSize(sizeStr);
                    string weldType = ExtractWeldableType(connTypes);
                    if (parsed != null)
                    {
                        var (largerSize, smallerSize) = parsed.Value;

                        // Same-size REDT is actually a TEE (e.g., "0.75x0.75" = standard tee)
                        if (Math.Abs(largerSize - smallerSize) < 0.001)
                        {
                            var result = LookupMakeup(weldType, "TEE", pipeSize, classRating, pipeSize);
                            lookupKey = $"{weldType}/TEE/{pipeSize}" + (!string.IsNullOrEmpty(classRating) ? $"/Class{classRating}" : "");
                            if (result != null)
                                contribution = result.Value.RunIn * 3;
                        }
                        else
                        {
                            var result = LookupMakeup(weldType, "REDT", largerSize, classRating, smallerSize);
                            lookupKey = $"{weldType}/REDT/{largerSize}x{smallerSize}" + (!string.IsNullOrEmpty(classRating) ? $"/Class{classRating}" : "");
                            if (result != null)
                            {
                                // If pipe matches run size, double run makeup (2 welds); if outlet size, single outlet makeup
                                if (Math.Abs(pipeSize - largerSize) < 0.001)
                                    contribution = result.Value.RunIn * 2;
                                else
                                    contribution = result.Value.OutletIn ?? 0;
                            }
                        }
                    }
                }
                else if (component == "90L" || component == "45L")
                {
                    // Elbows: 2x Makeup_Run_In (two welds)
                    string weldType = ExtractWeldableType(connTypes);
                    var result = LookupMakeup(weldType, component, pipeSize, classRating);
                    lookupKey = $"{weldType}/{component}/{pipeSize}" + (!string.IsNullOrEmpty(classRating) ? $"/Class{classRating}" : "");
                    if (result != null)
                        contribution = result.Value.RunIn * 2;
                }
                else if (component == "RED" || component == "SWG")
                {
                    // Reducing fittings: parse dual size, only count on larger size pipe, 1x makeup
                    string sizeStr = GetString(fitting, "Size");
                    var parsed = ParseDualSize(sizeStr);
                    string weldType = ExtractWeldableType(connTypes);
                    if (parsed != null && Math.Abs(pipeSize - parsed.Value.Larger) < 0.001)
                    {
                        var result = LookupMakeup(weldType, component, parsed.Value.Larger, classRating);
                        lookupKey = $"{weldType}/{component}/{parsed.Value.Larger}" + (!string.IsNullOrEmpty(classRating) ? $"/Class{classRating}" : "");
                        if (result != null)
                            contribution = result.Value.RunIn;
                    }
                    // Skip if pipe is the smaller size or parse failed - don't add to missed
                    else
                        contribution = 0;
                }
                else
                {
                    // Standard fitting (CAP, FLG, etc.) - single weld
                    string weldType = ExtractWeldableType(connTypes);
                    var result = LookupMakeup(weldType, component, pipeSize, classRating);
                    lookupKey = $"{weldType}/{component}/{pipeSize}" + (!string.IsNullOrEmpty(classRating) ? $"/Class{classRating}" : "");
                    if (result != null)
                        contribution = result.Value.RunIn;
                }

                if (contribution != null)
                {
                    totalInches += contribution.Value * qty;
                }
                else
                {
                    // Log missed fitting with lookup key
                    missed.Add(new MissedMakeup
                    {
                        DrawingNumber = GetString(fitting, "Drawing Number"),
                        Component = component,
                        Size = GetString(fitting, "Size"),
                        ConnectionType = connTypes,
                        ClassRating = GetString(fitting, "Class Rating"),
                        Description = GetString(fitting, "Raw Description"),
                        LookupKey = lookupKey,
                        Reason = "No Makeup Found"
                    });
                }
            }

            return (totalInches, missed);
        }

        // Olet lookup: use actual component name and find the non-OLW connection type
        private static (double? RunIn, string LookupKey) LookupOlet(string component, string connTypes, double pipeSize, string? classRating)
        {
            string weldType = ExtractWeldableType(connTypes, excludeOlw: true);
            var result = LookupMakeup(weldType, component, pipeSize, classRating);
            string key = $"{weldType}/{component}/{pipeSize}" + (!string.IsNullOrEmpty(classRating) ? $"/Class{classRating}" : "");
            return (result?.RunIn, key);
        }

        // Check if connection types are BU-only (no weldable connection)
        private static bool IsBoltUpOnly(string connTypes)
        {
            if (string.IsNullOrWhiteSpace(connTypes)) return false;

            var types = connTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim().ToUpper())
                                 .ToList();

            return types.All(t => t == "BU");
        }

        // Extract weldable connection type from comma-separated list (exclude BU for flanges, OLW for olets)
        private static string ExtractWeldableType(string connTypes, bool excludeOlw = false)
        {
            if (string.IsNullOrWhiteSpace(connTypes)) return "BW";

            var types = connTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim().ToUpper())
                                 .ToList();

            foreach (var t in types)
            {
                if (t == "BU") continue;
                if (excludeOlw && t == "OLW") continue;
                return t;
            }
            return types.FirstOrDefault() ?? "BW";
        }

        // Parse dual size (e.g., "6x1") and return both sizes (larger, smaller), or null if not dual format
        public static (double Larger, double Smaller)? ParseDualSize(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr)) return null;

            var parts = sizeStr.Split('x', 'X');
            if (parts.Length == 2 &&
                double.TryParse(parts[0].Trim(), out double size1) &&
                double.TryParse(parts[1].Trim(), out double size2))
            {
                return size1 >= size2 ? (size1, size2) : (size2, size1);
            }
            return null;
        }

        // Parse olet dual size (e.g., "6x1") and return the smaller size, or null if not dual
        public static double? ParseOletSmallSize(string sizeStr)
        {
            var parsed = ParseDualSize(sizeStr);
            return parsed?.Smaller;
        }

        // Check if a fitting is an olet type
        public static bool IsOlet(string component)
        {
            return OletComponents.Contains(component);
        }

        // Safe double extraction from material dictionary
        public static double GetDouble(Dictionary<string, object?> row, string key)
        {
            if (row.TryGetValue(key, out var val) && val != null)
            {
                if (val is double d) return d;
                string s = val.ToString()?.Trim() ?? "";
                s = s.TrimEnd('\'', '"');
                if (double.TryParse(s, out double parsed)) return parsed;
            }
            return 0;
        }

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

        private static string GetString(Dictionary<string, object?> row, string key)
        {
            if (row.TryGetValue(key, out var val) && val != null)
                return val.ToString()?.Trim() ?? "";
            return "";
        }

        private static string? GetNullableString(Dictionary<string, object?> row, string key)
        {
            if (row.TryGetValue(key, out var val) && val != null)
            {
                string s = val.ToString()?.Trim() ?? "";
                return string.IsNullOrEmpty(s) ? null : s;
            }
            return null;
        }

        // Get BOM quantity (integer, minimum 1)
        private static int GetBomQuantity(Dictionary<string, object?> row)
        {
            var val = row.GetValueOrDefault("Quantity");
            if (val == null) return 1;

            string s = val.ToString()?.Trim() ?? "1";
            s = s.TrimEnd('\'', '"');
            if (double.TryParse(s, out double d))
                return Math.Max(1, (int)Math.Floor(d));
            return 1;
        }

        // Parse pipe length in feet from Quantity field (e.g., "41.3'" → 41.3)
        public static double ParsePipeLengthFeet(Dictionary<string, object?> row)
        {
            var val = row.GetValueOrDefault("Quantity");
            if (val == null) return 0;

            string s = val.ToString()?.Trim() ?? "";
            s = s.TrimEnd('\'', '"');
            if (double.TryParse(s, out double d))
                return d;
            return 0;
        }
    }

    // JSON deserialization model for fitting makeup entries
    public class FittingMakeupEntry
    {
        public string Connection_Type { get; set; } = "";
        public string Component { get; set; } = "";
        public string? Class { get; set; }  // STD, XS, 160, 3000, 6000, etc.
        public double Run_Size { get; set; }
        public double? Outlet_Size { get; set; }
        public double Makeup_Run_In { get; set; }
        public double? Makeup_Outlet_In { get; set; }
    }

    // Tracks fittings that couldn't be found in the makeup table
    public class MissedMakeup
    {
        public string DrawingNumber { get; set; } = "";
        public string Component { get; set; } = "";
        public string Size { get; set; } = "";
        public string ConnectionType { get; set; } = "";
        public string ClassRating { get; set; } = "";
        public string Description { get; set; } = "";
        public string LookupKey { get; set; } = "";
        public string Reason { get; set; } = "";
    }
}
