using System;
using System.Windows;

namespace VANTAGE.Dialogs
{
    public partial class P6ExportDialog : Window
    {
        public TimeSpan StartTime { get; private set; } = new TimeSpan(7, 0, 0);  // 7:00 AM
        public TimeSpan FinishTime { get; private set; } = new TimeSpan(17, 0, 0); // 5:00 PM

        public P6ExportDialog(DateTime weekEndDate, int rowCount)
        {
            InitializeComponent();

            // Display info
            txtWeekEndDate.Text = weekEndDate.ToString("yyyy-MM-dd");
            txtRowCount.Text = $"{rowCount} activities will be exported";

            // Set default times
            cmbStartHour.SelectedIndex = 7;   // 7:00
            cmbStartMinute.SelectedIndex = 0; // :00
            cmbFinishHour.SelectedIndex = 17; // 17:00
            cmbFinishMinute.SelectedIndex = 0; // :00
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            // Parse selected times
            int startHour = cmbStartHour.SelectedIndex;
            int startMinute = cmbStartMinute.SelectedIndex * 15; // 0, 15, 30, 45
            int finishHour = cmbFinishHour.SelectedIndex;
            int finishMinute = cmbFinishMinute.SelectedIndex * 15;

            StartTime = new TimeSpan(startHour, startMinute, 0);
            FinishTime = new TimeSpan(finishHour, finishMinute, 0);

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