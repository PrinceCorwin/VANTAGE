using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using VANTAGE.Models;
using VANTAGE.Repositories;
using VANTAGE.Utilities;

namespace VANTAGE.ViewModels
{
    public class ScheduleViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<ScheduleMasterRow> _masterRows = new ObservableCollection<ScheduleMasterRow>();
        private List<ScheduleMasterRow> _allMasterRows = new List<ScheduleMasterRow>(); // Store unfiltered data
        private DateTime? _selectedWeekEndDate;
        private bool _isLoading;
        private bool _filterActualStart;
        private bool _filterActualFinish;
        private bool _filter3WLA;

        // Master grid data (filtered)
        public ObservableCollection<ScheduleMasterRow> MasterRows
        {
            get => _masterRows;
            set
            {
                _masterRows = value;
                OnPropertyChanged(nameof(MasterRows));
            }
        }

        // Available week ending dates
        public ObservableCollection<DateTime> AvailableWeekEndDates { get; set; } = new ObservableCollection<DateTime>();

        // Selected week ending date
        public DateTime? SelectedWeekEndDate
        {
            get => _selectedWeekEndDate;
            set
            {
                if (_selectedWeekEndDate != value)
                {
                    _selectedWeekEndDate = value;
                    OnPropertyChanged(nameof(SelectedWeekEndDate));

                    // Load data for new week
                    if (value.HasValue)
                    {
                        _ = LoadScheduleDataAsync(value.Value);
                    }
                }
            }
        }

        // Filter toggles - MUTUALLY EXCLUSIVE
        public bool FilterActualStart
        {
            get => _filterActualStart;
            set
            {
                if (_filterActualStart != value)
                {
                    _filterActualStart = value;

                    // Turn off other filters if this one is being activated
                    if (value)
                    {
                        _filterActualFinish = false;
                        _filter3WLA = false;
                        OnPropertyChanged(nameof(FilterActualFinish));
                        OnPropertyChanged(nameof(Filter3WLA));
                    }

                    OnPropertyChanged(nameof(FilterActualStart));
                    ApplyFilter();
                }
            }
        }

        public bool FilterActualFinish
        {
            get => _filterActualFinish;
            set
            {
                if (_filterActualFinish != value)
                {
                    _filterActualFinish = value;

                    // Turn off other filters if this one is being activated
                    if (value)
                    {
                        _filterActualStart = false;
                        _filter3WLA = false;
                        OnPropertyChanged(nameof(FilterActualStart));
                        OnPropertyChanged(nameof(Filter3WLA));
                    }

                    OnPropertyChanged(nameof(FilterActualFinish));
                    ApplyFilter();
                }
            }
        }

        public bool Filter3WLA
        {
            get => _filter3WLA;
            set
            {
                if (_filter3WLA != value)
                {
                    _filter3WLA = value;

                    // Turn off other filters if this one is being activated
                    if (value)
                    {
                        _filterActualStart = false;
                        _filterActualFinish = false;
                        OnPropertyChanged(nameof(FilterActualStart));
                        OnPropertyChanged(nameof(FilterActualFinish));
                    }

                    OnPropertyChanged(nameof(Filter3WLA));
                    ApplyFilter();
                }
            }
        }

        // Loading indicator
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        // Initialize - load available dates
        public async Task InitializeAsync()
        {
            try
            {
                IsLoading = true;

                var dates = await ScheduleRepository.GetAvailableWeekEndDatesAsync();

                AvailableWeekEndDates.Clear();
                foreach (var date in dates)
                {
                    AvailableWeekEndDates.Add(date);
                }

                // Auto-select most recent date
                if (AvailableWeekEndDates.Count > 0)
                {
                    SelectedWeekEndDate = AvailableWeekEndDates[0];
                }

                AppLogger.Info($"Loaded {AvailableWeekEndDates.Count} available week ending dates",
                    "ScheduleViewModel.InitializeAsync");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleViewModel.InitializeAsync");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Load schedule data for selected week
        public async Task LoadScheduleDataAsync(DateTime weekEndDate)
        {
            try
            {
                IsLoading = true;

                var masterRows = await ScheduleRepository.GetScheduleMasterRowsAsync(weekEndDate);

                // Store the full unfiltered dataset
                _allMasterRows = masterRows;

                // Apply current filter (or show all if no filter active)
                ApplyFilter();

                AppLogger.Info($"Loaded {_allMasterRows.Count} schedule activities for {weekEndDate:yyyy-MM-dd}",
                    "ScheduleViewModel.LoadScheduleDataAsync");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleViewModel.LoadScheduleDataAsync");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Apply filter by rebuilding the ObservableCollection
        private void ApplyFilter()
        {
            System.Diagnostics.Debug.WriteLine($"=== ApplyFilter: ActualStart={FilterActualStart}, ActualFinish={FilterActualFinish}, 3WLA={Filter3WLA} ===");

            List<ScheduleMasterRow> filteredRows;

            // If no filters active, show all
            if (!FilterActualStart && !FilterActualFinish && !Filter3WLA)
            {
                filteredRows = _allMasterRows;
                System.Diagnostics.Debug.WriteLine($"No filters active - showing all {filteredRows.Count} rows");
            }
            else
            {
                // Apply the active filter
                filteredRows = _allMasterRows.Where(row => FilterMasterRow(row)).ToList();
                System.Diagnostics.Debug.WriteLine($"Filter applied - showing {filteredRows.Count} of {_allMasterRows.Count} rows");
            }

            // Rebuild the ObservableCollection
            MasterRows = new ObservableCollection<ScheduleMasterRow>(filteredRows);
        }

        // Filter predicate for master rows - returns true if row should be shown
        private bool FilterMasterRow(ScheduleMasterRow row)
        {
            // Actual Start filter - show rows where P6 and MS start dates differ
            if (FilterActualStart)
            {
                bool hasVariance = row.HasStartVariance;
                System.Diagnostics.Debug.WriteLine($"[{row.SchedActNO}] Filter result: {hasVariance}");
                return hasVariance;
            }

            // Actual Finish filter - show rows where P6 and MS finish dates differ
            if (FilterActualFinish)
            {
                return row.HasFinishVariance;
            }

            // 3WLA filter - show rows starting/finishing in next 3 weeks
            if (Filter3WLA)
            {
                var today = DateTime.Today;
                var threeWeeksOut = today.AddDays(21);

                return (row.P6_PlannedStart.HasValue && row.P6_PlannedStart.Value >= today && row.P6_PlannedStart.Value <= threeWeeksOut) ||
                       (row.P6_PlannedFinish.HasValue && row.P6_PlannedFinish.Value >= today && row.P6_PlannedFinish.Value <= threeWeeksOut);
            }

            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}