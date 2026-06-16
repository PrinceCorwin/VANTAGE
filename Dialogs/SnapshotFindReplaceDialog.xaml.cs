using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Grid;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Snapshot-specific find/replace. Mirrors FindReplaceDialog's UX but operates purely
    // in memory on the SnapshotData rows loaded into ModifySnapshotDialog's grid — no DB
    // writes here, no ownership gating, no derived-field math. The caller (ModifySnapshotDialog)
    // marks the touched UniqueIDs as dirty so its existing Save path validates and pushes
    // the edits to Azure VMS_ProgressSnapshots.
    public partial class SnapshotFindReplaceDialog : Syncfusion.Windows.Shared.ChromelessWindow
    {
        private SfDataGrid _dataGrid = null!;
        private string _columnMappingName = null!;

        // UniqueIDs of rows whose target-column value changed. Caller reads this after
        // ShowDialog() to push them into ModifySnapshotDialog's dirty set.
        public HashSet<string> ChangedUniqueIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public SnapshotFindReplaceDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
        }

        public void SetTargetColumn(SfDataGrid dataGrid, string columnMappingName, string columnHeaderText)
        {
            _dataGrid = dataGrid;
            _columnMappingName = columnMappingName;
            txtColumnName.Text = $"Column: {columnHeaderText}";
        }

        private void ChkReplaceAllCells_Changed(object sender, RoutedEventArgs e)
        {
            bool replaceAll = chkReplaceAllCells.IsChecked == true;
            if (replaceAll && chkFindBlanks.IsChecked == true)
                chkFindBlanks.IsChecked = false;
            UpdateFindControlsState();
        }

        private void ChkFindBlanks_Changed(object sender, RoutedEventArgs e)
        {
            bool findBlanks = chkFindBlanks.IsChecked == true;
            if (findBlanks && chkReplaceAllCells.IsChecked == true)
                chkReplaceAllCells.IsChecked = false;
            UpdateFindControlsState();
        }

        private void UpdateFindControlsState()
        {
            bool replaceAll = chkReplaceAllCells.IsChecked == true;
            bool findBlanks = chkFindBlanks.IsChecked == true;
            bool disableFindControls = replaceAll || findBlanks;

            txtFind.IsEnabled = !disableFindControls;
            lblFindWhat.Opacity = disableFindControls ? 0.5 : 1.0;
            chkMatchCase.IsEnabled = !disableFindControls;
            chkWholeCell.IsEnabled = !disableFindControls;
            btnCount.IsEnabled = !replaceAll;

            if (disableFindControls)
                txtFind.Text = string.Empty;
        }

        private void BtnCount_Click(object sender, RoutedEventArgs e)
        {
            bool findBlanks = chkFindBlanks.IsChecked == true;

            if (!findBlanks && string.IsNullOrEmpty(txtFind.Text))
            {
                AppMessageBox.Show("Please enter text to find, or check 'Find blanks' to find empty cells.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_dataGrid == null || string.IsNullOrEmpty(_columnMappingName))
            {
                AppMessageBox.Show("No column selected.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string findText = txtFind.Text;
            bool matchCase = chkMatchCase.IsChecked == true;
            bool wholeCell = chkWholeCell.IsChecked == true;

            var rows = GetVisibleRows();
            int matchCount = 0;
            foreach (var row in rows)
            {
                var currentValue = GetPropertyValue(row, _columnMappingName);
                string currentText = FormatValueForComparison(currentValue);

                if (IsMatch(findText, findBlanks, matchCase, wholeCell, currentValue, currentText))
                    matchCount++;
            }

            string searchDesc = findBlanks ? "blank cells" : $"'{findText}'";
            AppMessageBox.Show(
                $"Found {matchCount:N0} match(es) for {searchDesc} in column '{_columnMappingName}'.",
                "Find Results", MessageBoxButton.OK, MessageBoxImage.None);
        }

        private void BtnReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            bool replaceAllCells = chkReplaceAllCells.IsChecked == true;
            bool findBlanks = chkFindBlanks.IsChecked == true;

            if (!replaceAllCells && !findBlanks && string.IsNullOrEmpty(txtFind.Text))
            {
                AppMessageBox.Show("Please enter text to find, check 'Find blanks', or check 'Replace ALL cells'.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_dataGrid == null || string.IsNullOrEmpty(_columnMappingName))
            {
                AppMessageBox.Show("No column selected.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Verify the SnapshotData property exists and is editable per the central rule.
            var prop = typeof(SnapshotData).GetProperty(_columnMappingName);
            if (prop == null || !prop.CanWrite || !SnapshotEditableColumns.IsEditable(_columnMappingName))
            {
                AppMessageBox.Show("This column is not editable.", "Not Editable",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (replaceAllCells)
            {
                var confirm = AppMessageBox.Show(
                    $"This will replace ALL visible values in column '{_columnMappingName}' with '{txtReplace.Text}'.\n\n" +
                    "Edits stay in memory until you click Save. Continue?",
                    "Confirm Replace All", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;
            }

            string findText = txtFind.Text;
            string replaceText = txtReplace.Text ?? string.Empty;
            bool matchCase = chkMatchCase.IsChecked == true;
            bool wholeCell = chkWholeCell.IsChecked == true;

            btnReplaceAll.IsEnabled = false;
            try
            {
                var rows = GetVisibleRows();
                int matchCount = 0;
                int changed = 0;
                int conversionFailures = 0;

                foreach (var row in rows)
                {
                    var currentValue = prop.GetValue(row);
                    string currentText = FormatValueForComparison(currentValue);

                    if (!IsMatch(findText, findBlanks, matchCase, wholeCell, currentValue, currentText))
                        continue;

                    matchCount++;

                    string newTextValue = findBlanks || wholeCell || replaceAllCells
                        ? replaceText
                        : matchCase
                            ? currentText.Replace(findText, replaceText)
                            : System.Text.RegularExpressions.Regex.Replace(
                                currentText,
                                System.Text.RegularExpressions.Regex.Escape(findText),
                                replaceText,
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    try
                    {
                        object? newValue = ConvertToPropertyType(newTextValue, prop.PropertyType);

                        // Skip rows where the value isn't actually changing — avoids
                        // marking unchanged rows dirty and bloating the save set.
                        if (Equals(currentValue, newValue))
                            continue;

                        prop.SetValue(row, newValue);
                        ChangedUniqueIds.Add(row.UniqueID);
                        changed++;
                    }
                    catch
                    {
                        conversionFailures++;
                    }
                }

                // SnapshotData has no INotifyPropertyChanged — refresh the view so the
                // grid repaints the mutated cells.
                _dataGrid.View?.Refresh();

                string message = $"Replaced {changed:N0} cell(s) in column '{_columnMappingName}'.";
                if (matchCount > changed + conversionFailures)
                    message += $"\n\n{matchCount - changed - conversionFailures:N0} match(es) skipped (value unchanged).";
                if (conversionFailures > 0)
                    message += $"\n\n{conversionFailures:N0} match(es) skipped (conversion failed).";
                message += "\n\nEdits are in memory only — click Save in the Modify dialog to commit.";

                AppMessageBox.Show(message, "Replace Complete", MessageBoxButton.OK, MessageBoxImage.None);

                DialogResult = changed > 0;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SnapshotFindReplaceDialog.BtnReplaceAll_Click");
                AppMessageBox.Show($"Error during replace: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnReplaceAll.IsEnabled = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = ChangedUniqueIds.Count > 0;
            Close();
        }

        // Honors active grid filters by iterating the view's records, not the underlying source.
        private List<SnapshotData> GetVisibleRows()
        {
            var list = new List<SnapshotData>();
            if (_dataGrid?.View == null) return list;

            foreach (var record in _dataGrid.View.Records)
            {
                if (record.Data is SnapshotData snap)
                    list.Add(snap);
            }
            return list;
        }

        private static object? GetPropertyValue(SnapshotData row, string propertyName)
        {
            var prop = typeof(SnapshotData).GetProperty(propertyName);
            return prop?.GetValue(row);
        }

        private static bool IsMatch(string findText, bool findBlanks, bool matchCase, bool wholeCell,
            object? currentValue, string currentText)
        {
            if (findBlanks)
                return currentValue == null || string.IsNullOrWhiteSpace(currentText);

            if (currentValue == null)
                return false;

            if (wholeCell)
            {
                return matchCase
                    ? currentText == findText
                    : currentText.Equals(findText, StringComparison.OrdinalIgnoreCase);
            }
            return matchCase
                ? currentText.Contains(findText)
                : currentText.IndexOf(findText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // SnapshotData properties are string, string?, double, or int. Conversions match
        // those types only — anything else falls through to Convert.ChangeType.
        private static object? ConvertToPropertyType(string value, Type targetType)
        {
            if (targetType == typeof(string))
                return value;

            // string? — empty input becomes null to match the canonical "no value" state
            // used elsewhere in the snapshot path (e.g. ActStart/ActFin).
            if (targetType == typeof(string) && Nullable.GetUnderlyingType(targetType) == null)
                return value;

            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                if (string.IsNullOrWhiteSpace(value))
                    return targetType == typeof(int?) ? (object?)null : 0;
                return int.Parse(value);
            }

            if (targetType == typeof(double) || targetType == typeof(double?))
            {
                if (string.IsNullOrWhiteSpace(value))
                    return targetType == typeof(double?) ? (object?)null : 0.0;
                return double.Parse(value);
            }

            return Convert.ChangeType(value, targetType);
        }

        private static string FormatValueForComparison(object? value)
        {
            if (value == null) return string.Empty;
            return value.ToString() ?? string.Empty;
        }
    }
}
