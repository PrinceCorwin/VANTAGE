using System.Collections.Generic;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ProjectSpecificFunctionsDialog : Window
    {
        private readonly List<ProjectSpecificFunctionItem> _functions = new();

        public ProjectSpecificFunctionsDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            LoadFunctions();
        }

        private void LoadFunctions()
        {
            _functions.Clear();

            _functions.Add(new ProjectSpecificFunctionItem
            {
                Project = "Fluor T&M 25.005",
                Description = "Update Pipe Support Fab",
                FunctionKey = "fluor_tm_25005_update_pipe_support_fab"
            });

            sfFunctions.ItemsSource = _functions;
            txtNoFunctions.Visibility = _functions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            btnRun.IsEnabled = sfFunctions.SelectedItem is ProjectSpecificFunctionItem;
            txtSelectionSummary.Text = $"{_functions.Count} function(s) available";
        }

        private void SfFunctions_SelectionChanged(object sender, Syncfusion.UI.Xaml.Grid.GridSelectionChangedEventArgs e)
        {
            btnRun.IsEnabled = sfFunctions.SelectedItem is ProjectSpecificFunctionItem;
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (sfFunctions.SelectedItem is not ProjectSpecificFunctionItem selectedFunction)
            {
                MessageBox.Show("Select a function first.", "Run Function",
                    MessageBoxButton.OK, MessageBoxImage.None);
                return;
            }

            switch (selectedFunction.FunctionKey)
            {
                case "fluor_tm_25005_update_pipe_support_fab":
                    MessageBox.Show("Coming soon.", "Update Pipe Support Fab",
                        MessageBoxButton.OK, MessageBoxImage.None);
                    break;
                default:
                    MessageBox.Show("Coming soon.", "Project Specific Functions",
                        MessageBoxButton.OK, MessageBoxImage.None);
                    break;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private class ProjectSpecificFunctionItem
        {
            public string Project { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string FunctionKey { get; set; } = string.Empty;
        }
    }
}
