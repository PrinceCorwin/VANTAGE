using System.Collections.Generic;

namespace VANTAGE.Models
{
    // Represents a single UDF column mapping from P6 to Schedule
    public class ScheduleUDFMapping
    {
        public string TargetColumn { get; set; } = string.Empty;  // SchedUDF1, SchedUDF2, etc.
        public string PrimaryHeader { get; set; } = string.Empty;  // Row 1 header from P6
        public string SecondaryHeader { get; set; } = string.Empty; // Row 2 header from P6
        public string DisplayName { get; set; } = string.Empty;  // User's preferred column header
        public bool IsEnabled { get; set; } = false;
    }

    // Container for all UDF mappings (serialized to UserSettings as JSON)
    public class ScheduleUDFMappingConfig
    {
        public List<ScheduleUDFMapping> Mappings { get; set; } = new();

        // Initialize with default empty mappings for SchedUDF1-5
        public static ScheduleUDFMappingConfig CreateDefault()
        {
            return new ScheduleUDFMappingConfig
            {
                Mappings = new List<ScheduleUDFMapping>
                {
                    new() { TargetColumn = "SchedUDF1" },
                    new() { TargetColumn = "SchedUDF2" },
                    new() { TargetColumn = "SchedUDF3" },
                    new() { TargetColumn = "SchedUDF4" },
                    new() { TargetColumn = "SchedUDF5" }
                }
            };
        }
    }
}
