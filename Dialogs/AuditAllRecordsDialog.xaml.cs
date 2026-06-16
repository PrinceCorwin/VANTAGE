using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Admin → Audit All Records. Read-only scan of every active row in Azure
    // VMS_Activities for missing required metadata and ActivityValidator date/% rule
    // violations. Does NOT modify any records — pure report. The admin can see who
    // owns each problem record (AssignedTo column) and export the full list to Excel.
    // "Show in ProgressView" applies a UniqueID-IN filter to whatever records the
    // admin currently has in local cache (which may be a subset).
    public partial class AuditAllRecordsDialog : Window
    {
        public bool ShowInProgressRequested { get; private set; }
        public IReadOnlyList<string> OffendingUniqueIds { get; private set; } = Array.Empty<string>();

        private List<ValidationIssue> _issues = new();
        private readonly IReadOnlyList<string> _projectFilter;
        private readonly AuditScope _scope;

        // projectFilter scopes the scan to the supplied ProjectIDs. scope picks the
        // data source: Azure for the canonical state, Local for what the admin has
        // synced down (lets Show in ProgressView surface every offender).
        public AuditAllRecordsDialog(IReadOnlyList<string>? projectFilter = null, AuditScope scope = AuditScope.Azure)
        {
            _projectFilter = projectFilter ?? Array.Empty<string>();
            _scope = scope;

            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            Title = scope == AuditScope.Local
                ? "Audit All Records (Admin) — Local"
                : "Audit All Records (Admin) — Azure";

            Loaded += AuditAllRecordsDialog_Loaded;
        }

        private async void AuditAllRecordsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            string username = App.CurrentUser?.Username ?? "Unknown";

            try
            {
                var filter = _projectFilter;
                var scope = _scope;
                var issues = await Task.Run(() =>
                    scope == AuditScope.Local
                        ? ScanLocal(filter, status => Dispatcher.Invoke(() => txtBusyMessage.Text = status))
                        : ScanAzure(filter, status => Dispatcher.Invoke(() => txtBusyMessage.Text = status)));

                _issues = issues;
                pnlLoading.Visibility = Visibility.Collapsed;

                if (issues.Count == 0)
                {
                    txtNoIssues.Visibility = Visibility.Visible;
                    txtSummary.Text = "Scan complete. All records pass validation.";
                }
                else
                {
                    sfIssues.ItemsSource = issues;
                    btnExport.IsEnabled = true;
                    btnShowInProgress.IsEnabled = true;

                    int distinctRows = issues.Select(i => i.UniqueID).Distinct().Count();
                    int distinctUsers = issues.Select(i => i.AssignedTo).Distinct().Count();
                    txtSummary.Text =
                        $"Found {issues.Count:N0} issue(s) across {distinctRows:N0} record(s) " +
                        $"owned by {distinctUsers:N0} user(s). Drag the AssignedTo column header to the group area to group by user.";

                    AppLogger.Info(
                        $"Audit All Records: {distinctRows} record(s) with {issues.Count} violation(s) across {distinctUsers} user(s)",
                        "AuditAllRecordsDialog.Loaded", username);
                }
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "AuditAllRecordsDialog.Loaded");
                AppMessageBox.Show($"Error auditing records:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Streams every active row from Azure VMS_Activities (optionally scoped to
        // the supplied ProjectIDs), validates per row in C#, and returns only the
        // offenders. Memory footprint is bounded by issue count, not table size, so
        // this works on multi-million-row tables.
        private static List<ValidationIssue> ScanAzure(IReadOnlyList<string> projectFilter, Action<string> progress)
        {
            var issues = new List<ValidationIssue>();
            var requiredFields = ActivityRequiredMetadata.Fields;

            using var conn = AzureDbManager.GetConnection();
            conn.Open();

            // Explicit projection of the columns we need. SchedActNO, ProjectID,
            // Description are pulled out for the result rows; PercentEntry/ActStart/
            // ActFin feed ActivityValidator; the remaining required fields are scanned
            // for blanks. IsDeleted = 0 mirrors what the rest of the app considers
            // "active". WITH (NOLOCK) avoids reader-blocker waits on the audit.
            var sb = new StringBuilder();
            sb.Append("SELECT UniqueID, AssignedTo, SchedActNO, ProjectID, Description, ");
            sb.Append("PercentEntry, ActStart, ActFin");
            foreach (var f in requiredFields)
            {
                if (f.Equals("SchedActNO", StringComparison.OrdinalIgnoreCase)) continue;
                if (f.Equals("ProjectID", StringComparison.OrdinalIgnoreCase)) continue;
                if (f.Equals("Description", StringComparison.OrdinalIgnoreCase)) continue;
                sb.Append($", {f}");
            }
            sb.Append(" FROM VMS_Activities WITH (NOLOCK) WHERE ISNULL(IsDeleted, 0) = 0");

            var cmd = conn.CreateCommand();

            // Parameterised IN filter for the selected projects — keeps the engine
            // safe from injection even though IDs come from VMS_Projects, not user
            // input. Skipped when no filter supplied (caller hands us all projects).
            if (projectFilter != null && projectFilter.Count > 0)
            {
                var paramNames = new List<string>(projectFilter.Count);
                for (int i = 0; i < projectFilter.Count; i++)
                {
                    string pname = $"@p{i}";
                    paramNames.Add(pname);
                    cmd.Parameters.AddWithValue(pname, projectFilter[i]);
                }
                sb.Append(" AND ProjectID IN (").Append(string.Join(",", paramNames)).Append(')');
            }

            cmd.CommandText = sb.ToString();
            cmd.CommandTimeout = 0;

            int scanned = 0;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string uniqueId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                string assignedTo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                string schedActNO = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                string projectId = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                string description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                double percent = reader.IsDBNull(5) ? 0 : Convert.ToDouble(reader.GetValue(5));
                DateTime? actStart = ParseNullableDate(reader, 6);
                DateTime? actFin = ParseNullableDate(reader, 7);

                int ordinal = 8;
                foreach (var f in requiredFields)
                {
                    if (f.Equals("SchedActNO", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(schedActNO))
                            issues.Add(new ValidationIssue(assignedTo, uniqueId, schedActNO, projectId, description,
                                "Missing required field: SchedActNO"));
                        continue;
                    }
                    if (f.Equals("ProjectID", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(projectId))
                            issues.Add(new ValidationIssue(assignedTo, uniqueId, schedActNO, projectId, description,
                                "Missing required field: ProjectID"));
                        continue;
                    }
                    if (f.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(description))
                            issues.Add(new ValidationIssue(assignedTo, uniqueId, schedActNO, projectId, description,
                                "Missing required field: Description"));
                        continue;
                    }

                    string? value = reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        issues.Add(new ValidationIssue(assignedTo, uniqueId, schedActNO, projectId, description,
                            $"Missing required field: {f}"));
                    }
                    ordinal++;
                }

                string? dateViolation = ActivityValidator.Validate(percent, actStart, actFin);
                if (dateViolation != null)
                    issues.Add(new ValidationIssue(assignedTo, uniqueId, schedActNO, projectId, description,
                        dateViolation));

                scanned++;
                // Throttle UI updates — every 5K rows is plenty for "alive" feedback.
                if (scanned % 5000 == 0)
                    progress($"Scanning Azure activities... ({scanned:N0} rows, {issues.Count:N0} issues so far)");
            }

            progress($"Scan complete — {scanned:N0} rows examined.");
            return issues;
        }

        // Local-cache variant of ScanAzure. Reads from the SQLite Activities table
        // instead of Azure VMS_Activities. Local has no IsDeleted column (per
        // CLAUDE.md: "Azure has IsDeleted, Admins table"), so the row predicate
        // drops that filter.
        private static List<ValidationIssue> ScanLocal(IReadOnlyList<string> projectFilter, Action<string> progress)
        {
            var issues = new List<ValidationIssue>();
            var requiredFields = ActivityRequiredMetadata.Fields;

            using var conn = DatabaseSetup.GetConnection();
            conn.Open();

            var sb = new StringBuilder();
            sb.Append("SELECT UniqueID, AssignedTo, SchedActNO, ProjectID, Description, ");
            sb.Append("PercentEntry, ActStart, ActFin");
            foreach (var f in requiredFields)
            {
                if (f.Equals("SchedActNO", StringComparison.OrdinalIgnoreCase)) continue;
                if (f.Equals("ProjectID", StringComparison.OrdinalIgnoreCase)) continue;
                if (f.Equals("Description", StringComparison.OrdinalIgnoreCase)) continue;
                sb.Append($", {f}");
            }
            sb.Append(" FROM Activities WHERE 1 = 1");

            var cmd = conn.CreateCommand();

            if (projectFilter != null && projectFilter.Count > 0)
            {
                var paramNames = new List<string>(projectFilter.Count);
                for (int i = 0; i < projectFilter.Count; i++)
                {
                    string pname = $"@p{i}";
                    paramNames.Add(pname);
                    cmd.Parameters.AddWithValue(pname, projectFilter[i]);
                }
                sb.Append(" AND ProjectID IN (").Append(string.Join(",", paramNames)).Append(')');
            }

            cmd.CommandText = sb.ToString();

            int scanned = 0;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string uniqueId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                string assignedTo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                string schedActNO = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                string projectId = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                string description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                double percent = reader.IsDBNull(5) ? 0 : Convert.ToDouble(reader.GetValue(5));
                DateTime? actStart = ParseNullableLocalDate(reader, 6);
                DateTime? actFin = ParseNullableLocalDate(reader, 7);

                int ordinal = 8;
                foreach (var f in requiredFields)
                {
                    if (f.Equals("SchedActNO", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(schedActNO))
                            issues.Add(new ValidationIssue(assignedTo, uniqueId, schedActNO, projectId, description,
                                "Missing required field: SchedActNO"));
                        continue;
                    }
                    if (f.Equals("ProjectID", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(projectId))
                            issues.Add(new ValidationIssue(assignedTo, uniqueId, schedActNO, projectId, description,
                                "Missing required field: ProjectID"));
                        continue;
                    }
                    if (f.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(description))
                            issues.Add(new ValidationIssue(assignedTo, uniqueId, schedActNO, projectId, description,
                                "Missing required field: Description"));
                        continue;
                    }

                    string? value = reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        issues.Add(new ValidationIssue(assignedTo, uniqueId, schedActNO, projectId, description,
                            $"Missing required field: {f}"));
                    }
                    ordinal++;
                }

                string? dateViolation = ActivityValidator.Validate(percent, actStart, actFin);
                if (dateViolation != null)
                    issues.Add(new ValidationIssue(assignedTo, uniqueId, schedActNO, projectId, description,
                        dateViolation));

                scanned++;
                if (scanned % 5000 == 0)
                    progress($"Scanning local activities... ({scanned:N0} rows, {issues.Count:N0} issues so far)");
            }

            progress($"Scan complete — {scanned:N0} local rows examined.");
            return issues;
        }

        // SQLite reader variant of ParseNullableDate. Same logic as the Azure path
        // since dates are stored as TEXT in both stores; only the reader type differs.
        private static DateTime? ParseNullableLocalDate(SqliteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return null;
            string raw = reader.GetValue(ordinal)?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return DateTime.TryParse(raw, out var dt) ? dt : (DateTime?)null;
        }

        // Azure stores dates as TEXT to stay in sync with the local convention.
        // DateTime.TryParse matches the format range ActivityValidator was designed for.
        private static DateTime? ParseNullableDate(SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return null;
            string raw = reader.GetValue(ordinal)?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return DateTime.TryParse(raw, out var dt) ? dt : (DateTime?)null;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_issues.Count == 0) return;

            var dialog = new SaveFileDialog
            {
                Title = "Export Audit Report",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"AuditAllRecords_{DateTime.Now:yyyy-MM-dd_HHmm}.xlsx",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Audit Issues");

                ws.Cell(1, 1).Value = "AssignedTo";
                ws.Cell(1, 2).Value = "ProjectID";
                ws.Cell(1, 3).Value = "SchedActNO";
                ws.Cell(1, 4).Value = "Description";
                ws.Cell(1, 5).Value = "UniqueID";
                ws.Cell(1, 6).Value = "Violation";

                var header = ws.Range(1, 1, 1, 6);
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2D2D30");
                header.Style.Font.FontColor = XLColor.White;

                int row = 2;
                foreach (var issue in _issues)
                {
                    ws.Cell(row, 1).Value = issue.AssignedTo;
                    ws.Cell(row, 2).Value = issue.ProjectID;
                    ws.Cell(row, 3).Value = issue.SchedActNO;
                    ws.Cell(row, 4).Value = issue.Description;
                    ws.Cell(row, 5).Value = issue.UniqueID;
                    ws.Cell(row, 6).Value = issue.Violation;
                    row++;
                }

                ws.Columns().AdjustToContents();
                ws.SheetView.FreezeRows(1);

                wb.SaveAs(dialog.FileName);

                AppMessageBox.Show(
                    $"Exported {_issues.Count:N0} issue(s) to:\n{dialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (IOException ioEx)
            {
                AppMessageBox.Show(
                    $"Could not save the report — the file may be open in another application.\n\n{ioEx.Message}",
                    "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AuditAllRecordsDialog.BtnExport_Click");
                AppMessageBox.Show($"Error exporting report:\n{ex.Message}", "Export Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnShowInProgress_Click(object sender, RoutedEventArgs e)
        {
            if (_issues.Count == 0) return;

            OffendingUniqueIds = _issues
                .Select(i => i.UniqueID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ShowInProgressRequested = true;
            Close();
        }
    }
}
