using System.ComponentModel;

namespace VANTAGE.Models.AI
{
    // Status enum for review item validation state
    public enum ScanMatchStatus
    {
        Ready,      // Can be applied
        Warning,    // Has warnings but can be applied
        NotFound,   // UniqueID not found in database
        Error       // Invalid data, cannot be applied
    }

    // Review grid item combining extraction with database match
    public class ScanReviewItem : INotifyPropertyChanged
    {
        // From extraction (read-only after creation)
        public string ExtractedUniqueId { get; set; } = null!;
        public decimal? ExtractedPct { get; set; }
        public int Confidence { get; set; }
        public string? RawExtraction { get; set; }

        // From database match (read-only)
        public Activity? MatchedRecord { get; set; }
        public string? MatchedUniqueId { get; set; }  // For debugging - shows actual UniqueID we'll update
        public decimal? CurrentPercent { get; set; }
        public string? Description { get; set; }

        // User editable fields (with property changed)
        private decimal? _newPercent;
        public decimal? NewPercent
        {
            get => _newPercent;
            set
            {
                if (_newPercent != value)
                {
                    _newPercent = value;
                    OnPropertyChanged(nameof(NewPercent));
                }
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        // Validation (updated by validation service)
        private ScanMatchStatus _status;
        public ScanMatchStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        private string? _validationMessage;
        public string? ValidationMessage
        {
            get => _validationMessage;
            set
            {
                if (_validationMessage != value)
                {
                    _validationMessage = value;
                    OnPropertyChanged(nameof(ValidationMessage));
                }
            }
        }

        // Helper: Short display of UniqueID (last 6 chars for grid)
        public string ShortUniqueId => ExtractedUniqueId?.Length > 6
            ? ExtractedUniqueId.Substring(ExtractedUniqueId.Length - 6)
            : ExtractedUniqueId ?? "";

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
