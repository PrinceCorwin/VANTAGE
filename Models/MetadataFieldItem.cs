using System.ComponentModel;

namespace VANTAGE.Models
{
    // Represents one required metadata field in the Import Takeoff dialog
    public class MetadataFieldItem : INotifyPropertyChanged
    {
        private string _mode = "Enter Value";
        private string _enteredValue = string.Empty;

        // Activity property name
        public string FieldName { get; set; } = string.Empty;

        // Enter Value = user types a value for all rows; Use Source = use mapped source column
        public string Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    OnPropertyChanged(nameof(Mode));
                    OnPropertyChanged(nameof(IsTextEnabled));
                }
            }
        }

        // Value entered by user (applied to every row when Mode is Enter Value)
        public string EnteredValue
        {
            get => _enteredValue;
            set
            {
                if (_enteredValue != value)
                {
                    _enteredValue = value;
                    OnPropertyChanged(nameof(EnteredValue));
                }
            }
        }

        // Text field is enabled only in Enter Value mode
        public bool IsTextEnabled => Mode == "Enter Value";

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
