using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VANTAGE.Data;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class AdminProjectsDialog : Window
    {
        private ObservableCollection<ProjectItem> _projects = new();
        private ProjectItem? _selectedProject;

        public AdminProjectsDialog()
        {
            InitializeComponent();
            Loaded += AdminProjectsDialog_Loaded;
        }

        private async void AdminProjectsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadProjectsAsync();
        }

        private async System.Threading.Tasks.Task LoadProjectsAsync()
        {
            pnlLoading.Visibility = Visibility.Visible;
            lvProjects.Visibility = Visibility.Collapsed;

            try
            {
                var projects = await System.Threading.Tasks.Task.Run(() =>
                {
                    var projectList = new List<ProjectItem>();

                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT ProjectID, Description, ClientName,
                               ClientStreetAddress, ClientCity, ClientState, ClientZipCode,
                               ProjectStreetAddress, ProjectCity, ProjectState, ProjectZipCode,
                               ProjectManager, SiteManager, OM, SM, EN, PM, Phone, Fax
                        FROM VMS_Projects
                        ORDER BY ProjectID";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        projectList.Add(new ProjectItem
                        {
                            ProjectID = reader.GetString(0),
                            Description = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            ClientName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            ClientStreetAddress = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            ClientCity = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            ClientState = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                            ClientZipCode = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                            ProjectStreetAddress = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                            ProjectCity = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                            ProjectState = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                            ProjectZipCode = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                            ProjectManager = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                            SiteManager = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                            OM = reader.IsDBNull(13) ? 0 : (reader.GetBoolean(13) ? 1 : 0),
                            SM = reader.IsDBNull(14) ? 0 : (reader.GetBoolean(14) ? 1 : 0),
                            EN = reader.IsDBNull(15) ? 0 : (reader.GetBoolean(15) ? 1 : 0),
                            PM = reader.IsDBNull(16) ? 0 : (reader.GetBoolean(16) ? 1 : 0),
                            Phone = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
                            Fax = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                            IsNew = false
                        });
                    }

                    return projectList;
                });

                _projects = new ObservableCollection<ProjectItem>(projects);
                lvProjects.ItemsSource = _projects;

                pnlLoading.Visibility = Visibility.Collapsed;
                lvProjects.Visibility = Visibility.Visible;

                txtProjectCount.Text = $"{_projects.Count} project(s)";
                ClearForm();
            }
            catch (Exception ex)
            {
                pnlLoading.Visibility = Visibility.Collapsed;
                AppLogger.Error(ex, "AdminProjectsDialog.LoadProjectsAsync");
                MessageBox.Show($"Error loading projects:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LvProjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedProject = lvProjects.SelectedItem as ProjectItem;

            if (_selectedProject != null)
            {
                txtProjectID.Text = _selectedProject.ProjectID;
                txtDescription.Text = _selectedProject.Description;
                txtClientName.Text = _selectedProject.ClientName;
                txtPhone.Text = _selectedProject.Phone;
                txtFax.Text = _selectedProject.Fax;
                txtClientStreetAddress.Text = _selectedProject.ClientStreetAddress;
                txtClientCity.Text = _selectedProject.ClientCity;
                txtClientState.Text = _selectedProject.ClientState;
                txtClientZipCode.Text = _selectedProject.ClientZipCode;
                txtProjectStreetAddress.Text = _selectedProject.ProjectStreetAddress;
                txtProjectCity.Text = _selectedProject.ProjectCity;
                txtProjectState.Text = _selectedProject.ProjectState;
                txtProjectZipCode.Text = _selectedProject.ProjectZipCode;
                txtProjectManager.Text = _selectedProject.ProjectManager;
                txtSiteManager.Text = _selectedProject.SiteManager;
                chkOM.IsChecked = _selectedProject.OM == 1;
                chkSM.IsChecked = _selectedProject.SM == 1;
                chkEN.IsChecked = _selectedProject.EN == 1;
                chkPM.IsChecked = _selectedProject.PM == 1;

                txtProjectID.IsEnabled = false; // Can't change ProjectID of existing project
                btnSave.Content = "Save Changes";
                btnDelete.IsEnabled = true;
            }
            else
            {
                ClearForm();
            }
        }

        private void ClearForm()
        {
            _selectedProject = null;
            txtProjectID.Text = string.Empty;
            txtDescription.Text = string.Empty;
            txtClientName.Text = string.Empty;
            txtPhone.Text = string.Empty;
            txtFax.Text = string.Empty;
            txtClientStreetAddress.Text = string.Empty;
            txtClientCity.Text = string.Empty;
            txtClientState.Text = string.Empty;
            txtClientZipCode.Text = string.Empty;
            txtProjectStreetAddress.Text = string.Empty;
            txtProjectCity.Text = string.Empty;
            txtProjectState.Text = string.Empty;
            txtProjectZipCode.Text = string.Empty;
            txtProjectManager.Text = string.Empty;
            txtSiteManager.Text = string.Empty;
            chkOM.IsChecked = false;
            chkSM.IsChecked = false;
            chkEN.IsChecked = false;
            chkPM.IsChecked = false;

            txtProjectID.IsEnabled = true;
            btnSave.Content = "Add Project";
            btnDelete.IsEnabled = false;
            lvProjects.SelectedItem = null;
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            txtProjectID.Focus();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            string projectId = txtProjectID.Text.Trim();

            if (string.IsNullOrEmpty(projectId))
            {
                MessageBox.Show("Project ID is required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtProjectID.Focus();
                return;
            }

            // Check for duplicate ProjectID on new projects
            if (_selectedProject == null)
            {
                if (_projects.Any(p => p.ProjectID.Equals(projectId, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("A project with this Project ID already exists.", "Duplicate Project ID",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtProjectID.Focus();
                    return;
                }
            }

            btnSave.IsEnabled = false;

            try
            {
                var projectData = new ProjectItem
                {
                    ProjectID = projectId,
                    Description = txtDescription.Text.Trim(),
                    ClientName = txtClientName.Text.Trim(),
                    Phone = txtPhone.Text.Trim(),
                    Fax = txtFax.Text.Trim(),
                    ClientStreetAddress = txtClientStreetAddress.Text.Trim(),
                    ClientCity = txtClientCity.Text.Trim(),
                    ClientState = txtClientState.Text.Trim(),
                    ClientZipCode = txtClientZipCode.Text.Trim(),
                    ProjectStreetAddress = txtProjectStreetAddress.Text.Trim(),
                    ProjectCity = txtProjectCity.Text.Trim(),
                    ProjectState = txtProjectState.Text.Trim(),
                    ProjectZipCode = txtProjectZipCode.Text.Trim(),
                    ProjectManager = txtProjectManager.Text.Trim(),
                    SiteManager = txtSiteManager.Text.Trim(),
                    OM = chkOM.IsChecked == true ? 1 : 0,
                    SM = chkSM.IsChecked == true ? 1 : 0,
                    EN = chkEN.IsChecked == true ? 1 : 0,
                    PM = chkPM.IsChecked == true ? 1 : 0
                };

                if (_selectedProject == null)
                {
                    // Insert new project
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        using var azureConn = AzureDbManager.GetConnection();
                        azureConn.Open();

                        var cmd = azureConn.CreateCommand();
                        cmd.CommandText = @"
                            INSERT INTO VMS_Projects (ProjectID, Description, ClientName,
                                ClientStreetAddress, ClientCity, ClientState, ClientZipCode,
                                ProjectStreetAddress, ProjectCity, ProjectState, ProjectZipCode,
                                ProjectManager, SiteManager, OM, SM, EN, PM, Phone, Fax)
                            VALUES (@projectId, @description, @clientName,
                                @clientStreetAddress, @clientCity, @clientState, @clientZipCode,
                                @projectStreetAddress, @projectCity, @projectState, @projectZipCode,
                                @projectManager, @siteManager, @om, @sm, @en, @pm, @phone, @fax)";

                        AddProjectParameters(cmd, projectData);
                        cmd.ExecuteNonQuery();
                    });

                    projectData.IsNew = false;
                    _projects.Add(projectData);

                    AppLogger.Info($"Added new project: {projectId}",
                        "AdminProjectsDialog.BtnSave_Click", App.CurrentUser?.Username);

                    MessageBox.Show($"Project '{projectId}' added successfully.", "Project Added",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Update existing project
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        using var azureConn = AzureDbManager.GetConnection();
                        azureConn.Open();

                        var cmd = azureConn.CreateCommand();
                        cmd.CommandText = @"
                            UPDATE VMS_Projects SET
                                Description = @description,
                                ClientName = @clientName,
                                Phone = @phone,
                                Fax = @fax,
                                ClientStreetAddress = @clientStreetAddress,
                                ClientCity = @clientCity,
                                ClientState = @clientState,
                                ClientZipCode = @clientZipCode,
                                ProjectStreetAddress = @projectStreetAddress,
                                ProjectCity = @projectCity,
                                ProjectState = @projectState,
                                ProjectZipCode = @projectZipCode,
                                ProjectManager = @projectManager,
                                SiteManager = @siteManager,
                                OM = @om, SM = @sm, EN = @en, PM = @pm
                            WHERE ProjectID = @projectId";

                        AddProjectParameters(cmd, projectData);
                        cmd.ExecuteNonQuery();
                    });

                    // Update in-memory
                    _selectedProject.Description = projectData.Description;
                    _selectedProject.ClientName = projectData.ClientName;
                    _selectedProject.Phone = projectData.Phone;
                    _selectedProject.Fax = projectData.Fax;
                    _selectedProject.ClientStreetAddress = projectData.ClientStreetAddress;
                    _selectedProject.ClientCity = projectData.ClientCity;
                    _selectedProject.ClientState = projectData.ClientState;
                    _selectedProject.ClientZipCode = projectData.ClientZipCode;
                    _selectedProject.ProjectStreetAddress = projectData.ProjectStreetAddress;
                    _selectedProject.ProjectCity = projectData.ProjectCity;
                    _selectedProject.ProjectState = projectData.ProjectState;
                    _selectedProject.ProjectZipCode = projectData.ProjectZipCode;
                    _selectedProject.ProjectManager = projectData.ProjectManager;
                    _selectedProject.SiteManager = projectData.SiteManager;
                    _selectedProject.OM = projectData.OM;
                    _selectedProject.SM = projectData.SM;
                    _selectedProject.EN = projectData.EN;
                    _selectedProject.PM = projectData.PM;

                    AppLogger.Info($"Updated project: {projectId}",
                        "AdminProjectsDialog.BtnSave_Click", App.CurrentUser?.Username);

                    MessageBox.Show($"Project '{projectId}' updated successfully.", "Project Updated",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                lvProjects.Items.Refresh();
                txtProjectCount.Text = $"{_projects.Count} project(s)";
                ClearForm();

                // Update local Projects table and ProjectCache
                await RefreshLocalProjectsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminProjectsDialog.BtnSave_Click");
                MessageBox.Show($"Error saving project:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSave.IsEnabled = true;
            }
        }

        private void AddProjectParameters(Microsoft.Data.SqlClient.SqlCommand cmd, ProjectItem project)
        {
            cmd.Parameters.AddWithValue("@projectId", project.ProjectID);
            cmd.Parameters.AddWithValue("@description", project.Description);
            cmd.Parameters.AddWithValue("@clientName", project.ClientName);
            cmd.Parameters.AddWithValue("@phone", project.Phone);
            cmd.Parameters.AddWithValue("@fax", project.Fax);
            cmd.Parameters.AddWithValue("@clientStreetAddress", project.ClientStreetAddress);
            cmd.Parameters.AddWithValue("@clientCity", project.ClientCity);
            cmd.Parameters.AddWithValue("@clientState", project.ClientState);
            cmd.Parameters.AddWithValue("@clientZipCode", project.ClientZipCode);
            cmd.Parameters.AddWithValue("@projectStreetAddress", project.ProjectStreetAddress);
            cmd.Parameters.AddWithValue("@projectCity", project.ProjectCity);
            cmd.Parameters.AddWithValue("@projectState", project.ProjectState);
            cmd.Parameters.AddWithValue("@projectZipCode", project.ProjectZipCode);
            cmd.Parameters.AddWithValue("@projectManager", project.ProjectManager);
            cmd.Parameters.AddWithValue("@siteManager", project.SiteManager);
            cmd.Parameters.AddWithValue("@om", project.OM);
            cmd.Parameters.AddWithValue("@sm", project.SM);
            cmd.Parameters.AddWithValue("@en", project.EN);
            cmd.Parameters.AddWithValue("@pm", project.PM);
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProject == null)
                return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete project '{_selectedProject.ProjectID}'?\n\n" +
                "WARNING: This may cause issues if activities reference this project.\n\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            btnDelete.IsEnabled = false;

            try
            {
                string projectId = _selectedProject.ProjectID;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    using var azureConn = AzureDbManager.GetConnection();
                    azureConn.Open();

                    var cmd = azureConn.CreateCommand();
                    cmd.CommandText = "DELETE FROM VMS_Projects WHERE ProjectID = @projectId";
                    cmd.Parameters.AddWithValue("@projectId", projectId);
                    cmd.ExecuteNonQuery();
                });

                _projects.Remove(_selectedProject);

                AppLogger.Info($"Deleted project: {projectId}",
                    "AdminProjectsDialog.BtnDelete_Click", App.CurrentUser?.Username);

                MessageBox.Show($"Project '{projectId}' deleted successfully.", "Project Deleted",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                lvProjects.Items.Refresh();
                txtProjectCount.Text = $"{_projects.Count} project(s)";
                ClearForm();

                // Update local Projects table and ProjectCache
                await RefreshLocalProjectsAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminProjectsDialog.BtnDelete_Click");
                MessageBox.Show($"Error deleting project:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnDelete.IsEnabled = true;
            }
        }

        private async System.Threading.Tasks.Task RefreshLocalProjectsAsync()
        {
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    DatabaseSetup.MirrorTablesFromAzure();
                });

                // Reload ProjectCache so validation uses updated data
                ProjectCache.Reload();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AdminProjectsDialog.RefreshLocalProjectsAsync");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

    // Model for project item
    public class ProjectItem : INotifyPropertyChanged
    {
        private string _projectId = string.Empty;
        private string _description = string.Empty;
        private string _clientName = string.Empty;

        public string ProjectID
        {
            get => _projectId;
            set { _projectId = value; OnPropertyChanged(nameof(ProjectID)); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }

        public string ClientName
        {
            get => _clientName;
            set { _clientName = value; OnPropertyChanged(nameof(ClientName)); }
        }

        public string Phone { get; set; } = string.Empty;
        public string Fax { get; set; } = string.Empty;
        public string ClientStreetAddress { get; set; } = string.Empty;
        public string ClientCity { get; set; } = string.Empty;
        public string ClientState { get; set; } = string.Empty;
        public string ClientZipCode { get; set; } = string.Empty;
        public string ProjectStreetAddress { get; set; } = string.Empty;
        public string ProjectCity { get; set; } = string.Empty;
        public string ProjectState { get; set; } = string.Empty;
        public string ProjectZipCode { get; set; } = string.Empty;
        public string ProjectManager { get; set; } = string.Empty;
        public string SiteManager { get; set; } = string.Empty;
        public int OM { get; set; }
        public int SM { get; set; }
        public int EN { get; set; }
        public int PM { get; set; }
        public bool IsNew { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}