using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Utilities;



namespace VANTAGE.ViewModels
{

    public class ProgressViewModel : INotifyPropertyChanged
    {
        private BulkObservableCollection<Activity> _activities;
        private ICollectionView _activitiesView;
        private string _searchText;
        private int _totalRecordCount;
        private int _filteredCount;
        private bool _isLoading;
        private Dictionary<string, ColumnFilter> _activeFilters = new Dictionary<string, ColumnFilter>();
        public IEnumerable<string> ActiveFilterColumns => _activeFilters.Keys;
        private bool _myRecordsActive = false;
        private string _myRecordsUser = null;
        private string BuildUnifiedWhereClause()
        {
            var fb = new FilterBuilder();

            // 1) search box
            if (!string.IsNullOrWhiteSpace(_searchText))
                fb.AddTextSearch(_searchText);

            // 2) per-column filters
            foreach (var filter in _activeFilters.Values)
            {
                // Column names now match database - no translation needed!
        
                // Special handling for pre-built IN condition
                if (filter.FilterType == "IN")
                {
                    // filter.FilterValue will already be a SQL fragment like "Column IN ('a','b')" or full condition
                    fb.AddCondition(filter.FilterValue);
                    continue;
                }

                var cond = BuildFilterCondition(filter.ColumnName, filter.FilterType, filter.FilterValue);
                if (!string.IsNullOrEmpty(cond))
                    fb.AddCondition(cond);
            }

            // 3) My Records
            if (_myRecordsActive && !string.IsNullOrWhiteSpace(_myRecordsUser))
                fb.AddMyRecordsFilter(_myRecordsUser);

            return fb.BuildWhereClause();
        }

        private async Task RebuildAndReloadAsync()
        {
            _currentWhereClause = BuildUnifiedWhereClause();
            await LoadAllActivitiesAsync();  // Load all activities with new filter
        }

        public int TotalRecords
        {
            get => _totalRecords;
            set
            {
                _totalRecords = value;
                OnPropertyChanged(nameof(TotalRecords));
            }
        }
        public async Task ClearAllFiltersAsync()
        {
            try
            {
                IsLoading = true;

                _activeFilters.Clear();   // remove column filters
                _myRecordsActive = false; // remove My Records
                _myRecordsUser = null;
                _searchText = "";         // clear search box (optional; keep if this is your desired UX)
                OnPropertyChanged(nameof(SearchText));
                OnPropertyChanged(nameof(ActiveFilterColumns));

                await RebuildAndReloadAsync();
            }
            catch
            {
                // (optional) log
            }
            finally
            {
                IsLoading = false;
            }
        }

        
        /// Apply a filter to a column
        
        public async Task ApplyFilter(string columnName, string filterType, string filterValue)
        {
            _activeFilters[columnName] = new ColumnFilter
            {
                ColumnName = columnName,
                FilterType = filterType,
                FilterValue = filterValue
            };

            OnPropertyChanged(nameof(ActiveFilterColumns));

            await RebuildAndReloadAsync();
        }
        
        /// Apply a single column filter to the data
        
        private IEnumerable<Activity> ApplyColumnFilter(IEnumerable<Activity> data, ColumnFilter filter)
        {
            return data.Where(activity =>
            {
                // Get the property value using reflection
                var property = typeof(Activity).GetProperty(filter.ColumnName);
                if (property == null) return true; // Property not found, don't filter

                var value = property.GetValue(activity);
                var stringValue = value?.ToString() ?? "";
                var filterValue = filter.FilterValue ?? "";

                // Apply filter based on type
                switch (filter.FilterType)
                {
                    case "Equals":
                        return stringValue.Equals(filterValue, StringComparison.OrdinalIgnoreCase);

                    case "Does Not Equal":
                        return !stringValue.Equals(filterValue, StringComparison.OrdinalIgnoreCase);

                    case "Contains":
                        return stringValue.IndexOf(filterValue, StringComparison.OrdinalIgnoreCase) >= 0;

                    case "Does Not Contain":
                        return stringValue.IndexOf(filterValue, StringComparison.OrdinalIgnoreCase) < 0;

                    case "Begins With":
                        return stringValue.StartsWith(filterValue, StringComparison.OrdinalIgnoreCase);

                    case "Ends With":
                        return stringValue.EndsWith(filterValue, StringComparison.OrdinalIgnoreCase);

                    default:
                        return true; // Unknown filter type, don't filter
                }
            });
        }
        
