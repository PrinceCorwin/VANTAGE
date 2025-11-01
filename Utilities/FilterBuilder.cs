using System.Collections.Generic;
using System.Linq;

namespace VANTAGE.Utilities
{
    /// <summary>
    /// Builds SQL WHERE clauses for filtering activities
    /// </summary>
    public class FilterBuilder
    {
        private List<string> _conditions = new List<string>();

        /// <summary>
        /// Add a condition to the filter
        /// </summary>
        public void AddCondition(string condition)
        {
            if (!string.IsNullOrWhiteSpace(condition))
            {
                _conditions.Add(condition);
            }
        }

        /// <summary>
        /// Add "My Records" filter (assigned to current user)
        /// </summary>
        public void AddMyRecordsFilter(string currentUsername)
        {
            AddCondition($"AssignedTo = '{currentUsername}'");
        }

        /// <summary>
        /// Add text search filter (searches multiple columns)
        /// </summary>
        public void AddTextSearch(string searchText)
        {
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                // Escape single quotes
                searchText = searchText.Replace("'", "''");

                // Search across key columns using NewVantage names
                AddCondition($@"(
                    Description LIKE '%{searchText}%' OR
                    TagNO LIKE '%{searchText}%' OR
                    Area LIKE '%{searchText}%' OR
                    CompType LIKE '%{searchText}%' OR
                    UniqueID LIKE '%{searchText}%'
                )");
            }
        }

        /// <summary>
        /// Add complete/not complete filter
        /// </summary>
        public void AddCompletionFilter(bool complete)
        {
            if (complete)
            {
                AddCondition("PercentEntry >= 100");
            }
            else
            {
                AddCondition("PercentEntry < 100");
            }
        }

        /// <summary>
        /// Build final WHERE clause
        /// </summary>
        public string BuildWhereClause()
        {
            if (_conditions.Count == 0)
                return "";

            return "WHERE " + string.Join(" AND ", _conditions);
        }

        /// <summary>
        /// Check if any filters are active
        /// </summary>
        public bool HasFilters()
        {
            return _conditions.Count > 0;
        }

        /// <summary>
        /// Clear all filters
        /// </summary>
        public void Clear()
        {
            _conditions.Clear();
        }
    }
}