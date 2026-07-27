using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Syncfusion.SfSkinManager;
using ClosedXML.Excel;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Standalone window hosting only the Analysis summary table. Extracted from
    // AnalysisView so the Analysis tab can eventually be retired without losing this
    // grouped roll-up.
    //
    // The Projects control is deliberately NOT a Syncfusion multi-select ComboBoxAdv:
    // that control's checkbox dropdown template fails to render inside a standalone
    // Window (blank/white, won't open). Instead it's a plain-WPF ToggleButton + Popup +
    // checkbox ListBox, which themes reliably in any window.
    public partial class PhaseCodeAnalysisWindow : Window
    {
        // Lightweight row wrapper for the project checkbox list.
        public class ProjectSelection : INotifyPropertyChanged
        {
            public string ProjectId { get; set; } = string.Empty;

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

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
        private readonly ObservableCollection<ProjectSelection> _projects = new();

        public PhaseCodeAnalysisWindow()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            _isInitializing = true;
            try
            {
                PopulateGroupByDropdown();
                lstProjects.ItemsSource = _projects;
                RestoreSettings();
                // Populate the projects list from whichever source was just restored.
                PopulateProjectsList(rbSourceSnapshot.IsChecked == true ? "SnapshotAnalysis" : "Activities",
                    autoSelectFirst: true);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhaseCodeAnalysisWindow.ctor.Init");
            }
            finally
            {
                _isInitializing = false;
            }

            LoadSummaryData();

            // If Source mode was restored to Snapshot, refresh the status label from the
            // SnapshotAnalysis table contents so the user can see what's currently loaded.
            if (rbSourceSnapshot.IsChecked == true)
                _ = UpdateSnapshotStatusFromTableAsync();
        }

        // Populate Group By dropdown with priority fields first, then others alphabetically
        private void PopulateGroupByDropdown()
        {
            var otherFields = AllTextFields.Except(PriorityFields).OrderBy(f => f);
            var allFields = PriorityFields.OrderBy(f => f).Concat(otherFields).ToList();
            cmbGroupBy.ItemsSource = allFields;
        }

        // Rebuild the projects checkbox list from the given source table (Activities for
        // Local, SnapshotAnalysis for Snapshot). Called on load, on source switch, and
        // after a new snapshot selection — the two tables hold different project sets, so
        // the list (and any stale selection) must be refetched, not carried over.
        private void PopulateProjectsList(string sourceTable, bool autoSelectFirst)
        {
            // Whitelist the table name — never interpolate user input into a table identifier.
            if (sourceTable != "Activities" && sourceTable != "SnapshotAnalysis")
            {
                AppLogger.Error(new InvalidOperationException($"Invalid source table: {sourceTable}"),
                    "PhaseCodeAnalysisWindow.PopulateProjectsList");
                return;
            }

            try
            {
                var ids = new List<string>();
                using (var connection = DatabaseSetup.GetConnection())
                {
                    connection.Open();
                    var cmd = connection.CreateCommand();
                    cmd.CommandText = $"SELECT DISTINCT ProjectID FROM {sourceTable} WHERE ProjectID IS NOT NULL AND ProjectID != '' ORDER BY ProjectID";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var projectId = reader.GetString(0);
                        if (!string.IsNullOrWhiteSpace(projectId))
                            ids.Add(projectId);
                    }
                }

                // Rebuild under the init guard so the checkbox binding updates don't
                // re-trigger ProjectCheck_Changed (the caller re-aggregates explicitly).
                bool prevInit = _isInitializing;
                _isInitializing = true;
                try
                {
                    _projects.Clear();
                    foreach (var id in ids)
                        _projects.Add(new ProjectSelection { ProjectId = id });

                    if (autoSelectFirst && _projects.Count > 0)
                        _projects[0].IsSelected = true;
                }
                finally
                {
                    _isInitializing = prevInit;
                }

                UpdateProjectsSummary();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhaseCodeAnalysisWindow.PopulateProjectsList");
            }
        }

        // Currently-checked project IDs.
        private List<string> SelectedProjectIds =>
            _projects.Where(p => p.IsSelected).Select(p => p.ProjectId).ToList();

        // Update the toggle button caption to reflect the current selection.
        private void UpdateProjectsSummary()
        {
            var selected = SelectedProjectIds;
            if (selected.Count == 0)
                txtProjectsSummary.Text = "(all)";
            else if (selected.Count == 1)
                txtProjectsSummary.Text = selected[0];
            else
                txtProjectsSummary.Text = $"{selected.Count} selected";
        }

        // Restore saved settings (shares the Analysis view's UserSettings keys so the
        // Group By / user / source choices carry over between the two entry points).
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

            // User filter — default All Users when no saved value exists.
            var currentUserOnly = SettingsManager.GetAnalysisCurrentUserOnly();
            rbCurrentUser.IsChecked = currentUserOnly;
            rbAllUsers.IsChecked = !currentUserOnly;

            // Source mode — default Local when no saved value exists.
            var sourceMode = SettingsManager.GetAnalysisSourceMode();
            if (string.Equals(sourceMode, "Snapshot", StringComparison.OrdinalIgnoreCase))
            {
                rbSourceSnapshot.IsChecked = true;
                rbSourceLocal.IsChecked = false;
                btnReSelectSnapshots.IsEnabled = true;
            }
            else
            {
                rbSourceLocal.IsChecked = true;
                rbSourceSnapshot.IsChecked = false;
                btnReSelectSnapshots.IsEnabled = false;
            }

            // Project selection is set up by PopulateProjectsList after this returns.
        }

        // Load summary data grouped by selected field
        private void LoadSummaryData()
        {
            if (_isInitializing) return;

            var groupField = cmbGroupBy.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(groupField)) return;

            // Both branches query local SQLite — Activities for Local mode, SnapshotAnalysis
            // for Snapshot mode. Same query shape, just different table.
            string sourceTable = rbSourceSnapshot.IsChecked == true ? "SnapshotAnalysis" : "Activities";
            LoadSummaryFromLocalTable(groupField, sourceTable);
        }

        // Single aggregation path — same query shape works against Activities (Local mode)
        // or SnapshotAnalysis (Snapshot mode), since SnapshotAnalysis mirrors the Activities
        // schema. Runs against local SQLite, so it's sub-second regardless of source.
        private void LoadSummaryFromLocalTable(string groupField, string sourceTable)
        {
            // Whitelist the table name — never interpolate user input into a table identifier.
            if (sourceTable != "Activities" && sourceTable != "SnapshotAnalysis")
            {
                AppLogger.Error(new InvalidOperationException($"Invalid source table: {sourceTable}"),
                    "PhaseCodeAnalysisWindow.LoadSummaryFromLocalTable");
                return;
            }

            var currentUserOnly = rbCurrentUser.IsChecked == true;
            var selectedProjects = SelectedProjectIds;

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
                    FROM {sourceTable}
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
                AppLogger.Error(ex, "PhaseCodeAnalysisWindow.LoadSummaryFromLocalTable");
                AppMessageBox.Show($"Error loading summary data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Event handlers
        private void CmbGroupBy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            // Update grid column header to match selected field
            colGroupValue.HeaderText = cmbGroupBy.SelectedItem?.ToString() ?? "Group";

            SettingsManager.SetAnalysisGroupField(cmbGroupBy.SelectedItem?.ToString() ?? "PhaseCode");

            LoadSummaryData();
        }

        private void UserFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            SettingsManager.SetAnalysisCurrentUserOnly(rbCurrentUser.IsChecked == true);

            LoadSummaryData();
        }

        // A project checkbox was toggled — refresh the caption and re-aggregate.
        private void ProjectCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            UpdateProjectsSummary();
            LoadSummaryData();
        }

        // Source radio handler. Clicking Snapshot or Local just persists the choice and
        // re-runs aggregation against the right local table — does NOT auto-open the picker.
        // The picker only opens via the Re-select button. Empty SnapshotAnalysis = empty grid.
        private void SourceRadio_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            bool isSnapshot = rbSourceSnapshot.IsChecked == true;
            btnReSelectSnapshots.IsEnabled = isSnapshot;

            SettingsManager.SetAnalysisSourceMode(isSnapshot ? "Snapshot" : "Local");

            if (isSnapshot)
            {
                // Refresh the status label asynchronously from the table.
                _ = UpdateSnapshotStatusFromTableAsync();
            }
            else
            {
                txtSnapshotStatus.Text = string.Empty;
            }

            // The two sources hold different project sets — refetch the list (and reset
            // the stale selection) so the picker reflects the source now in effect.
            PopulateProjectsList(isSnapshot ? "SnapshotAnalysis" : "Activities", autoSelectFirst: true);

            LoadSummaryData();
        }

        // Re-select button — opens the picker, pre-checking whatever's currently loaded
        // in SnapshotAnalysis. On Apply, wipes + repopulates the local table from Azure
        // off the UI thread (busy overlay on the summary grid). Then re-aggregates.
        private async void BtnReSelectSnapshots_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var current = await VANTAGE.Repositories.SnapshotAnalysisRepository
                    .GetCurrentSnapshotKeysAsync();

                var dialog = new VANTAGE.Dialogs.SelectAnalysisSnapshotsDialog(current)
                {
                    Owner = this
                };
                if (dialog.ShowDialog() != true) return;

                summaryBusyOverlay.Visibility = Visibility.Visible;
                try
                {
                    int rowsWritten = await VANTAGE.Repositories.SnapshotAnalysisRepository
                        .PopulateFromAzureAsync(dialog.SelectedSnapshots);

                    txtSnapshotStatus.Text = dialog.SelectedSnapshots.Count == 0
                        ? "none"
                        : $"{dialog.SelectedSnapshots.Count} selected";

                    if (rbSourceSnapshot.IsChecked == true)
                    {
                        // Snapshot table contents changed — refetch the project list from it.
                        PopulateProjectsList("SnapshotAnalysis", autoSelectFirst: true);
                        LoadSummaryData();
                    }
                }
                finally
                {
                    summaryBusyOverlay.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                summaryBusyOverlay.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "PhaseCodeAnalysisWindow.BtnReSelectSnapshots_Click");
                AppMessageBox.Show(
                    "Failed to update snapshot cache. See log for details.",
                    "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Reads the current SnapshotAnalysis table to update the status label after the
        // window loads in Snapshot mode (so the user can see what's loaded without opening
        // the picker).
        private async System.Threading.Tasks.Task UpdateSnapshotStatusFromTableAsync()
        {
            try
            {
                var current = await VANTAGE.Repositories.SnapshotAnalysisRepository
                    .GetCurrentSnapshotKeysAsync();
                txtSnapshotStatus.Text = current.Count == 0
                    ? "none"
                    : $"{current.Count} selected";
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhaseCodeAnalysisWindow.UpdateSnapshotStatusFromTableAsync");
            }
        }

        // Export current filtered grid contents to Excel
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var rows = summaryGrid.ItemsSource as List<AnalysisSummaryRow>;
            if (rows == null || rows.Count == 0)
            {
                AppMessageBox.Show("No data to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Get visible (filtered) records from the grid view
            var visibleRows = summaryGrid.View?.Records?
                .Select(r => r.Data as AnalysisSummaryRow)
                .Where(r => r != null)
                .ToList();

            if (visibleRows == null || visibleRows.Count == 0)
            {
                AppMessageBox.Show("No data to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
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

                AppMessageBox.Show($"Exported {visibleRows.Count} rows to Excel.", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (IOException)
            {
                AppMessageBox.Show("Cannot save — the file may be open in another application.", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "PhaseCodeAnalysisWindow.BtnExport_Click");
                AppMessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
