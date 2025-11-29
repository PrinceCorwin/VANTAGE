using System.Windows;
using System.Linq;
using VANTAGE.Models;
using VANTAGE;

namespace MILESTONE.Views
{
    public partial class FindReplaceDialog : Syncfusion.Windows.Shared.ChromelessWindow
    {
        private Syncfusion.UI.Xaml.Grid.SfDataGrid _dataGrid = null!;
        private string _columnMappingName = null!;

        public FindReplaceDialog()
        {
            InitializeComponent();
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

            try
            {
                var allActivities = _dataGrid.View.Records.Select(r => r.Data).Cast<Activity>().ToList();

                var editableActivities = allActivities.Where(a =>
                    App.CurrentUser!.IsAdmin ||
                    string.Equals(a.AssignedTo, App.CurrentUser?.Username, System.StringComparison.OrdinalIgnoreCase)
                ).ToList();

                var provider = _dataGrid.View.GetPropertyAccessProvider();

                // Get the column to check its data type
                var column = _dataGrid.Columns.FirstOrDefault(c => c.MappingName == _columnMappingName);
                if (column == null)
                {
                    MessageBox.Show("Column not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int matchCount = 0;
                int replaceCount = 0;

                foreach (var activity in editableActivities)
                {
                    var currentValue = provider.GetValue(activity, _columnMappingName);

                    // Skip null values
                    if (currentValue == null)
                        continue;

                    string currentText = currentValue.ToString() ?? string.Empty;

                    bool isMatch = false;
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

                    if (isMatch)
                    {
                        matchCount++;

                        string newTextValue;
                        if (wholeCell)
                        {
                            newTextValue = replaceText;
                        }
                        else
                        {
                            newTextValue = matchCase
                                ? currentText.Replace(findText, replaceText)
                                : System.Text.RegularExpressions.Regex.Replace(
                                    currentText,
                                    System.Text.RegularExpressions.Regex.Escape(findText),
                                    replaceText,
                                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }

                        try
                        {
                            object? newValue = ConvertToPropertyType(newTextValue, currentValue.GetType());
                            provider.SetValue(activity, _columnMappingName, newValue);

                            // Mark as dirty and update metadata
                            activity.LocalDirty = 1;
                            activity.UpdatedBy = VANTAGE.App.CurrentUser?.Username ?? "Unknown";
                            activity.UpdatedUtcDate = DateTime.UtcNow;

                            // Save to local database (NOT syncing to Central)
                            await VANTAGE.Data.ActivityRepository.UpdateActivityInDatabase(activity);

                            replaceCount++;
                        }
                        catch (System.Exception conversionEx)
                        {
                            // Skip this value if conversion fails
                            System.Diagnostics.Debug.WriteLine($"Failed to convert '{newTextValue}' to {currentValue.GetType()}: {conversionEx.Message}");
                        }
                    }
                }

                _dataGrid.View.Refresh();

                int skippedCount = allActivities.Count - editableActivities.Count;
                string message = $"Replaced {replaceCount} occurrence(s) in column '{_columnMappingName}'.";

                if (skippedCount > 0)
                {
                    message += $"\n\n{skippedCount} record(s) skipped (not owned by you).";
                }

                if (matchCount > replaceCount)
                {
                    message += $"\n\n{matchCount - replaceCount} match(es) skipped (conversion failed).";
                }

                MessageBox.Show(message, "Replace Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                this.DialogResult = true;
                this.Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error during replace: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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