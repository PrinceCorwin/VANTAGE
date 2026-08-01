using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;
using VANTAGE.Services;

namespace VANTAGE.Utilities
{
    // Provides ffmpeg/ffprobe for the Manage Tutorials admin tool. The binaries are
    // NOT bundled with the installer (they're ~194 MB) — they're hosted in the
    // tutorials S3 bucket and downloaded once, on first admin use, into LocalAppData.
    public static class FfmpegProvider
    {
        private const string ToolsKey = "tools/ffmpeg-tools.zip";

        // SHA-256 of ffmpeg-tools.zip in S3 (ffmpeg 8.1.2 essentials, ffmpeg.exe + ffprobe.exe).
        private const string ExpectedSha256 = "9B30362A3919045B5123199E71A1CC445037D9BD505B084D4C074641FB09F4E7";

        private static string ToolsDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VANTAGE", "tools", "ffmpeg");

        public static string FfmpegPath => Path.Combine(ToolsDir, "ffmpeg.exe");
        public static string FfprobePath => Path.Combine(ToolsDir, "ffprobe.exe");

        public static bool IsAvailable => File.Exists(FfmpegPath) && File.Exists(FfprobePath);

        // Ensure ffmpeg/ffprobe are present locally, downloading + extracting them if
        // needed. Progress reports 0-100 of the download. Throws on failure.
        public static async Task EnsureAvailableAsync(IProgress<int>? downloadProgress = null)
        {
            if (IsAvailable) return;

            Directory.CreateDirectory(ToolsDir);
            string tempZip = Path.Combine(Path.GetTempPath(), $"vantage-ffmpeg-{Guid.NewGuid():N}.zip");

            try
            {
                await TutorialService.DownloadObjectAsync(ToolsKey, tempZip, downloadProgress);

                if (!VerifyHash(tempZip, ExpectedSha256))
                    throw new InvalidOperationException("Downloaded video tools failed verification (SHA-256 mismatch).");

                // Flat zip (ffmpeg.exe + ffprobe.exe); overwrite any partial prior extract.
                ZipFile.ExtractToDirectory(tempZip, ToolsDir, overwriteFiles: true);

                if (!IsAvailable)
                    throw new InvalidOperationException("Video tools were downloaded but ffmpeg/ffprobe were not found after extraction.");
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* best effort */ }
            }
        }

        private static bool VerifyHash(string filePath, string expectedHex)
        {
            using var stream = File.OpenRead(filePath);
            byte[] hash = SHA256.HashData(stream);
            string actual = Convert.ToHexString(hash);
            return string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase);
        }
    }
}
