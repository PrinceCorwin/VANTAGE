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
        private int _currentPage;
        private int _pageSize;
        private int _totalPages;
        private bool _isLoading;
        private Dictionary<string, ColumnFilter> _activeFilters = new Dictionary<string, ColumnFilter>();
        private int _totalRecords;
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

                // Clear all filter state
                _activeFilters.Clear();
                _currentWhereClause = null;
                _searchText = string.Empty;  // internal value
                OnPropertyChanged(nameof(SearchText)); // keep bindings honest

                // Reset paging
                CurrentPage = 0;

                // Reload unfiltered page 0
                await LoadCurrentPageAsync();

                // Ensure "Total: Z" shows the unfiltered database total
                TotalRecordCount = await ActivityRepository.GetTotalCountAsync();
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

        /// <summary>
        /// Apply a filter to a column
        /// </summary>
        public async Task ApplyFilter(string columnName, string filterType, string filterValue)
        {
            _activeFilters[columnName] = new ColumnFilter
            {
                ColumnName = columnName,
                FilterType = filterType,
                FilterValue = filterValue
            };
            await ApplyAllFiltersAsync();
        }
        /// <summary>
        /// Apply a single column filter to the data
        /// </summary>
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
        /// <summary>
        /// Clear filter from a column
        /// </summary>
        public async Task ClearFilter(string columnName)
        {
            _activeFilters.Remove(columnName);
            await ApplyAllFiltersAsync();
        }

        /// <summary>
        /// Apply all active filters to the data
        /// </summary>
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
                    string dbColumnName = ColumnMapper.GetDbColumnName(filter.ColumnName);

                    // Build SQL condition based on filter type
                    string condition = BuildFilterCondition(dbColumnName, filter.FilterType, filter.FilterValue);
                    if (!string.IsNullOrEmpty(condition))
                    {
                        filterBuilder.AddCondition(condition);
                    }
                }

                // Store the WHERE clause
                _currentWhereClause = filterBuilder.BuildWhereClause();

                // Reset to first page
                CurrentPage = 0;

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

            switch (filterType)
            {
                case "Contains":
                    return $"{dbColumnName} LIKE '%{filterValue}%'";
                case "Does Not Contain":
                    return $"{dbColumnName} NOT LIKE '%{filterValue}%'";
                case "Equals":
                    return $"{dbColumnName} = '{filterValue}'";
                case "Does Not Equal":
                    return $"{dbColumnName} <> '{filterValue}'";
                case "Begins With":      // matches your ListBox text
                case "Starts With":      // allow both, just in case
                    return $"{dbColumnName} LIKE '{filterValue}%'";
                case "Ends With":
                    return $"{dbColumnName} LIKE '%{filterValue}'";
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
            _currentPage = 0;
            _pageSize = 500; // Load 500 records at a time
            _totalRecordCount = 0;
            _totalPages = 0;
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

                // Build WHERE clause
                var filterBuilder = new FilterBuilder();

                // Add search filter if text exists
                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    filterBuilder.AddTextSearch(_searchText);
                }

                // Add any active column filters
                foreach (var filter in _activeFilters.Values)
                {
                    string dbColumnName = ColumnMapper.GetDbColumnName(filter.ColumnName);
                    string condition = BuildFilterCondition(dbColumnName, filter.FilterType, filter.FilterValue);
                    if (!string.IsNullOrEmpty(condition))
                    {
                        filterBuilder.AddCondition(condition);
                    }
                }

                // Store WHERE clause
                _currentWhereClause = filterBuilder.BuildWhereClause();

                // Reset to first page
                CurrentPage = 0;

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

        public int TotalRecordCount
        {
            get => _totalRecordCount;
            set
            {
                _totalRecordCount = value;
                OnPropertyChanged(nameof(TotalRecordCount));
                UpdateTotalPages();
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

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = value;
                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(CurrentPageDisplay));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                _pageSize = value;
                OnPropertyChanged(nameof(PageSize));
                UpdateTotalPages();
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            set
            {
                _totalPages = value;
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(CurrentPageDisplay));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }

        public string CurrentPageDisplay
        {
            get => $"Page {CurrentPage + 1} of {TotalPages}";
        }

        public bool CanGoPrevious => CurrentPage > 0 && !IsLoading;
        public bool CanGoNext => CurrentPage < TotalPages - 1 && !IsLoading;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
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

                if (active)
                {
                    // Build WHERE clause for My Records
                    var filterBuilder = new FilterBuilder();
                    filterBuilder.AddMyRecordsFilter(currentUsername);
                    _currentWhereClause = filterBuilder.BuildWhereClause();

                }
                else
                {
                    // Clear filter
                    _currentWhereClause = null;
                }

                // Reset to first page when filter changes
                CurrentPage = 0;

                // Reload with filter
                await LoadCurrentPageAsync();
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
        /// <summary>
        /// Load initial data - gets total count and first page
        /// </summary>
        public async Task LoadInitialDataAsync()
        {
            try
            {
                IsLoading = true;

                // Get total count
                TotalRecordCount = await ActivityRepository.GetTotalCountAsync();

                // Load first page
                await LoadCurrentPageAsync();
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

        /// <summary>
        /// Load the current page of data
        /// </summary>
        public async Task LoadCurrentPageAsync()
        {
            try
            {
                IsLoading = true;

                // Get page data with current filter
                var (pageData, totalCount) = await ActivityRepository.GetPageAsync(CurrentPage, PageSize, _currentWhereClause);

                // Update total records and recalculate pages
                TotalRecords = totalCount;
                TotalRecordCount = await ActivityRepository.GetTotalCountAsync(); // unfiltered DB total (Z)
                // TotalRecordCount = (int)totalCount;
                TotalPages = (int)Math.Ceiling((double)TotalRecords / PageSize);

                Activities.Clear();
                Activities.AddRange(pageData);
                FilteredCount = Activities.Count;

                // Update totals for all filtered records
                await UpdateTotalsAsync();

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

        /// <summary>
        /// Go to next page
        /// </summary>
        public async Task NextPageAsync()
        {
            if (!CanGoNext) return;

            CurrentPage++;
            await LoadCurrentPageAsync();
        }

        /// <summary>
        /// Go to previous page
        /// </summary>
        public async Task PreviousPageAsync()
        {
            if (!CanGoPrevious) return;

            CurrentPage--;
            await LoadCurrentPageAsync();
        }

        /// <summary>
        /// Go to first page
        /// </summary>
        public async Task FirstPageAsync()
        {
            if (CurrentPage == 0) return;

            CurrentPage = 0;
            await LoadCurrentPageAsync();
        }

        /// <summary>
        /// Go to last page
        /// </summary>
        public async Task LastPageAsync()
        {
            if (CurrentPage == TotalPages - 1) return;

            CurrentPage = TotalPages - 1;
            await LoadCurrentPageAsync();
        }

        /// <summary>
        /// Refresh current page
        /// </summary>
        public async Task RefreshAsync()
        {
            await LoadCurrentPageAsync();
        }

        /// <summary>
        /// Update total pages calculation
        /// </summary>
        private void UpdateTotalPages()
        {
            if (PageSize > 0)
            {
                TotalPages = (int)Math.Ceiling((double)TotalRecords / PageSize);
            }
            else
            {
                TotalPages = 0;
            }
        }


        /// <summary>
        /// Apply filters to the view
        /// </summary>
        //private void ApplyFilters()
        //{
        //    if (_activitiesView == null) return;

        //    _activitiesView.Filter = FilterActivity;
        //    FilteredCount = _activitiesView.Cast<Activity>().Count();
        //}

        /// <summary>
        /// Filter predicate for activities
        /// </summary>
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