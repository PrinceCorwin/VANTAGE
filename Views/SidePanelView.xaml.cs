using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using VANTAGE.Utilities;
using VANTAGE.ViewModels;

namespace VANTAGE.Views
{
    public partial class SidePanelView : UserControl
    {
        private SidePanelViewModel? _viewModel;
        private bool _webViewInitialized = false;

        public SidePanelView()
        {
            InitializeComponent();
            this.Loaded += SidePanelView_Loaded;
            this.DataContextChanged += SidePanelView_DataContextChanged;
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
                UpdateContextHeader();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SidePanelViewModel.ActiveTab):
                    UpdateTabVisuals();
                    break;
                case nameof(SidePanelViewModel.CurrentModuleDisplayName):
                    UpdateContextHeader();
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

            // Update Help tab button
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
            }
        }

        private void UpdateContextHeader()
        {
            if (_viewModel == null) return;
            txtContextHeader.Text = _viewModel.CurrentModuleDisplayName;
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

        // Public method for external navigation requests
        public void NavigateToSection(string anchor)
        {
            if (_viewModel != null && _webViewInitialized)
            {
                _viewModel.CurrentHelpAnchor = anchor;
            }
        }
    }
}