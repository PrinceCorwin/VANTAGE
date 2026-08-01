using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VANTAGE.Utilities
{
    // Result of probing an MP4 file with ffprobe.
    public class Mp4Info
    {
        public string VideoCodec { get; set; } = "";
        public string? AudioCodec { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double DurationSeconds { get; set; }
        public long FileSizeBytes { get; set; }
        public int ExtraTrackCount { get; set; }   // data/subtitle/other tracks that will be dropped

        public bool HasAudio => !string.IsNullOrEmpty(AudioCodec);

        // The app plays videos in a WebView2 (HTML5 <video>), which needs H.264 video
        // and (if present) AAC audio. Anything else cannot be stream-copied and must be
        // re-exported by the user.
        public bool CodecsAreStreamable =>
            string.Equals(VideoCodec, "h264", StringComparison.OrdinalIgnoreCase) &&
            (!HasAudio || string.Equals(AudioCodec, "aac", StringComparison.OrdinalIgnoreCase));

        public string DurationDisplay
        {
            get
            {
                var t = TimeSpan.FromSeconds(DurationSeconds);
                return t.Hours > 0 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
            }
        }

        public string FileSizeDisplay => $"{FileSizeBytes / (1024.0 * 1024.0):0.#} MB";
    }

    // Drives ffprobe/ffmpeg (provided by FfmpegProvider). Inspection validates codecs;
    // the remux losslessly moves the moov atom to the front (faststart) and drops
    // non-audio/video tracks.
    public static class Mp4Tooling
    {
        // Probe an MP4 and return its codecs, dimensions, duration, size, and extra-track count.
        public static async Task<Mp4Info> InspectAsync(string filePath)
        {
            string args = $"-v error -print_format json -show_format -show_streams \"{filePath}\"";
            var (exitCode, stdout, stderr) = await RunAsync(FfmpegProvider.FfprobePath, args);
            if (exitCode != 0)
                throw new InvalidOperationException($"Could not read the video file.\n\n{stderr.Trim()}");

            var info = new Mp4Info { FileSizeBytes = new FileInfo(filePath).Length };

            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;

            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    string codecType = stream.TryGetProperty("codec_type", out var ct) ? (ct.GetString() ?? "") : "";
                    string codecName = stream.TryGetProperty("codec_name", out var cn) ? (cn.GetString() ?? "") : "";

                    if (codecType.Equals("video", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(info.VideoCodec))
                    {
                        info.VideoCodec = codecName;
                        if (stream.TryGetProperty("width", out var w)) info.Width = w.GetInt32();
                        if (stream.TryGetProperty("height", out var h)) info.Height = h.GetInt32();
                    }
                    else if (codecType.Equals("audio", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(info.AudioCodec))
                    {
                        info.AudioCodec = codecName;
                    }
                    else
                    {
                        // data/subtitle/timecode or a second video/audio track — dropped by the remux
                        info.ExtraTrackCount++;
                    }
                }
            }

            if (root.TryGetProperty("format", out var format) &&
                format.TryGetProperty("duration", out var dur) &&
                double.TryParse(dur.GetString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            {
                info.DurationSeconds = seconds;
            }

            return info;
        }

        // Losslessly remux to a temp .mp4: keep only the primary video (+ audio if present),
        // move the moov atom to the front for streaming. Returns the output path.
        public static async Task<string> RemuxFastStartAsync(string inputPath, Mp4Info info, string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, $"remux_{Guid.NewGuid():N}.mp4");

            var map = new StringBuilder("-map 0:v:0");
            if (info.HasAudio) map.Append(" -map 0:a:0");

            string args = $"-y -i \"{inputPath}\" {map} -c copy -movflags +faststart \"{outputPath}\"";
            var (exitCode, _, stderr) = await RunAsync(FfmpegProvider.FfmpegPath, args);

            if (exitCode != 0 || !File.Exists(outputPath))
            {
                TryDelete(outputPath);
                throw new InvalidOperationException($"Video processing failed.\n\n{stderr.Trim()}");
            }

            return outputPath;
        }

        public static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }

        // Run a console tool with no window, capturing stdout/stderr.
        private static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string exePath, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            return (process.ExitCode, stdout.ToString(), stderr.ToString());
        }
    }
}
