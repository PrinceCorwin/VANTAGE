using System.Collections.Generic;
using System.Linq;

namespace VANTAGE.Utilities
{
    
    /// Builds SQL WHERE clauses for filtering activities
    
    public class FilterBuilder
    {
        private List<string> _conditions = new List<string>();

        
        /// Add a condition to the filter
        
        public void AddCondition(string condition)
        {
            if (!string.IsNullOrWhiteSpace(condition))
            {
                _conditions.Add(condition);
            }
        }

        
        /// Add "My Records" filter (assigned to current user)
        
        public void AddMyRecordsFilter(string currentUsername)
        {
            AddCondition($"AssignedTo = '{currentUsername}'");
        }

        
        /// Add text search filter (searches multiple columns)
        
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

        
        /// Add complete/not complete filter
        
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

        
        /// Build final WHERE clause
        
        public string BuildWhereClause()
        {
            if (_conditions.Count == 0)
                return "";

            return "WHERE " + string.Join(" AND ", _conditions);
        }

        
        /// Check if any filters are active
        
        public bool HasFilters()
        {
            return _conditions.Count > 0;
        }

        
        /// Clear all filters
        
        public void Clear()
        {
            _conditions.Clear();
        }
    }
}