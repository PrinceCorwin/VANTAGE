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

    // JSON structure for form references in WPTemplate.FormsJson.
    // A reference points at EITHER a form template (FormTemplateId) OR a saved Progress Book
    // layout (ProgressBookLayoutId). ProgressBookLayoutId is nullable and absent from legacy
    // JSON, so existing WP templates deserialize unchanged.
    public class FormReference
    {
        [JsonPropertyName("formTemplateId")]
        public string FormTemplateId { get; set; } = string.Empty;

        [JsonPropertyName("progressBookLayoutId")]
        public int? ProgressBookLayoutId { get; set; }
    }

    // JSON structure for WPTemplate.DefaultSettings
    public class WPTemplateSettings
    {
        // The filename patterns that reproduce the historical (pre-configurable) naming, used as
        // both the seeded default and the fallback when a template's stored pattern is empty/absent.
        // {FormIndex} (1-based) and {FormName} are only meaningful for individual form files; every
        // other {token} resolves the same way the WP Name Pattern does (Activity fields + built-ins).
        public const string DefaultIndividualFileNamePattern = "{FormIndex}. WP {FormName}";
        public const string DefaultMergedFileNamePattern = "{WorkPackage} - WP";

        [JsonPropertyName("expirationDays")]
        public int ExpirationDays { get; set; } = 14;

        // Filename patterns for generated PDFs (tokens resolved per work package at generation).
        // Absent from legacy JSON, so those templates deserialize to the defaults above.
        [JsonPropertyName("individualFileNamePattern")]
        public string IndividualFileNamePattern { get; set; } = DefaultIndividualFileNamePattern;

        [JsonPropertyName("mergedFileNamePattern")]
        public string MergedFileNamePattern { get; set; } = DefaultMergedFileNamePattern;
    }
}
