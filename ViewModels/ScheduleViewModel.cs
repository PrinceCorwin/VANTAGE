using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using VANTAGE.Models;
using VANTAGE.Repositories;
using VANTAGE.Utilities;

namespace VANTAGE.ViewModels
{
    public class ScheduleViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<ScheduleMasterRow> _masterRows = new ObservableCollection<ScheduleMasterRow>();
        private ICollectionView? _masterRowsView;
        private DateTime? _selectedWeekEndDate;
        private bool _isLoading;
        private bool _filterActualStart;
        private bool _filterActualFinish;
        private bool _filter3WLA;

        // Master grid data
        public ObservableCollection<ScheduleMasterRow> MasterRows
        {
            get => _masterRows;
            set
            {
                _masterRows = value;
                OnPropertyChanged(nameof(MasterRows));

                // Create collection view for filtering
                _masterRowsView = CollectionViewSource.GetDefaultView(_masterRows);
                _masterRowsView.Filter = FilterMasterRow;
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

        // Filter toggles
        public bool FilterActualStart
        {
            get => _filterActualStart;
            set
            {
                if (_filterActualStart != value)
                {
                    _filterActualStart = value;
                    OnPropertyChanged(nameof(FilterActualStart));
                    RefreshFilter();
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
                    OnPropertyChanged(nameof(FilterActualFinish));
                    RefreshFilter();
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
                    OnPropertyChanged(nameof(Filter3WLA));
                    RefreshFilter();
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

                // Replace the entire collection to trigger the setter
                MasterRows = new ObservableCollection<ScheduleMasterRow>(masterRows);

                AppLogger.Info($"Loaded {MasterRows.Count} schedule activities for {weekEndDate:yyyy-MM-dd}",
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

        // Filter predicate for master rows
        private bool FilterMasterRow(object obj)
        {
            if (obj is not ScheduleMasterRow row)
                return false;

            // If no filters active, show all
            if (!FilterActualStart && !FilterActualFinish && !Filter3WLA)
                return true;

            bool passesFilter = false;

            // Actual Start filter - show rows where P6 and MS differ
            if (FilterActualStart)
            {
                if (row.HasStartVariance)
                    passesFilter = true;
            }

            // Actual Finish filter - show rows where P6 and MS differ
            if (FilterActualFinish)
            {
                if (row.HasFinishVariance)
                    passesFilter = true;
            }

            // 3WLA filter - show rows starting/finishing in next 3 weeks
            if (Filter3WLA)
            {
                var today = DateTime.Today;
                var threeWeeksOut = today.AddDays(21);

                if ((row.P6_PlannedStart.HasValue && row.P6_PlannedStart.Value >= today && row.P6_PlannedStart.Value <= threeWeeksOut) ||
                    (row.P6_PlannedFinish.HasValue && row.P6_PlannedFinish.Value >= today && row.P6_PlannedFinish.Value <= threeWeeksOut))
                {
                    passesFilter = true;
                }
            }

            return passesFilter;
        }

        // Refresh filter
        private void RefreshFilter()
        {
            _masterRowsView?.Refresh();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}