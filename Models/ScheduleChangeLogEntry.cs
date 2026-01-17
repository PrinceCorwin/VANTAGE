using System;

namespace VANTAGE.Models
{
    // Represents a single change made in the Schedule detail grid
    public class ScheduleChangeLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Username { get; set; } = string.Empty;
        public string WeekEndDate { get; set; } = string.Empty;
        public string UniqueID { get; set; } = string.Empty;
        public string SchedActNO { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;

        // For UI binding - checkbox to select for applying to Activities
        public bool IsSelected { get; set; }
    }
}
