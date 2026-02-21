using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Dialog for selecting the application theme
    public partial class ThemeManagerDialog : Window
    {
        private bool _initialized;

        public ThemeManagerDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            // Set initial radio button state based on saved theme
            if (ThemeManager.CurrentTheme == "Light")
                rbLight.IsChecked = true;
            else if (ThemeManager.CurrentTheme == "Orchid")
                rbOrchid.IsChecked = true;
            else
                rbDark.IsChecked = true;

            _initialized = true;
        }

        // Apply and save selected theme when a radio button is checked
        private void RbTheme_Checked(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;

            string selectedTheme = rbOrchid.IsChecked == true ? "Orchid"
                : rbLight.IsChecked == true ? "Light" : "Dark";

            if (selectedTheme != ThemeManager.CurrentTheme)
            {
                ThemeManager.ApplyTheme(selectedTheme);
                ThemeManager.SaveTheme(selectedTheme);

                // Re-apply Syncfusion skin to this dialog after dictionary swap
                SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
