using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class FeedbackDialog : Window
    {
        private ObservableCollection<FeedbackItem> _allFeedback = new();
        private ObservableCollection<FeedbackItem> _filteredFeedback = new();
        private FeedbackItem? _selectedFeedback;
        private bool _isAdmin;

        public FeedbackDialog()
        {
            InitializeComponent();
            _isAdmin = AzureDbManager.IsUserAdmin(App.CurrentUser?.Username ?? "");

            // Enable admin features
            if (_isAdmin)
            {
                cboStatus.IsEnabled = true;
                txtStatusNote.Visibility = Visibility.Collapsed;
                btnDelete.Visibility = Visibility.Visible;
                chkShowDeleted.Visibility = Visibility.Visible;
            }

            Loaded += FeedbackDialog_Loaded;
        }

        private async void FeedbackDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-purge deleted items older than 30 days
            await PurgeOldDeletedItemsAsync();
            await LoadFeedbackAsync();
        }

        private async System.Threading.Tasks.Task PurgeOldDeletedItemsAsync()
        {
            try
            {
                int purged = await System.Threading.Tasks.Task.Run(() =>
                {
                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = @"
                        DELETE FROM VMS_Feedback
                        WHERE IsDeleted = 1
                          AND UpdatedUtcDate < DATEADD(day, -30, GETUTCDATE())";

                    return cmd.ExecuteNonQuery();
                });

                if (purged > 0)
                {
                    AppLogger.Info($"Auto-purged {purged} deleted feedback item(s) older than 30 days",
                        "FeedbackDialog.PurgeOldDeletedItemsAsync", App.CurrentUser?.Username);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FeedbackDialog.PurgeOldDeletedItemsAsync");
            }
        }

        private async System.Threading.Tasks.Task LoadFeedbackAsync()
        {
            pnlLoading.Visibility = Visibility.Visible;
            lvFeedback.Visibility = Visibility.Collapsed;

            try
            {
                bool includeDeleted = _isAdmin && (chkShowDeleted.IsChecked == true);

                var feedbackList = await System.Threading.Tasks.Task.Run(() =>
                {
                    var list = new List<FeedbackItem>();

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = includeDeleted
                        ? @"SELECT Id, Type, Title, Description, Status,
                                   CreatedBy, CreatedUtcDate, UpdatedBy, UpdatedUtcDate, IsDeleted
                            FROM VMS_Feedback
                            ORDER BY Id DESC"
                        : @"SELECT Id, Type, Title, Description, Status,
                                   CreatedBy, CreatedUtcDate, UpdatedBy, UpdatedUtcDate, IsDeleted
                            FROM VMS_Feedback
                            WHERE IsDeleted = 0
                            ORDER BY Id DESC";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new FeedbackItem
                        {
                            Id = reader.GetInt32(0),
                            Type = reader.GetString(1),
                            Title = reader.GetString(2),
                            Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            Status = reader.GetString(4),
                            CreatedBy = reader.GetString(5),
                            CreatedUtcDate = reader.IsDBNull(6) ? string.Empty : reader.GetDateTime(6).ToString("yyyy-MM-dd HH:mm"),
                            UpdatedBy = reader.IsDBNull(7) ? null : reader.GetString(7),
                            UpdatedUtcDate = reader.IsDBNull(8) ? null : reader.GetDateTime(8).ToString("yyyy-MM-dd HH:mm"),
                            IsDeleted = reader.GetBoolean(9),
                            IsNew = false
                        });
                    }

                    return list;
                });

                _allFeedback = new ObservableCollection<FeedbackItem>(feedbackList);
                ApplyFilters();

                pnlLoading.Visibility = Visibility.Collapsed;
                lvFeedback.Visibility = Visibility.Visible;

                ClearForm();
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "FeedbackDialog.LoadFeedbackAsync");
                MessageBox.Show($"Error loading feedback:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ChkShowDeleted_Changed(object sender, RoutedEventArgs e)
        {
            await LoadFeedbackAsync();
        }

        private void ApplyFilters()
        {
            // Guard against early calls before controls are initialized
            if (cboFilterType == null || cboFilterStatus == null || lvFeedback == null)
                return;

            string typeFilter = (cboFilterType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
            string statusFilter = (cboFilterStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";

            var filtered = _allFeedback.AsEnumerable();

            if (typeFilter != "All")
            {
                filtered = filtered.Where(f => f.Type == typeFilter);
            }

            if (statusFilter != "All")
            {
                filtered = filtered.Where(f => f.Status == statusFilter);
            }

            _filteredFeedback = new ObservableCollection<FeedbackItem>(filtered);
            lvFeedback.ItemsSource = _filteredFeedback;
            txtFeedbackCount.Text = $"{_filteredFeedback.Count} item(s)";
        }

        private void CboFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allFeedback != null)
            {
                ApplyFilters();
            }
        }

        private void LvFeedback_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedFeedback = lvFeedback.SelectedItem as FeedbackItem;

            if (_selectedFeedback != null)
            {
                // Set Type dropdown
                foreach (ComboBoxItem item in cboType.Items)
                {
                    if (item.Content?.ToString() == _selectedFeedback.Type)
                    {
                        cboType.SelectedItem = item;
                        break;
                    }
                }

                txtTitle.Text = _selectedFeedback.Title;
                txtDescription.Text = _selectedFeedback.Description;

                // Set Status dropdown
                foreach (ComboBoxItem item in cboStatus.Items)
                {
                    if (item.Content?.ToString() == _selectedFeedback.Status)
                    {
                        cboStatus.SelectedItem = item;
                        break;
                    }
                }

                // Show created/updated info
                string info = $"Created by {_selectedFeedback.CreatedBy} on {_selectedFeedback.CreatedUtcDate} UTC";
                if (!string.IsNullOrEmpty(_selectedFeedback.UpdatedBy))
                {
                    info += $"\nUpdated by {_selectedFeedback.UpdatedBy} on {_selectedFeedback.UpdatedUtcDate} UTC";
                }
                if (_selectedFeedback.IsDeleted)
                {
                    info += "\n\n** THIS ITEM IS DELETED **";
                }
                txtCreatedInfo.Text = info;

                // Check if current user is owner or admin
                string currentUser = App.CurrentUser?.Username ?? "";
                bool isOwner = _selectedFeedback.CreatedBy.Equals(currentUser, StringComparison.OrdinalIgnoreCase);
                bool canEdit = (_isAdmin || isOwner) && !_selectedFeedback.IsDeleted;

                // Enable Type for owner or admin (disabled for deleted items)
                cboType.IsEnabled = canEdit;
                // Enable Title/Description for owner or admin (disabled for deleted items)
                txtTitle.IsReadOnly = !canEdit;
                txtDescription.IsReadOnly = !canEdit;

                btnSave.Content = "Save Changes";
                btnSave.IsEnabled = !_selectedFeedback.IsDeleted;
                btnDelete.IsEnabled = _isAdmin;
                btnDelete.Content = _selectedFeedback.IsDeleted ? "Permanently Delete" : "Delete";
                btnRestore.Visibility = (_isAdmin && _selectedFeedback.IsDeleted) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                ClearForm();
            }
        }

        private void ClearForm()
        {
            _selectedFeedback = null;
            cboType.SelectedIndex = 0; // Default to "Idea"
            cboType.IsEnabled = true;
            txtTitle.Text = string.Empty;
            txtTitle.IsReadOnly = false;
            txtDescription.Text = string.Empty;
            txtDescription.IsReadOnly = false;
            cboStatus.SelectedIndex = 0; // Default to "New"
            txtCreatedInfo.Text = string.Empty;
            btnSave.Content = "Submit";
            btnSave.IsEnabled = true;
            btnDelete.IsEnabled = false;
            btnDelete.Content = "Delete";
            btnRestore.Visibility = Visibility.Collapsed;
            lvFeedback.SelectedItem = null;
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            txtTitle.Focus();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            string type = (cboType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Idea";
            string title = txtTitle.Text.Trim();
            string description = txtDescription.Text.Trim();
            string status = (cboStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "New";

            if (string.IsNullOrEmpty(title) || title.Length < 5)
            {
                MessageBox.Show("Title is required and must be at least 5 characters.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtTitle.Focus();
                return;
            }

            btnSave.IsEnabled = false;

            try
            {
                bool isNewFeedback = _selectedFeedback == null;
                string currentUser = App.CurrentUser?.Username ?? "Unknown";
                string nowUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                if (isNewFeedback)
                {
                    // Insert new feedback
                    int newId = await System.Threading.Tasks.Task.Run(() =>
                    {
                        using var azureConn = AzureDbManager.GetConnection();
                        azureConn.Open();

                        var cmd = azureConn.CreateCommand();
                        cmd.CommandText = @"
                            INSERT INTO VMS_Feedback (Type, Title, Description, Status, CreatedBy, CreatedUtcDate)
                            OUTPUT INSERTED.Id
                            VALUES (@type, @title, @description, @status, @createdBy, @createdUtcDate)";
                        cmd.Parameters.AddWithValue("@type", type);
                        cmd.Parameters.AddWithValue("@title", title);
                        cmd.Parameters.AddWithValue("@description", string.IsNullOrEmpty(description) ? DBNull.Value : description);
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@createdBy", currentUser);
                        cmd.Parameters.AddWithValue("@createdUtcDate", DateTime.UtcNow);

                        return Convert.ToInt32(cmd.ExecuteScalar());
                    });

                    var newFeedback = new FeedbackItem
                    {
                        Id = newId,
                        Type = type,
                        Title = title,
                        Description = description,
                        Status = status,
                        CreatedBy = currentUser,
                        CreatedUtcDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                        IsNew = false
                    };
                    _allFeedback.Insert(0, newFeedback);

                    AppLogger.Info($"Submitted feedback: {title} (ID: {newId})",
                        "FeedbackDialog.BtnSave_Click", currentUser);

                    // Notify admins
                    await NotifyAdminsAsync(newFeedback);

                    MessageBox.Show($"Feedback submitted successfully.", "Submitted",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Update existing feedback
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        using var azureConn = AzureDbManager.GetConnection();
                        azureConn.Open();

                        var cmd = azureConn.CreateCommand();
                        cmd.CommandText = @"
                            UPDATE VMS_Feedback
                            SET Type = @type, Title = @title, Description = @description, Status = @status,
                                UpdatedBy = @updatedBy, UpdatedUtcDate = @updatedUtcDate
                            WHERE Id = @id";
                        cmd.Parameters.AddWithValue("@type", type);
                        cmd.Parameters.AddWithValue("@title", title);
                        cmd.Parameters.AddWithValue("@description", string.IsNullOrEmpty(description) ? DBNull.Value : description);
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@updatedBy", currentUser);
                        cmd.Parameters.AddWithValue("@updatedUtcDate", DateTime.UtcNow);
                        cmd.Parameters.AddWithValue("@id", _selectedFeedback!.Id);

                        cmd.ExecuteNonQuery();
                    });

                    // Update in-memory
                    _selectedFeedback!.Type = type;
                    _selectedFeedback.Title = title;
                    _selectedFeedback.Description = description;
                    _selectedFeedback.Status = status;
                    _selectedFeedback.UpdatedBy = currentUser;
                    _selectedFeedback.UpdatedUtcDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

                    AppLogger.Info($"Updated feedback: {title} (ID: {_selectedFeedback.Id})",
                        "FeedbackDialog.BtnSave_Click", currentUser);

                    MessageBox.Show($"Feedback updated successfully.", "Updated",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                ApplyFilters();
                lvFeedback.Items.Refresh();
                ClearForm();

                // Update local database
                await RefreshLocalFeedbackAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FeedbackDialog.BtnSave_Click");
                MessageBox.Show($"Error saving feedback:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSave.IsEnabled = true;
            }
        }

        private async System.Threading.Tasks.Task NotifyAdminsAsync(FeedbackItem feedback)
        {
            try
            {
                // Get admin emails from Azure
                var adminEmails = await System.Threading.Tasks.Task.Run(() =>
                {
                    var emails = new List<string>();

                    using var conn = AzureDbManager.GetConnection();
                    conn.Open();

                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT u.Email
                        FROM Users u
                        INNER JOIN Admins a ON u.Username = a.Username
                        WHERE u.Email IS NOT NULL AND u.Email <> ''";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        emails.Add(reader.GetString(0));
                    }

                    return emails;
                });

                if (adminEmails.Count == 0)
                {
                    return;
                }

                string typeLabel = feedback.Type == "Bug" ? "Bug Report" : "Idea";
                string subject = $"MILESTONE Feedback: New {typeLabel} Submitted";
                string htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0078D7; color: white; padding: 15px 20px; border-radius: 4px 4px 0 0; }}
        .content {{ background-color: #f5f5f5; padding: 20px; border-radius: 0 0 4px 4px; }}
        .label {{ font-weight: 600; color: #555; }}
        .detail-row {{ padding: 8px 0; border-bottom: 1px solid #ddd; }}
        .footer {{ margin-top: 20px; font-size: 12px; color: #888; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2 style='margin: 0;'>New {typeLabel} Submitted</h2>
        </div>
        <div class='content'>
            <div class='detail-row'>
                <span class='label'>Title:</span> {System.Net.WebUtility.HtmlEncode(feedback.Title)}
            </div>
            <div class='detail-row'>
                <span class='label'>Submitted by:</span> {feedback.CreatedBy}
            </div>
            <div class='detail-row'>
                <span class='label'>Date:</span> {DateTime.UtcNow:MMMM d, yyyy h:mm tt} UTC
            </div>
            <div class='detail-row'>
                <span class='label'>Description:</span><br/>
                {(string.IsNullOrEmpty(feedback.Description) ? "(none)" : System.Net.WebUtility.HtmlEncode(feedback.Description).Replace("\n", "<br/>"))}
            </div>
            <div class='footer'>
                <p>Open MILESTONE and go to Tools > Feedback Board to review and update the status.</p>
            </div>
        </div>
    </div>
</body>
</html>";

                foreach (var email in adminEmails)
                {
                    await EmailService.SendEmailAsync(email, subject, htmlBody);
                }

                AppLogger.Info(
                    $"Sent feedback notification to {adminEmails.Count} admin(s) for: {feedback.Title}",
                    "FeedbackDialog.NotifyAdminsAsync",
                    App.CurrentUser?.Username);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FeedbackDialog.NotifyAdminsAsync");
                // Don't show error to user - notification failure shouldn't block submission
            }
        }

        private async System.Threading.Tasks.Task RefreshLocalFeedbackAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    DatabaseSetup.MirrorTablesFromAzure();
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FeedbackDialog.RefreshLocalFeedbackAsync");
                // Don't show error to user - local sync failure is not critical
            }
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFeedback == null || !_selectedFeedback.IsDeleted)
                return;

            btnRestore.IsEnabled = false;

            try
            {
                int feedbackId = _selectedFeedback.Id;
                string title = _selectedFeedback.Title;
                string currentUser = App.CurrentUser?.Username ?? "Unknown";

                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = @"
                        UPDATE Feedback
                        SET IsDeleted = 0, UpdatedBy = @updatedBy, UpdatedUtcDate = @updatedUtcDate
                        WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@id", feedbackId);
                    cmd.Parameters.AddWithValue("@updatedBy", currentUser);
                    cmd.Parameters.AddWithValue("@updatedUtcDate", DateTime.UtcNow);
                    cmd.ExecuteNonQuery();
                });

                // Update in-memory
                _selectedFeedback.IsDeleted = false;
                _selectedFeedback.UpdatedBy = currentUser;
                _selectedFeedback.UpdatedUtcDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

                AppLogger.Info($"Restored feedback: {title} (ID: {feedbackId})",
                    "FeedbackDialog.BtnRestore_Click", currentUser);

                MessageBox.Show("Feedback restored successfully.", "Restored",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Reload to refresh the list
                await LoadFeedbackAsync();
                await RefreshLocalFeedbackAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FeedbackDialog.BtnRestore_Click");
                MessageBox.Show($"Error restoring feedback:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnRestore.IsEnabled = true;
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFeedback == null)
                return;

            bool isPermanentDelete = _selectedFeedback.IsDeleted;
            string deleteType = isPermanentDelete ? "permanently delete" : "delete";
            string warning = isPermanentDelete
                ? "This will PERMANENTLY remove the item from the database."
                : "This will mark the item as deleted. Admins can restore or permanently delete it later.";

            var result = MessageBox.Show(
                $"Are you sure you want to {deleteType} this feedback?\n\n\"{_selectedFeedback.Title}\"\n\n{warning}",
                isPermanentDelete ? "Confirm Permanent Delete" : "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            btnDelete.IsEnabled = false;

            try
            {
                int feedbackId = _selectedFeedback.Id;
                string title = _selectedFeedback.Title;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    if (isPermanentDelete)
                    {
                        // Permanent delete - remove from database
                        cmd.CommandText = "DELETE FROM VMS_Feedback WHERE Id = @id";
                        cmd.Parameters.AddWithValue("@id", feedbackId);
                    }
                    else
                    {
                        // Soft delete - mark as deleted with timestamp
                        cmd.CommandText = @"
                            UPDATE VMS_Feedback
                            SET IsDeleted = 1, UpdatedBy = @updatedBy, UpdatedUtcDate = @updatedUtcDate
                            WHERE Id = @id";
                        cmd.Parameters.AddWithValue("@id", feedbackId);
                        cmd.Parameters.AddWithValue("@updatedBy", App.CurrentUser?.Username ?? "Unknown");
                        cmd.Parameters.AddWithValue("@updatedUtcDate", DateTime.UtcNow);
                    }
                    cmd.ExecuteNonQuery();
                });

                _allFeedback.Remove(_selectedFeedback);

                string action = isPermanentDelete ? "Permanently deleted" : "Deleted";
                AppLogger.Info($"{action} feedback: {title} (ID: {feedbackId})",
                    "FeedbackDialog.BtnDelete_Click", App.CurrentUser?.Username);

                MessageBox.Show($"Feedback {action.ToLower()} successfully.", action,
                    MessageBoxButton.OK, MessageBoxImage.Information);

                ApplyFilters();
                ClearForm();

                await RefreshLocalFeedbackAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "FeedbackDialog.BtnDelete_Click");
                MessageBox.Show($"Error deleting feedback:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnDelete.IsEnabled = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

    // Model for feedback item
    public class FeedbackItem : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _type = "Idea";
        private string _status = "New";
        private string _description = string.Empty;

        public int Id { get; set; }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(nameof(Type)); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedUtcDate { get; set; } = string.Empty;
        public string? UpdatedBy { get; set; }
        public string? UpdatedUtcDate { get; set; }
        public bool IsNew { get; set; }
        public bool IsDeleted { get; set; }

        // Display property for the date column
        public string CreatedDateDisplay => CreatedUtcDate.Length >= 10 ? CreatedUtcDate.Substring(0, 10) : CreatedUtcDate;

        // Display property for status showing deleted indicator
        public string StatusDisplay => IsDeleted ? "(Deleted)" : Status;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
