using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VANTAGE.Models
{
    // ViewModel for master grid row - combines P6 data with MS rollups
    public class ScheduleMasterRow : INotifyPropertyChanged
    {
        // P6 Data (from Schedule table)
        public string SchedActNO { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WbsId { get; set; } = string.Empty;
        public DateTime? P6_PlannedStart { get; set; }
        public DateTime? P6_PlannedFinish { get; set; }
        public DateTime? P6_ActualStart { get; set; }
        public DateTime? P6_ActualFinish { get; set; }
        public double P6_PercentComplete { get; set; }
        public double P6_BudgetMHs { get; set; }

        // MS Rollups (calculated from ProgressSnapshots)
        public DateTime? MS_ActualStart { get; set; }
        public DateTime? MS_ActualFinish { get; set; }
        public double MS_PercentComplete { get; set; }
        public double MS_BudgetMHs { get; set; }

        // Editable fields (from Schedule table)
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
                }
            }
        }

        // Variance indicators (calculated properties)
        public bool HasStartVariance => P6_ActualStart != MS_ActualStart ||
                                        (P6_ActualStart == null) != (MS_ActualStart == null);

        public bool HasFinishVariance => P6_ActualFinish != MS_ActualFinish ||
                                         (P6_ActualFinish == null) != (MS_ActualFinish == null);

        // Child collection for DetailsViewDataGrid
        public ObservableCollection<ProgressSnapshot> DetailActivities { get; set; } = new ObservableCollection<ProgressSnapshot>();

        // Internal tracking
        public DateTime WeekEndDate { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}