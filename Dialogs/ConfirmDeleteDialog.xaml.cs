using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Confirmation dialog that requires typing "DELETE" to proceed
    public partial class ConfirmDeleteDialog : Window
    {
        private const string ConfirmText = "DELETE";

        public ConfirmDeleteDialog(string message)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            txtMessage.Text = message;
            Loaded += (s, e) => txtConfirm.Focus();
        }

        private void TxtConfirm_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            btnConfirm.IsEnabled = txtConfirm.Text.Trim().ToUpper() == ConfirmText;
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
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
