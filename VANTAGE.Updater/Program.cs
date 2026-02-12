using System.Diagnostics;
using System.IO.Compression;

namespace VANTAGE.Updater;

// Standalone updater that replaces app files and relaunches.
// Launched by the main app after downloading an update ZIP.
// Args: --pid <id> --zip <path> --target <dir> --exe <name>
class Program
{
    static int Main(string[] args)
    {
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VANTAGE", "update_error.log");

        try
        {
            var config = ParseArgs(args);
            if (config == null)
            {
                Console.WriteLine("Usage: VANTAGE.Updater --pid <id> --zip <path> --target <dir> --exe <name>");
                return 1;
            }

            Console.WriteLine($"VANTAGE Updater started");
            Console.WriteLine($"  Target: {config.TargetDir}");
            Console.WriteLine($"  ZIP: {config.ZipPath}");

            // Wait for the main app to exit
            WaitForProcessExit(config.Pid);

            // Extract the update
            Console.WriteLine("Extracting update...");
            ExtractUpdate(config.ZipPath, config.TargetDir);
            Console.WriteLine("Update extracted successfully.");

            // Update version in Add/Remove Programs registry
            string exePath = Path.Combine(config.TargetDir, config.ExeName);
            try
            {
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                string? version = versionInfo.ProductVersion ?? versionInfo.FileVersion;
                if (!string.IsNullOrEmpty(version))
                {
                    // Trim to major.minor.build (strip .0 revision if present)
                    var parts = version.Split('.');
                    if (parts.Length >= 3)
                        version = $"{parts[0]}.{parts[1]}.{parts[2]}";
                    RegistryHelper.UpdateVersionInRegistry(version);
                    Console.WriteLine($"  Updated registry version to {version}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Warning: Could not update registry version: {ex.Message}");
            }

            // Clean up the temp ZIP
            TryDelete(config.ZipPath);

            // Relaunch the app
            Console.WriteLine($"Launching {exePath}...");
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");

            // Write error log
            try
            {
                string? logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir))
                    Directory.CreateDirectory(logDir);

                File.AppendAllText(logPath,
                    $"[{DateTime.UtcNow:O}] Update failed:\n{ex}\n\n");
            }
            catch { }

            // Try to relaunch the old version anyway
            var fallbackConfig = ParseArgs(args);
            if (fallbackConfig != null)
            {
                string exePath = Path.Combine(fallbackConfig.TargetDir, fallbackConfig.ExeName);
                if (File.Exists(exePath))
                {
                    Console.WriteLine("Attempting to launch previous version...");
                    try
                    {
                        Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                    }
                    catch { }
                }
            }

            return 1;
        }
    }

    // Wait for the main app process to exit before overwriting files
    static void WaitForProcessExit(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            Console.WriteLine($"Waiting for process {pid} to exit...");
            bool exited = process.WaitForExit(30_000);
            if (!exited)
            {
                Console.WriteLine("Process did not exit in time, forcing termination...");
                process.Kill();
                process.WaitForExit(5_000);
            }
            Console.WriteLine("Process exited.");
        }
        catch (ArgumentException)
        {
            // Process already exited
            Console.WriteLine("Main process already exited.");
        }
    }

    // Extract all files from ZIP to target directory, overwriting existing files
    static void ExtractUpdate(string zipPath, string targetDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        int fileCount = 0;

        foreach (var entry in archive.Entries)
        {
            // Skip directory entries
            if (string.IsNullOrEmpty(entry.Name)) continue;

            string destinationPath = Path.Combine(targetDir, entry.FullName);

            // Ensure the destination directory exists
            string? destDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            entry.ExtractToFile(destinationPath, overwrite: true);
            fileCount++;
        }

        Console.WriteLine($"  Extracted {fileCount} files.");
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    static UpdateConfig? ParseArgs(string[] args)
    {
        int pid = 0;
        string zip = string.Empty;
        string target = string.Empty;
        string exe = string.Empty;

        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--pid": int.TryParse(args[++i], out pid); break;
                case "--zip": zip = args[++i]; break;
                case "--target": target = args[++i]; break;
                case "--exe": exe = args[++i]; break;
            }
        }

        if (pid == 0 || string.IsNullOrEmpty(zip) || string.IsNullOrEmpty(target) || string.IsNullOrEmpty(exe))
            return null;

        return new UpdateConfig(pid, zip, target, exe);
    }

    record UpdateConfig(int Pid, string ZipPath, string TargetDir, string ExeName);
}
