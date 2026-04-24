namespace VANTAGE.Utilities
{
    // Central rule for which SnapshotData properties are editable in ModifySnapshotDialog.
    // Mirrors the Progress view's editable baseline (see ImportTakeoffDialog.ExcludedColumns)
    // with one intentional difference: ActStart and ActFin ARE editable here because editing
    // a historical snapshot's progress often requires correcting the associated dates.
    public static class SnapshotEditableColumns
    {
        public static readonly HashSet<string> NonEditable = new(StringComparer.OrdinalIgnoreCase)
        {
            // Identity / sync system
            "UniqueID",
            "AzureUploadUtcDate",
            "UpdatedBy",
            "UpdatedUtcDate",
            "CreatedBy",
            "ProgDate",
            // Calculated / weekly tracking
            "PrevEarnMHs",
            "EarnedMHsRoc",
            // P6-driven plan dates (not user-editable anywhere in the app)
            "PlanStart",
            "PlanFin"
        };

        public static bool IsEditable(string propertyName) => !NonEditable.Contains(propertyName);
    }
}
