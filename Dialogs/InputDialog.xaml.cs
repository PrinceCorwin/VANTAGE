using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Simple dialog for prompting user to enter text
    public partial class InputDialog : Window
    {
        public string InputText => txtInput.Text.Trim();

        public InputDialog(string title, string prompt, string? defaultValue = null)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            Title = title;
            txtPrompt.Text = prompt;

            if (!string.IsNullOrEmpty(defaultValue))
                txtInput.Text = defaultValue;

            // Select all text for easy replacement
            Loaded += (s, e) =>
            {
                txtInput.Focus();
                txtInput.SelectAll();
            };
        }

        // Show dialog with owner window
        public bool? ShowDialog(Window owner)
        {
            Owner = owner;
            return ShowDialog();
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
