using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using VANTAGE.Utilities;

namespace VANTAGE.ViewModels
{
    public class SidePanelViewModel : INotifyPropertyChanged
    {
        // ========================================
        // CONSTANTS
        // ========================================
        private const double DefaultWidth = 400;
        private const double MinWidth = 300;
        private const double MaxWidthRatio = 0.5; // 50% of window max

        // ========================================
        // FIELDS
        // ========================================
        private bool _isOpen;
        private double _panelWidth;
        private string _activeTab;
        private string _currentHelpAnchor;
        private string _currentModuleDisplayName;
        private string _helpHtmlPath = null!;

        // ========================================
        // CONSTRUCTOR
        // ========================================
        public SidePanelViewModel()
        {
            _isOpen = false;
            _panelWidth = DefaultWidth;
            _activeTab = "Help";
            _currentHelpAnchor = "getting-started";
            _currentModuleDisplayName = "Getting Started";

            // Build path to help HTML file
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            _helpHtmlPath = Path.Combine(appDir, "Help", "manual.html");

            // Load saved preferences
            LoadUserPreferences();
        }

        // ========================================
        // PROPERTIES
        // ========================================
        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (_isOpen != value)
                {
                    _isOpen = value;
                    OnPropertyChanged(nameof(IsOpen));
                    OnPropertyChanged(nameof(ContentColumnWidth));
                    OnPropertyChanged(nameof(SidebarColumnWidth));
                    OnPropertyChanged(nameof(SplitterWidth));
                    SaveUserPreferences();
                }
            }
        }

        public double PanelWidth
        {
            get => _panelWidth;
            set
            {
                double clamped = Math.Max(MinWidth, value);
                if (Math.Abs(_panelWidth - clamped) > 0.1)
                {
                    _panelWidth = clamped;
                    OnPropertyChanged(nameof(PanelWidth));
                    OnPropertyChanged(nameof(SidebarColumnWidth));
                    SaveUserPreferences();
                }
            }
        }

        public string ActiveTab
        {
            get => _activeTab;
            set
            {
                if (_activeTab != value)
                {
                    _activeTab = value;
                    OnPropertyChanged(nameof(ActiveTab));
                    OnPropertyChanged(nameof(IsHelpTabActive));
                    OnPropertyChanged(nameof(IsAiTabActive));
                    SaveUserPreferences();
                }
            }
        }

        public bool IsHelpTabActive => ActiveTab == "Help";
        public bool IsAiTabActive => ActiveTab == "AI";

        public string CurrentHelpAnchor
        {
            get => _currentHelpAnchor;
            set
            {
                if (_currentHelpAnchor != value)
                {
                    _currentHelpAnchor = value;
                    OnPropertyChanged(nameof(CurrentHelpAnchor));
                    OnPropertyChanged(nameof(HelpNavigationUrl));
                }
            }
        }

        public string CurrentModuleDisplayName
        {
            get => _currentModuleDisplayName;
            set
            {
                if (_currentModuleDisplayName != value)
                {
                    _currentModuleDisplayName = value;
                    OnPropertyChanged(nameof(CurrentModuleDisplayName));
                }
            }
        }

        // URL for WebView2 navigation
        public string HelpNavigationUrl
        {
            get
            {
                if (!File.Exists(_helpHtmlPath))
                {
                    AppLogger.Warning($"Help file not found: {_helpHtmlPath}", "SidePanelViewModel.HelpNavigationUrl");
                    return "about:blank";
                }

                string anchor = string.IsNullOrEmpty(_currentHelpAnchor) ? "" : $"#{_currentHelpAnchor}";
                return $"file:///{_helpHtmlPath.Replace('\\', '/')}{anchor}";
            }
        }

        // Grid column widths for MainWindow binding
        public string ContentColumnWidth => "*";
        public string SidebarColumnWidth => IsOpen ? PanelWidth.ToString() : "0";
        public double SplitterWidth => IsOpen ? 5 : 0;

        // ========================================
        // METHODS
        // ========================================
        public void Toggle()
        {
            IsOpen = !IsOpen;
        }

        public void Open()
        {
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void ShowHelp(string anchor, string displayName)
        {
            CurrentHelpAnchor = anchor;
            CurrentModuleDisplayName = displayName;
            ActiveTab = "Help";
            Open();
        }

        public void ShowAiAssistant()
        {
            ActiveTab = "AI";
            Open();
        }

        public void SetActiveTab(string tab)
        {
            if (tab == "Help" || tab == "AI")
            {
                ActiveTab = tab;
            }
        }

        // ========================================
        // PERSISTENCE
        // ========================================
        private void LoadUserPreferences()
        {
            try
            {
                if (App.CurrentUser == null) return;

                string widthStr = SettingsManager.GetUserSetting( "SidePanel.Width", DefaultWidth.ToString());
                if (double.TryParse(widthStr, out double width))
                {
                    _panelWidth = Math.Max(MinWidth, width);
                }

                string tabStr = SettingsManager.GetUserSetting( "SidePanel.ActiveTab", "Help");
                _activeTab = tabStr == "AI" ? "AI" : "Help";

                // Don't restore IsOpen - always start closed
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SidePanelViewModel.LoadUserPreferences");
            }
        }

        private void SaveUserPreferences()
        {
            try
            {
                if (App.CurrentUser == null) return;

                SettingsManager.SetUserSetting( "SidePanel.Width", _panelWidth.ToString(), "string");
                SettingsManager.SetUserSetting( "SidePanel.ActiveTab", _activeTab, "string");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "SidePanelViewModel.SaveUserPreferences");
            }
        }

        // ========================================
        // INotifyPropertyChanged
        // ========================================
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}