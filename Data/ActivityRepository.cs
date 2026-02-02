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
        private static MappingService? _mappingService;

        /// Initialize mapping service for a specific project
        public static void InitializeMappings(string? projectID = null)
        {
            _mappingService = new MappingService(projectID);
        }
        /// <summary>
        /// Get count of LocalDirty=1 records grouped by ProjectID for projects NOT in the selected list.
        /// Used to warn users about unsaved changes before removing projects from Local.
        /// </summary>
        /// <param name="selectedProjectIds">List of project IDs the user HAS selected (we want counts for everything else)</param>
        /// <returns>Dictionary of ProjectID to dirty record count (only projects with count > 0)</returns>
        public static async Task<Dictionary<string, int>> GetDirtyCountByExcludedProjectsAsync(List<string> selectedProjectIds)
        {
            return await Task.Run(() =>
            {
                var dirtyCounts = new Dictionary<string, int>();

                try
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    var cmd = connection.CreateCommand();

                    // Build the NOT IN clause for selected projects
                    if (selectedProjectIds != null && selectedProjectIds.Count > 0)
                    {
                        var paramNames = selectedProjectIds.Select((p, i) => $"@proj{i}").ToList();
                        cmd.CommandText = $@"
                    SELECT ProjectID, COUNT(*) as DirtyCount
                    FROM Activities 
                    WHERE LocalDirty = 1 
                    AND ProjectID IS NOT NULL
                    AND ProjectID NOT IN ({string.Join(",", paramNames)})
                    GROUP BY ProjectID
                    HAVING COUNT(*) > 0";

                        for (int i = 0; i < selectedProjectIds.Count; i++)
                        {
                            cmd.Parameters.AddWithValue($"@proj{i}", selectedProjectIds[i]);
                        }
                    }
                    else
                    {
                        // No projects selected - get all dirty records by project
                        cmd.CommandText = @"
                    SELECT ProjectID, COUNT(*) as DirtyCount
                    FROM Activities 
                    WHERE LocalDirty = 1 
                    AND ProjectID IS NOT NULL
                    GROUP BY ProjectID
                    HAVING COUNT(*) > 0";
                    }

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string projectId = reader.GetString(0);
                        int count = reader.GetInt32(1);
                        dirtyCounts[projectId] = count;
                    }

                    AppLogger.Info($"GetDirtyCountByExcludedProjectsAsync found {dirtyCounts.Count} projects with unsaved changes",
                        "ActivityRepository.GetDirtyCountByExcludedProjectsAsync");
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ActivityRepository.GetDirtyCountByExcludedProjectsAsync");
                }

                return dirtyCounts;
            });
        }

        /// <summary>
        /// Remove all activities from Local database for the specified project IDs.
        /// This is a hard delete (not soft delete) - records are permanently removed from Local only.
        /// Central database is NOT affected.
        /// </summary>
        /// <param name="projectIdsToRemove">List of project IDs to remove from Local</param>
        /// <returns>Number of records removed</returns>
        public static async Task<int> RemoveActivitiesByProjectIdsAsync(List<string> projectIdsToRemove)
        {
            if (projectIdsToRemove == null || projectIdsToRemove.Count == 0)
                return 0;

            return await Task.Run(() =>
            {
                try
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    using var transaction = connection.BeginTransaction();

                    try
                    {
                        var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;

                        // Build parameterized IN clause
                        var paramNames = projectIdsToRemove.Select((p, i) => $"@proj{i}").ToList();
                        cmd.CommandText = $"DELETE FROM Activities WHERE ProjectID IN ({string.Join(",", paramNames)})";

                        for (int i = 0; i < projectIdsToRemove.Count; i++)
                        {
                            cmd.Parameters.AddWithValue($"@proj{i}", projectIdsToRemove[i]);
                        }

                        int rowsDeleted = cmd.ExecuteNonQuery();

                        transaction.Commit();

                        AppLogger.Info($"RemoveActivitiesByProjectIdsAsync removed {rowsDeleted} records from {projectIdsToRemove.Count} projects: {string.Join(", ", projectIdsToRemove)}",
                            "ActivityRepository.RemoveActivitiesByProjectIdsAsync");

                        return rowsDeleted;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "ActivityRepository.RemoveActivitiesByProjectIdsAsync");
                    return 0;
                }
            });
        }

        // Get all LocalDirty=1 records for specified projects
        public static async Task<List<Activity>> GetDirtyActivitiesAsync(List<string> projectIds)
    {
        return await Task.Run(() =>
        {
            var dirtyActivities = new List<Activity>();

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                // DEBUG: Log the query
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
        SELECT * FROM Activities 
        WHERE LocalDirty = 1 
        AND ProjectID IN (" + string.Join(",", projectIds.Select((p, i) => $"@proj{i}")) + ")";

                for (int i = 0; i < projectIds.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@proj{i}", projectIds[i]);
                }

                // DEBUG OUTPUT
                AppLogger.Info($"GetDirtyActivitiesAsync called with {projectIds.Count} projects: {string.Join(", ", projectIds)}", "ActivityRepository.GetDirtyActivitiesAsync");

                using var reader = cmd.ExecuteReader();
                int count = 0;
                while (reader.Read())
                {
                    dirtyActivities.Add(MapReaderToActivity(reader));
                    count++;
                }

                AppLogger.Info($"GetDirtyActivitiesAsync returning {count} records", "ActivityRepository.GetDirtyActivitiesAsync");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ActivityRepository.GetDirtyActivitiesAsync");
            }

            return dirtyActivities;
        });
    }

        /// Update an existing activity in the database

        // COMPLETE UpdateActivityInDatabase - Replace in ActivityRepository.cs
        // Includes ALL editable columns from Activities table

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
                    Area = @Area,
                    AssignedTo = @AssignedTo,
                    AzureUploadUtcDate = @AzureUploadUtcDate,
                    Aux1 = @Aux1,
                    Aux2 = @Aux2,
                    Aux3 = @Aux3,
                    BaseUnit = @BaseUnit,
                    BudgetHoursGroup = @BudgetHoursGroup,
                    BudgetHoursROC = @BudgetHoursROC,
                    BudgetMHs = @BudgetMHs,
                    ChgOrdNO = @ChgOrdNO,
                    ClientBudget = @ClientBudget,
                    ClientCustom3 = @ClientCustom3,
                    ClientEquivQty = @ClientEquivQty,
                    CompType = @CompType,
                    DateTrigger = @DateTrigger,
                    Description = @Description,
                    DwgNO = @DwgNO,
                    EarnQtyEntry = @EarnQtyEntry,
                    EarnedMHsRoc = @EarnedMHsRoc,
                    EqmtNO = @EqmtNO,
                    EquivQTY = @EquivQTY,
                    EquivUOM = @EquivUOM,
                    Estimator = @Estimator,
                    HexNO = @HexNO,
                    HtTrace = @HtTrace,
                    InsulType = @InsulType,
                    LineNumber = @LineNumber,
                    LocalDirty = @LocalDirty,
                    MtrlSpec = @MtrlSpec,
                    Notes = @Notes,
                    PaintCode = @PaintCode,
                    PercentEntry = @PercentEntry,
                    PhaseCategory = @PhaseCategory,
                    PhaseCode = @PhaseCode,
                    PipeSize1 = @PipeSize1,
                    PipeSize2 = @PipeSize2,
                    PrevEarnMHs = @PrevEarnMHs,
                    PrevEarnQTY = @PrevEarnQTY,
                    ProgDate = @ProgDate,
                    ProjectID = @ProjectID,
                    PjtSystem = @PjtSystem,
                    PjtSystemNo = @PjtSystemNo,
                    Quantity = @Quantity,
                    RevNO = @RevNO,
                    RFINO = @RFINO,
                    ROCBudgetQTY = @ROCBudgetQTY,
                    ROCID = @ROCID,
                    ROCPercent = @ROCPercent,
                    ROCStep = @ROCStep,
                    SchedActNO = @SchedActNO,
                    SchFinish = @SchFinish,
                    SchStart = @SchStart,
                    SecondActno = @SecondActno,
                    SecondDwgNO = @SecondDwgNO,
                    Service = @Service,
                    ShopField = @ShopField,
                    ShtNO = @ShtNO,
                    SubArea = @SubArea,
                    SystemNO = @SystemNO,
                    TagNO = @TagNO,
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
                    UDF11 = @UDF11,
                    UDF12 = @UDF12,
                    UDF13 = @UDF13,
                    UDF14 = @UDF14,
                    UDF15 = @UDF15,
                    UDF16 = @UDF16,
                    UDF17 = @UDF17,
                    RespParty = @RespParty,
                    UDF20 = @UDF20,
                    UOM = @UOM,
                    UpdatedBy = @UpdatedBy,
                    UpdatedUtcDate = @UpdatedUtcDate,
                    WeekEndDate = @WeekEndDate,
                    WorkPackage = @WorkPackage,
                    XRay = @XRay
                WHERE UniqueID = @UniqueID";

                    // Add parameters for ALL columns
                    command.Parameters.AddWithValue("@Area", activity.Area ?? "");
                    command.Parameters.AddWithValue("@AssignedTo", activity.AssignedTo ?? "");
                    command.Parameters.AddWithValue("@AzureUploadUtcDate", activity.AzureUploadUtcDate?.ToString("yyyy-MM-dd") ?? "");
                    command.Parameters.AddWithValue("@Aux1", activity.Aux1 ?? "");
                    command.Parameters.AddWithValue("@Aux2", activity.Aux2 ?? "");
                    command.Parameters.AddWithValue("@Aux3", activity.Aux3 ?? "");
                    command.Parameters.AddWithValue("@BaseUnit", NumericHelper.RoundToPlaces(activity.BaseUnit));
                    command.Parameters.AddWithValue("@BudgetHoursGroup", NumericHelper.RoundToPlaces(activity.BudgetHoursGroup));
                    command.Parameters.AddWithValue("@BudgetHoursROC", NumericHelper.RoundToPlaces(activity.BudgetHoursROC));
                    command.Parameters.AddWithValue("@BudgetMHs", NumericHelper.RoundToPlaces(activity.BudgetMHs));
                    command.Parameters.AddWithValue("@ChgOrdNO", activity.ChgOrdNO ?? "");
                    command.Parameters.AddWithValue("@ClientBudget", NumericHelper.RoundToPlaces(activity.ClientBudget));
                    command.Parameters.AddWithValue("@ClientCustom3", NumericHelper.RoundToPlaces(activity.ClientCustom3));
                    command.Parameters.AddWithValue("@ClientEquivQty", NumericHelper.RoundToPlaces(activity.ClientEquivQty));
                    command.Parameters.AddWithValue("@CompType", activity.CompType ?? "");
                    command.Parameters.AddWithValue("@DateTrigger", activity.DateTrigger);
                    command.Parameters.AddWithValue("@Description", activity.Description ?? "");
                    command.Parameters.AddWithValue("@DwgNO", activity.DwgNO ?? "");
                    command.Parameters.AddWithValue("@EarnQtyEntry", NumericHelper.RoundToPlaces(activity.EarnQtyEntry));
                    command.Parameters.AddWithValue("@EarnedMHsRoc", NumericHelper.RoundToPlaces(activity.EarnedMHsRoc));
                    command.Parameters.AddWithValue("@EqmtNO", activity.EqmtNO ?? "");
                    command.Parameters.AddWithValue("@EquivQTY", NumericHelper.RoundToPlaces(activity.EquivQTY));
                    command.Parameters.AddWithValue("@EquivUOM", activity.EquivUOM ?? "");
                    command.Parameters.AddWithValue("@Estimator", activity.Estimator ?? "");
                    command.Parameters.AddWithValue("@HexNO", activity.HexNO);
                    command.Parameters.AddWithValue("@HtTrace", activity.HtTrace ?? "");
                    command.Parameters.AddWithValue("@InsulType", activity.InsulType ?? "");
                    command.Parameters.AddWithValue("@LineNumber", activity.LineNumber ?? "");
                    command.Parameters.AddWithValue("@LocalDirty", activity.LocalDirty);
                    command.Parameters.AddWithValue("@MtrlSpec", activity.MtrlSpec ?? "");
                    command.Parameters.AddWithValue("@Notes", activity.Notes ?? "");
                    command.Parameters.AddWithValue("@PaintCode", activity.PaintCode ?? "");
                    command.Parameters.AddWithValue("@PercentEntry", NumericHelper.RoundToPlaces(activity.PercentEntry));
                    command.Parameters.AddWithValue("@PhaseCategory", activity.PhaseCategory ?? "");
                    command.Parameters.AddWithValue("@PhaseCode", activity.PhaseCode ?? "");
                    command.Parameters.AddWithValue("@PipeSize1", NumericHelper.RoundToPlaces(activity.PipeSize1));
                    command.Parameters.AddWithValue("@PipeSize2", NumericHelper.RoundToPlaces(activity.PipeSize2));
                    command.Parameters.AddWithValue("@PrevEarnMHs", NumericHelper.RoundToPlaces(activity.PrevEarnMHs));
                    command.Parameters.AddWithValue("@PrevEarnQTY", NumericHelper.RoundToPlaces(activity.PrevEarnQTY));
                    command.Parameters.AddWithValue("@ProgDate", activity.ProgDate?.ToString("yyyy-MM-dd") ?? "");
                    command.Parameters.AddWithValue("@ProjectID", activity.ProjectID ?? "");
                    command.Parameters.AddWithValue("@PjtSystem", activity.PjtSystem ?? "");
                    command.Parameters.AddWithValue("@PjtSystemNo", activity.PjtSystemNo ?? "");
                    command.Parameters.AddWithValue("@Quantity", NumericHelper.RoundToPlaces(activity.Quantity));
                    command.Parameters.AddWithValue("@RevNO", activity.RevNO ?? "");
                    command.Parameters.AddWithValue("@RFINO", activity.RFINO ?? "");
                    command.Parameters.AddWithValue("@ROCBudgetQTY", NumericHelper.RoundToPlaces(activity.ROCBudgetQTY));
                    command.Parameters.AddWithValue("@ROCID", NumericHelper.RoundToPlaces(activity.ROCID));
                    command.Parameters.AddWithValue("@ROCPercent", NumericHelper.RoundToPlaces(activity.ROCPercent));
                    command.Parameters.AddWithValue("@ROCStep", activity.ROCStep ?? "");
                    command.Parameters.AddWithValue("@SchedActNO", activity.SchedActNO ?? "");
                    command.Parameters.AddWithValue("@SchFinish", activity.SchFinish?.ToString("yyyy-MM-dd") ?? "");
                    command.Parameters.AddWithValue("@SchStart", activity.SchStart?.ToString("yyyy-MM-dd") ?? "");
                    command.Parameters.AddWithValue("@SecondActno", activity.SecondActno ?? "");
                    command.Parameters.AddWithValue("@SecondDwgNO", activity.SecondDwgNO ?? "");
                    command.Parameters.AddWithValue("@Service", activity.Service ?? "");
                    command.Parameters.AddWithValue("@ShopField", activity.ShopField ?? "");
                    command.Parameters.AddWithValue("@ShtNO", activity.ShtNO ?? "");
                    command.Parameters.AddWithValue("@SubArea", activity.SubArea ?? "");
                    command.Parameters.AddWithValue("@SystemNO", activity.SystemNO ?? "");
                    command.Parameters.AddWithValue("@TagNO", activity.TagNO ?? "");
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
                    command.Parameters.AddWithValue("@UDF11", activity.UDF11 ?? "");
                    command.Parameters.AddWithValue("@UDF12", activity.UDF12 ?? "");
                    command.Parameters.AddWithValue("@UDF13", activity.UDF13 ?? "");
                    command.Parameters.AddWithValue("@UDF14", activity.UDF14 ?? "");
                    command.Parameters.AddWithValue("@UDF15", activity.UDF15 ?? "");
                    command.Parameters.AddWithValue("@UDF16", activity.UDF16 ?? "");
                    command.Parameters.AddWithValue("@UDF17", activity.UDF17 ?? "");
                    command.Parameters.AddWithValue("@RespParty", activity.RespParty ?? "");
                    command.Parameters.AddWithValue("@UDF20", activity.UDF20 ?? "");
                    command.Parameters.AddWithValue("@UOM", activity.UOM ?? "");
                    command.Parameters.AddWithValue("@UpdatedBy", activity.UpdatedBy ?? "");
                    command.Parameters.AddWithValue("@UpdatedUtcDate", activity.UpdatedUtcDate?.ToString("o") ?? DateTime.UtcNow.ToString("o"));
                    command.Parameters.AddWithValue("@WeekEndDate", activity.WeekEndDate?.ToString("yyyy-MM-dd") ?? "");
                    command.Parameters.AddWithValue("@WorkPackage", activity.WorkPackage ?? "");
                    command.Parameters.AddWithValue("@XRay", NumericHelper.RoundToPlaces(activity.XRay));
                    command.Parameters.AddWithValue("@UniqueID", activity.UniqueID);

                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0;
                });

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ActivityRepository.UpdateActivityInDatabase");
                return false;
            }
        }

        // Bulk update PercentEntry for multiple activities in a single transaction
        // Much faster than calling UpdateActivityInDatabase in a loop
        // Batches updates to avoid SQLite's 999 parameter limit
        public static async Task<int> BulkUpdatePercentAsync(
            List<string> uniqueIds,
            double percentValue,
            string updatedBy)
        {
            if (uniqueIds == null || uniqueIds.Count == 0)
                return 0;

            const int batchSize = 500; // Stay well under SQLite's 999 parameter limit

            return await Task.Run(() =>
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                try
                {
                    int totalAffected = 0;
                    var percentRounded = NumericHelper.RoundToPlaces(percentValue);
                    var updatedDate = DateTime.UtcNow.ToString("o");

                    // Process in batches to avoid "too many SQL variables" error
                    for (int batchStart = 0; batchStart < uniqueIds.Count; batchStart += batchSize)
                    {
                        var batch = uniqueIds.Skip(batchStart).Take(batchSize).ToList();

                        var cmd = connection.CreateCommand();
                        cmd.Transaction = transaction;

                        // Build parameterized WHERE IN clause for this batch
                        var idParams = new List<string>();
                        for (int i = 0; i < batch.Count; i++)
                        {
                            idParams.Add($"@id{i}");
                            cmd.Parameters.AddWithValue($"@id{i}", batch[i]);
                        }

                        cmd.CommandText = $@"
                            UPDATE Activities SET
                                PercentEntry = @percent,
                                EarnQtyEntry = ROUND((@percent / 100.0) * Quantity, 3),
                                UpdatedBy = @updatedBy,
                                UpdatedUtcDate = @updatedDate,
                                LocalDirty = 1
                            WHERE UniqueID IN ({string.Join(",", idParams)})";

                        cmd.Parameters.AddWithValue("@percent", percentRounded);
                        cmd.Parameters.AddWithValue("@updatedBy", updatedBy);
                        cmd.Parameters.AddWithValue("@updatedDate", updatedDate);

                        totalAffected += cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    AppLogger.Info($"Bulk updated PercentEntry to {percentValue}% for {totalAffected} records",
                        "ActivityRepository.BulkUpdatePercentAsync", updatedBy);

                    return totalAffected;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
        }


        // Bulk update a single column for multiple activities in one transaction
        // Each record can have a different value (for partial string replacements)
        // Optional derivedColumns handles recalculated fields (e.g. EarnMHsCalc when BudgetMHs changes)
        public static async Task<int> BulkUpdateColumnAsync(
            string columnName,
            List<(string UniqueID, object? NewValue)> updates,
            string updatedBy,
            Dictionary<string, List<(string UniqueID, object? Value)>>? derivedColumns = null)
        {
            if (updates == null || updates.Count == 0)
                return 0;

            return await Task.Run(() =>
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                try
                {
                    int totalAffected = 0;
                    var updatedDate = DateTime.UtcNow.ToString("o");

                    // Build SET clause: target column + any derived columns + metadata
                    var setClauses = new List<string> { $"{columnName} = @newValue" };
                    var derivedColNames = derivedColumns?.Keys.ToList() ?? new List<string>();
                    foreach (var col in derivedColNames)
                    {
                        setClauses.Add($"{col} = @derived_{col}");
                    }
                    setClauses.Add("UpdatedBy = @updatedBy");
                    setClauses.Add("UpdatedUtcDate = @updatedDate");
                    setClauses.Add("LocalDirty = 1");

                    string sql = $"UPDATE Activities SET {string.Join(", ", setClauses)} WHERE UniqueID = @uniqueId";

                    // Build lookup for derived values by UniqueID
                    var derivedLookups = new Dictionary<string, Dictionary<string, object?>>();
                    if (derivedColumns != null)
                    {
                        foreach (var (col, values) in derivedColumns)
                        {
                            derivedLookups[col] = values.ToDictionary(v => v.UniqueID, v => v.Value);
                        }
                    }

                    // Prepare and reuse command for each record
                    var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = sql;
                    cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@newValue", DBNull.Value));
                    cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@updatedBy", updatedBy));
                    cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@updatedDate", updatedDate));
                    cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@uniqueId", ""));
                    foreach (var col in derivedColNames)
                    {
                        cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter($"@derived_{col}", DBNull.Value));
                    }
                    cmd.Prepare();

                    foreach (var (uniqueId, newValue) in updates)
                    {
                        cmd.Parameters["@newValue"].Value = newValue ?? DBNull.Value;
                        cmd.Parameters["@uniqueId"].Value = uniqueId;

                        foreach (var col in derivedColNames)
                        {
                            if (derivedLookups[col].TryGetValue(uniqueId, out var derivedVal))
                                cmd.Parameters[$"@derived_{col}"].Value = derivedVal ?? DBNull.Value;
                            else
                                cmd.Parameters[$"@derived_{col}"].Value = DBNull.Value;
                        }

                        totalAffected += cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    AppLogger.Info($"Bulk updated {columnName} for {totalAffected} records",
                        "ActivityRepository.BulkUpdateColumnAsync", updatedBy);

                    return totalAffected;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
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
                AppLogger.Error(ex, "ActivityRepository.MethodName");
                throw;
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
            string? whereClause = null)
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
                         var totalCount = Convert.ToInt64(countCommand.ExecuteScalar() ?? 0);

                         // Get valid usernames for validation
                         var validUsernames = GetValidUsernames();

                         // Calculate offset
                         int offset = pageNumber * pageSize;

                         // NEW: Query using NewVantage column names
                         var command = connection.CreateCommand();
                         command.CommandText = $@"SELECT * FROM Activities {whereSQL} ORDER BY UniqueID LIMIT @pageSize OFFSET @offset";
                         command.Parameters.AddWithValue("@pageSize", pageSize);
                         command.Parameters.AddWithValue("@offset", offset);

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

                             DateTime? GetDateTimeFullSafe(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     if (reader.IsDBNull(i)) return null;
                                     var s = reader.GetString(i);
                                     if (DateTime.TryParse(s, out var dt)) return dt; // Keep full datetime with time
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
                             var activity = new Activity();
                             activity.BeginInit();
                             {
                                 activity.ActivityID = GetIntSafe("ActivityID");
                                 activity.HexNO = GetIntSafe("HexNO");

                                 // Categories
                                 activity.CompType = GetStringSafe("CompType");
                                 activity.PhaseCategory = GetStringSafe("PhaseCategory");
                                 activity.ROCStep = GetStringSafe("ROCStep");

                                 // Drawings
                                 activity.DwgNO = GetStringSafe("DwgNO");
                                 activity.RevNO = GetStringSafe("RevNO");
                                 activity.SecondDwgNO = GetStringSafe("SecondDwgNO");
                                 activity.ShtNO = GetStringSafe("ShtNO");

                                 // Notes
                                 activity.Notes = GetStringSafe("Notes");

                                 // Schedule
                                 activity.SecondActno = GetStringSafe("SecondActno");
                                 activity.SchStart = GetDateTimeSafe("SchStart");
                                 activity.SchFinish = GetDateTimeSafe("SchFinish");
                                 activity.WeekEndDate = GetDateTimeSafe("WeekEndDate");
                                 activity.ProgDate = GetDateTimeFullSafe("ProgDate");

                                 // Tags / Aux
                                 activity.Aux1 = GetStringSafe("Aux1");
                                 activity.Aux2 = GetStringSafe("Aux2");
                                 activity.Aux3 = GetStringSafe("Aux3");
                                 activity.Area = GetStringSafe("Area");
                                 activity.ChgOrdNO = GetStringSafe("ChgOrdNO");
                                 activity.Description = GetStringSafe("Description");
                                 activity.EqmtNO = GetStringSafe("EqmtNO");
                                 activity.Estimator = GetStringSafe("Estimator");
                                 activity.InsulType = GetStringSafe("InsulType");
                                 activity.LineNumber = GetStringSafe("LineNumber");
                                 activity.MtrlSpec = GetStringSafe("MtrlSpec");
                                 activity.PhaseCode = GetStringSafe("PhaseCode");
                                 activity.PaintCode = GetStringSafe("PaintCode");
                                 activity.PipeGrade = GetStringSafe("PipeGrade");
                                 activity.ProjectID = GetStringSafe("ProjectID");
                                 activity.RFINO = GetStringSafe("RFINO");
                                 activity.SchedActNO = GetStringSafe("SchedActNO");
                                 activity.Service = GetStringSafe("Service");
                                 activity.ShopField = GetStringSafe("ShopField");
                                 activity.SubArea = GetStringSafe("SubArea");
                                 activity.PjtSystem = GetStringSafe("PjtSystem");
                                 activity.PjtSystemNo = GetStringSafe("PjtSystemNo");
                                 activity.SystemNO = GetStringSafe("SystemNO");
                                 activity.TagNO = GetStringSafe("TagNO");
                                 activity.HtTrace = GetStringSafe("HtTrace");
                                 activity.WorkPackage = GetStringSafe("WorkPackage");

                                 activity.XRay = GetDoubleSafe("XRay");

                                 // Trigger
                                 activity.DateTrigger = GetInt32FromObj("DateTrigger");

                                 // UDFs
                                 activity.UDF1 = GetStringSafe("UDF1");
                                 activity.UDF2 = GetStringSafe("UDF2");
                                 activity.UDF3 = GetStringSafe("UDF3");
                                 activity.UDF4 = GetStringSafe("UDF4");
                                 activity.UDF5 = GetStringSafe("UDF5");
                                 activity.UDF6 = GetStringSafe("UDF6");
                                 activity.UDF7 = GetIntSafe("UDF7");
                                 activity.UDF8 = GetStringSafe("UDF8");
                                 activity.UDF9 = GetStringSafe("UDF9");
                                 activity.UDF10 = GetStringSafe("UDF10");
                                 activity.UDF11 = GetStringSafe("UDF11");
                                 activity.UDF12 = GetStringSafe("UDF12");
                                 activity.UDF13 = GetStringSafe("UDF13");
                                 activity.UDF14 = GetStringSafe("UDF14");
                                 activity.UDF15 = GetStringSafe("UDF15");
                                 activity.UDF16 = GetStringSafe("UDF16");
                                 activity.UDF17 = GetStringSafe("UDF17");
                                 activity.RespParty = GetStringSafe("RespParty");
                                 activity.UniqueID = GetStringSafe("UniqueID");
                                 activity.UDF20 = GetStringSafe("UDF20");
                                 activity.AssignedTo = GetStringSafe("AssignedTo");
                                 activity.UpdatedBy = GetStringSafe("UpdatedBy");
                                 activity.CreatedBy = GetStringSafe("CreatedBy");
                                 activity.UpdatedUtcDate = GetDateTimeFullSafe("UpdatedUtcDate");
                                 activity.LocalDirty = GetIntSafe("LocalDirty");
                                 activity.SyncVersion = GetIntSafe("SyncVersion");

                                 // Values
                                 activity.BaseUnit = GetDoubleSafe("BaseUnit");
                                 activity.BudgetMHs = GetDoubleSafe("BudgetMHs");
                                 activity.BudgetHoursGroup = GetDoubleSafe("BudgetHoursGroup");
                                 activity.BudgetHoursROC = GetDoubleSafe("BudgetHoursROC");
                                 activity.EarnedMHsRoc = GetDoubleSafe("EarnedMHsRoc");
                                 activity.EarnQtyEntry = GetDoubleSafe("EarnQtyEntry");
                                 activity.PercentEntry = GetDoubleSafe("PercentEntry");
                                 activity.Quantity = GetDoubleSafe("Quantity");
                                 activity.UOM = GetStringSafe("UOM");

                                 // Equipment
                                 activity.EquivQTY = GetDoubleSafe("EquivQTY");
                                 activity.EquivUOM = GetStringSafe("EquivUOM");

                                 // ROC
                                 activity.ROCID = GetDoubleSafe("ROCID");
                                 activity.ROCPercent = GetDoubleSafe("ROCPercent");
                                 activity.ROCBudgetQTY = GetDoubleSafe("ROCBudgetQTY");

                                 // Pipe
                                 activity.PipeSize1 = GetDoubleSafe("PipeSize1");
                                 activity.PipeSize2 = GetDoubleSafe("PipeSize2");

                                 // Previous
                                 activity.PrevEarnMHs = GetDoubleSafe("PrevEarnMHs");
                                 activity.PrevEarnQTY = GetDoubleSafe("PrevEarnQTY");

                                 // Client
                                 activity.ClientEquivQty = GetDoubleSafe("ClientEquivQty");
                                 activity.ClientBudget = GetDoubleSafe("ClientBudget");
                                 activity.ClientCustom3 = GetDoubleSafe("ClientCustom3");
                             };
                             activity.EndInit();
                             //activity.SuppressCalculations = false;
                             activities.Add(activity);
                         }

                         // Return tuple with both activities and totalCount
                         return (activities, (int)totalCount);
                     }
                     catch (Exception ex)
                     {
                         AppLogger.Error(ex, "ActivityRepository.MethodName");
                         throw;
                     }
                 });
        }


        /// Get ALL activities with optional filtering (no pagination)
        /// Use this when pagination is disabled and Syncfusion handles virtualization

        /// <param name="whereClause">Optional SQL WHERE clause for filtering</param>
        /// <returns>Tuple of (all activities list, total count)</returns>
        public static async Task<(List<Activity> activities, int totalCount)> GetAllActivitiesAsync(
            string? whereClause = null)
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
                         var totalCount = Convert.ToInt64(countCommand.ExecuteScalar() ?? 0);

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

                             DateTime? GetDateTimeFullSafe(string name)
                             {
                                 try
                                 {
                                     int i = reader.GetOrdinal(name);
                                     if (reader.IsDBNull(i)) return null;
                                     var s = reader.GetString(i);
                                     if (DateTime.TryParse(s, out var dt)) return dt; // Keep full datetime with time
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
                             // Build activity object (EXACT same structure as GetPageAsync)
                             var activity = new Activity();
                             activity.BeginInit();
                             {
                                 activity.ActivityID = GetIntSafe("ActivityID");
                                 activity.HexNO = GetIntSafe("HexNO");

                                 // Schedule dates
                                 activity.SchStart = GetDateTimeSafe("SchStart");
                                 activity.SchFinish = GetDateTimeSafe("SchFinish");
                                 activity.ProgDate = GetDateTimeSafe("ProgDate");
                                 activity.WeekEndDate = GetDateTimeSafe("WeekEndDate");
                                 activity.AzureUploadUtcDate = GetDateTimeSafe("AzureUploadUtcDate");

                                 // Text fields - Category
                                 activity.Area = GetStringSafe("Area");
                                 activity.Aux1 = GetStringSafe("Aux1");
                                 activity.Aux2 = GetStringSafe("Aux2");
                                 activity.Aux3 = GetStringSafe("Aux3");
                                 activity.ChgOrdNO = GetStringSafe("ChgOrdNO");
                                 activity.CompType = GetStringSafe("CompType");
                                 activity.Description = GetStringSafe("Description");
                                 activity.DwgNO = GetStringSafe("DwgNO");
                                 activity.EqmtNO = GetStringSafe("EqmtNO");
                                 activity.Estimator = GetStringSafe("Estimator");
                                 activity.HtTrace = GetStringSafe("HtTrace");
                                 activity.InsulType = GetStringSafe("InsulType");
                                 activity.LineNumber = GetStringSafe("LineNumber");
                                 activity.MtrlSpec = GetStringSafe("MtrlSpec");
                                 activity.Notes = GetStringSafe("Notes");
                                 activity.PaintCode = GetStringSafe("PaintCode");
                                 activity.PhaseCategory = GetStringSafe("PhaseCategory");
                                 activity.PhaseCode = GetStringSafe("PhaseCode");
                                 activity.PipeGrade = GetStringSafe("PipeGrade");
                                 activity.ProjectID = GetStringSafe("ProjectID");
                                 activity.RevNO = GetStringSafe("RevNO");
                                 activity.RFINO = GetStringSafe("RFINO");
                                 activity.ROCStep = GetStringSafe("ROCStep");
                                 activity.SchedActNO = GetStringSafe("SchedActNO");
                                 activity.SecondActno = GetStringSafe("SecondActno");
                                 activity.SecondDwgNO = GetStringSafe("SecondDwgNO");
                                 activity.Service = GetStringSafe("Service");
                                 activity.ShopField = GetStringSafe("ShopField");
                                 activity.ShtNO = GetStringSafe("ShtNO");
                                 activity.SubArea = GetStringSafe("SubArea");
                                 activity.PjtSystem = GetStringSafe("PjtSystem");
                                 activity.PjtSystemNo = GetStringSafe("PjtSystemNo");
                                 activity.SystemNO = GetStringSafe("SystemNO");
                                 activity.TagNO = GetStringSafe("TagNO");
                                 activity.WorkPackage = GetStringSafe("WorkPackage");
                                 activity.DateTrigger = GetIntSafe("DateTrigger");
                                 activity.XRay = GetIntSafe("XRay");

                                 // UDFs
                                 activity.UDF1 = GetStringSafe("UDF1");
                                 activity.UDF2 = GetStringSafe("UDF2");
                                 activity.UDF3 = GetStringSafe("UDF3");
                                 activity.UDF4 = GetStringSafe("UDF4");
                                 activity.UDF5 = GetStringSafe("UDF5");
                                 activity.UDF6 = GetStringSafe("UDF6");
                                 activity.UDF7 = GetIntSafe("UDF7");
                                 activity.UDF8 = GetStringSafe("UDF8");
                                 activity.UDF9 = GetStringSafe("UDF9");
                                 activity.UDF10 = GetStringSafe("UDF10");
                                 activity.UDF11 = GetStringSafe("UDF11");
                                 activity.UDF12 = GetStringSafe("UDF12");
                                 activity.UDF13 = GetStringSafe("UDF13");
                                 activity.UDF14 = GetStringSafe("UDF14");
                                 activity.UDF15 = GetStringSafe("UDF15");
                                 activity.UDF16 = GetStringSafe("UDF16");
                                 activity.UDF17 = GetStringSafe("UDF17");
                                 activity.RespParty = GetStringSafe("RespParty");
                                 activity.UniqueID = GetStringSafe("UniqueID");
                                 activity.UDF20 = GetStringSafe("UDF20");
                                 activity.AssignedTo = GetStringSafe("AssignedTo");
                                 activity.UpdatedBy = GetStringSafe("UpdatedBy");
                                 activity.CreatedBy = GetStringSafe("CreatedBy");
                                 activity.UpdatedUtcDate = GetDateTimeFullSafe("UpdatedUtcDate");
                                 activity.LocalDirty = GetIntSafe("LocalDirty");


                                 // Values
                                 activity.BaseUnit = GetDoubleSafe("BaseUnit");
                                 activity.BudgetMHs = GetDoubleSafe("BudgetMHs");
                                 activity.BudgetHoursGroup = GetDoubleSafe("BudgetHoursGroup");
                                 activity.BudgetHoursROC = GetDoubleSafe("BudgetHoursROC");
                                 activity.EarnedMHsRoc = GetDoubleSafe("EarnedMHsRoc");
                                 activity.EarnQtyEntry = GetDoubleSafe("EarnQtyEntry");
                                 activity.PercentEntry = GetDoubleSafe("PercentEntry");
                                 activity.Quantity = GetDoubleSafe("Quantity");
                                 activity.UOM = GetStringSafe("UOM");   

                                 // Equipment
                                 activity.EquivQTY = GetDoubleSafe("EquivQTY");
                                 activity.EquivUOM = GetStringSafe("EquivUOM"); 

                                 // ROC
                                 activity.ROCID = GetDoubleSafe("ROCID");
                                 activity.ROCPercent = GetDoubleSafe("ROCPercent");
                                 activity.ROCBudgetQTY = GetDoubleSafe("ROCBudgetQTY");

                                 // Pipe
                                 activity.PipeSize1 = GetDoubleSafe("PipeSize1");
                                 activity.PipeSize2 = GetDoubleSafe("PipeSize2");

                                 // Previous
                                 activity.PrevEarnMHs = GetDoubleSafe("PrevEarnMHs");
                                 activity.PrevEarnQTY = GetDoubleSafe("PrevEarnQTY");

                                 // Client
                                 activity.ClientEquivQty = GetDoubleSafe("ClientEquivQty");
                                 activity.ClientBudget = GetDoubleSafe("ClientBudget");
                                 activity.ClientCustom3 = GetDoubleSafe("ClientCustom3");
                             };
                             activity.EndInit();
                             //activity.SuppressCalculations = false;
                             activities.Add(activity);
                         }

                         // Return tuple with both activities and totalCount
                         return (activities, (int)totalCount);
                     }
                     catch
                     {
                         throw;
                     }
                 });
        }


        /// Get totals for filtered records (all pages)

        public static async Task<(double budgetedMHs, double earnedMHs)> GetTotalsAsync(string? whereClause = null)
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
                 AppLogger.Error(ex, "ActivityRepository.MethodName");
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
                     ["EarnMHsCalc"] = "CASE WHEN PercentEntry >= 100 THEN BudgetMHs ELSE ROUND(PercentEntry / 100.0 * BudgetMHs, 3) END"
                 };

                 string? dbExpression = null;
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
                         vals.Add(Convert.ToString(raw) ?? "");
                     }
                 }

                 // Count distinct values (after deduplication)
                 int total = vals.Count;
                 return (vals, total);
             }
             catch (Exception ex)
             {
                 AppLogger.Error(ex, "ActivityRepository.MethodName");
                 return (vals, vals.Count);
             }
         });
        }


        /// Map database reader to Activity object

        private static Activity MapReaderToActivity(SqliteDataReader reader)
        {
            var activity = new Activity();
            activity.BeginInit();

            activity.ActivityID = reader.GetInt32(reader.GetOrdinal("ActivityID"));
            activity.Area = GetStringOrDefault(reader, "Area");
            activity.AssignedTo = GetStringOrDefault(reader, "AssignedTo");
            activity.AzureUploadUtcDate = GetDateTimeOrNull(reader, "AzureUploadUtcDate");
            activity.Aux1 = GetStringOrDefault(reader, "Aux1");
            activity.Aux2 = GetStringOrDefault(reader, "Aux2");
            activity.Aux3 = GetStringOrDefault(reader, "Aux3");
            activity.BaseUnit = GetDoubleOrDefault(reader, "BaseUnit");
            activity.BudgetMHs = GetDoubleOrDefault(reader, "BudgetMHs");
            activity.BudgetHoursGroup = GetDoubleOrDefault(reader, "BudgetHoursGroup");
            activity.BudgetHoursROC = GetDoubleOrDefault(reader, "BudgetHoursROC");
            activity.ChgOrdNO = GetStringOrDefault(reader, "ChgOrdNO");
            activity.ClientBudget = GetDoubleOrDefault(reader, "ClientBudget");
            activity.ClientCustom3 = GetDoubleOrDefault(reader, "ClientCustom3");
            activity.ClientEquivQty = GetDoubleOrDefault(reader, "ClientEquivQty");
            activity.CompType = GetStringOrDefault(reader, "CompType");
            activity.CreatedBy = GetStringOrDefault(reader, "CreatedBy");
            activity.DateTrigger = GetIntOrDefault(reader, "DateTrigger");
            activity.Description = GetStringOrDefault(reader, "Description");
            activity.DwgNO = GetStringOrDefault(reader, "DwgNO");
            activity.EarnedMHsRoc = GetDoubleOrDefault(reader, "EarnedMHsRoc");
            activity.EarnQtyEntry = GetDoubleOrDefault(reader, "EarnQtyEntry");
            activity.EqmtNO = GetStringOrDefault(reader, "EqmtNO");
            activity.EquivQTY = GetDoubleFromStringColumn(reader, "EquivQTY");
            activity.EquivUOM = GetStringOrDefault(reader, "EquivUOM");
            activity.Estimator = GetStringOrDefault(reader, "Estimator");
            activity.HexNO = GetIntOrDefault(reader, "HexNO");
            activity.HtTrace = GetStringOrDefault(reader, "HtTrace");
            activity.InsulType = GetStringOrDefault(reader, "InsulType");
            activity.UpdatedBy = GetStringOrDefault(reader, "UpdatedBy");
            activity.LineNumber = GetStringOrDefault(reader, "LineNumber");
            activity.LocalDirty = GetIntOrDefault(reader, "LocalDirty");
            activity.MtrlSpec = GetStringOrDefault(reader, "MtrlSpec");
            activity.Notes = GetStringOrDefault(reader, "Notes");
            activity.PaintCode = GetStringOrDefault(reader, "PaintCode");
            activity.PercentEntry = GetDoubleOrDefault(reader, "PercentEntry");
            activity.PhaseCategory = GetStringOrDefault(reader, "PhaseCategory");
            activity.PhaseCode = GetStringOrDefault(reader, "PhaseCode");
            activity.PipeGrade = GetStringOrDefault(reader, "PipeGrade");
            activity.PipeSize1 = GetDoubleOrDefault(reader, "PipeSize1");
            activity.PipeSize2 = GetDoubleOrDefault(reader, "PipeSize2");
            activity.PrevEarnMHs = GetDoubleOrDefault(reader, "PrevEarnMHs");
            activity.PrevEarnQTY = GetDoubleOrDefault(reader, "PrevEarnQTY");
            activity.ProgDate = GetDateTimeOrNull(reader, "ProgDate");
            activity.ProjectID = GetStringOrDefault(reader, "ProjectID");
            activity.Quantity = GetDoubleOrDefault(reader, "Quantity");
            activity.RevNO = GetStringOrDefault(reader, "RevNO");
            activity.RFINO = GetStringOrDefault(reader, "RFINO");
            activity.ROCBudgetQTY = GetDoubleOrDefault(reader, "ROCBudgetQTY");
            activity.ROCID = GetDoubleOrDefault(reader, "ROCID");
            activity.ROCPercent = GetDoubleOrDefault(reader, "ROCPercent");
            activity.ROCStep = GetStringOrDefault(reader, "ROCStep");
            activity.SchedActNO = GetStringOrDefault(reader, "SchedActNO");
            activity.SchFinish = GetDateTimeOrNull(reader, "SchFinish");
            activity.SchStart = GetDateTimeOrNull(reader, "SchStart");
            activity.SecondActno = GetStringOrDefault(reader, "SecondActno");
            activity.SecondDwgNO = GetStringOrDefault(reader, "SecondDwgNO");
            activity.Service = GetStringOrDefault(reader, "Service");
            activity.ShopField = GetStringOrDefault(reader, "ShopField");
            activity.ShtNO = GetStringOrDefault(reader, "ShtNO");
            activity.SubArea = GetStringOrDefault(reader, "SubArea");
            activity.PjtSystem = GetStringOrDefault(reader, "PjtSystem");
            activity.PjtSystemNo = GetStringOrDefault(reader, "PjtSystemNo");
            activity.SystemNO = GetStringOrDefault(reader, "SystemNO");
            activity.TagNO = GetStringOrDefault(reader, "TagNO");
            activity.UDF1 = GetStringOrDefault(reader, "UDF1");
            activity.UDF2 = GetStringOrDefault(reader, "UDF2");
            activity.UDF3 = GetStringOrDefault(reader, "UDF3");
            activity.UDF4 = GetStringOrDefault(reader, "UDF4");
            activity.UDF5 = GetStringOrDefault(reader, "UDF5");
            activity.UDF6 = GetStringOrDefault(reader, "UDF6");
            activity.UDF7 = GetIntOrDefault(reader, "UDF7");
            activity.UDF8 = GetStringOrDefault(reader, "UDF8");
            activity.UDF9 = GetStringOrDefault(reader, "UDF9");
            activity.UDF10 = GetStringOrDefault(reader, "UDF10");
            activity.UDF11 = GetStringOrDefault(reader, "UDF11");
            activity.UDF12 = GetStringOrDefault(reader, "UDF12");
            activity.UDF13 = GetStringOrDefault(reader, "UDF13");
            activity.UDF14 = GetStringOrDefault(reader, "UDF14");
            activity.UDF15 = GetStringOrDefault(reader, "UDF15");
            activity.UDF16 = GetStringOrDefault(reader, "UDF16");
            activity.UDF17 = GetStringOrDefault(reader, "UDF17");
            activity.RespParty = GetStringOrDefault(reader, "RespParty");
            activity.UDF20 = GetStringOrDefault(reader, "UDF20");
            activity.UniqueID = GetStringOrDefault(reader, "UniqueID");
            activity.UOM = GetStringOrDefault(reader, "UOM");
            activity.UpdatedUtcDate = GetDateTimeOrNull(reader, "UpdatedUtcDate");
            activity.WeekEndDate = GetDateTimeOrNull(reader, "WeekEndDate");
            activity.WorkPackage = GetStringOrDefault(reader, "WorkPackage");
            activity.XRay = GetDoubleOrDefault(reader, "XRay");

            activity.EndInit();
            return activity;
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
                          ["EarnMHsCalc"] = "CASE WHEN PercentEntry >= 100 THEN BudgetMHs ELSE ROUND(PercentEntry / 100.0 * BudgetMHs, 3) END"
                      };

                      string? dbExpression = null;
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
                              vals.Add(Convert.ToString(raw) ?? "");
                          }
                      }

                      int total = vals.Count;
                      return (vals, total);
                  }
                  catch (Exception ex)
                  {
                      AppLogger.Error(ex, "ActivityRepository.MethodName");
                      return (vals, vals.Count);
                  }
              });
        }

    }
}