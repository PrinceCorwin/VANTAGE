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
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("rendered", out var r)
                    && r.ValueKind == JsonValueKind.True)
                {
                    RevealDashboard();
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
                    UDF2 = a.UDF2,
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
            public string? UDF2 { get; set; }
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
