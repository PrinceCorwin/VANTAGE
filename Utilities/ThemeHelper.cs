using System.Windows;
using System.Windows.Media;

namespace VANTAGE.Utilities
{
    // Helper class for accessing theme resources from code-behind
    public static class ThemeHelper
    {
        // Get a Brush resource from the application theme
        public static Brush GetBrush(string resourceKey)
        {
            return (Brush)Application.Current.FindResource(resourceKey);
        }

        // Common brush properties for convenience
        public static Brush BackgroundColor => GetBrush("BackgroundColor");
        public static Brush ControlBackground => GetBrush("ControlBackground");
        public static Brush ControlBorder => GetBrush("ControlBorder");
        public static Brush ForegroundColor => GetBrush("ForegroundColor");
        public static Brush TextColorSecondary => GetBrush("TextColorSecondary");
        public static Brush SidebarBorder => GetBrush("SidebarBorder");
        public static Brush ButtonDangerBackground => GetBrush("ButtonDangerBackground");
        public static Brush ButtonSuccessBackground => GetBrush("ButtonSuccessBackground");
        public static Brush ButtonPrimaryBackground => GetBrush("ButtonPrimaryBackground");
        public static Brush OverlayText => GetBrush("OverlayText");
        public static Brush WarningText => GetBrush("WarningText");
        public static Brush ErrorText => GetBrush("ErrorText");
        public static Brush DisabledText => GetBrush("DisabledText");
    }
}
