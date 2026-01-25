using Syncfusion.Data;
using Syncfusion.UI.Xaml.Grid;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace VANTAGE.Utilities
{
    // Configures table summary rows for SfDataGrid instances
    public static class GridSummaryHelper
    {
        // Explicit whitelist of columns to summarize with their formats
        private static readonly Dictionary<string, string> ProgressGridSummaryColumns = new()
        {
            { "Quantity", "{Sum:N2}" },
            { "EarnQtyEntry", "{Sum:N2}" },
            { "BudgetMHs", "{Sum:N2}" },
            { "EarnMHsCalc", "{Sum:N2}" },
            { "ClientBudget", "{Sum:N2}" }
        };

        // Adds a table summary row at the bottom of the Progress grid
        public static void AddProgressTableSummary(SfDataGrid grid)
        {
            var summaryRow = new GridTableSummaryRow()
            {
                Position = TableSummaryRowPosition.Bottom,
                ShowSummaryInRow = false,
                SummaryColumns = new ObservableCollection<ISummaryColumn>()
            };

            foreach (var (columnName, format) in ProgressGridSummaryColumns)
            {
                summaryRow.SummaryColumns.Add(new GridSummaryColumn()
                {
                    Name = $"{columnName}Sum",
                    MappingName = columnName,
                    SummaryType = SummaryType.DoubleAggregate,
                    Format = format
                });
            }

            grid.TableSummaryRows.Clear();
            grid.TableSummaryRows.Add(summaryRow);
        }
    }
}
