using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // In-app video player for tutorial videos. Loads a short-lived pre-signed S3
    // URL inside a WebView2 so the link is never handed to an external browser —
    // there is no address bar to copy and the link expires quickly. If the link
    // lapses mid-view (user called away), the page shows a friendly expiry notice
    // instead of a broken frame.
    public partial class TutorialPlayerWindow : Window
    {
        private readonly string _videoUrl;
        private bool _initialized;

        // Saved window chrome so we can restore it when leaving video fullscreen.
        private bool _isFullScreen;
        private WindowState _prevWindowState;
        private WindowStyle _prevWindowStyle;
        private ResizeMode _prevResizeMode;

        public TutorialPlayerWindow(string videoUrl, string title)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));
            _videoUrl = videoUrl;
            Title = title;
            Loaded += TutorialPlayerWindow_Loaded;
            // Dispose the WebView2 on close so its browser process (and audio) stop.
            Closed += TutorialPlayerWindow_Closed;
        }

        private async void TutorialPlayerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeAndPlayAsync();
        }

        private async Task InitializeAndPlayAsync()
        {
            if (_initialized) return;

            try
            {
                // Dedicated user-data folder: this environment enables autoplay, so
                // it must not share the default WebView2 folder used elsewhere (mixing
                // environment options on one folder is unsupported).
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MILESTONE",
                    "WebView2Tutorials");

                var options = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                await webView.EnsureCoreWebView2Async(env);

                // Lock the surface down: no context menu (Save video as / Copy link),
                // no dev tools (inspect the signed URL), no status bar or zoom.
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                webView.DefaultBackgroundColor = System.Drawing.Color.Black;

                webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                // Drive real fullscreen: the video's fullscreen button raises this;
                // WebView2 does not resize itself, so the host window must.
                webView.CoreWebView2.ContainsFullScreenElementChanged += CoreWebView2_ContainsFullScreenElementChanged;

                _initialized = true;
                webView.NavigateToString(BuildPlayerHtml(_videoUrl));
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TutorialPlayerWindow.InitializeAndPlayAsync");
                txtLoading.Text = "Could not load the tutorial player.";
                busyIndicator.IsBusy = false;
            }
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            loadingOverlay.Visibility = Visibility.Collapsed;
            busyIndicator.IsBusy = false;
            webView.Visibility = Visibility.Visible;
        }

        // Enter/exit true window fullscreen in response to the video player's
        // fullscreen button (HTML Fullscreen API).
        private void CoreWebView2_ContainsFullScreenElementChanged(object? sender, object e)
        {
            if (webView.CoreWebView2.ContainsFullScreenElement)
            {
                if (_isFullScreen) return;
                _prevWindowState = WindowState;
                _prevWindowStyle = WindowStyle;
                _prevResizeMode = ResizeMode;

                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Normal;   // force a state change so Maximized re-lays out
                WindowState = WindowState.Maximized;
                _isFullScreen = true;
            }
            else
            {
                if (!_isFullScreen) return;
                WindowStyle = _prevWindowStyle;
                ResizeMode = _prevResizeMode;
                WindowState = _prevWindowState;
                _isFullScreen = false;
            }
        }

        // Tear down the WebView2 when the window closes so playback and audio stop
        // (closing the window alone leaves the browser process streaming).
        private void TutorialPlayerWindow_Closed(object? sender, EventArgs e)
        {
            try
            {
                webView.Dispose();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TutorialPlayerWindow.Closed");
            }
        }

        // Self-contained player page. The signed URL is injected as a JSON string
        // literal so any URL characters are safely escaped. The 'error' handler
        // covers the link expiring mid-stream — it swaps the video for a notice.
        private static string BuildPlayerHtml(string url)
        {
            string jsUrl = JsonSerializer.Serialize(url);
            return
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>" +
                "html,body{margin:0;height:100%;background:#000;overflow:hidden}" +
                "#v{width:100%;height:100%;background:#000;object-fit:contain}" +
                "#msg{position:fixed;inset:0;display:none;flex-direction:column;align-items:center;" +
                "justify-content:center;background:#000;color:#eee;font-family:'Segoe UI',sans-serif;" +
                "text-align:center;padding:24px}#msg h2{font-weight:600;margin:0 0 8px}" +
                "#msg p{margin:0;color:#aaa}</style></head><body>" +
                "<video id=\"v\" controls autoplay></video>" +
                "<div id=\"msg\"><h2>This tutorial session has expired</h2>" +
                "<p>Please reopen Tutorials to watch again.</p></div><script>" +
                "var v=document.getElementById('v'),m=document.getElementById('msg');" +
                "v.src=" + jsUrl + ";" +
                "v.addEventListener('error',function(){v.style.display='none';m.style.display='flex';});" +
                "</script></body></html>";
        }
    }
}
