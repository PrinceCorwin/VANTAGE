using System;
using System.Collections.Generic;
using System.IO;

namespace VANTAGE.Utilities
{
    // Turns a user-entered filename into a safe, flat S3 object key ending in .mp4.
    // Mirrors the app's filename guard (Plans/Security_Guidelines.md): strip path
    // separators/invalid chars, block Windows reserved names, cap length.
    public static class TutorialKeyValidator
    {
        public const int MaxKeyLength = 120;

        private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
            "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
        };

        // Produce a safe key. Path.GetFileName defeats any traversal/separator attempt;
        // invalid chars become '_'; reserved base names are prefixed; extension forced to .mp4.
        public static string Sanitize(string input)
        {
            input = (input ?? string.Empty).Trim();

            // Collapse any path into just its final segment.
            input = Path.GetFileName(input);

            foreach (char c in Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');

            string baseName = Path.GetFileNameWithoutExtension(input);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "video";
            if (ReservedNames.Contains(baseName))
                baseName = "_" + baseName;

            string key = baseName + ".mp4";
            if (key.Length > MaxKeyLength)
                key = baseName.Substring(0, MaxKeyLength - 4) + ".mp4";

            return key;
        }

        // Validate that a (already-sanitized) key is usable. Returns false + a reason otherwise.
        public static bool IsValid(string key, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Filename is required.";
                return false;
            }
            if (!key.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                error = "Filename must end in .mp4.";
                return false;
            }
            if (key.Length > MaxKeyLength)
            {
                error = $"Filename is too long (max {MaxKeyLength} characters).";
                return false;
            }
            return true;
        }
    }
}
