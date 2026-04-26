using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Syncfusion.Windows.Tools.Controls;

namespace VANTAGE.Utilities
{
    // Attaches hover-out auto-close behavior to a WPF ContextMenu.
    // Polls cursor position every 150ms while the menu is open. Two delays:
    //   InitialOpenGraceMs = 1500: from menu open until the cursor first enters the menu,
    //     so users can read the items before the close countdown starts.
    //   CursorLeftDelayMs  =  400: after the cursor has been over the menu and then left,
    //     close after this delay.
    // Hit-test uses Mouse.GetPosition relative to the popup-hosted control, more reliable
    // than IsMouseOver across Popup boundaries. Open submenus are treated as "still over"
    // so navigating into a submenu doesn't kill the parent.
    public static class MenuAutoClose
    {
        private const int InitialOpenGraceMs = 1500;
        private const int CursorLeftDelayMs = 400;
        private const int PollIntervalMs = 150;

        public static void Attach(ContextMenu menu)
        {
            DispatcherTimer? timer = null;
            DateTime? leftAt = null;
            bool hasBeenOver = false;

            menu.Opened += (_, _) =>
            {
                leftAt = null;
                hasBeenOver = false;
                timer?.Stop();
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PollIntervalMs) };
                timer.Tick += (_, _) =>
                {
                    if (!menu.IsOpen) { timer?.Stop(); return; }
                    bool over = IsCursorOverElement(menu) || HasOpenSubmenu(menu);
                    if (over) { hasBeenOver = true; leftAt = null; }
                    else
                    {
                        leftAt ??= DateTime.UtcNow;
                        int delay = hasBeenOver ? CursorLeftDelayMs : InitialOpenGraceMs;
                        if ((DateTime.UtcNow - leftAt.Value).TotalMilliseconds >= delay)
                        {
                            menu.IsOpen = false;
                            timer?.Stop();
                        }
                    }
                };
                timer.Start();
            };
            menu.Closed += (_, _) =>
            {
                timer?.Stop();
                leftAt = null;
                hasBeenOver = false;
            };
        }

        private static bool IsCursorOverElement(FrameworkElement element)
        {
            try
            {
                var p = Mouse.GetPosition(element);
                return p.X >= 0 && p.X <= element.ActualWidth
                    && p.Y >= 0 && p.Y <= element.ActualHeight;
            }
            catch
            {
                return true;
            }
        }

        private static bool HasOpenSubmenu(ContextMenu menu)
        {
            foreach (var item in menu.Items)
            {
                if (item is MenuItem mi && mi.IsSubmenuOpen) return true;
            }
            return false;
        }

        // DropDownButtonAdv (Syncfusion) hosts its dropdown content in a popup whose
        // visual tree is separate from the button. button.IsMouseOver doesn't propagate
        // from DropDownMenuItem children because they live in the popup's visual tree.
        // BUT the items ARE logical descendants of the button, and each item's own
        // IsMouseOver tracks cursor-over-that-item correctly. So walk the logical tree
        // and check IsMouseOver on every descendant.
        public static void Attach(DropDownButtonAdv button)
        {
            DispatcherTimer? timer = null;
            DateTime? leftAt = null;
            bool hasBeenOver = false;

            var dpd = DependencyPropertyDescriptor.FromProperty(
                DropDownButtonAdv.IsDropDownOpenProperty,
                typeof(DropDownButtonAdv));
            dpd.AddValueChanged(button, (_, _) =>
            {
                if (button.IsDropDownOpen)
                {
                    leftAt = null;
                    hasBeenOver = false;
                    timer?.Stop();
                    timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PollIntervalMs) };
                    timer.Tick += (_, _) =>
                    {
                        if (!button.IsDropDownOpen) { timer?.Stop(); return; }
                        bool over = IsCursorOverButtonOrDropdownArea(button);
                        if (over) { hasBeenOver = true; leftAt = null; }
                        else
                        {
                            leftAt ??= DateTime.UtcNow;
                            int delay = hasBeenOver ? CursorLeftDelayMs : InitialOpenGraceMs;
                            if ((DateTime.UtcNow - leftAt.Value).TotalMilliseconds >= delay)
                            {
                                button.IsDropDownOpen = false;
                                timer?.Stop();
                            }
                        }
                    };
                    timer.Start();
                }
                else
                {
                    timer?.Stop();
                    leftAt = null;
                    hasBeenOver = false;
                }
            });
        }

        // Hit-test for DropDownButtonAdv via cursor position relative to the button.
        // IsMouseOver-based detection is unreliable here: the popup is in a separate
        // visual tree, and Syncfusion keeps button.IsMouseOver in an ambiguous state
        // while the dropdown is open. Direct bounds check is more predictable: cursor
        // is "over" if it's inside the button's own rect OR a generous rect spanning
        // where the dropdown popup is rendered (below the button on the toolbar).
        private static bool IsCursorOverButtonOrDropdownArea(DropDownButtonAdv button)
        {
            try
            {
                Point p = Mouse.GetPosition(button);

                // Inside the button itself.
                if (p.X >= 0 && p.X <= button.ActualWidth
                    && p.Y >= 0 && p.Y <= button.ActualHeight)
                {
                    return true;
                }

                // Inside the dropdown area immediately below the button.
                // Toolbar dropdowns open downward; dropdown is typically at least as
                // wide as the button and rarely taller than ~600px. Generous rect
                // catches all realistic items without over-extending into other UI.
                if (p.Y >= button.ActualHeight && p.Y <= button.ActualHeight + 600
                    && p.X >= -40 && p.X <= button.ActualWidth + 240)
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
