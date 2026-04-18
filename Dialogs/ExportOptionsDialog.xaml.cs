using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public enum ExportChoice
    {
        Cancel,
        Filtered,
        All
    }

    // Themed dialog to choose between filtered vs. all records for export
    public partial class ExportOptionsDialog : Window
    {
        public ExportChoice Choice { get; private set; } = ExportChoice.Cancel;

        public ExportOptionsDialog(int allCount, int filteredCount)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            runAllCount.Text = $"{allCount:N0} activities";
            runFilteredCount.Text = $"{filteredCount:N0} activities";
        }

        private void BtnFiltered_Click(object sender, RoutedEventArgs e)
        {
            Choice = ExportChoice.Filtered;
            DialogResult = true;
            Close();
        }

        private void BtnAll_Click(object sender, RoutedEventArgs e)
        {
            Choice = ExportChoice.All;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Choice = ExportChoice.Cancel;
            DialogResult = false;
            Close();
        }
    }
}
