using Syncfusion.UI.Xaml.Grid;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using VANTAGE.Models;

namespace VANTAGE.Styles
{
    public class RecordOwnershipRowStyleSelector : StyleSelector
    {
        // Set by the host view after grid load — the same brush Syncfusion paints
        // selected rows with on mouse-select. Used for the IsBulkSelected highlight
        // so the visual matches the native row-selection color.
        public Brush? BulkSelectionBrush { get; set; }

        public override Style? SelectStyle(object item, DependencyObject container)
        {
            // Extract the Activity from Syncfusion's DataRow wrapper
            Activity? activity = null;
            DataRow? dataRow = null;

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

            // Apply row backgrounds — even rows use GridCellBackground, odd rows use GridAlternatingRowBackground
            if (dataRow != null)
            {
                var bgKey = dataRow.RowIndex % 2 != 0 ? "GridAlternatingRowBackground" : "GridCellBackground";
                style.Setters.Add(new Setter(VirtualizingCellsControl.BackgroundProperty, Application.Current.Resources[bgKey]));
            }

            // Apply dimmed foreground for non-owned records
            if (!isOwned)
            {
                style.Setters.Add(new Setter(VirtualizingCellsControl.ForegroundProperty, Application.Current.Resources["NotOwnedRowForeground"]));
            }

            // Add hover trigger to keep foreground readable
            var hoverTrigger = new Trigger
            {
                Property = VirtualizingCellsControl.IsMouseOverProperty,
                Value = true
            };
            hoverTrigger.Setters.Add(new Setter(VirtualizingCellsControl.BackgroundProperty, Application.Current.Resources["GridRowHoverBackground"]));
            style.Triggers.Add(hoverTrigger);

            // Bulk-selection highlight — driven by Activity.IsBulkSelected (transient,
            // INPC). Replaces engagement of Syncfusion's per-cell selection model for
            // Ctrl+A / Actions → Select All on large grids. The DataTrigger binds to the
            // row's data context so flipping the flag re-evaluates this style on the fly
            // without any grid-level invalidation call.
            var bulkSelectedTrigger = new DataTrigger
            {
                Binding = new Binding(nameof(Activity.IsBulkSelected)),
                Value = true
            };
            // Match Syncfusion's native row-selection brush if the host view supplied
            // it; otherwise fall back to the theme accent so we always paint something.
            object bulkBrush = BulkSelectionBrush
                ?? Application.Current.Resources["AccentColor"];
            bulkSelectedTrigger.Setters.Add(new Setter(VirtualizingCellsControl.BackgroundProperty, bulkBrush));
            style.Triggers.Add(bulkSelectedTrigger);

            return style;
        }
    }
}