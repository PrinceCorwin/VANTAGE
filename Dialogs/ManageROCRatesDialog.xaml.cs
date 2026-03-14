using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ManageROCRatesDialog : Window
    {
        private ObservableCollection<ROCRateItem> _items = new();
        private bool _isAdmin;
        private bool _isLoading;

        // Shop/Field dropdown options for the grid
        public List<int> ShopFieldOptions { get; } = new() { 1, 2 };

        public ManageROCRatesDialog()
        {
            InitializeComponent();
            DataContext = this;
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            _isAdmin = AzureDbManager.IsUserAdmin(App.CurrentUser?.Username ?? "");

            // Disable editing for non-admins
            if (!_isAdmin)
            {
                sfGrid.AllowEditing = false;
                btnAddRow.IsEnabled = false;
                btnDeleteRow.IsEnabled = false;
                btnSave.IsEnabled = false;
                btnDeleteSet.IsEnabled = false;
                cboSetName.IsEditable = false;
            }

            Loaded += ManageROCRatesDialog_Loaded;
        }

        private async void ManageROCRatesDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadProjectsAsync();
        }

        // Load distinct ProjectIDs from VMS_ROCRates
        private async System.Threading.Tasks.Task LoadProjectsAsync()
        {
            try
            {
                _isLoading = true;
                SetStatus("Loading projects...");

                var projects = await System.Threading.Tasks.Task.Run(() =>
                {
                    var list = new List<string>();
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT DISTINCT ProjectID FROM VMS_ROCRates ORDER BY ProjectID";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                        list.Add(reader.GetString(0));
                    return list;
                });

                cboProject.Items.Clear();
                foreach (var p in projects)
                    cboProject.Items.Add(p);

                // Also add projects from VMS_Projects that aren't in ROCRates yet
                try
                {
                    var allProjects = await System.Threading.Tasks.Task.Run(() =>
                    {
                        var list = new List<string>();
                        using var conn = AzureDbManager.GetConnection();
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT ProjectID FROM VMS_Projects ORDER BY ProjectID";
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                            list.Add(reader.GetString(0));
                        return list;
                    });

                    foreach (var p in allProjects)
                    {
                        if (!projects.Contains(p))
                            cboProject.Items.Add(p);
                    }
                }
                catch { /* VMS_Projects might not exist */ }

                if (cboProject.Items.Count > 0)
                    cboProject.SelectedIndex = 0;

                SetStatus("");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageROCRatesDialog.LoadProjectsAsync");
                SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Load set names for selected project
        private async System.Threading.Tasks.Task LoadSetsForProjectAsync(string projectId)
        {
            try
            {
                _isLoading = true;
                var sets = await System.Threading.Tasks.Task.Run(() =>
                {
                    var list = new List<string>();
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT DISTINCT SetName FROM VMS_ROCRates WHERE ProjectID = @ProjectID ORDER BY SetName";
                    cmd.Parameters.AddWithValue("@ProjectID", projectId);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                        list.Add(reader.GetString(0));
                    return list;
                });

                cboSetName.Items.Clear();
                foreach (var s in sets)
                    cboSetName.Items.Add(s);

                if (sets.Count > 0)
                    cboSetName.SelectedIndex = 0;
                else
                    _items.Clear();

                sfGrid.ItemsSource = _items;
                UpdateStatus();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageROCRatesDialog.LoadSetsForProjectAsync");
                SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Load rows for selected project + set
        private async System.Threading.Tasks.Task LoadSetDataAsync(string projectId, string setName)
        {
            try
            {
                SetStatus("Loading set...");

                var items = await System.Threading.Tasks.Task.Run(() =>
                {
                    var list = new List<ROCRateItem>();
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT Id, ROCStep, Percentage, ShopField, SortOrder
                        FROM VMS_ROCRates
                        WHERE ProjectID = @ProjectID AND SetName = @SetName
                        ORDER BY SortOrder, ROCStep";
                    cmd.Parameters.AddWithValue("@ProjectID", projectId);
                    cmd.Parameters.AddWithValue("@SetName", setName);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new ROCRateItem
                        {
                            Id = reader.GetInt32(0),
                            ROCStep = reader.GetString(1),
                            Percentage = reader.GetDouble(2),
                            ShopField = reader.GetInt32(3),
                            SortOrder = reader.GetInt32(4)
                        });
                    }
                    return list;
                });

                _items = new ObservableCollection<ROCRateItem>(items);
                sfGrid.ItemsSource = _items;
                UpdateStatus();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageROCRatesDialog.LoadSetDataAsync");
                SetStatus($"Error: {ex.Message}");
            }
        }

        private async void CboProject_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading || cboProject.SelectedItem == null) return;
            await LoadSetsForProjectAsync(cboProject.SelectedItem.ToString()!);
        }

        private void CboSetName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Selection change only — actual load happens on Load button click
        }

        private async void BtnLoadSet_Click(object sender, RoutedEventArgs e)
        {
            string? projectId = cboProject.SelectedItem?.ToString();
            string? setName = cboSetName.Text?.Trim();

            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(setName))
            {
                MessageBox.Show("Select a project and set name.", "Missing Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await LoadSetDataAsync(projectId, setName);
        }

        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            _items.Add(new ROCRateItem
            {
                ROCStep = "New Step",
                Percentage = 0,
                ShopField = 2,
                SortOrder = _items.Count > 0 ? _items.Max(i => i.SortOrder) + 1 : 0
            });
            UpdateStatus();
        }

        private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (sfGrid.SelectedItem is ROCRateItem item)
            {
                _items.Remove(item);
                UpdateStatus();
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string? projectId = cboProject.SelectedItem?.ToString();
            string? setName = cboSetName.Text?.Trim();

            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(setName))
            {
                MessageBox.Show("Select a project and enter a set name.", "Missing Info",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate percentage sum
            double totalPct = _items.Sum(i => i.Percentage);
            if (Math.Abs(totalPct - 100.0) > 0.01)
            {
                MessageBox.Show($"Percentages must sum to 100%. Current total: {totalPct:F2}%",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate no duplicate ROCStep names
            var dupes = _items.GroupBy(i => i.ROCStep, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0)
            {
                MessageBox.Show($"Duplicate ROC Step names: {string.Join(", ", dupes)}",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                btnSave.IsEnabled = false;
                SetStatus("Saving...");
                string username = App.CurrentUser?.Username ?? "Unknown";

                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();
                    using var transaction = conn.BeginTransaction();

                    try
                    {
                        // Delete existing rows for this project/set
                        using (var delCmd = conn.CreateCommand())
                        {
                            delCmd.Transaction = transaction;
                            delCmd.CommandText = "DELETE FROM VMS_ROCRates WHERE ProjectID = @ProjectID AND SetName = @SetName";
                            delCmd.Parameters.AddWithValue("@ProjectID", projectId);
                            delCmd.Parameters.AddWithValue("@SetName", setName);
                            delCmd.ExecuteNonQuery();
                        }

                        // Insert all current rows
                        foreach (var item in _items)
                        {
                            using var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                INSERT INTO VMS_ROCRates (ProjectID, SetName, ROCStep, Percentage, ShopField, SortOrder, CreatedBy, UpdatedBy)
                                VALUES (@ProjectID, @SetName, @ROCStep, @Percentage, @ShopField, @SortOrder, @CreatedBy, @UpdatedBy)";
                            cmd.Parameters.AddWithValue("@ProjectID", projectId);
                            cmd.Parameters.AddWithValue("@SetName", setName);
                            cmd.Parameters.AddWithValue("@ROCStep", item.ROCStep);
                            cmd.Parameters.AddWithValue("@Percentage", item.Percentage);
                            cmd.Parameters.AddWithValue("@ShopField", item.ShopField);
                            cmd.Parameters.AddWithValue("@SortOrder", item.SortOrder);
                            cmd.Parameters.AddWithValue("@CreatedBy", username);
                            cmd.Parameters.AddWithValue("@UpdatedBy", username);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                });

                // Reload to get new IDs
                await LoadSetDataAsync(projectId, setName);

                // Refresh set name list if this is a new set
                await LoadSetsForProjectAsync(projectId);
                cboSetName.Text = setName;

                SetStatus($"Saved {_items.Count} step(s)");
                AppLogger.Info($"Saved ROC rate set '{setName}' for project '{projectId}' ({_items.Count} steps)",
                    "ManageROCRatesDialog.BtnSave_Click", username);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageROCRatesDialog.BtnSave_Click");
                MessageBox.Show($"Error saving: {ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSave.IsEnabled = true;
            }
        }

        private async void BtnDeleteSet_Click(object sender, RoutedEventArgs e)
        {
            string? projectId = cboProject.SelectedItem?.ToString();
            string? setName = cboSetName.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(setName))
            {
                MessageBox.Show("Select a project and set to delete.", "Missing Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Delete the entire set '{setName}' for project '{projectId}'?\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                string username = App.CurrentUser?.Username ?? "Unknown";
                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM VMS_ROCRates WHERE ProjectID = @ProjectID AND SetName = @SetName";
                    cmd.Parameters.AddWithValue("@ProjectID", projectId);
                    cmd.Parameters.AddWithValue("@SetName", setName);
                    cmd.ExecuteNonQuery();
                });

                _items.Clear();
                sfGrid.ItemsSource = _items;
                await LoadSetsForProjectAsync(projectId);

                SetStatus($"Deleted set '{setName}'");
                AppLogger.Info($"Deleted ROC rate set '{setName}' for project '{projectId}'",
                    "ManageROCRatesDialog.BtnDeleteSet_Click", username);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageROCRatesDialog.BtnDeleteSet_Click");
                MessageBox.Show($"Error deleting: {ex.Message}", "Delete Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UpdateStatus()
        {
            double total = _items.Sum(i => i.Percentage);
            string color = Math.Abs(total - 100.0) < 0.01 ? "✓" : "⚠";
            txtStatus.Text = $"{_items.Count} step(s)  |  Total: {total:F2}% {color}";
        }

        private void SetStatus(string message)
        {
            txtStatus.Text = message;
        }
    }

    // ROC rate step item for grid binding
    public class ROCRateItem : INotifyPropertyChanged
    {
        private int _id;
        private string _rocStep = "";
        private double _percentage;
        private int _shopField = 2;
        private int _sortOrder;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string ROCStep
        {
            get => _rocStep;
            set { _rocStep = value; OnPropertyChanged(nameof(ROCStep)); }
        }

        public double Percentage
        {
            get => _percentage;
            set { _percentage = value; OnPropertyChanged(nameof(Percentage)); }
        }

        public int ShopField
        {
            get => _shopField;
            set { _shopField = value; OnPropertyChanged(nameof(ShopField)); }
        }

        public int SortOrder
        {
            get => _sortOrder;
            set { _sortOrder = value; OnPropertyChanged(nameof(SortOrder)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
