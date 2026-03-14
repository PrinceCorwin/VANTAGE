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
            // Generated components — mapped names
            { "BEV", "BEVEL" },
            { "OLW", "OLET WLD" },
            { "FSH", "PIPE" },
            { "FRH", "SPOOL" },
            { "BOLT", "HARDWARE" },
            { "WAS", "HARDWARE" },

            // Special fab record mappings
            { "SWG", "SWAGE CONC" },
            { "SAFSHW", "SHOWER" },
            { "ACT", "OPERATOR" },
            { "FS", "SUPPT" },
            { "TUBE", "TUBING" },

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

            // Instrument
            { "INST", "INSTRUM" },

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
            { "REDC", "FTG" },
            { "REDE", "FTG" },
            { "REDT", "FTG" },
            { "SOL", "FTG" },
            { "STR", "FTG" },
            { "STUB", "FTG" },
            { "TEE", "FTG" },
            { "TOL", "FTG" },
            { "TRAP", "FTG" },
            { "UN", "FTG" },
            { "WOL", "FTG" }
        };

        // Components that match the rate sheet EST_GRP directly (no mapping needed)
        private static readonly HashSet<string> DirectMatchComponents = new(StringComparer.OrdinalIgnoreCase)
        {
            "BW", "SW", "BU", "THRD", "GRV", "CUT", "GSKT"
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
        // Returns (FldMhu, Unit, KeyUsed) or (null, null, lastKeyAttempted) if not found.
        // Fallback chain: Thickness → Class Rating → size-only
        public static (double? FldMhu, string? Unit, string KeyAttempted) FindRate(
            string component, string size, string? thickness, string? classRating)
        {
            string estGrp = ResolveEstGrp(component);
            if (string.IsNullOrEmpty(estGrp))
                return (null, null, $"UNMAPPED:{component}");

            // Olet fab records have dual size (e.g., "24x1") — use the branch/smaller size for lookup
            if (FittingMakeupService.IsOlet(component))
            {
                var dualSize = FittingMakeupService.ParseDualSize(size);
                if (dualSize != null)
                    size = dualSize.Value.Smaller.ToString("0.###");
            }

            // Try with Thickness as SCH_RTG (with STD↔S40 synonym fallback)
            if (!string.IsNullOrWhiteSpace(thickness))
            {
                string schRtg = TranslateSchedule(thickness);
                string key = $"{estGrp}-{size}:{schRtg}";
                var rate = LookupRate(key);
                if (rate.HasValue) return (rate.Value.FldMhu, rate.Value.Unit, key);

                // STD and S40 are synonymous — try the other if first lookup failed
                string? synonym = GetScheduleSynonym(schRtg);
                if (synonym != null)
                {
                    string altKey = $"{estGrp}-{size}:{synonym}";
                    rate = LookupRate(altKey);
                    if (rate.HasValue) return (rate.Value.FldMhu, rate.Value.Unit, altKey);
                }
            }

            // Try with Class Rating as SCH_RTG
            if (!string.IsNullOrWhiteSpace(classRating))
            {
                string key = $"{estGrp}-{size}:{classRating}";
                var rate = LookupRate(key);
                if (rate.HasValue) return (rate.Value.FldMhu, rate.Value.Unit, key);
            }

            // Try size-only key (for FTG, GSKT, HARDWARE, etc.)
            {
                string key = $"{estGrp}-{size}";
                var rate = LookupRate(key);
                if (rate.HasValue) return (rate.Value.FldMhu, rate.Value.Unit, key);
            }

            // Build the best key description for the missed rates report
            string attemptedKey = !string.IsNullOrWhiteSpace(thickness)
                ? $"{estGrp}-{size}:{TranslateSchedule(thickness)}"
                : !string.IsNullOrWhiteSpace(classRating)
                    ? $"{estGrp}-{size}:{classRating}"
                    : $"{estGrp}-{size}";

            return (null, null, attemptedKey);
        }

        // Resolve our component name to rate sheet EST_GRP
        private static string ResolveEstGrp(string component)
        {
            if (string.IsNullOrEmpty(component)) return "";

            if (DirectMatchComponents.Contains(component))
                return component;

            if (ComponentToEstGrp.TryGetValue(component, out string? estGrp))
                return estGrp;

            return "";
        }

        // Translate our Thickness values to rate sheet SCH_RTG format
        // STD, XS, XXS pass through. Numeric schedules get "S" prefix (40→S40).
        private static string TranslateSchedule(string thickness)
        {
            if (string.IsNullOrWhiteSpace(thickness)) return "";

            string t = thickness.Trim().ToUpper();

            // Direct pass-through values
            if (t == "STD" || t == "XS" || t == "XXS") return t;

            // Numeric schedules: prefix with "S"
            if (double.TryParse(t, out _))
                return $"S{t}";

            // Anything else (e.g., ".250\" WT") passes through as-is
            return t;
        }

        // STD and S40 are synonymous — return the alternate if applicable
        private static string? GetScheduleSynonym(string schRtg)
        {
            if (schRtg.Equals("STD", StringComparison.OrdinalIgnoreCase)) return "S40";
            if (schRtg.Equals("S40", StringComparison.OrdinalIgnoreCase)) return "STD";
            return null;
        }

        // Find rate with optional project-specific overrides. Checks project cache first,
        // falls back to embedded default. Returns RateSource to identify which was used.
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

                    // Olet: use branch/smaller size
                    if (FittingMakeupService.IsOlet(component))
                    {
                        var dualSize = FittingMakeupService.ParseDualSize(size);
                        if (dualSize != null)
                            lookupSize = dualSize.Value.Smaller.ToString("0.###");
                    }

                    // Try thickness key
                    if (!string.IsNullOrWhiteSpace(thickness))
                    {
                        string schRtg = TranslateSchedule(thickness);
                        string key = $"{estGrp}-{lookupSize}:{schRtg}";
                        if (projectRateCache.TryGetValue(key, out var projRate))
                            return (projRate.MH, projRate.Unit, "Project", key);

                        string? synonym = GetScheduleSynonym(schRtg);
                        if (synonym != null)
                        {
                            string altKey = $"{estGrp}-{lookupSize}:{synonym}";
                            if (projectRateCache.TryGetValue(altKey, out projRate))
                                return (projRate.MH, projRate.Unit, "Project", altKey);
                        }
                    }

                    // Try class rating key
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
