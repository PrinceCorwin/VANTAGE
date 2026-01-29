using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using VANTAGE.Data;
using VANTAGE.Dialogs;
using VANTAGE.Models.ProgressBook;
using VANTAGE.Services.ProgressBook;
using VANTAGE.Utilities;

namespace VANTAGE.Views
{
    public partial class ProgressBooksView : UserControl
    {
        // Currently loaded layout (null = new unsaved layout)
        private ProgressBookLayout? _currentLayout;

        // Column data
        private ObservableCollection<ColumnDisplayItem> _columns = new();

        // Groups for the ItemsControl (just field names, auto-sorted alphanumerically)
        private ObservableCollection<GroupItemViewModel> _groups = new();

        // Sort fields for the ItemsControl (stacking sort like Excel)
        private ObservableCollection<SortItemViewModel> _sortFields = new();

        // Available fields for grouping and columns
        private List<string> _allFields = new();
        private List<string> _commonFields = new() { "PhaseCode", "Area", "SubArea", "SystemNO", "WorkPackage", "TagNO" };

        // Flag to prevent recursive updates
        private bool _isLoading;

        // Track unsaved changes to warn user before switching layouts
        private bool _hasUnsavedChanges;

        // PDF preview stream (kept alive while viewer displays it)
        private MemoryStream? _previewStream;

        // Constant for default layout name
        private const string DefaultLayoutName = "Default Layout";

        // Maximum groups and sorts allowed
        private const int MaxGroups = 10;
        private const int MaxSorts = 10;

        // Special value for "None" sort option
        private const string NoneSortValue = "None";

        public ProgressBooksView()
        {
            InitializeComponent();
            Loaded += ProgressBooksView_Loaded;
        }

        private async void ProgressBooksView_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            try
            {
                InitializeFieldLists();
                PopulateDropdowns();
                await LoadSavedLayoutsAsync();
                LoadDefaultConfiguration();
                RestoreSplitterPosition();
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Restore splitter position from user settings
        private void RestoreSplitterPosition()
        {
            var splitterRatio = SettingsManager.GetUserSetting("ProgressBook.SplitterRatio");
            if (!string.IsNullOrEmpty(splitterRatio) && double.TryParse(splitterRatio, out double ratio) && ratio > 0 && ratio < 1)
            {
                LeftPanelColumn.Width = new GridLength(ratio, GridUnitType.Star);
                RightPanelColumn.Width = new GridLength(1 - ratio, GridUnitType.Star);
            }
        }

        // Save splitter position when drag completes
        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            double totalWidth = LeftPanelColumn.ActualWidth + RightPanelColumn.ActualWidth;
            if (totalWidth > 0)
            {
                double leftRatio = LeftPanelColumn.ActualWidth / totalWidth;
                SettingsManager.SetUserSetting("ProgressBook.SplitterRatio", leftRatio.ToString("F4"), "string");
            }
        }

        // Initialize the list of available Activity fields
        private void InitializeFieldLists()
        {
            _allFields = new List<string>
            {
                "ActivityID", // Short numeric ID for easy scanning
                "Area", "ChgOrdNO", "CompType", "Description", "DwgNO",
                "EqmtNO", "HtTrace", "InsulType", "LineNumber", "MtrlSpec", "Notes",
                "PaintCode", "PhaseCategory", "PhaseCode", "PipeGrade", "ProjectID",
                "RespParty", "RevNO", "RFINO", "ROCStep", "SchedActNO", "SecondDwgNO",
                "Service", "ShopField", "ShtNO", "SubArea", "PjtSystem", "PjtSystemNo", "SystemNO",
                "TagNO", "UDF1", "UDF2", "UDF3", "UDF4", "UDF5", "UDF6", "UDF7",
                "UDF8", "UDF9", "UDF10", "UniqueID", "WorkPackage"
            };
            _allFields.Sort();
        }

        // Populate the dropdowns
        private void PopulateDropdowns()
        {
            // Filter column dropdown - common fields first with star, then all
            var filterColumnItems = new List<string>();
            foreach (var field in _commonFields)
            {
                filterColumnItems.Add($"★ {field}");
            }
            filterColumnItems.Add("───────────");
            filterColumnItems.AddRange(_allFields);

            cboFilterColumn.ItemsSource = filterColumnItems;
            cboFilterColumn.SelectedIndex = 4; // WorkPackage

            // Add column dropdown - all fields
            RefreshAddColumnDropdown();
        }

