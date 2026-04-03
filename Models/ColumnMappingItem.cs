using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VANTAGE.Models
{
    // Represents one row in the Import Takeoff column mapping grid
    public class ColumnMappingItem : INotifyPropertyChanged
    {
        private string _selectedMapping = "Unmapped";

        // Column header from the Labor tab
        public string FileHeader { get; set; } = string.Empty;

        // Sample value from the first data row
        public string SampleValue { get; set; } = string.Empty;

        // Available Vantage columns for the dropdown
        public ObservableCollection<string> AvailableMappings { get; set; } = new();

        // Currently selected Vantage column mapping
        public string SelectedMapping
        {
            get => _selectedMapping;
            set
            {
                if (_selectedMapping != value)
                {
                    _selectedMapping = value;
                    OnPropertyChanged(nameof(SelectedMapping));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
