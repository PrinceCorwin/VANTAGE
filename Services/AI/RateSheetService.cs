using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace VANTAGE.Services.AI
{
    // Loads rate sheet data and provides rate lookups for takeoff labor rows
    public static class RateSheetService
    {
        private static Dictionary<string, (double FldMhu, string Unit)>? _rates;

        // Component-to-EST_GRP mapping for components that don't match directly
        private static readonly Dictionary<string, string> ComponentToEstGrp = new(StringComparer.OrdinalIgnoreCase)
        {
            { "BOLT", "HARD" },
            { "WAS", "HARD" },

            // Special fab record mappings
            { "ACT", "OPRTR" },
            { "FS", "SPT" },

            // Connection type mappings
            { "GRV", "BW" },

            // Valve types → VLV
            { "VBF", "VLV" },
            { "VBFL", "VLV" },
            { "VBFO", "VLV" },
            { "VBL", "VLV" },
            { "VCK", "VLV" },
            { "VGL", "VLV" },
            { "VGT", "VLV" },
            { "VND", "VLV" },
            { "VPL", "VLV" },
            { "VPRV", "VLV" },
            { "VPSV", "VLV" },
            { "VRLF", "VLV" },
            { "VSOL", "VLV" },
            { "VSPL", "VLV" },
            { "VSW", "VLV" },
            { "VVNT", "VLV" },
            { "VYG", "VLV" },


            // Fittings → FTG
            { "45L", "FTG" },
            { "90L", "FTG" },
            { "90LSR", "FTG" },
            { "ADPT", "FTG" },
            { "CAP", "FTG" },
            { "COV", "FTG" },
            { "CPLG", "FTG" },
            { "ELB", "FTG" },
            { "FLG", "FTG" },
            { "FLGA", "FTG" },
            { "FLGB", "FTG" },
            { "FLGLJ", "FTG" },
            { "FLGO", "FTG" },
            { "FO", "FTG" },
            { "LOL", "FTG" },
            { "NIP", "FTG" },
            { "PIPET", "FTG" },
            { "PLG", "FTG" },
            { "RED", "FTG" },
            { "REDT", "FTG" },
            { "SWG", "FTG" },
            { "SOL", "FTG" },
            { "STR", "FTG" },
            { "STUB", "FTG" },
            { "TEE", "FTG" },
            { "TOL", "FTG" },
            { "TRAP", "FTG" },
            { "UN", "FTG" },
            { "WOL", "FTG" }
        };


        // Lazy-load rate data from embedded resource into a dictionary keyed by GRP_SIZE_RTG
        private static Dictionary<string, (double FldMhu, string Unit)> LoadRates()
        {
            if (_rates != null) return _rates;

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("VANTAGE.Resources.RateSheet.json");
            if (stream == null)
                throw new InvalidOperationException("Embedded resource VANTAGE.Resources.RateSheet.json not found");

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            var entries = JsonConvert.DeserializeObject<List<RateEntry>>(json)
                ?? throw new InvalidOperationException("Failed to deserialize RateSheet.json");

            _rates = new Dictionary<string, (double FldMhu, string Unit)>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.Key) && entry.FldMhu.HasValue)
                    _rates[entry.Key] = (entry.FldMhu.Value, entry.Unit ?? "EA");
            }

            return _rates;
        }

        // Look up rate by exact GRP_SIZE_RTG key. Returns (FldMhu, Unit) or null.
        public static (double FldMhu, string Unit)? LookupRate(string grpSizeRtg)
        {
            var rates = LoadRates();
            return rates.TryGetValue(grpSizeRtg, out var entry) ? entry : null;
        }

        // Build lookup key and find rate for a labor row.
        // Returns (FldMhu, Unit, KeyUsed) or (null, null, keysAttempted) if not found.
        // Lookup: Thickness (+ toggle leading S) → Class Rating → size-only.
        // On miss, reports both thickness and class keys attempted.
        public static (double? FldMhu, string? Unit, string KeyAttempted) FindRate(
            string component, string size, string? thickness, string? classRating)
        {
            string estGrp = ResolveEstGrp(component);
            if (string.IsNullOrEmpty(estGrp))
                return (null, null, $"UNMAPPED:{component}");

            // Dual size (e.g., "2x0.75") — use the smaller size for rate lookup
            var dualSize = FittingMakeupService.ParseDualSize(size);
            if (dualSize != null)
                size = dualSize.Value.Smaller.ToString("0.###");

            string? thicknessKey = null;
            string? classKey = null;

            // Try with Thickness as-is, then fallback variations
            if (!string.IsNullOrWhiteSpace(thickness))
            {
                string t = thickness.Trim().ToUpper();
                thicknessKey = $"{estGrp}-{size}:{t}";
                var rate = LookupRate(thicknessKey);
                if (rate.HasValue) return (rate.Value.FldMhu, rate.Value.Unit, thicknessKey);

                // Trailing S (e.g., "10S" for SS schedule): try without trailing S, then with leading S
                if (t.EndsWith("S") && !t.StartsWith("S") && t.Length > 1)
                {
                    string withoutTrailing = t[..^1];  // "10S" → "10"
                    string altKey = $"{estGrp}-{size}:{withoutTrailing}";
                    rate = LookupRate(altKey);
                    if (rate.HasValue) return (rate.Value.FldMhu, rate.Value.Unit, altKey);

                    // Try with leading S: "10" → "S10"
                    string withLeadingS = $"S{withoutTrailing}";
                    altKey = $"{estGrp}-{size}:{withLeadingS}";
                    rate = LookupRate(altKey);
                    if (rate.HasValue) return (rate.Value.FldMhu, rate.Value.Unit, altKey);
                }
                else
                {
                    // Toggle leading "S": if it starts with S remove it, otherwise add it
                    string toggled = t.StartsWith("S") ? t[1..] : $"S{t}";
                    if (toggled.Length > 0)
                    {
                        string altKey = $"{estGrp}-{size}:{toggled}";
                        rate = LookupRate(altKey);
                        if (rate.HasValue) return (rate.Value.FldMhu, rate.Value.Unit, altKey);
                    }
                }
            }

            // Try with Class Rating
            if (!string.IsNullOrWhiteSpace(classRating))
            {
                classKey = $"{estGrp}-{size}:{classRating}";
                var rate = LookupRate(classKey);
                if (rate.HasValue) return (rate.Value.FldMhu, rate.Value.Unit, classKey);
            }

            // Try size-only key (for FTG, GSKT, HARDWARE, etc.)
            {
                string key = $"{estGrp}-{size}";
                var rate = LookupRate(key);
                if (rate.HasValue) return (rate.Value.FldMhu, rate.Value.Unit, key);
            }

            // Report all keys attempted for the missed rates tab
            string attemptedKey = (thicknessKey, classKey) switch
            {
                (not null, not null) => $"{thicknessKey}, {classKey}",
                (not null, null) => thicknessKey,
                (null, not null) => classKey,
                _ => $"{estGrp}-{size}"
            };

            return (null, null, attemptedKey);
        }

        // Resolve our component name to rate sheet EST_GRP
        private static string ResolveEstGrp(string component)
        {
            if (string.IsNullOrEmpty(component)) return "";

            // Use mapping if one exists, otherwise use component name directly
            if (ComponentToEstGrp.TryGetValue(component, out string? estGrp))
                return estGrp;

            return component;
        }


        // Find rate with optional project-specific overrides. Checks project cache first,
        // falls back to embedded default. Same lookup logic: thickness (+ toggle S) → class → size-only.
        public static (double? FldMhu, string? Unit, string? RateSource, string KeyAttempted) FindRateWithProjectOverride(
            Dictionary<string, (double MH, string Unit)>? projectRateCache,
            string component, string size, string? thickness, string? classRating)
        {
            // Try project rates first if cache provided
            if (projectRateCache != null)
            {
                string estGrp = ResolveEstGrp(component);
                if (!string.IsNullOrEmpty(estGrp))
                {
                    string lookupSize = size;

                    // Dual size (e.g., "2x0.75") — use the smaller size
                    var dualSize = FittingMakeupService.ParseDualSize(size);
                    if (dualSize != null)
                        lookupSize = dualSize.Value.Smaller.ToString("0.###");

                    // Try thickness as-is, then fallback variations
                    if (!string.IsNullOrWhiteSpace(thickness))
                    {
                        string t = thickness.Trim().ToUpper();
                        string key = $"{estGrp}-{lookupSize}:{t}";
                        if (projectRateCache.TryGetValue(key, out var projRate))
                            return (projRate.MH, projRate.Unit, "Project", key);

                        // Trailing S (e.g., "10S" for SS schedule): try without trailing S, then with leading S
                        if (t.EndsWith("S") && !t.StartsWith("S") && t.Length > 1)
                        {
                            string withoutTrailing = t[..^1];  // "10S" → "10"
                            string altKey = $"{estGrp}-{lookupSize}:{withoutTrailing}";
                            if (projectRateCache.TryGetValue(altKey, out projRate))
                                return (projRate.MH, projRate.Unit, "Project", altKey);

                            // Try with leading S: "10" → "S10"
                            string withLeadingS = $"S{withoutTrailing}";
                            altKey = $"{estGrp}-{lookupSize}:{withLeadingS}";
                            if (projectRateCache.TryGetValue(altKey, out projRate))
                                return (projRate.MH, projRate.Unit, "Project", altKey);
                        }
                        else
                        {
                            // Toggle leading "S": if it starts with S remove it, otherwise add it
                            string toggled = t.StartsWith("S") ? t[1..] : $"S{t}";
                            if (toggled.Length > 0)
                            {
                                string altKey = $"{estGrp}-{lookupSize}:{toggled}";
                                if (projectRateCache.TryGetValue(altKey, out projRate))
                                    return (projRate.MH, projRate.Unit, "Project", altKey);
                            }
                        }
                    }

                    // Try class rating
                    if (!string.IsNullOrWhiteSpace(classRating))
                    {
                        string key = $"{estGrp}-{lookupSize}:{classRating}";
                        if (projectRateCache.TryGetValue(key, out var projRate))
                            return (projRate.MH, projRate.Unit, "Project", key);
                    }

                    // Try size-only key
                    {
                        string key = $"{estGrp}-{lookupSize}";
                        if (projectRateCache.TryGetValue(key, out var projRate))
                            return (projRate.MH, projRate.Unit, "Project", key);
                    }
                }
            }

            // Fall back to embedded default rates
            var (fldMhu, unit, keyAttempted) = FindRate(component, size, thickness, classRating);
            if (fldMhu.HasValue)
                return (fldMhu, unit, "Default", keyAttempted);

            return (null, null, null, keyAttempted);
        }

        // Resolve component to EST_GRP (public for project rate cache building)
        public static string GetEstGrp(string component) => ResolveEstGrp(component);
    }

    // Rate sheet entry for JSON deserialization
    public class RateEntry
    {
        public string Key { get; set; } = "";
        public string EstGrp { get; set; } = "";
        public double? Size { get; set; }
        public string? SchRtg { get; set; }
        public string? Unit { get; set; }
        public double? FldMhu { get; set; }
        public double? DollarEach { get; set; }
    }

    // Tracks labor rows that couldn't be matched to a rate
    public class MissedRate
    {
        public string DrawingNumber { get; set; } = "";
        public string Component { get; set; } = "";
        public string Size { get; set; } = "";
        public string Thickness { get; set; } = "";
        public string ClassRating { get; set; } = "";
        public string LookupKey { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
