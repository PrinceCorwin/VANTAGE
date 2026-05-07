using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ManageUDFNamesDialog : Window
    {
        // Editor row model. EditableName is the user-typed visible name; blank means
        // "use the default header (MappingName)".
        public class UDFRow
        {
            public string FieldName { get; set; } = string.Empty;
            public string EditableName { get; set; } = string.Empty;
        }

        // Callback fired whenever the dialog mutates ProgressUDFNames.Active
        // (Save or Reset Defaults). The dialog never closes from a button click —
        // only the Close button or window X closes it — so the host (MainWindow)
        // uses this callback to re-apply UDF names on the cached ProgressView.
        private readonly Action? _onActiveChanged;

        public ManageUDFNamesDialog(Action? onActiveChanged = null)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            _onActiveChanged = onActiveChanged;
            LoadActiveIntoEditor();
        }

        private void LoadActiveIntoEditor()
        {
            PopulateEditor(SettingsManager.GetActiveUDFNames());
        }

        private void PopulateEditor(Dictionary<string, string> values)
        {
            var rows = ProgressRenameableColumns.UDFs.Select(udf => new UDFRow
            {
                FieldName = udf,
                EditableName = values.TryGetValue(udf, out var v) ? v : string.Empty
            }).ToList();
            icUDFRows.ItemsSource = rows;
        }

        // Read the current editor state into a Dictionary. Blank/whitespace
        // values are excluded so they fall back to the default header.
        private Dictionary<string, string> CollectEditorState()
        {
            var dict = new Dictionary<string, string>();
            if (icUDFRows.ItemsSource is IEnumerable<UDFRow> rows)
            {
                foreach (var r in rows)
                {
                    var trimmed = (r.EditableName ?? string.Empty).Trim();
                    if (trimmed.Length > 50) trimmed = trimmed.Substring(0, 50);
                    if (!string.IsNullOrWhiteSpace(trimmed))
                        dict[r.FieldName] = trimmed;
                }
            }
            return dict;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var state = CollectEditorState();
                SettingsManager.SetActiveUDFNames(state);
                _onActiveChanged?.Invoke();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageUDFNamesDialog.BtnSave_Click");
                AppMessageBox.Show("Failed to save UDF names. See log for details.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = AppMessageBox.Show(
                    "Clear all UDF overrides and revert column headers to their defaults?",
                    "Reset to Defaults",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.OK) return;

                SettingsManager.ClearActiveUDFNames();
                PopulateEditor(new Dictionary<string, string>());
                _onActiveChanged?.Invoke();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageUDFNamesDialog.BtnReset_Click");
                AppMessageBox.Show("Failed to reset. See log for details.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "UDF mapping (*.udfmap.json)|*.udfmap.json|JSON (*.json)|*.json",
                    FileName = "UDFNames.udfmap.json",
                    DefaultExt = ".udfmap.json"
                };
                if (dlg.ShowDialog() != true) return;

                var state = CollectEditorState();
                var json = System.Text.Json.JsonSerializer.Serialize(state,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);

                AppMessageBox.Show("UDF mapping exported.", "Exported",
                    MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageUDFNamesDialog.BtnExport_Click");
                AppMessageBox.Show("Export failed. See log for details.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "UDF mapping (*.udfmap.json;*.json)|*.udfmap.json;*.json|All files (*.*)|*.*"
                };
                if (dlg.ShowDialog() != true) return;

                Dictionary<string, string>? imported;
                try
                {
                    var json = File.ReadAllText(dlg.FileName);
                    imported = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                }
                catch (Exception readEx)
                {
                    AppLogger.Error(readEx, "ManageUDFNamesDialog.BtnImport_Click.Read");
                    AppMessageBox.Show("Could not read or parse the file.", "Import Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (imported == null)
                {
                    AppMessageBox.Show("File is not a valid UDF mapping.", "Import Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Filter to allowed UDFs only, trim/cap values, drop blanks
                var allowed = new HashSet<string>(ProgressRenameableColumns.UDFs);
                var filtered = new Dictionary<string, string>();
                foreach (var kv in imported)
                {
                    if (!allowed.Contains(kv.Key)) continue;
                    var val = (kv.Value ?? string.Empty).Trim();
                    if (val.Length > 50) val = val.Substring(0, 50);
                    if (!string.IsNullOrWhiteSpace(val))
                        filtered[kv.Key] = val;
                }

                PopulateEditor(filtered);

                AppMessageBox.Show("Mapping loaded into the editor. Click Save to apply it.",
                    "Imported", MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageUDFNamesDialog.BtnImport_Click");
                AppMessageBox.Show("Import failed. See log for details.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
