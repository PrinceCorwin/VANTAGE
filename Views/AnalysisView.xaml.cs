using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Views
{
    public partial class AnalysisView : UserControl
    {
        // Priority fields to appear at the top of the Group By dropdown (in alphabetical order)
        private static readonly string[] PriorityFields = new[]
        {
            "AssignedTo", "CompType", "DwgNO", "PhaseCategory", "PhaseCode",
            "PjtSystem", "SchedActNO", "Service", "SubArea", "WorkPackage"
        };

        // All non-numeric text fields from Activities table (excluding system/internal fields)
        private static readonly string[] AllTextFields = new[]
        {
            "Area", "AssignedTo", "Aux1", "Aux2", "Aux3", "ChgOrdNO", "CompType",
            "Description", "DwgNO", "EqmtNO", "Estimator", "HtTrace", "InsulType",
            "LineNumber", "MtrlSpec", "Notes", "PaintCode", "PhaseCategory", "PhaseCode",
            "PipeGrade", "PjtSystem", "PjtSystemNo", "ProjectID", "RFINO", "RespParty",
            "RevNO", "SchedActNO", "SecondActno", "SecondDwgNO", "Service", "ShopField",
            "ShtNO", "SubArea", "SystemNO", "TagNO", "UDF1", "UDF10", "UDF11", "UDF12",
            "UDF13", "UDF14", "UDF15", "UDF16", "UDF17", "UDF2", "UDF20", "UDF3", "UDF4",
            "UDF5", "UDF6", "UDF8", "UDF9", "WorkPackage"
        };

        private bool _isInitializing = true;

        public AnalysisView()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += AnalysisView_Loaded;
            Unloaded += AnalysisView_Unloaded;
        }

        private void AnalysisView_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;

            try
            {
                PopulateGroupByDropdown();
                PopulateProjectsDropdown();
                RestoreSettings();
                RestoreGridLayout();
            }
            finally
            {
                _isInitializing = false;
            }

            LoadSummaryData();
        }

        private void AnalysisView_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            SaveGridLayout();
        }

        // Populate Group By dropdown with priority fields first, then others alphabetically
        private void PopulateGroupByDropdown()
        {
            var otherFields = AllTextFields.Except(PriorityFields).OrderBy(f => f);
            var allFields = PriorityFields.OrderBy(f => f).Concat(otherFields).ToList();
            cmbGroupBy.ItemsSource = allFields;
        }

        // Populate projects dropdown from local Activities table
        private void PopulateProjectsDropdown()
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT DISTINCT ProjectID FROM Activities WHERE ProjectID IS NOT NULL AND ProjectID != '' ORDER BY ProjectID";

                var projects = new List<string>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var projectId = reader.GetString(0);
                    if (!string.IsNullOrWhiteSpace(projectId))
                        projects.Add(projectId);
                }

                cmbProjects.ItemsSource = projects;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AnalysisView.PopulateProjectsDropdown");
            }
        }

        // Restore saved settings
        private void RestoreSettings()
        {
            // Group By field
            var savedGroupField = SettingsManager.GetAnalysisGroupField();
            if (cmbGroupBy.Items.Contains(savedGroupField))
                cmbGroupBy.SelectedItem = savedGroupField;
            else
                cmbGroupBy.SelectedItem = "PhaseCode";

            // Update grid column header to match selected field
            colGroupValue.HeaderText = cmbGroupBy.SelectedItem?.ToString() ?? "Group";

            // User filter
            var currentUserOnly = SettingsManager.GetAnalysisCurrentUserOnly();
            rbCurrentUser.IsChecked = currentUserOnly;
            rbAllUsers.IsChecked = !currentUserOnly;

            // Selected projects - use SelectedItems as IList
            var selectedItems = cmbProjects.SelectedItems as System.Collections.IList;
            if (selectedItems == null) return;

            var savedProjects = SettingsManager.GetAnalysisSelectedProjects();
            if (!string.IsNullOrEmpty(savedProjects))
            {
                var projectList = savedProjects.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var project in projectList)
                {
                    if (cmbProjects.ItemsSource is List<string> items && items.Contains(project))
                    {
                        selectedItems.Add(project);
                    }
                }
            }
            else
            {
                // Default: select all projects
                if (cmbProjects.ItemsSource is List<string> items)
                {
                    foreach (var item in items)
                        selectedItems.Add(item);
                }
            }
        }

        // Save current settings
        private void SaveSettings()
        {
            SettingsManager.SetAnalysisGroupField(cmbGroupBy.SelectedItem?.ToString() ?? "PhaseCode");
            SettingsManager.SetAnalysisCurrentUserOnly(rbCurrentUser.IsChecked == true);

            var selectedProjects = cmbProjects.SelectedItems?.Cast<string>().ToList() ?? new List<string>();
            SettingsManager.SetAnalysisSelectedProjects(string.Join(",", selectedProjects));
        }

        // Save GridSplitter positions
        private void SaveGridLayout()
        {
            try
            {
                var layout = new
                {
                    Col0 = col0.Width.Value,
                    Col1 = col1.Width.Value,
                    Col2 = col2.Width.Value,
                    Col3 = col3.Width.Value,
                    Row0 = row0.Height.Value,
                    Row1 = row1.Height.Value
                };
                SettingsManager.SetAnalysisGridLayout(JsonSerializer.Serialize(layout));
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AnalysisView.SaveGridLayout");
            }
        }

        // Restore GridSplitter positions
        private void RestoreGridLayout()
        {
            try
            {
                var json = SettingsManager.GetAnalysisGridLayout();
                if (string.IsNullOrEmpty(json)) return;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("Col0", out var c0)) col0.Width = new GridLength(c0.GetDouble(), GridUnitType.Star);
                if (root.TryGetProperty("Col1", out var c1)) col1.Width = new GridLength(c1.GetDouble(), GridUnitType.Star);
                if (root.TryGetProperty("Col2", out var c2)) col2.Width = new GridLength(c2.GetDouble(), GridUnitType.Star);
                if (root.TryGetProperty("Col3", out var c3)) col3.Width = new GridLength(c3.GetDouble(), GridUnitType.Star);
                if (root.TryGetProperty("Row0", out var r0)) row0.Height = new GridLength(r0.GetDouble(), GridUnitType.Star);
                if (root.TryGetProperty("Row1", out var r1)) row1.Height = new GridLength(r1.GetDouble(), GridUnitType.Star);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AnalysisView.RestoreGridLayout");
            }
        }

        // Load summary data grouped by selected field
        private void LoadSummaryData()
        {
            if (_isInitializing) return;

            var groupField = cmbGroupBy.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(groupField)) return;

            var currentUserOnly = rbCurrentUser.IsChecked == true;
            var selectedProjects = cmbProjects.SelectedItems?.Cast<string>().ToList() ?? new List<string>();

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var cmd = connection.CreateCommand();

                // Build WHERE clause
                var whereClauses = new List<string>();
                var paramIndex = 0;

                if (currentUserOnly && App.CurrentUser != null)
                {
                    whereClauses.Add("AssignedTo = @user");
                    cmd.Parameters.AddWithValue("@user", App.CurrentUser.Username);
                }

                if (selectedProjects.Count > 0)
                {
                    var projectParams = selectedProjects.Select((p, i) =>
                    {
                        var paramName = $"@proj{paramIndex++}";
                        cmd.Parameters.AddWithValue(paramName, p);
                        return paramName;
                    });
                    whereClauses.Add($"ProjectID IN ({string.Join(",", projectParams)})");
                }

                var whereSQL = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

                // EarnMHsCalc is calculated: CASE WHEN PercentEntry >= 100 THEN BudgetMHs ELSE PercentEntry / 100.0 * BudgetMHs END
                cmd.CommandText = $@"
                    SELECT [{groupField}],
                           COALESCE(SUM(BudgetMHs), 0) as TotalBudgetMHs,
                           COALESCE(SUM(CASE WHEN PercentEntry >= 100 THEN BudgetMHs ELSE PercentEntry / 100.0 * BudgetMHs END), 0) as TotalEarnedMHs,
                           COALESCE(SUM(Quantity), 0) as TotalQuantity,
                           COALESCE(SUM(EarnQtyEntry), 0) as TotalQtyEarned
                    FROM Activities
                    {whereSQL}
                    GROUP BY [{groupField}]
                    ORDER BY [{groupField}]";

                var rows = new List<AnalysisSummaryRow>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var budgetMHs = reader.GetDouble(1);
                    var earnedMHs = reader.GetDouble(2);

                    // Calculate weighted percent complete (avoid division by zero)
                    var percentComplete = budgetMHs > 0 ? (earnedMHs / budgetMHs) * 100.0 : 0.0;

                    rows.Add(new AnalysisSummaryRow
                    {
                        GroupValue = reader.IsDBNull(0) ? "(blank)" : reader.GetString(0),
                        BudgetMHs = NumericHelper.RoundToPlaces(budgetMHs),
                        EarnedMHs = NumericHelper.RoundToPlaces(earnedMHs),
                        Quantity = NumericHelper.RoundToPlaces(reader.GetDouble(3)),
                        QtyEarned = NumericHelper.RoundToPlaces(reader.GetDouble(4)),
                        PercentComplete = NumericHelper.RoundToPlaces(percentComplete)
                    });
                }

                summaryGrid.ItemsSource = rows;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AnalysisView.LoadSummaryData");
                MessageBox.Show($"Error loading summary data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Event handlers
        private void CmbGroupBy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            // Update grid column header to match selected field
            colGroupValue.HeaderText = cmbGroupBy.SelectedItem?.ToString() ?? "Group";
            LoadSummaryData();
        }

        private void UserFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            LoadSummaryData();
        }

        private void CmbProjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            LoadSummaryData();
        }
    }
}
