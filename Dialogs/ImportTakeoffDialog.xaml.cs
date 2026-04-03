using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Services.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ImportTakeoffDialog : Window
    {
        private const string ProfileIndexKey = "ImportProfiles.Index";
        private const string ProfileDataPrefix = "ImportProfile.";

        private List<(string ProjectID, string SetName)> _rocSets = new();
        private string? _selectedFilePath;
        private bool _isLoadingDropdowns;
        private bool _isLoadingProfile;
        private ImportProfile? _activeProfile;
        private ObservableCollection<ColumnMappingItem> _mappingItems = new();
        private ObservableCollection<MetadataFieldItem> _metadataItems = new();

        // Activity properties excluded from mapping dropdowns
        private static readonly HashSet<string> ExcludedColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            // Read-only / system
            "ActivityID", "UniqueID", "LocalDirty", "SyncVersion",
            "AzureUploadUtcDate", "UpdatedBy", "UpdatedUtcDate",
            "CreatedBy", "AssignedTo", "WeekEndDate", "ProgDate",
            "PrevEarnMHs", "EarnMHsCalc", "PercentCompleteCalc",
            "EarnedQtyCalc", "Status", "ROCLookupID", "EarnedMHsRoc",
            // Date fields
            "ActStart", "ActFin", "PlanStart", "PlanFin",
            // Display / calculated / helper
            "PercentEntry_Display", "PercentCompleteCalc_Display",
            "EarnedQtyCalc_Display", "AzureUploadUtcDateDisplay",
            "UpdatedUtcDateDisplay", "AssignedToUsername",
            "IsMyRecord", "IsEditable",
            "HasInvalidProjectID", "HasMissingActStart", "HasMissingActFin",
            "ClientEquivEarnQTY"
        };

        // Numeric Activity properties for data type validation
        private static readonly HashSet<string> NumericColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            "HexNO", "XRay", "DateTrigger", "UDF7",
            "Quantity", "EarnQtyEntry", "PercentEntry",
            "BudgetMHs", "BaseUnit", "BudgetHoursGroup", "BudgetHoursROC",
            "EquivQTY", "ROCID", "ROCPercent", "ROCBudgetQTY",
            "PipeSize1", "PipeSize2", "PrevEarnQTY",
            "ClientEquivQty", "ClientBudget", "ClientCustom3"
        };

        // Required metadata fields that must be populated for every imported record
        private static readonly string[] RequiredMetadataFields = new[]
        {
            "ProjectID", "WorkPackage", "PhaseCode", "CompType",
            "PhaseCategory", "SchedActNO", "Description", "ROCStep", "RespParty"
        };

        // Vantage Excel backup template column order
        public static readonly string[] VantageExcelColumns = new[]
        {
            "HexNO", "CompType", "PhaseCategory", "ROCStep", "DwgNO", "RevNO",
            "SecondDwgNO", "ShtNO", "Notes", "SecondActno", "ActStart", "ActFin",
            "PlanStart", "PlanFin", "Status", "Aux1", "Aux2", "Aux3", "Area",
            "ChgOrdNO", "Description", "EqmtNO", "Estimator", "InsulType",
            "LineNumber", "MtrlSpec", "PhaseCode", "PaintCode", "PipeGrade",
            "ProjectID", "RFINO", "SchedActNO", "Service", "ShopField", "SubArea",
            "PjtSystem", "PjtSystemNo", "TagNO", "HtTrace", "WorkPackage", "XRay",
            "DateTrigger", "UDF1", "UDF2", "UDF3", "UDF4", "UDF5", "UDF6", "UDF7",
            "UDF8", "UDF9", "UDF10", "UDF11", "UDF12", "UDF13", "UDF14", "UDF15",
            "UDF16", "UDF17", "RespParty", "UniqueID", "UDF20", "BaseUnit",
            "BudgetMHs", "BudgetHoursGroup", "BudgetHoursROC", "EarnedMHsRoc",
            "EarnMHsCalc", "EarnedQtyCalc", "EarnQtyEntry", "EquivQTY", "EquivUOM",
            "PercentCompleteCalc", "PercentEntry", "Quantity", "ROCID", "ROCLookupID",
            "ROCPercent", "ROCBudgetQTY", "PipeSize1", "PipeSize2", "PrevEarnMHs",
            "PrevEarnQTY", "UpdatedUtcDate", "UOM", "ClientEquivQty", "ClientBudget",
            "ClientCustom3"
        };

        // All mappable Activity property names
        private List<string> _mappableColumns = new();

        public ImportTakeoffDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            BuildMappableColumnList();
            BuildMetadataItems();
            Loaded += ImportTakeoffDialog_Loaded;
        }

        // Build the list of Activity properties available for mapping
        private void BuildMappableColumnList()
        {
            _mappableColumns = typeof(Activity).GetProperties()
                .Where(p => p.CanWrite && !ExcludedColumns.Contains(p.Name))
                .Select(p => p.Name)
                .OrderBy(n => n)
                .ToList();
        }

        // Build the metadata field rows
        private void BuildMetadataItems()
        {
            _metadataItems.Clear();
            foreach (var field in RequiredMetadataFields)
            {
                var item = new MetadataFieldItem
                {
                    FieldName = field,
                    Mode = "Enter Value",
                    EnteredValue = string.Empty
                };
                item.PropertyChanged += MetadataItem_PropertyChanged;
                _metadataItems.Add(item);
            }
            icMetadataRows.ItemsSource = _metadataItems;
        }

        private async void ImportTakeoffDialog_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProfileDropdown();
            await LoadROCSetsAsync();
        }

        // Load ROC sets into the dropdown
        private async System.Threading.Tasks.Task LoadROCSetsAsync()
        {
            try
            {
                _isLoadingDropdowns = true;
                _rocSets = await ProjectRateRepository.GetROCSetsAsync();

                cboROCSet.Items.Clear();
                cboROCSet.Items.Add("+ Create New...");
                cboROCSet.Items.Add("None");
                foreach (var (projectId, setName) in _rocSets)
                    cboROCSet.Items.Add($"{projectId} - {setName}");

                cboROCSet.SelectedIndex = 1;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ImportTakeoffDialog.LoadROCSetsAsync");
                cboROCSet.Items.Clear();
                cboROCSet.Items.Add("+ Create New...");
                cboROCSet.Items.Add("None");
                cboROCSet.SelectedIndex = 1;
            }
            finally
            {
                _isLoadingDropdowns = false;
            }
        }

        // Open file picker for .xlsx, then populate column mapping
        private async void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Takeoff Excel File",
                Filter = "Excel Files|*.xlsx|All Files|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            _selectedFilePath = dialog.FileName;
            txtSourceLabel.Text = System.IO.Path.GetFileName(_selectedFilePath);
            txtSourceLabel.Opacity = 1.0;

            await PopulateMappingFromFileAsync(_selectedFilePath);
        }

        // Show spinner, hide other mapping content
        private void ShowMappingSpinner()
        {
            txtMappingPlaceholder.Visibility = Visibility.Collapsed;
            gridMappingContent.Visibility = Visibility.Collapsed;
            pnlMappingSpinner.Visibility = Visibility.Visible;
            btnImport.IsEnabled = false;
        }

        // Hide spinner
        private void HideMappingSpinner()
        {
            pnlMappingSpinner.Visibility = Visibility.Collapsed;
        }

        // Read Labor tab headers and sample values from Excel, populate mapping grid
        private async System.Threading.Tasks.Task PopulateMappingFromFileAsync(string filePath)
        {
            ShowMappingSpinner();

            try
            {
                // Read Excel on background thread to keep UI responsive
                var (headers, samples, error) = await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        using var workbook = new XLWorkbook(filePath);
                        var laborSheet = workbook.Worksheets.FirstOrDefault(ws =>
                            ws.Name.Equals("Labor", StringComparison.OrdinalIgnoreCase));

                        if (laborSheet == null)
                            return (new List<string>(), new List<string>(), "The selected file does not contain a 'Labor' worksheet.");

                        var lastCol = laborSheet.LastColumnUsed()?.ColumnNumber() ?? 0;
                        var lastRow = laborSheet.LastRowUsed()?.RowNumber() ?? 1;
                        if (lastCol == 0)
                            return (new List<string>(), new List<string>(), "The Labor worksheet appears to be empty.");

                        var h = new List<string>();
                        var s = new List<string>();

                        for (int col = 1; col <= lastCol; col++)
                        {
                            string header = laborSheet.Cell(1, col).GetString().Trim();
                            if (string.IsNullOrEmpty(header)) continue;

                            h.Add(header);

                            // Scan data rows for first non-empty sample value
                            string sample = "";
                            for (int row = 2; row <= lastRow; row++)
                            {
                                var cell = laborSheet.Cell(row, col);
                                if (!cell.IsEmpty())
                                {
                                    string val = cell.GetString().Trim();
                                    if (!string.IsNullOrEmpty(val))
                                    {
                                        sample = val;
                                        break;
                                    }
                                }
                            }
                            s.Add(sample);
                        }

                        return (h, s, (string?)null);
                    }
                    catch (Exception ex)
                    {
                        return (new List<string>(), new List<string>(), ex.Message);
                    }
                });

                HideMappingSpinner();

                if (error != null)
                {
                    MessageBox.Show(error, "File Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ClearMappingGrid();
                    return;
                }

                BuildMappingGrid(headers, samples);

                // Re-apply active profile's column mappings and metadata now that rows exist
                if (_activeProfile != null)
                    ApplyProfileToUI(_activeProfile);
            }
            catch (Exception ex)
            {
                HideMappingSpinner();
                AppLogger.Error(ex, "ImportTakeoffDialog.PopulateMappingFromFileAsync");
                MessageBox.Show($"Error reading file:\n{ex.Message}", "File Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ClearMappingGrid();
            }
        }

        // Build mapping rows from headers and sample values
        private void BuildMappingGrid(List<string> headers, List<string> samples)
        {
            _mappingItems.Clear();

            // First pass: determine static defaults and mark them used
            var usedMappings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                string defaultMapping = GetDefaultMapping(headers[i]);
                if (defaultMapping != "Unmapped")
                {
                    defaults[headers[i]] = defaultMapping;
                    usedMappings.Add(defaultMapping);
                }
            }

            // Second pass: build mapping items with filtered available options
            for (int i = 0; i < headers.Count; i++)
            {
                string header = headers[i];
                string sample = i < samples.Count ? samples[i] : "";
                string defaultMapping = defaults.ContainsKey(header) ? defaults[header] : "Unmapped";

                var item = new ColumnMappingItem
                {
                    FileHeader = header,
                    SampleValue = sample
                };

                // Unmapped + columns not used by other rows (but include own default)
                var available = new ObservableCollection<string> { "Unmapped" };
                foreach (var col in _mappableColumns)
                {
                    if (!usedMappings.Contains(col) || col == defaultMapping)
                        available.Add(col);
                }

                item.AvailableMappings = available;
                item.SelectedMapping = defaultMapping;
                item.PropertyChanged += MappingItem_PropertyChanged;

                _mappingItems.Add(item);
            }

            icMappingRows.ItemsSource = _mappingItems;
            txtMappingPlaceholder.Visibility = Visibility.Collapsed;
            gridMappingContent.Visibility = Visibility.Visible;
            btnImport.IsEnabled = true;

            SyncMetadataFromMappings();
        }

        // When a mapping selection changes, refresh available options in all other rows
        private void MappingItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ColumnMappingItem.SelectedMapping)) return;

            var usedMappings = _mappingItems
                .Where(m => m.SelectedMapping != "Unmapped")
                .Select(m => m.SelectedMapping)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var item in _mappingItems)
            {
                var currentSelection = item.SelectedMapping;
                var available = new ObservableCollection<string> { "Unmapped" };

                foreach (var col in _mappableColumns)
                {
                    if (!usedMappings.Contains(col) || col == currentSelection)
                        available.Add(col);
                }

                item.AvailableMappings = available;
                item.OnPropertyChanged(nameof(ColumnMappingItem.AvailableMappings));

                if (available.Contains(currentSelection))
                    item.SelectedMapping = currentSelection;
                else
                    item.SelectedMapping = "Unmapped";
            }

            SyncMetadataFromMappings();
        }

        private bool _isSyncingMetadata;

        // Sync metadata fields based on column mappings: if a metadata field is mapped
        // in the column mapping grid, set it to "Use Source" and disable text entry.
        // If unmapped, revert to "Enter Value".
        private void SyncMetadataFromMappings()
        {
            if (_isSyncingMetadata) return;
            _isSyncingMetadata = true;

            var mappedFields = _mappingItems
                .Where(m => m.SelectedMapping != "Unmapped")
                .Select(m => m.SelectedMapping)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var meta in _metadataItems)
            {
                if (mappedFields.Contains(meta.FieldName))
                    meta.Mode = "Use Source";
                else if (meta.Mode == "Use Source")
                    meta.Mode = "Enter Value";
            }

            _isSyncingMetadata = false;
        }

        // Reverse sync: when user changes a metadata field's Mode, update column mappings.
        // Enter Value → unmap any column mapped to that field.
        // Use Source → no auto-mapping (user must map a column manually).
        private void MetadataItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MetadataFieldItem.Mode)) return;
            if (_isSyncingMetadata) return;
            if (sender is not MetadataFieldItem meta) return;

            _isSyncingMetadata = true;

            if (meta.Mode == "Enter Value")
            {
                // Unmap any column currently mapped to this metadata field
                foreach (var mapping in _mappingItems)
                {
                    if (string.Equals(mapping.SelectedMapping, meta.FieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        mapping.SelectedMapping = "Unmapped";
                        break;
                    }
                }
            }

            _isSyncingMetadata = false;
        }

        // Static default mappings for known Labor tab columns
        private static string GetDefaultMapping(string fileHeader)
        {
            return fileHeader switch
            {
                "Drawing Number" => "DwgNO",
                "Component" => "UDF6",
                "Size" => "PipeSize1",
                "Thickness" => "UDF9",
                "Class Rating" => "UDF8",
                "Matl_Grp" => "UDF3",
                "Commodity Code" => "UDF12",
                "Description" => "Description",
                "Quantity" => "Quantity",
                "ShopField" => "UDF1",
                "ROCStep" => "ROCStep",
                "RateSheet" => "BaseUnit",
                "BudgetMHs" => "BudgetMHs",
                "UOM" => "UOM",
                "Matl_Grp_Desc" => "UDF4",
                _ => "Unmapped"
            };
        }

        // Reset the mapping grid to placeholder state
        private void ClearMappingGrid()
        {
            _mappingItems.Clear();
            icMappingRows.ItemsSource = null;
            txtMappingPlaceholder.Visibility = Visibility.Visible;
            gridMappingContent.Visibility = Visibility.Collapsed;
            btnImport.IsEnabled = false;
        }

        // Size fields that accept dual size format (e.g., "6x4")
        private static readonly HashSet<string> SizeColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            "PipeSize1", "PipeSize2"
        };

        // Resolve a cell value for a numeric field, handling dual sizes for PipeSize columns.
        // For size fields: "6x4" → 6 (larger value). For all others: straight parse.
        public static double? ResolveNumericValue(string val, string targetColumn)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;

            if (SizeColumns.Contains(targetColumn))
            {
                var dual = FittingMakeupService.ParseDualSize(val);
                if (dual != null) return dual.Value.Larger;
            }

            return double.TryParse(val, out double result) ? result : null;
        }

        // Validate data types: check that all values in mapped file columns are compatible
        private bool ValidateDataTypes()
        {
            if (_selectedFilePath == null) return true;

            var numericMappings = _mappingItems
                .Where(m => m.SelectedMapping != "Unmapped" && NumericColumns.Contains(m.SelectedMapping))
                .ToList();

            if (numericMappings.Count == 0) return true;

            try
            {
                using var workbook = new XLWorkbook(_selectedFilePath);
                var laborSheet = workbook.Worksheets.First(ws =>
                    ws.Name.Equals("Labor", StringComparison.OrdinalIgnoreCase));

                var lastRow = laborSheet.LastRowUsed()?.RowNumber() ?? 1;
                var lastCol = laborSheet.LastColumnUsed()?.ColumnNumber() ?? 0;

                // Build header-to-column index map
                var headerColMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int col = 1; col <= lastCol; col++)
                {
                    string header = laborSheet.Cell(1, col).GetString().Trim();
                    if (!string.IsNullOrEmpty(header))
                        headerColMap[header] = col;
                }

                var errors = new List<string>();
                foreach (var mapping in numericMappings)
                {
                    if (!headerColMap.TryGetValue(mapping.FileHeader, out int colIdx))
                        continue;

                    for (int row = 2; row <= lastRow; row++)
                    {
                        var cell = laborSheet.Cell(row, colIdx);
                        if (cell.IsEmpty()) continue;

                        string val = cell.GetString().Trim();
                        if (string.IsNullOrEmpty(val)) continue;

                        // Use ResolveNumericValue which handles dual sizes for PipeSize columns
                        if (ResolveNumericValue(val, mapping.SelectedMapping) == null)
                        {
                            errors.Add($"Column '{mapping.FileHeader}' mapped to numeric field '{mapping.SelectedMapping}' " +
                                       $"contains non-numeric value \"{val}\" (row {row}).");
                            break;
                        }
                    }
                }

                if (errors.Count > 0)
                {
                    string message = "Data type validation failed:\n\n" +
                        string.Join("\n\n", errors) +
                        "\n\nPlease fix the mappings or correct the source data.";
                    MessageBox.Show(message, "Data Type Mismatch",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ImportTakeoffDialog.ValidateDataTypes");
                MessageBox.Show($"Error validating data types:\n{ex.Message}", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        // Handle ROC Set dropdown — "+ Create New..." opens the manager
        private async void CboROCSet_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingDropdowns || cboROCSet.SelectedIndex != 0) return;

            _isLoadingDropdowns = true;
            cboROCSet.SelectedIndex = 1;
            _isLoadingDropdowns = false;

            var dialog = new ManageROCRatesDialog(openInNewSetMode: true)
            {
                Owner = this
            };
            dialog.ShowDialog();

            await LoadROCSetsAsync();
        }

        // Import button — validates then performs import/excel/both based on Output selection
        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateDataTypes()) return;

            if (_selectedFilePath == null)
            {
                MessageBox.Show("No file selected.", "No Source", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnImport.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                // Step 1: Read all Labor tab rows on background thread
                var rawRows = await System.Threading.Tasks.Task.Run(() => ReadLaborRows(_selectedFilePath));
                if (rawRows.Count == 0)
                {
                    MessageBox.Show("No data rows found in the Labor worksheet.", "Empty Data",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Step 2: Apply Handling filter (PIPE/SPL)
                rawRows = ApplyHandlingFilter(rawRows);

                // Step 3: Roll Up BU Hardware (if checked)
                if (chkRollUpBUHardware.IsChecked == true)
                    rawRows = RollUpBUHardware(rawRows);

                // Step 4: Roll Up Fab Per DWG (if checked)
                if (chkRollUpFabPerDwg.IsChecked == true)
                    rawRows = RollUpFabPerDwg(rawRows);

                if (rawRows.Count == 0)
                {
                    MessageBox.Show("No rows remain after applying filters and rollups.", "No Data",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Step 5: Map to Activity objects
                var activities = MapToActivities(rawRows);

                // Step 6: Apply metadata overrides
                ApplyMetadata(activities);

                // Step 6b: Apply ROC splits if a set is selected
                activities = await ApplyROCSplitsAsync(activities);

                // Step 7: Generate UniqueIDs and set system fields
                SetSystemFields(activities);

                // Step 8: Execute output
                bool doImport = rbImportRecords.IsChecked == true || rbImportAndExcel.IsChecked == true;
                bool doExcel = rbCreateExcel.IsChecked == true || rbImportAndExcel.IsChecked == true;

                int importedCount = 0;
                string? excelPath = null;

                // Prompt for save location before doing any work
                if (doExcel)
                {
                    var saveDialog = new SaveFileDialog
                    {
                        Title = "Save VANTAGE Excel File",
                        Filter = "Excel Files|*.xlsx",
                        FileName = $"Takeoff_Import_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                        DefaultExt = ".xlsx"
                    };
                    if (saveDialog.ShowDialog() != true) return;
                    excelPath = saveDialog.FileName;
                }

                if (doImport)
                    importedCount = await System.Threading.Tasks.Task.Run(() => InsertActivities(activities));

                if (doExcel && excelPath != null)
                    await System.Threading.Tasks.Task.Run(() => CreateVantageExcel(activities, excelPath));

                // Step 9: Report results
                var parts = new List<string>();
                if (doImport) parts.Add($"{importedCount} activities imported");
                if (doExcel && excelPath != null) parts.Add($"Excel saved to:\n{excelPath}");

                MessageBox.Show(string.Join("\n\n", parts), "Import Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = doImport;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ImportTakeoffDialog.BtnImport_Click");
                MessageBox.Show($"Import failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnImport.IsEnabled = true;
                btnCancel.IsEnabled = true;
            }
        }

        // ========================================
        // DATA PROCESSING METHODS
        // ========================================

        // Read all data rows from Labor tab as dictionaries keyed by header name
        private static List<Dictionary<string, string>> ReadLaborRows(string filePath)
        {
            var rows = new List<Dictionary<string, string>>();
            using var workbook = new XLWorkbook(filePath);
            var ws = workbook.Worksheets.First(s => s.Name.Equals("Labor", StringComparison.OrdinalIgnoreCase));

            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            if (lastCol == 0 || lastRow <= 1) return rows;

            // Read headers
            var headers = new List<string>();
            for (int col = 1; col <= lastCol; col++)
            {
                string h = ws.Cell(1, col).GetString().Trim();
                headers.Add(string.IsNullOrEmpty(h) ? $"__col{col}" : h);
            }

            // Read data rows
            for (int row = 2; row <= lastRow; row++)
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bool hasData = false;
                for (int col = 0; col < headers.Count; col++)
                {
                    var cell = ws.Cell(row, col + 1);
                    string val = cell.IsEmpty() ? "" : cell.GetString().Trim();
                    dict[headers[col]] = val;
                    if (!string.IsNullOrEmpty(val)) hasData = true;
                }
                if (hasData) rows.Add(dict);
            }

            return rows;
        }

        // Filter rows by Handling selection: Keep PIPE, Keep SPL, or both.
        // Only PIPE and SPL rows are affected — all other component types are always kept.
        private List<Dictionary<string, string>> ApplyHandlingFilter(List<Dictionary<string, string>> rows)
        {
            bool keepPipe = rbKeepPipe.IsChecked == true || rbKeepPipeAndSpl.IsChecked == true;
            bool keepSpl = rbKeepSpl.IsChecked == true || rbKeepPipeAndSpl.IsChecked == true;

            return rows.Where(r =>
            {
                string comp = r.GetValueOrDefault("Component", "").ToUpper();
                if (comp == "PIPE") return keepPipe;
                if (comp == "SPL") return keepSpl;
                return true;
            }).ToList();
        }

        // Roll up GSKT, WAS, HARD, BOLT MHs into BU rows per drawing, proportional to each BU row's original MHs.
        // Hardware from drawings with BU rows gets prorated in. ALL hardware rows are removed afterward,
        // including unclaimed ones from drawings that had no BU rows.
        private static List<Dictionary<string, string>> RollUpBUHardware(List<Dictionary<string, string>> rows)
        {
            var hardwareComponents = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GSKT", "WAS", "HARD", "BOLT" };

            // Group by drawing number
            var drawingGroups = rows.Select((r, i) => (Row: r, Index: i))
                .GroupBy(x => x.Row.GetValueOrDefault("Drawing Number", ""), StringComparer.OrdinalIgnoreCase);

            foreach (var group in drawingGroups)
            {
                var buRows = group.Where(x => x.Row.GetValueOrDefault("Component", "").Equals("BU", StringComparison.OrdinalIgnoreCase)).ToList();
                var hwRows = group.Where(x => hardwareComponents.Contains(x.Row.GetValueOrDefault("Component", ""))).ToList();

                if (buRows.Count == 0 || hwRows.Count == 0) continue;

                double hwTotal = hwRows.Sum(x => ParseDouble(x.Row.GetValueOrDefault("BudgetMHs", "0")));
                if (hwTotal == 0) continue;

                double buTotal = buRows.Sum(x => ParseDouble(x.Row.GetValueOrDefault("BudgetMHs", "0")));
                if (buTotal == 0) continue;

                // Prorate hardware into BU rows
                foreach (var bu in buRows)
                {
                    double originalMHs = ParseDouble(bu.Row.GetValueOrDefault("BudgetMHs", "0"));
                    double proportion = originalMHs / buTotal;
                    double newMHs = originalMHs + (proportion * hwTotal);
                    bu.Row["BudgetMHs"] = NumericHelper.RoundToPlaces(newMHs).ToString();
                }
            }

            // Remove ALL hardware rows (prorated and unclaimed)
            return rows.Where(r => !hardwareComponents.Contains(r.GetValueOrDefault("Component", ""))).ToList();
        }

        // Roll up all ShopField=1 rows per drawing into one Fabrication row, summing BudgetMHs
        private static List<Dictionary<string, string>> RollUpFabPerDwg(List<Dictionary<string, string>> rows)
        {
            var result = new List<Dictionary<string, string>>();
            var rowsToRemove = new HashSet<int>();
            var fabRowsToAdd = new List<(int InsertAfter, Dictionary<string, string> Row)>();

            var drawingGroups = rows.Select((r, i) => (Row: r, Index: i))
                .GroupBy(x => x.Row.GetValueOrDefault("Drawing Number", ""), StringComparer.OrdinalIgnoreCase);

            foreach (var group in drawingGroups)
            {
                var shopRows = group.Where(x => x.Row.GetValueOrDefault("ShopField", "") == "1").ToList();
                if (shopRows.Count == 0) continue;

                double totalMHs = shopRows.Sum(x => ParseDouble(x.Row.GetValueOrDefault("BudgetMHs", "0")));
                string drawingNumber = group.Key;

                // Create rolled-up fabrication row based on the first shop row
                var fabRow = new Dictionary<string, string>(shopRows[0].Row, StringComparer.OrdinalIgnoreCase);
                fabRow["Component"] = "FAB";
                fabRow["Description"] = $"Fabrication for DWG: {drawingNumber}";
                fabRow["BudgetMHs"] = NumericHelper.RoundToPlaces(totalMHs).ToString();
                fabRow["Quantity"] = "1";
                fabRow["ROCStep"] = "FAB";

                // Mark shop rows for removal
                foreach (var sr in shopRows)
                    rowsToRemove.Add(sr.Index);

                // Insert fab row at position of the first removed shop row
                fabRowsToAdd.Add((shopRows[0].Index, fabRow));
            }

            // Build result: insert fab rows at their positions, skip removed rows
            var fabByPosition = fabRowsToAdd.ToDictionary(f => f.InsertAfter, f => f.Row);
            for (int i = 0; i < rows.Count; i++)
            {
                if (fabByPosition.TryGetValue(i, out var fabRow))
                    result.Add(fabRow);
                if (!rowsToRemove.Contains(i))
                    result.Add(rows[i]);
            }

            return result;
        }

        // Map raw Labor rows to Activity objects using the column mapping grid
        private List<Activity> MapToActivities(List<Dictionary<string, string>> rawRows)
        {
            // Build fileHeader → activityProperty map from user selections
            var columnMap = _mappingItems
                .Where(m => m.SelectedMapping != "Unmapped")
                .ToDictionary(m => m.FileHeader, m => m.SelectedMapping, StringComparer.OrdinalIgnoreCase);

            // Cache property info for reflection
            var propCache = typeof(Activity).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            var activities = new List<Activity>();

            foreach (var row in rawRows)
            {
                var activity = new Activity();
                activity.BeginInit();

                foreach (var (fileHeader, actProp) in columnMap)
                {
                    string rawVal = row.GetValueOrDefault(fileHeader, "");
                    if (string.IsNullOrEmpty(rawVal)) continue;

                    if (!propCache.TryGetValue(actProp, out PropertyInfo? prop)) continue;

                    try
                    {
                        if (prop.PropertyType == typeof(double))
                        {
                            double? numVal = ResolveNumericValue(rawVal, actProp);
                            if (numVal != null) prop.SetValue(activity, numVal.Value);
                        }
                        else if (prop.PropertyType == typeof(int))
                        {
                            if (int.TryParse(rawVal, out int intVal))
                                prop.SetValue(activity, intVal);
                        }
                        else if (prop.PropertyType == typeof(string) || prop.PropertyType == typeof(string))
                        {
                            prop.SetValue(activity, rawVal);
                        }
                    }
                    catch { } // Skip individual field errors silently
                }

                activity.EndInit();
                activities.Add(activity);
            }

            return activities;
        }

        // Apply metadata overrides: Enter Value fields set a constant on every row
        private void ApplyMetadata(List<Activity> activities)
        {
            var propCache = typeof(Activity).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            foreach (var meta in _metadataItems)
            {
                if (meta.Mode != "Enter Value") continue;
                if (string.IsNullOrEmpty(meta.EnteredValue)) continue;

                if (!propCache.TryGetValue(meta.FieldName, out PropertyInfo? prop)) continue;

                foreach (var activity in activities)
                {
                    try
                    {
                        if (prop.PropertyType == typeof(double))
                        {
                            if (double.TryParse(meta.EnteredValue, out double dVal))
                                prop.SetValue(activity, dVal);
                        }
                        else if (prop.PropertyType == typeof(int))
                        {
                            if (int.TryParse(meta.EnteredValue, out int iVal))
                                prop.SetValue(activity, iVal);
                        }
                        else
                        {
                            prop.SetValue(activity, meta.EnteredValue);
                        }
                    }
                    catch { }
                }
            }
        }

        // Apply ROC percentage splits if a ROC set is selected.
        // For each activity whose component (UDF6) is in the applicable components list
        // and whose ShopField (UDF1) matches a ROC step's ShopField:
        // - Modify the original row with the first matching ROC step and its percentage of BudgetMHs
        // - Clone additional rows for remaining matching ROC steps
        // Activities whose component isn't in the list or ShopField doesn't match pass through unchanged.
        private async System.Threading.Tasks.Task<List<Activity>> ApplyROCSplitsAsync(List<Activity> activities)
        {
            // Determine selected ROC set
            if (cboROCSet.SelectedIndex <= 1) return activities; // "None" or "+ Create New..."

            int setIdx = cboROCSet.SelectedIndex - 2;
            if (setIdx < 0 || setIdx >= _rocSets.Count) return activities;

            var (projectId, setName) = _rocSets[setIdx];

            var (steps, components) = await System.Threading.Tasks.Task.Run(() =>
                ProjectRateRepository.GetROCSetDataAsync(projectId, setName).Result);

            if (steps.Count == 0 || components.Count == 0) return activities;

            var result = new List<Activity>();

            foreach (var activity in activities)
            {
                string component = activity.UDF6 ?? "";

                // Not in applicable components — pass through unchanged
                if (!components.Contains(component))
                {
                    result.Add(activity);
                    continue;
                }

                // Parse activity ShopField
                int actShopField = 0;
                if (int.TryParse(activity.UDF1, out int sf))
                    actShopField = sf;

                // Find ROC steps matching this row's ShopField
                var matchingSteps = steps.Where(s => s.ShopField == actShopField).ToList();

                if (matchingSteps.Count == 0)
                {
                    // No matching ShopField — pass through unchanged
                    result.Add(activity);
                    continue;
                }

                double originalMHs = activity.BudgetMHs;

                // First matching step modifies the original row
                activity.ROCStep = matchingSteps[0].ROCStep;
                activity.BudgetMHs = NumericHelper.RoundToPlaces(originalMHs * matchingSteps[0].Percentage / 100.0);
                result.Add(activity);

                // Remaining steps create cloned rows
                for (int i = 1; i < matchingSteps.Count; i++)
                {
                    var clone = CloneActivity(activity);
                    clone.ROCStep = matchingSteps[i].ROCStep;
                    clone.BudgetMHs = NumericHelper.RoundToPlaces(originalMHs * matchingSteps[i].Percentage / 100.0);
                    result.Add(clone);
                }
            }

            return result;
        }

        // Shallow clone an Activity for ROC splits
        private static Activity CloneActivity(Activity source)
        {
            var clone = new Activity();
            foreach (var prop in typeof(Activity).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanWrite && prop.CanRead)
                    prop.SetValue(clone, prop.GetValue(source));
            }
            return clone;
        }

        // Generate UniqueIDs and set system tracking fields (matches ExcelImporter pattern)
        private static void SetSystemFields(List<Activity> activities)
        {
            string user = App.CurrentUser?.Username ?? Environment.UserName;
            var timestamp = DateTime.Now.ToString("yyMMddHHmmss");
            var userSuffix = user.Length >= 3
                ? user.Substring(user.Length - 3).ToLower()
                : "usr";
            int sequence = 1;

            foreach (var a in activities)
            {
                a.UniqueID = $"i{timestamp}{sequence}{userSuffix}";
                sequence++;

                a.CreatedBy = user;
                a.AssignedTo = user;
                a.UpdatedBy = user;
                a.UpdatedUtcDate = DateTime.UtcNow;
                a.LocalDirty = 1;

                // Ensure minimum values for required numeric fields
                if (a.Quantity == 0) a.Quantity = 0.001;
                if (a.BudgetMHs == 0) a.BudgetMHs = 0.001;
                if (a.ClientBudget == 0) a.ClientBudget = 0.001;
            }
        }

        // Insert Activity records into local SQLite database
        private static int InsertActivities(List<Activity> activities)
        {
            using var connection = DatabaseSetup.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO Activities (
                    HexNO, ProjectID, Description, UniqueID,
                    Area, SubArea, PjtSystem, PjtSystemNo,
                    CompType, PhaseCategory, ROCStep,
                    AssignedTo, CreatedBy, UpdatedBy,
                    PercentEntry, Quantity, EarnQtyEntry, UOM,
                    BudgetMHs, BudgetHoursGroup, BudgetHoursROC, BaseUnit, EarnedMHsRoc,
                    ROCID, ROCPercent, ROCBudgetQTY,
                    DwgNO, RevNO, SecondDwgNO, ShtNO,
                    TagNO, WorkPackage, PhaseCode, Service, ShopField, SchedActNO, SecondActno,
                    EqmtNO, LineNumber, ChgOrdNO,
                    MtrlSpec, PipeGrade, PaintCode, InsulType, HtTrace,
                    Aux1, Aux2, Aux3, Estimator, RFINO, XRay,
                    EquivQTY, EquivUOM,
                    ClientEquivQty, ClientBudget, ClientCustom3,
                    PrevEarnMHs, PrevEarnQTY,
                    ActStart, ActFin, DateTrigger, Notes,
                    UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
                    UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, RespParty, UDF20,
                    PipeSize1, PipeSize2,
                    UpdatedUtcDate, LocalDirty
                ) VALUES (
                    @HexNO, @ProjectID, @Description, @UniqueID,
                    @Area, @SubArea, @PjtSystem, @PjtSystemNo,
                    @CompType, @PhaseCategory, @ROCStep,
                    @AssignedTo, @CreatedBy, @UpdatedBy,
                    @PercentEntry, @Quantity, @EarnQtyEntry, @UOM,
                    @BudgetMHs, @BudgetHoursGroup, @BudgetHoursROC, @BaseUnit, @EarnedMHsRoc,
                    @ROCID, @ROCPercent, @ROCBudgetQTY,
                    @DwgNO, @RevNO, @SecondDwgNO, @ShtNO,
                    @TagNO, @WorkPackage, @PhaseCode, @Service, @ShopField, @SchedActNO, @SecondActno,
                    @EqmtNO, @LineNumber, @ChgOrdNO,
                    @MtrlSpec, @PipeGrade, @PaintCode, @InsulType, @HtTrace,
                    @Aux1, @Aux2, @Aux3, @Estimator, @RFINO, @XRay,
                    @EquivQTY, @EquivUOM,
                    @ClientEquivQty, @ClientBudget, @ClientCustom3,
                    @PrevEarnMHs, @PrevEarnQTY,
                    @ActStart, @ActFin, @DateTrigger, @Notes,
                    @UDF1, @UDF2, @UDF3, @UDF4, @UDF5, @UDF6, @UDF7, @UDF8, @UDF9, @UDF10,
                    @UDF11, @UDF12, @UDF13, @UDF14, @UDF15, @UDF16, @UDF17, @RespParty, @UDF20,
                    @PipeSize1, @PipeSize2,
                    @UpdatedUtcDate, @LocalDirty
                )";

            // Add all parameters
            cmd.Parameters.Add("@HexNO", SqliteType.Integer);
            cmd.Parameters.Add("@ProjectID", SqliteType.Text);
            cmd.Parameters.Add("@Description", SqliteType.Text);
            cmd.Parameters.Add("@UniqueID", SqliteType.Text);
            cmd.Parameters.Add("@Area", SqliteType.Text);
            cmd.Parameters.Add("@SubArea", SqliteType.Text);
            cmd.Parameters.Add("@PjtSystem", SqliteType.Text);
            cmd.Parameters.Add("@PjtSystemNo", SqliteType.Text);
            cmd.Parameters.Add("@CompType", SqliteType.Text);
            cmd.Parameters.Add("@PhaseCategory", SqliteType.Text);
            cmd.Parameters.Add("@ROCStep", SqliteType.Text);
            cmd.Parameters.Add("@AssignedTo", SqliteType.Text);
            cmd.Parameters.Add("@CreatedBy", SqliteType.Text);
            cmd.Parameters.Add("@UpdatedBy", SqliteType.Text);
            cmd.Parameters.Add("@PercentEntry", SqliteType.Real);
            cmd.Parameters.Add("@Quantity", SqliteType.Real);
            cmd.Parameters.Add("@EarnQtyEntry", SqliteType.Real);
            cmd.Parameters.Add("@UOM", SqliteType.Text);
            cmd.Parameters.Add("@BudgetMHs", SqliteType.Real);
            cmd.Parameters.Add("@BudgetHoursGroup", SqliteType.Real);
            cmd.Parameters.Add("@BudgetHoursROC", SqliteType.Real);
            cmd.Parameters.Add("@BaseUnit", SqliteType.Real);
            cmd.Parameters.Add("@EarnedMHsRoc", SqliteType.Real);
            cmd.Parameters.Add("@ROCID", SqliteType.Integer);
            cmd.Parameters.Add("@ROCPercent", SqliteType.Real);
            cmd.Parameters.Add("@ROCBudgetQTY", SqliteType.Real);
            cmd.Parameters.Add("@DwgNO", SqliteType.Text);
            cmd.Parameters.Add("@RevNO", SqliteType.Text);
            cmd.Parameters.Add("@SecondDwgNO", SqliteType.Text);
            cmd.Parameters.Add("@ShtNO", SqliteType.Text);
            cmd.Parameters.Add("@TagNO", SqliteType.Text);
            cmd.Parameters.Add("@WorkPackage", SqliteType.Text);
            cmd.Parameters.Add("@PhaseCode", SqliteType.Text);
            cmd.Parameters.Add("@Service", SqliteType.Text);
            cmd.Parameters.Add("@ShopField", SqliteType.Text);
            cmd.Parameters.Add("@SchedActNO", SqliteType.Text);
            cmd.Parameters.Add("@SecondActno", SqliteType.Text);
            cmd.Parameters.Add("@EqmtNO", SqliteType.Text);
            cmd.Parameters.Add("@LineNumber", SqliteType.Text);
            cmd.Parameters.Add("@ChgOrdNO", SqliteType.Text);
            cmd.Parameters.Add("@MtrlSpec", SqliteType.Text);
            cmd.Parameters.Add("@PipeGrade", SqliteType.Text);
            cmd.Parameters.Add("@PaintCode", SqliteType.Text);
            cmd.Parameters.Add("@InsulType", SqliteType.Text);
            cmd.Parameters.Add("@HtTrace", SqliteType.Text);
            cmd.Parameters.Add("@Aux1", SqliteType.Text);
            cmd.Parameters.Add("@Aux2", SqliteType.Text);
            cmd.Parameters.Add("@Aux3", SqliteType.Text);
            cmd.Parameters.Add("@Estimator", SqliteType.Text);
            cmd.Parameters.Add("@RFINO", SqliteType.Text);
            cmd.Parameters.Add("@XRay", SqliteType.Integer);
            cmd.Parameters.Add("@EquivQTY", SqliteType.Text);
            cmd.Parameters.Add("@EquivUOM", SqliteType.Text);
            cmd.Parameters.Add("@ClientEquivQty", SqliteType.Real);
            cmd.Parameters.Add("@ClientBudget", SqliteType.Real);
            cmd.Parameters.Add("@ClientCustom3", SqliteType.Real);
            cmd.Parameters.Add("@PrevEarnMHs", SqliteType.Real);
            cmd.Parameters.Add("@PrevEarnQTY", SqliteType.Real);
            cmd.Parameters.Add("@ActStart", SqliteType.Text);
            cmd.Parameters.Add("@ActFin", SqliteType.Text);
            cmd.Parameters.Add("@DateTrigger", SqliteType.Integer);
            cmd.Parameters.Add("@Notes", SqliteType.Text);
            cmd.Parameters.Add("@UDF1", SqliteType.Text);
            cmd.Parameters.Add("@UDF2", SqliteType.Text);
            cmd.Parameters.Add("@UDF3", SqliteType.Text);
            cmd.Parameters.Add("@UDF4", SqliteType.Text);
            cmd.Parameters.Add("@UDF5", SqliteType.Text);
            cmd.Parameters.Add("@UDF6", SqliteType.Text);
            cmd.Parameters.Add("@UDF7", SqliteType.Text);
            cmd.Parameters.Add("@UDF8", SqliteType.Text);
            cmd.Parameters.Add("@UDF9", SqliteType.Text);
            cmd.Parameters.Add("@UDF10", SqliteType.Text);
            cmd.Parameters.Add("@UDF11", SqliteType.Text);
            cmd.Parameters.Add("@UDF12", SqliteType.Text);
            cmd.Parameters.Add("@UDF13", SqliteType.Text);
            cmd.Parameters.Add("@UDF14", SqliteType.Text);
            cmd.Parameters.Add("@UDF15", SqliteType.Text);
            cmd.Parameters.Add("@UDF16", SqliteType.Text);
            cmd.Parameters.Add("@UDF17", SqliteType.Text);
            cmd.Parameters.Add("@RespParty", SqliteType.Text);
            cmd.Parameters.Add("@UDF20", SqliteType.Text);
            cmd.Parameters.Add("@PipeSize1", SqliteType.Real);
            cmd.Parameters.Add("@PipeSize2", SqliteType.Real);
            cmd.Parameters.Add("@UpdatedUtcDate", SqliteType.Text);
            cmd.Parameters.Add("@LocalDirty", SqliteType.Integer);
            cmd.Prepare();

            int count = 0;
            foreach (var a in activities)
            {
                cmd.Parameters["@HexNO"].Value = a.HexNO;
                cmd.Parameters["@ProjectID"].Value = a.ProjectID ?? "";
                cmd.Parameters["@Description"].Value = a.Description ?? "";
                cmd.Parameters["@UniqueID"].Value = a.UniqueID;
                cmd.Parameters["@Area"].Value = a.Area ?? "";
                cmd.Parameters["@SubArea"].Value = a.SubArea ?? "";
                cmd.Parameters["@PjtSystem"].Value = a.PjtSystem ?? "";
                cmd.Parameters["@PjtSystemNo"].Value = a.PjtSystemNo ?? "";
                cmd.Parameters["@CompType"].Value = a.CompType ?? "";
                cmd.Parameters["@PhaseCategory"].Value = a.PhaseCategory ?? "";
                cmd.Parameters["@ROCStep"].Value = a.ROCStep ?? "";
                cmd.Parameters["@AssignedTo"].Value = a.AssignedTo ?? "";
                cmd.Parameters["@CreatedBy"].Value = a.CreatedBy ?? "";
                cmd.Parameters["@UpdatedBy"].Value = a.UpdatedBy ?? "";
                cmd.Parameters["@PercentEntry"].Value = a.PercentEntry;
                cmd.Parameters["@Quantity"].Value = a.Quantity;
                cmd.Parameters["@EarnQtyEntry"].Value = a.EarnQtyEntry;
                cmd.Parameters["@UOM"].Value = a.UOM ?? "";
                cmd.Parameters["@BudgetMHs"].Value = a.BudgetMHs;
                cmd.Parameters["@BudgetHoursGroup"].Value = a.BudgetHoursGroup;
                cmd.Parameters["@BudgetHoursROC"].Value = a.BudgetHoursROC;
                cmd.Parameters["@BaseUnit"].Value = a.BaseUnit;
                cmd.Parameters["@EarnedMHsRoc"].Value = a.EarnedMHsRoc;
                cmd.Parameters["@ROCID"].Value = a.ROCID;
                cmd.Parameters["@ROCPercent"].Value = a.ROCPercent;
                cmd.Parameters["@ROCBudgetQTY"].Value = a.ROCBudgetQTY;
                cmd.Parameters["@DwgNO"].Value = a.DwgNO ?? "";
                cmd.Parameters["@RevNO"].Value = a.RevNO ?? "";
                cmd.Parameters["@SecondDwgNO"].Value = a.SecondDwgNO ?? "";
                cmd.Parameters["@ShtNO"].Value = a.ShtNO ?? "";
                cmd.Parameters["@TagNO"].Value = a.TagNO ?? "";
                cmd.Parameters["@WorkPackage"].Value = a.WorkPackage ?? "";
                cmd.Parameters["@PhaseCode"].Value = a.PhaseCode ?? "";
                cmd.Parameters["@Service"].Value = a.Service ?? "";
                cmd.Parameters["@ShopField"].Value = a.ShopField ?? "";
                cmd.Parameters["@SchedActNO"].Value = a.SchedActNO ?? "";
                cmd.Parameters["@SecondActno"].Value = a.SecondActno ?? "";
                cmd.Parameters["@EqmtNO"].Value = a.EqmtNO ?? "";
                cmd.Parameters["@LineNumber"].Value = a.LineNumber ?? "";
                cmd.Parameters["@ChgOrdNO"].Value = a.ChgOrdNO ?? "";
                cmd.Parameters["@MtrlSpec"].Value = a.MtrlSpec ?? "";
                cmd.Parameters["@PipeGrade"].Value = a.PipeGrade ?? "";
                cmd.Parameters["@PaintCode"].Value = a.PaintCode ?? "";
                cmd.Parameters["@InsulType"].Value = a.InsulType ?? "";
                cmd.Parameters["@HtTrace"].Value = a.HtTrace ?? "";
                cmd.Parameters["@Aux1"].Value = a.Aux1 ?? "";
                cmd.Parameters["@Aux2"].Value = a.Aux2 ?? "";
                cmd.Parameters["@Aux3"].Value = a.Aux3 ?? "";
                cmd.Parameters["@Estimator"].Value = a.Estimator ?? "";
                cmd.Parameters["@RFINO"].Value = a.RFINO ?? "";
                cmd.Parameters["@XRay"].Value = a.XRay;
                cmd.Parameters["@EquivQTY"].Value = a.EquivQTY;
                cmd.Parameters["@EquivUOM"].Value = a.EquivUOM ?? "";
                cmd.Parameters["@ClientEquivQty"].Value = a.ClientEquivQty;
                cmd.Parameters["@ClientBudget"].Value = a.ClientBudget;
                cmd.Parameters["@ClientCustom3"].Value = a.ClientCustom3;
                cmd.Parameters["@PrevEarnMHs"].Value = a.PrevEarnMHs;
                cmd.Parameters["@PrevEarnQTY"].Value = a.PrevEarnQTY;
                cmd.Parameters["@ActStart"].Value = a.ActStart?.ToString("yyyy-MM-dd") ?? "";
                cmd.Parameters["@ActFin"].Value = a.ActFin?.ToString("yyyy-MM-dd") ?? "";
                cmd.Parameters["@DateTrigger"].Value = a.DateTrigger;
                cmd.Parameters["@Notes"].Value = a.Notes ?? "";
                cmd.Parameters["@UDF1"].Value = a.UDF1 ?? "";
                cmd.Parameters["@UDF2"].Value = a.UDF2 ?? "";
                cmd.Parameters["@UDF3"].Value = a.UDF3 ?? "";
                cmd.Parameters["@UDF4"].Value = a.UDF4 ?? "";
                cmd.Parameters["@UDF5"].Value = a.UDF5 ?? "";
                cmd.Parameters["@UDF6"].Value = a.UDF6 ?? "";
                cmd.Parameters["@UDF7"].Value = a.UDF7;
                cmd.Parameters["@UDF8"].Value = a.UDF8 ?? "";
                cmd.Parameters["@UDF9"].Value = a.UDF9 ?? "";
                cmd.Parameters["@UDF10"].Value = a.UDF10 ?? "";
                cmd.Parameters["@UDF11"].Value = a.UDF11 ?? "";
                cmd.Parameters["@UDF12"].Value = a.UDF12 ?? "";
                cmd.Parameters["@UDF13"].Value = a.UDF13 ?? "";
                cmd.Parameters["@UDF14"].Value = a.UDF14 ?? "";
                cmd.Parameters["@UDF15"].Value = a.UDF15 ?? "";
                cmd.Parameters["@UDF16"].Value = a.UDF16 ?? "";
                cmd.Parameters["@UDF17"].Value = a.UDF17 ?? "";
                cmd.Parameters["@RespParty"].Value = a.RespParty ?? "";
                cmd.Parameters["@UDF20"].Value = a.UDF20 ?? "";
                cmd.Parameters["@PipeSize1"].Value = a.PipeSize1;
                cmd.Parameters["@PipeSize2"].Value = a.PipeSize2;
                cmd.Parameters["@UpdatedUtcDate"].Value = a.UpdatedUtcDate?.ToString("o") ?? "";
                cmd.Parameters["@LocalDirty"].Value = a.LocalDirty;

                cmd.ExecuteNonQuery();
                count++;
            }

            transaction.Commit();
            return count;
        }

        // Create Vantage-formatted Excel file from Activity list at the specified path
        private static void CreateVantageExcel(List<Activity> activities, string filePath)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Activities");

            // Cache property info
            var propCache = typeof(Activity).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            // Write headers
            for (int col = 0; col < VantageExcelColumns.Length; col++)
                ws.Cell(1, col + 1).Value = VantageExcelColumns[col];

            // Style header row
            var headerRange = ws.Range(1, 1, 1, VantageExcelColumns.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#DAEEF3");

            // Write data rows
            for (int row = 0; row < activities.Count; row++)
            {
                var activity = activities[row];
                for (int col = 0; col < VantageExcelColumns.Length; col++)
                {
                    string colName = VantageExcelColumns[col];
                    if (!propCache.TryGetValue(colName, out PropertyInfo? prop)) continue;

                    object? val = prop.GetValue(activity);
                    if (val == null) continue;

                    if (val is DateTime dt)
                        ws.Cell(row + 2, col + 1).Value = dt.ToString("yyyy-MM-dd");
                    else if (val is double d)
                        ws.Cell(row + 2, col + 1).Value = d;
                    else if (val is int i)
                        ws.Cell(row + 2, col + 1).Value = i;
                    else if (val is long l)
                        ws.Cell(row + 2, col + 1).Value = l;
                    else
                        ws.Cell(row + 2, col + 1).Value = val.ToString() ?? "";
                }
            }

            ws.Columns().AdjustToContents(1, 100);
            ws.SheetView.FreezeRows(1);
            workbook.SaveAs(filePath);
        }

        // Helper: parse string to double, defaulting to 0
        private static double ParseDouble(string val)
        {
            return double.TryParse(val, out double result) ? result : 0;
        }

        // Populate metadata combobox with plain string items and restore bound selection
        private void MetadataComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox combo && combo.Items.Count == 0)
            {
                combo.Items.Add("Enter Value");
                combo.Items.Add("Use Source");

                // Re-apply the bound Mode value now that items exist
                if (combo.DataContext is MetadataFieldItem item)
                    combo.SelectedItem = item.Mode;
            }
        }

        // ========================================
        // IMPORT PROFILE MANAGEMENT
        // ========================================

        // Load saved profile names into the dropdown
        private void LoadProfileDropdown()
        {
            _isLoadingProfile = true;
            cboProfile.Items.Clear();
            cboProfile.Items.Add("(None)");

            var names = GetProfileNames();
            foreach (var name in names)
                cboProfile.Items.Add(name);

            cboProfile.SelectedIndex = 0;
            btnDeleteProfile.IsEnabled = false;
            _isLoadingProfile = false;
        }

        // Get saved profile names from settings
        private static List<string> GetProfileNames()
        {
            string json = SettingsManager.GetUserSetting(ProfileIndexKey);
            if (string.IsNullOrEmpty(json)) return new List<string>();
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
            catch { return new List<string>(); }
        }

        // Save profile names index to settings
        private static void SaveProfileNames(List<string> names)
        {
            SettingsManager.SetUserSetting(ProfileIndexKey, JsonSerializer.Serialize(names), "json");
        }

        // Build an ImportProfile from current dialog state
        private ImportProfile BuildProfileFromUI()
        {
            var profile = new ImportProfile();

            // Output mode
            if (rbImportRecords.IsChecked == true) profile.OutputMode = "ImportRecords";
            else if (rbCreateExcel.IsChecked == true) profile.OutputMode = "CreateExcel";
            else if (rbImportAndExcel.IsChecked == true) profile.OutputMode = "ImportAndExcel";

            // Handling
            if (rbKeepPipe.IsChecked == true) profile.HandlingMode = "KeepPipe";
            else if (rbKeepSpl.IsChecked == true) profile.HandlingMode = "KeepSpl";
            else if (rbKeepPipeAndSpl.IsChecked == true) profile.HandlingMode = "KeepPipeAndSpl";

            // Options
            profile.RollUpBUHardware = chkRollUpBUHardware.IsChecked == true;
            profile.RollUpFabPerDwg = chkRollUpFabPerDwg.IsChecked == true;

            // ROC Set
            profile.ROCSetSelection = cboROCSet.SelectedItem?.ToString() ?? "None";

            // Column mappings
            foreach (var item in _mappingItems)
            {
                profile.ColumnMappings.Add(new ColumnMappingEntry
                {
                    FileHeader = item.FileHeader,
                    SelectedMapping = item.SelectedMapping
                });
            }

            // Metadata
            foreach (var meta in _metadataItems)
            {
                profile.MetadataFields.Add(new MetadataEntry
                {
                    FieldName = meta.FieldName,
                    Mode = meta.Mode,
                    EnteredValue = meta.EnteredValue
                });
            }

            return profile;
        }

        // Apply a loaded profile to the dialog controls
        private void ApplyProfileToUI(ImportProfile profile)
        {
            _isLoadingProfile = true;

            // Output mode
            rbImportRecords.IsChecked = profile.OutputMode == "ImportRecords";
            rbCreateExcel.IsChecked = profile.OutputMode == "CreateExcel";
            rbImportAndExcel.IsChecked = profile.OutputMode == "ImportAndExcel";

            // Handling
            rbKeepPipe.IsChecked = profile.HandlingMode == "KeepPipe";
            rbKeepSpl.IsChecked = profile.HandlingMode == "KeepSpl";
            rbKeepPipeAndSpl.IsChecked = profile.HandlingMode == "KeepPipeAndSpl";

            // Options
            chkRollUpBUHardware.IsChecked = profile.RollUpBUHardware;
            chkRollUpFabPerDwg.IsChecked = profile.RollUpFabPerDwg;

            // ROC Set — find matching item in dropdown
            for (int i = 0; i < cboROCSet.Items.Count; i++)
            {
                if (string.Equals(cboROCSet.Items[i]?.ToString(), profile.ROCSetSelection, StringComparison.OrdinalIgnoreCase))
                {
                    _isLoadingDropdowns = true;
                    cboROCSet.SelectedIndex = i;
                    _isLoadingDropdowns = false;
                    break;
                }
            }

            // Column mappings — apply saved mappings to matching file headers
            if (profile.ColumnMappings.Count > 0)
            {
                var savedMap = profile.ColumnMappings.ToDictionary(
                    c => c.FileHeader, c => c.SelectedMapping, StringComparer.OrdinalIgnoreCase);

                foreach (var item in _mappingItems)
                {
                    if (savedMap.TryGetValue(item.FileHeader, out string? mapping))
                    {
                        if (item.AvailableMappings.Contains(mapping))
                            item.SelectedMapping = mapping;
                    }
                }
            }

            // Metadata
            if (profile.MetadataFields.Count > 0)
            {
                var savedMeta = profile.MetadataFields.ToDictionary(
                    m => m.FieldName, m => m, StringComparer.OrdinalIgnoreCase);

                foreach (var meta in _metadataItems)
                {
                    if (savedMeta.TryGetValue(meta.FieldName, out var saved))
                    {
                        meta.Mode = saved.Mode;
                        meta.EnteredValue = saved.EnteredValue;
                    }
                }
            }

            _isLoadingProfile = false;
        }

        // Profile dropdown selection changed — load the selected profile
        private void CboProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingProfile || cboProfile.SelectedIndex < 0) return;

            if (cboProfile.SelectedIndex == 0)
            {
                _activeProfile = null;
                btnDeleteProfile.IsEnabled = false;
                return;
            }

            string profileName = cboProfile.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(profileName)) return;

            btnDeleteProfile.IsEnabled = true;

            string json = SettingsManager.GetUserSetting($"{ProfileDataPrefix}{profileName}");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var profile = JsonSerializer.Deserialize<ImportProfile>(json);
                if (profile != null)
                {
                    _activeProfile = profile;
                    ApplyProfileToUI(profile);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ImportTakeoffDialog.CboProfile_SelectionChanged");
            }
        }

        // Save current settings as a named profile
        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            // If a profile is selected, default to overwriting it
            string defaultName = cboProfile.SelectedIndex > 0
                ? cboProfile.SelectedItem?.ToString() ?? ""
                : "";

            var inputDialog = new InputDialog("Save Import Profile", "Profile name:", defaultName)
            {
                Owner = this
            };

            if (inputDialog.ShowDialog() != true) return;

            string name = inputDialog.InputText.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Profile name cannot be empty.", "Invalid Name",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var profile = BuildProfileFromUI();
            profile.Name = name;

            string json = JsonSerializer.Serialize(profile);
            SettingsManager.SetUserSetting($"{ProfileDataPrefix}{name}", json, "json");

            // Update index
            var names = GetProfileNames();
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
                names.Add(name);
            SaveProfileNames(names);

            // Refresh dropdown and select the saved profile
            _isLoadingProfile = true;
            cboProfile.Items.Clear();
            cboProfile.Items.Add("(None)");
            foreach (var n in names)
                cboProfile.Items.Add(n);

            for (int i = 0; i < cboProfile.Items.Count; i++)
            {
                if (string.Equals(cboProfile.Items[i]?.ToString(), name, StringComparison.OrdinalIgnoreCase))
                {
                    cboProfile.SelectedIndex = i;
                    break;
                }
            }
            btnDeleteProfile.IsEnabled = cboProfile.SelectedIndex > 0;
            _isLoadingProfile = false;
        }

        // Delete the selected profile
        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (cboProfile.SelectedIndex <= 0) return;

            string name = cboProfile.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(name)) return;

            var result = MessageBox.Show($"Delete profile \"{name}\"?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            SettingsManager.RemoveUserSetting($"{ProfileDataPrefix}{name}");

            var names = GetProfileNames();
            names.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            SaveProfileNames(names);

            LoadProfileDropdown();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