        // Refresh the add column dropdown (exclude already added columns)
        private void RefreshAddColumnDropdown()
        {
            var usedFields = _columns.Select(c => c.FieldName).ToHashSet();
            var available = _allFields.Where(f => !usedFields.Contains(f)).ToList();
            cboAddColumn.ItemsSource = available;
            if (available.Count > 0)
                cboAddColumn.SelectedIndex = 0;
        }

        // Get the sort field options (None + columns)
        private List<string> GetSortFieldOptions()
        {
            var options = new List<string> { NoneSortValue };
            options.AddRange(_columns.Select(c => c.FieldName));
            return options;
        }

        // Load saved layouts into the dropdown
        private async System.Threading.Tasks.Task LoadSavedLayoutsAsync()
        {
            if (App.CurrentUser == null) return;

            var layouts = await ProgressBookLayoutRepository.GetAllForUserAsync(App.CurrentUser.Username);
            var items = new List<LayoutDropdownItem>
            {
                new LayoutDropdownItem { Id = 0, Name = DefaultLayoutName }
            };
            items.AddRange(layouts.Select(l => new LayoutDropdownItem { Id = l.Id, Name = l.Name }));

            cboSavedLayouts.ItemsSource = items;
            cboSavedLayouts.DisplayMemberPath = "Name";
            cboSavedLayouts.SelectedIndex = 0;

            UpdateDeleteButtonState();
        }

        // Load default configuration for a new layout
        private void LoadDefaultConfiguration()
        {
            _currentLayout = null;
            txtLayoutName.Text = DefaultLayoutName;
            rbLetter.IsChecked = true;
            sliderFontSize.Value = 8; // Default to 8pt for better scan accuracy

            // Default filter: WorkPackage
            SetFilterColumnSelection("WorkPackage");
            cboFilterValue.ItemsSource = null;
            cboFilterValue.SelectedItem = null;

            // Default columns: ActivityID (short ID for scanning), ROC and Description
            _columns.Clear();
            _columns.Add(new ColumnDisplayItem { FieldName = "ActivityID", IsRequired = true });
            _columns.Add(new ColumnDisplayItem { FieldName = "ROCStep", IsRequired = true });
            _columns.Add(new ColumnDisplayItem { FieldName = "Description", IsRequired = true });
            RefreshColumnsListBox();

            // Default grouping: PhaseCode
            _groups.Clear();
            AddGroup("PhaseCode", canDelete: false);
            RefreshGroups();

            // Default sort: ROCStep
            _sortFields.Clear();
            AddSortField("ROCStep", canDelete: false);
            RefreshSortFields();

            RefreshAddColumnDropdown();
                        UpdateDeleteButtonState();
            UpdateAddGroupButtonState();
            UpdateAddSortButtonState();

            // Load filter values for default column
            _ = LoadFilterValuesAsync();

            _hasUnsavedChanges = false;
        }

