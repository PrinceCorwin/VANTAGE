using System.Windows;
using System.Windows.Input;

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

        // Chromeless window — let the user drag it out of the way by clicking anywhere.
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}