        /// Clear filter from a column
        
        public async Task ClearFilter(string columnName)
        {
            _activeFilters.Remove(columnName);
            OnPropertyChanged(nameof(ActiveFilterColumns));
            await RebuildAndReloadAsync();
        }


        
        /// Apply all active filters to the data
        
        private async Task ApplyAllFiltersAsync()
        {
            try
            {
                IsLoading = true;

                // Build WHERE clause from active filters
                var filterBuilder = new FilterBuilder();

                // Add each active filter to WHERE clause
                foreach (var filter in _activeFilters.Values)
                {
                    // Column names now match database - no translation needed!

                    // Build SQL condition based on filter type
                    string condition = BuildFilterCondition(filter.ColumnName, filter.FilterType, filter.FilterValue);
                    if (!string.IsNullOrEmpty(condition))
                    {
                        filterBuilder.AddCondition(condition);
                    }
                }

                // Store the WHERE clause
                _currentWhereClause = filterBuilder.BuildWhereClause();

                // Reset to first page


                // Reload with filter
                await LoadCurrentPageAsync();
            }
            catch (Exception ex)
            {
                // TODO: Add proper logging when logging system is implemented
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string BuildFilterCondition(string dbColumnName, string filterType, string filterValue)
        {
            filterValue = (filterValue ?? "").Replace("'", "''");

            // Special handling for Status (calculated property)
            if (dbColumnName == "Status")
            {
                string statusCase = "CASE WHEN PercentEntry =0 THEN 'Not Started' WHEN PercentEntry >=100 THEN 'Complete' ELSE 'In Progress' END";
                switch (filterType)
                {
                    case "Equals":
                        return $"{statusCase} = '{filterValue}'";
                    case "Does Not Equal":
                        return $"{statusCase} <> '{filterValue}'";
                    case "Contains":
                        return $"{statusCase} LIKE '%{filterValue}%'";

                    case "Does Not Contain":
                        return $"{statusCase} NOT LIKE '%{filterValue}%'";

                    case "Begins With":
                    case "Starts With":
                        return $"{statusCase} LIKE '{filterValue}%'";

                    case "Ends With":
                        return $"{statusCase} LIKE '%{filterValue}'";
                    default:
                        return "";
                }
            }

            // Numeric columns
            var numericColumns = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "Quantity", "EarnQtyEntry", "PercentEntry", "PercentEntry_Display", "BudgetMHs", "EarnMHsCalc", "ROCPercent", "ROCBudgetQTY", "PipeSize1", "PipeSize2", "PrevEarnMHs", "PrevEarnQTY", "ClientEquivQty", "ClientBudget", "ClientCustom3", "XRay", "BaseUnit", "BudgetHoursGroup", "BudgetHoursROC", "EarnedMHsRoc", "EquivQTY", "ROCID", "HexNO"
            };
            if (numericColumns.Contains(dbColumnName))
            {
                switch (filterType)
                {
                    case "Equals":
                        return $"{dbColumnName} = {filterValue}";
                    case "Does Not Equal":
                        return $"{dbColumnName} <> {filterValue}";
                    case "Greater Than":
                        return $"{dbColumnName} > {filterValue}";
                    case "Greater Than Or Equal":
                        return $"{dbColumnName} >= {filterValue}";
                    case "Less Than":
                        return $"{dbColumnName} < {filterValue}";
                    case "Less Than Or Equal":
                        return $"{dbColumnName} <= {filterValue}";
                    case "Between":
                        var parts = filterValue.Split(',');
                        if (parts.Length ==2)
                            return $"{dbColumnName} BETWEEN {parts[0]} AND {parts[1]}";
                        return "";
                    default:
                        return "";
                }
            }

            // Date columns
            var dateColumns = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "SchStart", "SchFinish", "ProgDate", "WeekEndDate", "AzureUploadDate"
            };
            if (dateColumns.Contains(dbColumnName))
            {
                switch (filterType)
                {
                    case "Equals":
                        return $"{dbColumnName} = '{filterValue}'";
                    case "Not Equal":
                        return $"{dbColumnName} <> '{filterValue}'";
                    case "Before":
                        return $"{dbColumnName} < '{filterValue}'";
                    case "After":
                        return $"{dbColumnName} > '{filterValue}'";
                    case "Between":
                        var parts = filterValue?.Split(',');
                        if (parts != null && parts.Length ==2)
                            return $"{dbColumnName} BETWEEN '{parts[0]}' AND '{parts[1]}'";
                        return "";
                    case "Is Blank":
                        return $"({dbColumnName} IS NULL OR {dbColumnName} = '')";
                    case "Is Not Blank":
                        return $"({dbColumnName} IS NOT NULL AND {dbColumnName} <> '')";
                    default:
                        return "";
                }
            }

            // Default: text columns
            switch (filterType)
            {
                case "Contains":
                    return $"{dbColumnName} LIKE '%{filterValue}%'";
                case "Does Not Contain":
                    return $"{dbColumnName} NOT LIKE '%{filterValue}%'";
                case "Equals":
                    return $"{dbColumnName} = '{filterValue}'";
                case "Does Not Equal":
                case "Not Equal":
                    return $"{dbColumnName} <> '{filterValue}'";
                case "Begins With":
                case "Starts With":
                    return $"{dbColumnName} LIKE '{filterValue}%'";
                case "Ends With":
                    return $"{dbColumnName} LIKE '%{filterValue}'";
                case "Is Blank":
                    return $"({dbColumnName} IS NULL OR {dbColumnName} = '')";
                case "Is Not Blank":
                    return $"({dbColumnName} IS NOT NULL AND {dbColumnName} <> '')";
                default:
                    return "";
            }
        }

