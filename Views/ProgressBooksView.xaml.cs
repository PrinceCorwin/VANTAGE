using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VANTAGE.Models.ProgressBook;
using VANTAGE.Services.ProgressBook;
using VANTAGE.Utilities;

namespace VANTAGE.Views
{
    public partial class ProgressBooksView : UserControl
    {
        // Currently loaded layout (null = new unsaved layout)
        private ProgressBookLayout? _currentLayout;

        // Observable collections for UI binding
        private ObservableCollection<ColumnDisplayItem> _columns = new();
        private ObservableCollection<SubGroupDisplayItem> _subGroups = new();

        // Available fields for grouping and columns
        private List<string> _allFields = new();
        private List<string> _commonFields = new() { "PhaseCode", "Area", "UDF2", "TagNO", "Commodity", "SystemNO" };

        // Flag to prevent recursive updates
        private bool _isLoading;

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
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Initialize the list of available Activity fields
        private void InitializeFieldLists()
        {
            // Fields available for grouping and columns (from Activity model)
            _allFields = new List<string>
            {
                "ActivityID", "Area", "ChgOrdNO", "CompType", "Description", "DwgNO",
                "EqmtNO", "HtTrace", "InsulType", "LineNumber", "MtrlSpec", "Notes",
                "PaintCode", "PhaseCategory", "PhaseCode", "PipeGrade", "ProjectID",
                "RespParty", "RevNO", "RFINO", "ROCStep", "SchedActNO", "SecondDwgNO",
                "Service", "ShopField", "ShtNO", "SubArea", "PjtSystem", "SystemNO",
                "TagNO", "UDF1", "UDF2", "UDF3", "UDF4", "UDF5", "UDF6", "UDF7",
                "UDF8", "UDF9", "UDF10", "WorkPackage"
            };
            _allFields.Sort();
        }

        // Populate the main group and sort dropdowns
        private void PopulateDropdowns()
        {
            // Main group dropdown - common fields first with star, then all others
            var mainGroupItems = new List<string>();
            foreach (var field in _commonFields)
            {
                mainGroupItems.Add($"★ {field}");
            }
            mainGroupItems.Add("───────────");
            mainGroupItems.AddRange(_allFields);

            cboMainGroup.ItemsSource = mainGroupItems;
            cboMainGroup.SelectedIndex = 0; // Default to PhaseCode

            // Sort dropdown - all fields
            cboMainSort.ItemsSource = _allFields;
            cboMainSort.SelectedItem = "Description";
        }

        // Load saved layouts into the dropdown
        private async System.Threading.Tasks.Task LoadSavedLayoutsAsync()
        {
            if (App.CurrentUser == null) return;

            var layouts = await ProgressBookLayoutRepository.GetAllForUserAsync(App.CurrentUser.Username);
            var items = new List<LayoutDropdownItem>
            {
                new LayoutDropdownItem { Id = 0, Name = "(New Layout)" }
            };
            items.AddRange(layouts.Select(l => new LayoutDropdownItem { Id = l.Id, Name = l.Name }));

            cboSavedLayouts.ItemsSource = items;
            cboSavedLayouts.DisplayMemberPath = "Name";
            cboSavedLayouts.SelectedIndex = 0;
        }

