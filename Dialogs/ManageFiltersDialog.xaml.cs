using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.Models;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ManageFiltersDialog : Window
    {
        private const string FiltersSettingKey = "UserFilters.Progress";
        private const int MaxConditions = 5;

        private List<UserFilter> _filters = new();
        private List<string> _availableColumns;
        private UserFilter? _currentFilter;
        private bool _isNewFilter;

        public ManageFiltersDialog(List<string> availableColumns)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            _availableColumns = availableColumns.OrderBy(c => c).ToList();
            LoadFilters();
            RefreshFilterList();
        }

        private void LoadFilters()
        {
            try
            {
                var json = SettingsManager.GetUserSetting(FiltersSettingKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    _filters = JsonSerializer.Deserialize<List<UserFilter>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageFiltersDialog.LoadFilters");
                _filters = new();
            }
        }

        private void SaveFiltersToSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_filters);
                SettingsManager.SetUserSetting(FiltersSettingKey, json, "json");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageFiltersDialog.SaveFiltersToSettings");
            }
        }

        private void RefreshFilterList()
        {
            lstFilters.Items.Clear();
            foreach (var filter in _filters)
            {
                lstFilters.Items.Add(filter.Name);
            }
        }

        private void LstFilters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstFilters.SelectedIndex < 0)
            {
                ClearEditor();
                return;
            }

            _currentFilter = _filters[lstFilters.SelectedIndex];
            _isNewFilter = false;
            LoadFilterIntoEditor(_currentFilter);
        }

        private void LoadFilterIntoEditor(UserFilter filter)
        {
            txtFilterName.Text = filter.Name;
            pnlConditions.Children.Clear();

            foreach (var condition in filter.Conditions)
            {
                AddConditionRow(condition);
            }

            // Ensure at least one condition row
            if (filter.Conditions.Count == 0)
            {
                AddConditionRow(null);
            }
        }

        private void ClearEditor()
        {
            txtFilterName.Text = string.Empty;
            pnlConditions.Children.Clear();
            _currentFilter = null;
        }

        private void AddConditionRow(FilterCondition? condition)
        {
            if (pnlConditions.Children.Count >= MaxConditions)
            {
                MessageBox.Show($"Maximum of {MaxConditions} conditions allowed.", "Limit Reached",
                    MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Column dropdown
            var cboColumn = new ComboBox
            {
                Height = 28,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["ControlBorder"]
            };
            foreach (var col in _availableColumns)
            {
                cboColumn.Items.Add(col);
            }
            if (condition != null && !string.IsNullOrEmpty(condition.Column))
            {
                cboColumn.SelectedItem = condition.Column;
            }
            Grid.SetColumn(cboColumn, 0);
            row.Children.Add(cboColumn);

            // Criteria dropdown
            var cboCriteria = new ComboBox
            {
                Height = 28,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["ControlBorder"]
            };
            foreach (var criteria in FilterCriteria.AllCriteria)
            {
                cboCriteria.Items.Add(criteria);
            }
            if (condition != null && !string.IsNullOrEmpty(condition.Criteria))
            {
                cboCriteria.SelectedItem = condition.Criteria;
            }
            Grid.SetColumn(cboCriteria, 2);
            row.Children.Add(cboCriteria);

            // Value textbox
            var txtValue = new TextBox
            {
                Height = 28,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["ControlBorder"],
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0),
                Text = condition?.Value ?? string.Empty
            };
            Grid.SetColumn(txtValue, 4);
            row.Children.Add(txtValue);

            // Logic dropdown (AND/OR) - only show if not first row
            var cboLogic = new ComboBox
            {
                Height = 28,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["ControlBorder"],
                Visibility = pnlConditions.Children.Count > 0 ? Visibility.Visible : Visibility.Hidden
            };
            cboLogic.Items.Add("AND");
            cboLogic.Items.Add("OR");
            cboLogic.SelectedItem = condition?.LogicOperator ?? "AND";
            Grid.SetColumn(cboLogic, 6);
            row.Children.Add(cboLogic);

            // Delete button
            var btnDelete = new Button
            {
                Content = "X",
                Width = 24,
                Height = 24,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["StatusRed"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["ControlBorder"],
                Tag = row
            };
            btnDelete.Click += BtnDeleteCondition_Click;
            Grid.SetColumn(btnDelete, 8);
            row.Children.Add(btnDelete);

            pnlConditions.Children.Add(row);
        }

        private void BtnDeleteCondition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Grid row)
            {
                pnlConditions.Children.Remove(row);

                // Update visibility of first row's logic dropdown
                if (pnlConditions.Children.Count > 0 && pnlConditions.Children[0] is Grid firstRow)
                {
                    foreach (var child in firstRow.Children)
                    {
                        if (child is ComboBox cbo && (cbo.Items.Contains("AND") || cbo.Items.Contains("OR")))
                        {
                            cbo.Visibility = Visibility.Hidden;
                            break;
                        }
                    }
                }
            }
        }

        private void BtnAddCondition_Click(object sender, RoutedEventArgs e)
        {
            AddConditionRow(null);
        }

        private void BtnNewFilter_Click(object sender, RoutedEventArgs e)
        {
            lstFilters.SelectedIndex = -1;
            _currentFilter = new UserFilter();
            _isNewFilter = true;
            txtFilterName.Text = string.Empty;
            pnlConditions.Children.Clear();
            AddConditionRow(null);
            txtFilterName.Focus();
        }

        private void BtnDeleteFilter_Click(object sender, RoutedEventArgs e)
        {
            if (lstFilters.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a filter to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            var filterName = _filters[lstFilters.SelectedIndex].Name;
            var result = MessageBox.Show($"Delete filter '{filterName}'?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _filters.RemoveAt(lstFilters.SelectedIndex);
                SaveFiltersToSettings();
                RefreshFilterList();
                ClearEditor();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var filterName = txtFilterName.Text.Trim();
            if (string.IsNullOrEmpty(filterName))
            {
                MessageBox.Show("Please enter a filter name.", "Name Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtFilterName.Focus();
                return;
            }

            // Check for duplicate name (except current filter)
            var existingIndex = _filters.FindIndex(f => f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0 && (_isNewFilter || _filters[existingIndex] != _currentFilter))
            {
                MessageBox.Show("A filter with this name already exists.", "Duplicate Name",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build conditions from UI
            var conditions = new List<FilterCondition>();
            foreach (var child in pnlConditions.Children)
            {
                if (child is Grid row)
                {
                    var condition = new FilterCondition();
                    foreach (var element in row.Children)
                    {
                        var col = Grid.GetColumn((UIElement)element);
                        switch (col)
                        {
                            case 0 when element is ComboBox cboCol:
                                condition.Column = cboCol.SelectedItem?.ToString() ?? string.Empty;
                                break;
                            case 2 when element is ComboBox cboCrit:
                                condition.Criteria = cboCrit.SelectedItem?.ToString() ?? string.Empty;
                                break;
                            case 4 when element is TextBox txtVal:
                                condition.Value = txtVal.Text;
                                break;
                            case 6 when element is ComboBox cboLogic:
                                condition.LogicOperator = cboLogic.SelectedItem?.ToString() ?? "AND";
                                break;
                        }
                    }

                    // Only add if column and criteria are selected
                    if (!string.IsNullOrEmpty(condition.Column) && !string.IsNullOrEmpty(condition.Criteria))
                    {
                        conditions.Add(condition);
                    }
                }
            }
            if (conditions.Count == 0)
            {
                MessageBox.Show("Please add at least one valid condition.", "No Conditions",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Save filter (treat as new if no current filter is set)
            if (_isNewFilter || _currentFilter == null)
            {
                _currentFilter = new UserFilter { Name = filterName, Conditions = conditions };
                _filters.Add(_currentFilter);
            }
            else
            {
                _currentFilter.Name = filterName;
                _currentFilter.Conditions = conditions;
            }

            SaveFiltersToSettings();
            RefreshFilterList();
            _isNewFilter = false;

            // Select the saved filter by index
            var savedIndex = _filters.FindIndex(f => f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));
            if (savedIndex >= 0)
                lstFilters.SelectedIndex = savedIndex;

            MessageBox.Show("Filter saved.", "Success", MessageBoxButton.OK, MessageBoxImage.None);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        public static List<UserFilter> GetSavedFilters()
        {
            try
            {
                var json = SettingsManager.GetUserSetting(FiltersSettingKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    return JsonSerializer.Deserialize<List<UserFilter>>(json) ?? new();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ManageFiltersDialog.GetSavedFilters");
            }
            return new();
        }
    }
}
