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
        /// <summary>
        /// Apply a filter to a column
        /// </summary>
        public void ApplyFilter(string columnName, string filterType, string filterValue)
        {
            _activeFilters[columnName] = new ColumnFilter
            {
                ColumnName = columnName,
                FilterType = filterType,
                FilterValue = filterValue
            };

            ApplyAllFilters();
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
        public void ClearFilter(string columnName)
        {
            _activeFilters.Remove(columnName);
            ApplyAllFilters();
        }

        /// <summary>
        /// Apply all active filters to the data
        /// </summary>
        private async void ApplyAllFilters()
        {
            try
            {
                IsLoading = true;

                // Reload current page from database
                var pageData = await ActivityRepository.GetPageAsync(CurrentPage, PageSize);

                // Apply each active filter
                var filteredData = pageData.AsEnumerable();

                foreach (var filter in _activeFilters.Values)
                {
                    filteredData = ApplyColumnFilter(filteredData, filter);
                }

                // Update the collection
                Activities.Clear();
                Activities.AddRange(filteredData.ToList());

                FilteredCount = Activities.Count;

                System.Diagnostics.Debug.WriteLine($"✓ Filters applied: {_activeFilters.Count} active, {FilteredCount} records shown");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error applying filters: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
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
                ApplyFilters();
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
            private set
            {
                _totalPages = value;
                OnPropertyChanged(nameof(TotalPages));
            }
        }

        public string CurrentPageDisplay => $"Page {CurrentPage + 1} of {TotalPages}";

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

        /// <summary>
        /// Load initial data - gets total count and first page
        /// </summary>
        public async Task LoadInitialDataAsync()
        {
            try
            {
                IsLoading = true;
                System.Diagnostics.Debug.WriteLine("→ Loading initial data...");

                // Get total count
                TotalRecordCount = await ActivityRepository.GetTotalCountAsync();
                System.Diagnostics.Debug.WriteLine($"✓ Total records in database: {TotalRecordCount}");

                // Load first page
                await LoadCurrentPageAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading initial data: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"→ Loading page {CurrentPage + 1}...");

                var pageData = await ActivityRepository.GetPageAsync(CurrentPage, PageSize);

                Activities.Clear();
                Activities.AddRange(pageData);

                FilteredCount = Activities.Count;

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {pageData.Count} activities for page {CurrentPage + 1}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading page: {ex.Message}");
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
                TotalPages = (int)Math.Ceiling((double)TotalRecordCount / PageSize);
            }
            else
            {
                TotalPages = 0;
            }
        }

        /// <summary>
        /// Apply filters to the view
        /// </summary>
        private void ApplyFilters()
        {
            if (_activitiesView == null) return;

            _activitiesView.Filter = FilterActivity;
            FilteredCount = _activitiesView.Cast<Activity>().Count();
        }

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