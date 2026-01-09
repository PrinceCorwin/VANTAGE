using System.Windows;
using VANTAGE.Models;

namespace VANTAGE.Dialogs
{
    // Dialog for selecting the type of form template to create
    public partial class TemplateTypeDialog : Window
    {
        public string? SelectedType { get; private set; }

        public TemplateTypeDialog()
        {
            InitializeComponent();
        }

        private void BtnCover_Click(object sender, RoutedEventArgs e)
        {
            SelectedType = TemplateTypes.Cover;
            DialogResult = true;
            Close();
        }

        private void BtnList_Click(object sender, RoutedEventArgs e)
        {
            SelectedType = TemplateTypes.List;
            DialogResult = true;
            Close();
        }

        private void BtnGrid_Click(object sender, RoutedEventArgs e)
        {
            SelectedType = TemplateTypes.Grid;
            DialogResult = true;
            Close();
        }

        private void BtnForm_Click(object sender, RoutedEventArgs e)
        {
            SelectedType = TemplateTypes.Form;
            DialogResult = true;
            Close();
        }
    }
}
