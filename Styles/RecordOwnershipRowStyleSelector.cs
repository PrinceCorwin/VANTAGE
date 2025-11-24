using System;
using System.Windows;
using System.Windows.Controls;
using Syncfusion.UI.Xaml.Grid;
using VANTAGE.Models;

namespace VANTAGE.Styles
{
    public class RecordOwnershipRowStyleSelector : StyleSelector
    {
        public override Style SelectStyle(object item, DependencyObject container)
        {
            // Extract the Activity from Syncfusion's DataRow wrapper
            Activity activity = null;
            DataRow dataRow = null;

            if (item is DataRow dr)
            {
                activity = dr.RowData as Activity;
                dataRow = dr;
            }
            else
            {
                activity = item as Activity;
            }

            // If activity is null, use default style
            if (activity == null)
            {
                return null;
            }

            // Check if current user owns this record
            bool isOwned = string.Equals(activity.AssignedTo, App.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase);

            var style = new Style(typeof(VirtualizingCellsControl));

            // Apply alternating background for all rows
            if (dataRow != null && dataRow.RowIndex % 2 != 0)
            {
                style.Setters.Add(new Setter(VirtualizingCellsControl.BackgroundProperty, Application.Current.Resources["GridAlternatingRowBackground"]));
            }

            // Apply dimmed foreground for non-owned records
            if (!isOwned)
            {
                style.Setters.Add(new Setter(VirtualizingCellsControl.ForegroundProperty, Application.Current.Resources["NotOwnedRowForeground"]));
            }

            return style;
        }
    }
}