        // Load a saved layout configuration
        private void LoadLayoutConfiguration(ProgressBookLayout layout)
        {
            _isLoading = true;
            try
            {
                _currentLayout = layout;
                txtLayoutName.Text = layout.Name;

                var config = layout.GetConfiguration();

                rbLetter.IsChecked = config.PaperSize == PaperSize.Letter;
                rbTabloid.IsChecked = config.PaperSize == PaperSize.Tabloid;
                sliderFontSize.Value = config.FontSize;

                // Load filter settings
                SetFilterColumnSelection(config.FilterField);
                chkExcludeCompleted.IsChecked = config.ExcludeCompleted;

                // Load columns
                _columns.Clear();
                foreach (var col in config.Columns.OrderBy(c => c.DisplayOrder))
                {
                    bool isRequired = col.FieldName == "ActivityID" || col.FieldName == "UniqueID" || col.FieldName == "ROCStep" || col.FieldName == "Description";
                    _columns.Add(new ColumnDisplayItem
                    {
                        FieldName = col.FieldName,
                        IsRequired = isRequired
                    });
                }
                RefreshColumnsListBox();

                // Load groups
                _groups.Clear();
                for (int i = 0; i < config.Groups.Count; i++)
                {
                    AddGroup(config.Groups[i], canDelete: i > 0);
                }
                if (_groups.Count == 0)
                {
                    AddGroup("PhaseCode", canDelete: false);
                }
                RefreshGroups();

                // Load sort fields
                _sortFields.Clear();
                for (int i = 0; i < config.SortFields.Count; i++)
                {
                    AddSortField(config.SortFields[i], canDelete: i > 0);
                }
                if (_sortFields.Count == 0)
                {
                    AddSortField("ROCStep", canDelete: false);
                }
                RefreshSortFields();

                RefreshAddColumnDropdown();
                                UpdateDeleteButtonState();
                UpdateAddGroupButtonState();
                UpdateAddSortButtonState();

                // Load filter values then set selected value
                _ = LoadFilterValuesAsync(config.FilterValue);

                _hasUnsavedChanges = false;
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Set the filter column dropdown to the specified field
        private void SetFilterColumnSelection(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                fieldName = "WorkPackage";

            var starredVersion = $"★ {fieldName}";
            var items = cboFilterColumn.ItemsSource as List<string>;
            if (items != null)
            {
                var starredIndex = items.IndexOf(starredVersion);
                if (starredIndex >= 0)
                {
                    cboFilterColumn.SelectedIndex = starredIndex;
                    return;
                }
                var normalIndex = items.IndexOf(fieldName);
                if (normalIndex >= 0)
                {
                    cboFilterColumn.SelectedIndex = normalIndex;
                }
            }
        }

        // Get the currently selected filter column field (without the star)
        private string GetSelectedFilterColumn()
        {
            var selected = cboFilterColumn.SelectedItem as string;
            if (string.IsNullOrEmpty(selected) || selected.StartsWith("──"))
                return "WorkPackage";
            return selected.Replace("★ ", "");
        }

        // Load distinct filter values for the selected column
        private async System.Threading.Tasks.Task LoadFilterValuesAsync(string? selectValue = null)
        {
            try
            {
                var filterColumn = GetSelectedFilterColumn();
                var username = App.CurrentUser?.Username ?? "";

                // Query distinct values for this column from user's records
                var whereClause = $"AssignedTo = '{username}' AND {filterColumn} IS NOT NULL AND {filterColumn} != ''";
                var (activities, _) = await ActivityRepository.GetAllActivitiesAsync(whereClause);

                // Get distinct values using reflection
                var distinctValues = activities
                    .Select(a => GetActivityFieldValue(a, filterColumn))
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList();

                cboFilterValue.ItemsSource = distinctValues;

                // Select the specified value or first available
                if (!string.IsNullOrEmpty(selectValue) && distinctValues.Contains(selectValue))
                {
                    cboFilterValue.SelectedItem = selectValue;
                }
                else if (distinctValues.Count > 0)
                {
                    cboFilterValue.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressBooksView.LoadFilterValuesAsync");
            }
        }

        // Get a field value from an Activity using reflection
        private string? GetActivityFieldValue(Models.Activity activity, string fieldName)
        {
            try
            {
                var prop = typeof(Models.Activity).GetProperty(fieldName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                return prop?.GetValue(activity)?.ToString();
            }
            catch
            {
                return null;
            }
        }

        // Add a group to the collection
        private void AddGroup(string groupField, bool canDelete)
        {
            var group = new GroupItemViewModel
            {
                Index = _groups.Count,
                GroupField = groupField,
                CanDelete = canDelete,
                AvailableFields = _allFields
            };
            group.PropertyChanged += Group_PropertyChanged;
            _groups.Add(group);
        }

        // Add a sort field to the collection
        private void AddSortField(string sortField, bool canDelete)
        {
            var sort = new SortItemViewModel
            {
                Index = _sortFields.Count,
                SortField = sortField,
                CanDelete = canDelete,
                AvailableFields = GetSortFieldOptions()
            };
            sort.PropertyChanged += SortField_PropertyChanged;
            _sortFields.Add(sort);
        }

        // Handle changes to group properties
        private void Group_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_isLoading)
                _hasUnsavedChanges = true;
        }

        // Handle changes to sort field properties
        private void SortField_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_isLoading)
                _hasUnsavedChanges = true;
        }

