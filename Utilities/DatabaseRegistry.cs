using System.IO;
using System.Text;
using System.Text.Json;

namespace VANTAGE.Utilities
{
    // One database the user can switch between. Display Name is independent of the
    // on-disk FileName so renames never touch the file.
    public class DatabaseEntry
    {
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string CreatedUtc { get; set; } = string.Empty;
        public string LastUsedUtc { get; set; } = string.Empty;
    }

    // Source of truth for which local databases exist and which one is active.
    //
    // This CANNOT live in the database itself: AppSettings are stored inside each
    // local .db, so every database would carry its own "active" flag. The pointer
    // must sit outside - a manifest JSON alongside the .db files in %LocalAppData%\VANTAGE.
    //
    // Existing users are migrated seamlessly: on first read with no manifest, one
    // "Primary" entry is seeded pointing at the legacy VANTAGE_Local.db, so nobody
    // loses their current database and nothing has to be re-synced.
    public static class DatabaseRegistry
    {
        private const string ManifestFileName = "databases.json";
        public const string LegacyDbFileName = "VANTAGE_Local.db";
        public const string PrimaryName = "Primary";

        private static readonly object _lock = new();

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true
        };

        // %LocalAppData%\VANTAGE - the one folder that holds every local database.
        public static string VantageFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VANTAGE");

        private static string ManifestPath => Path.Combine(VantageFolder, ManifestFileName);

        // Full path to a database file given its manifest FileName.
        public static string FullPath(string fileName) => Path.Combine(VantageFolder, fileName);

        // Load the manifest, self-heal it, and return the entries. Creates the
        // manifest (seeded with Primary) on first call. Always returns at least
        // one entry with exactly one marked active.
        public static List<DatabaseEntry> Load()
        {
            lock (_lock)
            {
                List<DatabaseEntry>? entries = null;

                try
                {
                    if (File.Exists(ManifestPath))
                    {
                        string json = File.ReadAllText(ManifestPath);
                        entries = JsonSerializer.Deserialize<List<DatabaseEntry>>(json);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "DatabaseRegistry.Load");
                    entries = null;
                }

                bool needsSave = false;

                if (entries == null || entries.Count == 0)
                {
                    // First run (or unreadable manifest): seed Primary pointing at the
                    // legacy database file, whether or not it exists yet.
                    entries = new List<DatabaseEntry>
                    {
                        new DatabaseEntry
                        {
                            Name = PrimaryName,
                            FileName = LegacyDbFileName,
                            IsActive = true,
                            CreatedUtc = NowUtc(),
                            LastUsedUtc = NowUtc()
                        }
                    };
                    needsSave = true;
                }

                // Ensure exactly one active entry.
                int activeCount = entries.Count(e => e.IsActive);
                if (activeCount != 1)
                {
                    foreach (var e in entries) e.IsActive = false;
                    entries[0].IsActive = true;
                    needsSave = true;
                }

                if (needsSave) SaveInternal(entries);

                return entries;
            }
        }

        public static void Save(List<DatabaseEntry> entries)
        {
            lock (_lock) { SaveInternal(entries); }
        }

        private static void SaveInternal(List<DatabaseEntry> entries)
        {
            try
            {
                Directory.CreateDirectory(VantageFolder);
                string json = JsonSerializer.Serialize(entries, JsonOpts);
                File.WriteAllText(ManifestPath, json);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "DatabaseRegistry.Save");
                throw;
            }
        }

        public static DatabaseEntry GetActive()
        {
            var entries = Load();
            return entries.First(e => e.IsActive);
        }

        // Full path to the active database file - the value DbPath resolves to.
        public static string GetActiveDatabasePath() => FullPath(GetActive().FileName);

        // Register a new (already schema-initialized) database. Not made active.
        public static void Add(DatabaseEntry entry)
        {
            lock (_lock)
            {
                var entries = Load();
                entries.Add(entry);
                SaveInternal(entries);
            }
        }

        // Mark the given database active; all others become inactive. Stamps LastUsedUtc.
        public static void SetActive(string fileName)
        {
            lock (_lock)
            {
                var entries = Load();
                foreach (var e in entries)
                {
                    e.IsActive = string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase);
                    if (e.IsActive) e.LastUsedUtc = NowUtc();
                }
                SaveInternal(entries);
            }
        }

        public static void Rename(string fileName, string newName)
        {
            lock (_lock)
            {
                var entries = Load();
                var entry = entries.FirstOrDefault(e =>
                    string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                {
                    entry.Name = newName;
                    SaveInternal(entries);
                }
            }
        }

        // Remove an entry and delete its database files (including -wal/-shm sidecars).
        // Refuses to delete the active database.
        public static void Delete(string fileName)
        {
            lock (_lock)
            {
                var entries = Load();
                var entry = entries.FirstOrDefault(e =>
                    string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase));
                if (entry == null) return;
                if (entry.IsActive)
                    throw new InvalidOperationException("Cannot delete the active database.");

                entries.Remove(entry);
                SaveInternal(entries);

                // Best-effort file cleanup - manifest is already updated, so a failed
                // file delete only leaves an orphan on disk, not a dangling entry.
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                {
                    try
                    {
                        string path = FullPath(fileName + suffix);
                        if (File.Exists(path)) File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error(ex, "DatabaseRegistry.Delete");
                    }
                }
            }
        }

        // True if a display name is already taken (case-insensitive).
        public static bool NameExists(string name, string? exceptFileName = null)
        {
            var entries = Load();
            return entries.Any(e =>
                string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(e.FileName, exceptFileName, StringComparison.OrdinalIgnoreCase));
        }

        // Build a filesystem-safe, unique file name (VANTAGE_<slug>.db) from a display name.
        public static string GenerateUniqueFileName(string displayName)
        {
            var sb = new StringBuilder();
            foreach (char c in displayName.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' || c == '-' || c == '_') sb.Append('-');
            }
            string slug = sb.ToString().Trim('-');
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            if (string.IsNullOrEmpty(slug)) slug = "db";

            var existing = Load()
                .Select(e => e.FileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string candidate = $"VANTAGE_{slug}.db";
            int n = 2;
            while (existing.Contains(candidate))
            {
                candidate = $"VANTAGE_{slug}-{n}.db";
                n++;
            }
            return candidate;
        }

        private static string NowUtc() => DateTime.UtcNow.ToString("o");
    }
}
