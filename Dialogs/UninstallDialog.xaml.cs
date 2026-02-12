using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Confirmation dialog for the --uninstall flow
    public partial class UninstallDialog : Window
    {
        public bool KeepData => chkKeepData.IsChecked == true;

        public UninstallDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
