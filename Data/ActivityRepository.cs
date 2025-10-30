using System;
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
                    command.CommandText = @"
                UPDATE Activities SET
                    HexNO = @HexNO,
                    Catg_ComponentType = @CompType,
                    Catg_PhaseCategory = @PhaseCategory,
                    Catg_ROC_Step = @ROCStep,
                    Notes_Comments = @Notes,
                    Tag_Area = @Area,
                    Tag_Aux1 = @Aux1,
                    Tag_Aux2 = @Aux2,
                    Tag_Aux3 = @Aux3,
                    Tag_Descriptions = @Description,
                    Tag_Phase_Code = @PhaseCode,
                    Tag_ProjectID = @ProjectID,
                    Tag_Sch_ActNo = @SchedActNO,
                    Tag_Service = @Service,
                    Tag_ShopField = @ShopField,
                    Tag_SubArea = @SubArea,
                    Tag_System = @System,
                    Tag_TagNo = @TagNO,
                    Tag_WorkPackage = @WorkPackage,
                    UDFOne = @UDFOne,
                    UDFTwo = @UDFTwo,
                    UDFThree = @UDFThree,
                    UDFFour = @UDFFour,
                    UDFFive = @UDFFive,
                    UDFSix = @UDFSix,
                    UDFSeven = @UDFSeven,
                    UDFEight = @UDFEight,
                    UDFNine = @UDFNine,
                    UDFTen = @UDFTen,
                    UDFEleven = @AssignedTo,
                    UDFTwelve = @LastModifiedBy,
                    UDFFourteen = @UDFFourteen,
                    UDFFifteen = @UDFFifteen,
                    UDFSixteen = @UDFSixteen,
                    UDFSeventeen = @UDFSeventeen,
                    UDFEighteen = @UDFEighteen,
                    UDFTwenty = @UDFTwenty,
                    Val_BudgetedHours_Ind = @BudgetMHs,
                    Val_EarnedQty = @EarnQtyEntry,
                    Val_Perc_Complete = @PercentEntry,
                    Val_Quantity = @Quantity,
                    Val_UOM = @UOM,
                    Val_Pipe_Size1 = @PipeSize1,
                    Val_Pipe_Size2 = @PipeSize2,
                    Val_UDF_Two = @ClientBudget,
                    Val_UDF_Three = @ClientCustom3,
                    Val_TimeStamp = @WeekEndDate,
                    AzureUploadDate = @AzureUploadDate,
                    Val_ProgDate = @ProgDate
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
                    command.Parameters.AddWithValue("@UDFOne", activity.UDFOne ?? "");
                    command.Parameters.AddWithValue("@UDFTwo", activity.UDFTwo ?? "");
                    command.Parameters.AddWithValue("@UDFThree", activity.UDFThree ?? "");
                    command.Parameters.AddWithValue("@UDFFour", activity.UDFFour ?? "");
                    command.Parameters.AddWithValue("@UDFFive", activity.UDFFive ?? "");
                    command.Parameters.AddWithValue("@UDFSix", activity.UDFSix ?? "");
                    command.Parameters.AddWithValue("@UDFSeven", activity.UDFSeven);
                    command.Parameters.AddWithValue("@UDFEight", activity.UDFEight ?? "");
                    command.Parameters.AddWithValue("@UDFNine", activity.UDFNine ?? "");
                    command.Parameters.AddWithValue("@UDFTen", activity.UDFTen ?? "");
                    command.Parameters.AddWithValue("@AssignedTo", activity.AssignedTo ?? "");
                    command.Parameters.AddWithValue("@LastModifiedBy", activity.LastModifiedBy ?? "");
                    command.Parameters.AddWithValue("@UDFFourteen", activity.UDFFourteen ?? "");
                    command.Parameters.AddWithValue("@UDFFifteen", activity.UDFFifteen ?? "");
                    command.Parameters.AddWithValue("@UDFSixteen", activity.UDFSixteen ?? "");
                    command.Parameters.AddWithValue("@UDFSeventeen", activity.UDFSeventeen ?? "");
                    command.Parameters.AddWithValue("@UDFEighteen", activity.UDFEighteen ?? "");
                    command.Parameters.AddWithValue("@UDFTwenty", activity.UDFTwenty ?? "");
                    command.Parameters.AddWithValue("@BudgetMHs", activity.BudgetMHs);
                    command.Parameters.AddWithValue("@EarnQtyEntry", activity.EarnQtyEntry);
                    command.Parameters.AddWithValue("@PercentEntry", activity.PercentEntry);
                    command.Parameters.AddWithValue("@Quantity", activity.Quantity);
                    command.Parameters.AddWithValue("@UOM", activity.UOM ?? "");
                    command.Parameters.AddWithValue("@PipeSize1", activity.PipeSize1);
                    command.Parameters.AddWithValue("@PipeSize2", activity.PipeSize2);
                    command.Parameters.AddWithValue("@ClientBudget", activity.ClientBudget);
                    command.Parameters.AddWithValue("@ClientCustom3", activity.ClientCustom3);
                    command.Parameters.AddWithValue("@WeekEndDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.Parameters.AddWithValue("@AzureUploadDate", activity.AzureUploadDate ?? "");
                    command.Parameters.AddWithValue("@ProgDate", activity.ProgDate ?? "");

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
        /// <param name="pageNumber">Page number (1-based)</param>
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
                    using var connection = DatabaseSetup.GetConnection(); // FIX: Add DatabaseSetup.
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
                    int offset = pageNumber * pageSize;         // correct for 0-based pages

                    System.Diagnostics.Debug.WriteLine(
                        $"PAGING DEBUG -> pageNumber={pageNumber}, pageSize={pageSize}, offset={offset}"
                    );

                    // Get paginated data with filter - use SELECT * and read by column name to avoid index mismatches
                    var command = connection.CreateCommand();
                    command.CommandText = $@"
                SELECT *
                FROM Activities
                {whereSQL}
                ORDER BY UDFNineteen
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

                        var activity = new Activity
                        {
                            ActivityID = GetIntSafe("ActivityID"),
                            HexNO = GetIntSafe("HexNO"),

                            // Categories
                            CompType = GetStringSafe("Catg_ComponentType"),
                            PhaseCategory = GetStringSafe("Catg_PhaseCategory"),
                            ROCStep = GetStringSafe("Catg_ROC_Step"),

                            // Drawings
                            DwgNO = GetStringSafe("Dwg_PrimeDrawingNO"),
                            RevNO = GetStringSafe("Dwg_RevisionNo"),
                            SecondDwgNO = GetStringSafe("Dwg_SecondaryDrawingNO"),
                            ShtNO = GetStringSafe("Dwg_ShtNo"),

                            // Notes
                            Notes = GetStringSafe("Notes_Comments"),

                            // Schedule
                            OldActno = GetStringSafe("Sch_Actno"),
                            Start = GetStringSafe("Sch_Start"),
                            Finish = GetStringSafe("Sch_Finish"),

                            // Tags / Aux
                            Aux1 = GetStringSafe("Tag_Aux1"),
                            Aux2 = GetStringSafe("Tag_Aux2"),
                            Aux3 = GetStringSafe("Tag_Aux3"),
                            Area = GetStringSafe("Tag_Area"),
                            ChgOrdNO = GetStringSafe("Tag_CONo"),
                            Description = GetStringSafe("Tag_Descriptions"),
                            EqmtNO = GetStringSafe("Tag_EqmtNo"),
                            Estimator = GetStringSafe("Tag_Estimator"),
                            InsulType = GetStringSafe("Tag_Insulation_Typ"),
                            LineNO = GetStringSafe("Tag_LineNo"),
                            MtrlSpec = GetStringSafe("Tag_Matl_Spec"),
                            PhaseCode = GetStringSafe("Tag_Phase_Code"),
                            PaintCode = GetStringSafe("Tag_Paint_Code"),
                            PipeGrade = GetStringSafe("Tag_Pipe_Grade"),
                            ProjectID = GetStringSafe("Tag_ProjectID"),
                            RFINO = GetStringSafe("Tag_RFINo"),
                            SchedActNO = GetStringSafe("Tag_Sch_ActNo"),
                            Service = GetStringSafe("Tag_Service"),
                            ShopField = GetStringSafe("Tag_ShopField"),
                            SubArea = GetStringSafe("Tag_SubArea"),
                            System = GetStringSafe("Tag_System"),
                            SystemNO = GetStringSafe("Tag_SystemNo"),
                            TagNO = GetStringSafe("Tag_TagNo"),
                            HtTrace = GetStringSafe("Tag_Tracing"),
                            WorkPackage = GetStringSafe("Tag_WorkPackage"),

                            XRay = GetDoubleSafe("Tag_XRAY"),

                            // Trigger
                            DateTrigger = GetInt32FromObj("Trg_DateTrigger"),

                            // UDFs
                            UDFOne = GetStringSafe("UDFOne"),
                            UDFTwo = GetStringSafe("UDFTwo"),
                            UDFThree = GetStringSafe("UDFThree"),
                            UDFFour = GetStringSafe("UDFFour"),
                            UDFFive = GetStringSafe("UDFFive"),
                            UDFSix = GetStringSafe("UDFSix"),
                            UDFSeven = GetIntSafe("UDFSeven"),
                            UDFEight = GetStringSafe("UDFEight"),
                            UDFNine = GetStringSafe("UDFNine"),
                            UDFTen = GetStringSafe("UDFTen"),
                            AssignedTo = string.IsNullOrWhiteSpace(GetStringSafe("UDFEleven")) ? "Unassigned" : (validUsernames.Contains(GetStringSafe("UDFEleven")) ? GetStringSafe("UDFEleven") : "Unassigned"),
                            LastModifiedBy = GetStringSafe("UDFTwelve"),
                            CreatedBy = GetStringSafe("UDFThirteen"),
                            UDFFourteen = GetStringSafe("UDFFourteen"),
                            UDFFifteen = GetStringSafe("UDFFifteen"),
                            UDFSixteen = GetStringSafe("UDFSixteen"),
                            UDFSeventeen = GetStringSafe("UDFSeventeen"),
                            UDFEighteen = GetStringSafe("UDFEighteen"),
                            UniqueID = GetStringSafe("UDFNineteen"),
                            UDFTwenty = GetStringSafe("UDFTwenty"),

                            // Values
                            BaseUnit = GetDoubleSafe("Val_Base_Unit"),
                            BudgetMHs = GetDoubleSafe("Val_BudgetedHours_Ind"),
                            BudgetHoursGroup = GetDoubleSafe("Val_BudgetedHours_Group"),
                            BudgetHoursROC = GetDoubleSafe("Val_BudgetedHours_ROC"),
                            EarnedMHsRoc = GetIntSafe("Val_EarnedHours_ROC"),
                            EarnQtyEntry = GetDoubleSafe("Val_EarnedQty"),
                            PercentEntry = GetDoubleSafe("Val_Perc_Complete"),
                            Quantity = GetDoubleSafe("Val_Quantity"),
                            UOM = GetStringSafe("Val_UOM"),

                            // Equipment
                            EquivQTY = GetDoubleSafe("Val_EQ_QTY"),
                            EquivUOM = GetStringSafe("Val_EQ_UOM"),

                            // ROC
                            ROCID = GetIntSafe("Tag_ROC_ID"),
                            ROCPercent = GetDoubleSafe("Val_ROC_Perc"),
                            ROCBudgetQTY = GetDoubleSafe("Val_ROC_BudgetQty"),

                            // Pipe
                            PipeSize1 = GetDoubleSafe("Val_Pipe_Size1"),
                            PipeSize2 = GetDoubleSafe("Val_Pipe_Size2"),

                            // Previous
                            PrevEarnMHs = GetDoubleSafe("Val_Prev_Earned_Hours"),
                            PrevEarnQTY = GetDoubleSafe("Val_Prev_Earned_Qty"),
                            WeekEndDate = GetStringSafe("Val_TimeStamp"),

                            // Client
                            ClientEquivQty = GetDoubleSafe("Val_Client_EQ_QTY_BDG"),
                            ClientBudget = GetDoubleSafe("Val_UDF_Two"),
                            ClientCustom3 = GetDoubleSafe("Val_UDF_Three"),
                            AzureUploadDate = GetStringSafe("AzureUploadDate"),
                            ProgDate = GetStringSafe("Val_ProgDate")
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

                    var command = connection.CreateCommand();
                    command.CommandText = $@"
                             SELECT 
                                COALESCE(SUM(Val_BudgetedHours_Ind), 0) as TotalBudgeted,
                                COALESCE(SUM(
                                    CASE 
                                        WHEN Val_Perc_Complete >= 1.0 THEN Val_BudgetedHours_Ind
                                        ELSE ROUND(Val_Perc_Complete * Val_BudgetedHours_Ind, 3)
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

                    // Map of calculated/display-only properties to SQL expressions for display
                    var calcMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        // Percent fields: display as 0-100
                        ["PercentEntry"] = "ROUND(Val_Perc_Complete * 100.0, 2)",
                        ["PercentEntry_Display"] = "ROUND(Val_Perc_Complete * 100.0, 2)",
                        ["PercentCompleteCalc"] = "ROUND(Val_Perc_Complete * 100.0, 2)",
                        ["PercentCompleteCalc_Display"] = "ROUND(Val_Perc_Complete * 100.0, 2)",
                        // Earned qty ratio: display as 0-100
                        ["EarnedQtyCalc"] = "ROUND(CASE WHEN Val_Quantity > 0 THEN (Val_EarnedQty / Val_Quantity) ELSE NULL END * 100.0, 2)",
                        ["EarnedQtyCalc_Display"] = "ROUND(CASE WHEN Val_Quantity > 0 THEN (Val_EarnedQty / Val_Quantity) ELSE NULL END * 100.0, 2)",
                        // Earned MHs calculated: raw hours
                        ["EarnMHsCalc"] = "CASE WHEN Val_Perc_Complete >= 1.0 THEN Val_BudgetedHours_Ind ELSE ROUND(Val_Perc_Complete * Val_BudgetedHours_Ind, 3) END",
                        // Status: match Activity.Status logic
                        ["Status"] = "CASE WHEN Val_Perc_Complete IS NULL OR Val_Perc_Complete = 0 THEN 'Not Started' WHEN Val_Perc_Complete >= 1.0 THEN 'Complete' ELSE 'In Progress' END",
                        // AssignedTo: show 'Unassigned' for null/empty
                        ["AssignedTo"] = "CASE WHEN TRIM(COALESCE(NULLIF(UDFEleven, ''), '')) = '' OR UDFEleven = 'Unassigned' THEN 'Unassigned' ELSE UDFEleven END"
                    };

                    string dbExpression = null;
                    if (!string.IsNullOrEmpty(columnName) && calcMap.TryGetValue(columnName, out var expr))
                    {
                        dbExpression = expr;
                    }
                    else
                    {
                        // Sanitize column name by mapping property to actual DB column if needed
                        string dbColumn = ColumnMapper.GetDbColumnName(columnName);
                        if (!string.IsNullOrEmpty(columnName) && columnName.EndsWith("_Display", StringComparison.OrdinalIgnoreCase))
                        {
                            var baseProp = columnName.Substring(0, columnName.Length - "_Display".Length);
                            dbExpression = ColumnMapper.GetDbColumnName(baseProp);
                        }
                        else
                        {
                            dbExpression = ColumnMapper.GetDbColumnName(columnName);
                        }
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
    }
}