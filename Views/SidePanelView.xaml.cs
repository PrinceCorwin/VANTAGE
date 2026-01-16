using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using VANTAGE.Utilities;
using VANTAGE.ViewModels;

namespace VANTAGE.Views
{
    public partial class SidePanelView : UserControl
    {
        private SidePanelViewModel? _viewModel;
        private bool _webViewInitialized = false;
        private DispatcherTimer? _searchDebounceTimer;
        private CoreWebView2FindOptions? _findOptions;

        public SidePanelView()
        {
            InitializeComponent();
            this.Loaded += SidePanelView_Loaded;
            this.DataContextChanged += SidePanelView_DataContextChanged;
            InitializeSearchDebounce();
        }

        private void SidePanelView_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeWebView();
        }

        private void SidePanelView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is SidePanelViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is SidePanelViewModel newVm)
            {
                _viewModel = newVm;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                UpdateTabVisuals();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SidePanelViewModel.ActiveTab):
                    UpdateTabVisuals();
                    break;
                case nameof(SidePanelViewModel.HelpNavigationUrl):
                    NavigateToHelp();
                    break;
            }
        }

        private async void InitializeWebView()
        {
            if (_webViewInitialized) return;

            try
            {
                helpOverlay.Visibility = Visibility.Visible;
                txtHelpStatus.Text = "Initializing help viewer...";

                // Initialize WebView2 with user data folder in AppData
                string userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MILESTONE",
                    "WebView2");

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webViewHelp.EnsureCoreWebView2Async(env);

                // Configure WebView2 settings
                webViewHelp.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webViewHelp.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webViewHelp.CoreWebView2.Settings.IsZoomControlEnabled = true;

                // Set dark background to match app theme
                webViewHelp.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30);

                // Create reusable find options
                _findOptions = webViewHelp.CoreWebView2.Environment.CreateFindOptions();
                _findOptions.SuppressDefaultFindDialog = true;
                _findOptions.ShouldHighlightAllMatches = true;

                // Subscribe to find events for match count updates
                webViewHelp.CoreWebView2.Find.MatchCountChanged += Find_MatchCountChanged;
                webViewHelp.CoreWebView2.Find.ActiveMatchIndexChanged += Find_ActiveMatchIndexChanged;

                _webViewInitialized = true;
                helpOverlay.Visibility = Visibility.Collapsed;

                // Navigate to initial help content
                NavigateToHelp();

                AppLogger.Info("WebView2 initialized successfully", "SidePanelView.InitializeWebView");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SidePanelView.InitializeWebView");
                txtHelpStatus.Text = "Failed to load help viewer.\nPlease ensure WebView2 Runtime is installed.";
            }
        }

        private void Find_MatchCountChanged(object? sender, object e)
        {
            if (_viewModel == null || !_webViewInitialized) return;

            int matchCount = (int)webViewHelp.CoreWebView2.Find.MatchCount;
            int activeIndex = (int)webViewHelp.CoreWebView2.Find.ActiveMatchIndex;

            // ActiveMatchIndex is 0-based, display as 1-based
            _viewModel.UpdateMatchInfo(matchCount, matchCount > 0 ? activeIndex + 1 : 0);
            UpdateSearchButtonStates();
        }

        private void Find_ActiveMatchIndexChanged(object? sender, object e)
        {
            if (_viewModel == null || !_webViewInitialized) return;

            int activeIndex = (int)webViewHelp.CoreWebView2.Find.ActiveMatchIndex;

            // ActiveMatchIndex is 0-based, display as 1-based
            if (activeIndex >= 0)
            {
                _viewModel.CurrentMatchIndex = activeIndex + 1;
            }
        }

        private void NavigateToHelp()
        {
            if (!_webViewInitialized || _viewModel == null) return;

            try
            {
                string url = _viewModel.HelpNavigationUrl;
                if (!string.IsNullOrEmpty(url) && url != "about:blank")
                {
                    webViewHelp.CoreWebView2.Navigate(url);
                }
                else
                {
                    // Show placeholder if help file doesn't exist
                    string placeholderHtml = @"
                        <html>
                        <head>
                            <style>
                                body { 
                                    font-family: 'Segoe UI', sans-serif; 
                                    background-color: #1e1e1e; 
                                    color: #ffffff; 
                                    padding: 20px;
                                    margin: 0;
                                }
                                h1 { color: #0078d4; }
                                p { color: #aaaaaa; }
                            </style>
                        </head>
                        <body>
                            <h1>Help Content</h1>
                            <p>Help documentation is not yet available.</p>
                            <p>The help manual will appear here once created.</p>
                        </body>
                        </html>";
                    webViewHelp.CoreWebView2.NavigateToString(placeholderHtml);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SidePanelView.NavigateToHelp");
            }
        }

        private void UpdateTabVisuals()
        {
            if (_viewModel == null) return;

            if (_viewModel.IsHelpTabActive)
            {
                btnHelpTab.Background = (Brush)FindResource("AccentColor");
                btnHelpTab.Foreground = Brushes.White;
                btnHelpTab.BorderThickness = new Thickness(0);

                btnAiTab.Background = Brushes.Transparent;
                btnAiTab.Foreground = (Brush)FindResource("ForegroundColor");
                btnAiTab.BorderThickness = new Thickness(1);

                gridHelpContent.Visibility = Visibility.Visible;
                gridAiContent.Visibility = Visibility.Collapsed;
                searchFieldRow.Visibility = Visibility.Visible;
            }
            else
            {
                btnAiTab.Background = (Brush)FindResource("AccentColor");
                btnAiTab.Foreground = Brushes.White;
                btnAiTab.BorderThickness = new Thickness(0);

                btnHelpTab.Background = Brushes.Transparent;
                btnHelpTab.Foreground = (Brush)FindResource("ForegroundColor");
                btnHelpTab.BorderThickness = new Thickness(1);

                gridHelpContent.Visibility = Visibility.Collapsed;
                gridAiContent.Visibility = Visibility.Visible;
                searchFieldRow.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnHelpTab_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.SetActiveTab("Help");
        }

        private void BtnAiTab_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.SetActiveTab("AI");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.Close();
        }

        // ========================================
        // ACTION BUTTONS
        // ========================================

        private void BtnBackToTop_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // TODO: Scroll WebView2 to top
        }

        private void BtnPrintPdf_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // TODO: Print help content to PDF
        }

        private void BtnViewInBrowser_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // TODO: Open help HTML in default browser
        }

        // ========================================
        // SEARCH FUNCTIONALITY
        // ========================================

        private void InitializeSearchDebounce()
        {
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Reset and start debounce timer
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer?.Start();
        }

        private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _searchDebounceTimer?.Stop();
            ExecuteSearch();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Stop debounce timer and act immediately
                _searchDebounceTimer?.Stop();

                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    FindPrevious();
                }
                else
                {
                    // If no search has been done yet, execute search first
                    if (_viewModel != null && !_viewModel.HasMatches && !string.IsNullOrEmpty(txtSearch.Text))
                    {
                        ExecuteSearch();
                    }
                    else
                    {
                        FindNext();
                    }
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ClearSearch();
                e.Handled = true;
            }
        }

        private void BtnSearchPrev_Click(object sender, RoutedEventArgs e)
        {
            FindPrevious();
        }

        private void BtnSearchNext_Click(object sender, RoutedEventArgs e)
        {
            FindNext();
        }

        private async void ExecuteSearch()
        {
            if (!_webViewInitialized || _viewModel == null || _findOptions == null) return;

            string searchText = txtSearch.Text.Trim();
            _viewModel.SearchText = searchText;

            if (string.IsNullOrEmpty(searchText))
            {
                StopSearch();
                return;
            }

            try
            {
                // Stop any existing search first
                webViewHelp.CoreWebView2.Find.Stop();

                // Configure find options
                _findOptions.FindTerm = searchText;
                _findOptions.IsCaseSensitive = false;
                _findOptions.ShouldMatchWord = false;

                // Start the find session
                await webViewHelp.CoreWebView2.Find.StartAsync(_findOptions);

                // Match count will be updated via MatchCountChanged event
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SidePanelView.ExecuteSearch");
                _viewModel.UpdateMatchInfo(0, 0);
                UpdateSearchButtonStates();
            }
        }

        private void FindNext()
        {
            if (!_webViewInitialized || _viewModel == null || !_viewModel.HasMatches) return;

            try
            {
                webViewHelp.CoreWebView2.Find.FindNext();
                // Index will be updated via ActiveMatchIndexChanged event
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SidePanelView.FindNext");
            }
        }

        private void FindPrevious()
        {
            if (!_webViewInitialized || _viewModel == null || !_viewModel.HasMatches) return;

            try
            {
                webViewHelp.CoreWebView2.Find.FindPrevious();
                // Index will be updated via ActiveMatchIndexChanged event
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SidePanelView.FindPrevious");
            }
        }

        private void StopSearch()
        {
            if (!_webViewInitialized) return;

            try
            {
                webViewHelp.CoreWebView2.Find.Stop();
                _viewModel?.ClearSearch();
                UpdateSearchButtonStates();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SidePanelView.StopSearch");
            }
        }

        private void ClearSearch()
        {
            txtSearch.Text = string.Empty;
            StopSearch();
        }

        private void UpdateSearchButtonStates()
        {
            bool hasMatches = _viewModel?.HasMatches ?? false;
            btnSearchPrev.IsEnabled = hasMatches;
            btnSearchNext.IsEnabled = hasMatches;
        }
    }
}