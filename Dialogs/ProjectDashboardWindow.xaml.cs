using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Syncfusion.SfSkinManager;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Standalone window hosting the Progress Dashboard (Dashboards/vantage-dashboard.html)
    // in a WebView2. The dashboard is a self-contained vanilla-JS page; this window feeds it
    // the current local activities via CoreWebView2.PostWebMessageAsJson and reserves a top
    // toolbar for future controls.
    public partial class ProjectDashboardWindow : Window
    {
        private bool _webViewInitialized;

        // Whether the dashboard is in customize (edit-layout) mode. The button toggles it and
        // posts a setCustomize message to the page; the page posts customizeExited when its
        // own "Done" button is used, so the button label stays in sync.
        private bool _customizeMode;

        // Id of the layout currently shown ("default" = the page's built-in Default). User
        // layouts are persisted in UserSettings via SettingsManager; the page authors the JSON.
        private string _currentLayoutId = "default";
        private bool _suppressComboEvent;

        // Resolved when the page confirms it has re-rendered in print mode (rail hidden), before PrintToPdf.
        private TaskCompletionSource<bool>? _printReadyTcs;

        // Combo item: displays Name, carries the layout Id.
        private sealed class LayoutItem
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public override string ToString() => Name;
        }

        // Last-built activities array JSON (bare array). Reused by Open in Browser so the
        // snapshot opens with the same dataset without re-querying the database.
        private string _activitiesArrayJson = "[]";

        // Last-built ProjectID -> Description map JSON. Feeds the header subtitle
        // ("ProjectID: Description"). Reused by Open in Browser.
        private string _projectsJson = "{}";

        // Hides the dashboard's built-in "Import weekly file" control. Injected as a global
        // CSS rule (not an element removal) so it survives the page's header re-renders.
        // The dashboard is fed from the local database, so the file-import control is redundant.
        private const string HideImportScript =
            "(function(){var s=document.createElement('style');s.textContent='[data-import]{display:none !important;}';document.head.appendChild(s);})();";

        public ProjectDashboardWindow()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += ProjectDashboardWindow_Loaded;
        }

        private async void ProjectDashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            if (_webViewInitialized) return;

            try
            {
                // Reuse the same WebView2 user-data folder as the help sidebar.
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MILESTONE",
                    "WebView2");

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webView.EnsureCoreWebView2Async(env);

                // Map the app base directory to a virtual host so the dashboard html loads locally.
                string appBaseFolder = AppDomain.CurrentDomain.BaseDirectory;
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "dashboard.local", appBaseFolder, CoreWebView2HostResourceAccessKind.Allow);

                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = true;
                // Match the dashboard's cream body so there's no white/black flash before first paint.
                webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 0xFA, 0xF9, 0xF5);

                webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                _webViewInitialized = true;

                string htmlPath = Path.Combine(appBaseFolder, "Dashboards", "vantage-dashboard.html");
                if (!File.Exists(htmlPath))
                {
                    ShowOverlayError("Dashboard file not found.");
                    txtStatus.Text = "Dashboard file not found.";
                    AppLogger.Error(new FileNotFoundException("Dashboard html missing", htmlPath),
                        "ProjectDashboardWindow.InitializeWebViewAsync");
                    return;
                }

                webView.CoreWebView2.Navigate("https://dashboard.local/Dashboards/vantage-dashboard.html");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProjectDashboardWindow.InitializeWebViewAsync");
                ShowOverlayError("Failed to load dashboard viewer.");
                txtStatus.Text = "Failed to load dashboard viewer.";
                AppMessageBox.Show(
                    "Failed to initialize the dashboard viewer.\nPlease ensure the WebView2 Runtime is installed.",
                    "Dashboard Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess) return;
            await webView.CoreWebView2.ExecuteScriptAsync(HideImportScript);
            // Send the last-used layout before the data so the report opens on it (no Default flash).
            InitializeLayoutSelection();
            txtLoadingPhase.Text = "Loading records…";
            await PushDataAsync();
        }

        // Query all local activities, project to the dashboard schema, and inject into the page.
        // The overlay stays up until the page confirms it has rendered (CoreWebView2_WebMessageReceived).
        private async Task PushDataAsync()
        {
            try
            {
                var (activities, _) = await ActivityRepository.GetAllActivitiesAsync();

                _activitiesArrayJson = BuildActivitiesArrayJson(activities);
                _projectsJson = BuildProjectsJson(activities);

                // The page listens for e.data.activities (+ optional e.data.projects).
                string message = "{\"activities\":" + _activitiesArrayJson
                    + ",\"projects\":" + _projectsJson + "}";
                webView.CoreWebView2.PostWebMessageAsJson(message);

                txtStatus.Text = $"{activities.Count:N0} activities • local database";

                // Safety net: reveal even if the render confirmation never arrives.
                _ = RevealFallbackAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProjectDashboardWindow.PushDataAsync");
                ShowOverlayError("Error loading activities — see log.");
                txtStatus.Text = "Error loading activities — see log.";
            }
        }

        // The page posts { "rendered": true } once loadVantageData finishes painting the DOM.
        // Only then do we reveal the WebView2 — so Chromium's intermediate paint states are never seen.
        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return;

                if (root.TryGetProperty("rendered", out var r) && r.ValueKind == JsonValueKind.True)
                {
                    RevealDashboard();
                    return;
                }

                if (root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    switch (t.GetString())
                    {
                        case "customizeExited":
                            // The page's own "Done" button was used — resync the toolbar button.
                            _customizeMode = false;
                            UpdateCustomizeButton();
                            break;
                        case "saveLayout":
                            HandleSaveLayout(root);
                            break;
                        case "deleteLayout":
                            if (root.TryGetProperty("id", out var delId) && delId.ValueKind == JsonValueKind.String)
                                HandleDeleteLayout(delId.GetString() ?? "");
                            break;
                        case "printReady":
                            _printReadyTcs?.TrySetResult(true);
                            break;
                        case "publishLayout":
                            HandlePublishLayout(root);
                            break;
                        case "importLayouts":
                            _ = OpenImportDialogAsync();
                            break;
                        case "cz":
                            // Manager-toolbar actions. "pdf" exports; publish/import have their own message types.
                            if (root.TryGetProperty("action", out var act) && act.ValueKind == JsonValueKind.String && act.GetString() == "pdf")
                                _ = ExportPdfAsync();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProjectDashboardWindow.CoreWebView2_WebMessageReceived");
            }
        }

        // Swap the loading overlay for the fully-rendered dashboard. Idempotent.
        private void RevealDashboard()
        {
            busyIndicator.IsBusy = false;
            loadingOverlay.Visibility = Visibility.Collapsed;
            webView.Visibility = Visibility.Visible;
        }

        // Defensive: never leave the overlay covering the dashboard forever if the
        // render confirmation is somehow not delivered.
        private async Task RevealFallbackAsync()
        {
            await Task.Delay(10000);
            if (loadingOverlay.Visibility == Visibility.Visible)
                RevealDashboard();
        }

        // Stop the spinner and show an error message on the overlay (kept visible over the hidden webview).
        private void ShowOverlayError(string message)
        {
            busyIndicator.IsBusy = false;
            txtLoadingPhase.Text = message;
        }

        // Serialize activities to the exact field names the dashboard normalizer expects.
        private static string BuildActivitiesArrayJson(List<Activity> activities)
        {
            var dtos = new List<ActivityDto>(activities.Count);
            foreach (var a in activities)
            {
                dtos.Add(new ActivityDto
                {
                    ProjectID = a.ProjectID,
                    Status = a.Status,
                    CompType = a.CompType,
                    PhaseCategory = a.PhaseCategory,
                    PhaseCode = a.PhaseCode,
                    ROCStep = a.ROCStep,
                    SchedActNO = a.SchedActNO,
                    Area = a.Area,
                    UDF1 = a.UDF1,
                    UDF2 = a.UDF2,
                    UDF3 = a.UDF3,
                    UDF4 = a.UDF4,
                    UDF5 = a.UDF5,
                    UDF6 = a.UDF6,
                    UDF7 = a.UDF7,
                    UDF8 = a.UDF8,
                    UDF9 = a.UDF9,
                    UDF10 = a.UDF10,
                    UDF11 = a.UDF11,
                    UDF12 = a.UDF12,
                    UDF13 = a.UDF13,
                    UDF14 = a.UDF14,
                    UDF15 = a.UDF15,
                    UDF16 = a.UDF16,
                    UDF17 = a.UDF17,
                    UDF20 = a.UDF20,
                    EarnQtyEntry = a.EarnQtyEntry,
                    ShopField = a.ShopField,
                    WorkPackage = a.WorkPackage,
                    Aux1 = a.Aux1,
                    Aux2 = a.Aux2,
                    Aux3 = a.Aux3,
                    Description = a.Description,
                    TagNO = a.TagNO,
                    LineNumber = a.LineNumber,
                    DwgNO = a.DwgNO,
                    PipeSize1 = a.PipeSize1,
                    UOM = a.UOM,
                    BudgetMHs = a.BudgetMHs,
                    EarnMHsCalc = a.EarnMHsCalc,
                    Quantity = a.Quantity,
                    ClientBudget = a.ClientBudget,
                    PercentEntry = a.PercentEntry,
                    PercentCompleteCalc = a.PercentCompleteCalc,
                    PlanStart = FormatDate(a.PlanStart),
                    PlanFin = FormatDate(a.PlanFin),
                    ActStart = FormatDate(a.ActStart),
                    ActFin = FormatDate(a.ActFin)
                });
            }
            return JsonSerializer.Serialize(dtos);
        }

        private static string? FormatDate(DateTime? d) => d?.ToString("yyyy-MM-dd");

        // Distinct ProjectID -> Description map (descriptions from the Projects table cache).
        private static string BuildProjectsJson(List<Activity> activities)
        {
            var map = new Dictionary<string, string>();
            foreach (var a in activities)
            {
                string? id = a.ProjectID;
                if (string.IsNullOrWhiteSpace(id) || map.ContainsKey(id)) continue;
                map[id] = ProjectCache.GetProjectDescription(id);
            }
            return JsonSerializer.Serialize(map);
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!_webViewInitialized) return;
            txtStatus.Text = "Refreshing…";
            await PushDataAsync();
        }

        // Toggle the page into / out of customize (edit-layout) mode.
        private void BtnCustomize_Click(object sender, RoutedEventArgs e)
        {
            if (!_webViewInitialized) return;
            _customizeMode = !_customizeMode;
            UpdateCustomizeButton();
            string msg = "{\"type\":\"setCustomize\",\"on\":" + (_customizeMode ? "true" : "false") + "}";
            webView.CoreWebView2.PostWebMessageAsJson(msg);
        }

        // Populate the layout combo (Default + saved user layouts) and select the current id.
        private void PopulateLayoutCombo()
        {
            _suppressComboEvent = true;
            try
            {
                cboLayout.Items.Clear();
                cboLayout.Items.Add(new LayoutItem { Id = "default", Name = "Default" });
                foreach (var r in SettingsManager.GetReportLayoutList())
                    cboLayout.Items.Add(new LayoutItem { Id = r.Id, Name = r.Name });

                cboLayout.SelectedItem = null;
                foreach (LayoutItem it in cboLayout.Items)
                {
                    if (it.Id == _currentLayoutId) { cboLayout.SelectedItem = it; break; }
                }
                if (cboLayout.SelectedItem == null && cboLayout.Items.Count > 0)
                    cboLayout.SelectedIndex = 0;
            }
            finally { _suppressComboEvent = false; }
            UpdateDeleteButtonState();
        }

        // The Delete button applies to saved user layouts only — never the built-in Default.
        private void UpdateDeleteButtonState()
        {
            btnDeleteLayout.IsEnabled = cboLayout.SelectedItem is LayoutItem it && it.Id != "default";
        }

        // Pick the initial layout from last-used (falling back to Default) and send it to the page.
        private void InitializeLayoutSelection()
        {
            string lastUsed = SettingsManager.GetLastUsedReportLayout();
            bool exists = lastUsed == "default" || SettingsManager.GetReportLayoutList().Exists(r => r.Id == lastUsed);
            _currentLayoutId = string.IsNullOrWhiteSpace(lastUsed) || !exists ? "default" : lastUsed;
            PopulateLayoutCombo();
            PostSetLayout(_currentLayoutId);
        }

        // Send a layout to the page. "default" -> null (the page renders its built-in DEFAULT_LAYOUT).
        private void PostSetLayout(string id)
        {
            string layoutJson = "null";
            if (id != "default")
            {
                var json = SettingsManager.GetReportLayoutJson(id);
                if (!string.IsNullOrWhiteSpace(json)) layoutJson = json;
            }
            webView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"setLayout\",\"layout\":" + layoutJson + "}");
        }

        private void CboLayout_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressComboEvent || !_webViewInitialized) return;
            if (cboLayout.SelectedItem is not LayoutItem it) return;
            // Switching layout leaves customize mode; any unsaved edits to the previous layout are dropped.
            if (_customizeMode)
            {
                _customizeMode = false;
                UpdateCustomizeButton();
                webView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"setCustomize\",\"on\":false}");
            }
            _currentLayoutId = it.Id;
            SettingsManager.SetLastUsedReportLayout(it.Id);
            PostSetLayout(it.Id);
            UpdateDeleteButtonState();
        }

        // Delete the selected saved layout from local storage (toolbar Delete button).
        private void BtnDeleteLayout_Click(object sender, RoutedEventArgs e)
        {
            if (cboLayout.SelectedItem is not LayoutItem it || it.Id == "default") return;
            var result = AppMessageBox.Show($"Delete the layout \"{it.Name}\"?\n\nThis removes it from this computer and can't be undone.",
                "Delete layout", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            HandleDeleteLayout(it.Id);
        }

        // Persist a layout the page saved (Save / Save as). Never overwrites the built-in Default.
        private void HandleSaveLayout(JsonElement root)
        {
            if (!root.TryGetProperty("layout", out var lay) || lay.ValueKind != JsonValueKind.Object) return;
            string id = lay.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? (i.GetString() ?? "") : "";
            string name = lay.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? (n.GetString() ?? "Untitled") : "Untitled";
            if (string.IsNullOrWhiteSpace(id) || id == "default") return;

            SettingsManager.SaveReportLayout(id, name, lay.GetRawText());
            _currentLayoutId = id;
            SettingsManager.SetLastUsedReportLayout(id);
            PopulateLayoutCombo();
            txtStatus.Text = $"Saved layout: {name}";
        }

        private void HandleDeleteLayout(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "default") return;
            SettingsManager.DeleteReportLayout(id);
            if (_currentLayoutId == id)
            {
                _currentLayoutId = "default";
                SettingsManager.SetLastUsedReportLayout(string.Empty);
                PostSetLayout("default");
            }
            PopulateLayoutCombo();
            txtStatus.Text = "Layout deleted.";
        }

        // Publish the current layout to the shared Cloud library (dbo.VMS_ReportLayouts). Upserts by name+author.
        private void HandlePublishLayout(JsonElement root)
        {
            if (!root.TryGetProperty("layout", out var lay) || lay.ValueKind != JsonValueKind.Object) return;
            string name = lay.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? (n.GetString() ?? "Untitled") : "Untitled";
            int ver = 1;
            if (lay.TryGetProperty("schemaVersion", out var sv) && sv.ValueKind == JsonValueKind.Number) ver = sv.GetInt32();
            _ = PublishToCloudAsync(name, ver, lay.GetRawText());
        }

        private async Task PublishToCloudAsync(string name, int schemaVersion, string layoutJson)
        {
            // Unwind the WebView2 callback before any synchronous CheckConnection / modal MessageBox
            // (same WebView2 reentrancy hazard as OpenImportDialogAsync).
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);

            if (!AzureDbManager.CheckConnection(out _))
            {
                AppMessageBox.Show("Cannot connect to the cloud database. Check your connection and try again.",
                    "Publish to Cloud", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string author = App.CurrentUser?.Username ?? "Unknown";
            try
            {
                txtStatus.Text = $"Publishing \"{name}\"…";
                await AzureReportLayoutRepository.PublishAsync(name, author, schemaVersion, layoutJson);
                AppLogger.Info($"Published report layout '{name}'", "ProjectDashboardWindow.PublishToCloudAsync", author);
                txtStatus.Text = $"Published \"{name}\" to Cloud.";
                AppMessageBox.Show($"Published \"{name}\" to the Cloud library.\n\nTeammates can now Import it.",
                    "Publish to Cloud", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProjectDashboardWindow.PublishToCloudAsync");
                txtStatus.Text = "Publish failed — see log.";
                AppMessageBox.Show("Could not publish to the Cloud — see log.",
                    "Publish to Cloud", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Browse the shared Cloud library and import a one-time local copy of a layout.
        private async Task OpenImportDialogAsync()
        {
            // Let the WebView2 WebMessageReceived callback fully unwind before opening any modal
            // dialog. Calling ShowDialog() (a nested message loop) synchronously inside that native
            // callback — especially while the page is still rendering — intermittently crashes the
            // WebView2 runtime with an access violation that no managed catch can trap.
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);

            try
            {
                if (!AzureDbManager.CheckConnection(out _))
                {
                    AppMessageBox.Show("Cannot connect to the cloud database. Check your connection and try again.",
                        "Import from Cloud", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dlg = new ReportLayoutImportDialog { Owner = this };
                if (dlg.ShowDialog() != true || dlg.SelectedLayoutId is not Guid cloudId) return;

                string? json = await AzureReportLayoutRepository.GetJsonAsync(cloudId);
                if (string.IsNullOrWhiteSpace(json))
                {
                    AppMessageBox.Show("That layout could not be downloaded — it may have just been deleted.",
                        "Import from Cloud", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Re-home the imported layout as a fresh local user layout: new id, unlocked, keep the name.
                var node = System.Text.Json.Nodes.JsonNode.Parse(json);
                string name = node?["name"]?.GetValue<string>() ?? "Imported layout";
                string localId = "L" + Guid.NewGuid().ToString("N").Substring(0, 12);
                if (node is System.Text.Json.Nodes.JsonObject obj)
                {
                    obj["id"] = localId;
                    obj["locked"] = false;
                }
                string localJson = node!.ToJsonString();

                SettingsManager.SaveReportLayout(localId, name, localJson);
                _currentLayoutId = localId;
                SettingsManager.SetLastUsedReportLayout(localId);
                PopulateLayoutCombo();
                PostSetLayout(localId);
                txtStatus.Text = $"Imported \"{name}\" from the Cloud.";
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProjectDashboardWindow.OpenImportDialogAsync");
                AppMessageBox.Show("Could not import the layout — see log.",
                    "Import from Cloud", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Reflect the current mode on the toolbar button (accent navy while editing).
        private void UpdateCustomizeButton()
        {
            btnCustomize.Content = _customizeMode ? "Done" : "Customize";
            if (_customizeMode)
            {
                btnCustomize.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x1E, 0x1B, 0x6B));
                btnCustomize.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                btnCustomize.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "ControlBackground");
                btnCustomize.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "ForegroundColor");
            }
        }

        private async void BtnExportPdf_Click(object sender, RoutedEventArgs e) => await ExportPdfAsync();

        // Export the report to a landscape PDF. Hides the filter rail for the render, then restores it.
        private async Task ExportPdfAsync()
        {
            if (!_webViewInitialized) return;
            try
            {
                // Switch the page to print mode (report only) and wait for it to re-render.
                _printReadyTcs = new TaskCompletionSource<bool>();
                webView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"setPrintMode\",\"on\":true}");
                await Task.WhenAny(_printReadyTcs.Task, Task.Delay(3000));

                // Filename patterned per project.
                string projPart = "Report";
                try
                {
                    using var pd = JsonDocument.Parse(_projectsJson);
                    var ids = new List<string>();
                    foreach (var p in pd.RootElement.EnumerateObject()) ids.Add(p.Name);
                    projPart = ids.Count == 1 ? ids[0] : (ids.Count > 1 ? "MultiProject" : "Report");
                }
                catch { /* keep default */ }
                projPart = string.Join("_", projPart.Split(Path.GetInvalidFileNameChars()));

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Dashboard as PDF",
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    FileName = $"ProjectDashboard_{projPart}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = ".pdf"
                };
                bool saved = saveDialog.ShowDialog() == true;

                if (saved)
                {
                    var settings = webView.CoreWebView2.Environment.CreatePrintSettings();
                    settings.Orientation = CoreWebView2PrintOrientation.Landscape;
                    settings.ShouldPrintBackgrounds = true;
                    await webView.CoreWebView2.PrintToPdfAsync(saveDialog.FileName, settings);
                }

                webView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"setPrintMode\",\"on\":false}");

                if (saved)
                {
                    var result = AppMessageBox.Show(
                        $"PDF saved to:\n{saveDialog.FileName}\n\nOpen now?",
                        "PDF Saved", MessageBoxButton.YesNo, MessageBoxImage.None);
                    if (result == MessageBoxResult.Yes)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = saveDialog.FileName, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProjectDashboardWindow.ExportPdfAsync");
                try { webView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"setPrintMode\",\"on\":false}"); } catch { }
                AppMessageBox.Show("Could not export the PDF — see log.", "Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Write a standalone snapshot (current dataset baked in) and open it in the default browser.
        private void BtnOpenInBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string appBaseFolder = AppDomain.CurrentDomain.BaseDirectory;
                string htmlPath = Path.Combine(appBaseFolder, "Dashboards", "vantage-dashboard.html");
                if (!File.Exists(htmlPath))
                {
                    AppMessageBox.Show("Dashboard file not found.", "Dashboard",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string html = File.ReadAllText(htmlPath);

                // Bake the current dataset in so the standalone file opens with live data,
                // and hide the redundant file-import control in the snapshot too.
                string inject = "<style>[data-import]{display:none !important;}</style>"
                    + "<script>window.__VANTAGE_DATA__ = " + _activitiesArrayJson + ";"
                    + "window.__VANTAGE_PROJECTS__ = " + _projectsJson + ";</script>";
                int bodyClose = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                html = bodyClose >= 0 ? html.Insert(bodyClose, inject) : html + inject;

                string outPath = Path.Combine(Path.GetTempPath(),
                    $"VantageDashboard_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                File.WriteAllText(outPath, html);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = outPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ProjectDashboardWindow.BtnOpenInBrowser_Click");
                AppMessageBox.Show("Could not open the dashboard in your browser — see log.",
                    "Dashboard", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Minimal projection matching the dashboard's expected column names.
        private sealed class ActivityDto
        {
            public string? ProjectID { get; set; }
            public string? Status { get; set; }
            public string? CompType { get; set; }
            public string? PhaseCategory { get; set; }
            public string? PhaseCode { get; set; }
            public string? ROCStep { get; set; }
            public string? SchedActNO { get; set; }
            public string? Area { get; set; }
            public string? UDF1 { get; set; }
            public string? UDF2 { get; set; }
            public string? UDF3 { get; set; }
            public string? UDF4 { get; set; }
            public string? UDF5 { get; set; }
            public string? UDF6 { get; set; }
            public int UDF7 { get; set; }
            public string? UDF8 { get; set; }
            public string? UDF9 { get; set; }
            public string? UDF10 { get; set; }
            public string? UDF11 { get; set; }
            public string? UDF12 { get; set; }
            public string? UDF13 { get; set; }
            public string? UDF14 { get; set; }
            public string? UDF15 { get; set; }
            public string? UDF16 { get; set; }
            public string? UDF17 { get; set; }
            public string? UDF20 { get; set; }
            public double EarnQtyEntry { get; set; }
            public string? ShopField { get; set; }
            public string? WorkPackage { get; set; }
            public string? Aux1 { get; set; }
            public string? Aux2 { get; set; }
            public string? Aux3 { get; set; }
            public string? Description { get; set; }
            public string? TagNO { get; set; }
            public string? LineNumber { get; set; }
            public string? DwgNO { get; set; }
            public double PipeSize1 { get; set; }
            public string? UOM { get; set; }
            public double BudgetMHs { get; set; }
            public double EarnMHsCalc { get; set; }
            public double Quantity { get; set; }
            public double ClientBudget { get; set; }
            public double PercentEntry { get; set; }
            public double PercentCompleteCalc { get; set; }
            public string? PlanStart { get; set; }
            public string? PlanFin { get; set; }
            public string? ActStart { get; set; }
            public string? ActFin { get; set; }
        }
    }
}
