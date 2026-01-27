using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Data.Sqlite;
using VANTAGE.Data;
using VANTAGE.Utilities;

namespace VANTAGE.Diagnostics
{
    // Diagnostic tool to troubleshoot why MS rollups aren't calculating in Schedule Module
    public static class ScheduleDiagnostic
    {
        // Run complete diagnostic and show results in MessageBox
        public static void RunDiagnostic()
        {
            var sb = new StringBuilder();
            sb.AppendLine("SCHEDULE MODULE DIAGNOSTIC REPORT");
            sb.AppendLine("=" + new string('=', 60));
            sb.AppendLine();

            try
            {
                // 1. Check Schedule table
                sb.AppendLine("1. SCHEDULE TABLE (Local SQLite - P6 Import Data)");
                sb.AppendLine("-".PadRight(60, '-'));

                using (var localConn = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}"))
                {
                    localConn.Open();

                    // Query 1: Count and date range
                    var schedCmd = localConn.CreateCommand();
                    schedCmd.CommandText = "SELECT COUNT(*), MIN(WeekEndDate), MAX(WeekEndDate) FROM Schedule";
                    using (var schedReader = schedCmd.ExecuteReader())
                    {
                        if (schedReader.Read())
                        {
                            sb.AppendLine($"Total Schedule rows: {schedReader.GetInt32(0)}");
                            sb.AppendLine($"WeekEndDate range: {schedReader.GetString(1)} to {schedReader.GetString(2)}");
                        }
                    } // Reader closed here

                    // Query 2: Sample SchedActNOs
                    var schedActCmd = localConn.CreateCommand();
                    schedActCmd.CommandText = "SELECT SchedActNO FROM Schedule LIMIT 5";
                    var sampleActNos = new List<string>();
                    using (var schedActReader = schedActCmd.ExecuteReader())
                    {
                        while (schedActReader.Read())
                        {
                            sampleActNos.Add(schedActReader.GetString(0));
                        }
                    } // Reader closed here

                    sb.AppendLine("Sample SchedActNOs:");
                    foreach (var actNo in sampleActNos)
                    {
                        sb.AppendLine($"  - {actNo}");
                    }
                }
                sb.AppendLine();

                // 2. Check ScheduleProjectMappings
                sb.AppendLine("2. SCHEDULE PROJECT MAPPINGS (Local SQLite)");
                sb.AppendLine("-".PadRight(60, '-'));

                using (var localConn = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}"))
                {
                    localConn.Open();

                    var mappingCmd = localConn.CreateCommand();
                    mappingCmd.CommandText = "SELECT WeekEndDate, ProjectID FROM ScheduleProjectMappings ORDER BY WeekEndDate";

                    var mappings = new List<(string date, string project)>();
                    using (var mappingReader = mappingCmd.ExecuteReader())
                    {
                        while (mappingReader.Read())
                        {
                            mappings.Add((mappingReader.GetString(0), mappingReader.GetString(1)));
                        }
                    } // Reader closed here

                    sb.AppendLine($"Total mappings: {mappings.Count}");
                    foreach (var mapping in mappings)
                    {
                        sb.AppendLine($"  {mapping.date} → ProjectID: {mapping.project}");
                    }
                }
                sb.AppendLine();

                // 3. Check ProgressSnapshots
                sb.AppendLine("3. PROGRESS SNAPSHOTS (Azure SQL)");
                sb.AppendLine("-".PadRight(60, '-'));

                using (var azureConn = AzureDbManager.GetConnection())
                {
                    azureConn.Open();

                    // Query 1: Counts
                    var snapCmd = azureConn.CreateCommand();
                    snapCmd.CommandText = @"
                        SELECT 
                            COUNT(*) as TotalSnapshots,
                            COUNT(DISTINCT WeekEndDate) as UniqueWeekEndDates,
                            COUNT(DISTINCT ProjectID) as UniqueProjectIDs,
                            COUNT(DISTINCT SchedActNO) as UniqueSchedActNOs
                        FROM VMS_ProgressSnapshots";

                    using (var snapReader = snapCmd.ExecuteReader())
                    {
                        if (snapReader.Read())
                        {
                            sb.AppendLine($"Total snapshots: {snapReader.GetInt32(0)}");
                            sb.AppendLine($"Unique WeekEndDates: {snapReader.GetInt32(1)}");
                            sb.AppendLine($"Unique ProjectIDs: {snapReader.GetInt32(2)}");
                            sb.AppendLine($"Unique SchedActNOs: {snapReader.GetInt32(3)}");
                        }
                    } // Reader closed here

                    // Query 2: Distinct WeekEndDates
                    var datesCmd = azureConn.CreateCommand();
                    datesCmd.CommandText = "SELECT DISTINCT WeekEndDate FROM VMS_ProgressSnapshots ORDER BY WeekEndDate";
                    var weekEndDates = new List<string>();
                    using (var datesReader = datesCmd.ExecuteReader())
                    {
                        while (datesReader.Read())
                        {
                            // WeekEndDate is stored as VARCHAR in Azure, not DATETIME
                            if (!datesReader.IsDBNull(0))
                            {
                                weekEndDates.Add(datesReader.GetString(0));
                            }
                        }
                    } // Reader closed here

                    sb.AppendLine("WeekEndDates in ProgressSnapshots:");
                    foreach (var date in weekEndDates)
                    {
                        sb.AppendLine($"  - {date}");
                    }

                    // Query 3: Distinct ProjectIDs
                    var projectsCmd = azureConn.CreateCommand();
                    projectsCmd.CommandText = "SELECT DISTINCT ProjectID FROM VMS_ProgressSnapshots ORDER BY ProjectID";
                    var projectIds = new List<string>();
                    using (var projectsReader = projectsCmd.ExecuteReader())
                    {
                        while (projectsReader.Read())
                        {
                            projectIds.Add(projectsReader.GetString(0));
                        }
                    } // Reader closed here

                    sb.AppendLine("ProjectIDs in ProgressSnapshots:");
                    foreach (var projectId in projectIds)
                    {
                        sb.AppendLine($"  - {projectId}");
                    }

                    // Query 4: Sample SchedActNOs
                    var actnoCmd = azureConn.CreateCommand();
                    actnoCmd.CommandText = "SELECT TOP 5 SchedActNO FROM VMS_ProgressSnapshots WHERE SchedActNO IS NOT NULL AND SchedActNO != ''";
                    var sampleActNos = new List<string>();
                    using (var actnoReader = actnoCmd.ExecuteReader())
                    {
                        while (actnoReader.Read())
                        {
                            sampleActNos.Add(actnoReader.GetString(0));
                        }
                    } // Reader closed here

                    sb.AppendLine("Sample SchedActNOs in ProgressSnapshots:");
                    foreach (var actNo in sampleActNos)
                    {
                        sb.AppendLine($"  - {actNo}");
                    }
                }
                sb.AppendLine();

                // 4. Check for matching data
                sb.AppendLine("4. DATA ALIGNMENT CHECK");
                sb.AppendLine("-".PadRight(60, '-'));

                using (var localConn2 = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}"))
                {
                    localConn2.Open();

                    // Read all WeekEndDates from Schedule first
                    var weekCmd = localConn2.CreateCommand();
                    weekCmd.CommandText = "SELECT DISTINCT WeekEndDate FROM Schedule";
                    var schedWeeks = new List<string>();
                    using (var weekReader = weekCmd.ExecuteReader())
                    {
                        while (weekReader.Read())
                        {
                            schedWeeks.Add(weekReader.GetString(0));
                        }
                    } // Reader closed here

                    foreach (var weekEndDate in schedWeeks)
                    {
                        sb.AppendLine($"Checking week: {weekEndDate}");

                        // Get ProjectIDs for this week
                        var projectCmd = localConn2.CreateCommand();
                        projectCmd.CommandText = "SELECT ProjectID FROM ScheduleProjectMappings WHERE WeekEndDate = @date";
                        projectCmd.Parameters.AddWithValue("@date", weekEndDate);

                        var projectIds = new List<string>();
                        using (var projectReader = projectCmd.ExecuteReader())
                        {
                            while (projectReader.Read())
                            {
                                projectIds.Add(projectReader.GetString(0));
                            }
                        } // Reader closed here

                        if (projectIds.Count == 0)
                        {
                            sb.AppendLine($"  ❌ NO ProjectID mappings found!");
                            continue;
                        }

                        sb.AppendLine($"  Mapped ProjectIDs: {string.Join(", ", projectIds)}");

                        // Check Azure for matching snapshots
                        using (var azureConn2 = AzureDbManager.GetConnection())
                        {
                            azureConn2.Open();

                            var checkCmd = azureConn2.CreateCommand();
                            checkCmd.CommandText = $@"
                                SELECT COUNT(*) 
                                FROM VMS_ProgressSnapshots 
                                WHERE WeekEndDate = @weekEndDate
                                  AND ProjectID IN ({string.Join(",", projectIds.Select((p, i) => $"@p{i}"))})
                                  AND SchedActNO IS NOT NULL 
                                  AND SchedActNO != ''";

                            // WeekEndDate is stored as TEXT in Azure, so pass it as-is
                            checkCmd.Parameters.AddWithValue("@weekEndDate", weekEndDate);
                            for (int i = 0; i < projectIds.Count; i++)
                            {
                                checkCmd.Parameters.AddWithValue($"@p{i}", projectIds[i]);
                            }

                            var matchingSnapshots = Convert.ToInt32(checkCmd.ExecuteScalar() ?? 0);
                            sb.AppendLine($"  Matching ProgressSnapshots: {matchingSnapshots}");

                            if (matchingSnapshots == 0)
                            {
                                sb.AppendLine($"  ❌ NO matching snapshots found for this week/project combination!");
                            }
                            else
                            {
                                sb.AppendLine($"  ✓ {matchingSnapshots} snapshots available for MS rollup");
                            }
                        } // Azure connection closed here
                    }
                }
                sb.AppendLine();

                // 5. Recommendations
                sb.AppendLine("5. RECOMMENDATIONS");
                sb.AppendLine("-".PadRight(60, '-'));
                sb.AppendLine("To fix MS rollup calculations:");
                sb.AppendLine("1. Ensure P6 import WeekEndDate matches Submit Progress WeekEndDate");
                sb.AppendLine("2. Ensure P6 import ProjectIDs match your Activity ProjectIDs");
                sb.AppendLine("3. Ensure Activity.SchedActNO matches P6 task_code values");
                sb.AppendLine("4. Submit Progress AFTER importing P6 (or re-submit)");

            }
            catch (Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine("ERROR DURING DIAGNOSTIC:");
                sb.AppendLine(ex.Message);
                AppLogger.Error(ex, "ScheduleDiagnostic.RunDiagnostic");
            }

            // Show results
            var resultWindow = new Window
            {
                Title = "Schedule Module Diagnostic",
                Width = 700,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new System.Windows.Controls.ScrollViewer
                {
                    Content = new System.Windows.Controls.TextBlock
                    {
                        Text = sb.ToString(),
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 12,
                        Padding = new Thickness(10),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            resultWindow.ShowDialog();
        }
    }
}