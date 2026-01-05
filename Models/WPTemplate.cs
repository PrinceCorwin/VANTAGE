using System.ComponentModel;
using System.Text.Json.Serialization;

namespace VANTAGE.Models
{
    // Main WPTemplate entity stored in database
    public class WPTemplate : INotifyPropertyChanged
    {
        private string _wpTemplateId = System.Guid.NewGuid().ToString();
        private string _wpTemplateName = string.Empty;
        private string _formsJson = string.Empty;
        private string _defaultSettings = string.Empty;
        private bool _isBuiltIn;
        private string _createdBy = string.Empty;
        private string _createdUtc = string.Empty;

        public string WPTemplateID
        {
            get => _wpTemplateId;
            set { _wpTemplateId = value; OnPropertyChanged(nameof(WPTemplateID)); }
        }

        public string WPTemplateName
        {
            get => _wpTemplateName;
            set { _wpTemplateName = value; OnPropertyChanged(nameof(WPTemplateName)); }
        }

        // JSON array of form references: [{"formTemplateId": "..."}, ...]
        public string FormsJson
        {
            get => _formsJson;
            set { _formsJson = value; OnPropertyChanged(nameof(FormsJson)); }
        }

        // JSON with settings: { "expirationDays": 14 }
        public string DefaultSettings
        {
            get => _defaultSettings;
            set { _defaultSettings = value; OnPropertyChanged(nameof(DefaultSettings)); }
        }

        public bool IsBuiltIn
        {
            get => _isBuiltIn;
            set { _isBuiltIn = value; OnPropertyChanged(nameof(IsBuiltIn)); }
        }

        public string CreatedBy
        {
            get => _createdBy;
            set { _createdBy = value; OnPropertyChanged(nameof(CreatedBy)); }
        }

        public string CreatedUtc
        {
            get => _createdUtc;
            set { _createdUtc = value; OnPropertyChanged(nameof(CreatedUtc)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // JSON structure for form references in WPTemplate.FormsJson
    public class FormReference
    {
        [JsonPropertyName("formTemplateId")]
        public string FormTemplateId { get; set; } = string.Empty;
    }

    // JSON structure for WPTemplate.DefaultSettings
    public class WPTemplateSettings
    {
        [JsonPropertyName("expirationDays")]
        public int ExpirationDays { get; set; } = 14;
    }
}
