using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Syncfusion.Windows.Tools.Controls;
using VANTAGE.Models;
using VANTAGE.Repositories;
using VANTAGE.Services;
using VANTAGE.Services.PdfRenderers;
using VANTAGE.Utilities;
using VANTAGE.Interfaces;
using VANTAGE.Dialogs;
using VANTAGE.Converters;

namespace VANTAGE.Views
{
    public partial class WorkPackageView : UserControl, IHelpAware
    {
        private List<FormTemplate> _formTemplates = new();
        private List<WPTemplate> _wpTemplates = new();
        private List<Models.ProjectItem> _projects = new();
        private List<User> _users = new();
        private ObservableCollection<FormTemplate> _wpFormsList = new();

        private FormTemplate? _selectedFormTemplate;
        private WPTemplate? _selectedWPTemplate;
        private bool _hasUnsavedChanges;

        // Clone state for form templates
        private string? _clonedFormType;
        private string? _clonedFormStructure;

        // Current editor type being displayed
        private string? _currentEditorType;

        // Flag to suppress type dialog during programmatic dropdown updates
        private bool _suppressTypeDialog;

        // Cover editor controls
        private TextBox? _coverTitleBox;
        private TextBox? _coverImagePathBox;
        private Slider? _coverImageWidthSlider;
        private TextBox? _coverFooterTextBox;

        // List editor controls
        private TextBox? _listTitleBox;
        private ListBox? _listItemsBox;
        private ObservableCollection<string>? _listItems;
        private TextBox? _listNewItemBox;
        private TextBox? _listFooterTextBox;

        // Grid editor controls
        private TextBox? _gridTitleBox;
        private ListBox? _gridColumnsBox;
        private ObservableCollection<TemplateColumn>? _gridColumns;
        private TextBox? _gridNewColumnNameBox;
        private Syncfusion.Windows.Shared.IntegerTextBox? _gridNewColumnWidthBox;
        private Syncfusion.Windows.Shared.IntegerTextBox? _gridRowCountBox;
        private Slider? _gridRowHeightSlider;
        private TextBox? _gridFooterTextBox;

        // Form editor controls
        private TextBox? _formTitleBox;
        private ListBox? _formColumnsBox;
        private ObservableCollection<TemplateColumn>? _formColumns;
        private TextBox? _formNewColumnNameBox;
        private Syncfusion.Windows.Shared.IntegerTextBox? _formNewColumnWidthBox;
        private ListBox? _formSectionsBox;
        private ObservableCollection<SectionDefinition>? _formSections;
        private TextBox? _formNewSectionBox;
        private ListBox? _formSectionItemsBox;
        private TextBox? _formNewItemBox;
        private Slider? _formRowHeightSlider;
        private TextBox? _formFooterTextBox;

        private readonly WorkPackageGenerator _generator = new();

        // IHelpAware implementation
        public string HelpAnchor => "work-packages";
        public string ModuleDisplayName => "Work Packages";

