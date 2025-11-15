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

            if (item is DataRow dataRow)
            {
                activity = dataRow.RowData as Activity;
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

            // Return dimmed style for non-owned records (even for admins)
            if (!isOwned)
            {
                var style = new Style(typeof(VirtualizingCellsControl));
                style.Setters.Add(new Setter(VirtualizingCellsControl.BackgroundProperty, Application.Current.Resources["NotOwnedRowBackground"]));
                style.Setters.Add(new Setter(VirtualizingCellsControl.ForegroundProperty, Application.Current.Resources["NotOwnedRowForeground"]));
                return style;
            }

            return null; // Use default styling for owned records
        }
    }
}
