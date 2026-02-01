using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Dialog for prompting user to enter a template name with duplicate validation
    public partial class TemplateNameDialog : Window
    {
        private readonly List<string> _existingNames;

        public string TemplateName => txtName.Text.Trim();

        public TemplateNameDialog(string defaultName, List<string> existingNames, string? promptText = null)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            _existingNames = existingNames ?? new List<string>();
            txtName.Text = defaultName;

            if (!string.IsNullOrEmpty(promptText))
            {
                txtPrompt.Text = promptText;
            }

            // Select all text for easy replacement
            Loaded += (s, e) =>
            {
                txtName.Focus();
                txtName.SelectAll();
            };
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text.Trim();

            // Validate not empty
            if (string.IsNullOrWhiteSpace(name))
            {
                txtError.Text = "Please enter a template name.";
                txtError.Visibility = Visibility.Visible;
                txtName.Focus();
                return;
            }

            // Check for duplicate
            if (_existingNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                txtError.Text = $"A template named '{name}' already exists. Please choose a different name.";
                txtError.Visibility = Visibility.Visible;
                txtName.Focus();
                txtName.SelectAll();
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