        // Load default configuration for a new layout
        private void LoadDefaultConfiguration()
        {
            _currentLayout = null;
            txtLayoutName.Text = string.Empty;
            rbLetter.IsChecked = true;
            sliderFontSize.Value = 10;

            // Default columns: ROC and Description (required)
            _columns.Clear();
            _columns.Add(new ColumnDisplayItem { FieldName = "ROCStep", Width = 15, IsRequired = true });
            _columns.Add(new ColumnDisplayItem { FieldName = "Description", Width = 60, IsRequired = true });
            RefreshColumnsListBox();

            // No sub-groups by default
            _subGroups.Clear();
            RefreshSubGroupsListBox();

            btnDeleteLayout.IsEnabled = false;
            UpdateZone2Summary();
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

                // Paper size
                rbLetter.IsChecked = config.PaperSize == PaperSize.Letter;
                rbTabloid.IsChecked = config.PaperSize == PaperSize.Tabloid;

                // Font size
                sliderFontSize.Value = config.FontSize;

                // Main grouping
                SetMainGroupSelection(config.MainGroupField);
                cboMainSort.SelectedItem = config.MainGroupSortField;

                // Columns
                _columns.Clear();
                foreach (var col in config.Columns.OrderBy(c => c.DisplayOrder))
                {
                    bool isRequired = col.FieldName == "ROCStep" || col.FieldName == "Description";
                    _columns.Add(new ColumnDisplayItem
                    {
                        FieldName = col.FieldName,
                        Width = col.Width,
                        IsRequired = isRequired
                    });
                }
                RefreshColumnsListBox();

                // Sub-groups
                _subGroups.Clear();
                foreach (var sg in config.SubGroups)
                {
                    _subGroups.Add(new SubGroupDisplayItem
                    {
                        GroupField = sg.GroupField,
                        SortField = sg.SortField
                    });
                }
                RefreshSubGroupsListBox();

                btnDeleteLayout.IsEnabled = true;
                UpdateZone2Summary();
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Set the main group dropdown to the specified field
        private void SetMainGroupSelection(string fieldName)
        {
            // Try to find the starred version first
            var starredVersion = $"★ {fieldName}";
            var items = cboMainGroup.ItemsSource as List<string>;
            if (items != null)
            {
                var starredIndex = items.IndexOf(starredVersion);
                if (starredIndex >= 0)
                {
                    cboMainGroup.SelectedIndex = starredIndex;
                    return;
                }
                var normalIndex = items.IndexOf(fieldName);
                if (normalIndex >= 0)
                {
                    cboMainGroup.SelectedIndex = normalIndex;
                }
            }
        }

        // Get the currently selected main group field (without the star)
        private string GetSelectedMainGroupField()
        {
            var selected = cboMainGroup.SelectedItem as string;
            if (string.IsNullOrEmpty(selected) || selected.StartsWith("──"))
                return "PhaseCode"; // Default

            return selected.Replace("★ ", "");
        }

        // Build the current configuration from UI controls
        private ProgressBookConfiguration BuildCurrentConfiguration()
        {
            var config = new ProgressBookConfiguration
            {
                PaperSize = rbLetter.IsChecked == true ? PaperSize.Letter : PaperSize.Tabloid,
                FontSize = (int)sliderFontSize.Value,
                MainGroupField = GetSelectedMainGroupField(),
                MainGroupSortField = cboMainSort.SelectedItem as string ?? "Description"
            };

            // Columns with order
            int order = 0;
            foreach (var col in _columns)
            {
                config.Columns.Add(new ColumnConfig
                {
                    FieldName = col.FieldName,
                    Width = col.Width,
                    DisplayOrder = order++
                });
            }

            // Sub-groups
            foreach (var sg in _subGroups)
            {
                config.SubGroups.Add(new SubGroupConfig
                {
                    GroupField = sg.GroupField,
                    SortField = sg.SortField
                });
            }

            return config;
        }

        // Refresh the columns ListBox display
        private void RefreshColumnsListBox()
        {
            lstColumns.Items.Clear();
            foreach (var col in _columns)
            {
                var item = CreateColumnListItem(col);
                lstColumns.Items.Add(item);
            }
        }

        // Create a ListBox item for a column
        private Grid CreateColumnListItem(ColumnDisplayItem col)
        {
            var grid = new Grid { Tag = col, Height = 32, Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            // Drag handle
            var dragHandle = new TextBlock
            {
                Text = "≡",
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.SizeAll,
                Foreground = (System.Windows.Media.Brush)FindResource("TextColorSecondary")
            };
            Grid.SetColumn(dragHandle, 0);
            grid.Children.Add(dragHandle);

            // Field name
            var fieldText = new TextBlock
            {
                Text = col.FieldName + (col.IsRequired ? " *" : ""),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("ForegroundColor")
            };
            Grid.SetColumn(fieldText, 1);
            grid.Children.Add(fieldText);

            // Width input
            var widthBox = new TextBox
            {
                Text = col.Width.ToString(),
                Width = 50,
                Height = 24,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Tag = col
            };
            widthBox.LostFocus += WidthBox_LostFocus;
            Grid.SetColumn(widthBox, 2);
            grid.Children.Add(widthBox);

            // Remove button (disabled for required columns)
            if (!col.IsRequired)
            {
                var removeBtn = new Button
                {
                    Content = "✕",
                    Width = 24,
                    Height = 24,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Foreground = (System.Windows.Media.Brush)FindResource("StatusRed"),
                    BorderThickness = new Thickness(0),
                    Tag = col,
                    ToolTip = "Remove column"
                };
                removeBtn.Click += RemoveColumn_Click;
                Grid.SetColumn(removeBtn, 3);
                grid.Children.Add(removeBtn);
            }

            return grid;
        }

        // Width textbox lost focus - validate and update
        private void WidthBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is ColumnDisplayItem col)
            {
                if (int.TryParse(tb.Text, out int width) && width >= 1 && width <= 100)
                {
                    col.Width = width;
                }
                else
                {
                    tb.Text = col.Width.ToString();
                }
                UpdateZone2Summary();
            }
        }

