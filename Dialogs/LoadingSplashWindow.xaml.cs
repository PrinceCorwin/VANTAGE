using System.Windows;

namespace VANTAGE.Dialogs
{
    public partial class LoadingSplashWindow : Window
    {
        public LoadingSplashWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Update the status text displayed on the splash screen.
        /// </summary>
        public void UpdateStatus(string status)
        {
            txtStatus.Text = status;

            // Force UI to update immediately
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        }
    }
}