using System;

namespace VANTAGE.Models
{
    // Local-only table for P6 imported data and 3WLA edits
    // Composite PK: (SchedActNO, WeekEndDate)
    public class Schedule
    {
        // ========================================
        // PRIMARY KEY FIELDS
        // ========================================

        public string SchedActNO { get; set; } = string.Empty;
        public DateTime WeekEndDate { get; set; }

        // ========================================
        // CORE FIELDS
        // ========================================

        public string WbsId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // ========================================
        // P6 IMPORTED FIELDS (FROM P6 file)
        // ========================================

        public DateTime? P6_PlannedStart { get; set; }
        public DateTime? P6_PlannedFinish { get; set; }
        public DateTime? P6_ActualStart { get; set; }
        public DateTime? P6_ActualFinish { get; set; }
        public double P6_PercentComplete { get; set; }
        public double P6_BudgetMHs { get; set; }

        // ========================================
        // 3WLA FIELDS (User edits in Schedule Module)
        // ========================================

        public DateTime? ThreeWeekStart { get; set; }
        public DateTime? ThreeWeekFinish { get; set; }
        public string? MissedStartReason { get; set; }
        public string? MissedFinishReason { get; set; }

        // ========================================
        // AUDIT FIELDS
        // ========================================

        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime UpdatedUtcDate { get; set; }
    }
}