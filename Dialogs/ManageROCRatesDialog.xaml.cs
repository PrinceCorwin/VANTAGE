using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Services.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ManageROCRatesDialog : Window
    {
        private ObservableCollection<ROCRateItem> _items = new();
        private bool _isLoading;
        private bool _isEditMode;
        private bool _isNewSet;

        // Track original project+set for rename detection during modify
        private string? _originalProjectId;
        private string? _originalSetName;

        // When true, dialog opens directly in New Set edit mode
        private bool _openInNewSetMode;

        // All sets for the view-mode dropdown
        private List<(string ProjectID, string SetName)> _allSets = new();

        // All projects for the edit-mode project dropdown
        private List<string> _allProjects = new();

        // Component checklist items
        private ObservableCollection<ComponentCheckItem> _componentItems = new();

        // Shop/Field dropdown options for the grid
        public List<int> ShopFieldOptions { get; } = new() { 1, 2 };

        public ManageROCRatesDialog(bool openInNewSetMode = false)
        {
            _openInNewSetMode = openInNewSetMode;
            InitializeComponent();
            DataContext = this;
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += ManageROCRatesDialog_Loaded;
        }

        private async void ManageROCRatesDialog_Loaded(object sender, RoutedEventArgs e)
        {
            LoadComponentChecklist();
            SetComponentChecklistEnabled(false);
            await LoadAllSetsAsync();

            // If opened from "+ Create New...", go straight to new set edit mode
            if (_openInNewSetMode)
                BtnNewSet_Click(this, new RoutedEventArgs());
        }

        // Load all (ProjectID, SetName) pairs for the view-mode dropdown.
        // If selectIndex is provided, select that set and load its data.
        private async System.Threading.Tasks.Task LoadAllSetsAsync(int selectIndex = -1)
        {
            try
            {
                _isLoading = true;
                SetStatus("Loading sets...");

                _allSets = await ProjectRateRepository.GetROCSetsAsync();

                cboSet.Items.Clear();
                foreach (var (projectId, setName) in _allSets)
                    cboSet.Items.Add($"{projectId} - {setName}");

                if (selectIndex >= 0 && selectIndex < _allSets.Count)
                    cboSet.SelectedIndex = selectIndex;

                if (cboSet.SelectedIndex < 0)
                {
                    _items.Clear();
                    sfGrid.ItemsSource = _items;
                    ApplyComponentSelections(null);
                }

                SetStatus("");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageROCRatesDialog.LoadAllSetsAsync");
                SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }

            // Load the selected set's data now that _isLoading is cleared
            if (cboSet.SelectedIndex >= 0 && cboSet.SelectedIndex < _allSets.Count)
            {
                var (projectId, setName) = _allSets[cboSet.SelectedIndex];
                await LoadSetDataAsync(projectId, setName);
            }
        }

        // Load rows for a specific project+set into the grid and component checklist
        private async System.Threading.Tasks.Task LoadSetDataAsync(string projectId, string setName)
        {
            try
            {
                SetStatus("Loading set...");

                var (items, components) = await System.Threading.Tasks.Task.Run(() =>
                {
                    var list = new List<ROCRateItem>();
                    string? comp = null;
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT Id, ROCStep, Percentage, ShopField, SortOrder, Components
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
                        // Read Components from the first row that has it
                        if (comp == null && !reader.IsDBNull(5))
                            comp = reader.GetString(5);
                    }
                    return (list, comp);
                });

                _items = new ObservableCollection<ROCRateItem>(items);
                sfGrid.ItemsSource = _items;

                // Apply component selections
                ApplyComponentSelections(components);

                UpdateStatus();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageROCRatesDialog.LoadSetDataAsync");
                SetStatus($"Error: {ex.Message}");
            }
        }

        // Load all projects for the edit-mode project dropdown
        private async System.Threading.Tasks.Task LoadProjectsAsync()
        {
            try
            {
                var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                await System.Threading.Tasks.Task.Run(() =>
                {
                    // From VMS_ROCRates
                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT DISTINCT ProjectID FROM VMS_ROCRates ORDER BY ProjectID";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                        projects.Add(reader.GetString(0));
                });

                try
                {
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        // From VMS_Projects
                        using var conn = AzureDbManager.GetConnection();
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT ProjectID FROM VMS_Projects ORDER BY ProjectID";
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                            projects.Add(reader.GetString(0));
                    });
                }
                catch { /* VMS_Projects might not exist */ }

                _allProjects = projects.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();

                cboEditProject.Items.Clear();
                foreach (var p in _allProjects)
                    cboEditProject.Items.Add(p);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageROCRatesDialog.LoadProjectsAsync");
            }
        }

        // View-mode dropdown selection changed — load the selected set
        private async void CboSet_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading || cboSet.SelectedIndex < 0) return;

            var (projectId, setName) = _allSets[cboSet.SelectedIndex];
            await LoadSetDataAsync(projectId, setName);
        }

        // Switch to edit mode
        private void EnterEditMode(bool isNew)
        {
            _isEditMode = true;
            _isNewSet = isNew;

            // Show/hide panels
            pnlViewHeader.Visibility = Visibility.Collapsed;
            pnlEditHeader.Visibility = Visibility.Visible;

            // Show edit buttons, hide view buttons
            btnAddRow.Visibility = Visibility.Visible;
            btnDeleteRow.Visibility = Visibility.Visible;
            btnSave.Visibility = Visibility.Visible;
            btnCancel.Visibility = Visibility.Visible;

            // Enable grid editing and component checkboxes
            sfGrid.AllowEditing = true;
            SetComponentChecklistEnabled(true);

            SetStatus("");
        }

        // Switch to view mode
        private void ExitEditMode()
        {
            _isEditMode = false;
            _isNewSet = false;
            _originalProjectId = null;
            _originalSetName = null;

            // Show/hide panels
            pnlViewHeader.Visibility = Visibility.Visible;
            pnlEditHeader.Visibility = Visibility.Collapsed;

            // Hide edit buttons
            btnAddRow.Visibility = Visibility.Collapsed;
            btnDeleteRow.Visibility = Visibility.Collapsed;
            btnSave.Visibility = Visibility.Collapsed;
            btnCancel.Visibility = Visibility.Collapsed;

            // Disable grid editing and component checkboxes
            sfGrid.AllowEditing = false;
            SetComponentChecklistEnabled(false);

            SetStatus("");
        }

        // New Set button — switch to edit mode with empty grid
        private async void BtnNewSet_Click(object sender, RoutedEventArgs e)
        {
            await LoadProjectsAsync();
            EnterEditMode(isNew: true);

            // Clear fields
            if (cboEditProject.Items.Count > 0)
                cboEditProject.SelectedIndex = 0;
            txtEditSetName.Text = "";

            _items = new ObservableCollection<ROCRateItem>();
            sfGrid.ItemsSource = _items;

            // Clear all component checkboxes for new set
            foreach (var c in _componentItems) c.IsChecked = false;
            UpdateComponentCount();
        }

        // Modify button — switch to edit mode with current set data
        private async void BtnModify_Click(object sender, RoutedEventArgs e)
        {
            if (cboSet.SelectedIndex < 0 || _allSets.Count == 0)
            {
                MessageBox.Show("Select a set to modify.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (projectId, setName) = _allSets[cboSet.SelectedIndex];
            _originalProjectId = projectId;
            _originalSetName = setName;

            await LoadProjectsAsync();
            EnterEditMode(isNew: false);

            // Pre-fill project and set name
            int projIdx = _allProjects.IndexOf(projectId);
            if (projIdx >= 0)
                cboEditProject.SelectedIndex = projIdx;
            txtEditSetName.Text = setName;

            // Data is already loaded from view mode
        }

        // Cancel button — discard and return to view mode
        private async void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode();

            // Reload to restore view state (no set selected)
            await LoadAllSetsAsync();
        }

        // Save button — validate and save the set
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string? projectId = cboEditProject.SelectedItem?.ToString();
            string? setName = txtEditSetName.Text?.Trim();

            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(setName))
            {
                MessageBox.Show("Select a project and enter a set name.", "Missing Info",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_items.Count == 0)
            {
                MessageBox.Show("Add at least one ROC step.", "No Steps",
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
                        // If modifying and project/set name changed, delete the original
                        if (!_isNewSet && _originalProjectId != null && _originalSetName != null)
                        {
                            bool renamed = !string.Equals(projectId, _originalProjectId, StringComparison.OrdinalIgnoreCase)
                                        || !string.Equals(setName, _originalSetName, StringComparison.OrdinalIgnoreCase);
                            if (renamed)
                            {
                                using var delOrigCmd = conn.CreateCommand();
                                delOrigCmd.Transaction = transaction;
                                delOrigCmd.CommandText = "DELETE FROM VMS_ROCRates WHERE ProjectID = @ProjectID AND SetName = @SetName";
                                delOrigCmd.Parameters.AddWithValue("@ProjectID", _originalProjectId);
                                delOrigCmd.Parameters.AddWithValue("@SetName", _originalSetName);
                                delOrigCmd.ExecuteNonQuery();
                            }
                        }

                        // Delete target rows (handles overwrite/update)
                        using (var delCmd = conn.CreateCommand())
                        {
                            delCmd.Transaction = transaction;
                            delCmd.CommandText = "DELETE FROM VMS_ROCRates WHERE ProjectID = @ProjectID AND SetName = @SetName";
                            delCmd.Parameters.AddWithValue("@ProjectID", projectId);
                            delCmd.Parameters.AddWithValue("@SetName", setName);
                            delCmd.ExecuteNonQuery();
                        }

                        // Build comma-separated components string from checked items
                        string componentsValue = string.Join(",",
                            _componentItems.Where(c => c.IsChecked).Select(c => c.Name));

                        // Insert all rows
                        foreach (var item in _items)
                        {
                            using var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                INSERT INTO VMS_ROCRates (ProjectID, SetName, ROCStep, Percentage, ShopField, SortOrder, Components, CreatedBy, UpdatedBy)
                                VALUES (@ProjectID, @SetName, @ROCStep, @Percentage, @ShopField, @SortOrder, @Components, @CreatedBy, @UpdatedBy)";
                            cmd.Parameters.AddWithValue("@ProjectID", projectId);
                            cmd.Parameters.AddWithValue("@SetName", setName);
                            cmd.Parameters.AddWithValue("@ROCStep", item.ROCStep);
                            cmd.Parameters.AddWithValue("@Percentage", item.Percentage);
                            cmd.Parameters.AddWithValue("@ShopField", item.ShopField);
                            cmd.Parameters.AddWithValue("@SortOrder", item.SortOrder);
                            cmd.Parameters.AddWithValue("@Components", string.IsNullOrEmpty(componentsValue) ? (object)DBNull.Value : componentsValue);
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

                AppLogger.Info($"Saved ROC rate set '{setName}' for project '{projectId}' ({_items.Count} steps)",
                    "ManageROCRatesDialog.BtnSave_Click", username);

                // Switch back to view mode and select the saved set
                ExitEditMode();
                await LoadAllSetsAsync();

                // Find and select the saved set
                int idx = _allSets.FindIndex(s =>
                    s.ProjectID.Equals(projectId, StringComparison.OrdinalIgnoreCase)
                    && s.SetName.Equals(setName, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    cboSet.SelectedIndex = idx;
                    await LoadSetDataAsync(projectId, setName);
                }

                SetStatus($"Saved {_items.Count} step(s)");
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

        // Delete the selected set
        private async void BtnDeleteSet_Click(object sender, RoutedEventArgs e)
        {
            if (cboSet.SelectedIndex < 0 || _allSets.Count == 0)
            {
                MessageBox.Show("Select a set to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (projectId, setName) = _allSets[cboSet.SelectedIndex];

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

                AppLogger.Info($"Deleted ROC rate set '{setName}' for project '{projectId}'",
                    "ManageROCRatesDialog.BtnDeleteSet_Click", username);

                _items.Clear();
                sfGrid.ItemsSource = _items;
                await LoadAllSetsAsync();

                SetStatus($"Deleted set '{setName}'");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageROCRatesDialog.BtnDeleteSet_Click");
                MessageBox.Show($"Error deleting: {ex.Message}", "Delete Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditMode)
            {
                var result = MessageBox.Show("You have unsaved changes. Close anyway?",
                    "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }
            Close();
        }

        // ========================================
        // COMPONENT CHECKLIST
        // ========================================

        // Build the component checklist from the rate sheet
        private void LoadComponentChecklist()
        {
            var allComponents = RateSheetService.GetAllComponents();
            _componentItems = new ObservableCollection<ComponentCheckItem>(
                allComponents.Select(c => new ComponentCheckItem { Name = c, IsChecked = false, IsEnabled = false })
            );

            foreach (var item in _componentItems)
                item.PropertyChanged += ComponentItem_PropertyChanged;

            icComponents.ItemsSource = _componentItems;
            UpdateComponentCount();
        }

        // Apply a comma-separated components string to the checklist
        private void ApplyComponentSelections(string? components)
        {
            if (string.IsNullOrEmpty(components))
            {
                foreach (var item in _componentItems) item.IsChecked = false;
            }
            else
            {
                var checkedSet = new HashSet<string>(
                    components.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in _componentItems)
                    item.IsChecked = checkedSet.Contains(item.Name);
            }

            UpdateComponentCount();
        }

        // Enable/disable checkboxes based on edit mode
        private void SetComponentChecklistEnabled(bool enabled)
        {
            foreach (var item in _componentItems)
                item.IsEnabled = enabled;
        }

        private void BtnCheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _componentItems)
                item.IsChecked = true;
            UpdateComponentCount();
        }

        private void BtnUncheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _componentItems)
                item.IsChecked = false;
            UpdateComponentCount();
        }

        private void ComponentItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ComponentCheckItem.IsChecked))
                UpdateComponentCount();
        }

        private void UpdateComponentCount()
        {
            int checkedCount = _componentItems.Count(c => c.IsChecked);
            txtComponentCount.Text = $"{checkedCount} / {_componentItems.Count}";
        }

        private void UpdateStatus()
        {
            double total = _items.Sum(i => i.Percentage);
            string indicator = Math.Abs(total - 100.0) < 0.01 ? "✓" : "⚠";
            txtStatus.Text = $"{_items.Count} step(s)  |  Total: {total:F2}% {indicator}";
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

    // Component checkbox item for the applicable components checklist
    public class ComponentCheckItem : INotifyPropertyChanged
    {
        private string _name = "";
        private bool _isChecked;
        private bool _isEnabled = true;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; OnPropertyChanged(nameof(IsChecked)); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
