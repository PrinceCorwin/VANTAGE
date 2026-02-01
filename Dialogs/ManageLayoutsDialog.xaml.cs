using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.Models;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ManageLayoutsDialog : Window
    {
        private readonly Func<GridLayout> _getCurrentLayout;
        private readonly Action<GridLayout> _applyLayout;
        private readonly Action _resetToDefault;
        private List<string> _layoutNames = new();

        // Result indicating if a layout was applied
        public bool LayoutApplied { get; private set; }
        public string? AppliedLayoutName { get; private set; }

        public ManageLayoutsDialog(Func<GridLayout> getCurrentLayout, Action<GridLayout> applyLayout, Action resetToDefault)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            _getCurrentLayout = getCurrentLayout;
            _applyLayout = applyLayout;
            _resetToDefault = resetToDefault;
            LoadLayoutsList();
        }

        private void LoadLayoutsList()
        {
            _layoutNames = SettingsManager.GetGridLayoutNames();
            lstLayouts.Items.Clear();
            foreach (var name in _layoutNames)
            {
                lstLayouts.Items.Add(name);
            }
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = lstLayouts.SelectedIndex >= 0;
            btnApply.IsEnabled = hasSelection;
            btnRename.IsEnabled = hasSelection;
            btnDelete.IsEnabled = hasSelection;
        }

        private void LstLayouts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (lstLayouts.SelectedIndex < 0)
                return;

            var layoutName = _layoutNames[lstLayouts.SelectedIndex];

            var result = MessageBox.Show(
                $"Current grid layout will be replaced by '{layoutName}'.\n\nClick Cancel to save your current layout first.",
                "Apply Layout",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.OK)
                return;

            // Load and apply the selected layout
            var layout = SettingsManager.GetGridLayout(layoutName);
            if (layout != null)
            {
                _applyLayout(layout);
                SettingsManager.SetActiveLayoutName(layoutName);
                LayoutApplied = true;
                AppliedLayoutName = layoutName;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Failed to load layout.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            if (lstLayouts.SelectedIndex < 0)
                return;

            var oldName = _layoutNames[lstLayouts.SelectedIndex];
            var newName = PromptForLayoutName("Rename Layout", $"Enter new name for '{oldName}':", oldName);

            if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
                return;

            // Check for duplicate
            if (_layoutNames.Contains(newName))
            {
                MessageBox.Show("A layout with this name already exists.", "Duplicate Name",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Load old layout, update name, save as new, delete old
            var layout = SettingsManager.GetGridLayout(oldName);
            if (layout != null)
            {
                layout.Name = newName;
                SettingsManager.SaveGridLayout(layout);
                SettingsManager.DeleteGridLayout(oldName);

                // Update index
                var index = _layoutNames.IndexOf(oldName);
                _layoutNames[index] = newName;
                SettingsManager.SaveGridLayoutNames(_layoutNames);

                // Update active layout if it was renamed
                if (SettingsManager.GetActiveLayoutName() == oldName)
                {
                    SettingsManager.SetActiveLayoutName(newName);
                }

                LoadLayoutsList();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (lstLayouts.SelectedIndex < 0)
                return;

            var layoutName = _layoutNames[lstLayouts.SelectedIndex];
            var result = MessageBox.Show($"Delete layout '{layoutName}'?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SettingsManager.DeleteGridLayout(layoutName);

                // Clear active layout if it was deleted
                if (SettingsManager.GetActiveLayoutName() == layoutName)
                {
                    SettingsManager.SetActiveLayoutName(string.Empty);
                }

                LoadLayoutsList();
            }
        }

        private void BtnSaveNew_Click(object sender, RoutedEventArgs e)
        {
            var layoutName = txtNewLayoutName.Text.Trim();
            if (string.IsNullOrWhiteSpace(layoutName))
            {
                MessageBox.Show("Please enter a layout name.", "Name Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNewLayoutName.Focus();
                return;
            }

            if (SaveNewLayout(layoutName))
            {
                txtNewLayoutName.Clear();
                LoadLayoutsList();
                MessageBox.Show($"Layout '{layoutName}' saved.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private bool SaveNewLayout(string layoutName)
        {
            // Check limit
            if (_layoutNames.Count >= SettingsManager.MaxLayouts)
            {
                MessageBox.Show($"Maximum of {SettingsManager.MaxLayouts} layouts allowed. Delete one first.",
                    "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Check for duplicate
            if (_layoutNames.Contains(layoutName))
            {
                MessageBox.Show("A layout with this name already exists.", "Duplicate Name",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Gather current layout
            var layout = _getCurrentLayout();
            layout.Name = layoutName;

            // Save
            SettingsManager.SaveGridLayout(layout);
            _layoutNames.Add(layoutName);
            SettingsManager.SaveGridLayoutNames(_layoutNames);
            SettingsManager.SetActiveLayoutName(layoutName);

            return true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnDefault_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset grid layouts to application defaults?\n\nYour saved layouts will not be affected.",
                "Reset to Default",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.OK)
                return;

            // Clear active layout since we're going back to defaults
            SettingsManager.SetActiveLayoutName(string.Empty);

            _resetToDefault();
            DialogResult = true;
            Close();
        }

        private string? PromptForLayoutName(string title, string prompt, string defaultValue = "")
        {
            // Simple input dialog using MessageBox input
            // WPF doesn't have a built-in input dialog, so we use a simple approach
            var dialog = new Window
            {
                Title = title,
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["BackgroundColor"]
            };

            Syncfusion.SfSkinManager.SfSkinManager.SetTheme(dialog,
                new Syncfusion.SfSkinManager.Theme(ThemeManager.GetSyncfusionThemeName()));

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = prompt,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"]
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = new TextBox
            {
                Text = defaultValue,
                Height = 28,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["ControlBorder"],
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0)
            };
            Grid.SetRow(textBox, 2);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(buttonPanel, 4);

            string? result = null;

            var okButton = new Button
            {
                Content = "OK",
                Width = 70,
                Height = 28,
                Margin = new Thickness(0, 0, 10, 0),
                Background = (System.Windows.Media.Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["ControlBorder"]
            };
            okButton.Click += (s, args) =>
            {
                result = textBox.Text.Trim();
                dialog.DialogResult = true;
                dialog.Close();
            };
            buttonPanel.Children.Add(okButton);

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 70,
                Height = 28,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["ControlBackground"],
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["ControlBorder"]
            };
            cancelButton.Click += (s, args) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(buttonPanel);
            dialog.Content = grid;

            textBox.SelectAll();
            textBox.Focus();

            if (dialog.ShowDialog() == true)
                return result;

            return null;
        }
    }
}