        public ProgressViewModel()
        {
            // Initialize collections
            _activities = new BulkObservableCollection<Activity>();
            _activitiesView = CollectionViewSource.GetDefaultView(_activities);
            _searchText = "";
            _totalRecordCount = 0;
            _isLoading = false;
        }

        // ========================================
        // PROPERTIES
        // ========================================
        private double _budgetedMHs;
        public double BudgetedMHs
        {
            get => _budgetedMHs;
            set
            {
                _budgetedMHs = value;
                OnPropertyChanged(nameof(BudgetedMHs));
                OnPropertyChanged(nameof(PercentComplete));
            }
        }

        private double _earnedMHs;
        public double EarnedMHs
        {
            get => _earnedMHs;
            set
            {
                _earnedMHs = value;
                OnPropertyChanged(nameof(EarnedMHs));
                OnPropertyChanged(nameof(PercentComplete));
            }
        }

        public double PercentComplete
        {
            get
            {
                if (BudgetedMHs == 0) return 0;
                return (EarnedMHs / BudgetedMHs) * 100;
            }
        }
        public async Task UpdateTotalsAsync()
        {
            try
            {
                var (budgeted, earned) = await ActivityRepository.GetTotalsAsync(_currentWhereClause);
                BudgetedMHs = budgeted;
                EarnedMHs = earned;

            }
            catch (Exception ex)
            {
                // TODO: Add proper logging when logging system is implemented
            }
        }
        public BulkObservableCollection<Activity> Activities
        {
            get => _activities;
            set
            {
                _activities = value;
                OnPropertyChanged(nameof(Activities));
                _activitiesView = CollectionViewSource.GetDefaultView(_activities);
                OnPropertyChanged(nameof(ActivitiesView));
            }
        }

