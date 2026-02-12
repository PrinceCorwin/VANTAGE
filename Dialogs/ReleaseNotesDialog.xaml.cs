using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Dialog showing release notes history for all published versions
    public partial class ReleaseNotesDialog : Window
    {
        public ReleaseNotesDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            Loaded += ReleaseNotesDialog_Loaded;
        }

        private void ReleaseNotesDialog_Loaded(object sender, RoutedEventArgs e)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            txtVersion.Text = $"Current version: {version?.Major}.{version?.Minor}.{version?.Build}";
            LoadReleaseNotes();
        }

        // Load and display release notes from the bundled JSON file
        private void LoadReleaseNotes()
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = Path.Combine(appDir, "ReleaseNotes.json");

                if (!File.Exists(filePath))
                {
                    return;
                }

                string json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<ReleaseNotesData>(json);

                if (data?.Releases != null)
                {
                    icReleases.ItemsSource = data.Releases;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ReleaseNotesDialog.LoadReleaseNotes");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
