using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Verification dialog shown before the VP vs Vtg file picker.
    // Reminds the user to trim the JC Labor Productivity report and
    // offers a "Do not show again" checkbox that writes to UserSettings.
    public partial class VPvsVtgPrepDialog : Window
    {
        public const string SkipSettingName = "SkipVPvsVtgPrepDialog";

        public bool DoNotShowAgain => chkDoNotShowAgain.IsChecked == true;

        public VPvsVtgPrepDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (DoNotShowAgain)
            {
                SettingsManager.SetUserSetting(SkipSettingName, "true", "bool");
            }
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
