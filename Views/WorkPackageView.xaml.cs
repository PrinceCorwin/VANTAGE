using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
using Syncfusion.Windows.Tools.Controls;
using VANTAGE.Models;
using VANTAGE.Repositories;
using VANTAGE.Services;
using VANTAGE.Services.PdfRenderers;
using VANTAGE.Utilities;

namespace VANTAGE.Views
{
    // Helper class for user dropdown items
    public class UserItem
    {
        public string Display { get; set; } = "";
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
    }

    // Helper class for project dropdown items
    public class ProjectItem
    {
        public string ProjectID { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public partial class WorkPackageView : UserControl
    {
        private List<FormTemplate> _formTemplates = new();
        private List<WPTemplate> _wpTemplates = new();
        private List<ProjectItem> _projects = new();
        private List<User> _users = new();
        private ObservableCollection<FormTemplate> _wpFormsList = new();

        private FormTemplate? _selectedFormTemplate;
        private WPTemplate? _selectedWPTemplate;
        private bool _hasUnsavedChanges;

        // Clone state for form templates
        private string? _clonedFormType;
        private string? _clonedFormStructure;

        private readonly WorkPackageGenerator _generator = new();

        public WorkPackageView()
        {
            InitializeComponent();
            Loaded += WorkPackageView_Loaded;
        }

        private async void WorkPackageView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        // Load all data for the view
        private async Task LoadDataAsync()
        {
            try
            {
                lblStatus.Text = "Loading...";

                // Load templates
                _formTemplates = await TemplateRepository.GetAllFormTemplatesAsync();
                _wpTemplates = await TemplateRepository.GetAllWPTemplatesAsync();

                // Load projects and users from local database
                await LoadProjectsAndUsersAsync();

                // Populate dropdowns
                PopulateDropdowns();

                // Load last used output folder from settings (no default - user must set)
                var lastOutput = SettingsManager.GetUserSetting(App.CurrentUserID, "WorkPackage.LastOutputPath");
                if (!string.IsNullOrEmpty(lastOutput))
                {
                    txtOutputFolder.Text = lastOutput;
                }

                // Load last used logo path from settings, or use default Summit logo
                var lastLogo = SettingsManager.GetUserSetting(App.CurrentUserID, "WorkPackage.LastLogoPath");
                if (!string.IsNullOrEmpty(lastLogo) && File.Exists(lastLogo))
                {
                    txtLogoPath.Text = lastLogo;
                }
                else
                {
                    // Default to Summit logo - check multiple possible locations
                    var possiblePaths = new[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "SummitS-Full Summit Peak Logo.jpg"),
                        @"C:\Users\steve\source\repos\PrinceCorwin\VANTAGE\Images\SummitS-Full Summit Peak Logo.jpg"
                    };

                    foreach (var path in possiblePaths)
                    {
                        if (File.Exists(path))
                        {
                            txtLogoPath.Text = path;
                            break;
                        }
                    }
                }

                lblStatus.Text = "Ready";
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.LoadDataAsync");
                lblStatus.Text = "Error loading data";
            }
        }

        // Load projects and users from database
        private async Task LoadProjectsAndUsersAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    // Load projects
                    _projects = new List<ProjectItem>();
                    var projCmd = connection.CreateCommand();
                    projCmd.CommandText = "SELECT ProjectID, Description FROM Projects ORDER BY ProjectID";
                    using (var reader = projCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _projects.Add(new ProjectItem
                            {
                                ProjectID = reader.GetString(0),
                                Description = reader.GetString(1)
                            });
                        }
                    }

