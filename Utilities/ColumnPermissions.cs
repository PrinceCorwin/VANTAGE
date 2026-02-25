using System;
using System.Collections.Generic;

namespace VANTAGE.Utilities
{
    public static class ColumnPermissions
    {
        // Read-only fields that cannot be modified via Find & Replace
        public static readonly HashSet<string> ReadOnlyColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ActivityID",
            "UniqueID",
            "LocalDirty",
            "SyncVersion",
            "AzureUploadUtcDate",
            "UpdatedBy",
            "UpdatedUtcDate",
            "CreatedBy",
            "AssignedTo",
            "WeekEndDate",
            "ProgDate",
            "PrevEarnMHs",
            "EarnMHsCalc",
            "PercentCompleteCalc",
            "EarnedQtyCalc",
            "Status",
            "ROCLookupID"
        };

        public static bool IsReadOnly(string columnName)
        {
            return ReadOnlyColumns.Contains(columnName);
        }
    }
}