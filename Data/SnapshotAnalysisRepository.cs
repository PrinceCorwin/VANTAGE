using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Repositories
{
    // SnapshotAnalysis is a local SQLite cache that mirrors the Activities schema and
    // persists across app restarts. The Analysis view's Snapshot mode aggregates from
    // this table. The picker dialog clears + repopulates it from Azure VMS_ProgressSnapshots
    // for the user's chosen submission tuples.
    public static class SnapshotAnalysisRepository
    {
        // Columns we pull from Azure VMS_ProgressSnapshots and write to local SnapshotAnalysis.
        // Order MUST match the SELECT and INSERT below. Identity + aggregation + Group By fields
        // are covered; LocalDirty / SyncVersion / AzureUploadUtcDate / ActivityID are omitted —
        // ActivityID doesn't exist on VMS_ProgressSnapshots (it lives on VMS_Activities only),
        // and the other three have no meaning for a snapshot cache.
        private static readonly string[] _columns = new[]
        {
            "UniqueID", "WeekEndDate", "Area", "AssignedTo",
            "Aux1", "Aux2", "Aux3", "BaseUnit", "BudgetHoursGroup",
            "BudgetHoursROC", "BudgetMHs", "ChgOrdNO", "ClientBudget", "ClientCustom3",
            "ClientEquivQty", "CompType", "CreatedBy", "DateTrigger", "Description",
            "DwgNO", "EarnQtyEntry", "EarnedMHsRoc", "EqmtNO", "EquivQTY",
            "EquivUOM", "Estimator", "HexNO", "HtTrace", "InsulType",
            "LineNumber", "MtrlSpec", "Notes", "PaintCode", "PercentEntry",
            "PhaseCategory", "PhaseCode", "PipeGrade", "PipeSize1", "PipeSize2",
            "PrevEarnMHs", "PrevEarnQTY", "ProgDate", "ProjectID", "Quantity",
            "RevNO", "RFINO", "ROCBudgetQTY", "ROCID", "ROCPercent",
            "ROCStep", "SchedActNO", "ActFin", "ActStart", "SecondActno",
            "SecondDwgNO", "Service", "ShopField", "ShtNO", "SubArea",
            "PjtSystem", "PjtSystemNo", "TagNO", "UDF1", "UDF2",
            "UDF3", "UDF4", "UDF5", "UDF6", "UDF7",
            "UDF8", "UDF9", "UDF10", "UDF11", "UDF12",
            "UDF13", "UDF14", "UDF15", "UDF16", "UDF17",
            "RespParty", "UDF20", "UpdatedBy", "UpdatedUtcDate", "UOM",
            "WorkPackage", "XRay"
        };

        private static string ColumnList => string.Join(", ", _columns);

        // SELECT-side qualified list — needed because the INNER JOIN derived-table 'snaps'
        // exposes columns that collide with snapshot columns (WeekEndDate, ProjectID,
        // AssignedTo). Qualifying every snapshot column with 's.' makes the SELECT unambiguous.
        private static string ColumnListAliased =>
            string.Join(", ", System.Linq.Enumerable.Select(_columns, c => "s." + c));

        // Returns the (AssignedTo, ProjectID, WeekEndDate) tuples currently loaded in the
        // local SnapshotAnalysis table, with per-tuple row counts. Used by the picker
        // dialog to pre-check the rows the user previously selected.
        public static async Task<List<AnalysisSnapshotKey>> GetCurrentSnapshotKeysAsync()
        {
            return await Task.Run(() =>
            {
                var keys = new List<AnalysisSnapshotKey>();
                try
                {
                    using var conn = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT AssignedTo, ProjectID, WeekEndDate, COUNT(*) AS RowCount
                        FROM SnapshotAnalysis
                        GROUP BY AssignedTo, ProjectID, WeekEndDate
                        ORDER BY WeekEndDate DESC, ProjectID, AssignedTo";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        keys.Add(new AnalysisSnapshotKey
                        {
                            AssignedTo = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                            ProjectID = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            WeekEndDate = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            RowCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                        });
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "SnapshotAnalysisRepository.GetCurrentSnapshotKeysAsync");
                }
                return keys;
            });
        }

        // Wipes the local SnapshotAnalysis table and repopulates it from Azure
        // VMS_ProgressSnapshots, filtered to the given submission tuples. Returns the
        // number of rows inserted. Throws on Azure failure (caller surfaces). The local
        // wipe + insert happens in a single SQLite transaction so partial states never
        // become visible.
        public static async Task<int> PopulateFromAzureAsync(
            List<AnalysisSnapshotKey> selected, IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                if (selected == null || selected.Count == 0)
                {
                    // Empty selection means "clear the cache" — wipe and return 0.
                    ClearLocal();
                    return 0;
                }

                progress?.Report("Fetching snapshot rows from Azure...");

                // Step 1: pull rows from Azure into memory.
                var azureRows = new List<object?[]>();
                using (var azureConn = AzureDbManager.GetConnection())
                {
                    azureConn.Open();
                    using var cmd = azureConn.CreateCommand();
                    cmd.CommandTimeout = 600;

                    var valuesRows = new List<string>();
                    for (int i = 0; i < selected.Count; i++)
                    {
                        var s = selected[i];
                        valuesRows.Add($"(@sp{i}, @sw{i}, @su{i})");
                        cmd.Parameters.AddWithValue($"@sp{i}", s.ProjectID);
                        cmd.Parameters.AddWithValue($"@sw{i}", s.WeekEndDate);
                        cmd.Parameters.AddWithValue($"@su{i}", s.AssignedTo);
                    }

                    cmd.CommandText = $@"
                        SELECT {ColumnListAliased}
                        FROM VMS_ProgressSnapshots s
                        INNER JOIN (VALUES {string.Join(",", valuesRows)}) snaps(projectId, weekEndDate, assignedTo)
                            ON s.ProjectID = snaps.projectId
                           AND s.WeekEndDate = snaps.weekEndDate
                           AND s.AssignedTo = snaps.assignedTo";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var row = new object?[_columns.Length];
                        for (int i = 0; i < _columns.Length; i++)
                            row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        azureRows.Add(row);
                    }
                }

                progress?.Report($"Writing {azureRows.Count} rows to local cache...");

                // Step 2: wipe + bulk insert into local SQLite in one transaction. Indexes
                // are dropped before insert and recreated after — same pattern Refill uses.
                using var localConn = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                localConn.Open();

                using (var pragmaCmd = localConn.CreateCommand())
                {
                    pragmaCmd.CommandText = @"
                        PRAGMA synchronous = OFF;
                        PRAGMA temp_store = MEMORY;
                        PRAGMA cache_size = -200000;";
                    pragmaCmd.ExecuteNonQuery();
                }

                using var tx = localConn.BeginTransaction();

                using (var dropIdxCmd = localConn.CreateCommand())
                {
                    dropIdxCmd.Transaction = tx;
                    dropIdxCmd.CommandText = @"
                        DROP INDEX IF EXISTS idx_snapanalysis_project;
                        DROP INDEX IF EXISTS idx_snapanalysis_keys;";
                    dropIdxCmd.ExecuteNonQuery();
                }

                using (var wipeCmd = localConn.CreateCommand())
                {
                    wipeCmd.Transaction = tx;
                    wipeCmd.CommandText = "DELETE FROM SnapshotAnalysis";
                    wipeCmd.ExecuteNonQuery();
                }

                if (azureRows.Count > 0)
                {
                    string placeholders = string.Join(", ",
                        System.Linq.Enumerable.Range(0, _columns.Length).Select(i => "@p" + i));

                    using var insertCmd = localConn.CreateCommand();
                    insertCmd.Transaction = tx;
                    insertCmd.CommandText =
                        $"INSERT INTO SnapshotAnalysis ({ColumnList}) VALUES ({placeholders})";
                    for (int i = 0; i < _columns.Length; i++)
                        insertCmd.Parameters.Add(new SqliteParameter("@p" + i, DBNull.Value));
                    insertCmd.Prepare();

                    foreach (var row in azureRows)
                    {
                        for (int i = 0; i < _columns.Length; i++)
                            insertCmd.Parameters[i].Value = row[i] ?? DBNull.Value;
                        insertCmd.ExecuteNonQuery();
                    }
                }

                using (var createIdxCmd = localConn.CreateCommand())
                {
                    createIdxCmd.Transaction = tx;
                    createIdxCmd.CommandText = @"
                        CREATE INDEX IF NOT EXISTS idx_snapanalysis_project ON SnapshotAnalysis(ProjectID);
                        CREATE INDEX IF NOT EXISTS idx_snapanalysis_keys ON SnapshotAnalysis(AssignedTo, ProjectID, WeekEndDate);";
                    createIdxCmd.ExecuteNonQuery();
                }

                tx.Commit();

                AppLogger.Info(
                    $"SnapshotAnalysis repopulated with {azureRows.Count} rows from {selected.Count} snapshot tuple(s)",
                    "SnapshotAnalysisRepository.PopulateFromAzureAsync");

                return azureRows.Count;
            });
        }

        // Wipes the local SnapshotAnalysis table. Used when the user explicitly clears
        // the selection (applies the picker with no rows checked).
        private static void ClearLocal()
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={DatabaseSetup.DbPath}");
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM SnapshotAnalysis";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SnapshotAnalysisRepository.ClearLocal");
            }
        }
    }
}