        // Refresh the groups ItemsControl
        private void RefreshGroups()
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                _groups[i].Index = i;
                _groups[i].CanDelete = i > 0;
            }
            icGroups.ItemsSource = null;
            icGroups.ItemsSource = _groups;
        }

        // Refresh the sort fields ItemsControl
        private void RefreshSortFields()
        {
            var sortOptions = GetSortFieldOptions();
            for (int i = 0; i < _sortFields.Count; i++)
            {
                _sortFields[i].Index = i;
                _sortFields[i].CanDelete = i > 0;
                _sortFields[i].AvailableFields = sortOptions;
            }
            icSortFields.ItemsSource = null;
            icSortFields.ItemsSource = _sortFields;
        }

        // Update the Add Group button enabled state
        private void UpdateAddGroupButtonState()
        {
            btnAddGroup.IsEnabled = _groups.Count < MaxGroups;
        }

        // Update the Add Sort button enabled state
        private void UpdateAddSortButtonState()
        {
            btnAddSort.IsEnabled = _sortFields.Count < MaxSorts;
        }

        // Build the current configuration from UI controls
        private ProgressBookConfiguration BuildCurrentConfiguration()
        {
            var config = new ProgressBookConfiguration
            {
                PaperSize = rbLetter.IsChecked == true ? PaperSize.Letter : PaperSize.Tabloid,
                FontSize = (int)sliderFontSize.Value,
                FilterField = GetSelectedFilterColumn(),
                FilterValue = cboFilterValue.SelectedItem as string ?? string.Empty,
                ExcludeCompleted = chkExcludeCompleted.IsChecked == true
            };

            // Add groups
            foreach (var group in _groups)
            {
                config.Groups.Add(group.GroupField);
            }

            // Add columns
            int order = 0;
            foreach (var col in _columns)
            {
                config.Columns.Add(new ColumnConfig
                {
                    FieldName = col.FieldName,
                    DisplayOrder = order++
                });
            }

            // Add sort fields (skip "None" values)
            foreach (var sort in _sortFields)
            {
                config.SortFields.Add(sort.SortField);
            }

            return config;
        }

        // Refresh the columns ListBox with simple text items (widths are auto-calculated)
        private void RefreshColumnsListBox()
        {
            lstColumns.Items.Clear();
            foreach (var col in _columns)
            {
                string display = col.IsRequired
                    ? $"{col.FieldName} *"
                    : col.FieldName;
                lstColumns.Items.Add(display);
            }

            // Update sort field options when columns change
            RefreshSortFields();
        }

        // Event Handlers

        private async void CboSavedLayouts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Discard and switch layouts?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    _isLoading = true;
                    if (e.RemovedItems.Count > 0)
                        cboSavedLayouts.SelectedItem = e.RemovedItems[0];
                    _isLoading = false;
                    return;
                }
            }

            if (cboSavedLayouts.SelectedItem is LayoutDropdownItem item)
            {
                if (item.Id == 0)
                {
                    LoadDefaultConfiguration();
                }
                else
                {
                    var layout = await ProgressBookLayoutRepository.GetByIdAsync(item.Id);
                    if (layout != null)
                    {
                        LoadLayoutConfiguration(layout);
                    }
                }
            }
        }

        private void SliderFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtFontSizeLabel == null) return;

            int fontSize = (int)sliderFontSize.Value;
            txtFontSizeLabel.Text = $"Font Size: {fontSize}pt";
            if (txtDescFontNote != null)
            {
                txtDescFontNote.Text = $"(DESC column renders at {fontSize - 1}pt)";
            }

            // Show warning for small font sizes that may affect scan accuracy
            if (txtFontSizeWarning != null)
            {
                txtFontSizeWarning.Visibility = fontSize < 7 ? Visibility.Visible : Visibility.Collapsed;
            }

            if (!_isLoading)
                _hasUnsavedChanges = true;
        }

        private void PaperSize_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoading)
                _hasUnsavedChanges = true;
        }

        // Filter column selection changed - reload filter values
        private async void FilterColumn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            await LoadFilterValuesAsync();
            _hasUnsavedChanges = true;
        }

        // Filter value selection changed
        private void FilterValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoading)
                _hasUnsavedChanges = true;
        }

        // Column list actions
        private void BtnMoveColumnUp_Click(object sender, RoutedEventArgs e)
        {
            int index = lstColumns.SelectedIndex;
            if (index > 0)
            {
                var item = _columns[index];
                _columns.RemoveAt(index);
                _columns.Insert(index - 1, item);
                RefreshColumnsListBox();
                lstColumns.SelectedIndex = index - 1;
                                _hasUnsavedChanges = true;
            }
        }

        private void BtnMoveColumnDown_Click(object sender, RoutedEventArgs e)
        {
            int index = lstColumns.SelectedIndex;
            if (index >= 0 && index < _columns.Count - 1)
            {
                var item = _columns[index];
                _columns.RemoveAt(index);
                _columns.Insert(index + 1, item);
                RefreshColumnsListBox();
                lstColumns.SelectedIndex = index + 1;
                                _hasUnsavedChanges = true;
            }
        }

        private void BtnRemoveColumn_Click(object sender, RoutedEventArgs e)
        {
            int index = lstColumns.SelectedIndex;
            if (index < 0) return;

            var col = _columns[index];
            if (col.IsRequired)
            {
                MessageBox.Show($"{col.FieldName} is required and cannot be removed.",
                    "Required Column", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _columns.RemoveAt(index);
            RefreshColumnsListBox();
            RefreshAddColumnDropdown();
                        _hasUnsavedChanges = true;
        }

        private void BtnAddColumn_Click(object sender, RoutedEventArgs e)
        {
            var field = cboAddColumn.SelectedItem as string;
            if (string.IsNullOrEmpty(field)) return;

            _columns.Add(new ColumnDisplayItem { FieldName = field, IsRequired = false });
            RefreshColumnsListBox();
            RefreshAddColumnDropdown();
                        _hasUnsavedChanges = true;
        }

        // Group actions
        private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            if (_groups.Count >= MaxGroups)
            {
                MessageBox.Show($"Maximum of {MaxGroups} grouping levels allowed.",
                    "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddGroup("PhaseCode", canDelete: true);
            RefreshGroups();
            UpdateAddGroupButtonState();
            _hasUnsavedChanges = true;
        }

        private void BtnRemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int index && index > 0 && index < _groups.Count)
            {
                _groups[index].PropertyChanged -= Group_PropertyChanged;
                _groups.RemoveAt(index);
                RefreshGroups();
                UpdateAddGroupButtonState();
                _hasUnsavedChanges = true;
            }
        }

        // Sort actions
        private void BtnAddSort_Click(object sender, RoutedEventArgs e)
        {
            if (_sortFields.Count >= MaxSorts)
            {
                MessageBox.Show($"Maximum of {MaxSorts} sort levels allowed.",
                    "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddSortField(NoneSortValue, canDelete: true);
            RefreshSortFields();
            UpdateAddSortButtonState();
            _hasUnsavedChanges = true;
        }

        private void BtnRemoveSort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int index && index > 0 && index < _sortFields.Count)
            {
                _sortFields[index].PropertyChanged -= SortField_PropertyChanged;
                _sortFields.RemoveAt(index);
                RefreshSortFields();
                UpdateAddSortButtonState();
                _hasUnsavedChanges = true;
            }
        }

        // Clone layout - populates name field with "{layout}-Copy", user must save
        private void BtnCloneLayout_Click(object sender, RoutedEventArgs e)
        {
            string currentName = _currentLayout?.Name ?? txtLayoutName.Text.Trim();
            if (string.IsNullOrEmpty(currentName))
                currentName = DefaultLayoutName;

            txtLayoutName.Text = $"{currentName}-Copy";
            txtLayoutName.Focus();
            txtLayoutName.SelectAll();

            _hasUnsavedChanges = true;
        }

        // Delete layout - Default Layout cannot be deleted
        private async void BtnDeleteLayout_Click(object sender, RoutedEventArgs e)
        {
            if (_currentLayout == null)
            {
                MessageBox.Show("Default Layout cannot be deleted.", "Cannot Delete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Delete layout '{_currentLayout.Name}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var success = await ProgressBookLayoutRepository.DeleteAsync(_currentLayout.Id);
                if (success)
                {
                    _hasUnsavedChanges = false;
                    await LoadSavedLayoutsAsync();
                    LoadDefaultConfiguration();
                    cboSavedLayouts.SelectedIndex = 0;
                }
            }
        }

        // Update Delete button enabled state based on selected layout
        private void UpdateDeleteButtonState()
        {
            btnDeleteLayout.IsEnabled = _currentLayout != null;
        }

        // Save layout - blocks "Default Layout" name, warns before overwriting
        private async void BtnSaveLayout_Click(object sender, RoutedEventArgs e)
        {
            var layoutName = txtLayoutName.Text.Trim();
            if (string.IsNullOrEmpty(layoutName))
            {
                MessageBox.Show("Please enter a layout name.", "Name Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtLayoutName.Focus();
                return;
            }

            if (layoutName.Equals(DefaultLayoutName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot save as 'Default Layout'. Please use a different name.",
                    "Reserved Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtLayoutName.Focus();
                txtLayoutName.SelectAll();
                return;
            }

            if (App.CurrentUser == null)
            {
                MessageBox.Show("Please ensure you are logged in.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string projectId = "Global";

            try
            {
                var config = BuildCurrentConfiguration();

                if (_currentLayout != null && _currentLayout.Name.Equals(layoutName, StringComparison.OrdinalIgnoreCase))
                {
                    var overwriteResult = MessageBox.Show(
                        $"Overwrite layout '{layoutName}'?",
                        "Confirm Overwrite",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (overwriteResult != MessageBoxResult.Yes)
                        return;

                    _currentLayout.UpdatedUtc = DateTime.UtcNow;
                    _currentLayout.SetConfiguration(config);

                    var success = await ProgressBookLayoutRepository.UpdateAsync(_currentLayout);
                    if (success)
                    {
                        _hasUnsavedChanges = false;
                        await LoadSavedLayoutsAsync();
                        SelectLayoutInDropdown(_currentLayout.Id);
                        MessageBox.Show($"Layout '{layoutName}' updated.", "Saved",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    var existingLayout = await ProgressBookLayoutRepository.GetByNameAsync(layoutName, projectId);
                    if (existingLayout != null)
                    {
                        var overwriteResult = MessageBox.Show(
                            $"A layout named '{layoutName}' already exists. Overwrite it?",
                            "Confirm Overwrite",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (overwriteResult != MessageBoxResult.Yes)
                            return;

                        existingLayout.UpdatedUtc = DateTime.UtcNow;
                        existingLayout.SetConfiguration(config);

                        var success = await ProgressBookLayoutRepository.UpdateAsync(existingLayout);
                        if (success)
                        {
                            _currentLayout = existingLayout;
                            _hasUnsavedChanges = false;
                            await LoadSavedLayoutsAsync();
                            SelectLayoutInDropdown(existingLayout.Id);
                            MessageBox.Show($"Layout '{layoutName}' updated.", "Saved",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        var newLayout = new ProgressBookLayout
                        {
                            Name = layoutName,
                            ProjectId = projectId,
                            CreatedBy = App.CurrentUser.Username,
                            CreatedUtc = DateTime.UtcNow,
                            UpdatedUtc = DateTime.UtcNow
                        };
                        newLayout.SetConfiguration(config);

                        var newId = await ProgressBookLayoutRepository.InsertAsync(newLayout);
                        if (newId > 0)
                        {
                            _currentLayout = newLayout;
                            _hasUnsavedChanges = false;
                            await LoadSavedLayoutsAsync();
                            SelectLayoutInDropdown(newId);
                            MessageBox.Show($"Layout '{layoutName}' saved.", "Saved",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressBooksView.BtnSaveLayout_Click");
                MessageBox.Show($"Error saving layout: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectLayoutInDropdown(int layoutId)
        {
            var items = cboSavedLayouts.ItemsSource as List<LayoutDropdownItem>;
            if (items != null)
            {
                var item = items.FirstOrDefault(i => i.Id == layoutId);
                if (item != null)
                {
                    _isLoading = true;
                    cboSavedLayouts.SelectedItem = item;
                    _isLoading = false;
                }
            }
        }

        private async void BtnRefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnRefreshPreview.IsEnabled = false;
                txtPreviewPlaceholder.Text = "Generating preview...";

                var config = BuildCurrentConfiguration();

                // Get filtered data based on filter column/value
                var username = App.CurrentUser?.Username ?? "";
                var filterColumn = config.FilterField;
                var filterValue = config.FilterValue;

                string whereClause;
                if (!string.IsNullOrEmpty(filterValue))
                {
                    var escapedValue = filterValue.Replace("'", "''");
                    whereClause = $"AssignedTo = '{username}' AND {filterColumn} = '{escapedValue}'";
                }
                else
                {
                    whereClause = $"AssignedTo = '{username}'";
                }

                // Add filter for excluding completed activities
                if (config.ExcludeCompleted)
                {
                    whereClause += " AND PercentEntry < 100";
                }

                var (activities, _) = await ActivityRepository.GetAllActivitiesAsync(whereClause);

                // Take a sample for preview
                var sampleActivities = activities.Take(100).ToList();

                if (sampleActivities.Count == 0)
                {
                    ShowPreviewPlaceholder("No records found for the selected filter.\n\nTry selecting a different value.");
                    return;
                }

                string projectId = sampleActivities.FirstOrDefault()?.ProjectID ?? "Unknown";
                string projectDescription = ProjectCache.GetProjectDescription(projectId);

                // Generate PDF - use filter value as the book name
                var generator = new ProgressBookPdfGenerator();
                var bookName = string.IsNullOrEmpty(filterValue) ? "Preview" : filterValue;
                var pdfDocument = generator.Generate(config, sampleActivities, bookName, projectId, projectDescription);

                _previewStream?.Dispose();
                _previewStream = new MemoryStream();
                pdfDocument.Save(_previewStream);
                pdfDocument.Close(true);

                _previewStream.Position = 0;
                pdfViewer.Load(_previewStream);
                pdfViewer.MinimumZoomPercentage = 10;
                pdfViewer.ZoomMode = Syncfusion.Windows.PdfViewer.ZoomMode.FitWidth;

                pdfViewer.Visibility = Visibility.Visible;
                previewPlaceholderBorder.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressBooksView.BtnRefreshPreview_Click");
                ShowPreviewPlaceholder($"Error generating preview:\n\n{ex.Message}");
            }
            finally
            {
                btnRefreshPreview.IsEnabled = true;
            }
        }

        private void ShowPreviewPlaceholder(string message)
        {
            txtPreviewPlaceholder.Text = message;
            previewPlaceholderBorder.Visibility = Visibility.Visible;
            pdfViewer.Visibility = Visibility.Collapsed;
        }

        private void BtnGenerateBook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = BuildCurrentConfiguration();

                if (string.IsNullOrEmpty(config.FilterValue))
                {
                    MessageBox.Show("Please select a Progress Book value to generate.",
                        "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new GenerateProgressBookDialog(config);
                dialog.Owner = Window.GetWindow(this);
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProgressBooksView.BtnGenerateBook_Click");
                MessageBox.Show($"Error opening generate dialog: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Helper classes
    // Display item for columns list (widths are auto-calculated by PDF generator)
    public class ColumnDisplayItem
    {
        public string FieldName { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
    }

    public class LayoutDropdownItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // ViewModel for Group items in the ItemsControl
    public class GroupItemViewModel : INotifyPropertyChanged
    {
        private string _groupField = string.Empty;

        public int Index { get; set; }

        public string GroupField
        {
            get => _groupField;
            set
            {
                if (_groupField != value)
                {
                    _groupField = value;
                    OnPropertyChanged(nameof(GroupField));
                }
            }
        }

        public bool CanDelete { get; set; }
        public List<string> AvailableFields { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // ViewModel for Sort items in the ItemsControl
    public class SortItemViewModel : INotifyPropertyChanged
    {
        private string _sortField = string.Empty;

        public int Index { get; set; }

        public string SortField
        {
            get => _sortField;
            set
            {
                if (_sortField != value)
                {
                    _sortField = value;
                    OnPropertyChanged(nameof(SortField));
                }
            }
        }

        public bool CanDelete { get; set; }
        public List<string> AvailableFields { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
