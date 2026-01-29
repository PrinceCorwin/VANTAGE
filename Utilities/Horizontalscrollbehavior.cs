using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Syncfusion.UI.Xaml.Grid;

namespace VANTAGE.Utilities
{
    // Enables native horizontal scroll wheel support for mice like Logitech MX Master
    // WPF doesn't handle WM_MOUSEHWHEEL (0x020E) by default
    public static class HorizontalScrollBehavior
    {
        private const int WM_MOUSEHWHEEL = 0x020E;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public static void EnableForWindow(Window window)
        {
            if (window.IsLoaded)
            {
                AttachHook(window);
            }
            else
            {
                window.Loaded += (s, e) => AttachHook(window);
            }
        }

        private static void AttachHook(Window window)
        {
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            if (hwndSource != null)
            {
                hwndSource.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    if (msg == WM_MOUSEHWHEEL)
                    {
                        handled = HandleHorizontalScroll(window, wParam);
                    }
                    return IntPtr.Zero;
                });
            }
        }

        private static bool HandleHorizontalScroll(Window window, IntPtr wParam)
        {
            // Extract scroll delta (high word of wParam, signed)
            int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);

            // Get cursor position in screen coordinates
            if (!GetCursorPos(out POINT screenPoint))
                return false;

            // Convert to window coordinates
            var windowPoint = window.PointFromScreen(new Point(screenPoint.X, screenPoint.Y));

            // Hit test to find element under cursor
            var hitResult = VisualTreeHelper.HitTest(window, windowPoint);
            if (hitResult?.VisualHit == null)
                return false;

            double scrollAmount = delta > 0 ? -60 : 60;

            // First, try to find SfDataGrid (most common case in MILESTONE)
            var dataGrid = FindParent<SfDataGrid>(hitResult.VisualHit);
            if (dataGrid != null)
            {
                // Find a ScrollViewer that actually has horizontal content to scroll
                // SfDataGrid has multiple internal ScrollViewers; the first one found
                // by DFS may not be the one controlling horizontal content scrolling
                var scrollViewer = FindHorizontalScrollViewer(dataGrid);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollAmount);
                    return true;
                }
            }

            // Fall back to finding any ScrollViewer in the parent chain
            var sv = FindParent<ScrollViewer>(hitResult.VisualHit);
            if (sv != null && sv.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)
            {
                sv.ScrollToHorizontalOffset(sv.HorizontalOffset + scrollAmount);
                return true;
            }

            return false;
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var current = child;
            while (current != null)
            {
                if (current is T found)
                    return found;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // Find a ScrollViewer that has horizontal scrollable content
        private static ScrollViewer? FindHorizontalScrollViewer(DependencyObject parent)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer sv && sv.ScrollableWidth > 0)
                    return sv;

                var result = FindHorizontalScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}