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
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Syncfusion.Windows.Tools.Controls;
using VANTAGE.Models;
using VANTAGE.Models.ProgressBook;
using VANTAGE.Repositories;
using VANTAGE.Services;
using VANTAGE.Services.PdfRenderers;
using VANTAGE.Services.ProgressBook;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;
using VANTAGE.Dialogs;
using VANTAGE.Converters;

namespace VANTAGE.Views
{
    public partial class WorkPackageView : UserControl
    {
        private List<FormTemplate> _formTemplates = new();
        private List<WPTemplate> _wpTemplates = new();
        private List<Models.ProjectItem> _projects = new();
        private List<User> _users = new();
        private ObservableCollection<FormTemplate> _wpFormsList = new();

        // Saved Progress Book layouts, offered as addable forms in a WP template. Loaded with the
        // rest of the template library; a layout added to a WP is represented in _wpFormsList by a
        // synthetic FormTemplate (TemplateType ProgBook) carrying LinkedProgressBookLayoutId.
        private List<ProgressBookLayout> _progressBookLayouts = new();

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
        private Button? _coverImageBrowseBtn;
        private CheckBox? _coverNoImageBox;
        private Slider? _coverImageWidthSlider;
        private TextBox? _coverFooterTextBox;

        // External file editor controls
        private TextBox? _externalFilePathBox;
        private TextBox? _externalRenameBox;

        // List editor controls
        private TextBox? _listTitleBox;
        private ListBox? _listItemsBox;
        private ObservableCollection<string>? _listItems;
        private TextBox? _listNewItemBox;
        private TextBox? _listEditItemBox;
        private TextBox? _listFooterTextBox;
        private Slider? _listFontSizeSlider;
        private Grid? _listAddPanel;
        private Grid? _listEditPanel;
        private int _listEditIndex = -1;

        // Predefined TOC list items available in Add Item dropdown
        private static readonly List<(string Label, string Value)> _tocPredefinedItems = new()
        {
            ("WP Doc Expiration Date", "WP DOC EXPIRATION DATE: {ExpirationDate}"),
            ("Printed Date", "PRINTED: {PrintedDate}"),
            ("WP Name", "WP NAME: {WPName}"),
            ("Schedule Activity No", "SCHEDULE ACTIVITY NO: {SchedActNO}"),
            ("Phase Code", "PHASE CODE: {PhaseCode}")
        };

        // Spacing/separator items (shown after a separator line in dropdown)
        private static readonly List<(string Label, string Value)> _tocSpacingItems = new()
        {
            ("Blank Line", ""),
            ("Line Separator", "---")
        };

        // Grid editor controls
        private TextBox? _gridTitleBox;
        private ListBox? _gridColumnsBox;
        private ObservableCollection<TemplateColumn>? _gridColumns;
        private TextBox? _gridNewColumnNameBox;
        private Syncfusion.Windows.Shared.IntegerTextBox? _gridNewColumnWidthBox;
        private Syncfusion.Windows.Shared.IntegerTextBox? _gridRowCountBox;
        private Slider? _gridRowHeightSlider;
        private Slider? _gridFontSizeSlider;
        private TextBox? _gridFooterTextBox;
        private Grid? _gridColumnEditPanel;
        private TextBox? _gridColumnEditNameBox;
        private Syncfusion.Windows.Shared.IntegerTextBox? _gridColumnEditWidthBox;
        private int _gridColumnEditIndex = -1;
        private float _gridBaseHeaderFontSize = 9f;  // Preserve template's base font size

        // Form editor controls
        private TextBox? _formTitleBox;
        private ListBox? _formColumnsBox;
        private ObservableCollection<TemplateColumn>? _formColumns;
        private TextBox? _formNewColumnNameBox;
        private Syncfusion.Windows.Shared.IntegerTextBox? _formNewColumnWidthBox;
        private Grid? _formColumnEditPanel;
        private TextBox? _formColumnEditNameBox;
        private Syncfusion.Windows.Shared.IntegerTextBox? _formColumnEditWidthBox;
        private int _formColumnEditIndex = -1;
        private ListBox? _formSectionsBox;
        private ObservableCollection<SectionDefinition>? _formSections;
        private TextBox? _formNewSectionBox;
        private Grid? _formSectionEditPanel;
        private TextBox? _formSectionEditBox;
        private int _formSectionEditIndex = -1;
        private ListBox? _formSectionItemsBox;
        private TextBox? _formNewItemBox;
        private Grid? _formItemEditPanel;
        private TextBox? _formItemEditBox;
        private int _formItemEditIndex = -1;
        private Slider? _formRowHeightSlider;
        private Slider? _formFontSizeSlider;
        private TextBox? _formFooterTextBox;

        // Drawings editor fields (parent folder + in-place rename)
        private TextBox? _drawingsFolderPathBox;
        private TextBox? _drawingsRenameBox;

        private readonly WorkPackageGenerator _generator = new();

