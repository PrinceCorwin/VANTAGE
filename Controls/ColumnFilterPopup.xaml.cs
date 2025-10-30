using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using VANTAGE.Data;

namespace VANTAGE.Controls
{
    public partial class ColumnFilterPopup : UserControl
    {
        public event EventHandler<FilterEventArgs> FilterApplied;
        public event EventHandler FilterCleared;

        // New: returns selected values when OK pressed
        public class ValueSelection
        {
            public List<string> SelectedValues { get; set; }
        }

        private string _columnName;
        private int _displayLimit = 1000; // default limit for unique options
        private List<string> _allValues = new List<string>();

        // Snapshot of state when popup opens
        private string _snapshotSearch = string.Empty;
        private HashSet<string> _snapshotCheckedValues = new HashSet<string>(StringComparer.Ordinal);
        private bool? _snapshotSelectAll = false;

        public ColumnFilterPopup()
        {
            InitializeComponent();
            IsVisibleChanged += ColumnFilterPopup_IsVisibleChanged;
        }

        // Capture a fresh snapshot any time the control becomes visible
        private void ColumnFilterPopup_IsVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                CaptureSnapshot();
            }
        }

        private void CaptureSnapshot()
        {
            try
            {
                _snapshotSearch = txtSearch?.Text ?? string.Empty;
                _snapshotSelectAll = chkSelectAll?.IsChecked;

                _snapshotCheckedValues.Clear();
                if (itemsValues != null)
                {
                    foreach (var obj in itemsValues.Items)
                    {
                        if (obj is CheckBox child)
                        {
                            var tag = child.Tag?.ToString() ?? string.Empty;
                            if (child.IsChecked == true)
                            {
                                _snapshotCheckedValues.Add(tag);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Swallow exceptions to avoid breaking the UI; snapshots are best-effort.
            }
        }

        private void RestoreFromSnapshot()
        {
            try
            {
                if (txtSearch != null)
                {
                    txtSearch.Text = _snapshotSearch;
                }

                if (chkSelectAll != null)
                {
                    chkSelectAll.IsChecked = _snapshotSelectAll;
                }

                if (itemsValues != null)
                {
                    foreach (var obj in itemsValues.Items)
                    {
                        if (obj is CheckBox child)
                        {
                            var tag = child.Tag?.ToString() ?? string.Empty;
                            child.IsChecked = _snapshotCheckedValues.Contains(tag);
                        }
                    }
                }
            }
            catch
            {
                // Ignore restore errors; closing the popup is the primary goal.
            }
        }

        // Called by XAML: Cancel button click
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Restore UI state to snapshot
            RestoreFromSnapshot();

            // Close popup/context menu containing this control
            CloseContainingPopup();
        }

        // Tries to find and close an enclosing Popup or ContextMenu
        private void CloseContainingPopup()
        {
            try
            {
                DependencyObject? current = this as DependencyObject;

                while (current != null)
                {
                    // Check for Popup
                    if (current is Popup popup)
                    {
                        popup.IsOpen = false;
                        return;
                    }

                    // Check for ContextMenu
                    if (current is ContextMenu cm)
                    {
                        cm.IsOpen = false;
                        return;
                    }

                    // Try logical parent first, then visual parent
                    DependencyObject? next = LogicalTreeHelper.GetParent(current);
                    if (next == null)
                    {
                        next = VisualTreeHelper.GetParent(current);
                    }

                    current = next;
                }
            }
            catch
            {
                // If anything goes wrong, try to relinquish focus as a fallback
                try
                {
                    var parentWindow = Window.GetWindow(this);
                    parentWindow?.Focus();
                }
                catch
                {
                    // ignore
                }
            }
        }

        public void Initialize(string columnName, int displayLimit = 1000)
        {
            _columnName = columnName;
            _displayLimit = displayLimit;
            // Keep XAML-defined content for clear button (do not override)

            // Load distinct values async
            _ = LoadDistinctValuesAsync();
        }

        private async Task LoadDistinctValuesAsync()
        {
            try
            {
                itemsValues.Items.Clear();
                txtTooMany.Visibility = Visibility.Collapsed;

                // Query repository for distinct values for this column (with server-side limit awareness)
                var (values, totalCount) = await ActivityRepository.GetDistinctColumnValuesAsync(_columnName, _displayLimit + 1);

                // Always put 'Unassigned' at the top for AssignedTo
                if (!string.IsNullOrEmpty(_columnName) && _columnName.Equals("AssignedTo", StringComparison.OrdinalIgnoreCase))
                {
                    var rest = values.Where(v => !string.IsNullOrWhiteSpace(v) && !v.Equals("Unassigned", StringComparison.OrdinalIgnoreCase)).ToList();
                    _allValues = new List<string> { "Unassigned" };
                    _allValues.AddRange(rest);
                }
                else
                {
                    _allValues = values;
                }

                bool tooMany = totalCount > _displayLimit;
                if (tooMany)
                {
                    txtTooMany.Visibility = Visibility.Visible;
                }

                foreach (var val in _allValues)
                {
                    string display = val;
                    string tag = val;

                    // Percent/ratio columns: show as 0-100 with %
                    if (!string.IsNullOrEmpty(_columnName) &&
                        (_columnName.Equals("PercentEntry", StringComparison.OrdinalIgnoreCase)
                        || _columnName.Equals("PercentEntry_Display", StringComparison.OrdinalIgnoreCase)
                        || _columnName.Equals("PercentCompleteCalc", StringComparison.OrdinalIgnoreCase)
                        || _columnName.Equals("PercentCompleteCalc_Display", StringComparison.OrdinalIgnoreCase)
                        || _columnName.Equals("EarnedQtyCalc", StringComparison.OrdinalIgnoreCase)
                        || _columnName.Equals("EarnedQtyCalc_Display", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (string.IsNullOrEmpty(val))
                        {
                            display = "(blank)";
                            tag = "";
                        }
                        else if (double.TryParse(val, out var dbl))
                        {
                            display = dbl.ToString("N2") + "%";
                            tag = dbl.ToString("N2"); // store 0-100 string for filtering
                        }
                    }

                    // AssignedTo: show 'Unassigned' for empty
                    if (!string.IsNullOrEmpty(_columnName) && _columnName.Equals("AssignedTo", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(val) || val.Equals("Unassigned", StringComparison.OrdinalIgnoreCase))
                        {
                            display = "Unassigned";
                            tag = "Unassigned";
                        }
                    }

                    // Status: show as-is, but blank if empty
                    if (!string.IsNullOrEmpty(_columnName) && _columnName.Equals("Status", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(val))
                        {
                            display = "(blank)";
                            tag = "(blank)";
                        }
                    }

                    var cb = CreateThemedCheckBox(tag, true);
                    cb.Content = display;
                    itemsValues.Items.Add(cb);
                }
            }
            catch (Exception ex)
            {
                // ignore
            }
        }

        private CheckBox CreateThemedCheckBox(string actualValue, bool isChecked)
        {
            // Display friendly text for null/empty values
            string display = string.IsNullOrEmpty(actualValue) ? "(blank)" : actualValue;

            var cb = new CheckBox
            {
                Content = display,
                Tag = actualValue, // keep the real value in Tag so callers get exact DB value (empty string for blanks)
                IsChecked = isChecked,
                Margin = new Thickness(2),
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"],
                FontFamily = Application.Current.Resources.Contains("FontFamilyPrimary") ? (FontFamily)Application.Current.Resources["FontFamilyPrimary"] : this.FontFamily,
                FontSize = Application.Current.Resources.Contains("FontSizeNormal") ? (double)Application.Current.Resources["FontSizeNormal"] : this.FontSize,
                FontWeight = FontWeights.Normal
            };
            return cb;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var search = txtSearch.Text?.Trim() ?? "";
            itemsValues.Items.Clear();

            // Filter and build items using the same display/tag rules as the initial load
            var candidates = string.IsNullOrEmpty(search) ? _allValues : _allValues;

            foreach (var v in candidates)
            {
                var raw = v ?? string.Empty;
                var display = raw;
                var tag = raw;

                // Percent formatting
                if (!string.IsNullOrEmpty(_columnName) && (_columnName.Equals("PercentEntry", StringComparison.OrdinalIgnoreCase) || _columnName.Equals("PercentEntry_Display", StringComparison.OrdinalIgnoreCase)))
                {
                    if (string.IsNullOrEmpty(raw))
                    {
                        display = "(blank)";
                    }
                    else if (double.TryParse(raw, out var dbl))
                    {
                        display = (dbl * 100.0).ToString("N2") + "%";
                        tag = dbl.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                // AssignedTo: show 'Unassigned' when DB value empty
                if (!string.IsNullOrEmpty(_columnName) && _columnName.Equals("AssignedTo", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(raw))
                    {
                        display = "Unassigned";
                    }
                }

                // Apply search filter against both display and raw
                if (!string.IsNullOrEmpty(search))
                {
                    if (display.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 && raw.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                var cb = CreateThemedCheckBox(tag, true);
                cb.Content = display;
                itemsValues.Items.Add(cb);
            }
        }

        private void ChkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var obj in itemsValues.Items)
            {
                if (obj is CheckBox cb)
                    cb.IsChecked = true;
            }
        }

        private void ChkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var obj in itemsValues.Items)
            {
                if (obj is CheckBox cb)
                    cb.IsChecked = false;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // Gather selected values
            var selected = new List<string>();
            foreach (var obj in itemsValues.Items)
            {
                if (obj is CheckBox cb)
                {
                    if (cb.IsChecked == true)
                    {
                        var real = cb.Tag?.ToString() ?? string.Empty;
                        if (string.IsNullOrEmpty(real))
                        {
                            selected.Add("__BLANK__");
                        }
                        else
                        {
                            selected.Add(real);
                        }
                    }
                }
            }

            if (!selected.Any())
            {
                FilterCleared?.Invoke(this, EventArgs.Empty);
                return;
            }

            FilterApplied?.Invoke(this, new FilterEventArgs
            {
                FilterType = "List",
                FilterValue = string.Join("||", selected)
            });
        }

        private void BtnClearFrom_Click(object sender, RoutedEventArgs e)
        {
            // Clear filter for this column
            FilterCleared?.Invoke(this, EventArgs.Empty);
        }

        private void BtnTextFilters_Click(object sender, RoutedEventArgs e)
        {
            // Open a simple TextFilterPopup (reuse existing original control behavior)
            var tf = new TextFilterPopup();
            tf.Initialize(_columnName);
            tf.FilterApplied += (s, ev) =>
            {
                FilterApplied?.Invoke(this, new FilterEventArgs
                {
                    FilterType = ev.FilterType,
                    FilterValue = ev.FilterValue
                });
            };
            // Optional: wrap in a ScrollViewer so very tall content scrolls instead of clipping.
            var scroller = new ScrollViewer
            {
                Content = tf,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = SystemParameters.WorkArea.Height * 0.9  // cap at 90% of screen height
            };

            var w = new Window
            {
                Content = scroller,                // <— not tf directly
                SizeToContent = SizeToContent.WidthAndHeight,
                MinWidth = 300,                    // keep your desired width
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Owner = Application.Current.MainWindow,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            w.ShowDialog();
        }
    }

    public class FilterEventArgs : EventArgs
    {
        public string FilterType { get; set; }
        public string FilterValue { get; set; }
    }
}