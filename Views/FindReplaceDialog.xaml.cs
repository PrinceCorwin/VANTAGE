using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VANTAGE.Models;
using VANTAGE;
using VANTAGE.Data;
using VANTAGE.Repositories;
using VANTAGE.Utilities;

namespace MILESTONE.Views
{
    public partial class FindReplaceDialog : Syncfusion.Windows.Shared.ChromelessWindow
    {
        private Syncfusion.UI.Xaml.Grid.SfDataGrid _dataGrid = null!;
        private string _columnMappingName = null!;
        private DataGridMode _mode = DataGridMode.Activity;

        // Enum to track which type of grid we're working with
        private enum DataGridMode
        {
            Activity,
            ProgressSnapshot
        }

        // Track if any replacements were made (for caller to trigger refresh)
        public bool ReplacementsMade { get; private set; } = false;

        public FindReplaceDialog()
        {
            InitializeComponent();
        }

        public void SetTargetColumn(Syncfusion.UI.Xaml.Grid.SfDataGrid dataGrid, string columnMappingName, string columnHeaderText)
        {
            _dataGrid = dataGrid;
            _columnMappingName = columnMappingName;
            txtColumnName.Text = $"Column: {columnHeaderText}";

            // Detect mode based on first record type
            DetectGridMode();
        }

        private void DetectGridMode()
        {
            if (_dataGrid?.View?.Records == null || !_dataGrid.View.Records.Any())
            {
                _mode = DataGridMode.Activity; // Default
                return;
            }

            var firstRecord = _dataGrid.View.Records.FirstOrDefault()?.Data;
            if (firstRecord is ProgressSnapshot)
            {
                _mode = DataGridMode.ProgressSnapshot;
            }
            else
            {
                _mode = DataGridMode.Activity;
            }
        }