        // Gets the default logo path if it exists, otherwise null
        private string? GetDefaultLogoPath()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "System", "SummitS-Full Summit Peak Logo.jpg");
            return File.Exists(path) ? path : null;
        }

        // Resolves the logo path. Returns "(none)" sentinel when "No Image" is checked
        // (renderer treats this as "skip drawing"); otherwise resolves "(default)" or
        // returns the user-picked path.
        private string? GetResolvedLogoPath()
        {
            if (chkLogoNoImage?.IsChecked == true)
                return "(none)";
            if (string.IsNullOrEmpty(txtLogoPath.Text) || txtLogoPath.Text == "(default)")
                return GetDefaultLogoPath();
            return txtLogoPath.Text;
        }

        public WorkPackageView()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
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
                _progressBookLayouts = await ProgressBookLayoutRepository.GetAllAsync();

                // Load projects and users from local database
                await LoadProjectsAndUsersAsync();

                // Populate dropdowns
                PopulateDropdowns();

                // Load last used output folder from settings (no default - user must set)
                var lastOutput = SettingsManager.GetUserSetting( "WorkPackage.LastOutputPath");
                if (!string.IsNullOrEmpty(lastOutput))
                {
                    txtOutputFolder.Text = lastOutput;
                }

                // Load last used logo path from settings, or show "(default)" for built-in logo
                var lastLogo = SettingsManager.GetUserSetting( "WorkPackage.LastLogoPath");
                if (!string.IsNullOrEmpty(lastLogo) && File.Exists(lastLogo))
                {
                    txtLogoPath.Text = lastLogo;
                }
                else
                {
                    txtLogoPath.Text = "(default)";
                }

                // Load last "Logo No Image" preference and apply enabled state to path/browse
                var lastNoLogo = SettingsManager.GetUserSetting("WorkPackage.LogoNoImage");
                chkLogoNoImage.IsChecked = string.Equals(lastNoLogo, "True", StringComparison.OrdinalIgnoreCase);
                ApplyLogoNoImageEnabledState();

                // Load last "No Subfolders" / "Individual PDFs" preferences. Both default false.
                // The Changed handlers enforce the mutex, so if a prior session left both
                // somehow true, loading IndividualPdfs second auto-corrects NoSubfolders.
                var lastNoSubfolders = SettingsManager.GetUserSetting("WorkPackage.NoSubfolders");
                chkNoSubfolders.IsChecked = string.Equals(lastNoSubfolders, "True", StringComparison.OrdinalIgnoreCase);

                var lastIndividualPdfs = SettingsManager.GetUserSetting("WorkPackage.IndividualPdfs");
                chkIndividualPdfs.IsChecked = string.Equals(lastIndividualPdfs, "True", StringComparison.OrdinalIgnoreCase);

                // Load last used WP Name Pattern from settings
                var lastPattern = SettingsManager.GetUserSetting("WorkPackage.WPNamePattern");
                if (!string.IsNullOrEmpty(lastPattern))
                {
                    txtWPNamePattern.Text = lastPattern;
                }

                // Restore splitter position
                var splitterRatio = SettingsManager.GetUserSetting( "WorkPackage.SplitterRatio");
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
            var lastProjectId = SettingsManager.GetUserSetting( "WorkPackage.LastProjectID");
            if (!string.IsNullOrEmpty(lastProjectId) && _projects.Any(p => p.ProjectID == lastProjectId))
            {
                cboProject.SelectedValue = lastProjectId;
            }

            // WP Templates dropdown (Generate tab)
            cboWPTemplate.ItemsSource = _wpTemplates;

            // Restore last selected WP template, or default to first
            var lastWPTemplateId = SettingsManager.GetUserSetting( "WorkPackage.LastWPTemplateID");
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
            var lastPKGManager = SettingsManager.GetUserSetting( "WorkPackage.LastPKGManager");
            if (!string.IsNullOrEmpty(lastPKGManager))
            {
                var pkgManagerItem = userItems.FirstOrDefault(u => u.Username == lastPKGManager);
                if (pkgManagerItem != null)
                    cboPKGManager.SelectedItem = pkgManagerItem;
            }

            // Restore last selected Scheduler
            var lastScheduler = SettingsManager.GetUserSetting( "WorkPackage.LastScheduler");
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

        // Sort order for built-in form templates
        private static readonly List<string> _builtInFormOrder = new()
        {
            "Cover Sheet - Template",
            "Table of Contents - Template",
            "Checklist - Template",
            "Punchlist - Template",
            "Signoff Sheet - Template",
            "Drawing Log - Template",
            "Drawings - Placeholder"
        };

        // Sort form templates: built-in in specified order, then user-created alphabetically
        private List<FormTemplate> GetSortedFormTemplates()
        {
            var builtIn = _formTemplates.Where(t => t.IsBuiltIn)
                .OrderBy(t => {
                    var idx = _builtInFormOrder.IndexOf(t.TemplateName);
                    return idx >= 0 ? idx : int.MaxValue;
                })
                .ToList();

            var userCreated = _formTemplates.Where(t => !t.IsBuiltIn)
                .OrderBy(t => t.TemplateName)
                .ToList();

            return builtIn.Concat(userCreated).ToList();
        }

        // Populate Add Form dropdown menu with form templates
        // Note: Drawings type excluded for v1 (per-WP location architecture needs design)
        private void PopulateAddFormMenu()
        {
            menuAddFormGroup.Items.Clear();
            foreach (var template in GetSortedFormTemplates())
            {
                var menuItem = new DropDownMenuItem
                {
                    Header = template.TemplateName,
                    Tag = template
                };
                menuItem.Click += AddFormMenuItem_Click;
                menuAddFormGroup.Items.Add(menuItem);
            }

            // Saved Progress Book layouts, offered as addable forms. Each is tagged with a
            // synthetic FormTemplate so it flows through the same list/add plumbing.
            foreach (var layout in _progressBookLayouts.OrderBy(l => l.Name))
            {
                var item = MakeLayoutFormItem(layout);
                var menuItem = new DropDownMenuItem
                {
                    Header = item.TemplateName,
                    Tag = item
                };
                menuItem.Click += AddFormMenuItem_Click;
                menuAddFormGroup.Items.Add(menuItem);
            }
        }

        // Build the synthetic FormTemplate that represents a saved Progress Book layout inside a
        // WP template's forms list.
        private static FormTemplate MakeLayoutFormItem(ProgressBookLayout layout)
        {
            return new FormTemplate
            {
                TemplateID = $"proglayout:{layout.Id}",   // marker only - never a real DB id
                TemplateName = $"Progress Book: {layout.Name}",
                TemplateType = TemplateTypes.ProgBook,
                LinkedProgressBookLayoutId = layout.Id
            };
        }

        // Placeholder item for a layout reference whose layout no longer exists, so it stays
        // visible (and is preserved on re-save) instead of silently vanishing.
        private static FormTemplate MakeMissingLayoutFormItem(int layoutId)
        {
            return new FormTemplate
            {
                TemplateID = $"proglayout:{layoutId}",
                TemplateName = $"Progress Book: (missing layout #{layoutId})",
                TemplateType = TemplateTypes.ProgBook,
                LinkedProgressBookLayoutId = layoutId
            };
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
            items.AddRange(GetSortedFormTemplates().Cast<object>());
            cboFormTemplateEdit.ItemsSource = items;
            cboFormTemplateEdit.DisplayMemberPath = "TemplateName";
        }

        // Project selection changed - load work packages and save selection
        private async void CboProject_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboProject.SelectedValue is string projectId)
            {
                SettingsManager.SetUserSetting( "WorkPackage.LastProjectID", projectId, "string");
                await LoadWorkPackagesAsync(projectId);
            }
        }

        // WP Template selection changed - save selection
        private void CboWPTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboWPTemplate.SelectedValue is string wpTemplateId)
            {
                SettingsManager.SetUserSetting( "WorkPackage.LastWPTemplateID", wpTemplateId, "string");
            }
        }

        // PKG Manager selection changed - save selection
        private void CboPKGManager_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboPKGManager.SelectedItem is Models.UserItem user)
            {
                SettingsManager.SetUserSetting( "WorkPackage.LastPKGManager", user.Username, "string");
            }
        }

        // Scheduler selection changed - save selection
        private void CboScheduler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboScheduler.SelectedItem is Models.UserItem user)
            {
                SettingsManager.SetUserSetting( "WorkPackage.LastScheduler", user.Username, "string");
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

            // Priority fields at top (alphabetical)
            var priorityFields = new[] { "Area", "CompType", "PhaseCategory", "PhaseCode",
                "SchedActNO", "PjtSystemNo", "UDF2", "WorkPackage" };

            // All other fields (alphabetical)
            var otherFields = new[] { "Aux1", "Aux2", "Aux3", "ChgOrdNO", "Description", "DwgNO",
                "EqmtNO", "Estimator", "HtTrace", "InsulType", "LineNumber", "MtrlSpec", "Notes",
                "PaintCode", "PipeGrade", "PjtSystem", "PjtSystemNo", "ProjectID", "RFINO", "RevNO", "ROCStep",
                "SecondActno", "SecondDwgNO", "Service", "ShopField", "ShtNO", "SubArea", "TagNO",
                "UDF1", "UDF3", "UDF4", "UDF5", "UDF6", "UDF8", "UDF9", "UDF10", "UDF11", "UDF12",
                "UDF13", "UDF14", "UDF15", "UDF16", "UDF17", "RespParty", "UDF20", "UOM" };

            // Add priority fields
            foreach (var field in priorityFields)
            {
                AddFieldMenuItem(contextMenu, field);
            }

            // Add separator
            contextMenu.Items.Add(new Separator());

            // Add remaining fields
            foreach (var field in otherFields)
            {
                AddFieldMenuItem(contextMenu, field);
            }

            contextMenu.IsOpen = true;
            contextMenu.PlacementTarget = btnInsertField;
        }

        // Helper to add a field menu item
        private void AddFieldMenuItem(ContextMenu menu, string field)
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
            menu.Items.Add(menuItem);
        }

        // Save WP Name Pattern when focus leaves the textbox
        private void TxtWPNamePattern_LostFocus(object sender, RoutedEventArgs e)
        {
            SettingsManager.SetUserSetting("WorkPackage.WPNamePattern", txtWPNamePattern.Text, "string");
        }

        // "No Image" toggle for the logo — persist preference and gate path/browse controls
        private void ChkLogoNoImage_Changed(object sender, RoutedEventArgs e)
        {
            SettingsManager.SetUserSetting("WorkPackage.LogoNoImage",
                (chkLogoNoImage.IsChecked == true).ToString(), "string");
            ApplyLogoNoImageEnabledState();
        }

        private void ApplyLogoNoImageEnabledState()
        {
            bool noImage = chkLogoNoImage.IsChecked == true;
            txtLogoPath.IsEnabled = !noImage;
            btnBrowseLogo.IsEnabled = !noImage;
        }

        // No Subfolders / Individual PDFs are mutually exclusive. Each handler
        // persists its own setting; when one auto-unchecks the other, the other's
        // Unchecked event fires and persists that setting too — no manual cascade
        // or suppress flag needed. The mutex if-block fails on the cascaded call
        // because that side is now false.
        private void ChkNoSubfolders_Changed(object sender, RoutedEventArgs e)
        {
            SettingsManager.SetUserSetting("WorkPackage.NoSubfolders",
                (chkNoSubfolders.IsChecked == true).ToString(), "string");

            if (chkNoSubfolders.IsChecked == true && chkIndividualPdfs.IsChecked == true)
            {
                chkIndividualPdfs.IsChecked = false;
            }
        }

        private void ChkIndividualPdfs_Changed(object sender, RoutedEventArgs e)
        {
            SettingsManager.SetUserSetting("WorkPackage.IndividualPdfs",
                (chkIndividualPdfs.IsChecked == true).ToString(), "string");

            if (chkIndividualPdfs.IsChecked == true && chkNoSubfolders.IsChecked == true)
            {
                chkNoSubfolders.IsChecked = false;
            }
        }

        // Open help sidebar at the WP Name Pattern section
        private void BtnWPNamePatternHelp_Click(object sender, RoutedEventArgs e)
        {
            popupWPNamePatternHelp.IsOpen = false;
            HelpService.OpenAt(HelpAnchors.WPNamePattern);
        }

        // Hover popup for WP Name Pattern help icon. The popup must stay open while
        // the mouse is over either the icon or the popup itself, so we use a short
        // close timer that gets cancelled if the mouse re-enters either region.
        private DispatcherTimer? _wpNamePatternHelpCloseTimer;

        private void BtnWPNamePatternHelp_MouseEnter(object sender, MouseEventArgs e)
        {
            _wpNamePatternHelpCloseTimer?.Stop();
            popupWPNamePatternHelp.IsOpen = true;
        }

        private void BtnWPNamePatternHelp_MouseLeave(object sender, MouseEventArgs e)
        {
            StartWPNamePatternHelpCloseTimer();
        }

        private void PopupWPNamePatternHelp_MouseEnter(object sender, MouseEventArgs e)
        {
            _wpNamePatternHelpCloseTimer?.Stop();
        }

        private void PopupWPNamePatternHelp_MouseLeave(object sender, MouseEventArgs e)
        {
            StartWPNamePatternHelpCloseTimer();
        }

        private void StartWPNamePatternHelpCloseTimer()
        {
            if (_wpNamePatternHelpCloseTimer == null)
            {
                _wpNamePatternHelpCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                _wpNamePatternHelpCloseTimer.Tick += (s, args) =>
                {
                    _wpNamePatternHelpCloseTimer.Stop();
                    popupWPNamePatternHelp.IsOpen = false;
                };
            }
            _wpNamePatternHelpCloseTimer.Stop();
            _wpNamePatternHelpCloseTimer.Start();
        }

        private void HyperlinkWPNamePatternHelp_Click(object sender, RoutedEventArgs e)
        {
            popupWPNamePatternHelp.IsOpen = false;
            HelpService.OpenAt(HelpAnchors.WPNamePattern);
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
                SettingsManager.SetUserSetting( "WorkPackage.LastLogoPath", dialog.FileName, "string");
            }
        }

        // Browse for output folder using modern IFileDialog COM interface
        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var selectedPath = ShowFolderPickerDialog("Select Output Folder");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                txtOutputFolder.Text = selectedPath;
                SettingsManager.SetUserSetting( "WorkPackage.LastOutputPath", selectedPath, "string");
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

        // Find external-file forms in a WP template whose linked PDF is missing on disk.
        // The path is static (no per-WP tokens), so one existence check covers a bulk run.
        private async Task<List<(string name, string path)>> CollectMissingExternalFilesAsync(string wpTemplateId)
        {
            var missing = new List<(string name, string path)>();
            try
            {
                var wpTemplate = await TemplateRepository.GetWPTemplateByIdAsync(wpTemplateId);
                if (wpTemplate == null) return missing;

                var formRefs = JsonSerializer.Deserialize<List<FormReference>>(wpTemplate.FormsJson);
                if (formRefs == null) return missing;

                foreach (var formRef in formRefs)
                {
                    var form = await TemplateRepository.GetFormTemplateByIdAsync(formRef.FormTemplateId);
                    if (form == null || form.TemplateType != TemplateTypes.ExternalFile)
                        continue;

                    var structure = JsonSerializer.Deserialize<ExternalFileStructure>(form.StructureJson);
                    var path = structure?.FilePath;
                    if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                        missing.Add((form.TemplateName, string.IsNullOrWhiteSpace(path) ? "(no file linked)" : path));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.CollectMissingExternalFilesAsync");
            }
            return missing;
        }

        // Find Drawings forms in a WP template whose per-WP subfolder ({parent}\{WP}) is missing
        // for any of the selected work packages. Returns one entry per missing (form, WP) pair.
        private async Task<List<(string name, string detail)>> CollectMissingDrawingsFoldersAsync(string wpTemplateId, List<string> selectedWPs)
        {
            var missing = new List<(string name, string detail)>();
            try
            {
                var wpTemplate = await TemplateRepository.GetWPTemplateByIdAsync(wpTemplateId);
                if (wpTemplate == null) return missing;

                var formRefs = JsonSerializer.Deserialize<List<FormReference>>(wpTemplate.FormsJson);
                if (formRefs == null) return missing;

                foreach (var formRef in formRefs)
                {
                    var form = await TemplateRepository.GetFormTemplateByIdAsync(formRef.FormTemplateId);
                    if (form == null || form.TemplateType != TemplateTypes.Drawings)
                        continue;

                    var parent = JsonSerializer.Deserialize<DrawingsStructure>(form.StructureJson)?.ParentFolderPath;
                    foreach (var wp in selectedWPs)
                    {
                        if (string.IsNullOrWhiteSpace(parent))
                            missing.Add((form.TemplateName, $"{wp} (no folder linked)"));
                        else if (!System.IO.Directory.Exists(System.IO.Path.Combine(parent, wp)))
                            missing.Add((form.TemplateName, System.IO.Path.Combine(parent, wp)));
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.CollectMissingDrawingsFoldersAsync");
            }
            return missing;
        }

        // Generate button click
        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (cboProject.SelectedValue is not string projectId)
            {
                AppMessageBox.Show("Please select a project.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedWPs = lstWorkPackages.SelectedItems.Cast<string>().ToList();
            if (!selectedWPs.Any())
            {
                AppMessageBox.Show("Please select at least one work package.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cboWPTemplate.SelectedValue is not string wpTemplateId)
            {
                AppMessageBox.Show("Please select a WP Template.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtOutputFolder.Text))
            {
                AppMessageBox.Show("Please select an output folder.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Warn about any external-file forms whose PDF can't be found, before generating.
            // The user chooses to proceed (those forms are omitted) or cancel and fix the links.
            var missingExternalFiles = await CollectMissingExternalFilesAsync(wpTemplateId);
            if (missingExternalFiles.Count > 0)
            {
                var list = string.Join("\n", missingExternalFiles.Select(m => $"  • {m.name}: {m.path}"));
                var choice = AppMessageBox.Show(
                    $"These external file(s) could not be found:\n\n{list}\n\n" +
                    "Click OK to generate without them, or Cancel to stop and re-link the file(s).",
                    "External File Not Found", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (choice != MessageBoxResult.OK)
                    return;
            }

            // Warn about any Drawings forms whose per-WP subfolder is missing (per selected WP).
            var missingDrawings = await CollectMissingDrawingsFoldersAsync(wpTemplateId, selectedWPs);
            if (missingDrawings.Count > 0)
            {
                const int cap = 20;
                var lines = missingDrawings.Take(cap).Select(m => $"  • {m.name}: {m.detail}").ToList();
                var list = string.Join("\n", lines);
                if (missingDrawings.Count > cap)
                    list += $"\n  ...and {missingDrawings.Count - cap} more";
                var choice = AppMessageBox.Show(
                    $"These drawings folder(s) could not be found:\n\n{list}\n\n" +
                    "Click OK to generate without them, or Cancel to stop.",
                    "Drawings Folder Not Found", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (choice != MessageBoxResult.OK)
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
                    GetResolvedLogoPath(),
                    chkNoSubfolders.IsChecked == true
                );

                int successCount = results.Count(r => r.Success);
                int failCount = results.Count - successCount;

                if (failCount == 0 && successCount > 0)
                {
                    lblStatus.Text = $"Generated {successCount} work package(s) successfully.";
                    var paths = string.Join("\n", results.Where(r => r.Success && !string.IsNullOrEmpty(r.MergedPdfPath)).Select(r => r.MergedPdfPath));
                    AppMessageBox.Show($"Successfully generated {successCount} work package(s).\n\nFiles saved to:\n{paths}",
                        "Generation Complete", MessageBoxButton.OK, MessageBoxImage.None);
                }
                else if (successCount == 0 && failCount == 0)
                {
                    lblStatus.Text = "No work packages generated";
                    var errors = string.Join("\n", results.Select(r => r.ErrorMessage ?? "Unknown error"));
                    AppMessageBox.Show($"No work packages were generated.\n\nErrors:\n{errors}",
                        "Generation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    lblStatus.Text = $"Generated {successCount} work package(s), {failCount} failed.";
                    var errors = string.Join("\n", results.Where(r => !r.Success).Select(r => r.ErrorMessage));
                    AppMessageBox.Show($"Generated {successCount} of {results.Count} work packages.\n\nErrors:\n{errors}",
                        "Generation Complete with Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.BtnGenerate_Click");
                AppMessageBox.Show($"Error generating work packages: {ex.Message}",
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
                SettingsManager.SetUserSetting( "WorkPackage.SplitterRatio", leftRatio.ToString("F4"), "string");
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
                ApplyWPFormsListFilter();
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
                if (formRef.ProgressBookLayoutId.HasValue)
                {
                    int layoutId = formRef.ProgressBookLayoutId.Value;
                    var layout = _progressBookLayouts.FirstOrDefault(l => l.Id == layoutId)
                                 ?? await ProgressBookLayoutRepository.GetByIdAsync(layoutId);
                    _wpFormsList.Add(layout != null ? MakeLayoutFormItem(layout) : MakeMissingLayoutFormItem(layoutId));
                    continue;
                }

                var formTemplate = await TemplateRepository.GetFormTemplateByIdAsync(formRef.FormTemplateId);
                if (formTemplate != null)
                {
                    _wpFormsList.Add(formTemplate);
                }
            }

            ApplyWPFormsListFilter();
        }

        // Bind the WP forms list for display. (All form types are shown.)
        private void ApplyWPFormsListFilter()
        {
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

        // Delete WP template (built-in templates are protected)
        private async void BtnDeleteWPTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWPTemplate == null) return;

            if (_selectedWPTemplate.IsBuiltIn)
            {
                AppMessageBox.Show("Built-in templates cannot be deleted.", "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = AppMessageBox.Show($"Are you sure you want to delete '{_selectedWPTemplate.WPTemplateName}'?",
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
                AppMessageBox.Show("Please enter a template name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string newName = txtWPTemplateName.Text.Trim();

            // Built-in templates: name must differ from all existing templates
            if (_selectedWPTemplate?.IsBuiltIn == true)
            {
                var duplicate = _wpTemplates.FirstOrDefault(t =>
                    t.WPTemplateName.Equals(newName, StringComparison.OrdinalIgnoreCase));

                if (duplicate != null)
                {
                    AppMessageBox.Show($"A template named '{newName}' already exists. Please choose a different name.",
                        "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtWPTemplateName.Focus();
                    txtWPTemplateName.SelectAll();
                    return;
                }
            }
            else
            {
                // User-created or new: name must not match any OTHER template
                var duplicate = _wpTemplates.FirstOrDefault(t =>
                    t.WPTemplateName.Equals(newName, StringComparison.OrdinalIgnoreCase) &&
                    t.WPTemplateID != _selectedWPTemplate?.WPTemplateID);

                if (duplicate != null)
                {
                    AppMessageBox.Show($"A template named '{newName}' already exists. Please choose a different name.",
                        "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtWPTemplateName.Focus();
                    txtWPTemplateName.SelectAll();
                    return;
                }
            }

            try
            {
                var formsJson = JsonSerializer.Serialize(_wpFormsList.Select(f =>
                    f.LinkedProgressBookLayoutId.HasValue
                        ? new FormReference { ProgressBookLayoutId = f.LinkedProgressBookLayoutId }
                        : new FormReference { FormTemplateId = f.TemplateID }).ToList());
                var settingsJson = JsonSerializer.Serialize(new WPTemplateSettings { ExpirationDays = (int)(txtExpirationDays.Value ?? 14) });

                string savedTemplateId;

                // Determine if creating new or updating existing
                bool isNewTemplate = _selectedWPTemplate == null
                    || _selectedWPTemplate.IsBuiltIn
                    || !_selectedWPTemplate.WPTemplateName.Equals(newName, StringComparison.OrdinalIgnoreCase);

                if (isNewTemplate)
                {
                    // Create new template (covers: built-in save-as, renamed save-as, + Add New)
                    var template = new WPTemplate
                    {
                        WPTemplateID = Guid.NewGuid().ToString(),
                        WPTemplateName = newName,
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
                    // Update existing user-created template (same name)
                    _selectedWPTemplate!.FormsJson = formsJson;
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

                AppMessageBox.Show($"WP template '{newName}' saved.", "Saved",
                    MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.BtnSaveWPTemplate_Click");
                AppMessageBox.Show($"Error saving template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                // Show Reset Defaults button only for user-created templates that have a
                // resettable default structure (not Drawings/ExternalFile)
                btnResetFormTemplateDefaults.Visibility = (!template.IsBuiltIn && FormTypeSupportsResetDefaults(template.TemplateType))
                    ? Visibility.Visible
                    : Visibility.Collapsed;

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
                string templateName;
                string structureJson;

                if (dialog.SelectedType == TemplateTypes.ExternalFile)
                {
                    // External file: browse to the PDF now. Its file name (without extension)
                    // becomes the default template name; cancelling the browser cancels the add.
                    var fileDialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "PDF Files|*.pdf",
                        Title = "Select PDF File to Include"
                    };
                    if (fileDialog.ShowDialog() != true)
                    {
                        RestoreFormTemplateSelectionAfterCancel();
                        return;
                    }
                    templateName = System.IO.Path.GetFileNameWithoutExtension(fileDialog.FileName);
                    structureJson = JsonSerializer.Serialize(new ExternalFileStructure
                    {
                        Title = templateName,
                        FilePath = fileDialog.FileName
                    });
                }
                else if (dialog.SelectedType == TemplateTypes.Drawings)
                {
                    // Drawings: browse to the parent folder now (its subfolders are named per WP).
                    // The folder name becomes the default template name; cancelling cancels the add.
                    var folderPath = ShowFolderPickerDialog("Select Drawings Parent Folder");
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        RestoreFormTemplateSelectionAfterCancel();
                        return;
                    }
                    templateName = new System.IO.DirectoryInfo(folderPath).Name;
                    if (string.IsNullOrWhiteSpace(templateName)) templateName = "Drawings";
                    structureJson = JsonSerializer.Serialize(new DrawingsStructure
                    {
                        Title = templateName,
                        ParentFolderPath = folderPath
                    });
                }
                else
                {
                    templateName = $"New {dialog.SelectedType} Template";
                    structureJson = dialog.SelectedType switch
                    {
                        TemplateTypes.Cover => JsonSerializer.Serialize(new CoverStructure()),
                        TemplateTypes.List => JsonSerializer.Serialize(new ListStructure()),
                        TemplateTypes.Grid => JsonSerializer.Serialize(new GridStructure()),
                        TemplateTypes.Form => JsonSerializer.Serialize(new FormStructure()),
                        _ => "{}"
                    };
                }

                // Create new template with selected type
                var newTemplate = new FormTemplate
                {
                    TemplateID = System.Guid.NewGuid().ToString(),
                    TemplateName = templateName,
                    TemplateType = dialog.SelectedType,
                    IsBuiltIn = false,
                    CreatedBy = App.CurrentUser?.Username ?? "Unknown",
                    CreatedUtc = DateTime.UtcNow.ToString("o"),
                    StructureJson = structureJson
                };

                _selectedFormTemplate = newTemplate;
                txtFormTemplateName.Text = newTemplate.TemplateName;
                lblFormTemplateType.Text = newTemplate.TemplateType;

                // Show Reset Defaults for new user-created templates (not Drawings/ExternalFile,
                // which have no meaningful "default structure" to reset to)
                btnResetFormTemplateDefaults.Visibility = FormTypeSupportsResetDefaults(newTemplate.TemplateType)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                // Load the editor for this type
                LoadFormTemplateEditor(newTemplate);
                _hasUnsavedChanges = true;
            }
            else
            {
                RestoreFormTemplateSelectionAfterCancel();
            }
        }

        // Restore the form-template picker after the user cancels the Add New flow.
        private void RestoreFormTemplateSelectionAfterCancel()
        {
            if (_formTemplates.Count > 0)
            {
                cboFormTemplateEdit.SelectedIndex = 0;
            }
            else
            {
                _selectedFormTemplate = null;
                txtFormTemplateName.Text = "";
                lblFormTemplateType.Text = "";
                btnResetFormTemplateDefaults.Visibility = Visibility.Collapsed;
                ClearFormEditor();
            }
        }

        // Whether a form type has a resettable default structure (Drawings and ExternalFile don't).
        private static bool FormTypeSupportsResetDefaults(string templateType)
        {
            return templateType != TemplateTypes.Drawings && templateType != TemplateTypes.ExternalFile;
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
                        var drawingsStructure = JsonSerializer.Deserialize<DrawingsStructure>(template.StructureJson) ?? new DrawingsStructure();
                        BuildDrawingsEditor(drawingsStructure);
                        break;

                    case TemplateTypes.ExternalFile:
                        var externalStructure = JsonSerializer.Deserialize<ExternalFileStructure>(template.StructureJson) ?? new ExternalFileStructure();
                        BuildExternalFileEditor(externalStructure);
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

        // Clone form template - creates and saves immediately
        // Delete form template (built-in templates are protected)
        private async void BtnDeleteFormTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFormTemplate == null) return;

            if (_selectedFormTemplate.IsBuiltIn)
            {
                AppMessageBox.Show("Built-in templates cannot be deleted.", "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = AppMessageBox.Show($"Are you sure you want to delete '{_selectedFormTemplate.TemplateName}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            var (success, blockingTemplates) = await TemplateRepository.DeleteFormTemplateAsync(_selectedFormTemplate.TemplateID);

            if (!success && blockingTemplates.Any())
            {
                AppMessageBox.Show($"Cannot delete this form template. It is used by the following WP templates:\n\n{string.Join("\n", blockingTemplates)}",
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

        // Reset form template to built-in defaults
        private async void BtnResetFormTemplateDefaults_Click(object sender, RoutedEventArgs e)
        {
            // Guard: only works for user-created templates
            if (_selectedFormTemplate == null || _selectedFormTemplate.IsBuiltIn)
            {
                return;
            }

            try
            {
                // Get built-in templates of the same type
                var builtInTemplates = await TemplateRepository.GetBuiltInFormTemplatesByTypeAsync(_selectedFormTemplate.TemplateType);

                if (builtInTemplates.Count == 0)
                {
                    AppMessageBox.Show("No built-in template found for this type.", "Cannot Reset",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                FormTemplate? sourceTemplate = null;

                if (builtInTemplates.Count == 1)
                {
                    // Single built-in, use it directly
                    sourceTemplate = builtInTemplates[0];
                }
                else
                {
                    // Multiple built-ins, show selection dialog
                    var dialog = new ResetTemplateDialog(builtInTemplates)
                    {
                        Owner = Window.GetWindow(this)
                    };

                    if (dialog.ShowDialog() != true || dialog.SelectedTemplate == null)
                    {
                        return;
                    }

                    sourceTemplate = dialog.SelectedTemplate;
                }

                // Confirm the reset
                var result = AppMessageBox.Show(
                    $"Reset '{_selectedFormTemplate.TemplateName}' to match '{sourceTemplate.TemplateName}' defaults?\n\nThis will replace all current settings.",
                    "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                // Copy StructureJson from built-in to current template
                _selectedFormTemplate.StructureJson = sourceTemplate.StructureJson;

                // Save to database
                await TemplateRepository.UpdateFormTemplateAsync(_selectedFormTemplate);

                // Reload the editor to reflect new values
                LoadFormTemplateEditor(_selectedFormTemplate);

                _hasUnsavedChanges = false;
                lblStatus.Text = "Template reset to defaults";
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.BtnResetFormTemplateDefaults_Click");
                AppMessageBox.Show($"Error resetting template: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Save form template
        private async void BtnSaveFormTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFormTemplateName.Text))
            {
                AppMessageBox.Show("Please enter a template name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string newName = txtFormTemplateName.Text.Trim();

            // Built-in templates: name must differ from all existing templates
            if (_selectedFormTemplate?.IsBuiltIn == true)
            {
                var duplicate = _formTemplates.FirstOrDefault(t =>
                    t.TemplateName.Equals(newName, StringComparison.OrdinalIgnoreCase));

                if (duplicate != null)
                {
                    AppMessageBox.Show($"A template named '{newName}' already exists. Please choose a different name.",
                        "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtFormTemplateName.Focus();
                    txtFormTemplateName.SelectAll();
                    return;
                }
            }
            else
            {
                // User-created or new: name must not match any OTHER template
                var duplicate = _formTemplates.FirstOrDefault(t =>
                    t.TemplateName.Equals(newName, StringComparison.OrdinalIgnoreCase) &&
                    t.TemplateID != _selectedFormTemplate?.TemplateID);

                if (duplicate != null)
                {
                    AppMessageBox.Show($"A template named '{newName}' already exists. Please choose a different name.",
                        "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtFormTemplateName.Focus();
                    txtFormTemplateName.SelectAll();
                    return;
                }
            }

            // Get structure JSON from current editor if supported
            string? editorStructureJson = GetStructureJsonFromEditor();

            try
            {
                string savedTemplateId;

                // Determine if creating new or updating existing. Insert (creates a new copy)
                // when: no template, a built-in (save-as copy), the selected template isn't in the
                // DB-loaded list yet (a brand-new "+ Add New" template), OR the Name field was
                // changed. Changing the top Name field + Save deliberately creates a second copy;
                // to rename a form in place, use the Rename control in the editor section.
                bool isNewTemplate = _selectedFormTemplate == null
                    || _selectedFormTemplate.IsBuiltIn
                    || !_formTemplates.Any(t => t.TemplateID == _selectedFormTemplate.TemplateID)
                    || !_selectedFormTemplate.TemplateName.Equals(newName, StringComparison.OrdinalIgnoreCase);

                if (isNewTemplate)
                {
                    // Create new template (covers: built-in save-as, renamed save-as, + Add New)
                    // Get type and structure from current template or clone data
                    string templateType = _selectedFormTemplate?.TemplateType ?? _clonedFormType ?? "";
                    string structureJson = editorStructureJson ?? _selectedFormTemplate?.StructureJson ?? _clonedFormStructure ?? "";

                    if (string.IsNullOrEmpty(templateType) || string.IsNullOrEmpty(structureJson))
                    {
                        AppMessageBox.Show("Please select a template first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var template = new FormTemplate
                    {
                        TemplateID = Guid.NewGuid().ToString(),
                        TemplateName = newName,
                        TemplateType = templateType,
                        StructureJson = structureJson,
                        IsBuiltIn = false,
                        CreatedBy = App.CurrentUser?.Username ?? "Unknown",
                        CreatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    };
                    await TemplateRepository.InsertFormTemplateAsync(template);
                    savedTemplateId = template.TemplateID;

                    // Clear clone data
                    _clonedFormType = null;
                    _clonedFormStructure = null;
                }
                else
                {
                    // Update existing user-created template (same name)
                    if (editorStructureJson != null)
                    {
                        _selectedFormTemplate!.StructureJson = editorStructureJson;
                    }
                    await TemplateRepository.UpdateFormTemplateAsync(_selectedFormTemplate!);
                    savedTemplateId = _selectedFormTemplate!.TemplateID;
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

                lblStatus.Text = "Template saved";
                AppMessageBox.Show($"Form template '{newName}' saved.", "Saved",
                    MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.BtnSaveFormTemplate_Click");
                AppMessageBox.Show($"Error saving template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Export all user-created templates to JSON
        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allForms = await TemplateRepository.GetAllFormTemplatesAsync();
                var allWPs = await TemplateRepository.GetAllWPTemplatesAsync();

                // Only export user-created templates (built-ins exist on every install)
                var userForms = allForms.Where(f => !f.IsBuiltIn).ToList();
                var userWPs = allWPs.Where(w => !w.IsBuiltIn).ToList();

                if (userForms.Count == 0 && userWPs.Count == 0)
                {
                    AppMessageBox.Show("No user-created templates to export. Built-in templates are already available on every installation.",
                        "Nothing to Export", MessageBoxButton.OK, MessageBoxImage.None);
                    return;
                }

                // Build formTemplateId → index map
                var formIdToIndex = new Dictionary<string, int>();
                for (int i = 0; i < userForms.Count; i++)
                    formIdToIndex[userForms[i].TemplateID] = i;

                // Build export structure
                var formEntries = userForms.Select((f, i) => new Dictionary<string, object>
                {
                    ["index"] = i,
                    ["templateName"] = f.TemplateName,
                    ["templateType"] = f.TemplateType,
                    ["structureJson"] = f.StructureJson
                }).ToList();

                var wpEntries = new List<Dictionary<string, object>>();
                foreach (var wp in userWPs)
                {
                    // Convert FormsJson GUIDs to indices
                    var formRefs = JsonSerializer.Deserialize<List<FormReference>>(wp.FormsJson) ?? new();
                    var indices = formRefs
                        .Where(r => formIdToIndex.ContainsKey(r.FormTemplateId))
                        .Select(r => formIdToIndex[r.FormTemplateId])
                        .ToList();

                    wpEntries.Add(new Dictionary<string, object>
                    {
                        ["wpTemplateName"] = wp.WPTemplateName,
                        ["formIndices"] = indices,
                        ["defaultSettings"] = wp.DefaultSettings
                    });
                }

                var exportData = new Dictionary<string, object>
                {
                    ["version"] = 1,
                    ["exportedUtc"] = DateTime.UtcNow.ToString("o"),
                    ["formTemplates"] = formEntries,
                    ["wpTemplates"] = wpEntries
                };

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(exportData, jsonOptions);

                var dialog = new SaveFileDialog
                {
                    Filter = "JSON Files|*.json",
                    Title = "Export Templates",
                    FileName = "VANTAGE_Templates.json"
                };

                if (dialog.ShowDialog() != true) return;

                await File.WriteAllTextAsync(dialog.FileName, json);
                AppMessageBox.Show($"Exported {userForms.Count} form template(s) and {userWPs.Count} WP template(s).",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.BtnExport_Click");
                AppMessageBox.Show($"Error exporting templates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Import templates from JSON file
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
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Validate version
                if (!root.TryGetProperty("version", out var versionEl) || versionEl.GetInt32() != 1)
                {
                    AppMessageBox.Show("Unrecognized template file format.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Get existing template names for conflict detection
                var existingForms = await TemplateRepository.GetAllFormTemplatesAsync();
                var existingWPs = await TemplateRepository.GetAllWPTemplatesAsync();
                var existingFormNames = new HashSet<string>(existingForms.Select(f => f.TemplateName), StringComparer.OrdinalIgnoreCase);
                var existingWPNames = new HashSet<string>(existingWPs.Select(w => w.WPTemplateName), StringComparer.OrdinalIgnoreCase);

                // Import form templates and build index → new ID map
                var indexToNewId = new Dictionary<int, string>();
                int formCount = 0;

                if (root.TryGetProperty("formTemplates", out var formsEl))
                {
                    foreach (var formEl in formsEl.EnumerateArray())
                    {
                        int index = formEl.GetProperty("index").GetInt32();
                        string name = formEl.GetProperty("templateName").GetString() ?? "Unnamed";
                        string type = formEl.GetProperty("templateType").GetString() ?? "Form";
                        string structureJson = formEl.GetProperty("structureJson").GetString() ?? "{}";

                        // Handle name conflicts
                        if (existingFormNames.Contains(name))
                            name += " (Imported)";

                        var newId = Guid.NewGuid().ToString();
                        var template = new FormTemplate
                        {
                            TemplateID = newId,
                            TemplateName = name,
                            TemplateType = type,
                            StructureJson = structureJson,
                            IsBuiltIn = false,
                            CreatedBy = App.CurrentUser?.Username ?? "Unknown",
                            CreatedUtc = DateTime.UtcNow.ToString("o")
                        };

                        if (await TemplateRepository.InsertFormTemplateAsync(template))
                        {
                            indexToNewId[index] = newId;
                            formCount++;
                        }
                    }
                }

                // Import WP templates with remapped form references
                int wpCount = 0;

                if (root.TryGetProperty("wpTemplates", out var wpsEl))
                {
                    foreach (var wpEl in wpsEl.EnumerateArray())
                    {
                        string name = wpEl.GetProperty("wpTemplateName").GetString() ?? "Unnamed";
                        string defaultSettings = wpEl.GetProperty("defaultSettings").GetString() ?? "{\"expirationDays\":14}";

                        // Remap form indices to new GUIDs
                        var formRefs = new List<FormReference>();
                        if (wpEl.TryGetProperty("formIndices", out var indicesEl))
                        {
                            foreach (var idxEl in indicesEl.EnumerateArray())
                            {
                                int idx = idxEl.GetInt32();
                                if (indexToNewId.TryGetValue(idx, out var newFormId))
                                    formRefs.Add(new FormReference { FormTemplateId = newFormId });
                            }
                        }

                        // Handle name conflicts
                        if (existingWPNames.Contains(name))
                            name += " (Imported)";

                        var template = new WPTemplate
                        {
                            WPTemplateID = Guid.NewGuid().ToString(),
                            WPTemplateName = name,
                            FormsJson = JsonSerializer.Serialize(formRefs),
                            DefaultSettings = defaultSettings,
                            IsBuiltIn = false,
                            CreatedBy = App.CurrentUser?.Username ?? "Unknown",
                            CreatedUtc = DateTime.UtcNow.ToString("o")
                        };

                        if (await TemplateRepository.InsertWPTemplateAsync(template))
                            wpCount++;
                    }
                }

                // Refresh template lists in the UI
                _formTemplates = await TemplateRepository.GetAllFormTemplatesAsync();
                _wpTemplates = await TemplateRepository.GetAllWPTemplatesAsync();
                PopulateWPTemplateEditDropdown();
                _suppressTypeDialog = true;
                PopulateFormTemplateEditDropdown();
                _suppressTypeDialog = false;

                AppMessageBox.Show($"Imported {formCount} form template(s) and {wpCount} WP template(s).",
                    "Import Complete", MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (JsonException)
            {
                AppMessageBox.Show("The selected file is not a valid template JSON file.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.BtnImport_Click");
                AppMessageBox.Show($"Error importing templates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Check for unsaved changes before leaving
        public bool CanLeaveView()
        {
            if (!_hasUnsavedChanges) return true;

            var result = AppMessageBox.Show("You have unsaved changes. Save as a new template?",
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

            _coverImageBrowseBtn = new Button
            {
                Content = "Browse",
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(5, 0, 0, 0)
            };
            _coverImageBrowseBtn.Click += BrowseCoverImage_Click;
            Grid.SetColumn(_coverImageBrowseBtn, 1);
            imagePathGrid.Children.Add(_coverImageBrowseBtn);

            _coverNoImageBox = new CheckBox
            {
                Content = "No Image",
                IsChecked = structure.NoImage,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                ToolTip = "Render the cover sheet with no image"
            };
            _coverNoImageBox.Checked += CoverNoImage_Changed;
            _coverNoImageBox.Unchecked += CoverNoImage_Changed;
            Grid.SetColumn(_coverNoImageBox, 2);
            imagePathGrid.Children.Add(_coverNoImageBox);

            panel.Children.Add(imagePathGrid);
            ApplyCoverNoImageEnabledState();

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

        // "No Image" toggle on the cover template editor — saves with template,
        // disables path/browse so the user understands the path is being ignored.
        private void CoverNoImage_Changed(object sender, RoutedEventArgs e)
        {
            _hasUnsavedChanges = true;
            ApplyCoverNoImageEnabledState();
        }

        private void ApplyCoverNoImageEnabledState()
        {
            bool noImage = _coverNoImageBox?.IsChecked == true;
            if (_coverImagePathBox != null) _coverImagePathBox.IsEnabled = !noImage;
            if (_coverImageBrowseBtn != null) _coverImageBrowseBtn.IsEnabled = !noImage;
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

            // Items header with Add Item dropdown
            var itemsHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            itemsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var itemsLabel = new TextBlock
            {
                Text = "Items (use blank lines for spacing)",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(itemsLabel, 0);
            itemsHeaderGrid.Children.Add(itemsLabel);

            // Add Item dropdown (Syncfusion DropDownButtonAdv)
            var addItemDropdown = new DropDownButtonAdv
            {
                Label = "+ Add Item",
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = (Brush)FindResource("ForegroundColor"),
                BorderThickness = new Thickness(0),
                IconTemplate = null,
                SmallIcon = null,
                LargeIcon = null,
                FontSize = 12,
                SizeMode = Syncfusion.Windows.Tools.Controls.SizeMode.Normal,
                ToolTip = "Add a predefined item to the list"
            };

            var menuGroup = new DropDownMenuGroup
            {
                Background = (Brush)FindResource("ControlBackground"),
                Foreground = (Brush)FindResource("ForegroundColor"),
                BorderBrush = (Brush)FindResource("BorderColor")
            };

            // Populate dropdown with predefined items
            foreach (var (label, value) in _tocPredefinedItems)
            {
                var menuItem = new DropDownMenuItem
                {
                    Header = label,
                    Tag = value
                };
                menuItem.Click += ListItemDropdown_Click;
                menuGroup.Items.Add(menuItem);
            }

            // Add separator then spacing items (Blank Line, Line Separator)
            menuGroup.Items.Add(new Separator());
            foreach (var (label, value) in _tocSpacingItems)
            {
                var menuItem = new DropDownMenuItem
                {
                    Header = label,
                    Tag = value
                };
                menuItem.Click += ListItemDropdown_Click;
                menuGroup.Items.Add(menuItem);
            }

            // Add separator and "Add New" option for custom text
            menuGroup.Items.Add(new Separator());
            var addNewItem = new DropDownMenuItem { Header = "Add New..." };
            addNewItem.Click += ListItemShowAddPanel_Click;
            menuGroup.Items.Add(addNewItem);

            addItemDropdown.Content = menuGroup;
            Grid.SetColumn(addItemDropdown, 1);
            itemsHeaderGrid.Children.Add(addItemDropdown);

            panel.Children.Add(itemsHeaderGrid);

            // Items list with buttons
            var itemsGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            itemsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _listItemsBox = new ListBox
            {
                Height = 180,
                ItemsSource = _listItems,
                ToolTip = "Select an item to move or remove",
                ItemTemplate = CreateBlankLineItemTemplate()
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

            // Add new item panel (hidden by default, shown when "Add New..." clicked)
            _listAddPanel = new Grid { Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
            _listAddPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _listAddPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _listAddPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var addLabel = new TextBlock
            {
                Text = "New item:",
                Foreground = (Brush)FindResource("ForegroundColor"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            _listAddPanel.Children.Add(addLabel);

            _listNewItemBox = new TextBox { Height = 28, ToolTip = "Enter new item text", Margin = new Thickness(0, 0, 5, 0) };
            _listNewItemBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) ListItemAdd_Click(s, e);
                if (e.Key == System.Windows.Input.Key.Escape) ListItemHideAddPanel();
            };
            Grid.SetColumn(_listNewItemBox, 1);
            _listAddPanel.Children.Add(_listNewItemBox);

            var addBtnPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var btnAdd = new Button { Content = "Add", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 5, 0), ToolTip = "Add item to list" };
            btnAdd.Click += ListItemAdd_Click;
            addBtnPanel.Children.Add(btnAdd);
            var btnCancelAdd = new Button { Content = "Cancel", Padding = new Thickness(8, 4, 8, 4), ToolTip = "Cancel adding" };
            btnCancelAdd.Click += (s, e) => ListItemHideAddPanel();
            addBtnPanel.Children.Add(btnCancelAdd);
            Grid.SetColumn(addBtnPanel, 2);
            _listAddPanel.Children.Add(addBtnPanel);
            panel.Children.Add(_listAddPanel);

            // Edit item panel (hidden by default, shown when Edit button clicked)
            _listEditPanel = new Grid { Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
            _listEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _listEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _listEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var editLabel = new TextBlock
            {
                Text = "Edit item:",
                Foreground = (Brush)FindResource("ForegroundColor"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            _listEditPanel.Children.Add(editLabel);

            _listEditItemBox = new TextBox { Height = 28, ToolTip = "Edit the selected item", Margin = new Thickness(0, 0, 5, 0) };
            _listEditItemBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) ListItemSaveEdit_Click(s, e);
                if (e.Key == System.Windows.Input.Key.Escape) ListItemHideEditPanel();
            };
            Grid.SetColumn(_listEditItemBox, 1);
            _listEditPanel.Children.Add(_listEditItemBox);

            var editBtnPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var btnSaveEdit = new Button { Content = "Save", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 5, 0), ToolTip = "Save changes" };
            btnSaveEdit.Click += ListItemSaveEdit_Click;
            editBtnPanel.Children.Add(btnSaveEdit);
            var btnCancelEdit = new Button { Content = "Cancel", Padding = new Thickness(8, 4, 8, 4), ToolTip = "Cancel editing" };
            btnCancelEdit.Click += (s, e) => ListItemHideEditPanel();
            editBtnPanel.Children.Add(btnCancelEdit);
            Grid.SetColumn(editBtnPanel, 2);
            _listEditPanel.Children.Add(editBtnPanel);
            panel.Children.Add(_listEditPanel);

            // Font Size Adjust slider
            var fontLabel = new TextBlock
            {
                Text = $"Font Size Adjust: {structure.FontSizeAdjustPercent:+0;-0;0}%",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(fontLabel);
            _listFontSizeSlider = new Slider
            {
                Minimum = -30,
                Maximum = 50,
                Value = structure.FontSizeAdjustPercent,
                TickFrequency = 10,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 0, 0, 15),
                ToolTip = "Adjust font size (-30% to +50%)"
            };
            _listFontSizeSlider.ValueChanged += (s, e) =>
            {
                fontLabel.Text = $"Font Size Adjust: {(int)_listFontSizeSlider.Value:+0;-0;0}%";
                _hasUnsavedChanges = true;
            };
            panel.Children.Add(_listFontSizeSlider);

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
            if (_listItemsBox?.SelectedIndex >= 0 && _listItems != null && _listEditItemBox != null && _listEditPanel != null)
            {
                _listEditIndex = _listItemsBox.SelectedIndex;
                _listEditItemBox.Text = _listItems[_listEditIndex];
                ListItemHideAddPanel();
                _listEditPanel.Visibility = Visibility.Visible;
                _listEditItemBox.Focus();
                _listEditItemBox.SelectAll();
            }
        }

        // Show Add panel when "Add New..." clicked from dropdown
        private void ListItemShowAddPanel_Click(object sender, RoutedEventArgs e)
        {
            if (_listAddPanel != null && _listNewItemBox != null)
            {
                ListItemHideEditPanel();
                _listNewItemBox.Clear();
                _listAddPanel.Visibility = Visibility.Visible;
                _listNewItemBox.Focus();
            }
        }

        // Hide Add panel
        private void ListItemHideAddPanel()
        {
            if (_listAddPanel != null)
            {
                _listAddPanel.Visibility = Visibility.Collapsed;
                _listNewItemBox?.Clear();
            }
        }

        // Hide Edit panel
        private void ListItemHideEditPanel()
        {
            if (_listEditPanel != null)
            {
                _listEditPanel.Visibility = Visibility.Collapsed;
                _listEditIndex = -1;
            }
        }

        // Save edited item in place
        private void ListItemSaveEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_listEditItemBox != null && _listItems != null && _listEditIndex >= 0 && _listEditIndex < _listItems.Count)
            {
                _listItems[_listEditIndex] = _listEditItemBox.Text;
                _hasUnsavedChanges = true;
                ListItemHideEditPanel();
            }
        }

        private void ListItemAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_listNewItemBox != null && _listItems != null)
            {
                _listItems.Add(_listNewItemBox.Text);
                _hasUnsavedChanges = true;
                ListItemHideAddPanel();
            }
        }

        // Handle Add Item dropdown menu item click
        private void ListItemDropdown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is DropDownMenuItem menuItem && menuItem.Tag is string value && _listItems != null)
            {
                _listItems.Add(value);
                _hasUnsavedChanges = true;
            }
        }

        // Create DataTemplate for list items that shows "blank line" in italic dimmed text for empty strings
        private DataTemplate CreateBlankLineItemTemplate()
        {
            var template = new DataTemplate();

            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding(".")
            {
                Converter = new BlankLineDisplayConverter()
            });

            // Create style with triggers for blank lines
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, FindResource("ForegroundColor")));

            // Trigger for blank lines: italic and dimmed
            var blankTrigger = new DataTrigger
            {
                Binding = new Binding(".") { Converter = new IsBlankLineConverter() },
                Value = true
            };
            blankTrigger.Setters.Add(new Setter(TextBlock.FontStyleProperty, FontStyles.Italic));
            blankTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, FindResource("TextColorSecondary")));
            style.Triggers.Add(blankTrigger);

            textBlockFactory.SetValue(TextBlock.StyleProperty, style);
            template.VisualTree = textBlockFactory;

            return template;
        }

        // Get List structure JSON from editor
        private string GetListStructureJson()
        {
            var structure = new ListStructure
            {
                Title = _listTitleBox?.Text ?? "TABLE OF CONTENTS",
                Items = _listItems?.ToList() ?? new List<string>(),
                FontSizeAdjustPercent = (int)(_listFontSizeSlider?.Value ?? 0),
                FooterText = string.IsNullOrWhiteSpace(_listFooterTextBox?.Text) ? null : _listFooterTextBox.Text
            };
            return JsonSerializer.Serialize(structure);
        }

        // Get Cover structure JSON from editor
        private string GetCoverStructureJson()
        {
            var structure = new CoverStructure
            {
                Title = _coverTitleBox?.Text ?? "COVER SHEET",
                ImagePath = _coverImagePathBox?.Text == "(default)" ? null : _coverImagePathBox?.Text,
                ImageWidthPercent = (int)(_coverImageWidthSlider?.Value ?? 80),
                NoImage = _coverNoImageBox?.IsChecked == true,
                FooterText = string.IsNullOrWhiteSpace(_coverFooterTextBox?.Text) ? null : _coverFooterTextBox.Text
            };
            return JsonSerializer.Serialize(structure);
        }

        // Build and display the Grid type editor
        private void BuildGridEditor(GridStructure structure)
        {
            _currentEditorType = TemplateTypes.Grid;
            _gridColumns = new ObservableCollection<TemplateColumn>(structure.Columns);
            _gridBaseHeaderFontSize = structure.BaseHeaderFontSize;  // Preserve for save

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
            var btnUp = new Button { Content = "▲", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Move up" };
            btnUp.Click += GridColumnMoveUp_Click;
            btnPanel.Children.Add(btnUp);

            var btnDown = new Button { Content = "▼", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Move down" };
            btnDown.Click += GridColumnMoveDown_Click;
            btnPanel.Children.Add(btnDown);

            var btnEdit = new Button { Content = "✎", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Edit" };
            btnEdit.Click += GridColumnEdit_Click;
            btnPanel.Children.Add(btnEdit);

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

            // Column edit panel (hidden by default)
            _gridColumnEditPanel = new Grid { Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
            _gridColumnEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _gridColumnEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _gridColumnEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            _gridColumnEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var colEditLabel = new TextBlock
            {
                Text = "Edit:",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(colEditLabel, 0);
            _gridColumnEditPanel.Children.Add(colEditLabel);

            _gridColumnEditNameBox = new TextBox { Height = 28, ToolTip = "Edit column name", Margin = new Thickness(0, 0, 5, 0) };
            _gridColumnEditNameBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) GridColumnSaveEdit_Click(s, e);
                if (e.Key == System.Windows.Input.Key.Escape) GridColumnHideEditPanel();
            };
            Grid.SetColumn(_gridColumnEditNameBox, 1);
            _gridColumnEditPanel.Children.Add(_gridColumnEditNameBox);

            _gridColumnEditWidthBox = new Syncfusion.Windows.Shared.IntegerTextBox
            {
                MinValue = 5,
                MaxValue = 100,
                Height = 28,
                Width = 55,
                ToolTip = "Width %"
            };
            Grid.SetColumn(_gridColumnEditWidthBox, 2);
            _gridColumnEditPanel.Children.Add(_gridColumnEditWidthBox);

            var colEditBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 0, 0, 0) };
            var btnColSaveEdit = new Button { Content = "Save", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 3, 0), ToolTip = "Save changes" };
            btnColSaveEdit.Click += GridColumnSaveEdit_Click;
            colEditBtnPanel.Children.Add(btnColSaveEdit);
            var btnColCancelEdit = new Button { Content = "Cancel", Padding = new Thickness(8, 4, 8, 4), ToolTip = "Cancel editing" };
            btnColCancelEdit.Click += (s, e) => GridColumnHideEditPanel();
            colEditBtnPanel.Children.Add(btnColCancelEdit);
            Grid.SetColumn(colEditBtnPanel, 3);
            _gridColumnEditPanel.Children.Add(colEditBtnPanel);
            panel.Children.Add(_gridColumnEditPanel);

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

            // Font Size Adjust slider
            var fontLabel = new TextBlock
            {
                Text = $"Font Size Adjust: {structure.FontSizeAdjustPercent:+0;-0;0}%",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(fontLabel);
            _gridFontSizeSlider = new Slider
            {
                Minimum = -30,
                Maximum = 50,
                Value = structure.FontSizeAdjustPercent,
                TickFrequency = 10,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 0, 0, 15)
            };
            _gridFontSizeSlider.ValueChanged += (s, e) =>
            {
                fontLabel.Text = $"Font Size Adjust: {(int)_gridFontSizeSlider.Value:+0;-0;0}%";
                _hasUnsavedChanges = true;
            };
            panel.Children.Add(_gridFontSizeSlider);

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

                // Prorate remaining columns to total 100% (fixedIndex=-1 means scale all)
                ProrateGridColumnsExcept(-1, 0);

                // Refresh the ListBox to show updated percentages
                _gridColumnsBox.ItemsSource = null;
                _gridColumnsBox.ItemsSource = _gridColumns;

                _hasUnsavedChanges = true;
            }
        }

        private void GridColumnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_gridNewColumnNameBox != null && _gridNewColumnWidthBox != null && _gridColumns != null)
            {
                if (string.IsNullOrWhiteSpace(_gridNewColumnNameBox.Text))
                {
                    AppMessageBox.Show("Please enter a column name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int newWidth = (int)(_gridNewColumnWidthBox.Value ?? 20);
                if (newWidth > 95) newWidth = 95; // Cap to leave room for others

                // Add the new column first
                _gridColumns.Add(new TemplateColumn
                {
                    Name = _gridNewColumnNameBox.Text,
                    WidthPercent = newWidth
                });

                // Prorate OTHER columns to fill remaining space (new column stays fixed)
                ProrateGridColumnsExcept(_gridColumns.Count - 1, newWidth);

                // Refresh the ListBox to show updated percentages
                _gridColumnsBox!.ItemsSource = null;
                _gridColumnsBox.ItemsSource = _gridColumns;

                _gridNewColumnNameBox.Clear();
                _hasUnsavedChanges = true;
            }
        }

        // Grid column edit handlers
        private void GridColumnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_gridColumnsBox?.SelectedIndex >= 0 && _gridColumns != null && _gridColumnEditPanel != null)
            {
                _gridColumnEditIndex = _gridColumnsBox.SelectedIndex;
                var col = _gridColumns[_gridColumnEditIndex];
                _gridColumnEditNameBox!.Text = col.Name;
                _gridColumnEditWidthBox!.Value = col.WidthPercent;

                _gridColumnEditPanel.Visibility = Visibility.Visible;
                _gridColumnEditNameBox.Focus();
                _gridColumnEditNameBox.SelectAll();
            }
        }

        private void GridColumnSaveEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_gridColumnEditIndex >= 0 && _gridColumns != null && _gridColumnEditNameBox != null && _gridColumnEditWidthBox != null)
            {
                if (string.IsNullOrWhiteSpace(_gridColumnEditNameBox.Text))
                {
                    AppMessageBox.Show("Please enter a column name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int editedWidth = (int)(_gridColumnEditWidthBox.Value ?? 20);
                if (editedWidth > 95) editedWidth = 95; // Cap to leave room for others

                _gridColumns[_gridColumnEditIndex].Name = _gridColumnEditNameBox.Text;
                _gridColumns[_gridColumnEditIndex].WidthPercent = editedWidth;

                // Prorate OTHER columns to fill remaining space (edited column stays fixed)
                ProrateGridColumnsExcept(_gridColumnEditIndex, editedWidth);

                // Refresh the ListBox to show updated values
                _gridColumnsBox!.ItemsSource = null;
                _gridColumnsBox.ItemsSource = _gridColumns;
                _gridColumnsBox.SelectedIndex = _gridColumnEditIndex;

                GridColumnHideEditPanel();
                _hasUnsavedChanges = true;
            }
        }

        // Prorate grid columns except the one at fixedIndex, which keeps fixedValue
        // Other columns are scaled to fill (100 - fixedValue)
        private void ProrateGridColumnsExcept(int fixedIndex, int fixedValue)
        {
            if (_gridColumns == null || _gridColumns.Count <= 1) return;

            int remainingSpace = 100 - fixedValue;
            if (remainingSpace < 0) remainingSpace = 0;

            // Sum of all OTHER columns (excluding the fixed one)
            int sumOfOthers = 0;
            for (int i = 0; i < _gridColumns.Count; i++)
            {
                if (i != fixedIndex) sumOfOthers += _gridColumns[i].WidthPercent;
            }

            if (sumOfOthers == 0) return; // Avoid division by zero

            // Prorate other columns to fill remaining space
            int runningTotal = 0;
            int lastOtherIndex = -1;

            // Find the last "other" column index for remainder assignment
            for (int i = _gridColumns.Count - 1; i >= 0; i--)
            {
                if (i != fixedIndex) { lastOtherIndex = i; break; }
            }

            for (int i = 0; i < _gridColumns.Count; i++)
            {
                if (i == fixedIndex) continue; // Skip the fixed column

                if (i == lastOtherIndex)
                {
                    // Last other column gets remainder to ensure exact total
                    _gridColumns[i].WidthPercent = remainingSpace - runningTotal;
                }
                else
                {
                    int adjusted = (int)Math.Round((_gridColumns[i].WidthPercent / (double)sumOfOthers) * remainingSpace);
                    if (adjusted < 1) adjusted = 1; // Minimum 1%
                    _gridColumns[i].WidthPercent = adjusted;
                    runningTotal += adjusted;
                }
            }
        }

        private void GridColumnHideEditPanel()
        {
            if (_gridColumnEditPanel != null)
            {
                _gridColumnEditPanel.Visibility = Visibility.Collapsed;
                _gridColumnEditIndex = -1;
            }
        }

        // Get Grid structure JSON from editor
        private string GetGridStructureJson()
        {
            var structure = new GridStructure
            {
                Title = _gridTitleBox?.Text ?? "GRID",
                Columns = _gridColumns?.ToList() ?? new List<TemplateColumn>(),
                RowCount = (int)(_gridRowCountBox?.Value ?? 22),
                RowHeightIncreasePercent = (int)(_gridRowHeightSlider?.Value ?? 0),
                BaseHeaderFontSize = _gridBaseHeaderFontSize,  // Preserve original base font size
                FontSizeAdjustPercent = (int)(_gridFontSizeSlider?.Value ?? 0),
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
            var btnColEdit = new Button { Content = "✎", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Edit" };
            btnColEdit.Click += FormColumnEdit_Click;
            colBtnPanel.Children.Add(btnColEdit);
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

            // Column edit panel (hidden by default)
            _formColumnEditPanel = new Grid { Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
            _formColumnEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _formColumnEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _formColumnEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            _formColumnEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var colEditLabel = new TextBlock
            {
                Text = "Edit:",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(colEditLabel, 0);
            _formColumnEditPanel.Children.Add(colEditLabel);

            _formColumnEditNameBox = new TextBox { Height = 28, ToolTip = "Edit column name", Margin = new Thickness(0, 0, 5, 0) };
            _formColumnEditNameBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) FormColumnSaveEdit_Click(s, e);
                if (e.Key == System.Windows.Input.Key.Escape) FormColumnHideEditPanel();
            };
            Grid.SetColumn(_formColumnEditNameBox, 1);
            _formColumnEditPanel.Children.Add(_formColumnEditNameBox);

            _formColumnEditWidthBox = new Syncfusion.Windows.Shared.IntegerTextBox
            {
                MinValue = 5,
                MaxValue = 100,
                Height = 28,
                Width = 55,
                ToolTip = "Width %"
            };
            Grid.SetColumn(_formColumnEditWidthBox, 2);
            _formColumnEditPanel.Children.Add(_formColumnEditWidthBox);

            var colEditBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 0, 0, 0) };
            var btnColSaveEdit = new Button { Content = "Save", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 3, 0), ToolTip = "Save changes" };
            btnColSaveEdit.Click += FormColumnSaveEdit_Click;
            colEditBtnPanel.Children.Add(btnColSaveEdit);
            var btnColCancelEdit = new Button { Content = "Cancel", Padding = new Thickness(8, 4, 8, 4), ToolTip = "Cancel editing" };
            btnColCancelEdit.Click += (s, e) => FormColumnHideEditPanel();
            colEditBtnPanel.Children.Add(btnColCancelEdit);
            Grid.SetColumn(colEditBtnPanel, 3);
            _formColumnEditPanel.Children.Add(colEditBtnPanel);
            panel.Children.Add(_formColumnEditPanel);

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
            var btnSecEdit = new Button { Content = "✎", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Edit" };
            btnSecEdit.Click += FormSectionEdit_Click;
            secBtnPanel.Children.Add(btnSecEdit);
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

            // Section edit panel (hidden by default)
            _formSectionEditPanel = new Grid { Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
            _formSectionEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _formSectionEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _formSectionEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var secEditLabel = new TextBlock
            {
                Text = "Edit:",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(secEditLabel, 0);
            _formSectionEditPanel.Children.Add(secEditLabel);

            _formSectionEditBox = new TextBox { Height = 28, ToolTip = "Edit section name", Margin = new Thickness(0, 0, 5, 0) };
            _formSectionEditBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) FormSectionSaveEdit_Click(s, e);
                if (e.Key == System.Windows.Input.Key.Escape) FormSectionHideEditPanel();
            };
            Grid.SetColumn(_formSectionEditBox, 1);
            _formSectionEditPanel.Children.Add(_formSectionEditBox);

            var secEditBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 0, 0, 0) };
            var btnSecSaveEdit = new Button { Content = "Save", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 3, 0), ToolTip = "Save changes" };
            btnSecSaveEdit.Click += FormSectionSaveEdit_Click;
            secEditBtnPanel.Children.Add(btnSecSaveEdit);
            var btnSecCancelEdit = new Button { Content = "Cancel", Padding = new Thickness(8, 4, 8, 4), ToolTip = "Cancel editing" };
            btnSecCancelEdit.Click += (s, e) => FormSectionHideEditPanel();
            secEditBtnPanel.Children.Add(btnSecCancelEdit);
            Grid.SetColumn(secEditBtnPanel, 2);
            _formSectionEditPanel.Children.Add(secEditBtnPanel);
            panel.Children.Add(_formSectionEditPanel);

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
            var btnItemEdit = new Button { Content = "✎", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 3), ToolTip = "Edit" };
            btnItemEdit.Click += FormItemEdit_Click;
            itemBtnPanel.Children.Add(btnItemEdit);
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

            // Item edit panel (hidden by default)
            _formItemEditPanel = new Grid { Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
            _formItemEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _formItemEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _formItemEditPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var itemEditLabel = new TextBlock
            {
                Text = "Edit:",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(itemEditLabel, 0);
            _formItemEditPanel.Children.Add(itemEditLabel);

            _formItemEditBox = new TextBox { Height = 28, ToolTip = "Edit item text", Margin = new Thickness(0, 0, 5, 0) };
            _formItemEditBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) FormItemSaveEdit_Click(s, e);
                if (e.Key == System.Windows.Input.Key.Escape) FormItemHideEditPanel();
            };
            Grid.SetColumn(_formItemEditBox, 1);
            _formItemEditPanel.Children.Add(_formItemEditBox);

            var itemEditBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 0, 0, 0) };
            var btnItemSaveEdit = new Button { Content = "Save", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 3, 0), ToolTip = "Save changes" };
            btnItemSaveEdit.Click += FormItemSaveEdit_Click;
            itemEditBtnPanel.Children.Add(btnItemSaveEdit);
            var btnItemCancelEdit = new Button { Content = "Cancel", Padding = new Thickness(8, 4, 8, 4), ToolTip = "Cancel editing" };
            btnItemCancelEdit.Click += (s, e) => FormItemHideEditPanel();
            itemEditBtnPanel.Children.Add(btnItemCancelEdit);
            Grid.SetColumn(itemEditBtnPanel, 2);
            _formItemEditPanel.Children.Add(itemEditBtnPanel);
            panel.Children.Add(_formItemEditPanel);

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

            // Font Size Adjust slider
            panel.Children.Add(new TextBlock
            {
                Text = "Font Size Adjust %",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });
            var fontSliderGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            fontSliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fontSliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            _formFontSizeSlider = new Slider
            {
                Minimum = -30,
                Maximum = 50,
                Value = structure.FontSizeAdjustPercent,
                TickFrequency = 10,
                IsSnapToTickEnabled = true,
                ToolTip = "Adjust font size (-30% to +50%)"
            };
            _formFontSizeSlider.ValueChanged += (s, e) => _hasUnsavedChanges = true;
            Grid.SetColumn(_formFontSizeSlider, 0);
            fontSliderGrid.Children.Add(_formFontSizeSlider);

            var fontSliderLabel = new TextBlock
            {
                Foreground = (Brush)FindResource("ForegroundColor"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            fontSliderLabel.SetBinding(TextBlock.TextProperty, new Binding("Value")
            {
                Source = _formFontSizeSlider,
                StringFormat = "{0:+0;-0;0}%"
            });
            Grid.SetColumn(fontSliderLabel, 1);
            fontSliderGrid.Children.Add(fontSliderLabel);
            panel.Children.Add(fontSliderGrid);

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

                // Prorate remaining columns to total 100% (fixedIndex=-1 means scale all)
                ProrateFormColumnsExcept(-1, 0);

                // Refresh the ListBox to show updated percentages
                _formColumnsBox.ItemsSource = null;
                _formColumnsBox.ItemsSource = _formColumns;

                _hasUnsavedChanges = true;
            }
        }

        private void FormColumnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_formNewColumnNameBox != null && _formNewColumnWidthBox != null && _formColumns != null)
            {
                if (string.IsNullOrWhiteSpace(_formNewColumnNameBox.Text))
                {
                    AppMessageBox.Show("Please enter a column name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int newWidth = (int)(_formNewColumnWidthBox.Value ?? 20);

                // Add the new column first
                _formColumns.Add(new TemplateColumn
                {
                    Name = _formNewColumnNameBox.Text,
                    WidthPercent = newWidth
                });

                // Prorate other columns to fill remaining space (new column keeps its input value)
                int newColumnIndex = _formColumns.Count - 1;
                ProrateFormColumnsExcept(newColumnIndex, newWidth);

                // Refresh the ListBox to show updated percentages
                _formColumnsBox!.ItemsSource = null;
                _formColumnsBox.ItemsSource = _formColumns;

                _formNewColumnNameBox.Clear();
                _hasUnsavedChanges = true;
            }
        }

        // Column edit handlers
        private void FormColumnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_formColumnsBox?.SelectedIndex >= 0 && _formColumns != null &&
                _formColumnEditNameBox != null && _formColumnEditWidthBox != null && _formColumnEditPanel != null)
            {
                _formColumnEditIndex = _formColumnsBox.SelectedIndex;
                var col = _formColumns[_formColumnEditIndex];
                _formColumnEditNameBox.Text = col.Name;
                _formColumnEditWidthBox.Value = col.WidthPercent;

                _formColumnEditPanel.Visibility = Visibility.Visible;
                _formColumnEditNameBox.Focus();
                _formColumnEditNameBox.SelectAll();
            }
            else
            {
                AppMessageBox.Show("Please select a column to edit.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void FormColumnHideEditPanel()
        {
            if (_formColumnEditPanel != null)
            {
                _formColumnEditPanel.Visibility = Visibility.Collapsed;
                _formColumnEditIndex = -1;
            }
        }

        // Prorate form columns except the one at fixedIndex, which keeps fixedValue
        // Other columns are scaled to fill (100 - fixedValue)
        private void ProrateFormColumnsExcept(int fixedIndex, int fixedValue)
        {
            if (_formColumns == null || _formColumns.Count <= 1) return;

            int remainingSpace = 100 - fixedValue;
            if (remainingSpace < 0) remainingSpace = 0;

            // Sum of all OTHER columns (excluding the fixed one)
            int sumOfOthers = 0;
            for (int i = 0; i < _formColumns.Count; i++)
            {
                if (i != fixedIndex) sumOfOthers += _formColumns[i].WidthPercent;
            }

            if (sumOfOthers == 0) return; // Avoid division by zero

            // Prorate other columns to fill remaining space
            int runningTotal = 0;
            int lastOtherIndex = -1;

            // Find the last "other" column index for remainder assignment
            for (int i = _formColumns.Count - 1; i >= 0; i--)
            {
                if (i != fixedIndex) { lastOtherIndex = i; break; }
            }

            for (int i = 0; i < _formColumns.Count; i++)
            {
                if (i == fixedIndex) continue; // Skip the fixed column

                if (i == lastOtherIndex)
                {
                    // Last other column gets remainder to ensure exact total
                    _formColumns[i].WidthPercent = remainingSpace - runningTotal;
                }
                else
                {
                    int adjusted = (int)Math.Round((_formColumns[i].WidthPercent / (double)sumOfOthers) * remainingSpace);
                    if (adjusted < 1) adjusted = 1; // Minimum 1%
                    _formColumns[i].WidthPercent = adjusted;
                    runningTotal += adjusted;
                }
            }
        }

        private void FormColumnSaveEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_formColumnEditNameBox != null && _formColumnEditWidthBox != null && _formColumns != null &&
                _formColumnEditIndex >= 0 && _formColumnEditIndex < _formColumns.Count)
            {
                if (string.IsNullOrWhiteSpace(_formColumnEditNameBox.Text))
                {
                    AppMessageBox.Show("Column name cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int newWidth = (int)(_formColumnEditWidthBox.Value ?? 20);
                _formColumns[_formColumnEditIndex].Name = _formColumnEditNameBox.Text;
                _formColumns[_formColumnEditIndex].WidthPercent = newWidth;

                // Prorate other columns to fill remaining space (edited column keeps its input value)
                ProrateFormColumnsExcept(_formColumnEditIndex, newWidth);

                // Refresh the ListBox to show updated values
                _formColumnsBox!.ItemsSource = null;
                _formColumnsBox.ItemsSource = _formColumns;
                _formColumnsBox.SelectedIndex = _formColumnEditIndex;

                FormColumnHideEditPanel();
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
                    AppMessageBox.Show("Please enter a section name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _formSections.Add(new SectionDefinition { Name = _formNewSectionBox.Text });
                _formNewSectionBox.Clear();
                _hasUnsavedChanges = true;
            }
        }

        // Section edit handlers
        private void FormSectionEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_formSectionsBox?.SelectedIndex >= 0 && _formSections != null &&
                _formSectionEditBox != null && _formSectionEditPanel != null)
            {
                _formSectionEditIndex = _formSectionsBox.SelectedIndex;
                var section = _formSections[_formSectionEditIndex];
                _formSectionEditBox.Text = section.Name;

                _formSectionEditPanel.Visibility = Visibility.Visible;
                _formSectionEditBox.Focus();
                _formSectionEditBox.SelectAll();
            }
            else
            {
                AppMessageBox.Show("Please select a section to edit.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void FormSectionHideEditPanel()
        {
            if (_formSectionEditPanel != null)
            {
                _formSectionEditPanel.Visibility = Visibility.Collapsed;
                _formSectionEditIndex = -1;
            }
        }

        private void FormSectionSaveEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_formSectionEditBox != null && _formSections != null &&
                _formSectionEditIndex >= 0 && _formSectionEditIndex < _formSections.Count)
            {
                if (string.IsNullOrWhiteSpace(_formSectionEditBox.Text))
                {
                    AppMessageBox.Show("Section name cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _formSections[_formSectionEditIndex].Name = _formSectionEditBox.Text;

                // Refresh the ListBox to show updated value
                _formSectionsBox!.ItemsSource = null;
                _formSectionsBox.ItemsSource = _formSections;
                _formSectionsBox.SelectedIndex = _formSectionEditIndex;

                FormSectionHideEditPanel();
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
                    AppMessageBox.Show("Please enter an item text.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                AppMessageBox.Show("Please select a section first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Item edit handlers
        private void FormItemEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_formSectionsBox?.SelectedItem is SectionDefinition section &&
                _formSectionItemsBox?.SelectedIndex >= 0 &&
                _formItemEditBox != null && _formItemEditPanel != null)
            {
                _formItemEditIndex = _formSectionItemsBox.SelectedIndex;
                _formItemEditBox.Text = section.Items[_formItemEditIndex];

                _formItemEditPanel.Visibility = Visibility.Visible;
                _formItemEditBox.Focus();
                _formItemEditBox.SelectAll();
            }
            else
            {
                AppMessageBox.Show("Please select an item to edit.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void FormItemHideEditPanel()
        {
            if (_formItemEditPanel != null)
            {
                _formItemEditPanel.Visibility = Visibility.Collapsed;
                _formItemEditIndex = -1;
            }
        }

        private void FormItemSaveEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_formSectionsBox?.SelectedItem is SectionDefinition section &&
                _formItemEditBox != null && _formItemEditIndex >= 0 && _formItemEditIndex < section.Items.Count)
            {
                if (string.IsNullOrWhiteSpace(_formItemEditBox.Text))
                {
                    AppMessageBox.Show("Item text cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                section.Items[_formItemEditIndex] = _formItemEditBox.Text;

                // Refresh the ListBox to show updated value
                _formSectionItemsBox!.ItemsSource = null;
                _formSectionItemsBox.ItemsSource = section.Items;
                _formSectionItemsBox.SelectedIndex = _formItemEditIndex;

                FormItemHideEditPanel();
                _hasUnsavedChanges = true;
            }
        }

        // Get Form structure JSON from editor
        private string GetFormStructureJson()
        {
            var structure = new FormStructure
            {
                Title = _formTitleBox?.Text ?? "FORM",
                Columns = _formColumns?.ToList() ?? new List<TemplateColumn>(),
                Sections = _formSections?.ToList() ?? new List<SectionDefinition>(),
                RowHeightIncreasePercent = (int)(_formRowHeightSlider?.Value ?? 0),
                FontSizeAdjustPercent = (int)(_formFontSizeSlider?.Value ?? 0),
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
                TemplateTypes.Drawings => GetDrawingsStructureJson(),
                TemplateTypes.ExternalFile => GetExternalFileStructureJson(),
                _ => null // Return null for unsupported editors (will use original structure)
            };
        }

        // Build and display the External File type editor: shows the linked PDF path and a
        // Relink button to point at a new location. The file's pages are merged into the work
        // package at generation time; there is no content to edit here.
        private void BuildExternalFileEditor(ExternalFileStructure structure)
        {
            _currentEditorType = TemplateTypes.ExternalFile;

            var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            panel.Children.Add(new TextBlock
            {
                Text = "The pages of this PDF are merged into the work package wherever this form is placed.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("TextColorSecondary"),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 0, 0, 15)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "PDF File",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var pathGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _externalFilePathBox = new TextBox
            {
                Text = structure.FilePath ?? "",
                Height = 28,
                IsReadOnly = true,
                ToolTip = "The linked PDF. Use Relink to point at a new location."
            };
            Grid.SetColumn(_externalFilePathBox, 0);
            pathGrid.Children.Add(_externalFilePathBox);

            var relinkBtn = new Button
            {
                Content = "Relink...",
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Browse to a new location for this PDF"
            };
            relinkBtn.Click += RelinkExternalFile_Click;
            Grid.SetColumn(relinkBtn, 1);
            pathGrid.Children.Add(relinkBtn);

            panel.Children.Add(pathGrid);

            // Rename this form in place. (The top Name field + Save creates a new copy; this
            // renames the existing form without spawning a duplicate.)
            panel.Children.Add(new TextBlock
            {
                Text = "Rename this form",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var renameGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            renameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            renameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _externalRenameBox = new TextBox
            {
                Text = _selectedFormTemplate?.TemplateName ?? structure.Title,
                Height = 28,
                ToolTip = "New name for this form"
            };
            Grid.SetColumn(_externalRenameBox, 0);
            renameGrid.Children.Add(_externalRenameBox);

            var renameBtn = new Button
            {
                Content = "Rename",
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Rename this form in place (does not create a copy)"
            };
            renameBtn.Click += RenameExternalFile_Click;
            Grid.SetColumn(renameBtn, 1);
            renameGrid.Children.Add(renameBtn);

            panel.Children.Add(renameGrid);

            panel.Children.Add(new TextBlock
            {
                Text = "If this file can't be found when you generate, you'll be asked whether to cancel or continue without it.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("TextColorSecondary"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 0)
            });

            FormEditorContent.Content = panel;
        }

        // Relink the external-file template to a new PDF location.
        private void RelinkExternalFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Files|*.pdf",
                Title = "Relink PDF File"
            };

            if (dialog.ShowDialog() == true && _externalFilePathBox != null)
            {
                _externalFilePathBox.Text = dialog.FileName;
                _hasUnsavedChanges = true;
            }
        }

        // Rename the current external-file form in place - updates the existing record (name +
        // current link) without creating a duplicate. The top Name field + Save still creates a
        // new copy; this is the rename-in-place path.
        private async void RenameExternalFile_Click(object sender, RoutedEventArgs e)
        {
            await RenameCurrentFormInPlaceAsync(_externalRenameBox, name => JsonSerializer.Serialize(new ExternalFileStructure
            {
                Title = name,
                FilePath = string.IsNullOrWhiteSpace(_externalFilePathBox?.Text) ? null : _externalFilePathBox.Text
            }));
        }

        // Rename the currently-selected user form template in place (updates the existing record,
        // no duplicate) and persist the current structure. Shared by the External File and
        // Drawings editors. buildStructureJson receives the new name and returns the StructureJson.
        private async Task RenameCurrentFormInPlaceAsync(TextBox? renameBox, Func<string, string> buildStructureJson)
        {
            if (_selectedFormTemplate == null || renameBox == null) return;

            string newName = renameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                AppMessageBox.Show("Please enter a name.", "Rename", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Rename only applies to an already-saved user template.
            if (_selectedFormTemplate.IsBuiltIn || !_formTemplates.Any(t => t.TemplateID == _selectedFormTemplate.TemplateID))
            {
                AppMessageBox.Show("Save this form first, then rename it.", "Rename", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Name must be unique among other templates.
            if (_formTemplates.Any(t => t.TemplateID != _selectedFormTemplate.TemplateID
                                        && t.TemplateName.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            {
                AppMessageBox.Show($"A template named '{newName}' already exists. Please choose a different name.",
                    "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Persist the new name plus the current structure (so a relink done here isn't lost).
                _selectedFormTemplate.TemplateName = newName;
                _selectedFormTemplate.StructureJson = buildStructureJson(newName);
                await TemplateRepository.UpdateFormTemplateAsync(_selectedFormTemplate);

                string renamedId = _selectedFormTemplate.TemplateID;
                _formTemplates = await TemplateRepository.GetAllFormTemplatesAsync();
                _suppressTypeDialog = true;
                PopulateFormTemplateEditDropdown();
                _suppressTypeDialog = false;
                PopulateAddFormMenu();

                var renamed = _formTemplates.FirstOrDefault(t => t.TemplateID == renamedId);
                if (renamed != null)
                {
                    cboFormTemplateEdit.SelectedItem = renamed;
                    _selectedFormTemplate = renamed;
                }
                _hasUnsavedChanges = false;
                lblStatus.Text = "Form renamed";
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "WorkPackageView.RenameCurrentFormInPlaceAsync");
                AppMessageBox.Show($"Error renaming form: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Serialize the external-file editor back to StructureJson.
        private string GetExternalFileStructureJson()
        {
            var structure = new ExternalFileStructure
            {
                Title = txtFormTemplateName.Text,
                FilePath = string.IsNullOrWhiteSpace(_externalFilePathBox?.Text) ? null : _externalFilePathBox.Text
            };
            return JsonSerializer.Serialize(structure);
        }

        // Clear editor content
        private void ClearFormEditor()
        {
            FormEditorContent.Content = null;
            _currentEditorType = null;
        }

        // Build and display the Drawings type editor: shows the parent folder (whose per-WP
        // subfolders hold the drawing PDFs) with Relink + in-place Rename, mirroring External File.
        private void BuildDrawingsEditor(DrawingsStructure structure)
        {
            _currentEditorType = TemplateTypes.Drawings;

            var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            panel.Children.Add(new TextBlock
            {
                Text = "At generation, the PDFs in the subfolder named exactly like the work package (inside this parent folder) are merged into the work package at this form's position.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("TextColorSecondary"),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 0, 0, 15)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Parent folder",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var pathGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _drawingsFolderPathBox = new TextBox
            {
                Text = structure.ParentFolderPath ?? "",
                Height = 28,
                IsReadOnly = true,
                ToolTip = "The parent folder. Its subfolders are named per work package. Use Relink to change it."
            };
            Grid.SetColumn(_drawingsFolderPathBox, 0);
            pathGrid.Children.Add(_drawingsFolderPathBox);

            var relinkBtn = new Button
            {
                Content = "Relink...",
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Browse to a new parent folder"
            };
            relinkBtn.Click += RelinkDrawingsFolder_Click;
            Grid.SetColumn(relinkBtn, 1);
            pathGrid.Children.Add(relinkBtn);

            panel.Children.Add(pathGrid);

            // Rename this form in place (the top Name field + Save creates a new copy).
            panel.Children.Add(new TextBlock
            {
                Text = "Rename this form",
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ForegroundColor"),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var renameGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            renameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            renameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _drawingsRenameBox = new TextBox
            {
                Text = _selectedFormTemplate?.TemplateName ?? structure.Title,
                Height = 28,
                ToolTip = "New name for this form"
            };
            Grid.SetColumn(_drawingsRenameBox, 0);
            renameGrid.Children.Add(_drawingsRenameBox);

            var renameBtn = new Button
            {
                Content = "Rename",
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Rename this form in place (does not create a copy)"
            };
            renameBtn.Click += RenameDrawings_Click;
            Grid.SetColumn(renameBtn, 1);
            renameGrid.Children.Add(renameBtn);

            panel.Children.Add(renameGrid);

            panel.Children.Add(new TextBlock
            {
                Text = "If a work package has no matching subfolder when you generate, you'll be asked whether to cancel or continue without its drawings.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("TextColorSecondary"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 0)
            });

            FormEditorContent.Content = panel;
        }

        // Relink the drawings form to a new parent folder.
        private void RelinkDrawingsFolder_Click(object sender, RoutedEventArgs e)
        {
            var selectedPath = ShowFolderPickerDialog("Select Drawings Parent Folder");
            if (!string.IsNullOrEmpty(selectedPath) && _drawingsFolderPathBox != null)
            {
                _drawingsFolderPathBox.Text = selectedPath;
                _hasUnsavedChanges = true;
            }
        }

        // Rename the current drawings form in place (does not create a copy).
        private async void RenameDrawings_Click(object sender, RoutedEventArgs e)
        {
            await RenameCurrentFormInPlaceAsync(_drawingsRenameBox, name => JsonSerializer.Serialize(new DrawingsStructure
            {
                Title = name,
                ParentFolderPath = string.IsNullOrWhiteSpace(_drawingsFolderPathBox?.Text) ? null : _drawingsFolderPathBox.Text
            }));
        }

        // Collect Drawings editor values to JSON.
        private string GetDrawingsStructureJson()
        {
            var structure = new DrawingsStructure
            {
                Title = txtFormTemplateName.Text,
                ParentFolderPath = string.IsNullOrWhiteSpace(_drawingsFolderPathBox?.Text) ? null : _drawingsFolderPathBox.Text
            };
            return JsonSerializer.Serialize(structure);
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
