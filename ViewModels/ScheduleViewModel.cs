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
        private List<ScheduleMasterRow> _allMasterRows = new List<ScheduleMasterRow>();
        private ObservableCollection<ProgressSnapshot> _detailActivities = new ObservableCollection<ProgressSnapshot>();
        private DateTime? _selectedWeekEndDate;
        private bool _isLoading;
        private bool _filterActualStart;
        private bool _filterActualFinish;
        private bool _filter3WLA;
        private string? _selectedSchedActNO;
        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                if (_hasUnsavedChanges != value)
                {
                    _hasUnsavedChanges = value;
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                }
            }
        }

        public string RequiredFieldsButtonText => $"{RequiredFieldsCount} Required Fields";

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

        // Detail grid data (child ProgressSnapshots for selected master row)
        public ObservableCollection<ProgressSnapshot> DetailActivities
        {
            get => _detailActivities;
            set
            {
                _detailActivities = value;
                OnPropertyChanged(nameof(DetailActivities));
            }
        }

        // Track which SchedActNO is currently selected
        public string? SelectedSchedActNO
        {
            get => _selectedSchedActNO;
            set
            {
                _selectedSchedActNO = value;
                OnPropertyChanged(nameof(SelectedSchedActNO));
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

                    // Clear detail when week changes
                    ClearDetailActivities();

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

                    if (value)
                    {
                        _filterActualFinish = false;
                        _filter3WLA = false;
                        _filterRequiredFields = false;
                        OnPropertyChanged(nameof(FilterActualFinish));
                        OnPropertyChanged(nameof(Filter3WLA));
                        OnPropertyChanged(nameof(FilterRequiredFields));
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

                    if (value)
                    {
                        _filterActualStart = false;
                        _filter3WLA = false;
                        _filterRequiredFields = false;
                        OnPropertyChanged(nameof(FilterActualStart));
                        OnPropertyChanged(nameof(Filter3WLA));
                        OnPropertyChanged(nameof(FilterRequiredFields));
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

                    if (value)
                    {
                        _filterActualStart = false;
                        _filterActualFinish = false;
                        _filterRequiredFields = false;
                        OnPropertyChanged(nameof(FilterActualStart));
                        OnPropertyChanged(nameof(FilterActualFinish));
                        OnPropertyChanged(nameof(FilterRequiredFields));
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

        private int _requiredFieldsCount;
        public int RequiredFieldsCount
        {
            get => _requiredFieldsCount;
            set
            {
                if (_requiredFieldsCount != value)
                {
                    _requiredFieldsCount = value;
                    OnPropertyChanged(nameof(RequiredFieldsCount));
                    OnPropertyChanged(nameof(RequiredFieldsButtonText));
                }
            }
        }

        private bool _filterRequiredFields;
        public bool FilterRequiredFields
        {
            get => _filterRequiredFields;
            set
            {
                if (_filterRequiredFields != value)
                {
                    _filterRequiredFields = value;

                    if (value)
                    {
                        _filterActualStart = false;
                        _filterActualFinish = false;
                        _filter3WLA = false;
                        OnPropertyChanged(nameof(FilterActualStart));
                        OnPropertyChanged(nameof(FilterActualFinish));
                        OnPropertyChanged(nameof(Filter3WLA));
                    }

                    OnPropertyChanged(nameof(FilterRequiredFields));
                    ApplyFilter();
                }
            }
        }

        // ========================================
        // DETAIL ACTIVITIES METHODS
        // ========================================

        public void ClearDetailActivities()
        {
            DetailActivities.Clear();
            SelectedSchedActNO = null;
        }

        public async Task LoadDetailActivitiesAsync(string schedActNO)
        {
            try
            {
                if (string.IsNullOrEmpty(schedActNO) || !SelectedWeekEndDate.HasValue)
                {
                    ClearDetailActivities();
                    return;
                }

                SelectedSchedActNO = schedActNO;

                var snapshots = await ScheduleRepository.GetSnapshotsBySchedActNOAsync(
                    schedActNO,
                    SelectedWeekEndDate.Value);

                DetailActivities = new ObservableCollection<ProgressSnapshot>(snapshots);

                AppLogger.Info($"Loaded {snapshots.Count} detail activities for {schedActNO}",
                    "ScheduleViewModel.LoadDetailActivitiesAsync");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleViewModel.LoadDetailActivitiesAsync");
                ClearDetailActivities();
            }
        }

        // Called after detail grid edit to recalculate MS rollups for the selected SchedActNO
        public async Task RecalculateMSRollupsAsync(string schedActNO)
        {
            try
            {
                if (string.IsNullOrEmpty(schedActNO) || !SelectedWeekEndDate.HasValue)
                    return;

                // Find the master row in both filtered and unfiltered collections
                var masterRow = _allMasterRows.FirstOrDefault(r => r.SchedActNO == schedActNO);
                var displayedRow = MasterRows.FirstOrDefault(r => r.SchedActNO == schedActNO);

                if (masterRow == null)
                    return;

                // Recalculate from current DetailActivities (already updated in memory)
                if (DetailActivities.Count > 0)
                {
                    // MS_ActualStart = MIN(SchStart) where SchStart is not null
                    var starts = DetailActivities
                        .Where(d => d.SchStart.HasValue)
                        .Select(d => d.SchStart!.Value)
                        .ToList();

                    masterRow.MS_ActualStart = starts.Any() ? starts.Min() : (DateTime?)null;

                    // MS_ActualFinish = MAX(SchFinish) only if ALL activities have SchFinish
                    var allHaveFinish = DetailActivities.All(d => d.SchFinish.HasValue);
                    if (allHaveFinish && DetailActivities.Count > 0)
                    {
                        masterRow.MS_ActualFinish = DetailActivities.Max(d => d.SchFinish!.Value);
                    }
                    else
                    {
                        masterRow.MS_ActualFinish = null;
                    }

                    // MS_PercentComplete = weighted average (BudgetMHs * PercentEntry) / SUM(BudgetMHs)
                    double totalBudget = DetailActivities.Sum(d => d.BudgetMHs);
                    if (totalBudget > 0)
                    {
                        double weightedSum = DetailActivities.Sum(d => d.BudgetMHs * d.PercentEntry);
                        masterRow.MS_PercentComplete = weightedSum / totalBudget;
                    }
                    else
                    {
                        masterRow.MS_PercentComplete = 0;
                    }

                    // MS_BudgetMHs = SUM(BudgetMHs)
                    masterRow.MS_BudgetMHs = totalBudget;
                }

                // Update the displayed row if it exists (might be filtered out)
                if (displayedRow != null && displayedRow != masterRow)
                {
                    displayedRow.MS_ActualStart = masterRow.MS_ActualStart;
                    displayedRow.MS_ActualFinish = masterRow.MS_ActualFinish;
                    displayedRow.MS_PercentComplete = masterRow.MS_PercentComplete;
                    displayedRow.MS_BudgetMHs = masterRow.MS_BudgetMHs;
                }

                // Update required fields count since variance properties may have changed
                UpdateRequiredFieldsCount();

                AppLogger.Info($"Recalculated MS rollups for {schedActNO}", "ScheduleViewModel.RecalculateMSRollupsAsync");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleViewModel.RecalculateMSRollupsAsync");
            }
        }

        // ========================================
        // INITIALIZATION AND DATA LOADING
        // ========================================

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

        public void UpdateRequiredFieldsCount()
        {
            if (_allMasterRows == null || _allMasterRows.Count == 0)
            {
                RequiredFieldsCount = 0;
                return;
            }

            int count = 0;
            foreach (var row in _allMasterRows)
            {
                if (row.IsMissedStartReasonRequired) count++;
                if (row.IsMissedFinishReasonRequired) count++;
                if (row.IsThreeWeekStartRequired) count++;
                if (row.IsThreeWeekFinishRequired) count++;
            }

            RequiredFieldsCount = count;
        }

        public async Task LoadScheduleDataAsync(DateTime weekEndDate)
        {
            try
            {
                IsLoading = true;
                var masterRows = await ScheduleRepository.GetScheduleMasterRowsAsync(weekEndDate);
                _allMasterRows = masterRows;
                ApplyFilter();
                UpdateRequiredFieldsCount();

                // Reset unsaved changes flag when fresh data loads
                HasUnsavedChanges = false;

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

        // ========================================
        // FILTERING
        // ========================================

        private void ApplyFilter()
        {
            List<ScheduleMasterRow> filteredRows;

            if (!FilterActualStart && !FilterActualFinish && !Filter3WLA && !FilterRequiredFields)
            {
                filteredRows = _allMasterRows;
            }
            else
            {
                filteredRows = _allMasterRows.Where(row => FilterMasterRow(row)).ToList();
            }

            MasterRows = new ObservableCollection<ScheduleMasterRow>(filteredRows);
        }

        private bool FilterMasterRow(ScheduleMasterRow row)
        {
            if (FilterRequiredFields)
            {
                return row.IsMissedStartReasonRequired ||
                       row.IsMissedFinishReasonRequired ||
                       row.IsThreeWeekStartRequired ||
                       row.IsThreeWeekFinishRequired;
            }

            if (FilterActualStart)
            {
                return row.HasStartVariance;
            }

            if (FilterActualFinish)
            {
                return row.HasFinishVariance;
            }

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