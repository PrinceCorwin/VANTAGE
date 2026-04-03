using System.Collections.Generic;

namespace VANTAGE.Models
{
    // Saved import profile for the Import from AI Takeoff dialog
    public class ImportProfile
    {
        public string Name { get; set; } = string.Empty;

        // Output: "ImportRecords", "CreateExcel", "ImportAndExcel"
        public string OutputMode { get; set; } = "ImportRecords";

        // Handling: "KeepPipe", "KeepSpl", "KeepPipeAndSpl"
        public string HandlingMode { get; set; } = "KeepSpl";

        // Options
        public bool RollUpBUHardware { get; set; }
        public bool RollUpFabPerDwg { get; set; }

        // ROC Set selection display text (e.g., "None" or "PROJ1 - SetA")
        public string ROCSetSelection { get; set; } = "None";

        // Column mappings: file header → Activity property (or "Unmapped")
        public List<ColumnMappingEntry> ColumnMappings { get; set; } = new();

        // Metadata: field name → mode + entered value
        public List<MetadataEntry> MetadataFields { get; set; } = new();
    }

    // Serializable column mapping entry for import profiles
    public class ColumnMappingEntry
    {
        public string FileHeader { get; set; } = string.Empty;
        public string SelectedMapping { get; set; } = "Unmapped";
    }

    // Serializable metadata field entry for import profiles
    public class MetadataEntry
    {
        public string FieldName { get; set; } = string.Empty;
        public string Mode { get; set; } = "Enter Value";
        public string EnteredValue { get; set; } = string.Empty;
    }
}
