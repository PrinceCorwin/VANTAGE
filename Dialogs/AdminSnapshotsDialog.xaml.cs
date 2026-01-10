using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using VANTAGE.Data;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class AdminSnapshotsDialog : Window
    {
        private List<SnapshotGroupItem> _groups = new();

        public AdminSnapshotsDialog()
        {
            InitializeComponent();
            Loaded += AdminSnapshotsDialog_Loaded;
        }

        private async void AdminSnapshotsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSnapshotsAsync();
        }

        private async System.Threading.Tasks.Task LoadSnapshotsAsync()
        {
            pnlLoading.Visibility = Visibility.Visible;
            lvSnapshots.Visibility = Visibility.Collapsed;
            txtNoSnapshots.Visibility = Visibility.Collapsed;

            try
            {
                _groups = await System.Threading.Tasks.Task.Run(() =>
                {
                    var groupList = new List<SnapshotGroupItem>();

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT AssignedTo, ProjectID, WeekEndDate, COUNT(*) as SnapshotCount
                        FROM ProgressSnapshots
                        GROUP BY AssignedTo, ProjectID, WeekEndDate
                        ORDER BY AssignedTo, ProjectID, WeekEndDate DESC";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string username = reader.IsDBNull(0) ? "(Unknown)" : reader.GetString(0);
                        string projectId = reader.IsDBNull(1) ? "(Unknown)" : reader.GetString(1);
                        string weekEndDateStr = reader.GetString(2);
                        int count = reader.GetInt32(3);

                        if (DateTime.TryParse(weekEndDateStr, out DateTime weekEndDate))
                        {
                            var item = new SnapshotGroupItem
                            {
                                Username = username,
                                ProjectID = projectId,
                                WeekEndDate = weekEndDate,
                                WeekEndDateStr = weekEndDateStr,
                                SnapshotCount = count
                            };
                            item.PropertyChanged += GroupItem_PropertyChanged;
                            groupList.Add(item);
                        }
                    }

                    return groupList;
                });

                pnlLoading.Visibility = Visibility.Collapsed;

                if (_groups.Count == 0)
                {
                    txtNoSnapshots.Visibility = Visibility.Visible;
                    btnDelete.IsEnabled = false;
                    btnDeleteAll.IsEnabled = false;
                }
                else
                {
                    lvSnapshots.ItemsSource = _groups;
                    lvSnapshots.Visibility = Visibility.Visible;
                    btnDeleteAll.IsEnabled = true;
                }

                UpdateSelectionSummary();
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "AdminSnapshotsDialog.LoadSnapshotsAsync");
                MessageBox.Show($"Error loading snapshots:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GroupItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SnapshotGroupItem.IsSelected))
            {
                UpdateSelectionSummary();
            }
        }

        private void UpdateSelectionSummary()
        {
            var selectedGroups = _groups.Where(g => g.IsSelected).ToList();
            int groupCount = selectedGroups.Count;
            int snapshotCount = selectedGroups.Sum(g => g.SnapshotCount);

            txtSelectionSummary.Text = $"{groupCount} group(s) selected ({snapshotCount} snapshots)";
            btnDelete.IsEnabled = groupCount > 0;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var group in _groups)
            {
                group.IsSelected = true;
            }
        }

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var group in _groups)
            {
                group.IsSelected = false;
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedGroups = _groups.Where(g => g.IsSelected).ToList();

            if (selectedGroups.Count == 0)
            {
                MessageBox.Show("Please select at least one group to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int totalSnapshots = selectedGroups.Sum(g => g.SnapshotCount);

            var groupSummary = selectedGroups
                .GroupBy(g => g.Username)
                .Select(u => $"  {u.Key}: {u.Sum(g => g.SnapshotCount)} snapshots")
                .ToList();

            string summaryText = string.Join("\n", groupSummary);

            var confirmResult = MessageBox.Show(
                $"Are you sure you want to delete {totalSnapshots} snapshot(s)?\n\n" +
                $"By user:\n{summaryText}\n\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            btnDelete.IsEnabled = false;
            btnDeleteAll.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                int deletedTotal = await System.Threading.Tasks.Task.Run(() =>
                {
                    int deleted = 0;

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    foreach (var group in selectedGroups)
                    {
                        var cmd = azureConn.CreateCommand();
                        cmd.CommandText = @"
                            DELETE FROM ProgressSnapshots
                            WHERE AssignedTo = @username
                              AND ProjectID = @projectId
                              AND WeekEndDate = @weekEndDate";
                        cmd.Parameters.AddWithValue("@username", group.Username);
                        cmd.Parameters.AddWithValue("@projectId", group.ProjectID);
                        cmd.Parameters.AddWithValue("@weekEndDate", group.WeekEndDateStr);

                        deleted += cmd.ExecuteNonQuery();
                    }

                    return deleted;
                });

                AppLogger.Info(
                    $"Admin deleted {deletedTotal} snapshots from {selectedGroups.Count} group(s)",
                    "AdminSnapshotsDialog.BtnDelete_Click",
                    App.CurrentUser?.Username);

                MessageBox.Show(
                    $"Successfully deleted {deletedTotal} snapshot(s).",
                    "Delete Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Reload the list
                await LoadSnapshotsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminSnapshotsDialog.BtnDelete_Click");
                MessageBox.Show($"Error deleting snapshots:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCancel.IsEnabled = true;
            }
        }

        private async void BtnDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            int totalSnapshots = _groups.Sum(g => g.SnapshotCount);

            var confirmResult = MessageBox.Show(
                $"⚠️ DANGER: This will delete ALL {totalSnapshots} snapshot(s) from ALL users!\n\n" +
                "This action cannot be undone.\n\n" +
                "Are you absolutely sure?",
                "Delete All Snapshots",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
                return;

            // Second confirmation - type DELETE
            if (!ShowDeleteConfirmation())
                return;

            btnDelete.IsEnabled = false;
            btnDeleteAll.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                int deletedTotal = await System.Threading.Tasks.Task.Run(() =>
                {
                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = "DELETE FROM ProgressSnapshots";
                    return cmd.ExecuteNonQuery();
                });

                AppLogger.Info(
                    $"Admin deleted ALL snapshots: {deletedTotal} total",
                    "AdminSnapshotsDialog.BtnDeleteAll_Click",
                    App.CurrentUser?.Username);

                MessageBox.Show(
                    $"Successfully deleted all {deletedTotal} snapshot(s).",
                    "Delete Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Reload the list
                await LoadSnapshotsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminSnapshotsDialog.BtnDeleteAll_Click");
                MessageBox.Show($"Error deleting snapshots:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCancel.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private bool ShowDeleteConfirmation()
        {
            var confirmDialog = new Window
            {
                Title = "Confirm Delete All",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = ThemeHelper.BackgroundColor,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Type DELETE to confirm:",
                Foreground = ThemeHelper.ForegroundColor,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var textBox = new System.Windows.Controls.TextBox
            {
                Height = 30,
                FontSize = 14,
                Background = ThemeHelper.ControlBackground,
                Foreground = ThemeHelper.ForegroundColor,
                BorderBrush = ThemeHelper.SidebarBorder,
                Padding = new Thickness(5)
            };
            stack.Children.Add(textBox);

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            bool confirmed = false;

            var btnCancel = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Background = ThemeHelper.SidebarBorder,
                Foreground = ThemeHelper.ForegroundColor
            };
            btnCancel.Click += (s, args) => confirmDialog.Close();

            var btnConfirm = new System.Windows.Controls.Button
            {
                Content = "Confirm",
                Width = 80,
                Height = 30,
                Background = ThemeHelper.ButtonDangerBackground,
                Foreground = ThemeHelper.ForegroundColor
            };
            btnConfirm.Click += (s, args) =>
            {
                if (textBox.Text == "DELETE")
                {
                    confirmed = true;
                    confirmDialog.Close();
                }
                else
                {
                    MessageBox.Show("You must type DELETE exactly.", "Invalid",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnConfirm);
            stack.Children.Add(btnPanel);

            confirmDialog.Content = stack;
            confirmDialog.ShowDialog();

            return confirmed;
        }
    }

    // Model for snapshot group
    public class SnapshotGroupItem : INotifyPropertyChanged
    {
        public string Username { get; set; } = string.Empty;
        public string ProjectID { get; set; } = string.Empty;
        public DateTime WeekEndDate { get; set; }
        public string WeekEndDateStr { get; set; } = string.Empty;
        public int SnapshotCount { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public string WeekEndDateDisplay => WeekEndDate.ToString("MM/dd/yyyy");

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}