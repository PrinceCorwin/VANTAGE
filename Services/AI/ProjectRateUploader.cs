using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ClosedXML.Excel;
using Microsoft.Win32;
using VANTAGE.Data;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // Handles project rate Excel upload: file pick → validate columns → project pick → upload
    public static class ProjectRateUploader
    {
        // Required columns (at least one alias must be present for each)
        private static readonly string[] RequiredColumns = { "Item", "Size", "MH" };

        // All recognized column aliases (accept old and new names)
        private static readonly Dictionary<string, string[]> ColumnAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Item", new[] { "Item", "EST_GRP", "EstGrp" } },
            { "Size", new[] { "Size" } },
            { "Sch-Class", new[] { "Sch-Class", "SchClass", "SCH_RTG", "SchRtg" } },
            { "Unit", new[] { "Unit" } },
            { "MH", new[] { "MH", "FLD_MHU", "FldMhu" } }
        };

        // Run the full upload flow. Returns true if upload succeeded.
        public static async Task<bool> UploadAsync(Window owner)
        {
            // Step 1: Pick file
            var fileDlg = new OpenFileDialog
            {
                Title = "Select Project Rate Sheet",
                Filter = "Excel Files|*.xlsx|All Files|*.*"
            };
            if (fileDlg.ShowDialog(owner) != true) return false;

            // Step 2: Validate columns and parse rows
            List<ProjectRateItem> parsed;
            try
            {
                AppLogger.Info($"Parsing rate sheet: {fileDlg.FileName}", "ProjectRateUploader.UploadAsync");
                parsed = await Task.Run(() => ParseAndValidate(fileDlg.FileName));
                AppLogger.Info($"Parsed {parsed.Count} rate(s) from file", "ProjectRateUploader.UploadAsync");
            }
            catch (InvalidOperationException ex)
            {
                AppLogger.Error(ex, "ProjectRateUploader.UploadAsync.Validate");
                MessageBox.Show(ex.Message, "Invalid Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProjectRateUploader.UploadAsync.Parse");
                MessageBox.Show($"Error reading file: {ex.Message}", "Read Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (parsed.Count == 0)
            {
                MessageBox.Show("No valid data rows found in the file.", "Empty File",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Step 3: Ask for ProjectID and SetName
            var ids = PromptForProjectAndSetName(owner);
            if (ids == null)
            {
                AppLogger.Info("Upload cancelled — no project/set name provided", "ProjectRateUploader.UploadAsync");
                return false;
            }
            string projectId = ids.Value.ProjectID;
            string setName = ids.Value.SetName;
            AppLogger.Info($"Project='{projectId}', SetName='{setName}'", "ProjectRateUploader.UploadAsync");

            // Step 4: Confirm and upload
            var confirm = MessageBox.Show(
                $"Upload {parsed.Count} rate(s) as '{setName}' for project '{projectId}'?\n\nIf a set with this name already exists for this project, it will be replaced.",
                "Confirm Upload", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                AppLogger.Info("Upload cancelled by user at confirmation", "ProjectRateUploader.UploadAsync");
                return false;
            }

            try
            {
                string username = App.CurrentUser?.Username ?? "Unknown";
                AppLogger.Info($"Starting import of {parsed.Count} rates...", "ProjectRateUploader.UploadAsync");
                await ProjectRateRepository.ImportProjectRatesAsync(projectId, setName, parsed, username);

                MessageBox.Show($"Uploaded {parsed.Count} rate(s) as '{setName}' for project '{projectId}'.",
                    "Upload Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                AppLogger.Info($"Uploaded {parsed.Count} project rate(s) '{setName}' for '{projectId}'",
                    "ProjectRateUploader.UploadAsync", username);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProjectRateUploader.UploadAsync.Import");
                MessageBox.Show($"Upload error: {ex.Message}", "Upload Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Parse Excel and validate required columns exist
        private static List<ProjectRateItem> ParseAndValidate(string filePath)
        {
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();

            var usedRange = ws.RangeUsed();
            if (usedRange == null)
                throw new InvalidOperationException("The file appears to be empty.");

            int lastRow = usedRange.LastRow().RowNumber();
            int lastCol = usedRange.LastColumn().ColumnNumber();

            // Map headers
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int col = 1; col <= lastCol; col++)
            {
                string header = ws.Cell(1, col).GetString().Trim();
                if (!string.IsNullOrEmpty(header))
                    headerMap[header] = col;
            }

            // Resolve columns using aliases
            int itemCol = FindColumn(headerMap, "Item");
            int sizeCol = FindColumn(headerMap, "Size");
            int mhCol = FindColumn(headerMap, "MH");
            int schClassCol = FindColumn(headerMap, "Sch-Class");
            int unitCol = FindColumn(headerMap, "Unit");

            // Check required columns
            var missing = new List<string>();
            if (itemCol == 0) missing.Add("Item");
            if (sizeCol == 0) missing.Add("Size");
            if (mhCol == 0) missing.Add("MH");

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Missing required column(s): {string.Join(", ", missing)}\n\n" +
                    $"Expected columns:\n" +
                    $"  • Item — Rate group (e.g., BW, FTG, PIPE)\n" +
                    $"  • Size — Pipe/fitting size (numeric)\n" +
                    $"  • MH — Man-hours per unit\n" +
                    $"  • Sch-Class — Schedule/class (optional, e.g., S40, STD, 150)\n" +
                    $"  • Unit — Unit of measure (optional, defaults to EA)");
            }

            // Parse data rows
            var list = new List<ProjectRateItem>();
            for (int row = 2; row <= lastRow; row++)
            {
                string item = ws.Cell(row, itemCol).GetString().Trim();
                if (string.IsNullOrEmpty(item)) continue;

                double size = 0;
                if (ws.Cell(row, sizeCol).Value.IsNumber)
                    size = ws.Cell(row, sizeCol).Value.GetNumber();
                else
                    double.TryParse(ws.Cell(row, sizeCol).GetString(), out size);

                string schClass = schClassCol > 0 ? ws.Cell(row, schClassCol).GetString().Trim() : "";
                string unit = unitCol > 0 ? ws.Cell(row, unitCol).GetString().Trim() : "EA";
                if (string.IsNullOrEmpty(unit)) unit = "EA";

                double mh = 0;
                if (ws.Cell(row, mhCol).Value.IsNumber)
                    mh = ws.Cell(row, mhCol).Value.GetNumber();
                else
                    double.TryParse(ws.Cell(row, mhCol).GetString(), out mh);

                list.Add(new ProjectRateItem
                {
                    Item = item,
                    Size = size,
                    SchClass = schClass,
                    Unit = unit,
                    MH = mh
                });
            }

            return list;
        }

        // Find a column index using known aliases
        private static int FindColumn(Dictionary<string, int> headerMap, string canonicalName)
        {
            if (!ColumnAliases.TryGetValue(canonicalName, out var aliases))
                return headerMap.GetValueOrDefault(canonicalName, 0);

            foreach (var alias in aliases)
            {
                if (headerMap.TryGetValue(alias, out int col))
                    return col;
            }
            return 0;
        }

        // Load project list from VMS_Projects
        private static List<string> LoadProjectIds()
        {
            var list = new List<string>();
            try
            {
                using var conn = AzureDbManager.GetConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT ProjectID FROM VMS_Projects ORDER BY ProjectID";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(reader.GetString(0));
            }
            catch { /* table may not exist */ }
            return list;
        }

        // Prompt for ProjectID (dropdown) and SetName (text)
        private static (string ProjectID, string SetName)? PromptForProjectAndSetName(Window owner)
        {
            var projects = LoadProjectIds();

            var dlg = new Window
            {
                Title = "Project Rate Details",
                Width = 380,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                Background = (System.Windows.Media.Brush)Application.Current.FindResource("BackgroundColor")
            };

            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };
            var fg = (System.Windows.Media.Brush)Application.Current.FindResource("ForegroundColor");

            var lblProject = new System.Windows.Controls.TextBlock
            {
                Text = "Project ID:",
                Foreground = fg,
                Margin = new Thickness(0, 0, 0, 4),
                FontSize = 13
            };
            var cboProject = new System.Windows.Controls.ComboBox
            {
                Height = 28,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10),
                IsEditable = false
            };
            foreach (var p in projects)
                cboProject.Items.Add(p);
            if (projects.Count > 0)
                cboProject.SelectedIndex = 0;

            var lblSetName = new System.Windows.Controls.TextBlock
            {
                Text = "Rate Set Name:",
                Foreground = fg,
                Margin = new Thickness(0, 0, 0, 4),
                FontSize = 13
            };
            var txtSetName = new System.Windows.Controls.TextBox
            {
                Height = 28,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 16)
            };

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnOk = new System.Windows.Controls.Button
            {
                Content = "OK",
                Width = 70,
                Height = 28,
                IsDefault = true,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnOk.Click += (s, e) =>
            {
                if (cboProject.SelectedItem == null)
                {
                    MessageBox.Show("Select a Project ID.", "Missing Field",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtSetName.Text))
                {
                    MessageBox.Show("Rate Set Name is required.", "Missing Field",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                dlg.DialogResult = true;
            };

            var btnCancel = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 70,
                Height = 28,
                IsCancel = true
            };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            panel.Children.Add(lblProject);
            panel.Children.Add(cboProject);
            panel.Children.Add(lblSetName);
            panel.Children.Add(txtSetName);
            panel.Children.Add(btnPanel);
            dlg.Content = panel;

            if (dlg.ShowDialog() == true)
                return (cboProject.SelectedItem!.ToString()!, txtSetName.Text.Trim());

            return null;
        }
    }
}
