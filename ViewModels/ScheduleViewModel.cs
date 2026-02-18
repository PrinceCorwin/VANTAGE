using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VANTAGE.Models;
using VANTAGE.Repositories;
using VANTAGE.Utilities;
namespace VANTAGE.ViewModels
{
    // Filter types for P6 vs MS discrepancies
    public enum DiscrepancyFilterType { None, Start, Finish, MHs, PercentComplete }

    public class ScheduleViewModel : INotifyPropertyChanged, IScheduleCellIndicators
    {
        private ObservableCollection<ScheduleMasterRow> _masterRows = new ObservableCollection<ScheduleMasterRow>();
        private List<ScheduleMasterRow> _allMasterRows = new List<ScheduleMasterRow>();
        private ObservableCollection<ProgressSnapshot> _detailActivities = new ObservableCollection<ProgressSnapshot>();
        private DateTime? _selectedWeekEndDate;
        private bool _isLoading;
        private bool _filterMissedStart;
        private bool _filterMissedFinish;
        private bool _filter3WLA;
        private DiscrepancyFilterType _discrepancyFilter = DiscrepancyFilterType.None;
        private string? _selectedSchedActNO;
        private bool _hasUnsavedChanges;
        private CancellationTokenSource? _detailLoadCts;

        // IScheduleCellIndicators - always false on ViewModel (only meaningful on ScheduleMasterRow)
        public bool IsMissedStartReasonRequired => false;
        public bool IsMissedFinishReasonRequired => false;
        public bool IsThreeWeekStartRequired => false;
        public bool IsThreeWeekFinishRequired => false;
        public bool HasThreeWeekStartForecast => false;
        public bool HasThreeWeekFinishForecast => false;
        public bool HasStartVariance => false;
        public bool HasFinishVariance => false;
        public bool HasBudgetMHsVariance => false;
        public bool HasPercentCompleteVariance => false;

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
        public bool FilterMissedStart
        {
            get => _filterMissedStart;
            set
            {
                if (_filterMissedStart != value)
                {
                    _filterMissedStart = value;

                    if (value)
                    {
                        _filterMissedFinish = false;
                        _filter3WLA = false;
                        _filterRequiredFields = false;
                        _discrepancyFilter = DiscrepancyFilterType.None;
                        OnPropertyChanged(nameof(FilterMissedFinish));
                        OnPropertyChanged(nameof(Filter3WLA));
                        OnPropertyChanged(nameof(FilterRequiredFields));
                        OnPropertyChanged(nameof(DiscrepancyFilter));
                    }

                    OnPropertyChanged(nameof(FilterMissedStart));
                    ApplyFilter();
                }
            }
        }

        public bool FilterMissedFinish
        {
            get => _filterMissedFinish;
            set
            {
                if (_filterMissedFinish != value)
                {
                    _filterMissedFinish = value;

                    if (value)
                    {
                        _filterMissedStart = false;
                        _filter3WLA = false;
                        _filterRequiredFields = false;
                        _discrepancyFilter = DiscrepancyFilterType.None;
                        OnPropertyChanged(nameof(FilterMissedStart));
                        OnPropertyChanged(nameof(Filter3WLA));
                        OnPropertyChanged(nameof(FilterRequiredFields));
                        OnPropertyChanged(nameof(DiscrepancyFilter));
                    }

                    OnPropertyChanged(nameof(FilterMissedFinish));
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
                        _filterMissedStart = false;
                        _filterMissedFinish = false;
                        _filterRequiredFields = false;
                        _discrepancyFilter = DiscrepancyFilterType.None;
                        OnPropertyChanged(nameof(FilterMissedStart));
                        OnPropertyChanged(nameof(FilterMissedFinish));
                        OnPropertyChanged(nameof(FilterRequiredFields));
                        OnPropertyChanged(nameof(DiscrepancyFilter));
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
                        _filterMissedStart = false;
                        _filterMissedFinish = false;
                        _filter3WLA = false;
                        _discrepancyFilter = DiscrepancyFilterType.None;
                        OnPropertyChanged(nameof(FilterMissedStart));
                        OnPropertyChanged(nameof(FilterMissedFinish));
                        OnPropertyChanged(nameof(Filter3WLA));
                        OnPropertyChanged(nameof(DiscrepancyFilter));
                    }

                    OnPropertyChanged(nameof(FilterRequiredFields));
                    ApplyFilter();
                }
            }
        }
        public DiscrepancyFilterType DiscrepancyFilter
        {
            get => _discrepancyFilter;
            set
            {
                if (_discrepancyFilter != value)
                {
                    _discrepancyFilter = value;

                    // Mutual exclusivity - clear other filters when activating
                    if (value != DiscrepancyFilterType.None)
                    {
                        _filterMissedStart = false;
                        _filterMissedFinish = false;
                        _filter3WLA = false;
                        _filterRequiredFields = false;
                        OnPropertyChanged(nameof(FilterMissedStart));
                        OnPropertyChanged(nameof(FilterMissedFinish));
                        OnPropertyChanged(nameof(Filter3WLA));
                        OnPropertyChanged(nameof(FilterRequiredFields));
                    }

                    OnPropertyChanged(nameof(DiscrepancyFilter));
                    ApplyFilter();
                }
            }
        }