        public ICollectionView ActivitiesView => _activitiesView;

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                _ = ApplySearchFilterAsync(); // Fire and forget async
            }
        }

        private async Task ApplySearchFilterAsync()
        {
            try
            {
                IsLoading = true;
                await RebuildAndReloadAsync();
            }
            catch (Exception ex)
            {
                // TODO: Add proper logging when logging system is implemented
            }
            finally
            {
                IsLoading = false;
            }
        }

        public int TotalRecordCount
        {
            get => _totalRecordCount;
            set
            {
                _totalRecordCount = value;
                OnPropertyChanged(nameof(TotalRecordCount));
            }
        }

        public int FilteredCount
        {
            get => _filteredCount;
            set
            {
                _filteredCount = value;
                OnPropertyChanged(nameof(FilteredCount));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        // ========================================
        // METHODS
        // ========================================
        private string _currentWhereClause = null;

        public async Task ApplyMyRecordsFilter(bool active, string currentUsername)
        {
            try
            {
                IsLoading = true;

                _myRecordsActive = active;
                _myRecordsUser = active ? currentUsername : null;

                await RebuildAndReloadAsync();
            }
            catch (Exception ex)
            {
                // TODO: Add proper logging when logging system is implemented
                System.Diagnostics.Debug.WriteLine($"✗ Error applying My Records filter: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// Load initial data - gets total count and first page

        public async Task LoadInitialDataAsync()
        {
            try
            {
                IsLoading = true;
                TotalRecordCount = await ActivityRepository.GetTotalCountAsync();
                await LoadAllActivitiesAsync();  // Changed from LoadCurrentPageAsync
            }
            catch (Exception ex)
            {
                // TODO: Add proper logging when logging system is implemented
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }


        /// Load the current page of data

        /// <summary>
        /// Load all activities with current filter (no pagination)
        /// </summary>
        public async Task LoadAllActivitiesAsync()
        {
            try
            {
                IsLoading = true;

                // Get ALL activities with current filter
                var (allActivities, totalCount) = await ActivityRepository.GetAllActivitiesAsync(_currentWhereClause);

                // Update counts
                TotalRecordCount = await ActivityRepository.GetTotalCountAsync(); // unfiltered DB total
                FilteredCount = totalCount; // filtered count

                // Clear and populate
                Activities.Clear();
                Activities.AddRange(allActivities);

                // Update totals for all filtered records
                await UpdateTotalsAsync();

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {allActivities.Count} activities (no pagination)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading activities: {ex.Message}");
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }



        /// <summary>
        /// Refresh all activities
        /// </summary>
        public async Task RefreshAsync()
        {
            await LoadAllActivitiesAsync();
        }
        
        /// Filter predicate for activities
        
        private bool FilterActivity(object obj)
        {
            if (obj is not Activity activity)
                return false;

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.ToLower();

                if (activity.UniqueID?.ToLower().Contains(search) == true ||
                    activity.Description?.ToLower().Contains(search) == true ||
                    activity.TagNO?.ToLower().Contains(search) == true ||
                    activity.ProjectID?.ToLower().Contains(search) == true)
                {
                    return true;
                }

                return false;
            }

            return true;
        }

        // Returns unique values for a column from the currently filtered records (in-memory)
        public IEnumerable<string> GetUniqueValuesForColumn(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                return Enumerable.Empty<string>();

            // Use reflection to get property values for the column
            return _activities
                .Select(a => {
                    var prop = typeof(Activity).GetProperty(columnName);
                    if (prop == null) return null;
                    var val = prop.GetValue(a);
                    return val?.ToString() ?? string.Empty;
                })
                .Distinct()
                .ToList();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    // Add this helper class at the bottom of the file (outside ProgressViewModel class)
    public class ColumnFilter
    {
        public string ColumnName { get; set; }
        public string FilterType { get; set; }
        public string FilterValue { get; set; }
    }
}