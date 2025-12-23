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

        public DateTime? MS_ActualStart { get; set; }
        public DateTime? MS_ActualFinish { get; set; }
        public double MS_PercentComplete { get; set; }
        public double MS_BudgetMHs { get; set; }

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

                return P6_ActualStart.Value.Date != MS_ActualStart.Value.Date;
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

                return P6_ActualFinish.Value.Date != MS_ActualFinish.Value.Date;
            }
        }
        // ========================================
        // REQUIRED FIELD INDICATORS (for conditional formatting)
        // ========================================

        public bool IsMissedStartReasonRequired
        {
            get
            {
                return HasStartVariance && string.IsNullOrWhiteSpace(MissedStartReason);
            }
        }

        public bool IsMissedFinishReasonRequired
        {
            get
            {
                return HasFinishVariance && string.IsNullOrWhiteSpace(MissedFinishReason);
            }
        }

        public bool IsThreeWeekStartRequired
        {
            get
            {
                if (ThreeWeekStart.HasValue)
                    return false;

                if (!P6_PlannedStart.HasValue)
                    return false;

                // Required if P6_PlannedStart is within 21 days after WeekEndDate
                var daysUntilStart = (P6_PlannedStart.Value - WeekEndDate).TotalDays;
                return daysUntilStart >= 0 && daysUntilStart <= 21;
            }
        }

        public bool IsThreeWeekFinishRequired
        {
            get
            {
                if (ThreeWeekFinish.HasValue)
                    return false;

                if (!P6_PlannedFinish.HasValue)
                    return false;

                // Required if P6_PlannedFinish is within 21 days after WeekEndDate
                var daysUntilFinish = (P6_PlannedFinish.Value - WeekEndDate).TotalDays;
                return daysUntilFinish >= 0 && daysUntilFinish <= 21;
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