        // Clears all filter toggles and discrepancy filter
        public void ClearAllFilters()
        {
            _filterMissedStart = false;
            _filterMissedFinish = false;
            _filter3WLA = false;
            _filterRequiredFields = false;
            _discrepancyFilter = DiscrepancyFilterType.None;

            OnPropertyChanged(nameof(FilterMissedStart));
            OnPropertyChanged(nameof(FilterMissedFinish));
            OnPropertyChanged(nameof(Filter3WLA));
            OnPropertyChanged(nameof(FilterRequiredFields));
            OnPropertyChanged(nameof(DiscrepancyFilter));

            ApplyFilter();
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
            // Cancel any in-progress load
            _detailLoadCts?.Cancel();
            _detailLoadCts = new CancellationTokenSource();
            var token = _detailLoadCts.Token;

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

                // Check if cancelled before updating UI
                if (token.IsCancellationRequested)
                    return;

                DetailActivities = new ObservableCollection<ProgressSnapshot>(snapshots);

                AppLogger.Info($"Loaded {snapshots.Count} detail activities for {schedActNO}",
                    "ScheduleViewModel.LoadDetailActivitiesAsync");
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled - ignore
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleViewModel.LoadDetailActivitiesAsync");
                ClearDetailActivities();
            }
        }

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
                    // V_Start = MIN(ActStart) where ActStart is not null
                    var starts = DetailActivities
                        .Where(d => d.ActStart.HasValue)
                        .Select(d => d.ActStart!.Value)
                        .ToList();
                    masterRow.V_Start = starts.Any() ? starts.Min() : (DateTime?)null;

                    // V_Finish = MAX(ActFin) only if ALL activities have ActFin
                    var allHaveFinish = DetailActivities.All(d => d.ActFin.HasValue);
                    if (allHaveFinish && DetailActivities.Count > 0)
                    {
                        masterRow.V_Finish = DetailActivities.Max(d => d.ActFin!.Value);
                    }
                    else
                    {
                        masterRow.V_Finish = null;
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

                // Clear "Started Early" if no longer valid
                if (masterRow.MissedStartReason == "Started Early")
                {
                    bool stillStartedEarly = masterRow.V_Start != null &&
                                             masterRow.P6_Start != null &&
                                             masterRow.V_Start.Value.Date < masterRow.P6_Start.Value.Date;
                    if (!stillStartedEarly)
                    {
                        masterRow.MissedStartReason = null;
                    }
                }

                // Clear "Finished Early" if no longer valid
                if (masterRow.MissedFinishReason == "Finished Early")
                {
                    bool stillFinishedEarly = masterRow.V_Finish != null &&
                                              masterRow.P6_Finish != null &&
                                              masterRow.V_Finish.Value.Date < masterRow.P6_Finish.Value.Date;
                    if (!stillFinishedEarly)
                    {
                        masterRow.MissedFinishReason = null;
                    }
                }

                // Update the displayed row if it exists (might be filtered out)
                if (displayedRow != null && displayedRow != masterRow)
                {
                    displayedRow.V_Start = masterRow.V_Start;
                    displayedRow.V_Finish = masterRow.V_Finish;
                    displayedRow.MS_PercentComplete = masterRow.MS_PercentComplete;
                    displayedRow.MS_BudgetMHs = masterRow.MS_BudgetMHs;
                    displayedRow.MissedStartReason = masterRow.MissedStartReason;
                    displayedRow.MissedFinishReason = masterRow.MissedFinishReason;
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
                    // Setting SelectedWeekEndDate triggers LoadScheduleDataAsync which manages IsLoading
                    SelectedWeekEndDate = AvailableWeekEndDates[0];
                }
                else
                {
                    // No data to load - clear loading state
                    IsLoading = false;
                }

                AppLogger.Info($"Loaded {AvailableWeekEndDates.Count} available week ending dates",
                    "ScheduleViewModel.InitializeAsync");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ScheduleViewModel.InitializeAsync");
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

            if (!FilterMissedStart && !FilterMissedFinish && !Filter3WLA && !FilterRequiredFields && DiscrepancyFilter == DiscrepancyFilterType.None)
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

            if (FilterMissedStart)
            {
                return row.IsMissedStartReasonRequired;
            }

            if (FilterMissedFinish)
            {
                return row.IsMissedFinishReasonRequired;
            }

            if (Filter3WLA)
            {
                return row.IsThreeWeekStartRequired || row.IsThreeWeekFinishRequired;
            }

            if (DiscrepancyFilter != DiscrepancyFilterType.None)
            {
                return DiscrepancyFilter switch
                {
                    DiscrepancyFilterType.Start => row.HasStartVariance,
                    DiscrepancyFilterType.Finish => row.HasFinishVariance,
                    DiscrepancyFilterType.MHs => row.HasBudgetMHsVariance,
                    DiscrepancyFilterType.PercentComplete => row.HasPercentCompleteVariance,
                    _ => true
                };
            }

            return true;
        }
        // Returns the unfiltered list of all master rows (for export)
        public List<ScheduleMasterRow> GetAllMasterRows()
        {
            return _allMasterRows ?? new List<ScheduleMasterRow>();
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}