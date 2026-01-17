using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Repositories
{
    // Repository for FormTemplates and WPTemplates CRUD operations
    public static class TemplateRepository
    {
        // Get all form templates
        public static async Task<List<FormTemplate>> GetAllFormTemplatesAsync()
        {
            return await Task.Run(() =>
            {
                var templates = new List<FormTemplate>();
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT TemplateID, TemplateName, TemplateType, StructureJson,
                               IsBuiltIn, CreatedBy, CreatedUtc
                        FROM FormTemplates
                        ORDER BY IsBuiltIn DESC, TemplateName";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        templates.Add(new FormTemplate
                        {
                            TemplateID = reader.GetString(0),
                            TemplateName = reader.GetString(1),
                            TemplateType = reader.GetString(2),
                            StructureJson = reader.GetString(3),
                            IsBuiltIn = reader.GetInt32(4) == 1,
                            CreatedBy = reader.GetString(5),
                            CreatedUtc = reader.GetString(6)
                        });
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TemplateRepository.GetAllFormTemplatesAsync");
                }
                return templates;
            });
        }

        // Get a single form template by ID
        public static async Task<FormTemplate?> GetFormTemplateByIdAsync(string templateId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT TemplateID, TemplateName, TemplateType, StructureJson,
                               IsBuiltIn, CreatedBy, CreatedUtc
                        FROM FormTemplates
                        WHERE TemplateID = @templateId";
                    cmd.Parameters.AddWithValue("@templateId", templateId);

                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return new FormTemplate
                        {
                            TemplateID = reader.GetString(0),
                            TemplateName = reader.GetString(1),
                            TemplateType = reader.GetString(2),
                            StructureJson = reader.GetString(3),
                            IsBuiltIn = reader.GetInt32(4) == 1,
                            CreatedBy = reader.GetString(5),
                            CreatedUtc = reader.GetString(6)
                        };
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TemplateRepository.GetFormTemplateByIdAsync");
                }
                return null;
            });
        }

        // Get built-in form templates by type (for Reset Defaults feature)
        public static async Task<List<FormTemplate>> GetBuiltInFormTemplatesByTypeAsync(string templateType)
        {
            return await Task.Run(() =>
            {
                var templates = new List<FormTemplate>();
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT TemplateID, TemplateName, TemplateType, StructureJson,
                               IsBuiltIn, CreatedBy, CreatedUtc
                        FROM FormTemplates
                        WHERE TemplateType = @templateType AND IsBuiltIn = 1
                        ORDER BY TemplateName";
                    cmd.Parameters.AddWithValue("@templateType", templateType);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        templates.Add(new FormTemplate
                        {
                            TemplateID = reader.GetString(0),
                            TemplateName = reader.GetString(1),
                            TemplateType = reader.GetString(2),
                            StructureJson = reader.GetString(3),
                            IsBuiltIn = reader.GetInt32(4) == 1,
                            CreatedBy = reader.GetString(5),
                            CreatedUtc = reader.GetString(6)
                        });
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TemplateRepository.GetBuiltInFormTemplatesByTypeAsync");
                }
                return templates;
            });
        }

        // Insert a new form template
        public static async Task<bool> InsertFormTemplateAsync(FormTemplate template)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO FormTemplates (TemplateID, TemplateName, TemplateType,
                                                   StructureJson, IsBuiltIn, CreatedBy, CreatedUtc)
                        VALUES (@templateId, @templateName, @templateType,
                                @structureJson, @isBuiltIn, @createdBy, @createdUtc)";
                    cmd.Parameters.AddWithValue("@templateId", template.TemplateID);
                    cmd.Parameters.AddWithValue("@templateName", template.TemplateName);
                    cmd.Parameters.AddWithValue("@templateType", template.TemplateType);
                    cmd.Parameters.AddWithValue("@structureJson", template.StructureJson);
                    cmd.Parameters.AddWithValue("@isBuiltIn", template.IsBuiltIn ? 1 : 0);
                    cmd.Parameters.AddWithValue("@createdBy", template.CreatedBy);
                    cmd.Parameters.AddWithValue("@createdUtc", template.CreatedUtc);

                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TemplateRepository.InsertFormTemplateAsync");
                    return false;
                }
            });
        }

        // Update an existing form template
        public static async Task<bool> UpdateFormTemplateAsync(FormTemplate template)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE FormTemplates
                        SET TemplateName = @templateName,
                            StructureJson = @structureJson
                        WHERE TemplateID = @templateId";
                    cmd.Parameters.AddWithValue("@templateId", template.TemplateID);
                    cmd.Parameters.AddWithValue("@templateName", template.TemplateName);
                    cmd.Parameters.AddWithValue("@structureJson", template.StructureJson);

                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TemplateRepository.UpdateFormTemplateAsync");
                    return false;
                }
            });
        }

        // Delete a form template (returns list of WP templates using it if blocked)
        public static async Task<(bool Success, List<string> BlockingWPTemplates)> DeleteFormTemplateAsync(string templateId)
        {
            return await Task.Run(() =>
            {
                var blockingTemplates = new List<string>();
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    // Check if any WP templates reference this form template
                    var checkCmd = connection.CreateCommand();
                    checkCmd.CommandText = @"
                        SELECT WPTemplateName, FormsJson
                        FROM WPTemplates";

                    using (var reader = checkCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var wpName = reader.GetString(0);
                            var formsJson = reader.GetString(1);
                            if (formsJson.Contains(templateId))
                            {
                                blockingTemplates.Add(wpName);
                            }
                        }
                    }

                    if (blockingTemplates.Count > 0)
                    {
                        return (false, blockingTemplates);
                    }

                    // No references, safe to delete
                    var deleteCmd = connection.CreateCommand();
                    deleteCmd.CommandText = "DELETE FROM FormTemplates WHERE TemplateID = @templateId";
                    deleteCmd.Parameters.AddWithValue("@templateId", templateId);
                    deleteCmd.ExecuteNonQuery();

                    return (true, blockingTemplates);
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TemplateRepository.DeleteFormTemplateAsync");
                    return (false, blockingTemplates);
                }
            });
        }

        // Get all WP templates
        public static async Task<List<WPTemplate>> GetAllWPTemplatesAsync()
        {
            return await Task.Run(() =>
            {
                var templates = new List<WPTemplate>();
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT WPTemplateID, WPTemplateName, FormsJson, DefaultSettings,
                               IsBuiltIn, CreatedBy, CreatedUtc
                        FROM WPTemplates
                        ORDER BY IsBuiltIn DESC, WPTemplateName";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        templates.Add(new WPTemplate
                        {
                            WPTemplateID = reader.GetString(0),
                            WPTemplateName = reader.GetString(1),
                            FormsJson = reader.GetString(2),
                            DefaultSettings = reader.GetString(3),
                            IsBuiltIn = reader.GetInt32(4) == 1,
                            CreatedBy = reader.GetString(5),
                            CreatedUtc = reader.GetString(6)
                        });
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TemplateRepository.GetAllWPTemplatesAsync");
                }
                return templates;
            });
        }

        // Get a single WP template by ID
        public static async Task<WPTemplate?> GetWPTemplateByIdAsync(string wpTemplateId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT WPTemplateID, WPTemplateName, FormsJson, DefaultSettings,
                               IsBuiltIn, CreatedBy, CreatedUtc
                        FROM WPTemplates
                        WHERE WPTemplateID = @wpTemplateId";
                    cmd.Parameters.AddWithValue("@wpTemplateId", wpTemplateId);

                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return new WPTemplate
                        {
                            WPTemplateID = reader.GetString(0),
                            WPTemplateName = reader.GetString(1),
                            FormsJson = reader.GetString(2),
                            DefaultSettings = reader.GetString(3),
                            IsBuiltIn = reader.GetInt32(4) == 1,
                            CreatedBy = reader.GetString(5),
                            CreatedUtc = reader.GetString(6)
                        };
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TemplateRepository.GetWPTemplateByIdAsync");
                }
                return null;
            });
        }

        // Insert a new WP template
        public static async Task<bool> InsertWPTemplateAsync(WPTemplate template)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO WPTemplates (WPTemplateID, WPTemplateName, FormsJson,
                                                 DefaultSettings, IsBuiltIn, CreatedBy, CreatedUtc)
                        VALUES (@wpTemplateId, @wpTemplateName, @formsJson,
                                @defaultSettings, @isBuiltIn, @createdBy, @createdUtc)";
                    cmd.Parameters.AddWithValue("@wpTemplateId", template.WPTemplateID);
                    cmd.Parameters.AddWithValue("@wpTemplateName", template.WPTemplateName);
                    cmd.Parameters.AddWithValue("@formsJson", template.FormsJson);
                    cmd.Parameters.AddWithValue("@defaultSettings", template.DefaultSettings);
                    cmd.Parameters.AddWithValue("@isBuiltIn", template.IsBuiltIn ? 1 : 0);
                    cmd.Parameters.AddWithValue("@createdBy", template.CreatedBy);
                    cmd.Parameters.AddWithValue("@createdUtc", template.CreatedUtc);

                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TemplateRepository.InsertWPTemplateAsync");
                    return false;
                }
            });
        }

        // Update an existing WP template
        public static async Task<bool> UpdateWPTemplateAsync(WPTemplate template)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE WPTemplates
                        SET WPTemplateName = @wpTemplateName,
                            FormsJson = @formsJson,
                            DefaultSettings = @defaultSettings
                        WHERE WPTemplateID = @wpTemplateId";
                    cmd.Parameters.AddWithValue("@wpTemplateId", template.WPTemplateID);
                    cmd.Parameters.AddWithValue("@wpTemplateName", template.WPTemplateName);
                    cmd.Parameters.AddWithValue("@formsJson", template.FormsJson);
                    cmd.Parameters.AddWithValue("@defaultSettings", template.DefaultSettings);

                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TemplateRepository.UpdateWPTemplateAsync");
                    return false;
                }
            });
        }

        // Delete a WP template
        public static async Task<bool> DeleteWPTemplateAsync(string wpTemplateId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = "DELETE FROM WPTemplates WHERE WPTemplateID = @wpTemplateId";
                    cmd.Parameters.AddWithValue("@wpTemplateId", wpTemplateId);
                    cmd.ExecuteNonQuery();

                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "TemplateRepository.DeleteWPTemplateAsync");
                    return false;
                }
            });
        }

        // Check if built-in templates exist (used to seed on first run)
        public static bool BuiltInTemplatesExist()
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM FormTemplates WHERE IsBuiltIn = 1";
                var count = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
                return count > 0;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TemplateRepository.BuiltInTemplatesExist");
                return false;
            }
        }
    }
}
