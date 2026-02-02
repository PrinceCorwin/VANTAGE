using System.Collections.Generic;
using System.Windows;
using System.Linq;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Utilities;
using VANTAGE;

namespace VANTAGE.Dialogs
{
    public partial class FindReplaceDialog : Syncfusion.Windows.Shared.ChromelessWindow
    {
        private Syncfusion.UI.Xaml.Grid.SfDataGrid _dataGrid = null!;
        private string _columnMappingName = null!;

        public FindReplaceDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
        }
        
        public void SetTargetColumn(Syncfusion.UI.Xaml.Grid.SfDataGrid dataGrid, string columnMappingName, string columnHeaderText)
        {
            _dataGrid = dataGrid;
            _columnMappingName = columnMappingName;
            txtColumnName.Text = $"Column: {columnHeaderText}";
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

            // Show busy dialog
            var busyDialog = new BusyDialog(this, "Finding matches...");
            busyDialog.Show();
            btnReplaceAll.IsEnabled = false;

            try
            {
                var allActivities = _dataGrid.View.Records.Select(r => r.Data).Cast<Activity>().ToList();

                var editableActivities = allActivities.Where(a =>
                    App.CurrentUser!.IsAdmin ||
                    string.Equals(a.AssignedTo, App.CurrentUser?.Username, System.StringComparison.OrdinalIgnoreCase)
                ).ToList();

                var provider = _dataGrid.View.GetPropertyAccessProvider();

                var column = _dataGrid.Columns.FirstOrDefault(c => c.MappingName == _columnMappingName);
                if (column == null)
                {
                    busyDialog.Close();
                    MessageBox.Show("Column not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Phase A: Find matches and compute new values in memory
                int matchCount = 0;
                int conversionFailures = 0;
                var updates = new List<(string UniqueID, object? NewValue)>();
                var derivedColumns = new Dictionary<string, List<(string UniqueID, object? Value)>>();
                bool isProgressField = _columnMappingName is "PercentEntry" or "EarnQtyEntry" or "Quantity" or "BudgetMHs";
                string currentUser = App.CurrentUser?.Username ?? "Unknown";
                var now = DateTime.UtcNow;

                foreach (var activity in editableActivities)
                {
                    var currentValue = provider.GetValue(activity, _columnMappingName);
                    if (currentValue == null)
                        continue;

                    string currentText = currentValue.ToString() ?? string.Empty;

                    bool isMatch;
                    if (wholeCell)
                    {
                        isMatch = matchCase
                            ? currentText == findText
                            : currentText.Equals(findText, System.StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        isMatch = matchCase
                            ? currentText.Contains(findText)
                            : currentText.IndexOf(findText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    }

                    if (!isMatch) continue;

                    matchCount++;

                    string newTextValue = wholeCell
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
                        object? newValue = ConvertToPropertyType(newTextValue, currentValue.GetType());

                        // Update in-memory object
                        provider.SetValue(activity, _columnMappingName, newValue);

                        if (isProgressField)
                        {
                            activity.RecalculateDerivedFields(_columnMappingName);

                            // Collect derived field values for DB update
                            CollectDerivedValue(derivedColumns, "EarnMHsCalc", activity.UniqueID, activity.EarnMHsCalc);
                            CollectDerivedValue(derivedColumns, "EarnQtyEntry", activity.UniqueID, activity.EarnQtyEntry);
                            CollectDerivedValue(derivedColumns, "PercentEntry", activity.UniqueID, activity.PercentEntry);
                        }

                        activity.LocalDirty = 1;
                        activity.UpdatedBy = currentUser;
                        activity.UpdatedUtcDate = now;

                        updates.Add((activity.UniqueID, newValue));
                    }
                    catch
                    {
                        conversionFailures++;
                    }
                }

                if (updates.Count == 0)
                {
                    busyDialog.Close();
                    string noMatchMsg = matchCount == 0
                        ? "No matches found."
                        : $"Found {matchCount} match(es) but all failed type conversion.";
                    MessageBox.Show(noMatchMsg, "Replace Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Phase B: Batch write to database in single transaction
                busyDialog.UpdateStatus($"Saving {updates.Count:N0} changes...");

                // Only include derived columns that differ from the target column
                var filteredDerived = derivedColumns
                    .Where(kv => kv.Key != _columnMappingName)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                int replaceCount = await ActivityRepository.BulkUpdateColumnAsync(
                    _columnMappingName,
                    updates,
                    currentUser,
                    filteredDerived.Count > 0 ? filteredDerived : null);

                // Phase C: Refresh grid
                _dataGrid.View.Refresh();

                busyDialog.Close();

                int skippedCount = allActivities.Count - editableActivities.Count;
                string message = $"Replaced {replaceCount:N0} occurrence(s) in column '{_columnMappingName}'.";

                if (skippedCount > 0)
                    message += $"\n\n{skippedCount:N0} record(s) skipped (not owned by you).";

                if (conversionFailures > 0)
                    message += $"\n\n{conversionFailures:N0} match(es) skipped (conversion failed).";

                MessageBox.Show(message, "Replace Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                this.DialogResult = true;
                this.Close();
            }
            catch (System.Exception ex)
            {
                busyDialog.Close();
                MessageBox.Show($"Error during replace: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnReplaceAll.IsEnabled = true;
            }
        }

        // Collects a derived field value into the lookup dictionary
        private static void CollectDerivedValue(
            Dictionary<string, List<(string UniqueID, object? Value)>> dict,
            string columnName, string uniqueId, object? value)
        {
            if (!dict.ContainsKey(columnName))
                dict[columnName] = new List<(string UniqueID, object? Value)>();
            dict[columnName].Add((uniqueId, value));
        }

        // Helper method to convert string back to the original property type
        private object? ConvertToPropertyType(string value, System.Type targetType)
        {
            if (targetType == typeof(string))
                return value;

            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                if (string.IsNullOrWhiteSpace(value) && targetType == typeof(int?))
                    return null;
                return int.Parse(value);
            }

            if (targetType == typeof(double) || targetType == typeof(double?))
            {
                if (string.IsNullOrWhiteSpace(value) && targetType == typeof(double?))
                    return null;
                return double.Parse(value);
            }

            if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
            {
                if (string.IsNullOrWhiteSpace(value) && targetType == typeof(DateTime?))
                    return null;
                return DateTime.Parse(value);
            }

            // Add more types as needed
            return System.Convert.ChangeType(value, targetType);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}