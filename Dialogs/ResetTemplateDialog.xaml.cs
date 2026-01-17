using System.Collections.Generic;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Models;

namespace VANTAGE.Dialogs
{
    // Dialog for selecting a built-in template to reset to
    public partial class ResetTemplateDialog : Window
    {
        public FormTemplate? SelectedTemplate => cboTemplates.SelectedItem as FormTemplate;

        public ResetTemplateDialog(List<FormTemplate> builtInTemplates, string? promptText = null)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme("FluentDark"));

            cboTemplates.ItemsSource = builtInTemplates;

            if (builtInTemplates.Count > 0)
            {
                cboTemplates.SelectedIndex = 0;
            }

            if (!string.IsNullOrEmpty(promptText))
            {
                txtPrompt.Text = promptText;
            }

            Loaded += (s, e) => cboTemplates.Focus();
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (cboTemplates.SelectedItem == null)
            {
                MessageBox.Show("Please select a template.", "Selection Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
