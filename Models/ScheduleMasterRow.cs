using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VANTAGE.Models
{
    // ViewModel row combining P6 data + MS rollups for Schedule Module master grid
    public class ScheduleMasterRow : INotifyPropertyChanged, IScheduleCellIndicators
    {
        // ========================================
        // P6 DATA (from Schedule table)
        // ========================================

        public string SchedActNO { get; set; } = string.Empty;
        public string WbsId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? P6_Start { get; set; }
        public DateTime? P6_Finish { get; set; }
        public DateTime? P6_ActualStart { get; set; }
        public DateTime? P6_ActualFinish { get; set; }
        public double P6_PercentComplete { get; set; }
        public double P6_BudgetMHs { get; set; }

        // ========================================
        // MS ROLLUPS (calculated from ProgressSnapshots)
        // ========================================

        private DateTime? _vStart;
        public DateTime? V_Start
        {
            get => _vStart;
            set
            {
                if (_vStart != value)
                {
                    _vStart = value;
                    OnPropertyChanged(nameof(V_Start));
                    OnPropertyChanged(nameof(HasStartVariance));
                    OnPropertyChanged(nameof(IsMissedStartReasonRequired));
                    OnPropertyChanged(nameof(IsThreeWeekStartEditable));
                    OnPropertyChanged(nameof(IsThreeWeekStartRequired));
                }
            }
        }

        private DateTime? _vFinish;
        public DateTime? V_Finish
        {
            get => _vFinish;
            set
            {
                if (_vFinish != value)
                {
                    _vFinish = value;
                    OnPropertyChanged(nameof(V_Finish));
                    OnPropertyChanged(nameof(HasFinishVariance));
                    OnPropertyChanged(nameof(IsMissedFinishReasonRequired));
                    OnPropertyChanged(nameof(IsThreeWeekFinishEditable));
                    OnPropertyChanged(nameof(IsThreeWeekFinishRequired));
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
                    OnPropertyChanged(nameof(HasPercentCompleteVariance));
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
        // USER-DEFINED FIELDS (Mapped from P6 UDFs, read-only)
        // ========================================

        public string SchedUDF1 { get; set; } = string.Empty;
        public string SchedUDF2 { get; set; } = string.Empty;
        public string SchedUDF3 { get; set; } = string.Empty;
        public string SchedUDF4 { get; set; } = string.Empty;
        public string SchedUDF5 { get; set; } = string.Empty;

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
                    OnPropertyChanged(nameof(HasThreeWeekStartForecast));
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
                    OnPropertyChanged(nameof(HasThreeWeekFinishForecast));
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
                if (!P6_ActualStart.HasValue && !V_Start.HasValue)
                    return false;

                if (P6_ActualStart.HasValue != V_Start.HasValue)
                    return true;

                return P6_ActualStart!.Value.Date != V_Start!.Value.Date;
            }
        }

        public bool HasFinishVariance
        {
            get
            {
                if (!P6_ActualFinish.HasValue && !V_Finish.HasValue)
                    return false;

                if (P6_ActualFinish.HasValue != V_Finish.HasValue)
                    return true;

                return P6_ActualFinish!.Value.Date != V_Finish!.Value.Date;
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

        public bool HasPercentCompleteVariance
        {
            get
            {
                // Highlight if P6 percent is greater than MS percent
                return P6_PercentComplete > MS_PercentComplete;
            }
        }

        // ========================================
        // EDITABLE STATE PROPERTIES
        // ========================================

        // ThreeWeekStart is only editable when there's no actual start (forecasting)
        // When V_Start exists, the field shows the actual and is read-only
        public bool IsThreeWeekStartEditable => V_Start == null;

        // ThreeWeekFinish is only editable when there's no actual finish (forecasting)
        // When V_Finish exists, the field shows the actual and is read-only
        public bool IsThreeWeekFinishEditable => V_Finish == null;

        // ========================================
        // REQUIRED FIELD INDICATORS (for conditional formatting)
        // ========================================

        public bool IsMissedStartReasonRequired
        {
            get
            {
                // MissedStartReason required if P6_Start is within current week and was missed
                if (!string.IsNullOrWhiteSpace(MissedStartReason))
                    return false; // Already has reason

                if (!P6_Start.HasValue)
                    return false; // No P6 start to miss

                // P6_Start must be within current week (WeekEndDate-7 to WeekEndDate)
                var weekStart = WeekEndDate.Date.AddDays(-7);
                var weekEnd = WeekEndDate.Date;
                if (P6_Start.Value.Date < weekStart || P6_Start.Value.Date > weekEnd)
                    return false; // P6 start is outside current week

                // Required if: no actual start OR started late
                if (!V_Start.HasValue)
                    return true; // Never started - needs explanation

                return V_Start.Value.Date > P6_Start.Value.Date; // Started late - needs explanation
            }
        }

        public bool IsMissedFinishReasonRequired
        {
            get
            {
                // MissedFinishReason required if P6_Finish is within current week and was missed
                if (!string.IsNullOrWhiteSpace(MissedFinishReason))
                    return false; // Already has reason

                if (!P6_Finish.HasValue)
                    return false; // No P6 finish to miss

                // P6_Finish must be within current week (WeekEndDate-7 to WeekEndDate)
                var weekStart = WeekEndDate.Date.AddDays(-7);
                var weekEnd = WeekEndDate.Date;
                if (P6_Finish.Value.Date < weekStart || P6_Finish.Value.Date > weekEnd)
                    return false; // P6 finish is outside current week

                // Required if: no actual finish OR finished late
                if (!V_Finish.HasValue)
                    return true; // Never finished - needs explanation

                return V_Finish.Value.Date > P6_Finish.Value.Date; // Finished late - needs explanation
            }
        }

        public bool IsThreeWeekStartRequired
        {
            get
            {
                // 3WLA Start required if P6_Start <= WeekEndDate+21, no actual, no forecast
                if (ThreeWeekStart.HasValue)
                    return false; // Already has 3WLA date

                if (V_Start.HasValue)
                    return false; // Already started - no forecast needed

                if (!P6_Start.HasValue)
                    return false; // No P6 start date

                // Required if P6_Start is past-due or within 3-week lookahead
                return P6_Start.Value.Date <= WeekEndDate.Date.AddDays(21);
            }
        }

        public bool IsThreeWeekFinishRequired
        {
            get
            {
                // 3WLA Finish required if P6_Finish <= WeekEndDate+21, no actual, no forecast
                if (ThreeWeekFinish.HasValue)
                    return false; // Already has 3WLA date

                if (V_Finish.HasValue)
                    return false; // Already finished - no forecast needed

                if (!P6_Finish.HasValue)
                    return false; // No P6 finish date

                // Required if P6_Finish is past-due or within 3-week lookahead
                return P6_Finish.Value.Date <= WeekEndDate.Date.AddDays(21);
            }
        }

        // Gold highlight: 3WLA Start has a forecast different from P6
        public bool HasThreeWeekStartForecast
        {
            get
            {
                if (!ThreeWeekStart.HasValue)
                    return false; // No forecast - no gold

                if (!P6_Start.HasValue)
                    return true; // Has forecast but no P6 date to compare

                return ThreeWeekStart.Value.Date != P6_Start.Value.Date;
            }
        }

        // Gold highlight: 3WLA Finish has a forecast different from P6
        public bool HasThreeWeekFinishForecast
        {
            get
            {
                if (!ThreeWeekFinish.HasValue)
                    return false; // No forecast - no gold

                if (!P6_Finish.HasValue)
                    return true; // Has forecast but no P6 date to compare

                return ThreeWeekFinish.Value.Date != P6_Finish.Value.Date;
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