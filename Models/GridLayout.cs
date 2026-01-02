using System.Collections.Generic;

namespace VANTAGE.Models
{
    // Represents a named grid layout configuration that saves all grid preferences together
    public class GridLayout
    {
        public string Name { get; set; } = string.Empty;
        public GridPreferencesData ProgressGrid { get; set; } = new();
        public GridPreferencesData ScheduleMasterGrid { get; set; } = new();
        public GridPreferencesData ScheduleDetailGrid { get; set; } = new();
        public double ScheduleMasterHeight { get; set; }
        public double ScheduleDetailHeight { get; set; }
    }

    // Grid column preferences data (shared structure for all grids)
    public class GridPreferencesData
    {
        public int Version { get; set; } = 1;
        public string SchemaHash { get; set; } = string.Empty;
        public List<GridColumnPrefData> Columns { get; set; } = new();
    }

    // Individual column preference
    public class GridColumnPrefData
    {
        public string Name { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        public double Width { get; set; }
        public bool IsHidden { get; set; }
    }
}
