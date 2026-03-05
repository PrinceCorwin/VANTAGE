using System;
using System.Windows;

namespace VANTAGE.Services.Plugins
{
    // Host interface provided to plugins - gives them access to app capabilities
    public interface IPluginHost
    {
        // Add a menu item to the Tools menu
        // header: Display text for the menu item
        // onClick: Action to execute when clicked
        // addSeparatorBefore: If true, adds a horizontal separator before this item
        void AddToolsMenuItem(string header, Action onClick, bool addSeparatorBefore = false);

        // Get the main window (for showing dialogs with proper owner)
        Window MainWindow { get; }

        // Get the current logged-in username
        string CurrentUsername { get; }

        // Show an info message to the user
        void ShowInfo(string message, string title = "Information");

        // Show an error message to the user
        void ShowError(string message, string title = "Error");

        // Show a confirmation dialog, returns true if user clicked Yes
        bool ShowConfirmation(string message, string title = "Confirm");

        // Log an info message
        void LogInfo(string message, string source);

        // Log an error
        void LogError(Exception ex, string source);
    }
}
