using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Tools → Validate My Records. Scans the current user's Activities for missing
    // required metadata and ActivityValidator date/% rule violations. Offenders are
    // auto-marked LocalDirty = 1 so they surface in ProgressView's dirty highlight for
    // normal-flow editing. Read-only results grid lists the violations.
    public partial class ValidateMyRecordsDialog : Window
    {
        // True when any rows were marked dirty; MainWindow uses this to refresh
        // ProgressView so the new dirty highlights appear without a manual reload.
        public bool MarkedAnyDirty { get; private set; }

        // Set by the Show in ProgressView button — MainWindow uses these to switch
        // to ProgressView and apply the UniqueID-IN filter so the user sees only
        // the offending rows (not other unrelated dirty edits).
        public bool ShowInProgressRequested { get; private set; }
        public IReadOnlyList<string> OffendingUniqueIds { get; private set; } = Array.Empty<string>();

        // Cache the scan results so Export and Show-in-Progress can act on them
        // without rescanning.
        private List<ValidationIssue> _issues = new();

        public ValidateMyRecordsDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += ValidateMyRecordsDialog_Loaded;
        }

        private async void ValidateMyRecordsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.CurrentUser == null)
            {
                AppMessageBox.Show("No user logged in.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            string username = App.CurrentUser.Username;

            try
            {
                var issues = await Task.Run(() => ScanAndMarkDirty(username));

                pnlLoading.Visibility = Visibility.Collapsed;

                _issues = issues;

                if (issues.Count == 0)
                {
                    txtNoIssues.Visibility = Visibility.Visible;
                    txtSummary.Text = "Scan complete. No issues found.";
                }
                else
                {
                    sfIssues.ItemsSource = issues;
                    MarkedAnyDirty = true;
                    btnExport.IsEnabled = true;
                    btnShowInProgress.IsEnabled = true;

                    int distinctRows = issues.Select(i => i.UniqueID).Distinct().Count();
                    txtSummary.Text =
                        $"Found {issues.Count:N0} issue(s) across {distinctRows:N0} record(s). " +
                        "These records have been marked as unsaved — fix them in ProgressView and re-sync.";

                    AppLogger.Info(
                        $"Validate My Records marked {distinctRows} record(s) dirty " +
                        $"({issues.Count} violation(s) total)",
                        "ValidateMyRecordsDialog.Loaded", username);
                }
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "ValidateMyRecordsDialog.Loaded");
                AppMessageBox.Show($"Error scanning records:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Scans the user's local Activities, returns one issue per (UniqueID, violation)
        // pair, and marks every offending UniqueID LocalDirty = 1 in a single batch UPDATE.
        // Note: deliberately does NOT touch UpdatedBy/UpdatedUtcDate — we're surfacing
        // legacy issues, not claiming the user edited them.
        private static List<ValidationIssue> ScanAndMarkDirty(string username)
        {
            var issues = new List<ValidationIssue>();
            var offenderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var conn = DatabaseSetup.GetConnection();
            conn.Open();

            // Fetch only the columns needed for validation. Required-metadata fields
            // come from the single source of truth so adding a field there propagates here.
            var requiredFields = ActivityRequiredMetadata.Fields;

            var sb = new StringBuilder();
            sb.Append("SELECT UniqueID, SchedActNO, ProjectID, Description, PercentEntry, ActStart, ActFin");
            foreach (var f in requiredFields)
            {
                // SchedActNO, ProjectID, Description already selected — skip duplicates.
                if (f.Equals("SchedActNO", StringComparison.OrdinalIgnoreCase)) continue;
                if (f.Equals("ProjectID", StringComparison.OrdinalIgnoreCase)) continue;
                if (f.Equals("Description", StringComparison.OrdinalIgnoreCase)) continue;
                sb.Append($", {f}");
            }
            sb.Append(" FROM Activities WHERE AssignedTo = @user");

            var cmd = conn.CreateCommand();
            cmd.CommandText = sb.ToString();
            cmd.Parameters.AddWithValue("@user", username);

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string uniqueId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    string schedActNO = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    string projectId = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    string description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                    double percent = reader.IsDBNull(4) ? 0 : reader.GetDouble(4);
                    DateTime? actStart = ParseNullableDate(reader, 5);
                    DateTime? actFin = ParseNullableDate(reader, 6);

                    // Missing-metadata checks: each required field that's blank produces
                    // its own issue row so the user sees exactly what needs filling in.
                    int ordinal = 7;
                    foreach (var f in requiredFields)
                    {
                        if (f.Equals("SchedActNO", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrWhiteSpace(schedActNO))
                            {
                                issues.Add(new ValidationIssue(uniqueId, schedActNO, projectId, description,
                                    "Missing required field: SchedActNO"));
                                offenderIds.Add(uniqueId);
                            }
                            continue;
                        }
                        if (f.Equals("ProjectID", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrWhiteSpace(projectId))
                            {
                                issues.Add(new ValidationIssue(uniqueId, schedActNO, projectId, description,
                                    "Missing required field: ProjectID"));
                                offenderIds.Add(uniqueId);
                            }
                            continue;
                        }
                        if (f.Equals("Description", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrWhiteSpace(description))
                            {
                                issues.Add(new ValidationIssue(uniqueId, schedActNO, projectId, description,
                                    "Missing required field: Description"));
                                offenderIds.Add(uniqueId);
                            }
                            continue;
                        }

                        string? value = reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            issues.Add(new ValidationIssue(uniqueId, schedActNO, projectId, description,
                                $"Missing required field: {f}"));
                            offenderIds.Add(uniqueId);
                        }
                        ordinal++;
                    }

                    // Date/% rules from ActivityValidator — single source of truth.
                    string? dateViolation = ActivityValidator.Validate(percent, actStart, actFin);
                    if (dateViolation != null)
                    {
                        issues.Add(new ValidationIssue(uniqueId, schedActNO, projectId, description, dateViolation));
                        offenderIds.Add(uniqueId);
                    }
                }
            }

            // Batch UPDATE to flip LocalDirty on every offender. One transaction, one
            // command, batched IN clause — fast even at thousands of rows.
            if (offenderIds.Count > 0)
            {
                MarkDirty(conn, offenderIds);
            }

            return issues;
        }

        // Marks the given UniqueIDs LocalDirty = 1 in batches to stay under SQLite's
        // 999-parameter default limit. Single transaction so the whole thing is atomic.
        private static void MarkDirty(SqliteConnection conn, ICollection<string> uniqueIds)
        {
            const int batchSize = 500;
            using var tx = conn.BeginTransaction();
            try
            {
                var idList = uniqueIds.ToList();
                for (int i = 0; i < idList.Count; i += batchSize)
                {
                    var batch = idList.Skip(i).Take(batchSize).ToList();
                    var placeholders = string.Join(",", batch.Select((_, idx) => $"@id{idx}"));

                    var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = $"UPDATE Activities SET LocalDirty = 1 WHERE UniqueID IN ({placeholders})";
                    for (int j = 0; j < batch.Count; j++)
                        cmd.Parameters.AddWithValue($"@id{j}", batch[j]);

                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // Local SQLite stores dates as TEXT; DateTime.TryParse handles the same range
        // of formats ActivityValidator was designed for (matches ParseDates in
        // ModifySnapshotDialog).
        private static DateTime? ParseNullableDate(SqliteDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return null;
            string raw = reader.GetString(ordinal);
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
                Title = "Export Validation Report",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"ValidationReport_{DateTime.Now:yyyy-MM-dd_HHmm}.xlsx",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Validation Issues");

                ws.Cell(1, 1).Value = "ProjectID";
                ws.Cell(1, 2).Value = "SchedActNO";
                ws.Cell(1, 3).Value = "Description";
                ws.Cell(1, 4).Value = "UniqueID";
                ws.Cell(1, 5).Value = "Violation";

                var header = ws.Range(1, 1, 1, 5);
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2D2D30");
                header.Style.Font.FontColor = XLColor.White;

                int row = 2;
                foreach (var issue in _issues)
                {
                    ws.Cell(row, 1).Value = issue.ProjectID;
                    ws.Cell(row, 2).Value = issue.SchedActNO;
                    ws.Cell(row, 3).Value = issue.Description;
                    ws.Cell(row, 4).Value = issue.UniqueID;
                    ws.Cell(row, 5).Value = issue.Violation;
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
                // Most common cause: file is already open in Excel.
                AppMessageBox.Show(
                    $"Could not save the report — the file may be open in another application.\n\n{ioEx.Message}",
                    "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ValidateMyRecordsDialog.BtnExport_Click");
                AppMessageBox.Show($"Error exporting report:\n{ex.Message}", "Export Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnShowInProgress_Click(object sender, RoutedEventArgs e)
        {
            if (_issues.Count == 0) return;

            // Distinct UniqueIDs across all issue rows — a single record with multiple
            // violations only contributes one row to the ProgressView filter.
            OffendingUniqueIds = _issues
                .Select(i => i.UniqueID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ShowInProgressRequested = true;
            Close();
        }
    }

    // One row per (record, violation) — a record can produce multiple rows if it has
    // both a missing required field and a bad date. AssignedTo is only populated by
    // the admin "Audit All Records" dialog; the user-facing dialog leaves it empty
    // since every row is owned by the current user.
    public class ValidationIssue
    {
        public string AssignedTo { get; }
        public string UniqueID { get; }
        public string SchedActNO { get; }
        public string ProjectID { get; }
        public string Description { get; }
        public string Violation { get; }

        public ValidationIssue(string uniqueId, string schedActNO, string projectId, string description, string violation)
            : this(string.Empty, uniqueId, schedActNO, projectId, description, violation)
        {
        }

        public ValidationIssue(string assignedTo, string uniqueId, string schedActNO, string projectId, string description, string violation)
        {
            AssignedTo = assignedTo;
            UniqueID = uniqueId;
            SchedActNO = schedActNO;
            ProjectID = projectId;
            Description = description;
            Violation = violation;
        }
    }
}
