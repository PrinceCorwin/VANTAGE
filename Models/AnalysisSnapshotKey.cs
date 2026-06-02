using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VANTAGE.Models
{
    // One Submit Week submission group surfaced in the Analysis view's Snapshots picker.
    // (AssignedTo, ProjectID, WeekEndDate) is the same key ManageSnapshotsDialog groups by.
    // ProgDate is intentionally NOT in the key — scanning ProgDate across all rows times out
    // at production volume (per ManageSnapshotsDialog's comment), and a (user, project, week)
    // tuple is effectively unique for analysis purposes. If the New ActNOs from P6 dialog has
    // created sibling rows with a different UtcNow ProgDate for the same tuple, the snapshot
    // aggregation joins on just (user, project, week) and rolls those siblings together —
    // which is the correct semantics for analysis.
    public class AnalysisSnapshotKey : INotifyPropertyChanged
    {
        public string ProjectID { get; set; } = string.Empty;
        public string WeekEndDate { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public int RowCount { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public string DisplayText =>
            $"{AssignedTo} | {ProjectID} | {WeekEndDate} ({RowCount} rows)";

        public override string ToString() => DisplayText;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