                    // Load users
                    _users = new List<User>();
                    var userCmd = connection.CreateCommand();
                    userCmd.CommandText = "SELECT UserID, Username, FullName FROM Users ORDER BY FullName";
                    using (var reader = userCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _users.Add(new User
                            {
                                UserID = reader.GetInt32(0),
                                Username = reader.GetString(1),
                                FullName = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2)
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error(ex, "WorkPackageView.LoadProjectsAndUsersAsync");
                }
            });
        }

        // Populate all dropdowns
        private void PopulateDropdowns()
        {
            // Projects dropdown
            cboProject.ItemsSource = _projects;

            // WP Templates dropdown (Generate tab)
            cboWPTemplate.ItemsSource = _wpTemplates;
            if (_wpTemplates.Any())
                cboWPTemplate.SelectedIndex = 0;

            // Users for PKG Manager and Scheduler (show "FullName (Username)")
            var userItems = _users.Select(u => new UserItem
            {
                Display = $"{u.FullName} ({u.Username})",
                Username = u.Username,
                FullName = u.FullName
            }).ToList();
            cboPKGManager.ItemsSource = userItems;
            cboPKGManager.DisplayMemberPath = "Display";
            cboScheduler.ItemsSource = userItems;
            cboScheduler.DisplayMemberPath = "Display";

            // WP Templates dropdown (Edit tab) - with "+ Add New" option
            PopulateWPTemplateEditDropdown();

            // Form Templates dropdown (Edit tab) - with "+ Add New" option
            PopulateFormTemplateEditDropdown();

            // Add Form menu
            PopulateAddFormMenu();
        }

        // Populate Add Form dropdown menu with form templates
        private void PopulateAddFormMenu()
        {
            menuAddFormGroup.Items.Clear();
            foreach (var template in _formTemplates)
            {
                var menuItem = new DropDownMenuItem
                {
                    Header = template.TemplateName,
                    Tag = template
                };
                menuItem.Click += AddFormMenuItem_Click;
                menuAddFormGroup.Items.Add(menuItem);
            }
        }

        // Handle Add Form menu item click
        private void AddFormMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is DropDownMenuItem menuItem && menuItem.Tag is FormTemplate template)
            {
                _wpFormsList.Add(template);
                _hasUnsavedChanges = true;
            }
        }

        private void PopulateWPTemplateEditDropdown()
        {
            var items = new List<object> { new { WPTemplateName = "+ Add New", WPTemplateID = (string?)null } };
            items.AddRange(_wpTemplates.Cast<object>());
            cboWPTemplateEdit.ItemsSource = items;
            cboWPTemplateEdit.DisplayMemberPath = "WPTemplateName";
        }

        private void PopulateFormTemplateEditDropdown()
        {
            var items = new List<object> { new { TemplateName = "+ Add New", TemplateID = (string?)null } };
            items.AddRange(_formTemplates.Cast<object>());
            cboFormTemplateEdit.ItemsSource = items;
            cboFormTemplateEdit.DisplayMemberPath = "TemplateName";
        }

        // Project selection changed - load work packages
        private async void CboProject_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboProject.SelectedValue is string projectId)
            {
                await LoadWorkPackagesAsync(projectId);
            }
        }

        // Load distinct work packages for selected project
        private async Task LoadWorkPackagesAsync(string projectId)
        {
            try
            {
                var workPackages = new List<string>();

                await Task.Run(() =>
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT DISTINCT WorkPackage
                        FROM Activities
                        WHERE ProjectID = @projectId
                          AND WorkPackage IS NOT NULL
                          AND WorkPackage != ''
                        ORDER BY WorkPackage";
                    cmd.Parameters.AddWithValue("@projectId", projectId);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        workPackages.Add(reader.GetString(0));
                    }
                });

                lstWorkPackages.ItemsSource = workPackages;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.LoadWorkPackagesAsync");
            }
        }

        // Select all work packages
        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            lstWorkPackages.SelectAll();
        }

        // Clear work package selection
        private void BtnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            lstWorkPackages.UnselectAll();
        }

        // Insert field token into WP Name Pattern
        private void BtnInsertField_Click(object sender, RoutedEventArgs e)
        {
            var contextMenu = new ContextMenu();
            var fields = new[] { "WorkPackage", "Area", "SystemNO", "UDF1", "UDF2", "UDF3", "UDF4", "UDF5",
                "PhaseCode", "CompType", "SchedActNO" };

            foreach (var field in fields)
            {
                var menuItem = new MenuItem { Header = field };
                menuItem.Click += (s, args) =>
                {
                    int caretIndex = txtWPNamePattern.CaretIndex;
                    string token = $"{{{field}}}";
                    txtWPNamePattern.Text = txtWPNamePattern.Text.Insert(caretIndex, token);
                    txtWPNamePattern.CaretIndex = caretIndex + token.Length;
                    txtWPNamePattern.Focus();
                };
                contextMenu.Items.Add(menuItem);
            }

            contextMenu.IsOpen = true;
            contextMenu.PlacementTarget = btnInsertField;
        }

        // Browse for logo
        private void BtnBrowseLogo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title = "Select Logo Image"
            };

            if (dialog.ShowDialog() == true)
            {
                txtLogoPath.Text = dialog.FileName;
                SettingsManager.SetUserSetting(App.CurrentUserID, "WorkPackage.LastLogoPath", dialog.FileName, "string");
            }
        }

        // Browse for output folder using modern IFileDialog COM interface
        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var selectedPath = ShowFolderPickerDialog("Select Output Folder");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                txtOutputFolder.Text = selectedPath;
                SettingsManager.SetUserSetting(App.CurrentUserID, "WorkPackage.LastOutputPath", selectedPath, "string");
            }
        }

        // Modern folder picker using IFileDialog COM interface (same as Windows Explorer)
        private string? ShowFolderPickerDialog(string title)
        {
            try
            {
                var dialog = (IFileOpenDialog)new FileOpenDialog();
                dialog.SetOptions(FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM);
                dialog.SetTitle(title);

                // Set initial folder if we have one
                if (!string.IsNullOrEmpty(txtOutputFolder.Text) && Directory.Exists(txtOutputFolder.Text))
                {
                    var riid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"); // IShellItem
                    if (SHCreateItemFromParsingName(txtOutputFolder.Text, IntPtr.Zero, ref riid, out var initialFolder) == 0)
                    {
                        dialog.SetFolder(initialFolder);
                    }
                }

                var hwnd = new WindowInteropHelper(Window.GetWindow(this)).Handle;
                if (dialog.Show(hwnd) == 0)
                {
                    dialog.GetResult(out var item);
                    item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
                    return path;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.ShowFolderPickerDialog");
            }
            return null;
        }

        // COM interfaces for modern folder picker
        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialog { }

        [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr hwndOwner);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(FOS fos);
            void GetOptions(out FOS pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(out IntPtr ppenum);
            void GetSelectedItems(out IntPtr ppsai);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [Flags]
        private enum FOS : uint
        {
            FOS_PICKFOLDERS = 0x00000020,
            FOS_FORCEFILESYSTEM = 0x00000040
        }

        private enum SIGDN : uint
        {
            SIGDN_FILESYSPATH = 0x80058000
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            out IShellItem ppv);

        // Generate button click
        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (cboProject.SelectedValue is not string projectId)
            {
                MessageBox.Show("Please select a project.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedWPs = lstWorkPackages.SelectedItems.Cast<string>().ToList();
            if (!selectedWPs.Any())
            {
                MessageBox.Show("Please select at least one work package.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cboWPTemplate.SelectedValue is not string wpTemplateId)
            {
                MessageBox.Show("Please select a WP Template.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtOutputFolder.Text))
            {
                MessageBox.Show("Please select an output folder.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get user selections
            var pkgManager = cboPKGManager.SelectedItem as UserItem;
            var scheduler = cboScheduler.SelectedItem as UserItem;

            btnGenerate.IsEnabled = false;
            lblStatus.Text = $"Generating {selectedWPs.Count} work package(s)...";

            try
            {
                var results = await _generator.GenerateBulkAsync(
                    wpTemplateId,
                    projectId,
                    selectedWPs,
                    pkgManager?.Username ?? "",
                    pkgManager?.FullName ?? "",
                    scheduler?.Username ?? "",
                    scheduler?.FullName ?? "",
                    txtWPNamePattern.Text,
                    txtOutputFolder.Text,
                    chkIndividualPdfs.IsChecked == true,
                    string.IsNullOrEmpty(txtLogoPath.Text) ? null : txtLogoPath.Text
                );

                int successCount = results.Count(r => r.Success);
                int failCount = results.Count - successCount;

                if (failCount == 0 && successCount > 0)
                {
                    lblStatus.Text = $"Generated {successCount} work package(s) successfully.";
                    var paths = string.Join("\n", results.Where(r => r.Success && !string.IsNullOrEmpty(r.MergedPdfPath)).Select(r => r.MergedPdfPath));
                    MessageBox.Show($"Successfully generated {successCount} work package(s).\n\nFiles saved to:\n{paths}",
                        "Generation Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (successCount == 0 && failCount == 0)
                {
                    lblStatus.Text = "No work packages generated";
                    var errors = string.Join("\n", results.Select(r => r.ErrorMessage ?? "Unknown error"));
                    MessageBox.Show($"No work packages were generated.\n\nErrors:\n{errors}",
                        "Generation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    lblStatus.Text = $"Generated {successCount} work package(s), {failCount} failed.";
                    var errors = string.Join("\n", results.Where(r => !r.Success).Select(r => r.ErrorMessage));
                    MessageBox.Show($"Generated {successCount} of {results.Count} work packages.\n\nErrors:\n{errors}",
                        "Generation Complete with Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.BtnGenerate_Click");
                MessageBox.Show($"Error generating work packages: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                lblStatus.Text = "Error during generation";
            }
            finally
            {
                btnGenerate.IsEnabled = true;
            }
        }

        // Tab selection changed - update preview context label
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != MainTabControl) return;

            var selectedTab = MainTabControl.SelectedItem as TabItem;
            if (selectedTab?.Header?.ToString() == "Generate")
            {
                lblPreviewContext.Text = "Previewing: Selected Work Package";
            }
            else if (selectedTab?.Header?.ToString() == "WP Templates")
            {
                lblPreviewContext.Text = "Previewing: Sample Data";
            }
            else if (selectedTab?.Header?.ToString() == "Form Templates")
            {
                lblPreviewContext.Text = "Previewing: Sample Data";
            }
        }

        // Refresh preview - generates PDF and loads into viewer
        private async void BtnRefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                lblStatus.Text = "Generating preview...";
                btnRefreshPreview.IsEnabled = false;
                MemoryStream? previewStream = null;

                var selectedTab = MainTabControl.SelectedItem as TabItem;

                if (selectedTab?.Header?.ToString() == "Form Templates" && _selectedFormTemplate != null)
                {
                    // Preview single form template
                    previewStream = _generator.GenerateFormPreview(_selectedFormTemplate,
                        string.IsNullOrEmpty(txtLogoPath.Text) ? null : txtLogoPath.Text);
                }
                else if (cboWPTemplate.SelectedValue is string wpTemplateId)
                {
                    // Preview full WP template
                    previewStream = await _generator.GeneratePreviewAsync(wpTemplateId,
                        string.IsNullOrEmpty(txtLogoPath.Text) ? null : txtLogoPath.Text);
                }

                if (previewStream != null && previewStream.Length > 0)
                {
                    // Load PDF into viewer
                    previewStream.Position = 0;
                    pdfViewer.Load(previewStream);

                    // Show viewer, hide placeholder
                    pdfViewer.Visibility = Visibility.Visible;
                    pdfPlaceholderBorder.Visibility = Visibility.Collapsed;

                    lblStatus.Text = "Preview generated";
                }
                else
                {
                    ShowPreviewPlaceholder("No preview available\n\nSelect a template first");
                    lblStatus.Text = "No preview available";
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.BtnRefreshPreview_Click");
                ShowPreviewPlaceholder($"Error generating preview:\n\n{ex.Message}");
                lblStatus.Text = "Error generating preview";
            }
            finally
            {
                btnRefreshPreview.IsEnabled = true;
            }
        }

        // Show placeholder text and hide PDF viewer
        private void ShowPreviewPlaceholder(string message)
        {
            pdfPlaceholder.Text = message;
            pdfPlaceholderBorder.Visibility = Visibility.Visible;
            pdfViewer.Visibility = Visibility.Collapsed;
        }

        // WP Template edit selection changed
        private async void CboWPTemplateEdit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboWPTemplateEdit.SelectedItem is WPTemplate template)
            {
                _selectedWPTemplate = template;
                txtWPTemplateName.Text = template.WPTemplateName;

                // Parse settings
                var settings = JsonSerializer.Deserialize<WPTemplateSettings>(template.DefaultSettings);
                txtExpirationDays.Value = settings?.ExpirationDays ?? 14;

                // Load forms list
                await LoadWPFormsListAsync(template);
            }
            else
            {
                // "+ Add New" selected
                _selectedWPTemplate = null;
                txtWPTemplateName.Text = "";
                txtExpirationDays.Value = 14;
                _wpFormsList.Clear();
                lstWPForms.ItemsSource = _wpFormsList;
            }
        }

        // Load forms list for WP template
        private async Task LoadWPFormsListAsync(WPTemplate template)
        {
            _wpFormsList.Clear();

            var formRefs = JsonSerializer.Deserialize<List<FormReference>>(template.FormsJson);
            if (formRefs == null) return;

            foreach (var formRef in formRefs)
            {
                var formTemplate = await TemplateRepository.GetFormTemplateByIdAsync(formRef.FormTemplateId);
                if (formTemplate != null)
                {
                    _wpFormsList.Add(formTemplate);
                }
            }

            lstWPForms.ItemsSource = _wpFormsList;
        }

        // Move form up in list
        private void BtnMoveFormUp_Click(object sender, RoutedEventArgs e)
        {
            if (lstWPForms.SelectedIndex > 0)
            {
                int index = lstWPForms.SelectedIndex;
                var item = _wpFormsList[index];
                _wpFormsList.RemoveAt(index);
                _wpFormsList.Insert(index - 1, item);
                lstWPForms.SelectedIndex = index - 1;
                _hasUnsavedChanges = true;
            }
        }

        // Move form down in list
        private void BtnMoveFormDown_Click(object sender, RoutedEventArgs e)
        {
            if (lstWPForms.SelectedIndex < _wpFormsList.Count - 1 && lstWPForms.SelectedIndex >= 0)
            {
                int index = lstWPForms.SelectedIndex;
                var item = _wpFormsList[index];
                _wpFormsList.RemoveAt(index);
                _wpFormsList.Insert(index + 1, item);
                lstWPForms.SelectedIndex = index + 1;
                _hasUnsavedChanges = true;
            }
        }

        // Remove form from WP template
        private void BtnRemoveForm_Click(object sender, RoutedEventArgs e)
        {
            if (lstWPForms.SelectedItem is FormTemplate template)
            {
                _wpFormsList.Remove(template);
                _hasUnsavedChanges = true;
            }
        }

        // Clone WP template
        private void BtnCloneWPTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWPTemplate == null) return;

            txtWPTemplateName.Text = _selectedWPTemplate.WPTemplateName + " (Copy)";
            _selectedWPTemplate = null; // Make it a new template
            _hasUnsavedChanges = true;
        }

        // Delete WP template
        private async void BtnDeleteWPTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWPTemplate == null) return;

            if (_selectedWPTemplate.IsBuiltIn)
            {
                MessageBox.Show("Built-in templates cannot be deleted.", "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete '{_selectedWPTemplate.WPTemplateName}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await TemplateRepository.DeleteWPTemplateAsync(_selectedWPTemplate.WPTemplateID);
                _wpTemplates = await TemplateRepository.GetAllWPTemplatesAsync();
                PopulateWPTemplateEditDropdown();
                cboWPTemplate.ItemsSource = _wpTemplates;
                lblStatus.Text = "Template deleted";
            }
        }

        // Save WP template
        private async void BtnSaveWPTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtWPTemplateName.Text))
            {
                MessageBox.Show("Please enter a template name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if saving a built-in template
            if (_selectedWPTemplate?.IsBuiltIn == true)
            {
                var result = MessageBox.Show("This is a built-in template. Save as a new template?",
                    "Save as New", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
                _selectedWPTemplate = null; // Create new
            }

            try
            {
                var formsJson = JsonSerializer.Serialize(_wpFormsList.Select(f => new FormReference { FormTemplateId = f.TemplateID }).ToList());
                var settingsJson = JsonSerializer.Serialize(new WPTemplateSettings { ExpirationDays = (int)(txtExpirationDays.Value ?? 14) });

                string savedTemplateId;
                if (_selectedWPTemplate == null)
                {
                    // Create new
                    var template = new WPTemplate
                    {
                        WPTemplateID = Guid.NewGuid().ToString(),
                        WPTemplateName = txtWPTemplateName.Text,
                        FormsJson = formsJson,
                        DefaultSettings = settingsJson,
                        IsBuiltIn = false,
                        CreatedBy = App.CurrentUser?.Username ?? "Unknown",
                        CreatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    };
                    await TemplateRepository.InsertWPTemplateAsync(template);
                    savedTemplateId = template.WPTemplateID;
                    lblStatus.Text = "Template created";
                }
                else
                {
                    // Update existing
                    _selectedWPTemplate.WPTemplateName = txtWPTemplateName.Text;
                    _selectedWPTemplate.FormsJson = formsJson;
                    _selectedWPTemplate.DefaultSettings = settingsJson;
                    await TemplateRepository.UpdateWPTemplateAsync(_selectedWPTemplate);
                    savedTemplateId = _selectedWPTemplate.WPTemplateID;
                    lblStatus.Text = "Template saved";
                }

                _hasUnsavedChanges = false;
                _wpTemplates = await TemplateRepository.GetAllWPTemplatesAsync();
                PopulateWPTemplateEditDropdown();
                cboWPTemplate.ItemsSource = _wpTemplates;

                // Auto-select the saved template
                var savedTemplate = _wpTemplates.FirstOrDefault(t => t.WPTemplateID == savedTemplateId);
                if (savedTemplate != null)
                {
                    cboWPTemplateEdit.SelectedItem = savedTemplate;
                    _selectedWPTemplate = savedTemplate;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.BtnSaveWPTemplate_Click");
                MessageBox.Show($"Error saving template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Form Template edit selection changed
        private void CboFormTemplateEdit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboFormTemplateEdit.SelectedItem is FormTemplate template)
            {
                _selectedFormTemplate = template;
                txtFormTemplateName.Text = template.TemplateName;
                lblFormTemplateType.Text = template.TemplateType;

                // TODO: Load type-specific editor
            }
            else
            {
                // "+ Add New" selected - show type selection dialog
                _selectedFormTemplate = null;
                txtFormTemplateName.Text = "";
                lblFormTemplateType.Text = "";

                // TODO: Show type selection dialog
            }
        }

        // Clone form template
        private void BtnCloneFormTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFormTemplate == null) return;

            // Preserve clone data for save
            _clonedFormType = _selectedFormTemplate.TemplateType;
            _clonedFormStructure = _selectedFormTemplate.StructureJson;

            txtFormTemplateName.Text = _selectedFormTemplate.TemplateName + " (Copy)";
            _selectedFormTemplate = null; // Make it a new template
            _hasUnsavedChanges = true;
        }

        // Delete form template
        private async void BtnDeleteFormTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFormTemplate == null) return;

            if (_selectedFormTemplate.IsBuiltIn)
            {
                MessageBox.Show("Built-in templates cannot be deleted.", "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (success, blockingTemplates) = await TemplateRepository.DeleteFormTemplateAsync(_selectedFormTemplate.TemplateID);

            if (!success && blockingTemplates.Any())
            {
                MessageBox.Show($"Cannot delete this form template. It is used by the following WP templates:\n\n{string.Join("\n", blockingTemplates)}",
                    "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (success)
            {
                _formTemplates = await TemplateRepository.GetAllFormTemplatesAsync();
                PopulateFormTemplateEditDropdown();
                PopulateAddFormMenu();
                lblStatus.Text = "Template deleted";
            }
        }

        // Save form template
        private async void BtnSaveFormTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFormTemplateName.Text))
            {
                MessageBox.Show("Please enter a template name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if saving a built-in template - preserve clone data
            if (_selectedFormTemplate?.IsBuiltIn == true)
            {
                var result = MessageBox.Show("This is a built-in template. Save as a new template?",
                    "Save as New", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                // Preserve data for clone
                _clonedFormType = _selectedFormTemplate.TemplateType;
                _clonedFormStructure = _selectedFormTemplate.StructureJson;
                _selectedFormTemplate = null; // Create new
            }

            try
            {
                string savedTemplateId;

                if (_selectedFormTemplate == null)
                {
                    // Creating new template - check for clone data
                    if (string.IsNullOrEmpty(_clonedFormType) || string.IsNullOrEmpty(_clonedFormStructure))
                    {
                        MessageBox.Show("Please select a template to clone first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Create new from clone data
                    var template = new FormTemplate
                    {
                        TemplateID = Guid.NewGuid().ToString(),
                        TemplateName = txtFormTemplateName.Text,
                        TemplateType = _clonedFormType,
                        StructureJson = _clonedFormStructure,
                        IsBuiltIn = false,
                        CreatedBy = App.CurrentUser?.Username ?? "Unknown",
                        CreatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    };
                    await TemplateRepository.InsertFormTemplateAsync(template);
                    savedTemplateId = template.TemplateID;
                    lblStatus.Text = "Template created";

                    // Clear clone data
                    _clonedFormType = null;
                    _clonedFormStructure = null;
                }
                else
                {
                    // Update existing
                    _selectedFormTemplate.TemplateName = txtFormTemplateName.Text;
                    await TemplateRepository.UpdateFormTemplateAsync(_selectedFormTemplate);
                    savedTemplateId = _selectedFormTemplate.TemplateID;
                    lblStatus.Text = "Template saved";
                }

                _hasUnsavedChanges = false;
                _formTemplates = await TemplateRepository.GetAllFormTemplatesAsync();
                PopulateFormTemplateEditDropdown();
                PopulateAddFormMenu();

                // Auto-select the saved template
                var savedTemplate = _formTemplates.FirstOrDefault(t => t.TemplateID == savedTemplateId);
                if (savedTemplate != null)
                {
                    cboFormTemplateEdit.SelectedItem = savedTemplate;
                    _selectedFormTemplate = savedTemplate;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.BtnSaveFormTemplate_Click");
                MessageBox.Show($"Error saving template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Import templates
        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files|*.json",
                Title = "Import Templates"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var json = await File.ReadAllTextAsync(dialog.FileName);
                // TODO: Parse and import templates
                MessageBox.Show("Import functionality coming soon.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.BtnImport_Click");
                MessageBox.Show($"Error importing templates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Export templates
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Show export dialog
            MessageBox.Show("Export functionality coming soon.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Check for unsaved changes before leaving
        public bool CanLeaveView()
        {
            if (!_hasUnsavedChanges) return true;

            var result = MessageBox.Show("You have unsaved changes. Save as a new template?",
                "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return false;
            if (result == MessageBoxResult.Yes)
            {
                // Trigger save based on active tab
                var selectedTab = MainTabControl.SelectedItem as TabItem;
                if (selectedTab?.Header?.ToString() == "WP Templates")
                    BtnSaveWPTemplate_Click(this, new RoutedEventArgs());
                else if (selectedTab?.Header?.ToString() == "Form Templates")
                    BtnSaveFormTemplate_Click(this, new RoutedEventArgs());
            }

            return true;
        }
    }
}
