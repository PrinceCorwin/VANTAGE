using System.Windows;

namespace VANTAGE.Utilities
{
    // Wrapper around MessageBox.Show that ensures the dialog renders in front.
    //
    // Reason: After long-running awaits or window-focus loss, a parameterless
    // MessageBox.Show can appear behind the owning window. Passing an explicit
    // owner + activating it first forces the dialog to the foreground.
    //
    // Usage: call AppMessageBox.Show(...) anywhere MessageBox.Show(...) was
    // previously used. Signatures mirror MessageBox.Show 1:1.
    public static class AppMessageBox
    {
        private static Window? FindOwner()
        {
            if (Application.Current == null) return null;

            return Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive && w.IsVisible)
                ?? Application.Current.MainWindow;
        }

        private static void BringToFront(Window owner)
        {
            try
            {
                if (owner.WindowState == WindowState.Minimized)
                    owner.WindowState = WindowState.Normal;
                owner.Activate();
                // Topmost-toggle forces Windows to raise the z-order
                owner.Topmost = true;
                owner.Topmost = false;
            }
            catch { }
        }

        public static MessageBoxResult Show(string messageBoxText)
            => Show(null, messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);

        public static MessageBoxResult Show(string messageBoxText, string caption)
            => Show(null, messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
            => Show(null, messageBoxText, caption, button, MessageBoxImage.None);

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
            => Show(null, messageBoxText, caption, button, icon);

        public static MessageBoxResult Show(Window? owner, string messageBoxText)
            => Show(owner, messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);

        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption)
            => Show(owner, messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);

        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button)
            => Show(owner, messageBoxText, caption, button, MessageBoxImage.None);

        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            owner ??= FindOwner();
            if (owner != null)
            {
                BringToFront(owner);
                return MessageBox.Show(owner, messageBoxText, caption, button, icon);
            }
            return MessageBox.Show(messageBoxText, caption, button, icon);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
            => Show(null, messageBoxText, caption, button, icon, defaultResult);

        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            owner ??= FindOwner();
            if (owner != null)
            {
                BringToFront(owner);
                return MessageBox.Show(owner, messageBoxText, caption, button, icon, defaultResult);
            }
            return MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
        }
    }
}
