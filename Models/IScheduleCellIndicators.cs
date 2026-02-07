namespace VANTAGE.Models
{
    // Interface for cell styling indicators used by Schedule grid DataTriggers.
    // Both ScheduleMasterRow (actual data) and ScheduleViewModel implement this because
    // Syncfusion's SfDataGrid briefly sets cell DataContext to ViewModel during cell recycling.
    // ViewModel returns false for all; Row returns computed values.
    public interface IScheduleCellIndicators
    {
        bool IsMissedStartReasonRequired { get; }
        bool IsMissedFinishReasonRequired { get; }
        bool IsThreeWeekStartRequired { get; }
        bool IsThreeWeekFinishRequired { get; }
        bool HasStartVariance { get; }
        bool HasFinishVariance { get; }
        bool HasBudgetMHsVariance { get; }
        bool HasPercentCompleteVariance { get; }
    }
}