        // Remove column button click
        private void RemoveColumn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ColumnDisplayItem col)
            {
                _columns.Remove(col);
                RefreshColumnsListBox();
                UpdateZone2Summary();
            }
        }

        // Refresh the sub-groups ListBox display
        private void RefreshSubGroupsListBox()
        {
            lstSubGroups.Items.Clear();
            foreach (var sg in _subGroups)
            {
                var item = CreateSubGroupListItem(sg);
                lstSubGroups.Items.Add(item);
            }
        }

        // Create a ListBox item for a sub-group
        private Grid CreateSubGroupListItem(SubGroupDisplayItem sg)
        {
            var grid = new Grid { Tag = sg, Height = 32, Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

            // Group field dropdown
            var groupCombo = new ComboBox
            {
                ItemsSource = _columns.Select(c => c.FieldName).ToList(),
                SelectedItem = sg.GroupField,
                Height = 26,
                Tag = sg
            };
            groupCombo.SelectionChanged += SubGroupCombo_Changed;
            Grid.SetColumn(groupCombo, 0);
            grid.Children.Add(groupCombo);

            // Sort field dropdown
            var sortCombo = new ComboBox
            {
                ItemsSource = _columns.Select(c => c.FieldName).ToList(),
                SelectedItem = sg.SortField,
                Height = 26,
                Tag = sg
            };
            sortCombo.SelectionChanged += SubGroupSortCombo_Changed;
            Grid.SetColumn(sortCombo, 2);
            grid.Children.Add(sortCombo);

            // Remove button
            var removeBtn = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = (System.Windows.Media.Brush)FindResource("StatusRed"),
                BorderThickness = new Thickness(0),
                Tag = sg,
                ToolTip = "Remove sub-group"
            };
            removeBtn.Click += RemoveSubGroup_Click;
            Grid.SetColumn(removeBtn, 3);
            grid.Children.Add(removeBtn);

            return grid;
        }

        private void SubGroupCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.Tag is SubGroupDisplayItem sg)
            {
                sg.GroupField = combo.SelectedItem as string ?? "";
            }
        }

        private void SubGroupSortCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.Tag is SubGroupDisplayItem sg)
            {
                sg.SortField = combo.SelectedItem as string ?? "";
            }
        }

        private void RemoveSubGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SubGroupDisplayItem sg)
            {
                _subGroups.Remove(sg);
                RefreshSubGroupsListBox();
            }
        }

        // Update the Zone 2 summary text
        private void UpdateZone2Summary()
        {
            var columnNames = string.Join(" | ", _columns.Select(c => c.FieldName));
            txtZone2Summary.Text = $"Zone 2 (45%): {columnNames}";
        }

        // Event Handlers

        private async void CboSavedLayouts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

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
            if (txtFontSizeDisplay == null) return;

            int fontSize = (int)sliderFontSize.Value;
            txtFontSizeDisplay.Text = $"{fontSize}pt";
            if (txtDescFontNote != null)
            {
                txtDescFontNote.Text = $"(DESC column will render at {fontSize - 1}pt)";
            }
        }

        private void BtnAddSubGroup_Click(object sender, RoutedEventArgs e)
        {
            if (_columns.Count == 0)
            {
                MessageBox.Show("Add columns to Zone 2 first before creating sub-groups.",
                    "No Columns", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var firstColumn = _columns.First().FieldName;
            _subGroups.Add(new SubGroupDisplayItem
            {
                GroupField = firstColumn,
                SortField = firstColumn
            });
            RefreshSubGroupsListBox();
        }

        private void BtnAddColumn_Click(object sender, RoutedEventArgs e)
        {
            // Show dialog to select a field
            var availableFields = _allFields.Where(f =>
                !_columns.Any(c => c.FieldName == f) &&
                f != "ROCStep" && f != "Description").ToList();

            if (availableFields.Count == 0)
            {
                MessageBox.Show("All available fields have been added.",
                    "No More Fields", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Simple input dialog - select from list
            var dialog = new Dialogs.SelectFieldDialog(availableFields);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedField))
            {
                _columns.Add(new ColumnDisplayItem
                {
                    FieldName = dialog.SelectedField,
                    Width = 10,
                    IsRequired = false
                });
                RefreshColumnsListBox();
                UpdateZone2Summary();
            }
        }

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

            if (App.CurrentUser == null)
            {
                MessageBox.Show("Please ensure you are logged in.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Use "Global" as project context for layouts (user-scoped, not project-scoped)
            string projectId = "Global";

            try
            {
                var config = BuildCurrentConfiguration();

                if (_currentLayout != null)
                {
                    // Update existing layout
                    _currentLayout.Name = layoutName;
                    _currentLayout.UpdatedUtc = DateTime.UtcNow;
                    _currentLayout.SetConfiguration(config);

                    var success = await ProgressBookLayoutRepository.UpdateAsync(_currentLayout);
                    if (success)
                    {
                        await LoadSavedLayoutsAsync();
                        SelectLayoutInDropdown(_currentLayout.Id);
                        MessageBox.Show($"Layout '{layoutName}' updated.", "Saved",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // Check for duplicate name
                    if (await ProgressBookLayoutRepository.LayoutExistsAsync(layoutName, projectId))
                    {
                        MessageBox.Show($"A layout named '{layoutName}' already exists.",
                            "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Create new layout
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
                        await LoadSavedLayoutsAsync();
                        SelectLayoutInDropdown(newId);
                        btnDeleteLayout.IsEnabled = true;
                        MessageBox.Show($"Layout '{layoutName}' saved.", "Saved",
                            MessageBoxButton.OK, MessageBoxImage.Information);
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

        private async void BtnDeleteLayout_Click(object sender, RoutedEventArgs e)
        {
            if (_currentLayout == null) return;

            var result = MessageBox.Show($"Delete layout '{_currentLayout.Name}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var success = await ProgressBookLayoutRepository.DeleteAsync(_currentLayout.Id);
                if (success)
                {
                    await LoadSavedLayoutsAsync();
                    LoadDefaultConfiguration();
                    cboSavedLayouts.SelectedIndex = 0;
                }
            }
        }

        private void BtnRefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            // Phase 5 will implement PDF preview generation
            MessageBox.Show("Preview generation will be implemented in Phase 5.",
                "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnGenerateBook_Click(object sender, RoutedEventArgs e)
        {
            // Phase 6 will implement the generate dialog
            MessageBox.Show("Progress Book generation will be implemented in Phase 6.",
                "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Helper classes for UI binding
    public class ColumnDisplayItem
    {
        public string FieldName { get; set; } = string.Empty;
        public int Width { get; set; } = 10;
        public bool IsRequired { get; set; }
    }

    public class SubGroupDisplayItem
    {
        public string GroupField { get; set; } = string.Empty;
        public string SortField { get; set; } = string.Empty;
    }

    public class LayoutDropdownItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
