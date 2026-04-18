using System;

namespace VANTAGE.Utilities
{
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
