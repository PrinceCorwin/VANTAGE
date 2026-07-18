using System.Collections.Generic;
using System.IO;

namespace VANTAGE.Utilities
{
    // Shared filename sanitization for any path built from user-influenced data
    // (WorkPackage codes, filter values, project IDs). Strips characters the OS
    // rejects and guards the Windows reserved device names (CON, PRN, NUL, COMx,
    // LPTx) that would otherwise create unusable or dangerous files.
    // See Plans/Security_Guidelines.md.
    public static class FileNameHelper
    {
        private static readonly HashSet<string> ReservedNames = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        // Replace invalid characters with '_' and prefix reserved base names so the
        // result is always a safe, non-empty file name (without extension).
        public static string SanitizeFileName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "_";

            foreach (char c in Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');

            input = input.Trim();
            if (input.Length == 0)
                return "_";

            string baseName = Path.GetFileNameWithoutExtension(input);
            if (ReservedNames.Contains(baseName))
                input = "_" + input;

            return input;
        }
    }
}