        private async void BtnReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtFind.Text))
            {
                MessageBox.Show("Please enter text to find.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_dataGrid == null || string.IsNullOrEmpty(_columnMappingName))
            {
                MessageBox.Show("No column selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string findText = txtFind.Text;
            string replaceText = txtReplace.Text ?? string.Empty;
            bool matchCase = chkMatchCase.IsChecked == true;
            bool wholeCell = chkWholeCell.IsChecked == true;

            try
            {
                // Branch based on mode
                if (_mode == DataGridMode.ProgressSnapshot)
                {
                    await ReplaceInProgressSnapshots(findText, replaceText, matchCase, wholeCell);
                }
                else
                {
                    await ReplaceInActivities(findText, replaceText, matchCase, wholeCell);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FindReplaceDialog.BtnReplaceAll_Click");
                MessageBox.Show($"Error during replace: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task ReplaceInActivities(string findText, string replaceText, bool matchCase, bool wholeCell)
        {
            // Get all records from the grid
            var allRecords = _dataGrid.View.Records.Select(r => r.Data).ToList();

            // Filter to Activity type
            var allActivities = new List<Activity>();
            foreach (var record in allRecords)
            {
                if (record is Activity activity)
                {
                    allActivities.Add(activity);
                }
            }

            if (allActivities.Count == 0)
            {
                MessageBox.Show("No records found in grid.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var editableActivities = allActivities.Where(a =>
                App.CurrentUser!.IsAdmin ||
                string.Equals(a.AssignedTo, App.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            var column = _dataGrid.Columns.FirstOrDefault(c => c.MappingName == _columnMappingName);
            if (column == null)
            {
                MessageBox.Show("Column not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int matchCount = 0;
            int replaceCount = 0;

            // Get property info for the column
            var propertyInfo = typeof(Activity).GetProperty(_columnMappingName);
            if (propertyInfo == null)
            {
                MessageBox.Show($"Property '{_columnMappingName}' not found on Activity.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (var activity in editableActivities)
            {
                var currentValue = propertyInfo.GetValue(activity);

                if (currentValue == null)
                    continue;

                string currentText = currentValue.ToString() ?? string.Empty;
                Type valueType = currentValue.GetType();

                bool isMatch = CheckMatch(currentText, findText, matchCase, wholeCell, currentValue);

                if (isMatch)
                {
                    matchCount++;

                    string newTextValue = GetReplacementText(currentText, findText, replaceText, matchCase, wholeCell, currentValue);

                    try
                    {
                        object? newValue = ConvertToPropertyType(newTextValue, valueType);
                        propertyInfo.SetValue(activity, newValue);

                        activity.LocalDirty = 1;
                        activity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
                        activity.UpdatedUtcDate = DateTime.UtcNow;

                        await ActivityRepository.UpdateActivityInDatabase(activity);

                        replaceCount++;
                    }
                    catch (Exception conversionEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to convert '{newTextValue}' to {valueType}: {conversionEx.Message}");
                    }
                }
            }

            _dataGrid.View.Refresh();
            ReplacementsMade = replaceCount > 0;

            int skippedCount = allActivities.Count - editableActivities.Count;
            ShowResultMessage(replaceCount, matchCount, skippedCount);
        }

        private async System.Threading.Tasks.Task ReplaceInProgressSnapshots(string findText, string replaceText, bool matchCase, bool wholeCell)
        {
            var allRecords = _dataGrid.View.Records.Select(r => r.Data).ToList();

            var allSnapshots = new List<ProgressSnapshot>();
            foreach (var record in allRecords)
            {
                if (record is ProgressSnapshot snapshot)
                {
                    allSnapshots.Add(snapshot);
                }
            }

            if (allSnapshots.Count == 0)
            {
                MessageBox.Show("No records found in grid.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var editableSnapshots = allSnapshots.Where(s =>
                App.CurrentUser!.IsAdmin ||
                string.Equals(s.AssignedTo, App.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            var column = _dataGrid.Columns.FirstOrDefault(c => c.MappingName == _columnMappingName);
            if (column == null)
            {
                MessageBox.Show("Column not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int matchCount = 0;
            int replaceCount = 0;
            string username = App.CurrentUser?.Username ?? "Unknown";

            var propertyInfo = typeof(ProgressSnapshot).GetProperty(_columnMappingName);
            if (propertyInfo == null)
            {
                MessageBox.Show($"Property '{_columnMappingName}' not found on ProgressSnapshot.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (var snapshot in editableSnapshots)
            {
                var currentValue = propertyInfo.GetValue(snapshot);

                if (currentValue == null)
                    continue;

                string currentText = currentValue.ToString() ?? string.Empty;
                Type valueType = currentValue.GetType();

                bool isMatch = CheckMatch(currentText, findText, matchCase, wholeCell, currentValue);

                if (isMatch)
                {
                    matchCount++;

                    string newTextValue = GetReplacementText(currentText, findText, replaceText, matchCase, wholeCell, currentValue);

                    try
                    {
                        object? newValue = ConvertToPropertyType(newTextValue, valueType);
                        propertyInfo.SetValue(snapshot, newValue);

                        snapshot.UpdatedBy = username;
                        snapshot.UpdatedUtcDate = DateTime.UtcNow;

                        bool success = await ScheduleRepository.UpdateSnapshotAsync(snapshot, username);

                        if (success)
                        {
                            replaceCount++;
                        }
                    }
                    catch (Exception conversionEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to convert '{newTextValue}' to {valueType}: {conversionEx.Message}");
                    }
                }
            }

            _dataGrid.View.Refresh();
            ReplacementsMade = replaceCount > 0;

            int skippedCount = allSnapshots.Count - editableSnapshots.Count;
            ShowResultMessage(replaceCount, matchCount, skippedCount);

            if (replaceCount > 0)
            {
                AppLogger.Info($"Find-Replace in ProgressSnapshots: replaced {replaceCount} in column '{_columnMappingName}'",
                    "FindReplaceDialog.ReplaceInProgressSnapshots", username);
            }
        }

        private bool CheckMatch(string currentText, string findText, bool matchCase, bool wholeCell, object? originalValue)
        {
            // For numeric types, try numeric comparison first
            if (originalValue != null && IsNumericType(originalValue.GetType()))
            {
                if (double.TryParse(findText, out double findNumber) &&
                    double.TryParse(currentText, out double currentNumber))
                {
                    if (wholeCell)
                    {
                        // Compare as numbers with tolerance for floating point
                        return Math.Abs(findNumber - currentNumber) < 0.0001;
                    }
                    else
                    {
                        // For partial match on numbers, use formatted string comparison
                        // Format both to same precision for comparison
                        string formattedCurrent = currentNumber.ToString("G15");
                        string formattedFind = findNumber.ToString("G15");

                        return matchCase
                            ? formattedCurrent.Contains(formattedFind)
                            : formattedCurrent.IndexOf(formattedFind, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                }
            }

            // String comparison
            if (wholeCell)
            {
                return matchCase
                    ? currentText == findText
                    : currentText.Equals(findText, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return matchCase
                    ? currentText.Contains(findText)
                    : currentText.IndexOf(findText, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private string GetReplacementText(string currentText, string findText, string replaceText, bool matchCase, bool wholeCell, object? originalValue)
        {
            if (wholeCell)
            {
                return replaceText;
            }
            else
            {
                // For numeric partial replacement, this gets tricky - just do string replacement
                return matchCase
                    ? currentText.Replace(findText, replaceText)
                    : System.Text.RegularExpressions.Regex.Replace(
                        currentText,
                        System.Text.RegularExpressions.Regex.Escape(findText),
                        replaceText,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
        }

        private bool IsNumericType(Type type)
        {
            return type == typeof(double) || type == typeof(double?) ||
                   type == typeof(float) || type == typeof(float?) ||
                   type == typeof(decimal) || type == typeof(decimal?) ||
                   type == typeof(int) || type == typeof(int?) ||
                   type == typeof(long) || type == typeof(long?);
        }

        private void ShowResultMessage(int replaceCount, int matchCount, int skippedCount)
        {
            string message = $"Replaced {replaceCount} occurrence(s) in column '{_columnMappingName}'.";

            if (skippedCount > 0)
            {
                message += $"\n\n{skippedCount} record(s) skipped (not owned by you).";
            }

            if (matchCount > replaceCount)
            {
                message += $"\n\n{matchCount - replaceCount} match(es) skipped (conversion failed or save failed).";
            }

            MessageBox.Show(message, "Replace Complete", MessageBoxButton.OK, MessageBoxImage.Information);

            this.DialogResult = true;
            this.Close();
        }

        private object? ConvertToPropertyType(string value, Type targetType)
        {
            // Handle nullable types - get underlying type
            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            bool isNullable = Nullable.GetUnderlyingType(targetType) != null;

            if (string.IsNullOrWhiteSpace(value))
            {
                if (isNullable)
                    return null;
                if (underlyingType == typeof(string))
                    return string.Empty;
                throw new ArgumentException($"Cannot convert empty value to non-nullable {targetType.Name}");
            }

            if (underlyingType == typeof(string))
                return value;

            if (underlyingType == typeof(int))
                return int.Parse(value);

            if (underlyingType == typeof(long))
                return long.Parse(value);

            if (underlyingType == typeof(double))
                return double.Parse(value);

            if (underlyingType == typeof(float))
                return float.Parse(value);

            if (underlyingType == typeof(decimal))
                return decimal.Parse(value);

            if (underlyingType == typeof(DateTime))
                return DateTime.Parse(value);

            return Convert.ChangeType(value, underlyingType);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}