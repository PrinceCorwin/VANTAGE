namespace VANTAGE.Models
{
    // Represents an aggregated row in the Analysis summary grid
    // Groups activities by a user-selected field and sums key metrics
    public class AnalysisSummaryRow
    {
        public string GroupValue { get; set; } = string.Empty;  // The field value being grouped
        public double BudgetMHs { get; set; }                   // Sum of BudgetMHs
        public double EarnedMHs { get; set; }                   // Sum of EarnedMHs
        public double Quantity { get; set; }                    // Sum of Quantity
        public double QtyEarned { get; set; }                   // Sum of QtyEarned
        public double PercentComplete { get; set; }             // Weighted: (EarnedMHs / BudgetMHs) * 100
    }
}
