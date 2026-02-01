using System;
using System.ComponentModel;
using System.IO;
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
        private readonly string _helpHtmlPath = null!;

        // Search fields
        private string _searchText = string.Empty;
        private int _matchCount;
        private int _currentMatchIndex;

        // ========================================
        // CONSTRUCTOR
        // ========================================
        public SidePanelViewModel()
        {
            _isOpen = false;
            _panelWidth = DefaultWidth;
            _activeTab = "Help";

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

                // Use virtual host mapping so images load correctly
                return "https://help.local/manual.html";
            }
        }

        // Grid column widths for MainWindow binding
        public static string ContentColumnWidth => "*";
        public string SidebarColumnWidth => IsOpen ? PanelWidth.ToString() : "0";
        public double SplitterWidth => IsOpen ? 5 : 0;

        // ========================================
        // SEARCH PROPERTIES
        // ========================================
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    OnPropertyChanged(nameof(HasSearchText));
                }
            }
        }

        public bool HasSearchText => !string.IsNullOrEmpty(_searchText);

        public int MatchCount
        {
            get => _matchCount;
            set
            {
                if (_matchCount != value)
                {
                    _matchCount = value;
                    OnPropertyChanged(nameof(MatchCount));
                    OnPropertyChanged(nameof(HasMatches));
                    OnPropertyChanged(nameof(MatchCountDisplay));
                }
            }
        }

        public int CurrentMatchIndex
        {
            get => _currentMatchIndex;
            set
            {
                if (_currentMatchIndex != value)
                {
                    _currentMatchIndex = value;
                    OnPropertyChanged(nameof(CurrentMatchIndex));
                    OnPropertyChanged(nameof(MatchCountDisplay));
                }
            }
        }

        public bool HasMatches => _matchCount > 0;

        public string MatchCountDisplay
        {
            get
            {
                if (!HasSearchText) return string.Empty;
                if (_matchCount == 0) return "No results";
                return $"{_currentMatchIndex} of {_matchCount}";
            }
        }

        // ========================================
        // SEARCH METHODS
        // ========================================
        public void ClearSearch()
        {
            SearchText = string.Empty;
            MatchCount = 0;
            CurrentMatchIndex = 0;
        }

        public void UpdateMatchInfo(int matchCount, int currentIndex)
        {
            _matchCount = matchCount;
            _currentMatchIndex = currentIndex;
            OnPropertyChanged(nameof(MatchCount));
            OnPropertyChanged(nameof(CurrentMatchIndex));
            OnPropertyChanged(nameof(HasMatches));
            OnPropertyChanged(nameof(MatchCountDisplay));
        }

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

        public void ShowHelp()
        {
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

                string widthStr = SettingsManager.GetUserSetting("SidePanel.Width", DefaultWidth.ToString());
                if (double.TryParse(widthStr, out double width))
                {
                    _panelWidth = Math.Max(MinWidth, width);
                }

                string tabStr = SettingsManager.GetUserSetting("SidePanel.ActiveTab", "Help");
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

                SettingsManager.SetUserSetting("SidePanel.Width", _panelWidth.ToString(), "string");
                SettingsManager.SetUserSetting("SidePanel.ActiveTab", _activeTab, "string");
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