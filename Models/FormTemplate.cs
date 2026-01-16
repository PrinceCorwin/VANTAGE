using System.ComponentModel;
using System.Text.Json.Serialization;

namespace VANTAGE.Models
{
    // Main FormTemplate entity stored in database
    public class FormTemplate : INotifyPropertyChanged
    {
        private string _templateId = System.Guid.NewGuid().ToString();
        private string _templateName = string.Empty;
        private string _templateType = string.Empty;
        private string _structureJson = string.Empty;
        private bool _isBuiltIn;
        private string _createdBy = string.Empty;
        private string _createdUtc = string.Empty;

        public string TemplateID
        {
            get => _templateId;
            set { _templateId = value; OnPropertyChanged(nameof(TemplateID)); }
        }

        public string TemplateName
        {
            get => _templateName;
            set { _templateName = value; OnPropertyChanged(nameof(TemplateName)); }
        }

        // Cover, List, Form, or Grid
        public string TemplateType
        {
            get => _templateType;
            set { _templateType = value; OnPropertyChanged(nameof(TemplateType)); }
        }

        // JSON structure varies by TemplateType
        public string StructureJson
        {
            get => _structureJson;
            set { _structureJson = value; OnPropertyChanged(nameof(StructureJson)); }
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

    // JSON structure for Cover type templates
    // Default image: images/CoverPic.png (null means use default)
    public class CoverStructure
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "COVER SHEET";

        // null = use default images/CoverPic.png; otherwise absolute path to custom image
        [JsonPropertyName("imagePath")]
        public string? ImagePath { get; set; }

        [JsonPropertyName("imageWidthPercent")]
        public int ImageWidthPercent { get; set; } = 80;

        [JsonPropertyName("footerText")]
        public string? FooterText { get; set; }
    }

    // JSON structure for List type templates (e.g., TOC)
    public class ListStructure
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "TABLE OF CONTENTS";

        [JsonPropertyName("items")]
        public List<string> Items { get; set; } = new();

        [JsonPropertyName("fontSizeAdjustPercent")]
        public int FontSizeAdjustPercent { get; set; } = 0;

        [JsonPropertyName("footerText")]
        public string? FooterText { get; set; }
    }

    // JSON structure for Form type templates (sections with items)
    public class FormStructure
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "FORM";

        [JsonPropertyName("columns")]
        public List<TemplateColumn> Columns { get; set; } = new();

        [JsonPropertyName("rowHeightIncreasePercent")]
        public int RowHeightIncreasePercent { get; set; } = 0;

        [JsonPropertyName("fontSizeAdjustPercent")]
        public int FontSizeAdjustPercent { get; set; } = 0;

        [JsonPropertyName("sections")]
        public List<SectionDefinition> Sections { get; set; } = new();

        [JsonPropertyName("footerText")]
        public string? FooterText { get; set; }
    }

    // JSON structure for Grid type templates (empty rows for data entry)
    public class GridStructure
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "GRID";

        [JsonPropertyName("columns")]
        public List<TemplateColumn> Columns { get; set; } = new();

        [JsonPropertyName("rowCount")]
        public int RowCount { get; set; } = 22;

        [JsonPropertyName("rowHeightIncreasePercent")]
        public int RowHeightIncreasePercent { get; set; } = 0;

        [JsonPropertyName("fontSizeAdjustPercent")]
        public int FontSizeAdjustPercent { get; set; } = 0;

        [JsonPropertyName("footerText")]
        public string? FooterText { get; set; }
    }

    // Column definition used by Form and Grid types
    public class TemplateColumn
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("widthPercent")]
        public int WidthPercent { get; set; } = 10;
    }

    // Section definition used by Form type
    public class SectionDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("items")]
        public List<string> Items { get; set; } = new();
    }

    // JSON structure for Drawings type templates (drawing images)
    public class DrawingsStructure
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "DRAWINGS";

        // Source type: "Local" for local folder, "Procore" for Procore API (future)
        [JsonPropertyName("source")]
        public string Source { get; set; } = "Local";

        // For Local source: folder path pattern (can use tokens like {WorkPackage})
        [JsonPropertyName("folderPath")]
        public string? FolderPath { get; set; }

        // File extensions to include (e.g., "*.pdf,*.png,*.jpg")
        [JsonPropertyName("fileExtensions")]
        public string FileExtensions { get; set; } = "*.pdf,*.png,*.jpg,*.jpeg,*.tif,*.tiff";

        // Images per page (1, 2, or 4)
        [JsonPropertyName("imagesPerPage")]
        public int ImagesPerPage { get; set; } = 1;

        // Include drawing file name as caption
        [JsonPropertyName("showCaptions")]
        public bool ShowCaptions { get; set; } = true;

        [JsonPropertyName("footerText")]
        public string? FooterText { get; set; }
    }

    // Constants for template types
    public static class TemplateTypes
    {
        public const string Cover = "Cover";
        public const string List = "List";
        public const string Form = "Form";
        public const string Grid = "Grid";
        public const string Drawings = "Drawings";
    }

    // Drawing item for the Generate tab DwgNO grid
    public class DrawingItem
    {
        public string WorkPackage { get; set; } = string.Empty;
        public string DwgNO { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
    }
}
