using System.Windows;

namespace VANTAGE.Dialogs
{
    public partial class BusyDialog : Window
    {
        public BusyDialog(Window owner, string message = "Please wait...")
        {
            InitializeComponent();
            Owner = owner;
            txtStatus.Text = message;
        }

        public void UpdateStatus(string message)
        {
            txtStatus.Text = message;
        }
    }
}