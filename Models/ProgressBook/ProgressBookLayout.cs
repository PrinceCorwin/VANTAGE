using System;
using System.ComponentModel;
using System.Text.Json;

namespace VANTAGE.Models.ProgressBook
{
    // Database entity for stored progress book layouts
    public class ProgressBookLayout : INotifyPropertyChanged
    {
        private int _id;
        private string _name = string.Empty;
        private string _projectId = string.Empty;
        private string _createdBy = string.Empty;
        private DateTime _createdUtc;
        private DateTime _updatedUtc;
        private string _configurationJson = string.Empty;

        // Auto-increment primary key
        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        // Display name for the layout
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        // Project this layout belongs to
        public string ProjectId
        {
            get => _projectId;
            set { _projectId = value; OnPropertyChanged(nameof(ProjectId)); }
        }

        // Username who created the layout
        public string CreatedBy
        {
            get => _createdBy;
            set { _createdBy = value; OnPropertyChanged(nameof(CreatedBy)); }
        }

        public DateTime CreatedUtc
        {
            get => _createdUtc;
            set { _createdUtc = value; OnPropertyChanged(nameof(CreatedUtc)); }
        }

        public DateTime UpdatedUtc
        {
            get => _updatedUtc;
            set { _updatedUtc = value; OnPropertyChanged(nameof(UpdatedUtc)); }
        }

        // JSON-serialized ProgressBookConfiguration
        public string ConfigurationJson
        {
            get => _configurationJson;
            set { _configurationJson = value; OnPropertyChanged(nameof(ConfigurationJson)); }
        }

        // Deserialize the configuration from JSON
        public ProgressBookConfiguration GetConfiguration()
        {
            if (string.IsNullOrWhiteSpace(ConfigurationJson))
                return ProgressBookConfiguration.CreateDefault();

            try
            {
                return JsonSerializer.Deserialize<ProgressBookConfiguration>(ConfigurationJson)
                    ?? ProgressBookConfiguration.CreateDefault();
            }
            catch
            {
                return ProgressBookConfiguration.CreateDefault();
            }
        }

        // Serialize the configuration to JSON
        public void SetConfiguration(ProgressBookConfiguration config)
        {
            ConfigurationJson = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = false
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
