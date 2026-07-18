using System;
using System.Collections.Generic;
using System.Reflection;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    // Drives the Progress module's global search bar. Builds a one-time cache of typed getter
    // delegates for every writable string property on Activity, so a search matches against ALL
    // text columns with no per-call reflection cost — important at 100k-row scale.
    //
    // The "writable string" rule is deliberate and self-maintaining:
    //   • Numeric / date / bool columns are not string-typed, so they're excluded automatically.
    //   • Calculated / display columns (Status, ROCLookupID, *_Display, AssignedToUsername) are
    //     getter-only, so CanWrite == false excludes them.
    //   • Every real editable text column has a public setter, so it's included without a hand list.
    // Add a new text column to Activity and it becomes searchable with zero changes here.
    public static class ActivityTextSearch
    {
        private static readonly Func<Activity, string?>[] _getters = BuildGetters();

        private static Func<Activity, string?>[] BuildGetters()
        {
            var getters = new List<Func<Activity, string?>>();
            foreach (var prop in typeof(Activity).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType != typeof(string)) continue;
                if (!prop.CanRead || !prop.CanWrite) continue;

                var getMethod = prop.GetGetMethod();
                if (getMethod == null) continue;

                // Compiled open delegate — no reflection on the hot path.
                var getter = (Func<Activity, string?>)Delegate.CreateDelegate(
                    typeof(Func<Activity, string?>), getMethod);
                getters.Add(getter);
            }
            return getters.ToArray();
        }

        // True if searchText appears (case-insensitive substring) in any text column.
        public static bool Matches(Activity activity, string searchText)
        {
            foreach (var getter in _getters)
            {
                var value = getter(activity);
                if (value != null && value.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
