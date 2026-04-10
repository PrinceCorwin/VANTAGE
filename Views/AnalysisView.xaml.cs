using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClosedXML.Excel;
using Syncfusion.SfSkinManager;
using Syncfusion.UI.Xaml.Charts;
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
            "CreatedBy", "Description", "DwgNO", "EqmtNO", "EquivUOM", "Estimator",
            "HtTrace", "InsulType", "LineNumber", "MtrlSpec", "Notes", "PaintCode",
            "PhaseCategory", "PhaseCode", "PipeGrade", "PjtSystem", "PjtSystemNo",
            "ProjectID", "RFINO", "RespParty", "RevNO", "ROCStep", "SchedActNO",
            "SecondActno", "SecondDwgNO", "Service", "ShopField", "ShtNO", "SubArea",
            "TagNO", "UDF1", "UDF10", "UDF11", "UDF12", "UDF13", "UDF14", "UDF15",
            "UDF16", "UDF17", "UDF2", "UDF20", "UDF3", "UDF4", "UDF5", "UDF6",
            "UDF8", "UDF9", "UOM", "WorkPackage"
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
            ThemeManager.ThemeChanged += OnThemeChanged;
            _isInitializing = true;

            try
            {
                PopulateGroupByDropdown();
                PopulateProjectsDropdown();
                PopulateChartFilters();
                InitializeSection_1_1();
                RestoreSettings();
                RestoreGridLayout();
            }
            finally
            {
                _isInitializing = false;
            }

            LoadSummaryData();
            UpdateVisual_1_1();
        }

        private void AnalysisView_Unloaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            SaveSettings();
            SaveGridLayout();
        }

        // Re-apply Syncfusion skin to grid when theme changes
        private void OnThemeChanged(string themeName)
        {
            Dispatcher.Invoke(() =>
            {
                var sfTheme = new Theme(ThemeManager.GetSyncfusionThemeName());
                SfSkinManager.SetTheme(summaryGrid, sfTheme);
                UpdateVisual_1_1();
            });
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
                    // Top row columns
                    TopCol0 = topCol0.Width.Value,
                    TopCol1 = topCol1.Width.Value,
                    TopCol2 = topCol2.Width.Value,
                    TopCol3 = topCol3.Width.Value,
                    // Bottom row columns
                    BottomCol0 = bottomCol0.Width.Value,
                    BottomCol1 = bottomCol1.Width.Value,
                    BottomCol2 = bottomCol2.Width.Value,
                    // Row heights
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

                // Top row columns
                if (root.TryGetProperty("TopCol0", out var tc0)) topCol0.Width = new GridLength(tc0.GetDouble(), GridUnitType.Star);
                if (root.TryGetProperty("TopCol1", out var tc1)) topCol1.Width = new GridLength(tc1.GetDouble(), GridUnitType.Star);
                if (root.TryGetProperty("TopCol2", out var tc2)) topCol2.Width = new GridLength(tc2.GetDouble(), GridUnitType.Star);
                if (root.TryGetProperty("TopCol3", out var tc3)) topCol3.Width = new GridLength(tc3.GetDouble(), GridUnitType.Star);
                // Bottom row columns
                if (root.TryGetProperty("BottomCol0", out var bc0)) bottomCol0.Width = new GridLength(bc0.GetDouble(), GridUnitType.Star);
                if (root.TryGetProperty("BottomCol1", out var bc1)) bottomCol1.Width = new GridLength(bc1.GetDouble(), GridUnitType.Star);
                if (root.TryGetProperty("BottomCol2", out var bc2)) bottomCol2.Width = new GridLength(bc2.GetDouble(), GridUnitType.Star);
                // Row heights
                if (root.TryGetProperty("Row0", out var r0)) row0.Height = new GridLength(r0.GetDouble(), GridUnitType.Star);
                if (root.TryGetProperty("Row1", out var r1)) row1.Height = new GridLength(r1.GetDouble(), GridUnitType.Star);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AnalysisView.RestoreGridLayout");
            }
        }

        // Save layout when any splitter is dragged
        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            SaveGridLayout();
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
            UpdateVisual_1_1();
        }

        private void CmbProjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            LoadSummaryData();
            UpdateVisual_1_1();
        }

        // Chart filter fields and their corresponding ComboBoxAdv controls
        private static readonly string[] ChartFilterFields = new[]
        {
            "Area", "Aux1", "Aux2", "Aux3", "CompType", "DwgNO",
            "PhaseCategory", "PhaseCode", "ProjectID", "ROCStep", "SchedActNO", "WorkPackage"
        };

        // Populate all chart filter dropdowns with distinct values from Activities
        private void PopulateChartFilters()
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                foreach (var field in ChartFilterFields)
                {
                    var combo = GetChartFilterCombo(field);
                    if (combo == null) continue;

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = $"SELECT DISTINCT COALESCE([{field}], '') FROM Activities ORDER BY COALESCE([{field}], '')";

                    var values = new List<string>();
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        values.Add(reader.GetString(0));
                    }

                    // Ensure blank option exists and is labeled
                    if (values.Contains(""))
                    {
                        values.Remove("");
                        values.Insert(0, "(blank)");
                    }

                    combo.ItemsSource = values;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AnalysisView.PopulateChartFilters");
            }
        }

        // Map field name to its ComboBoxAdv control
        private Syncfusion.Windows.Tools.Controls.ComboBoxAdv? GetChartFilterCombo(string field)
        {
            return field switch
            {
                "Area" => cmbFilter_Area,
                "Aux1" => cmbFilter_Aux1,
                "Aux2" => cmbFilter_Aux2,
                "Aux3" => cmbFilter_Aux3,
                "CompType" => cmbFilter_CompType,
                "DwgNO" => cmbFilter_DwgNO,
                "PhaseCategory" => cmbFilter_PhaseCategory,
                "PhaseCode" => cmbFilter_PhaseCode,
                "ProjectID" => cmbFilter_ProjectID,
                "ROCStep" => cmbFilter_ROCStep,
                "SchedActNO" => cmbFilter_SchedActNO,
                "WorkPackage" => cmbFilter_WorkPackage,
                _ => null
            };
        }

        // Refresh all charts when any chart filter changes
        private void ChartFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            UpdateVisual_1_1();
        }

        // Build WHERE clauses from active chart filters
        private void AppendChartFilterClauses(List<string> whereClauses, Microsoft.Data.Sqlite.SqliteCommand cmd, ref int paramIndex)
        {
            foreach (var field in ChartFilterFields)
            {
                var combo = GetChartFilterCombo(field);
                if (combo?.SelectedItems == null) continue;

                var selected = combo.SelectedItems.Cast<string>().ToList();
                if (selected.Count == 0) continue;

                // Convert "(blank)" back to empty string for SQL
                bool includesBlank = selected.Remove("(blank)");

                var conditions = new List<string>();

                if (selected.Count > 0)
                {
                    var paramNames = new List<string>();
                    foreach (var val in selected)
                    {
                        var paramName = $"@cf{paramIndex++}";
                        cmd.Parameters.AddWithValue(paramName, val);
                        paramNames.Add(paramName);
                    }
                    conditions.Add($"[{field}] IN ({string.Join(",", paramNames)})");
                }

                if (includesBlank)
                {
                    conditions.Add($"([{field}] IS NULL OR [{field}] = '')");
                }

                whereClauses.Add($"({string.Join(" OR ", conditions)})");
            }
        }

        // Visual type options for section dropdowns
        private static readonly string[] VisualTypes = new[]
        {
            "Bar Chart", "Column Chart", "Doughnut Chart", "Line Chart", "Pie Chart"
        };

        // Numeric fields available for Y axis (alphabetical)
        private static readonly string[] DataFields = new[]
        {
            "% Complete", "BaseUnit", "BudgetHoursGroup", "BudgetHoursROC", "BudgetMHs",
            "ClientBudget", "ClientCustom3", "ClientEquivQty", "EarnedMHs", "EarnedMHsRoc",
            "EquivQTY", "PrevEarnMHs", "PrevEarnQTY", "Quantity", "QtyEarned", "ROCBudgetQTY"
        };

        // Populate section 1,1 dropdowns and restore saved selections
        private void InitializeSection_1_1()
        {
            cmbVisualType_1_1.ItemsSource = VisualTypes;
            cmbXAxis_1_1.ItemsSource = AllTextFields.OrderBy(f => f).ToArray();
            cmbDataField_1_1.ItemsSource = DataFields;

            var savedVisual = SettingsManager.GetUserSetting("AnalysisVisual_1_1", "Column Chart");
            var savedXAxis = SettingsManager.GetUserSetting("AnalysisXAxis_1_1", "PhaseCode");
            var savedField = SettingsManager.GetUserSetting("AnalysisField_1_1", "BudgetMHs");

            cmbVisualType_1_1.SelectedItem = VisualTypes.Contains(savedVisual) ? savedVisual : "Column Chart";
            cmbXAxis_1_1.SelectedItem = AllTextFields.Contains(savedXAxis) ? savedXAxis : "PhaseCode";
            cmbDataField_1_1.SelectedItem = DataFields.Contains(savedField) ? savedField : "BudgetMHs";
        }

        private void CmbVisualType_1_1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            SettingsManager.SetUserSetting("AnalysisVisual_1_1", cmbVisualType_1_1.SelectedItem?.ToString() ?? "Column Chart");
            UpdateVisual_1_1();
        }

        private void CmbXAxis_1_1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            SettingsManager.SetUserSetting("AnalysisXAxis_1_1", cmbXAxis_1_1.SelectedItem?.ToString() ?? "PhaseCode");
            UpdateVisual_1_1();
        }

        private void CmbDataField_1_1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            SettingsManager.SetUserSetting("AnalysisField_1_1", cmbDataField_1_1.SelectedItem?.ToString() ?? "BudgetMHs");
            UpdateVisual_1_1();
        }

        // Query data grouped by the chart's own X axis, then build the visual
        private void UpdateVisual_1_1()
        {
            var xAxisField = cmbXAxis_1_1.SelectedItem?.ToString();
            var dataField = cmbDataField_1_1.SelectedItem?.ToString() ?? "BudgetMHs";
            var visualType = cmbVisualType_1_1.SelectedItem?.ToString() ?? "Column Chart";

            if (string.IsNullOrEmpty(xAxisField)) return;

            var rows = QueryChartData(xAxisField, dataField);
            if (rows == null || rows.Count == 0)
            {
                visualHost_1_1.Content = null;
                return;
            }

            visualHost_1_1.Content = CreateChart(visualType, dataField, rows);
        }

        // Query the database for chart data, filtered by chart filters
        private List<ChartDataPoint>? QueryChartData(string groupField, string valueField)
        {
            // Map display names to database expressions
            string sqlExpr = valueField switch
            {
                "% Complete" => "CASE WHEN COALESCE(SUM(BudgetMHs), 0) > 0 THEN (COALESCE(SUM(CASE WHEN PercentEntry >= 100 THEN BudgetMHs ELSE PercentEntry / 100.0 * BudgetMHs END), 0) / SUM(BudgetMHs)) * 100.0 ELSE 0 END",
                "EarnedMHs" => "COALESCE(SUM(CASE WHEN PercentEntry >= 100 THEN BudgetMHs ELSE PercentEntry / 100.0 * BudgetMHs END), 0)",
                "QtyEarned" => "COALESCE(SUM(EarnQtyEntry), 0)",
                _ => $"COALESCE(SUM([{valueField}]), 0)"
            };

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var cmd = connection.CreateCommand();
                var whereClauses = new List<string>();
                int paramIndex = 0;

                // Apply chart filter selections
                AppendChartFilterClauses(whereClauses, cmd, ref paramIndex);

                var whereSQL = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

                cmd.CommandText = $@"
                    SELECT [{groupField}], {sqlExpr} as Value
                    FROM Activities
                    {whereSQL}
                    GROUP BY [{groupField}]
                    ORDER BY [{groupField}]";

                var rows = new List<ChartDataPoint>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    rows.Add(new ChartDataPoint
                    {
                        Label = reader.IsDBNull(0) ? "(blank)" : reader.GetString(0),
                        Value = NumericHelper.RoundToPlaces(reader.GetDouble(1))
                    });
                }

                return rows;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AnalysisView.QueryChartData");
                return null;
            }
        }

        // Factory: creates a Syncfusion chart
        private FrameworkElement CreateChart(string visualType, string dataField, List<ChartDataPoint> rows)
        {
            if (visualType == "Pie Chart" || visualType == "Doughnut Chart")
                return CreateCircularChart(visualType, rows);

            return CreateCartesianChart(visualType, dataField, rows);
        }

        // Column, Bar, Line charts
        private SfChart CreateCartesianChart(string visualType, string label, List<ChartDataPoint> rows)
        {
            var chart = new SfChart { Margin = new Thickness(4) };
            SfSkinManager.SetTheme(chart, new Theme(ThemeManager.GetSyncfusionThemeName()));

            var foreground = TryFindResource("ForegroundColor") as SolidColorBrush ?? new SolidColorBrush(Colors.White);

            var xAxis = new CategoryAxis
            {
                LabelPlacement = LabelPlacement.BetweenTicks,
                ShowGridLines = false,
                LabelsIntersectAction = AxisLabelsIntersectAction.Auto
            };
            xAxis.LabelStyle = new LabelStyle { Foreground = foreground, FontSize = 10 };
            chart.PrimaryAxis = xAxis;

            var yAxis = new NumericalAxis { ShowGridLines = true };
            yAxis.LabelStyle = new LabelStyle { Foreground = foreground, FontSize = 11 };
            chart.SecondaryAxis = yAxis;

            XyDataSeries series = visualType switch
            {
                "Bar Chart" => new BarSeries(),
                "Line Chart" => new LineSeries(),
                _ => new ColumnSeries()
            };

            series.ItemsSource = rows;
            series.XBindingPath = "Label";
            series.YBindingPath = "Value";
            series.Label = label;
            series.ShowTooltip = true;

            chart.Palette = ChartColorPalette.Metro;
            chart.Series.Add(series);

            return chart;
        }

        // Pie and Doughnut charts
        private SfChart CreateCircularChart(string visualType, List<ChartDataPoint> rows)
        {
            var chart = new SfChart { Margin = new Thickness(4) };
            SfSkinManager.SetTheme(chart, new Theme(ThemeManager.GetSyncfusionThemeName()));

            if (visualType == "Doughnut Chart")
            {
                var series = new DoughnutSeries
                {
                    ItemsSource = rows,
                    XBindingPath = "Label",
                    YBindingPath = "Value",
                    ShowTooltip = true
                };
                chart.Palette = ChartColorPalette.Metro;
                chart.Series.Add(series);
            }
            else
            {
                var series = new PieSeries
                {
                    ItemsSource = rows,
                    XBindingPath = "Label",
                    YBindingPath = "Value",
                    ShowTooltip = true
                };
                chart.Palette = ChartColorPalette.Metro;
                chart.Series.Add(series);
            }

            return chart;
        }

        // Export current filtered grid contents to Excel
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var rows = summaryGrid.ItemsSource as List<AnalysisSummaryRow>;
            if (rows == null || rows.Count == 0)
            {
                MessageBox.Show("No data to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Get visible (filtered) records from the grid view
            var visibleRows = summaryGrid.View?.Records?
                .Select(r => r.Data as AnalysisSummaryRow)
                .Where(r => r != null)
                .ToList();

            if (visibleRows == null || visibleRows.Count == 0)
            {
                MessageBox.Show("No data to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var groupField = cmbGroupBy.SelectedItem?.ToString() ?? "Group";
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                FileName = $"Analysis_{groupField}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                DefaultExt = ".xlsx"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("Analysis Summary");

                // Headers
                var headers = new[] { groupField, "BudgetMHs", "EarnedMHs", "Quantity", "QtyEarned", "% Complete" };
                for (int c = 0; c < headers.Length; c++)
                {
                    var cell = ws.Cell(1, c + 1);
                    cell.Value = headers[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2D2D30");
                    cell.Style.Font.FontColor = XLColor.White;
                }

                // Data rows
                for (int r = 0; r < visibleRows.Count; r++)
                {
                    var row = visibleRows[r]!;
                    int rowNum = r + 2;
                    ws.Cell(rowNum, 1).Value = row.GroupValue;
                    ws.Cell(rowNum, 2).Value = row.BudgetMHs;
                    ws.Cell(rowNum, 3).Value = row.EarnedMHs;
                    ws.Cell(rowNum, 4).Value = row.Quantity;
                    ws.Cell(rowNum, 5).Value = row.QtyEarned;
                    ws.Cell(rowNum, 6).Value = row.PercentComplete;
                }

                ws.Columns().AdjustToContents();
                workbook.SaveAs(dialog.FileName);

                MessageBox.Show($"Exported {visibleRows.Count} rows to Excel.", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (IOException)
            {
                MessageBox.Show("Cannot save — the file may be open in another application.", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AnalysisView.BtnExport_Click");
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
