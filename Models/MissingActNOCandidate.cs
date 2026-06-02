using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VANTAGE.Models
{
    // One row in the "New ActNOs from P6" dialog: a SchedActNO that exists in the just-imported
    // P6 Schedule table but does NOT appear in the local ProgressSnapshots mirror for the same
    // WeekEndDate across the user's selected ProjectIDs. The dialog binds these as editable rows
    // so the user can adjust values before creating stubs.
    public class MissingActNOCandidate : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public string SchedActNO { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double BudgetMHs { get; set; }
        public double PercentEntry { get; set; }
        public DateTime? ActStart { get; set; }
        public DateTime? ActFin { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
