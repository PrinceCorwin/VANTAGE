using System.Collections.Generic;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class BlankComponentDialog : Window
    {
        public List<BlankComponentItem> Items { get; }

        public BlankComponentDialog(List<(int ExcelRow, string DrawingNumber, string RawDescription)> blankRows)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            Items = new List<BlankComponentItem>();
            foreach (var (excelRow, dwg, desc) in blankRows)
            {
                Items.Add(new BlankComponentItem
                {
                    ExcelRow = excelRow,
                    DrawingNumber = dwg,
                    RawDescription = desc,
                    Component = ""
                });
            }

            itemsList.ItemsSource = Items;
        }

        // Returns map of excel row -> component for rows that were filled in
        public Dictionary<int, string> GetAssignments()
        {
            var result = new Dictionary<int, string>();
            foreach (var item in Items)
            {
                if (!string.IsNullOrWhiteSpace(item.Component))
                    result[item.ExcelRow] = item.Component;
            }
            return result;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

    // View model for each blank component row
    public class BlankComponentItem
    {
        public int ExcelRow { get; set; }
        public string DrawingNumber { get; set; } = "";
        public string RawDescription { get; set; } = "";
        public string Component { get; set; } = "";
    }
}
