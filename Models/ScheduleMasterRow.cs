using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VANTAGE.Models
{
    // ViewModel row combining P6 data + MS rollups for Schedule Module master grid
    public class ScheduleMasterRow : INotifyPropertyChanged
    {
        // ========================================
        // P6 DATA (from Schedule table)
        // ========================================

        public string SchedActNO { get; set; } = string.Empty;
        public string WbsId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? P6_PlannedStart { get; set; }
        public DateTime? P6_PlannedFinish { get; set; }
        public DateTime? P6_ActualStart { get; set; }
        public DateTime? P6_ActualFinish { get; set; }
        public double P6_PercentComplete { get; set; }
        public double P6_BudgetMHs { get; set; }

        // ========================================
        // MS ROLLUPS (calculated from ProgressSnapshots)
        // ========================================

        private DateTime? _msActualStart;
        public DateTime? MS_ActualStart
        {
            get => _msActualStart;
            set
            {
                if (_msActualStart != value)
                {
                    _msActualStart = value;
                    OnPropertyChanged(nameof(MS_ActualStart));
                    OnPropertyChanged(nameof(HasStartVariance));
                    OnPropertyChanged(nameof(IsMissedStartReasonRequired));
                    OnPropertyChanged(nameof(IsThreeWeekStartEditable));
                    OnPropertyChanged(nameof(IsThreeWeekStartRequired));  // Add this line
                }
            }
        }

        private DateTime? _msActualFinish;
        public DateTime? MS_ActualFinish
        {
            get => _msActualFinish;
            set
            {
                if (_msActualFinish != value)
                {
                    _msActualFinish = value;
                    OnPropertyChanged(nameof(MS_ActualFinish));
                    OnPropertyChanged(nameof(HasFinishVariance));
                    OnPropertyChanged(nameof(IsMissedFinishReasonRequired));
                    OnPropertyChanged(nameof(IsThreeWeekFinishEditable));
                    OnPropertyChanged(nameof(IsThreeWeekFinishRequired));  // Add this line
                }
            }
        }

        private double _msPercentComplete;
        public double MS_PercentComplete
        {
            get => _msPercentComplete;
            set
            {
                if (Math.Abs(_msPercentComplete - value) > 0.0001)
                {
                    _msPercentComplete = value;
                    OnPropertyChanged(nameof(MS_PercentComplete));
                }
            }
        }

        private double _msBudgetMHs;
        public double MS_BudgetMHs
        {
            get => _msBudgetMHs;
            set
            {
                if (Math.Abs(_msBudgetMHs - value) > 0.0001)
                {
                    _msBudgetMHs = value;
                    OnPropertyChanged(nameof(MS_BudgetMHs));
                    OnPropertyChanged(nameof(HasBudgetMHsVariance));
                }
            }
        }

        // ========================================
        // EDITABLE FIELDS (user edits in Schedule Module)
        // ========================================

        private string? _missedStartReason;
        public string? MissedStartReason
        {
            get => _missedStartReason;
            set
            {
                if (_missedStartReason != value)
                {
                    _missedStartReason = value;
                    OnPropertyChanged(nameof(MissedStartReason));
                    OnPropertyChanged(nameof(IsMissedStartReasonRequired));
                }
            }
        }

        private string? _missedFinishReason;
        public string? MissedFinishReason
        {
            get => _missedFinishReason;
            set
            {
                if (_missedFinishReason != value)
                {
                    _missedFinishReason = value;
                    OnPropertyChanged(nameof(MissedFinishReason));
                    OnPropertyChanged(nameof(IsMissedFinishReasonRequired));
                }
            }
        }

        private DateTime? _threeWeekStart;
        public DateTime? ThreeWeekStart
        {
            get => _threeWeekStart;
            set
            {
                if (_threeWeekStart != value)
                {
                    _threeWeekStart = value;
                    OnPropertyChanged(nameof(ThreeWeekStart));
                    OnPropertyChanged(nameof(IsThreeWeekStartRequired));
                }
            }
        }

        private DateTime? _threeWeekFinish;
        public DateTime? ThreeWeekFinish
        {
            get => _threeWeekFinish;
            set
            {
                if (_threeWeekFinish != value)
                {
                    _threeWeekFinish = value;
                    OnPropertyChanged(nameof(ThreeWeekFinish));
                    OnPropertyChanged(nameof(IsThreeWeekFinishRequired));
                }
            }
        }

        // ========================================
        // METADATA
        // ========================================

        public DateTime WeekEndDate { get; set; }

        // ========================================
        // COMPUTED PROPERTIES FOR FILTERING
        // ========================================

        public bool HasStartVariance
        {
            get
            {
                if (!P6_ActualStart.HasValue && !MS_ActualStart.HasValue)
                    return false;

                if (P6_ActualStart.HasValue != MS_ActualStart.HasValue)
                    return true;

                return P6_ActualStart!.Value.Date != MS_ActualStart!.Value.Date;
            }
        }

        public bool HasFinishVariance
        {
            get
            {
                if (!P6_ActualFinish.HasValue && !MS_ActualFinish.HasValue)
                    return false;

                if (P6_ActualFinish.HasValue != MS_ActualFinish.HasValue)
                    return true;

                return P6_ActualFinish!.Value.Date != MS_ActualFinish!.Value.Date;
            }
        }

        public bool HasBudgetMHsVariance
        {
            get
            {
                // No variance if both are zero or very small
                if (P6_BudgetMHs < 0.001 && MS_BudgetMHs < 0.001)
                    return false;

                // Variance if one is zero and the other isn't
                if (P6_BudgetMHs < 0.001 || MS_BudgetMHs < 0.001)
                    return true;

                // Check if ratio is less than 99% in either direction
                double ratio = MS_BudgetMHs / P6_BudgetMHs;
                return ratio < 0.99 || ratio > 1.01;
            }
        }

        // ========================================
        // EDITABLE STATE PROPERTIES
        // ========================================

        // ThreeWeekStart is only editable when there's no actual start (forecasting)
        // When MS_ActualStart exists, the field shows the actual and is read-only
        public bool IsThreeWeekStartEditable => MS_ActualStart == null;

        // ThreeWeekFinish is only editable when there's no actual finish (forecasting)
        // When MS_ActualFinish exists, the field shows the actual and is read-only
        public bool IsThreeWeekFinishEditable => MS_ActualFinish == null;

        // ========================================
        // REQUIRED FIELD INDICATORS (for conditional formatting)
        // ========================================

        public bool IsMissedStartReasonRequired
        {
            get
            {
                // Missed Start = P6 planned start has passed but MS hasn't started (or started late)
                if (string.IsNullOrWhiteSpace(MissedStartReason) == false)
                    return false; // Already has reason

                if (!P6_PlannedStart.HasValue)
                    return false; // No planned start to miss

                if (P6_PlannedStart.Value.Date > WeekEndDate.Date)
                    return false; // Planned start is still in the future

                // Planned start has passed - check if MS started on time
                if (!MS_ActualStart.HasValue)
                    return true; // Never started - needs explanation

                return MS_ActualStart.Value.Date > P6_PlannedStart.Value.Date; // Started late - needs explanation
            }
        }

        public bool IsMissedFinishReasonRequired
        {
            get
            {
                // Missed Finish = P6 planned finish has passed but MS hasn't finished (or finished late)
                if (string.IsNullOrWhiteSpace(MissedFinishReason) == false)
                    return false; // Already has reason

                if (!P6_PlannedFinish.HasValue)
                    return false; // No planned finish to miss

                if (P6_PlannedFinish.Value.Date > WeekEndDate.Date)
                    return false; // Planned finish is still in the future

                // Planned finish has passed - check if MS finished on time
                if (!MS_ActualFinish.HasValue)
                    return true; // Never finished - needs explanation

                return MS_ActualFinish.Value.Date > P6_PlannedFinish.Value.Date; // Finished late - needs explanation
            }
        }

        public bool IsThreeWeekStartRequired
        {
            get
            {
                if (ThreeWeekStart.HasValue)
                    return false; // Already has 3WLA date

                if (MS_ActualStart.HasValue)
                    return false; // Already started - no forecast needed

                if (!P6_PlannedStart.HasValue)
                    return false; // No planned start

                var daysUntilStart = (P6_PlannedStart.Value.Date - WeekEndDate.Date).TotalDays;

                // Future: within 21 days
                if (daysUntilStart >= 0 && daysUntilStart <= 21)
                    return true;

                // Past-due: planned start has passed AND MS hasn't started yet
                if (daysUntilStart < 0 && !MS_ActualStart.HasValue)
                    return true;

                return false;
            }
        }

        public bool IsThreeWeekFinishRequired
        {
            get
            {
                if (ThreeWeekFinish.HasValue)
                    return false; // Already has 3WLA date

                if (MS_ActualFinish.HasValue)
                    return false; // Already finished - no forecast needed

                if (!P6_PlannedFinish.HasValue)
                    return false; // No planned finish

                var daysUntilFinish = (P6_PlannedFinish.Value.Date - WeekEndDate.Date).TotalDays;

                // Future: within 21 days
                if (daysUntilFinish >= 0 && daysUntilFinish <= 21)
                    return true;

                // Past-due: planned finish has passed AND MS hasn't finished yet
                if (daysUntilFinish < 0 && !MS_ActualFinish.HasValue)
                    return true;

                return false;
            }
        }

        // ========================================
        // CHILD COLLECTION (for detail grid expansion)
        // ========================================

        public ObservableCollection<ProgressSnapshot>? DetailActivities { get; set; }

        // ========================================
        // INotifyPropertyChanged
        // ========================================

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}