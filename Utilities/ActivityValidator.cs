using System.Reflection;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    // Canonical list of required-metadata field names shared across the sync gate,
    // the Import Takeoff dialog, and user-facing error messages. Add/remove here to
    // update every call site in lockstep.
    public static class ActivityRequiredMetadata
    {
        // 10 non-empty string fields that must be populated for an Activity to sync.
        // Order matches the Import Takeoff dialog's visible row order.
        public static readonly string[] Fields = new[]
        {
            "ProjectID", "WorkPackage", "PhaseCode", "CompType",
            "PhaseCategory", "SchedActNO", "Description", "ROCStep", "RespParty", "UOM"
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

        // Cached PropertyInfo[] for the 10 required-metadata fields on Activity.
        // Reflection lookup happens once at type init; per-row cost is just GetValue.
        private static readonly PropertyInfo[] _requiredFieldProps =
            ActivityRequiredMetadata.Fields
                .Select(f => typeof(Activity).GetProperty(f)!)
                .ToArray();

        // Returns every canonical-sync violation on the given activity. Empty list
        // means the row is fully valid: every ActivityRequiredMetadata field is
        // non-blank, the conditional date-required rules pass, AND Validate returns
        // null for the current %/start/finish combination. Reusable batch-validation
        // primitive — shared by SyncManager's pre-sync gate and Submit Week's
        // date-rule gate so both behave identically and stay in lockstep with rule
        // changes here. Project-exists check is intentionally NOT included; it
        // requires a Projects-table lookup and lives at call sites where the
        // valid-ProjectID set is already cached or scope is a single project.
        public static List<string> GetAllViolations(Activity activity)
        {
            var violations = new List<string>();

            for (int i = 0; i < ActivityRequiredMetadata.Fields.Length; i++)
            {
                var raw = _requiredFieldProps[i].GetValue(activity) as string;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    violations.Add($"Missing required field: {ActivityRequiredMetadata.Fields[i]}");
                }
            }

            // Conditional date-required rules — counterpart to the inverse rules in
            // Validate. Together they enforce: ActStart present iff % > 0, ActFin
            // present iff % = 100.
            if (activity.PercentEntry > 0 && !activity.ActStart.HasValue)
            {
                violations.Add("ActStart is required when % Complete > 0.");
            }
            if (activity.PercentEntry >= 100 && !activity.ActFin.HasValue)
            {
                violations.Add("ActFin is required when % Complete is 100.");
            }

            var dateViolation = Validate(activity.PercentEntry, activity.ActStart, activity.ActFin);
            if (dateViolation != null)
            {
                violations.Add(dateViolation);
            }

            return violations;
        }
    }
}
