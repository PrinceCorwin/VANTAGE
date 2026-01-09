using System;

namespace VANTAGE.Models
{
    // Mapping table to track which ProjectIDs are covered by a schedule import
    // Composite PK: (WeekEndDate, ProjectID)
    public class ScheduleProjectMapping
    {
        public DateTime WeekEndDate { get; set; }
        public string ProjectID { get; set; } = string.Empty;
    }
}