        // Gets the default logo path if it exists, otherwise null
        private string? GetDefaultLogoPath()
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "SummitS-Full Summit Peak Logo.jpg"),
                @"C:\Users\steve\source\repos\PrinceCorwin\VANTAGE\Images\SummitS-Full Summit Peak Logo.jpg"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }
            return null;
        }

        // Resolves the logo path - converts "(default)" to actual path
        private string? GetResolvedLogoPath()
        {
            if (string.IsNullOrEmpty(txtLogoPath.Text) || txtLogoPath.Text == "(default)")
                return GetDefaultLogoPath();
            return txtLogoPath.Text;
        }

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

                // Load last used logo path from settings, or show "(default)" for built-in logo
                var lastLogo = SettingsManager.GetUserSetting(App.CurrentUserID, "WorkPackage.LastLogoPath");
                if (!string.IsNullOrEmpty(lastLogo) && File.Exists(lastLogo))
                {
                    txtLogoPath.Text = lastLogo;
                }
                else
                {
                    txtLogoPath.Text = "(default)";
                }

                // Restore splitter position
                var splitterRatio = SettingsManager.GetUserSetting(App.CurrentUserID, "WorkPackage.SplitterRatio");
                if (!string.IsNullOrEmpty(splitterRatio) && double.TryParse(splitterRatio, out double ratio) && ratio > 0 && ratio < 1)
                {
                    LeftPanelColumn.Width = new GridLength(ratio, GridUnitType.Star);
                    RightPanelColumn.Width = new GridLength(1 - ratio, GridUnitType.Star);
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
                    _projects = new List<Models.ProjectItem>();
                    var projCmd = connection.CreateCommand();
                    projCmd.CommandText = "SELECT ProjectID, Description FROM Projects ORDER BY ProjectID";
                    using (var reader = projCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _projects.Add(new Models.ProjectItem
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

        // Populate all dropdowns and restore saved selections
        private void PopulateDropdowns()
        {
            // Projects dropdown
            cboProject.ItemsSource = _projects;

            // Restore last selected project
            var lastProjectId = SettingsManager.GetUserSetting(App.CurrentUserID, "WorkPackage.LastProjectID");
            if (!string.IsNullOrEmpty(lastProjectId) && _projects.Any(p => p.ProjectID == lastProjectId))
            {
                cboProject.SelectedValue = lastProjectId;
            }

            // WP Templates dropdown (Generate tab)
            cboWPTemplate.ItemsSource = _wpTemplates;

            // Restore last selected WP template, or default to first
            var lastWPTemplateId = SettingsManager.GetUserSetting(App.CurrentUserID, "WorkPackage.LastWPTemplateID");
            if (!string.IsNullOrEmpty(lastWPTemplateId) && _wpTemplates.Any(t => t.WPTemplateID == lastWPTemplateId))
            {
                cboWPTemplate.SelectedValue = lastWPTemplateId;
            }
            else if (_wpTemplates.Any())
            {
                cboWPTemplate.SelectedIndex = 0;
            }

            // Users for PKG Manager and Scheduler (show "FullName (Username)")
            var userItems = _users.Select(u => new Models.UserItem
            {
                Display = $"{u.FullName} ({u.Username})",
                Username = u.Username,
                FullName = u.FullName
            }).ToList();
            cboPKGManager.ItemsSource = userItems;
            cboPKGManager.DisplayMemberPath = "Display";
            cboScheduler.ItemsSource = userItems;
            cboScheduler.DisplayMemberPath = "Display";

            // Restore last selected PKG Manager
            var lastPKGManager = SettingsManager.GetUserSetting(App.CurrentUserID, "WorkPackage.LastPKGManager");
            if (!string.IsNullOrEmpty(lastPKGManager))
            {
                var pkgManagerItem = userItems.FirstOrDefault(u => u.Username == lastPKGManager);
                if (pkgManagerItem != null)
                    cboPKGManager.SelectedItem = pkgManagerItem;
            }

            // Restore last selected Scheduler
            var lastScheduler = SettingsManager.GetUserSetting(App.CurrentUserID, "WorkPackage.LastScheduler");
            if (!string.IsNullOrEmpty(lastScheduler))
            {
                var schedulerItem = userItems.FirstOrDefault(u => u.Username == lastScheduler);
                if (schedulerItem != null)
                    cboScheduler.SelectedItem = schedulerItem;
            }

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

        // Project selection changed - load work packages and save selection
        private async void CboProject_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboProject.SelectedValue is string projectId)
            {
                SettingsManager.SetUserSetting(App.CurrentUserID, "WorkPackage.LastProjectID", projectId, "string");
                await LoadWorkPackagesAsync(projectId);
            }
        }

        // WP Template selection changed - save selection
        private void CboWPTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboWPTemplate.SelectedValue is string wpTemplateId)
            {
                SettingsManager.SetUserSetting(App.CurrentUserID, "WorkPackage.LastWPTemplateID", wpTemplateId, "string");
            }
        }

        // PKG Manager selection changed - save selection
        private void CboPKGManager_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboPKGManager.SelectedItem is Models.UserItem user)
            {
                SettingsManager.SetUserSetting(App.CurrentUserID, "WorkPackage.LastPKGManager", user.Username, "string");
            }
        }

        // Scheduler selection changed - save selection
        private void CboScheduler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboScheduler.SelectedItem is Models.UserItem user)
            {
                SettingsManager.SetUserSetting(App.CurrentUserID, "WorkPackage.LastScheduler", user.Username, "string");
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
            var pkgManager = cboPKGManager.SelectedItem as Models.UserItem;
            var scheduler = cboScheduler.SelectedItem as Models.UserItem;

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
                    GetResolvedLogoPath()
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
                    // Preview single form template with actual context if on Generate tab selections exist
                    var formContext = BuildPreviewContext();
                    previewStream = _generator.GenerateFormPreview(_selectedFormTemplate, formContext,
                        GetResolvedLogoPath());
                }
                else if (cboWPTemplate.SelectedValue is string wpTemplateId)
                {
                    // Preview full WP template with actual UI selections
                    var context = BuildPreviewContext();
                    previewStream = await _generator.GeneratePreviewAsync(wpTemplateId, context,
                        GetResolvedLogoPath());
                }

                if (previewStream != null && previewStream.Length > 0)
                {
                    // Load PDF into viewer
                    previewStream.Position = 0;
                    pdfViewer.Load(previewStream);
                    pdfViewer.MinimumZoomPercentage = 10;
                    pdfViewer.ZoomMode = Syncfusion.Windows.PdfViewer.ZoomMode.FitWidth;

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

        // Save splitter position when user drags it
        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // Save the left panel width as a ratio of total width
            double totalWidth = LeftPanelColumn.ActualWidth + RightPanelColumn.ActualWidth;
            if (totalWidth > 0)
            {
                double leftRatio = LeftPanelColumn.ActualWidth / totalWidth;
                SettingsManager.SetUserSetting(App.CurrentUserID, "WorkPackage.SplitterRatio", leftRatio.ToString("F4"), "string");
            }
        }

        // Build TokenContext from current UI selections (or placeholders if not selected)
        private TokenContext BuildPreviewContext()
        {
            // Get project ID
            var projectId = cboProject.SelectedValue as string ?? "SAMPLE";

            // Get first selected work package (for preview)
            var selectedWPs = lstWorkPackages.SelectedItems.Cast<string>().ToList();
            var workPackage = selectedWPs.FirstOrDefault() ?? "50.SAMPLE.WP";

            // Get PKG Manager
            var pkgManager = cboPKGManager.SelectedItem as Models.UserItem;
            var pkgManagerUsername = pkgManager?.Username ?? "pkgmgr";
            var pkgManagerFullName = pkgManager?.FullName ?? "Package Manager";

            // Get Scheduler
            var scheduler = cboScheduler.SelectedItem as Models.UserItem;
            var schedulerUsername = scheduler?.Username ?? "scheduler";
            var schedulerFullName = scheduler?.FullName ?? "Scheduler";

            // Get WP Name Pattern
            var wpNamePattern = txtWPNamePattern.Text;

            return new TokenContext
            {
                ProjectID = projectId,
                WorkPackage = workPackage,
                PKGManagerUsername = pkgManagerUsername,
                PKGManagerFullName = pkgManagerFullName,
                SchedulerUsername = schedulerUsername,
                SchedulerFullName = schedulerFullName,
                WPNamePattern = wpNamePattern
            };
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

                // Load type-specific editor
                LoadFormTemplateEditor(template);
            }
            else if (!_suppressTypeDialog)
            {
                // "+ Add New" selected by user - show type selection dialog
                ShowTypeSelectionDialog();
            }
        }

        // Show dialog for selecting template type when creating new template
        private void ShowTypeSelectionDialog()
        {
            var dialog = new TemplateTypeDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedType))
            {
                // Create new template with selected type
                var newTemplate = new FormTemplate
                {
                    TemplateID = System.Guid.NewGuid().ToString(),
                    TemplateName = $"New {dialog.SelectedType} Template",
                    TemplateType = dialog.SelectedType,
                    IsBuiltIn = false,
                    CreatedBy = App.CurrentUser?.Username ?? "Unknown",
                    CreatedUtc = DateTime.UtcNow.ToString("o")
                };

                // Initialize with default structure based on type
                newTemplate.StructureJson = dialog.SelectedType switch
                {
                    TemplateTypes.Cover => JsonSerializer.Serialize(new CoverStructure()),
                    TemplateTypes.List => JsonSerializer.Serialize(new ListStructure()),
                    TemplateTypes.Grid => JsonSerializer.Serialize(new GridStructure()),
                    TemplateTypes.Form => JsonSerializer.Serialize(new FormStructure()),
                    _ => "{}"
                };

                _selectedFormTemplate = newTemplate;
                txtFormTemplateName.Text = newTemplate.TemplateName;
                lblFormTemplateType.Text = newTemplate.TemplateType;

                // Load the editor for this type
                LoadFormTemplateEditor(newTemplate);
                _hasUnsavedChanges = true;
            }
            else
            {
                // User cancelled - reset to first template if available
                if (_formTemplates.Count > 0)
                {
                    cboFormTemplateEdit.SelectedIndex = 0;
                }
                else
                {
                    _selectedFormTemplate = null;
                    txtFormTemplateName.Text = "";
                    lblFormTemplateType.Text = "";
                    ClearFormEditor();
                }
            }
        }

        // Load the appropriate editor for the template type
        private void LoadFormTemplateEditor(FormTemplate template)
        {
            try
            {
                switch (template.TemplateType)
                {
                    case TemplateTypes.Cover:
                        var coverStructure = JsonSerializer.Deserialize<CoverStructure>(template.StructureJson) ?? new CoverStructure();
                        BuildCoverEditor(coverStructure);
                        break;

                    case TemplateTypes.List:
                        var listStructure = JsonSerializer.Deserialize<ListStructure>(template.StructureJson) ?? new ListStructure();
                        BuildListEditor(listStructure);
                        break;

                    case TemplateTypes.Grid:
                        var gridStructure = JsonSerializer.Deserialize<GridStructure>(template.StructureJson) ?? new GridStructure();
                        BuildGridEditor(gridStructure);
                        break;

                    case TemplateTypes.Form:
                        var formStructure = JsonSerializer.Deserialize<FormStructure>(template.StructureJson) ?? new FormStructure();
                        BuildFormEditor(formStructure);
                        break;

                    case TemplateTypes.Drawings:
                        ShowEditorPlaceholder(template.TemplateType);
                        break;

                    default:
                        ShowEditorPlaceholder(template.TemplateType);
                        break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.LoadFormTemplateEditor");
                ShowEditorPlaceholder(template.TemplateType);
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

                _suppressTypeDialog = true;
                PopulateFormTemplateEditDropdown();
                _suppressTypeDialog = false;

                PopulateAddFormMenu();

                // Select first template if available
                if (_formTemplates.Count > 0)
                {
                    cboFormTemplateEdit.SelectedIndex = 1; // Skip "+ Add New"
                }

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

            // Check for duplicate name (exclude current template if editing existing)
            string newName = txtFormTemplateName.Text.Trim();
            var duplicate = _formTemplates.FirstOrDefault(t =>
                t.TemplateName.Equals(newName, StringComparison.OrdinalIgnoreCase) &&
                t.TemplateID != _selectedFormTemplate?.TemplateID);

            if (duplicate != null)
            {
                MessageBox.Show($"A form template named '{newName}' already exists. Please choose a different name.",
                    "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get structure JSON from current editor if supported
            string? editorStructureJson = GetStructureJsonFromEditor();

            // Check if saving a built-in template - preserve clone data
            if (_selectedFormTemplate?.IsBuiltIn == true)
            {
                var result = MessageBox.Show("This is a built-in template. Save as a new template?",
                    "Save as New", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                // Preserve data for clone - use editor value if available
                _clonedFormType = _selectedFormTemplate.TemplateType;
                _clonedFormStructure = editorStructureJson ?? _selectedFormTemplate.StructureJson;
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

                    // Create new from clone data - use editor changes if available
                    var template = new FormTemplate
                    {
                        TemplateID = Guid.NewGuid().ToString(),
                        TemplateName = newName,
                        TemplateType = _clonedFormType,
                        StructureJson = editorStructureJson ?? _clonedFormStructure,
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
                    // Update existing - use editor structure if available
                    _selectedFormTemplate.TemplateName = newName;
                    if (editorStructureJson != null)
                    {
                        _selectedFormTemplate.StructureJson = editorStructureJson;
                    }
                    await TemplateRepository.UpdateFormTemplateAsync(_selectedFormTemplate);
                    savedTemplateId = _selectedFormTemplate.TemplateID;
                    lblStatus.Text = "Template saved";
                }

                _hasUnsavedChanges = false;
                _formTemplates = await TemplateRepository.GetAllFormTemplatesAsync();

                _suppressTypeDialog = true;
                PopulateFormTemplateEditDropdown();
                _suppressTypeDialog = false;

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

        #region Form Template Editors

        // Build and display the Cover type editor
        private void BuildCoverEditor(CoverStructure structure)
        {
            _currentEditorType = TemplateTypes.Cover;

            var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            // Title
            panel.Children.Add(new TextBlock
            {
                Text = "Title",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            _coverTitleBox = new TextBox
            {
                Text = structure.Title,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 15)
            };
            _coverTitleBox.TextChanged += (s, e) => _hasUnsavedChanges = true;
            panel.Children.Add(_coverTitleBox);

            // Image Path
            panel.Children.Add(new TextBlock
            {
                Text = "Cover Image",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            var imagePathGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            imagePathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            imagePathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _coverImagePathBox = new TextBox
            {
                Text = structure.ImagePath ?? "(default)",
                Height = 28,
                IsReadOnly = true,
                ToolTip = "Leave empty to use default cover image"
            };
            Grid.SetColumn(_coverImagePathBox, 0);
            imagePathGrid.Children.Add(_coverImagePathBox);

            var browseBtn = new Button
            {
                Content = "Browse",
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(5, 0, 0, 0)
            };
            browseBtn.Click += BrowseCoverImage_Click;
            Grid.SetColumn(browseBtn, 1);
            imagePathGrid.Children.Add(browseBtn);

            panel.Children.Add(imagePathGrid);

            // Image Width Percent
            panel.Children.Add(new TextBlock
            {
                Text = $"Image Width: {structure.ImageWidthPercent}%",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5),
                Name = "lblImageWidth"
            });
            _coverImageWidthSlider = new Slider
            {
                Minimum = 20,
                Maximum = 100,
                Value = structure.ImageWidthPercent,
                TickFrequency = 10,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 0, 0, 15)
            };
            // Update label when slider changes
            var widthLabel = panel.Children[panel.Children.Count - 1] as TextBlock;
            _coverImageWidthSlider.ValueChanged += (s, e) =>
            {
                if (widthLabel != null)
                    widthLabel.Text = $"Image Width: {(int)_coverImageWidthSlider.Value}%";
                _hasUnsavedChanges = true;
            };
            panel.Children.Add(_coverImageWidthSlider);

            // Footer Text
            panel.Children.Add(new TextBlock
            {
                Text = "Footer Text (optional)",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            _coverFooterTextBox = new TextBox
            {
                Text = structure.FooterText ?? "",
                Height = 60,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 15)
            };
            _coverFooterTextBox.TextChanged += (s, e) => _hasUnsavedChanges = true;
            panel.Children.Add(_coverFooterTextBox);

            FormEditorContent.Content = panel;
        }

        // Browse for cover image
        private void BrowseCoverImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title = "Select Cover Image"
            };

            if (dialog.ShowDialog() == true)
            {
                if (_coverImagePathBox != null)
                {
                    _coverImagePathBox.Text = dialog.FileName;
                    _hasUnsavedChanges = true;
                }
            }
        }

        // Build and display the List type editor
        private void BuildListEditor(ListStructure structure)
        {
            _currentEditorType = TemplateTypes.List;
            _listItems = new ObservableCollection<string>(structure.Items);

            var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            // Title
            panel.Children.Add(new TextBlock
            {
                Text = "Title",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            _listTitleBox = new TextBox
            {
                Text = structure.Title,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 15)
            };
            _listTitleBox.TextChanged += (s, e) => _hasUnsavedChanges = true;
            panel.Children.Add(_listTitleBox);

            // Items Label
            panel.Children.Add(new TextBlock
            {
                Text = "Items (use blank lines for spacing)",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });

            // Items list with buttons
            var itemsGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            itemsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _listItemsBox = new ListBox
            {
                Height = 180,
                ItemsSource = _listItems,
                ToolTip = "Select an item to move or remove"
            };
            Grid.SetColumn(_listItemsBox, 0);
            itemsGrid.Children.Add(_listItemsBox);

            // Button panel
            var btnPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
            var btnUp = new Button { Content = "▲", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 5), ToolTip = "Move up" };
            btnUp.Click += ListItemMoveUp_Click;
            btnPanel.Children.Add(btnUp);

            var btnDown = new Button { Content = "▼", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 5), ToolTip = "Move down" };
            btnDown.Click += ListItemMoveDown_Click;
            btnPanel.Children.Add(btnDown);

            var btnRemove = new Button { Content = "✕", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 5), ToolTip = "Remove" };
            btnRemove.Click += ListItemRemove_Click;
            btnPanel.Children.Add(btnRemove);

            var btnEdit = new Button { Content = "✎", Padding = new Thickness(8, 4, 8, 4), ToolTip = "Edit" };
            btnEdit.Click += ListItemEdit_Click;
            btnPanel.Children.Add(btnEdit);

            Grid.SetColumn(btnPanel, 1);
            itemsGrid.Children.Add(btnPanel);
            panel.Children.Add(itemsGrid);

            // Add new item row
            var addGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _listNewItemBox = new TextBox { Height = 28, ToolTip = "Enter new item text" };
            _listNewItemBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) ListItemAdd_Click(s, e);
            };
            Grid.SetColumn(_listNewItemBox, 0);
            addGrid.Children.Add(_listNewItemBox);

            var btnAdd = new Button { Content = "+ Add", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(5, 0, 0, 0) };
            btnAdd.Click += ListItemAdd_Click;
            Grid.SetColumn(btnAdd, 1);
            addGrid.Children.Add(btnAdd);
            panel.Children.Add(addGrid);

            // Footer Text
            panel.Children.Add(new TextBlock
            {
                Text = "Footer Text (optional)",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            _listFooterTextBox = new TextBox
            {
                Text = structure.FooterText ?? "",
                Height = 60,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 15)
            };
            _listFooterTextBox.TextChanged += (s, e) => _hasUnsavedChanges = true;
            panel.Children.Add(_listFooterTextBox);

            FormEditorContent.Content = panel;
        }

        // List item button handlers
        private void ListItemMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_listItemsBox?.SelectedIndex > 0 && _listItems != null)
            {
                int idx = _listItemsBox.SelectedIndex;
                _listItems.Move(idx, idx - 1);
                _listItemsBox.SelectedIndex = idx - 1;
                _hasUnsavedChanges = true;
            }
        }

        private void ListItemMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_listItemsBox != null && _listItems != null && _listItemsBox.SelectedIndex >= 0 && _listItemsBox.SelectedIndex < _listItems.Count - 1)
            {
                int idx = _listItemsBox.SelectedIndex;
                _listItems.Move(idx, idx + 1);
                _listItemsBox.SelectedIndex = idx + 1;
                _hasUnsavedChanges = true;
            }
        }

        private void ListItemRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_listItemsBox?.SelectedIndex >= 0 && _listItems != null)
            {
                _listItems.RemoveAt(_listItemsBox.SelectedIndex);
                _hasUnsavedChanges = true;
            }
        }

        private void ListItemEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_listItemsBox?.SelectedIndex >= 0 && _listItems != null && _listNewItemBox != null)
            {
                int idx = _listItemsBox.SelectedIndex;
                // Copy selected item to the new item box for editing
                _listNewItemBox.Text = _listItems[idx];
                _listItems.RemoveAt(idx);
                _listNewItemBox.Focus();
                _hasUnsavedChanges = true;
            }
        }

        private void ListItemAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_listNewItemBox != null && _listItems != null)
            {
                _listItems.Add(_listNewItemBox.Text);
                _listNewItemBox.Clear();
                _hasUnsavedChanges = true;
            }
        }

        // Get List structure JSON from editor
        private string GetListStructureJson()
        {
            var structure = new ListStructure
            {
                Title = _listTitleBox?.Text ?? "WORK PACKAGE TABLE OF CONTENTS",
                Items = _listItems?.ToList() ?? new List<string>(),
                FooterText = string.IsNullOrWhiteSpace(_listFooterTextBox?.Text) ? null : _listFooterTextBox.Text
            };
            return JsonSerializer.Serialize(structure);
        }

        // Get Cover structure JSON from editor
        private string GetCoverStructureJson()
        {
            var structure = new CoverStructure
            {
                Title = _coverTitleBox?.Text ?? "WORK PACKAGE COVER SHEET",
                ImagePath = _coverImagePathBox?.Text == "(default)" ? null : _coverImagePathBox?.Text,
                ImageWidthPercent = (int)(_coverImageWidthSlider?.Value ?? 80),
                FooterText = string.IsNullOrWhiteSpace(_coverFooterTextBox?.Text) ? null : _coverFooterTextBox.Text
            };
            return JsonSerializer.Serialize(structure);
        }

        // Build and display the Grid type editor
        private void BuildGridEditor(GridStructure structure)
        {
            _currentEditorType = TemplateTypes.Grid;
            _gridColumns = new ObservableCollection<TemplateColumn>(structure.Columns);

            var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            // Title
            panel.Children.Add(new TextBlock
            {
                Text = "Title",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            _gridTitleBox = new TextBox
            {
                Text = structure.Title,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 15)
            };
            _gridTitleBox.TextChanged += (s, e) => _hasUnsavedChanges = true;
            panel.Children.Add(_gridTitleBox);

            // Columns Label
            panel.Children.Add(new TextBlock
            {
                Text = "Columns (name and width %)",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });

            // Columns list with buttons
            var colGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            colGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            colGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _gridColumnsBox = new ListBox
            {
                Height = 140,
                ItemsSource = _gridColumns,
                ToolTip = "Select a column to move or remove"
            };
            // Custom display for columns showing name and width
            _gridColumnsBox.ItemTemplate = CreateColumnItemTemplate();
            Grid.SetColumn(_gridColumnsBox, 0);
            colGrid.Children.Add(_gridColumnsBox);

            // Button panel
            var btnPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
            var btnUp = new Button { Content = "▲", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 5), ToolTip = "Move up" };
            btnUp.Click += GridColumnMoveUp_Click;
            btnPanel.Children.Add(btnUp);

            var btnDown = new Button { Content = "▼", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 5), ToolTip = "Move down" };
            btnDown.Click += GridColumnMoveDown_Click;
            btnPanel.Children.Add(btnDown);

            var btnRemove = new Button { Content = "✕", Padding = new Thickness(8, 4, 8, 4), ToolTip = "Remove" };
            btnRemove.Click += GridColumnRemove_Click;
            btnPanel.Children.Add(btnRemove);

            Grid.SetColumn(btnPanel, 1);
            colGrid.Children.Add(btnPanel);
            panel.Children.Add(colGrid);

            // Add new column row
            var addGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _gridNewColumnNameBox = new TextBox { Height = 28, ToolTip = "Column name" };
            Grid.SetColumn(_gridNewColumnNameBox, 0);
            addGrid.Children.Add(_gridNewColumnNameBox);

            _gridNewColumnWidthBox = new Syncfusion.Windows.Shared.IntegerTextBox
            {
                Value = 20,
                MinValue = 1,
                MaxValue = 100,
                Height = 28,
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Width %"
            };
            Grid.SetColumn(_gridNewColumnWidthBox, 1);
            addGrid.Children.Add(_gridNewColumnWidthBox);

            var btnAdd = new Button { Content = "+ Add", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(5, 0, 0, 0) };
            btnAdd.Click += GridColumnAdd_Click;
            Grid.SetColumn(btnAdd, 2);
            addGrid.Children.Add(btnAdd);
            panel.Children.Add(addGrid);

            // Row Count
            var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            rowGrid.Children.Add(new TextBlock
            {
                Text = "Row Count:",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });

            _gridRowCountBox = new Syncfusion.Windows.Shared.IntegerTextBox
            {
                Value = structure.RowCount,
                MinValue = 1,
                MaxValue = 100,
                Height = 28,
                ToolTip = "Number of empty rows"
            };
            _gridRowCountBox.ValueChanged += (s, e) => _hasUnsavedChanges = true;
            Grid.SetColumn(_gridRowCountBox, 1);
            rowGrid.Children.Add(_gridRowCountBox);
            panel.Children.Add(rowGrid);

            // Row Height Increase
            var heightLabel = new TextBlock
            {
                Text = $"Row Height Increase: {structure.RowHeightIncreasePercent}%",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(heightLabel);

            _gridRowHeightSlider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = structure.RowHeightIncreasePercent,
                TickFrequency = 10,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 0, 0, 15)
            };
            _gridRowHeightSlider.ValueChanged += (s, e) =>
            {
                heightLabel.Text = $"Row Height Increase: {(int)_gridRowHeightSlider.Value}%";
                _hasUnsavedChanges = true;
            };
            panel.Children.Add(_gridRowHeightSlider);

            // Footer Text
            panel.Children.Add(new TextBlock
            {
                Text = "Footer Text (optional)",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            _gridFooterTextBox = new TextBox
            {
                Text = structure.FooterText ?? "",
                Height = 60,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 15)
            };
            _gridFooterTextBox.TextChanged += (s, e) => _hasUnsavedChanges = true;
            panel.Children.Add(_gridFooterTextBox);

            FormEditorContent.Content = panel;
        }

        // Create item template for column display
        private DataTemplate CreateColumnItemTemplate()
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding
            {
                Converter = new ColumnDisplayConverter()
            });
            template.VisualTree = factory;
            return template;
        }

        // Grid column button handlers
        private void GridColumnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_gridColumnsBox?.SelectedIndex > 0 && _gridColumns != null)
            {
                int idx = _gridColumnsBox.SelectedIndex;
                _gridColumns.Move(idx, idx - 1);
                _gridColumnsBox.SelectedIndex = idx - 1;
                _hasUnsavedChanges = true;
            }
        }

        private void GridColumnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_gridColumnsBox != null && _gridColumns != null && _gridColumnsBox.SelectedIndex >= 0 && _gridColumnsBox.SelectedIndex < _gridColumns.Count - 1)
            {
                int idx = _gridColumnsBox.SelectedIndex;
                _gridColumns.Move(idx, idx + 1);
                _gridColumnsBox.SelectedIndex = idx + 1;
                _hasUnsavedChanges = true;
            }
        }

        private void GridColumnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_gridColumnsBox?.SelectedIndex >= 0 && _gridColumns != null)
            {
                _gridColumns.RemoveAt(_gridColumnsBox.SelectedIndex);
                _hasUnsavedChanges = true;
            }
        }

        private void GridColumnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_gridNewColumnNameBox != null && _gridNewColumnWidthBox != null && _gridColumns != null)
            {
                if (string.IsNullOrWhiteSpace(_gridNewColumnNameBox.Text))
                {
                    MessageBox.Show("Please enter a column name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _gridColumns.Add(new TemplateColumn
                {
                    Name = _gridNewColumnNameBox.Text,
                    WidthPercent = (int)(_gridNewColumnWidthBox.Value ?? 20)
                });
                _gridNewColumnNameBox.Clear();
                _hasUnsavedChanges = true;
            }
        }

        // Get Grid structure JSON from editor
        private string GetGridStructureJson()
        {
            var structure = new GridStructure
            {
                Title = _gridTitleBox?.Text ?? "WORK PACKAGE GRID",
                Columns = _gridColumns?.ToList() ?? new List<TemplateColumn>(),
                RowCount = (int)(_gridRowCountBox?.Value ?? 22),
                RowHeightIncreasePercent = (int)(_gridRowHeightSlider?.Value ?? 0),
                FooterText = string.IsNullOrWhiteSpace(_gridFooterTextBox?.Text) ? null : _gridFooterTextBox.Text
            };
            return JsonSerializer.Serialize(structure);
        }

        // Build Form editor (sections with items and columns)
        private void BuildFormEditor(FormStructure structure)
        {
            _currentEditorType = TemplateTypes.Form;
            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel = new StackPanel { Margin = new Thickness(15) };

            // Title
            panel.Children.Add(new TextBlock
            {
                Text = "Title",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            _formTitleBox = new TextBox
            {
                Text = structure.Title,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 15)
            };
            _formTitleBox.TextChanged += (s, e) => _hasUnsavedChanges = true;
            panel.Children.Add(_formTitleBox);

            // Columns section
            panel.Children.Add(new TextBlock
            {
                Text = "Columns",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var colGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            colGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            colGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _formColumns = new ObservableCollection<TemplateColumn>(structure.Columns);
            _formColumnsBox = new ListBox
            {
                Height = 80,
                ItemsSource = _formColumns
            };
            _formColumnsBox.ItemTemplate = CreateColumnItemTemplate();
            Grid.SetColumn(_formColumnsBox, 0);
            colGrid.Children.Add(_formColumnsBox);

            var colBtnPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
            var btnColUp = new Button { Content = "▲", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Move up" };
            btnColUp.Click += FormColumnMoveUp_Click;
            colBtnPanel.Children.Add(btnColUp);
            var btnColDown = new Button { Content = "▼", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Move down" };
            btnColDown.Click += FormColumnMoveDown_Click;
            colBtnPanel.Children.Add(btnColDown);
            var btnColRemove = new Button { Content = "✕", Padding = new Thickness(8, 4, 8, 4), ToolTip = "Remove" };
            btnColRemove.Click += FormColumnRemove_Click;
            colBtnPanel.Children.Add(btnColRemove);
            Grid.SetColumn(colBtnPanel, 1);
            colGrid.Children.Add(colBtnPanel);
            panel.Children.Add(colGrid);

            // Add column row
            var addColGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            addColGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addColGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            addColGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _formNewColumnNameBox = new TextBox { Height = 28, ToolTip = "Column name" };
            Grid.SetColumn(_formNewColumnNameBox, 0);
            addColGrid.Children.Add(_formNewColumnNameBox);

            _formNewColumnWidthBox = new Syncfusion.Windows.Shared.IntegerTextBox
            {
                Value = 20,
                MinValue = 5,
                MaxValue = 100,
                Height = 28,
                Width = 55,
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Width %"
            };
            Grid.SetColumn(_formNewColumnWidthBox, 1);
            addColGrid.Children.Add(_formNewColumnWidthBox);

            var btnAddCol = new Button { Content = "Add", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(5, 0, 0, 0), ToolTip = "Add column" };
            btnAddCol.Click += FormColumnAdd_Click;
            Grid.SetColumn(btnAddCol, 2);
            addColGrid.Children.Add(btnAddCol);
            panel.Children.Add(addColGrid);

            // Separator
            panel.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 15) });

            // Sections
            panel.Children.Add(new TextBlock
            {
                Text = "Sections",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var secGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            secGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            secGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _formSections = new ObservableCollection<SectionDefinition>(structure.Sections);
            _formSectionsBox = new ListBox
            {
                Height = 80,
                ItemsSource = _formSections,
                DisplayMemberPath = "Name"
            };
            _formSectionsBox.SelectionChanged += FormSectionSelected;
            Grid.SetColumn(_formSectionsBox, 0);
            secGrid.Children.Add(_formSectionsBox);

            var secBtnPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
            var btnSecUp = new Button { Content = "▲", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Move up" };
            btnSecUp.Click += FormSectionMoveUp_Click;
            secBtnPanel.Children.Add(btnSecUp);
            var btnSecDown = new Button { Content = "▼", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Move down" };
            btnSecDown.Click += FormSectionMoveDown_Click;
            secBtnPanel.Children.Add(btnSecDown);
            var btnSecRemove = new Button { Content = "✕", Padding = new Thickness(8, 4, 8, 4), ToolTip = "Remove" };
            btnSecRemove.Click += FormSectionRemove_Click;
            secBtnPanel.Children.Add(btnSecRemove);
            Grid.SetColumn(secBtnPanel, 1);
            secGrid.Children.Add(secBtnPanel);
            panel.Children.Add(secGrid);

            // Add section row
            var addSecGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            addSecGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addSecGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _formNewSectionBox = new TextBox { Height = 28, ToolTip = "Section name" };
            Grid.SetColumn(_formNewSectionBox, 0);
            addSecGrid.Children.Add(_formNewSectionBox);

            var btnAddSec = new Button { Content = "Add", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(5, 0, 0, 0), ToolTip = "Add section" };
            btnAddSec.Click += FormSectionAdd_Click;
            Grid.SetColumn(btnAddSec, 1);
            addSecGrid.Children.Add(btnAddSec);
            panel.Children.Add(addSecGrid);

            // Section Items (shown when section selected)
            panel.Children.Add(new TextBlock
            {
                Text = "Section Items",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "(Select a section above to edit its items)",
                FontStyle = FontStyles.Italic,
                Foreground = (Brush)FindResource("TextColorSecondary"),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var itemGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _formSectionItemsBox = new ListBox { Height = 80 };
            Grid.SetColumn(_formSectionItemsBox, 0);
            itemGrid.Children.Add(_formSectionItemsBox);

            var itemBtnPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
            var btnItemUp = new Button { Content = "▲", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Move up" };
            btnItemUp.Click += FormItemMoveUp_Click;
            itemBtnPanel.Children.Add(btnItemUp);
            var btnItemDown = new Button { Content = "▼", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Move down" };
            btnItemDown.Click += FormItemMoveDown_Click;
            itemBtnPanel.Children.Add(btnItemDown);
            var btnItemRemove = new Button { Content = "✕", Padding = new Thickness(8, 4, 8, 4), ToolTip = "Remove" };
            btnItemRemove.Click += FormItemRemove_Click;
            itemBtnPanel.Children.Add(btnItemRemove);
            Grid.SetColumn(itemBtnPanel, 1);
            itemGrid.Children.Add(itemBtnPanel);
            panel.Children.Add(itemGrid);

            // Add item row
            var addItemGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            addItemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addItemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _formNewItemBox = new TextBox { Height = 28, ToolTip = "Item text" };
            Grid.SetColumn(_formNewItemBox, 0);
            addItemGrid.Children.Add(_formNewItemBox);

            var btnAddItem = new Button { Content = "Add", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(5, 0, 0, 0), ToolTip = "Add item" };
            btnAddItem.Click += FormItemAdd_Click;
            Grid.SetColumn(btnAddItem, 1);
            addItemGrid.Children.Add(btnAddItem);
            panel.Children.Add(addItemGrid);

            // Separator
            panel.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 15) });

            // Row Height Increase slider
            panel.Children.Add(new TextBlock
            {
                Text = "Row Height Increase %",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            var sliderGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            _formRowHeightSlider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = structure.RowHeightIncreasePercent,
                TickFrequency = 10,
                IsSnapToTickEnabled = true,
                ToolTip = "Increase row height for taller text"
            };
            _formRowHeightSlider.ValueChanged += (s, e) => _hasUnsavedChanges = true;
            Grid.SetColumn(_formRowHeightSlider, 0);
            sliderGrid.Children.Add(_formRowHeightSlider);

            var sliderLabel = new TextBlock
            {
                Foreground = (Brush)FindResource("ForegroundColor"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            sliderLabel.SetBinding(TextBlock.TextProperty, new Binding("Value")
            {
                Source = _formRowHeightSlider,
                StringFormat = "{0:0}%"
            });
            Grid.SetColumn(sliderLabel, 1);
            sliderGrid.Children.Add(sliderLabel);
            panel.Children.Add(sliderGrid);

            // Footer Text
            panel.Children.Add(new TextBlock
            {
                Text = "Footer Text (optional)",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            _formFooterTextBox = new TextBox
            {
                Text = structure.FooterText ?? "",
                Height = 28
            };
            _formFooterTextBox.TextChanged += (s, e) => _hasUnsavedChanges = true;
            panel.Children.Add(_formFooterTextBox);

            scrollViewer.Content = panel;
            FormEditorContent.Content = scrollViewer;
        }

        // Form column button handlers
        private void FormColumnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_formColumnsBox?.SelectedIndex > 0 && _formColumns != null)
            {
                int idx = _formColumnsBox.SelectedIndex;
                _formColumns.Move(idx, idx - 1);
                _formColumnsBox.SelectedIndex = idx - 1;
                _hasUnsavedChanges = true;
            }
        }

        private void FormColumnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_formColumnsBox != null && _formColumns != null &&
                _formColumnsBox.SelectedIndex >= 0 && _formColumnsBox.SelectedIndex < _formColumns.Count - 1)
            {
                int idx = _formColumnsBox.SelectedIndex;
                _formColumns.Move(idx, idx + 1);
                _formColumnsBox.SelectedIndex = idx + 1;
                _hasUnsavedChanges = true;
            }
        }

        private void FormColumnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_formColumnsBox?.SelectedIndex >= 0 && _formColumns != null)
            {
                _formColumns.RemoveAt(_formColumnsBox.SelectedIndex);
                _hasUnsavedChanges = true;
            }
        }

        private void FormColumnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_formNewColumnNameBox != null && _formNewColumnWidthBox != null && _formColumns != null)
            {
                if (string.IsNullOrWhiteSpace(_formNewColumnNameBox.Text))
                {
                    MessageBox.Show("Please enter a column name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _formColumns.Add(new TemplateColumn
                {
                    Name = _formNewColumnNameBox.Text,
                    WidthPercent = (int)(_formNewColumnWidthBox.Value ?? 20)
                });
                _formNewColumnNameBox.Clear();
                _hasUnsavedChanges = true;
            }
        }

        // Form section button handlers
        private void FormSectionMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_formSectionsBox?.SelectedIndex > 0 && _formSections != null)
            {
                int idx = _formSectionsBox.SelectedIndex;
                _formSections.Move(idx, idx - 1);
                _formSectionsBox.SelectedIndex = idx - 1;
                _hasUnsavedChanges = true;
            }
        }

        private void FormSectionMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_formSectionsBox != null && _formSections != null &&
                _formSectionsBox.SelectedIndex >= 0 && _formSectionsBox.SelectedIndex < _formSections.Count - 1)
            {
                int idx = _formSectionsBox.SelectedIndex;
                _formSections.Move(idx, idx + 1);
                _formSectionsBox.SelectedIndex = idx + 1;
                _hasUnsavedChanges = true;
            }
        }

        private void FormSectionRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_formSectionsBox?.SelectedIndex >= 0 && _formSections != null)
            {
                _formSections.RemoveAt(_formSectionsBox.SelectedIndex);
                if (_formSectionItemsBox != null)
                    _formSectionItemsBox.ItemsSource = null;
                _hasUnsavedChanges = true;
            }
        }

        private void FormSectionAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_formNewSectionBox != null && _formSections != null)
            {
                if (string.IsNullOrWhiteSpace(_formNewSectionBox.Text))
                {
                    MessageBox.Show("Please enter a section name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _formSections.Add(new SectionDefinition { Name = _formNewSectionBox.Text });
                _formNewSectionBox.Clear();
                _hasUnsavedChanges = true;
            }
        }

        // Called when section selection changes - show items for selected section
        private void FormSectionSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_formSectionsBox?.SelectedItem is SectionDefinition section && _formSectionItemsBox != null)
            {
                _formSectionItemsBox.ItemsSource = section.Items;
            }
            else if (_formSectionItemsBox != null)
            {
                _formSectionItemsBox.ItemsSource = null;
            }
        }

        // Form item button handlers (for items within selected section)
        private void FormItemMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_formSectionsBox?.SelectedItem is SectionDefinition section &&
                _formSectionItemsBox?.SelectedIndex > 0)
            {
                int idx = _formSectionItemsBox.SelectedIndex;
                var item = section.Items[idx];
                section.Items.RemoveAt(idx);
                section.Items.Insert(idx - 1, item);
                _formSectionItemsBox.ItemsSource = null;
                _formSectionItemsBox.ItemsSource = section.Items;
                _formSectionItemsBox.SelectedIndex = idx - 1;
                _hasUnsavedChanges = true;
            }
        }

        private void FormItemMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (_formSectionsBox?.SelectedItem is SectionDefinition section &&
                _formSectionItemsBox?.SelectedIndex >= 0 &&
                _formSectionItemsBox.SelectedIndex < section.Items.Count - 1)
            {
                int idx = _formSectionItemsBox.SelectedIndex;
                var item = section.Items[idx];
                section.Items.RemoveAt(idx);
                section.Items.Insert(idx + 1, item);
                _formSectionItemsBox.ItemsSource = null;
                _formSectionItemsBox.ItemsSource = section.Items;
                _formSectionItemsBox.SelectedIndex = idx + 1;
                _hasUnsavedChanges = true;
            }
        }

        private void FormItemRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_formSectionsBox?.SelectedItem is SectionDefinition section &&
                _formSectionItemsBox?.SelectedIndex >= 0)
            {
                section.Items.RemoveAt(_formSectionItemsBox.SelectedIndex);
                _formSectionItemsBox.ItemsSource = null;
                _formSectionItemsBox.ItemsSource = section.Items;
                _hasUnsavedChanges = true;
            }
        }

        private void FormItemAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_formSectionsBox?.SelectedItem is SectionDefinition section && _formNewItemBox != null)
            {
                if (string.IsNullOrWhiteSpace(_formNewItemBox.Text))
                {
                    MessageBox.Show("Please enter an item text.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                section.Items.Add(_formNewItemBox.Text);
                if (_formSectionItemsBox != null)
                {
                    _formSectionItemsBox.ItemsSource = null;
                    _formSectionItemsBox.ItemsSource = section.Items;
                }
                _formNewItemBox.Clear();
                _hasUnsavedChanges = true;
            }
            else
            {
                MessageBox.Show("Please select a section first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Get Form structure JSON from editor
        private string GetFormStructureJson()
        {
            var structure = new FormStructure
            {
                Title = _formTitleBox?.Text ?? "WORK PACKAGE FORM",
                Columns = _formColumns?.ToList() ?? new List<TemplateColumn>(),
                Sections = _formSections?.ToList() ?? new List<SectionDefinition>(),
                RowHeightIncreasePercent = (int)(_formRowHeightSlider?.Value ?? 0),
                FooterText = string.IsNullOrWhiteSpace(_formFooterTextBox?.Text) ? null : _formFooterTextBox.Text
            };
            return JsonSerializer.Serialize(structure);
        }

        // Get structure JSON from current editor (returns null if editor doesn't support editing)
        private string? GetStructureJsonFromEditor()
        {
            return _currentEditorType switch
            {
                TemplateTypes.Cover => GetCoverStructureJson(),
                TemplateTypes.List => GetListStructureJson(),
                TemplateTypes.Grid => GetGridStructureJson(),
                TemplateTypes.Form => GetFormStructureJson(),
                _ => null // Return null for unsupported editors (will use original structure)
            };
        }

        // Clear editor content
        private void ClearFormEditor()
        {
            FormEditorContent.Content = null;
            _currentEditorType = null;
        }

        // Show placeholder for unsupported editor types
        private void ShowEditorPlaceholder(string templateType)
        {
            _currentEditorType = templateType;
            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20)
            };
            panel.Children.Add(new TextBlock
            {
                Text = $"Editor for '{templateType}' type",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Coming soon - clone existing templates for now",
                Foreground = (Brush)FindResource("TextColorSecondary"),
                FontStyle = FontStyles.Italic,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            FormEditorContent.Content = panel;
        }

        #endregion
    }
}
