using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using VANTAGE.Models.ProgressBook;
using VANTAGE.Utilities;

namespace VANTAGE.Services.ProgressBook
{
    // Repository for ProgressBookLayout CRUD operations
    public static class ProgressBookLayoutRepository
    {
        // Get all layouts for a specific project
        public static async Task<List<ProgressBookLayout>> GetAllForProjectAsync(string projectId)
        {
            return await Task.Run(() =>
            {
                var layouts = new List<ProgressBookLayout>();
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT Id, Name, ProjectId, CreatedBy, CreatedUtc, UpdatedUtc, ConfigurationJson
                        FROM ProgressBookLayouts
                        WHERE ProjectId = @projectId
                        ORDER BY Name";
                    cmd.Parameters.AddWithValue("@projectId", projectId);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        layouts.Add(ReadLayoutFromReader(reader));
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ProgressBookLayoutRepository.GetAllForProjectAsync");
                }
                return layouts;
            });
        }

        // Get all layouts for current user (across all projects)
        public static async Task<List<ProgressBookLayout>> GetAllForUserAsync(string username)
        {
            return await Task.Run(() =>
            {
                var layouts = new List<ProgressBookLayout>();
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT Id, Name, ProjectId, CreatedBy, CreatedUtc, UpdatedUtc, ConfigurationJson
                        FROM ProgressBookLayouts
                        WHERE CreatedBy = @username
                        ORDER BY ProjectId, Name";
                    cmd.Parameters.AddWithValue("@username", username);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        layouts.Add(ReadLayoutFromReader(reader));
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ProgressBookLayoutRepository.GetAllForUserAsync");
                }
                return layouts;
            });
        }

        // Get a single layout by ID
        public static async Task<ProgressBookLayout?> GetByIdAsync(int id)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT Id, Name, ProjectId, CreatedBy, CreatedUtc, UpdatedUtc, ConfigurationJson
                        FROM ProgressBookLayouts
                        WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@id", id);

                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return ReadLayoutFromReader(reader);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ProgressBookLayoutRepository.GetByIdAsync");
                }
                return null;
            });
        }

        // Get a layout by name and project
        public static async Task<ProgressBookLayout?> GetByNameAsync(string name, string projectId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT Id, Name, ProjectId, CreatedBy, CreatedUtc, UpdatedUtc, ConfigurationJson
                        FROM ProgressBookLayouts
                        WHERE Name = @name AND ProjectId = @projectId";
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@projectId", projectId);

                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return ReadLayoutFromReader(reader);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ProgressBookLayoutRepository.GetByNameAsync");
                }
                return null;
            });
        }

        // Check if a layout name exists for a project
        public static async Task<bool> LayoutExistsAsync(string name, string projectId, int? excludeId = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    if (excludeId.HasValue)
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*) FROM ProgressBookLayouts
                            WHERE Name = @name AND ProjectId = @projectId AND Id != @excludeId";
                        cmd.Parameters.AddWithValue("@excludeId", excludeId.Value);
                    }
                    else
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*) FROM ProgressBookLayouts
                            WHERE Name = @name AND ProjectId = @projectId";
                    }
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@projectId", projectId);

                    var count = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
                    return count > 0;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ProgressBookLayoutRepository.LayoutExistsAsync");
                    return false;
                }
            });
        }

        // Insert a new layout, returns the new ID
        public static async Task<int> InsertAsync(ProgressBookLayout layout)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO ProgressBookLayouts (Name, ProjectId, CreatedBy, CreatedUtc, UpdatedUtc, ConfigurationJson)
                        VALUES (@name, @projectId, @createdBy, @createdUtc, @updatedUtc, @configurationJson);
                        SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@name", layout.Name);
                    cmd.Parameters.AddWithValue("@projectId", layout.ProjectId);
                    cmd.Parameters.AddWithValue("@createdBy", layout.CreatedBy);
                    cmd.Parameters.AddWithValue("@createdUtc", layout.CreatedUtc.ToString("O"));
                    cmd.Parameters.AddWithValue("@updatedUtc", layout.UpdatedUtc.ToString("O"));
                    cmd.Parameters.AddWithValue("@configurationJson", layout.ConfigurationJson);

                    var newId = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    layout.Id = newId;
                    return newId;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ProgressBookLayoutRepository.InsertAsync");
                    return 0;
                }
            });
        }

        // Update an existing layout
        public static async Task<bool> UpdateAsync(ProgressBookLayout layout)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE ProgressBookLayouts
                        SET Name = @name,
                            UpdatedUtc = @updatedUtc,
                            ConfigurationJson = @configurationJson
                        WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@id", layout.Id);
                    cmd.Parameters.AddWithValue("@name", layout.Name);
                    cmd.Parameters.AddWithValue("@updatedUtc", layout.UpdatedUtc.ToString("O"));
                    cmd.Parameters.AddWithValue("@configurationJson", layout.ConfigurationJson);

                    var rows = cmd.ExecuteNonQuery();
                    return rows > 0;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ProgressBookLayoutRepository.UpdateAsync");
                    return false;
                }
            });
        }

        // Delete a layout
        public static async Task<bool> DeleteAsync(int id)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "DELETE FROM ProgressBookLayouts WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@id", id);

                    var rows = cmd.ExecuteNonQuery();
                    return rows > 0;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ProgressBookLayoutRepository.DeleteAsync");
                    return false;
                }
            });
        }

        // Duplicate a layout with a new name
        public static async Task<int> DuplicateAsync(int sourceId, string newName, string username)
        {
            var source = await GetByIdAsync(sourceId);
            if (source == null)
                return 0;

            var duplicate = new ProgressBookLayout
            {
                Name = newName,
                ProjectId = source.ProjectId,
                CreatedBy = username,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                ConfigurationJson = source.ConfigurationJson
            };

            return await InsertAsync(duplicate);
        }

        // Helper to read a layout from a data reader
        private static ProgressBookLayout ReadLayoutFromReader(SqliteDataReader reader)
        {
            return new ProgressBookLayout
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                ProjectId = reader.GetString(2),
                CreatedBy = reader.GetString(3),
                CreatedUtc = DateTime.Parse(reader.GetString(4)),
                UpdatedUtc = DateTime.Parse(reader.GetString(5)),
                ConfigurationJson = reader.GetString(6)
            };
        }
    }
}
