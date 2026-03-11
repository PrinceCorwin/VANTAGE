using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace VANTAGE.Services.AI
{
    // Loads fitting makeup reference data and computes makeup lengths for pipe FRH records
    public static class FittingMakeupService
    {
        private static List<FittingMakeupEntry>? _entries;

        // Olet component types — always looked up as "SOL" with connection type "OLW"
        private static readonly HashSet<string> OletComponents = new(StringComparer.OrdinalIgnoreCase)
        {
            "SOL", "WOL", "TOL", "ELB", "LOL"
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

        // Look up a fitting makeup entry. Returns (Makeup_Run_In, Makeup_Outlet_In) or null if not found.
        public static (double RunIn, double? OutletIn)? LookupMakeup(
            string connType, string component, double runSize, double? classRating, double? outletSize = null)
        {
            var entries = LoadEntries();

            var match = entries.FirstOrDefault(e =>
                e.Connection_Type.Equals(connType, StringComparison.OrdinalIgnoreCase) &&
                e.Component.Equals(component, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(e.Run_Size - runSize) < 0.001 &&
                ClassMatches(e.Class, classRating) &&
                OutletMatches(e.Outlet_Size, outletSize));

            if (match == null) return null;
            return (match.Makeup_Run_In, match.Makeup_Outlet_In);
        }

        // If the JSON entry has a Class value, it must match the provided class.
        // If the JSON entry has no Class (null), it matches any class.
        private static bool ClassMatches(double? entryClass, double? lookupClass)
        {
            if (entryClass == null) return true;
            if (lookupClass == null) return false;
            return Math.Abs(entryClass.Value - lookupClass.Value) < 0.001;
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
            double pipeSize, double? pipeClass, List<Dictionary<string, object?>> fittings)
        {
            double totalInches = 0;
            var missed = new List<MissedMakeup>();

            foreach (var fitting in fittings)
            {
                string component = GetString(fitting, "Component").ToUpper();
                string connType = GetString(fitting, "Connection Type");
                double? classRating = GetNullableDouble(fitting, "Class Rating");
                int qty = GetBomQuantity(fitting);
                double fittingSize = GetDouble(fitting, "Size");
                double? connSize = GetNullableDouble(fitting, "Connection Size");

                double? contribution = null;

                if (OletComponents.Contains(component))
                {
                    // Olets: always look up as "SOL" with connection type "OLW"
                    contribution = LookupOlet(pipeSize, classRating);
                }
                else if (component == "TEE")
                {
                    // TEE: 3x Makeup_Run_In
                    var result = LookupMakeup(connType, "TEE", pipeSize, classRating);
                    if (result != null)
                        contribution = result.Value.RunIn * 3;
                }
                else if (component == "CROSS")
                {
                    // CROSS: 4x Makeup_Run_In
                    var result = LookupMakeup(connType, "CROSS", pipeSize, classRating);
                    if (result != null)
                        contribution = result.Value.RunIn * 4;
                }
                else if (component == "REDT")
                {
                    // REDT: use larger size as Run_Size, smaller as Outlet_Size
                    double largerSize = fittingSize;
                    double smallerSize = connSize ?? fittingSize;
                    if (smallerSize > largerSize)
                        (largerSize, smallerSize) = (smallerSize, largerSize);

                    var result = LookupMakeup(connType, "REDT", largerSize, classRating, smallerSize);
                    if (result != null)
                        contribution = result.Value.RunIn + (result.Value.OutletIn ?? 0);
                }
                else
                {
                    // Standard fitting (45L, 90L, CAP, FLG, etc.)
                    var result = LookupMakeup(connType, component, pipeSize, classRating);
                    if (result != null)
                        contribution = result.Value.RunIn;
                }

                if (contribution != null)
                {
                    totalInches += contribution.Value * qty;
                }
                else
                {
                    // Log missed fitting
                    missed.Add(new MissedMakeup
                    {
                        DrawingNumber = GetString(fitting, "Drawing Number"),
                        Component = component,
                        Size = GetString(fitting, "Size"),
                        ConnectionType = connType,
                        ClassRating = GetString(fitting, "Class Rating"),
                        Description = GetString(fitting, "Raw Description")
                    });
                }
            }

            return (totalInches, missed);
        }

        // Olet lookup: always use connection type "OLW" and component "SOL"
        private static double? LookupOlet(double pipeSize, double? classRating)
        {
            var result = LookupMakeup("OLW", "SOL", pipeSize, classRating);
            return result?.RunIn;
        }

        // Parse olet dual size (e.g., "6x1") and return the smaller size, or null if not dual
        public static double? ParseOletSmallSize(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr)) return null;

            // Check for "NxM" format
            var parts = sizeStr.Split('x', 'X');
            if (parts.Length == 2 &&
                double.TryParse(parts[0].Trim(), out double size1) &&
                double.TryParse(parts[1].Trim(), out double size2))
            {
                return Math.Min(size1, size2);
            }
            return null;
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
        public double? Class { get; set; }
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
    }
}
