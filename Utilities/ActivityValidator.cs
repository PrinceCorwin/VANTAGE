namespace VANTAGE.Utilities
{
    // Canonical list of required-metadata field names shared across the sync gate,
    // the Import Takeoff dialog, and user-facing error messages. Add/remove here to
    // update every call site in lockstep.
    public static class ActivityRequiredMetadata
    {
        // 9 non-empty string fields that must be populated for an Activity to sync.
        // Order matches the Import Takeoff dialog's visible row order.
        public static readonly string[] Fields = new[]
        {
            "ProjectID", "WorkPackage", "PhaseCode", "CompType",
            "PhaseCategory", "SchedActNO", "Description", "ROCStep", "RespParty"
        };

        // Comma-separated list for user-facing messages (e.g. "ProjectID, WorkPackage, ...").
        public static string FieldsDisplay => string.Join(", ", Fields);

        // Builds a SQL fragment of the form "X IS NULL OR X = '' OR Y IS NULL OR Y = '' ..."
        // tableAlias: pass "" for unqualified columns, or "a." to qualify with a table alias.
        public static string BuildMissingFieldSql(string tableAlias = "")
        {
            return string.Join(" OR ",
                Fields.Select(f => $"{tableAlias}{f} IS NULL OR {tableAlias}{f} = ''"));
        }
    }

    // Centralised hard-validation rules for PercentEntry, ActStart, and ActFin.
    // "Required metadata" constraints (ActStart needed when % > 0, ActFin needed when % = 100)
    // are surfaced through the required-metadata highlighting / sync gate, not here.
    // Returns the first violation message for the given prospective state, or null if valid.
    public static class ActivityValidator
    {
        public static string? Validate(double percentEntry, DateTime? actStart, DateTime? actFin)
        {
            if (actStart.HasValue && actStart.Value.Date > DateTime.Today)
                return "Start date cannot be in the future.";

            if (actFin.HasValue && actFin.Value.Date > DateTime.Today)
                return "Finish date cannot be in the future.";

            if (actStart.HasValue && actFin.HasValue && actFin.Value.Date < actStart.Value.Date)
                return "Finish date cannot be before Start date.";

            if (percentEntry == 0 && actStart.HasValue)
                return "Start date cannot be set when % Complete is 0.";

            if (percentEntry < 100 && actFin.HasValue)
                return "Finish date can only be set when % Complete is 100.";

            return null;
        }
    }
}
