﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Data
{

    /// <summary>
    /// Repository for Activity data access with pagination support
    /// </summary>
    public static class ActivityRepository
    {
      // Mapping service instance (initialized per-project when needed)
        private static MappingService _mappingService;

        /// <summary>
        /// Initialize mapping service for a specific project
        /// </summary>
        public static void InitializeMappings(string projectID = null)
      {
            _mappingService = new MappingService(projectID);
        }

        /// <summary>
        /// Get current mapping service (creates default if not initialized)
     /// </summary>
  private static MappingService GetMappingService()
        {
          if (_mappingService == null)
     {
   _mappingService = new MappingService(); // Use defaults
            }
          return _mappingService;
        }
  /// <summary>
   /// Update an existing activity in the database
    /// </summary>
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
        /// <summary>
        /// Get list of valid usernames from Users table
        /// </summary>
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
        /// <summary>
  /// Get total count of activities in database
      /// </summary>
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
        
        /// <summary>
        /// Get paginated activities with optional filtering
        /// </summary>
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

     System.Diagnostics.Debug.WriteLine(
   $"PAGING DEBUG -> pageNumber={pageNumber}, pageSize={pageSize}, offset={offset}"
        );

            // NEW: Query using NewVantage column names
           var command = connection.CreateCommand();
     command.CommandText = $@"
                SELECT *
         FROM Activities
       {whereSQL}
      ORDER BY UniqueID
 LIMIT @pageSize OFFSET @offset";

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
      // Get totals for filtered records (all pages)
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

    /// <summary>
        /// Get distinct values for a specific column. Returns up to 'limit' values and the true total count (can be > limit).
 /// </summary>
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
        /// Get distinct values for a column, filtered by a WHERE clause. Returns up to 'limit' values and the true total count (can be > limit).
        /// </summary>
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