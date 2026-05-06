using System.Windows;

namespace VANTAGE.Utilities
{
    // Helper for opening the help sidebar at a specific anchor in manual.html.
    // Used by info icons throughout the app.
    public static class HelpService
    {
        public static void OpenAt(string anchor)
        {
            try
            {
                if (Application.Current?.MainWindow is MainWindow mw)
                {
                    mw.OpenHelpAt(anchor);
                }
            }
            catch (System.Exception ex)
            {
                AppLogger.Error(ex, "HelpService.OpenAt");
            }
        }
    }
}
