using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Data
{

    /// Repository for Activity data access with pagination support

    public static class ActivityRepository
    {
        // Mapping service instance (initialized per-project when needed)
        private static MappingService _mappingService;


        /// Initialize mapping service for a specific project

        public static void InitializeMappings(string projectID = null)
        {
            _mappingService = new MappingService(projectID);
        }


        /// Archive activities to Deleted_Activities table and then delete from Activities

        public static async Task<int> ArchiveAndDeleteActivitiesAsync(List<int> activityIds, string performedBy)
        {
            if (activityIds == null || activityIds.Count == 0) return 0;

            using var connection = DatabaseSetup.GetConnection();
            await connection.OpenAsync();
            using var tx = connection.BeginTransaction();

            var activityCols = await GetTableColumnsAsync(connection, "Activities", tx);
            var deletedCols = await GetTableColumnsAsync(connection, "Deleted_Activities", tx);

            bool hasDeletedAt = deletedCols.Contains("DeletedDate", StringComparer.OrdinalIgnoreCase);
            bool hasDeletedBy = deletedCols.Contains("DeletedBy", StringComparer.OrdinalIgnoreCase);

            var common = activityCols.Where(c => deletedCols.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
            if (common.Count == 0) throw new InvalidOperationException("No overlapping columns between Activities and Deleted_Activities.");

            static string Q(string n) => $"\"{n.Replace("\"", "\"\"")}\"";
            string insertCols = string.Join(", ", common.Select(Q))
                 + (hasDeletedAt ? ", \"DeletedDate\"" : "")
                 + (hasDeletedBy ? ", \"DeletedBy\"" : "");
            string selectCols = string.Join(", ", common.Select(Q))
     + (hasDeletedAt ? ", @DeletedDate" : "")
  + (hasDeletedBy ? ", @deletedBy" : "");

            string idList = string.Join(",", activityIds);

            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText = $@"
   INSERT INTO Deleted_Activities ({insertCols})
       SELECT {selectCols}
  FROM Activities
     WHERE ActivityID IN ({idList});";
                if (hasDeletedAt) insert.Parameters.AddWithValue("@DeletedDate", DateTime.UtcNow.ToString("o"));
                if (hasDeletedBy) insert.Parameters.AddWithValue("@deletedBy", performedBy ?? "Unknown");
                await insert.ExecuteNonQueryAsync();
            }

            using (var del = connection.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = $"DELETE FROM Activities WHERE ActivityID IN ({idList});";
                await del.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            VANTAGE.Utilities.AppLogger.Info($"Archived & deleted {activityIds.Count} activities.",
             "ActivityRepository.ArchiveAndDeleteActivitiesAsync", performedBy);
            return activityIds.Count;
        }

        private static async Task<HashSet<string>> GetTableColumnsAsync(SqliteConnection conn, string table, SqliteTransaction tx)
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"PRAGMA table_info({table});";
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var name = rd["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name)) cols.Add(name);
            }
            return cols;
        }


        /// Get current mapping service (creates default if not initialized)

        private static MappingService GetMappingService()
        {
            if (_mappingService == null)
            {
                _mappingService = new MappingService(); // Use defaults
            }
            return _mappingService;
        }


        /// Update an existing activity in the database

        public static async Task<bool> UpdateActivityInDatabase(Activity activity)
        {
            try
            {
                await Task.Run(() =>
    {
        using var connection = DatabaseSetup.GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        // NEW: Use NewVantage column names directly
        command.CommandText = @"
         UPDATE Activities SET
          HexNO = @HexNO,
        CompType = @CompType,
                  PhaseCategory = @PhaseCategory,
        ROCStep = @ROCStep,
           Notes = @Notes,
         Area = @Area,
      Aux1 = @Aux1,
   Aux2 = @Aux2,
                Aux3 = @Aux3,
           Description = @Description,
               PhaseCode = @PhaseCode,
           ProjectID = @ProjectID,
      SchedActNO = @SchedActNO,
      Service = @Service,
          ShopField = @ShopField,
      SubArea = @SubArea,
       System = @System,
           TagNO = @TagNO,
    WorkPackage = @WorkPackage,
      UDF1 = @UDF1,
        UDF2 = @UDF2,
 UDF3 = @UDF3,
   UDF4 = @UDF4,
        UDF5 = @UDF5,
        UDF6 = @UDF6,
UDF7 = @UDF7,
      UDF8 = @UDF8,
         UDF9 = @UDF9,
       UDF10 = @UDF10,
              AssignedTo = @AssignedTo,
 LastModifiedBy = @LastModifiedBy,
        UDF14 = @UDF14,
  UDF15 = @UDF15,
     UDF16 = @UDF16,
         UDF17 = @UDF17,
             UDF18 = @UDF18,
      UDF20 = @UDF20,
        BudgetMHs = @BudgetMHs,
           EarnQtyEntry = @EarnQtyEntry,
   PercentEntry = @PercentEntry,
   Quantity = @Quantity,
        UOM = @UOM,
     PipeSize1 = @PipeSize1,
 PipeSize2 = @PipeSize2,
   ClientBudget = @ClientBudget,
            ClientCustom3 = @ClientCustom3
       WHERE ActivityID = @ActivityID";

        // Add parameters with NEW property names
        command.Parameters.AddWithValue("@ActivityID", activity.ActivityID);
        command.Parameters.AddWithValue("@HexNO", activity.HexNO);
        command.Parameters.AddWithValue("@CompType", activity.CompType ?? "");
        command.Parameters.AddWithValue("@PhaseCategory", activity.PhaseCategory ?? "");
        command.Parameters.AddWithValue("@ROCStep", activity.ROCStep ?? "");
        command.Parameters.AddWithValue("@Notes", activity.Notes ?? "");
        command.Parameters.AddWithValue("@Area", activity.Area ?? "");
        command.Parameters.AddWithValue("@Aux1", activity.Aux1 ?? "");
        command.Parameters.AddWithValue("@Aux2", activity.Aux2 ?? "");
        command.Parameters.AddWithValue("@Aux3", activity.Aux3 ?? "");
        command.Parameters.AddWithValue("@Description", activity.Description ?? "");
        command.Parameters.AddWithValue("@PhaseCode", activity.PhaseCode ?? "");
        command.Parameters.AddWithValue("@ProjectID", activity.ProjectID ?? "");
        command.Parameters.AddWithValue("@SchedActNO", activity.SchedActNO ?? "");
        command.Parameters.AddWithValue("@Service", activity.Service ?? "");
        command.Parameters.AddWithValue("@ShopField", activity.ShopField ?? "");
        command.Parameters.AddWithValue("@SubArea", activity.SubArea ?? "");
        command.Parameters.AddWithValue("@System", activity.System ?? "");
        command.Parameters.AddWithValue("@TagNO", activity.TagNO ?? "");
        command.Parameters.AddWithValue("@WorkPackage", activity.WorkPackage ?? "");
        command.Parameters.AddWithValue("@UDF1", activity.UDF1 ?? "");
        command.Parameters.AddWithValue("@UDF2", activity.UDF2 ?? "");
        command.Parameters.AddWithValue("@UDF3", activity.UDF3 ?? "");
        command.Parameters.AddWithValue("@UDF4", activity.UDF4 ?? "");
        command.Parameters.AddWithValue("@UDF5", activity.UDF5 ?? "");
        command.Parameters.AddWithValue("@UDF6", activity.UDF6 ?? "");
        command.Parameters.AddWithValue("@UDF7", activity.UDF7);
        command.Parameters.AddWithValue("@UDF8", activity.UDF8 ?? "");
        command.Parameters.AddWithValue("@UDF9", activity.UDF9 ?? "");
        command.Parameters.AddWithValue("@UDF10", activity.UDF10 ?? "");
        command.Parameters.AddWithValue("@AssignedTo", activity.AssignedTo ?? "Unassigned");
        command.Parameters.AddWithValue("@LastModifiedBy", activity.LastModifiedBy ?? "");
        command.Parameters.AddWithValue("@UDF14", activity.UDF14 ?? "");
        command.Parameters.AddWithValue("@UDF15", activity.UDF15 ?? "");
        command.Parameters.AddWithValue("@UDF16", activity.UDF16 ?? "");
        command.Parameters.AddWithValue("@UDF17", activity.UDF17 ?? "");
        command.Parameters.AddWithValue("@UDF18", activity.UDF18 ?? "");
        command.Parameters.AddWithValue("@UDF20", activity.UDF20 ?? "");
        command.Parameters.AddWithValue("@BudgetMHs", activity.BudgetMHs);
        command.Parameters.AddWithValue("@EarnQtyEntry", activity.EarnQtyEntry);
        command.Parameters.AddWithValue("@PercentEntry", activity.PercentEntry);
        command.Parameters.AddWithValue("@Quantity", activity.Quantity);
        command.Parameters.AddWithValue("@UOM", activity.UOM ?? "");
        command.Parameters.AddWithValue("@PipeSize1", activity.PipeSize1);
        command.Parameters.AddWithValue("@PipeSize2", activity.PipeSize2);
        command.Parameters.AddWithValue("@ClientBudget", activity.ClientBudget);
        command.Parameters.AddWithValue("@ClientCustom3", activity.ClientCustom3);
        command.Parameters.AddWithValue("@SchStart", activity.SchStart?.ToString("yyyy-MM-dd") ?? "");
        command.Parameters.AddWithValue("@SchFinish", activity.SchFinish?.ToString("yyyy-MM-dd") ?? "");
        command.Parameters.AddWithValue("@ProgDate", activity.ProgDate?.ToString("yyyy-MM-dd") ?? "");
        command.Parameters.AddWithValue("@WeekEndDate", activity.WeekEndDate?.ToString("yyyy-MM-dd") ?? "");
        command.Parameters.AddWithValue("@AzureUploadDate", activity.AzureUploadDate?.ToString("yyyy-MM-dd") ?? "");

        int rowsAffected = command.ExecuteNonQuery();

        return rowsAffected > 0;
    });

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        /// Get list of valid usernames from Users table

        private static HashSet<string> GetValidUsernames()
        {
            var validUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT Username FROM Users";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    validUsers.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                // TODO: Add proper logging when logging system is implemented
            }

            return validUsers;
        }


        /// Get total count of activities in database

        public static async Task<int> GetTotalCountAsync()
        {
            return await Task.Run(() =>
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM Activities";

                    var result = command.ExecuteScalar();
                    return Convert.ToInt32(result);
                });
        }


        /// Get paginated activities with optional filtering

        /// <param name="pageNumber">Page number (0-based)</param>
        /// <param name="pageSize">Number of records per page</param>
        /// <param name="whereClause">Optional SQL WHERE clause for filtering</param>
        /// <returns>Tuple of (activities list, total filtered count)</returns>
        public static async Task<(List<Activity> activities, int totalCount)> GetPageAsync(
            int pageNumber,
            int pageSize,
    string whereClause = null)
        {
            return await Task.Run(() =>
                 {
                     try
                     {
                         using var connection = DatabaseSetup.GetConnection();
                         connection.Open();

                         // Build WHERE clause
                         string whereSQL = string.IsNullOrWhiteSpace(whereClause) ? "" : whereClause;

                         // Get total count with filter
                         var countCommand = connection.CreateCommand();
                         countCommand.CommandText = $"SELECT COUNT(*) FROM Activities {whereSQL}";
                         var totalCount = (long)countCommand.ExecuteScalar();

                         // Get valid usernames for validation
                         var validUsernames = GetValidUsernames();

                         // Calculate offset
                         int offset = pageNumber * pageSize;

                         System.Diagnostics.Debug.WriteLine($"PAGING DEBUG -> pageNumber={pageNumber}, pageSize={pageSize}, offset={offset}");

                         // NEW: Query using NewVantage column names
                         var command = connection.CreateCommand();
                         command.CommandText = $@"SELECT * FROM Activities {whereSQL} ORDER BY UniqueID LIMIT @pageSize OFFSET @offset";
                         command.Parameters.AddWithValue("@pageSize", pageSize);
                         command.Parameters.AddWithValue("@offset", offset);
                         System.Diagnostics.Debug.WriteLine(
                                $"SQL PAGE SELECT -> LIMIT {pageSize} OFFSET {offset} | WHERE='{whereSQL.Replace("\n", " ").Replace("\r", "")}'"
                                 );

                         var activities = new List<Activity>();

                         using var reader = command.ExecuteReader();
                         while (reader.Read())
                         {
                             // Helper local functions to safely read by column name
                             string GetStringSafe(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     return reader.IsDBNull(i) ? "" : reader.GetString(i);
                                 }
                                 catch
                                 {
                                     return "";
                                 }
                             }
                             DateTime? GetDateTimeSafe(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     if (reader.IsDBNull(i)) return null;
                                     var s = reader.GetString(i);
                                     if (DateTime.TryParse(s, out var dt)) return dt.Date;
                                     return null;
                                 }
                                 catch
                                 {
                                     return null;
                                 }
                             }
                             int GetIntSafe(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     return reader.IsDBNull(i) ? 0 : reader.GetInt32(i);
                                 }
                                 catch
                                 {
                                     return 0;
                                 }
                             }

                             double GetDoubleSafe(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     return reader.IsDBNull(i) ? 0 : reader.GetDouble(i);
                                 }
                                 catch
                                 {
                                     return 0;
                                 }
                             }

                             int GetInt32FromObj(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     return reader.IsDBNull(i) ? 0 : Convert.ToInt32(reader.GetValue(i));
                                 }
                                 catch
                                 {
                                     return 0;
                                 }
                             }

                             // NEW: Read using NewVantage column names
                             var activity = new Activity
                             {
                                 ActivityID = GetIntSafe("ActivityID"),
                                 HexNO = GetIntSafe("HexNO"),

                                 // Categories
                                 CompType = GetStringSafe("CompType"),
                                 PhaseCategory = GetStringSafe("PhaseCategory"),
                                 ROCStep = GetStringSafe("ROCStep"),

                                 // Drawings
                                 DwgNO = GetStringSafe("DwgNO"),
                                 RevNO = GetStringSafe("RevNO"),
                                 SecondDwgNO = GetStringSafe("SecondDwgNO"),
                                 ShtNO = GetStringSafe("ShtNO"),

                                 // Notes
                                 Notes = GetStringSafe("Notes"),

                                 // Schedule
                                 SecondActno = GetStringSafe("SecondActno"),
                                 SchStart = GetDateTimeSafe("SchStart"),
                                 SchFinish = GetDateTimeSafe("SchFinish"),

                                 // Tags / Aux
                                 Aux1 = GetStringSafe("Aux1"),
                                 Aux2 = GetStringSafe("Aux2"),
                                 Aux3 = GetStringSafe("Aux3"),
                                 Area = GetStringSafe("Area"),
                                 ChgOrdNO = GetStringSafe("ChgOrdNO"),
                                 Description = GetStringSafe("Description"),
                                 EqmtNO = GetStringSafe("EqmtNO"),
                                 Estimator = GetStringSafe("Estimator"),
                                 InsulType = GetStringSafe("InsulType"),
                                 LineNO = GetStringSafe("LineNO"),
                                 MtrlSpec = GetStringSafe("MtrlSpec"),
                                 PhaseCode = GetStringSafe("PhaseCode"),
                                 PaintCode = GetStringSafe("PaintCode"),
                                 PipeGrade = GetStringSafe("PipeGrade"),
                                 ProjectID = GetStringSafe("ProjectID"),
                                 RFINO = GetStringSafe("RFINO"),
                                 SchedActNO = GetStringSafe("SchedActNO"),
                                 Service = GetStringSafe("Service"),
                                 ShopField = GetStringSafe("ShopField"),
                                 SubArea = GetStringSafe("SubArea"),
                                 System = GetStringSafe("System"),
                                 SystemNO = GetStringSafe("SystemNO"),
                                 TagNO = GetStringSafe("TagNO"),
                                 HtTrace = GetStringSafe("HtTrace"),
                                 WorkPackage = GetStringSafe("WorkPackage"),

                                 XRay = GetDoubleSafe("XRay"),

                                 // Trigger
                                 DateTrigger = GetInt32FromObj("DateTrigger"),

                                 // UDFs
                                 UDF1 = GetStringSafe("UDF1"),
                                 UDF2 = GetStringSafe("UDF2"),
                                 UDF3 = GetStringSafe("UDF3"),
                                 UDF4 = GetStringSafe("UDF4"),
                                 UDF5 = GetStringSafe("UDF5"),
                                 UDF6 = GetStringSafe("UDF6"),
                                 UDF7 = GetIntSafe("UDF7"),
                                 UDF8 = GetStringSafe("UDF8"),
                                 UDF9 = GetStringSafe("UDF9"),
                                 UDF10 = GetStringSafe("UDF10"),
                                 AssignedTo = string.IsNullOrWhiteSpace(GetStringSafe("AssignedTo")) ? "Unassigned" : (validUsernames.Contains(GetStringSafe("AssignedTo")) ? GetStringSafe("AssignedTo") : "Unassigned"),
                                 LastModifiedBy = GetStringSafe("LastModifiedBy"),
                                 CreatedBy = GetStringSafe("CreatedBy"),
                                 UDF14 = GetStringSafe("UDF14"),
                                 UDF15 = GetStringSafe("UDF15"),
                                 UDF16 = GetStringSafe("UDF16"),
                                 UDF17 = GetStringSafe("UDF17"),
                                 UDF18 = GetStringSafe("UDF18"),
                                 UniqueID = GetStringSafe("UniqueID"),
                                 UDF20 = GetStringSafe("UDF20"),

                                 // Values
                                 BaseUnit = GetDoubleSafe("BaseUnit"),
                                 BudgetMHs = GetDoubleSafe("BudgetMHs"),
                                 BudgetHoursGroup = GetDoubleSafe("BudgetHoursGroup"),
                                 BudgetHoursROC = GetDoubleSafe("BudgetHoursROC"),
                                 EarnedMHsRoc = GetDoubleSafe("EarnedMHsRoc"),
                                 EarnQtyEntry = GetDoubleSafe("EarnQtyEntry"),
                                 PercentEntry = GetDoubleSafe("PercentEntry"),
                                 Quantity = GetDoubleSafe("Quantity"),
                                 UOM = GetStringSafe("UOM"),

                                 // Equipment
                                 EquivQTY = GetDoubleSafe("EquivQTY"),
                                 EquivUOM = GetStringSafe("EquivUOM"),

                                 // ROC
                                 ROCID = GetDoubleSafe("ROCID"),
                                 ROCPercent = GetDoubleSafe("ROCPercent"),
                                 ROCBudgetQTY = GetDoubleSafe("ROCBudgetQTY"),

                                 // Pipe
                                 PipeSize1 = GetDoubleSafe("PipeSize1"),
                                 PipeSize2 = GetDoubleSafe("PipeSize2"),

                                 // Previous
                                 PrevEarnMHs = GetDoubleSafe("PrevEarnMHs"),
                                 PrevEarnQTY = GetDoubleSafe("PrevEarnQTY"),

                                 // Client
                                 ClientEquivQty = GetDoubleSafe("ClientEquivQty"),
                                 ClientBudget = GetDoubleSafe("ClientBudget"),
                                 ClientCustom3 = GetDoubleSafe("ClientCustom3")
                             };
                             activities.Add(activity);
                         }

                         // Return tuple with both activities and totalCount
                         return (activities, (int)totalCount);
                     }
                     catch (Exception ex)
                     {
                         throw;
                     }
                 });
        }


        /// Get ALL activities with optional filtering (no pagination)
        /// Use this when pagination is disabled and Syncfusion handles virtualization

        /// <param name="whereClause">Optional SQL WHERE clause for filtering</param>
        /// <returns>Tuple of (all activities list, total count)</returns>
        public static async Task<(List<Activity> activities, int totalCount)> GetAllActivitiesAsync(
                   string whereClause = null)
        {
            return await Task.Run(() =>
                 {
                     try
                     {
                         using var connection = DatabaseSetup.GetConnection();
                         connection.Open();

                         // Build WHERE clause (same logic as GetPageAsync)
                         string whereSQL = string.IsNullOrWhiteSpace(whereClause)
                   ? ""
                    : whereClause.Trim();

                         if (!string.IsNullOrWhiteSpace(whereSQL) &&
                    !whereSQL.TrimStart().StartsWith("WHERE", StringComparison.OrdinalIgnoreCase))
                         {
                             whereSQL = "WHERE " + whereSQL;
                         }

                         // Get total count with filter
                         var countCommand = connection.CreateCommand();
                         countCommand.CommandText = $"SELECT COUNT(*) FROM Activities {whereSQL}";
                         var totalCount = (long)countCommand.ExecuteScalar();

                         System.Diagnostics.Debug.WriteLine(
                       $"NO PAGINATION -> Loading ALL {totalCount} records | WHERE='{whereSQL.Replace("\n", " ").Replace("\r", "")}'"
                          );

                         // Get valid usernames for validation
                         var validUsernames = GetValidUsernames();

                         // Query ALL activities (no LIMIT/OFFSET - this is the key difference!)
                         var command = connection.CreateCommand();
                         command.CommandText = $@"
                                    SELECT *
                                        FROM Activities
                                    {whereSQL}
                                        ORDER BY UniqueID";

                         var activities = new List<Activity>();

                         using var reader = command.ExecuteReader();
                         while (reader.Read())
                         {
                             // Use the EXACT same helper functions as GetPageAsync
                             string GetStringSafe(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     return reader.IsDBNull(i) ? "" : reader.GetString(i);
                                 }
                                 catch
                                 {
                                     return "";
                                 }
                             }

                             DateTime? GetDateTimeSafe(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     if (reader.IsDBNull(i)) return null;
                                     var s = reader.GetString(i);
                                     if (DateTime.TryParse(s, out var dt)) return dt.Date;
                                     return null;
                                 }
                                 catch
                                 {
                                     return null;
                                 }
                             }

                             int GetIntSafe(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     return reader.IsDBNull(i) ? 0 : reader.GetInt32(i);
                                 }
                                 catch
                                 {
                                     return 0;
                                 }
                             }

                             double GetDoubleSafe(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     return reader.IsDBNull(i) ? 0 : reader.GetDouble(i);
                                 }
                                 catch
                                 {
                                     return 0;
                                 }
                             }

                             int GetInt32FromObj(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     return reader.IsDBNull(i) ? 0 : Convert.ToInt32(reader.GetValue(i));
                                 }
                                 catch
                                 {
                                     return 0;
                                 }
                             }

                             // Build activity object (EXACT same structure as GetPageAsync)
                             var activity = new Activity
                             {
                                 ActivityID = GetIntSafe("ActivityID"),
                                 HexNO = GetIntSafe("HexNO"),

                                 // Schedule dates
                                 SchStart = GetDateTimeSafe("SchStart"),
                                 SchFinish = GetDateTimeSafe("SchFinish"),
                                 ProgDate = GetDateTimeSafe("ProgDate"),
                                 WeekEndDate = GetDateTimeSafe("WeekEndDate"),
                                 AzureUploadDate = GetDateTimeSafe("AzureUploadDate"),

                                 // Text fields - Category
                                 Area = GetStringSafe("Area"),
                                 Aux1 = GetStringSafe("Aux1"),
                                 Aux2 = GetStringSafe("Aux2"),
                                 Aux3 = GetStringSafe("Aux3"),
                                 ChgOrdNO = GetStringSafe("ChgOrdNO"),
                                 CompType = GetStringSafe("CompType"),
                                 Description = GetStringSafe("Description"),
                                 DwgNO = GetStringSafe("DwgNO"),
                                 EqmtNO = GetStringSafe("EqmtNO"),
                                 Estimator = GetStringSafe("Estimator"),
                                 HtTrace = GetStringSafe("HtTrace"),
                                 InsulType = GetStringSafe("InsulType"),
                                 LineNO = GetStringSafe("LineNO"),
                                 MtrlSpec = GetStringSafe("MtrlSpec"),
                                 Notes = GetStringSafe("Notes"),
                                 PaintCode = GetStringSafe("PaintCode"),
                                 PhaseCategory = GetStringSafe("PhaseCategory"),
                                 PhaseCode = GetStringSafe("PhaseCode"),
                                 PipeGrade = GetStringSafe("PipeGrade"),
                                 ProjectID = GetStringSafe("ProjectID"),
                                 RevNO = GetStringSafe("RevNO"),
                                 RFINO = GetStringSafe("RFINO"),
                                 ROCStep = GetStringSafe("ROCStep"),
                                 SchedActNO = GetStringSafe("SchedActNO"),
                                 SecondActno = GetStringSafe("SecondActno"),
                                 SecondDwgNO = GetStringSafe("SecondDwgNO"),
                                 Service = GetStringSafe("Service"),
                                 ShopField = GetStringSafe("ShopField"),
                                 ShtNO = GetStringSafe("ShtNO"),
                                 SubArea = GetStringSafe("SubArea"),
                                 System = GetStringSafe("System"),
                                 SystemNO = GetStringSafe("SystemNO"),
                                 TagNO = GetStringSafe("TagNO"),
                                 WorkPackage = GetStringSafe("WorkPackage"),
                                 DateTrigger = GetIntSafe("DateTrigger"),
                                 XRay = GetIntSafe("XRay"),

                                 // UDFs
                                 UDF1 = GetStringSafe("UDF1"),
                                 UDF2 = GetStringSafe("UDF2"),
                                 UDF3 = GetStringSafe("UDF3"),
                                 UDF4 = GetStringSafe("UDF4"),
                                 UDF5 = GetStringSafe("UDF5"),
                                 UDF6 = GetStringSafe("UDF6"),
                                 UDF7 = GetIntSafe("UDF7"),
                                 UDF8 = GetStringSafe("UDF8"),
                                 UDF9 = GetStringSafe("UDF9"),
                                 UDF10 = GetStringSafe("UDF10"),
                                 AssignedTo = string.IsNullOrWhiteSpace(GetStringSafe("AssignedTo")) || GetStringSafe("AssignedTo") == "Unassigned"
                           ? "Unassigned" : (validUsernames.Contains(GetStringSafe("AssignedTo")) ? GetStringSafe("AssignedTo") : "Unassigned"),
                                 LastModifiedBy = GetStringSafe("LastModifiedBy"),
                                 CreatedBy = GetStringSafe("CreatedBy"),
                                 UDF14 = GetStringSafe("UDF14"),
                                 UDF15 = GetStringSafe("UDF15"),
                                 UDF16 = GetStringSafe("UDF16"),
                                 UDF17 = GetStringSafe("UDF17"),
                                 UDF18 = GetStringSafe("UDF18"),
                                 UniqueID = GetStringSafe("UniqueID"),
                                 UDF20 = GetStringSafe("UDF20"),

                                 // Values
                                 BaseUnit = GetDoubleSafe("BaseUnit"),
                                 BudgetMHs = GetDoubleSafe("BudgetMHs"),
                                 BudgetHoursGroup = GetDoubleSafe("BudgetHoursGroup"),
                                 BudgetHoursROC = GetDoubleSafe("BudgetHoursROC"),
                                 EarnedMHsRoc = GetDoubleSafe("EarnedMHsRoc"),
                                 EarnQtyEntry = GetDoubleSafe("EarnQtyEntry"),
                                 PercentEntry = GetDoubleSafe("PercentEntry"),
                                 Quantity = GetDoubleSafe("Quantity"),
                                 UOM = GetStringSafe("UOM"),

                                 // Equipment
                                 EquivQTY = GetDoubleSafe("EquivQTY"),
                                 EquivUOM = GetStringSafe("EquivUOM"),

                                 // ROC
                                 ROCID = GetDoubleSafe("ROCID"),
                                 ROCPercent = GetDoubleSafe("ROCPercent"),
                                 ROCBudgetQTY = GetDoubleSafe("ROCBudgetQTY"),

                                 // Pipe
                                 PipeSize1 = GetDoubleSafe("PipeSize1"),
                                 PipeSize2 = GetDoubleSafe("PipeSize2"),

                                 // Previous
                                 PrevEarnMHs = GetDoubleSafe("PrevEarnMHs"),
                                 PrevEarnQTY = GetDoubleSafe("PrevEarnQTY"),

                                 // Client
                                 ClientEquivQty = GetDoubleSafe("ClientEquivQty"),
                                 ClientBudget = GetDoubleSafe("ClientBudget"),
                                 ClientCustom3 = GetDoubleSafe("ClientCustom3")
                             };

                             activities.Add(activity);
                         }

                         System.Diagnostics.Debug.WriteLine($"✓ Loaded {activities.Count} activities (no pagination)");

                         // Return tuple with both activities and totalCount
                         return (activities, (int)totalCount);
                     }
                     catch (Exception ex)
                     {
                         System.Diagnostics.Debug.WriteLine($"✗ Error loading all activities: {ex.Message}");
                         throw;
                     }
                 });
        }


        /// Get totals for filtered records (all pages)

        public static async Task<(double budgetedMHs, double earnedMHs)> GetTotalsAsync(string whereClause = null)
        {
            return await Task.Run(() =>
         {
             try
             {
                 using var connection = DatabaseSetup.GetConnection();
                 connection.Open();

                 string whereSQL = string.IsNullOrWhiteSpace(whereClause) ? "" : whereClause;

                 // NEW: Use NewVantage column names
                 var command = connection.CreateCommand();
                 command.CommandText = $@"
                        SELECT 
                          COALESCE(SUM(BudgetMHs), 0) as TotalBudgeted, 
                       COALESCE(SUM(
                          CASE 
                            WHEN PercentEntry >= 100 THEN BudgetMHs
                       ELSE ROUND(PercentEntry / 100.0 * BudgetMHs, 3)
                       END
                                 ), 0) as TotalEarned
                    FROM Activities
                    {whereSQL}";

                 using var reader = command.ExecuteReader();
                 if (reader.Read())
                 {
                     double budgeted = reader.GetDouble(0);
                     double earned = reader.GetDouble(1);
                     return (budgeted, earned);
                 }

                 return (0, 0);
             }
             catch (Exception ex)
             {
                 return (0, 0);
             }
         });
        }


        /// Get distinct values for a specific column. Returns up to 'limit' values and the true total count (can be > limit).

        public static async Task<(List<string> values, int totalCount)> GetDistinctColumnValuesAsync(string columnName, int limit = 1000)
        {
            return await Task.Run(() =>
         {
             var vals = new List<string>();
             try
             {
                 using var connection = DatabaseSetup.GetConnection();
                 connection.Open();

                 // Map of calculated/display-only properties to SQL expressions
                 var calcMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                 {
                     // Status calculation
                     ["Status"] = "CASE WHEN PercentEntry = 0 THEN 'Not Started' WHEN PercentEntry >= 100 THEN 'Complete' ELSE 'In Progress' END",
                     // Earned MHs calculated
                     ["EarnMHsCalc"] = "CASE WHEN PercentEntry >= 100 THEN BudgetMHs ELSE ROUND(PercentEntry / 100.0 * BudgetMHs, 3) END",
                     // AssignedTo: show 'Unassigned' for null/empty
                     ["AssignedTo"] = "CASE WHEN TRIM(COALESCE(NULLIF(AssignedTo, ''), '')) = '' OR AssignedTo = 'Unassigned' THEN 'Unassigned' ELSE AssignedTo END"
                 };

                 string dbExpression = null;
                 if (!string.IsNullOrEmpty(columnName) && calcMap.TryGetValue(columnName, out var expr))
                 {
                     dbExpression = expr;
                 }
                 else
                 {
                     // Use column name directly (no more ColumnMapper.GetDbColumnName!)
                     dbExpression = columnName;
                 }

                 // Query distinct values limited
                 var cmd = connection.CreateCommand();
                 cmd.CommandText = $@"SELECT DISTINCT ({dbExpression}) FROM Activities ORDER BY 1 LIMIT @limit";
                 cmd.Parameters.AddWithValue("@limit", limit);

                 using var reader = cmd.ExecuteReader();
                 while (reader.Read())
                 {
                     if (reader.IsDBNull(0))
                     {
                         vals.Add("");
                     }
                     else
                     {
                         var raw = reader.GetValue(0);
                         vals.Add(Convert.ToString(raw));
                     }
                 }

                 // Always put 'Unassigned' at the top for AssignedTo
                 if (!string.IsNullOrEmpty(columnName) && columnName.Equals("AssignedTo", StringComparison.OrdinalIgnoreCase))
                 {
                     vals = vals.Where(v => !string.IsNullOrWhiteSpace(v) && !v.Equals("Unassigned", StringComparison.OrdinalIgnoreCase)).ToList();
                     vals.Insert(0, "Unassigned");
                 }

                 // Count distinct values (after deduplication)
                 int total = vals.Count;
                 return (vals, total);
             }
             catch (Exception ex)
             {
                 return (vals, vals.Count);
             }
         });
        }
        /// <summary>
        /// Get all deleted activities
        /// </summary>
        public static async Task<List<Activity>> GetDeletedActivitiesAsync()
        {
            return await Task.Run(() =>
            {
                var deletedActivities = new List<Activity>();

                try
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM Deleted_Activities ORDER BY DeletedDate DESC";

                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var activity = MapReaderToActivity(reader);
                        // You can add DeletedDate and deletedBy to the Activity object if needed
                        deletedActivities.Add(activity);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting deleted activities: {ex.Message}");
                }

                return deletedActivities;
            });
        }
        /// <summary>
        /// Map database reader to Activity object
        /// </summary>
        /// <summary>
        /// Map database reader to Activity object
        /// </summary>
        /// <summary>
        /// Map database reader to Activity object
        /// </summary>
        private static Activity MapReaderToActivity(SqliteDataReader reader)
        {
            return new Activity
            {
                ActivityID = reader.GetInt32(reader.GetOrdinal("ActivityID")),
                Area = GetStringOrDefault(reader, "Area"),
                AssignedTo = GetStringOrDefault(reader, "AssignedTo"),
                AzureUploadDate = GetDateTimeOrNull(reader, "AzureUploadDate"),
                Aux1 = GetStringOrDefault(reader, "Aux1"),
                Aux2 = GetStringOrDefault(reader, "Aux2"),
                Aux3 = GetStringOrDefault(reader, "Aux3"),
                BaseUnit = GetDoubleOrDefault(reader, "BaseUnit"),
                BudgetMHs = GetDoubleOrDefault(reader, "BudgetMHs"),
                BudgetHoursGroup = GetDoubleOrDefault(reader, "BudgetHoursGroup"),
                BudgetHoursROC = GetDoubleOrDefault(reader, "BudgetHoursROC"),
                ChgOrdNO = GetStringOrDefault(reader, "ChgOrdNO"),
                ClientBudget = GetDoubleOrDefault(reader, "ClientBudget"),
                ClientCustom3 = GetDoubleOrDefault(reader, "ClientCustom3"),
                ClientEquivQty = GetDoubleOrDefault(reader, "ClientEquivQty"),
                CompType = GetStringOrDefault(reader, "CompType"),
                CreatedBy = GetStringOrDefault(reader, "CreatedBy"),
                DateTrigger = GetIntOrDefault(reader, "DateTrigger"),
                Description = GetStringOrDefault(reader, "Description"),
                DwgNO = GetStringOrDefault(reader, "DwgNO"),
                EarnedMHsRoc = GetDoubleOrDefault(reader, "EarnedMHsRoc"),
                EarnQtyEntry = GetDoubleOrDefault(reader, "EarnQtyEntry"),
                EqmtNO = GetStringOrDefault(reader, "EqmtNO"),
                EquivQTY = GetDoubleFromStringColumn(reader, "EquivQTY"),
                EquivUOM = GetStringOrDefault(reader, "EquivUOM"),
                Estimator = GetStringOrDefault(reader, "Estimator"),
                // Finish = not in model (legacy column)
                HexNO = GetIntOrDefault(reader, "HexNO"),
                HtTrace = GetStringOrDefault(reader, "HtTrace"),
                InsulType = GetStringOrDefault(reader, "InsulType"),
                LastModifiedBy = GetStringOrDefault(reader, "LastModifiedBy"),
                LineNO = GetStringOrDefault(reader, "LineNO"),
                MtrlSpec = GetStringOrDefault(reader, "MtrlSpec"),
                Notes = GetStringOrDefault(reader, "Notes"),
                PaintCode = GetStringOrDefault(reader, "PaintCode"),
                PercentEntry = GetDoubleOrDefault(reader, "PercentEntry"),
                PhaseCategory = GetStringOrDefault(reader, "PhaseCategory"),
                PhaseCode = GetStringOrDefault(reader, "PhaseCode"),
                PipeGrade = GetStringOrDefault(reader, "PipeGrade"),
                PipeSize1 = GetDoubleOrDefault(reader, "PipeSize1"),
                PipeSize2 = GetDoubleOrDefault(reader, "PipeSize2"),
                PrevEarnMHs = GetDoubleOrDefault(reader, "PrevEarnMHs"),
                PrevEarnQTY = GetDoubleOrDefault(reader, "PrevEarnQTY"),
                ProgDate = GetDateTimeOrNull(reader, "ProgDate"),
                ProjectID = GetStringOrDefault(reader, "ProjectID"),
                Quantity = GetDoubleOrDefault(reader, "Quantity"),
                RevNO = GetStringOrDefault(reader, "RevNO"),
                RFINO = GetStringOrDefault(reader, "RFINO"),
                ROCBudgetQTY = GetDoubleOrDefault(reader, "ROCBudgetQTY"),
                ROCID = GetDoubleOrDefault(reader, "ROCID"),
                ROCPercent = GetDoubleOrDefault(reader, "ROCPercent"),
                ROCStep = GetStringOrDefault(reader, "ROCStep"),
                SchedActNO = GetStringOrDefault(reader, "SchedActNO"),
                SchFinish = GetDateTimeOrNull(reader, "SchFinish"),
                SchStart = GetDateTimeOrNull(reader, "SchStart"),
                SecondActno = GetStringOrDefault(reader, "SecondActno"),
                SecondDwgNO = GetStringOrDefault(reader, "SecondDwgNO"),
                Service = GetStringOrDefault(reader, "Service"),
                ShopField = GetStringOrDefault(reader, "ShopField"),
                ShtNO = GetStringOrDefault(reader, "ShtNO"),
                // Start = not in model (legacy column)
                // Status = calculated property, don't set it
                SubArea = GetStringOrDefault(reader, "SubArea"),
                System = GetStringOrDefault(reader, "System"),
                SystemNO = GetStringOrDefault(reader, "SystemNO"),
                TagNO = GetStringOrDefault(reader, "TagNO"),
                UDF1 = GetStringOrDefault(reader, "UDF1"),
                UDF2 = GetStringOrDefault(reader, "UDF2"),
                UDF3 = GetStringOrDefault(reader, "UDF3"),
                UDF4 = GetStringOrDefault(reader, "UDF4"),
                UDF5 = GetStringOrDefault(reader, "UDF5"),
                UDF6 = GetStringOrDefault(reader, "UDF6"),
                UDF7 = GetIntOrDefault(reader, "UDF7"),  // int, not string
                UDF8 = GetStringOrDefault(reader, "UDF8"),
                UDF9 = GetStringOrDefault(reader, "UDF9"),
                UDF10 = GetStringOrDefault(reader, "UDF10"),
                UDF14 = GetStringOrDefault(reader, "UDF14"),
                UDF15 = GetStringOrDefault(reader, "UDF15"),
                UDF16 = GetStringOrDefault(reader, "UDF16"),
                UDF17 = GetStringOrDefault(reader, "UDF17"),
                UDF18 = GetStringOrDefault(reader, "UDF18"),
                UDF20 = GetStringOrDefault(reader, "UDF20"),
                UniqueID = GetStringOrDefault(reader, "UniqueID"),
                UOM = GetStringOrDefault(reader, "UOM"),
                WeekEndDate = GetDateTimeOrNull(reader, "WeekEndDate"),
                WorkPackage = GetStringOrDefault(reader, "WorkPackage"),
                XRay = GetDoubleOrDefault(reader, "XRay")
            };
        }

        // Helper methods remain the same
        private static double GetDoubleFromStringColumn(SqliteDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal)) return 0.0;

            string value = reader.GetString(ordinal);
            if (string.IsNullOrWhiteSpace(value)) return 0.0;

            return double.TryParse(value, out double result) ? result : 0.0;
        }
        private static string GetStringOrDefault(SqliteDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);
        }

        private static double GetDoubleOrDefault(SqliteDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0.0 : reader.GetDouble(ordinal);
        }

        private static int GetIntOrDefault(SqliteDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }

        private static DateTime? GetDateTimeOrNull(SqliteDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal)) return null;
            string dateStr = reader.GetString(ordinal);
            return DateTime.TryParse(dateStr, out DateTime result) ? result : (DateTime?)null;
        }

        /// <summary>
        /// Restore activities from Deleted_Activities back to Activities
        /// </summary>
        public static async Task<int> RestoreActivitiesAsync(List<int> activityIds)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    int successCount = 0;
                    using var transaction = connection.BeginTransaction();

                    try
                    {
                        foreach (var activityId in activityIds)
                        {
                            // Copy back to Activities (excluding DeletedDate and deletedBy)
                            var copyCommand = connection.CreateCommand();
                            copyCommand.Transaction = transaction;

                            // Get all column names from Activities table (excluding the deleted metadata)
                            copyCommand.CommandText = @"
                        INSERT INTO Activities 
                        SELECT ActivityID, UniqueID, Description, TagNO, Area, Phase, 
                               Discipline, SubDiscipline, Category, SubCategory, 
                               AssignedTo, WBS, SchedActNO, ProjectID, 
                               SchStart, SchFinish, Quantity, Unit, EarnQtyEntry, 
                               PercentEntry, BudgetMHs, EarnedMHsRoc, 
                               HexNO, BaseUnit, BudgetHoursGroup, BudgetHoursROC, 
                               PipeClass, PipeSpec, PipeSize1, PipeSize2, 
                               PrevEarnMHs, PrevEarnQTY, ROCBudgetQTY, ROCPercent, 
                               ROCID, EquivQTY, ClientBudget, ClientCustom3, 
                               ClientEquivQty, WeekEndDate, ProgDate, 
                               AzureUploadDate, XRay, LastModifiedBy
                        FROM Deleted_Activities 
                        WHERE ActivityID = @ActivityID";

                            copyCommand.Parameters.AddWithValue("@ActivityID", activityId);

                            if (copyCommand.ExecuteNonQuery() > 0)
                            {
                                // Remove from Deleted_Activities
                                var deleteCommand = connection.CreateCommand();
                                deleteCommand.Transaction = transaction;
                                deleteCommand.CommandText = "DELETE FROM Deleted_Activities WHERE ActivityID = @ActivityID";
                                deleteCommand.Parameters.AddWithValue("@ActivityID", activityId);
                                deleteCommand.ExecuteNonQuery();

                                successCount++;
                            }
                        }

                        transaction.Commit();
                        return successCount;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error restoring activities: {ex.Message}");
                    return 0;
                }
            });
        }

        /// <summary>
        /// Permanently purge activities from Deleted_Activities
        /// </summary>
        public static async Task<int> PurgeDeletedActivitiesAsync(List<int> activityIds)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    int successCount = 0;
                    using var transaction = connection.BeginTransaction();

                    try
                    {
                        foreach (var activityId in activityIds)
                        {
                            var command = connection.CreateCommand();
                            command.Transaction = transaction;
                            command.CommandText = "DELETE FROM Deleted_Activities WHERE ActivityID = @ActivityID";
                            command.Parameters.AddWithValue("@ActivityID", activityId);

                            if (command.ExecuteNonQuery() > 0)
                                successCount++;
                        }

                        transaction.Commit();
                        return successCount;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error purging deleted activities: {ex.Message}");
                    return 0;
                }
            });
        }

        /// <summary>
        /// Auto-purge deleted records older than specified days
        /// </summary>
        public static async Task<int> AutoPurgeOldDeletedActivitiesAsync(int daysToKeep)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

                    var command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM Deleted_Activities WHERE DeletedDate < @CutoffDate";
                    command.Parameters.AddWithValue("@CutoffDate", cutoffDate.ToString("o")); // ISO 8601 format

                    int rowsAffected = command.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Auto-purged {rowsAffected} old deleted records");
                    }

                    return rowsAffected;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error auto-purging: {ex.Message}");
                    return 0;
                }
            });
        }

        /// Get distinct values for a column, filtered by a WHERE clause. Returns up to 'limit' values and the true total count (can be > limit).

        public static async Task<(List<string> values, int totalCount)> GetDistinctColumnValuesForFilterAsync(string columnName, string whereClause, int limit = 1000)
        {
            return await Task.Run(() =>
              {
                  var vals = new List<string>();
                  try
                  {
                      using var connection = DatabaseSetup.GetConnection();
                      connection.Open();

                      // Map of calculated properties to SQL expressions
                      var calcMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                      {
                          ["Status"] = "CASE WHEN PercentEntry = 0 THEN 'Not Started' WHEN PercentEntry >= 100 THEN 'Complete' ELSE 'In Progress' END",
                          ["EarnMHsCalc"] = "CASE WHEN PercentEntry >= 100 THEN BudgetMHs ELSE ROUND(PercentEntry / 100.0 * BudgetMHs, 3) END",
                          ["AssignedTo"] = "CASE WHEN TRIM(COALESCE(NULLIF(AssignedTo, ''), '')) = '' OR AssignedTo = 'Unassigned' THEN 'Unassigned' ELSE AssignedTo END"
                      };

                      string dbExpression = null;
                      if (!string.IsNullOrEmpty(columnName) && calcMap.TryGetValue(columnName, out var expr))
                      {
                          dbExpression = expr;
                      }
                      else
                      {
                          // Use column name directly
                          dbExpression = columnName;
                      }

                      string whereSQL = string.IsNullOrWhiteSpace(whereClause) ? "" : whereClause.Trim();
                      if (!string.IsNullOrWhiteSpace(whereSQL) && !whereSQL.TrimStart().StartsWith("WHERE", StringComparison.OrdinalIgnoreCase))
                      {
                          whereSQL = "WHERE " + whereSQL;
                      }

                      var cmd = connection.CreateCommand();
                      cmd.CommandText = $@"SELECT DISTINCT ({dbExpression}) FROM Activities {whereSQL} ORDER BY 1 LIMIT @limit";
                      cmd.Parameters.AddWithValue("@limit", limit);

                      using var reader = cmd.ExecuteReader();
                      while (reader.Read())
                      {
                          if (reader.IsDBNull(0))
                          {
                              vals.Add("");
                          }
                          else
                          {
                              var raw = reader.GetValue(0);
                              vals.Add(Convert.ToString(raw));
                          }
                      }

                      // Always put 'Unassigned' at the top for AssignedTo
                      if (!string.IsNullOrEmpty(columnName) && columnName.Equals("AssignedTo", StringComparison.OrdinalIgnoreCase))
                      {
                          vals = vals.Where(v => !string.IsNullOrWhiteSpace(v) && !v.Equals("Unassigned", StringComparison.OrdinalIgnoreCase)).ToList();
                          vals.Insert(0, "Unassigned");
                      }

                      int total = vals.Count;
                      return (vals, total);
                  }
                  catch (Exception ex)
                  {
                      return (vals, vals.Count);
                  }
              });
        }
    }
}