using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using Syncfusion.SfSkinManager;
using VANTAGE.Models.AI;
using VANTAGE.Services.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ConfigCreatorWindow : Window
    {
        private enum DrawMode { Bom, TitleBlock }

        // Tracks a drawn region on the canvas
        private class DrawnRegion
        {
            public DrawMode Mode { get; set; }
            public double XPct { get; set; }
            public double YPct { get; set; }
            public double WidthPct { get; set; }
            public double HeightPct { get; set; }
            public Rectangle? Visual { get; set; }
            public TextBlock? Label { get; set; }
        }

        private DrawMode _currentMode = DrawMode.Bom;
        private readonly List<DrawnRegion> _drawnRegions = new();
        private BitmapImage? _loadedImage;
        private TakeoffService? _service;
        private string? _editConfigKey;
        private string? _tempFilePath;

        // Drag tracking
        private bool _isDragging;
        private Point _dragStart;
        private Rectangle? _previewRect;

        // Pending regions loaded from S3 before image is available
        private List<DrawnRegion>? _pendingRegions;

        public ConfigCreatorWindow(string? editConfigKey = null)
        {
            InitializeComponent();
            // Apply Syncfusion theme only to ButtonAdv controls, not entire window
            // Window-level theme overrides programmatic Background on standard Buttons (mode toggles)
            var sfTheme = new Theme(ThemeManager.GetSyncfusionThemeName());
            SfSkinManager.SetTheme(btnLoadDrawing, sfTheme);
            SfSkinManager.SetTheme(btnUndo, sfTheme);
            SfSkinManager.SetTheme(btnClearAll, sfTheme);

            _editConfigKey = editConfigKey;
            if (_editConfigKey != null)
            {
                Title = "Edit Config";
                btnDeleteConfig.Visibility = Visibility.Visible;
            }

            Loaded += ConfigCreatorWindow_Loaded;
            Closed += ConfigCreatorWindow_Closed;
        }

        private async void ConfigCreatorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_editConfigKey == null) return;

            try
            {
                _service = new TakeoffService();
                var config = await _service.GetConfigAsync(_editConfigKey);
                if (config == null)
                {
                    MessageBox.Show("Could not load config from S3.", "Load Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Populate text field — use ProjectName as the config name (or fall back to ProjectId)
                txtConfigName.Text = !string.IsNullOrEmpty(config.ProjectName)
                    ? config.ProjectName
                    : config.ProjectId;

                // Build pending regions from config
                _pendingRegions = new List<DrawnRegion>();
                foreach (var bom in config.BomRegions)
                {
                    _pendingRegions.Add(new DrawnRegion
                    {
                        Mode = DrawMode.Bom,
                        XPct = bom.XPct,
                        YPct = bom.YPct,
                        WidthPct = bom.WidthPct,
                        HeightPct = bom.HeightPct
                    });
                }

                // Load title block regions (supports both old single-region and new multi-region format)
                foreach (var tb in config.TitleBlockRegions)
                {
                    _pendingRegions.Add(new DrawnRegion
                    {
                        Mode = DrawMode.TitleBlock,
                        XPct = tb.XPct,
                        YPct = tb.YPct,
                        WidthPct = tb.WidthPct,
                        HeightPct = tb.HeightPct
                    });
                }

                // Try to load a drawing from S3 for preview
                string prefix = TakeoffService.GetDrawingPrefix(_editConfigKey);
                var drawings = await _service.ListDrawingsAsync(prefix);
                var pdf = drawings.FirstOrDefault(d => d.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

                if (pdf.Key != null)
                {
                    SetCanvasHint("Downloading drawing from S3...");
                    _tempFilePath = await _service.DownloadDrawingToTempAsync(pdf.Key);
                    LoadPdfToImage(_tempFilePath);
                }
                else
                {
                    SetCanvasHint("No drawings in S3 — load a local PDF to preview regions");
                    // Apply pending regions once user loads a drawing
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ConfigCreatorWindow.ConfigCreatorWindow_Loaded");
                MessageBox.Show($"Error loading config: {ex.Message}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfigCreatorWindow_Closed(object? sender, EventArgs e)
        {
            _service?.Dispose();
            CleanupTempFile();
        }

        private void CleanupTempFile()
        {
            if (_tempFilePath != null && File.Exists(_tempFilePath))
            {
                try { File.Delete(_tempFilePath); }
                catch { /* best effort */ }
                _tempFilePath = null;
            }
        }

        // Load a local PDF file and render page 0 as the canvas background
        private void BtnLoadDrawing_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Drawing PDF",
                Filter = "PDF Files|*.pdf",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            LoadPdfToImage(dialog.FileName);
        }

        private void LoadPdfToImage(string pdfPath)
        {
            try
            {
                SetCanvasHint("Rendering PDF...");
                var imageBytes = PdfToImageConverter.ConvertPageToImage(pdfPath, 0, 150);
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    SetCanvasHint("Failed to render PDF. Try a different file.");
                    return;
                }

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new MemoryStream(imageBytes);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                _loadedImage = bmp;
                imgDrawing.Source = bmp;
                txtCanvasHint.Visibility = Visibility.Collapsed;

                ApplyPendingRegions();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ConfigCreatorWindow.LoadPdfToImage");
                SetCanvasHint($"Error: {ex.Message}");
            }
        }

        // Apply regions loaded from S3 config once the image is available
        private void ApplyPendingRegions()
        {
            if (_pendingRegions == null || _loadedImage == null) return;

            foreach (var r in _pendingRegions)
                _drawnRegions.Add(r);

            _pendingRegions = null;
            RedrawAllRegions();
        }

        private void SetCanvasHint(string text)
        {
            txtCanvasHint.Text = text;
            txtCanvasHint.Visibility = Visibility.Visible;
        }

        // Mode toggle buttons
        private void BtnModeBom_Click(object sender, RoutedEventArgs e)
        {
            _currentMode = DrawMode.Bom;
            btnModeBom.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
            btnModeBom.Foreground = Brushes.White;
            btnModeBom.BorderThickness = new Thickness(0);
            btnModeTitleBlock.Background = (Brush)FindResource("ControlBackground");
            btnModeTitleBlock.Foreground = (Brush)FindResource("ForegroundColor");
            btnModeTitleBlock.BorderBrush = (Brush)FindResource("BorderColor");
            btnModeTitleBlock.BorderThickness = new Thickness(1);
        }

        private void BtnModeTitleBlock_Click(object sender, RoutedEventArgs e)
        {
            _currentMode = DrawMode.TitleBlock;
            btnModeTitleBlock.Background = new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00));
            btnModeTitleBlock.Foreground = Brushes.White;
            btnModeTitleBlock.BorderThickness = new Thickness(0);
            btnModeBom.Background = (Brush)FindResource("ControlBackground");
            btnModeBom.Foreground = (Brush)FindResource("ForegroundColor");
            btnModeBom.BorderBrush = (Brush)FindResource("BorderColor");
            btnModeBom.BorderThickness = new Thickness(1);
        }

        // Undo / Clear
        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (_drawnRegions.Count == 0) return;

            var last = _drawnRegions[^1];
            if (last.Visual != null) drawCanvas.Children.Remove(last.Visual);
            if (last.Label != null) drawCanvas.Children.Remove(last.Label);
            _drawnRegions.RemoveAt(_drawnRegions.Count - 1);
            UpdateRegionCount();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            _drawnRegions.Clear();
            drawCanvas.Children.Clear();
            UpdateRegionCount();
        }

        // Calculate where the Stretch=Uniform image actually renders within the canvas
        private Rect GetImageRenderRect()
        {
            if (_loadedImage == null) return Rect.Empty;

            double canvasW = drawCanvas.ActualWidth;
            double canvasH = drawCanvas.ActualHeight;
            double imgW = _loadedImage.PixelWidth;
            double imgH = _loadedImage.PixelHeight;

            if (canvasW <= 0 || canvasH <= 0 || imgW <= 0 || imgH <= 0)
                return Rect.Empty;

            double scale = Math.Min(canvasW / imgW, canvasH / imgH);
            double renderW = imgW * scale;
            double renderH = imgH * scale;
            double offsetX = (canvasW - renderW) / 2;
            double offsetY = (canvasH - renderH) / 2;

            return new Rect(offsetX, offsetY, renderW, renderH);
        }

        // Convert canvas pixel coordinates to percentage of image
        private (double xPct, double yPct) CanvasToPercent(double canvasX, double canvasY)
        {
            var r = GetImageRenderRect();
            if (r.IsEmpty) return (0, 0);

            double xPct = (canvasX - r.X) / r.Width * 100.0;
            double yPct = (canvasY - r.Y) / r.Height * 100.0;
            return (xPct, yPct);
        }

        // Convert percentage coordinates to canvas pixels
        private (double x, double y) PercentToCanvas(double xPct, double yPct)
        {
            var r = GetImageRenderRect();
            if (r.IsEmpty) return (0, 0);

            double x = r.X + xPct / 100.0 * r.Width;
            double y = r.Y + yPct / 100.0 * r.Height;
            return (x, y);
        }

        // Mouse drag to draw rectangles
        private void DrawCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_loadedImage == null) return;

            var pos = e.GetPosition(drawCanvas);
            var imgRect = GetImageRenderRect();
            if (!imgRect.Contains(pos)) return;

            _isDragging = true;
            _dragStart = pos;

            var color = _currentMode == DrawMode.Bom
                ? Color.FromArgb(60, 0x2E, 0x7D, 0x32)
                : Color.FromArgb(60, 0xE6, 0x51, 0x00);
            var strokeColor = _currentMode == DrawMode.Bom
                ? Color.FromRgb(0x2E, 0x7D, 0x32)
                : Color.FromRgb(0xE6, 0x51, 0x00);

            _previewRect = new Rectangle
            {
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(color),
                Width = 0,
                Height = 0
            };

            Canvas.SetLeft(_previewRect, pos.X);
            Canvas.SetTop(_previewRect, pos.Y);
            drawCanvas.Children.Add(_previewRect);
            drawCanvas.CaptureMouse();
        }

        private void DrawCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _previewRect == null) return;

            var pos = e.GetPosition(drawCanvas);
            var imgRect = GetImageRenderRect();

            // Clamp to image bounds
            double x = Math.Max(imgRect.X, Math.Min(pos.X, imgRect.Right));
            double y = Math.Max(imgRect.Y, Math.Min(pos.Y, imgRect.Bottom));

            double left = Math.Min(_dragStart.X, x);
            double top = Math.Min(_dragStart.Y, y);
            double width = Math.Abs(x - _dragStart.X);
            double height = Math.Abs(y - _dragStart.Y);

            Canvas.SetLeft(_previewRect, left);
            Canvas.SetTop(_previewRect, top);
            _previewRect.Width = width;
            _previewRect.Height = height;
        }

        private void DrawCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging || _previewRect == null) return;

            _isDragging = false;
            drawCanvas.ReleaseMouseCapture();

            // Remove preview
            drawCanvas.Children.Remove(_previewRect);

            double left = Canvas.GetLeft(_previewRect);
            double top = Canvas.GetTop(_previewRect);
            double width = _previewRect.Width;
            double height = _previewRect.Height;
            _previewRect = null;

            // Minimum size check (ignore tiny accidental clicks)
            if (width < 5 || height < 5) return;

            var (xPct, yPct) = CanvasToPercent(left, top);
            var imgRect = GetImageRenderRect();
            double widthPct = width / imgRect.Width * 100.0;
            double heightPct = height / imgRect.Height * 100.0;

            // Allow multiple title block regions (e.g., PIPE INFO section + Project info section)
            var region = new DrawnRegion
            {
                Mode = _currentMode,
                XPct = Math.Round(xPct, 1),
                YPct = Math.Round(yPct, 1),
                WidthPct = Math.Round(widthPct, 1),
                HeightPct = Math.Round(heightPct, 1)
            };

            _drawnRegions.Add(region);
            AddRegionToCanvas(region);
            UpdateRegionCount();
        }

        // Add a visual rectangle + label to the canvas for a region
        private void AddRegionToCanvas(DrawnRegion region)
        {
            var imgRect = GetImageRenderRect();
            if (imgRect.IsEmpty) return;

            var (canvasX, canvasY) = PercentToCanvas(region.XPct, region.YPct);
            double canvasW = region.WidthPct / 100.0 * imgRect.Width;
            double canvasH = region.HeightPct / 100.0 * imgRect.Height;

            bool isBom = region.Mode == DrawMode.Bom;
            var fillColor = isBom
                ? Color.FromArgb(40, 0x2E, 0x7D, 0x32)
                : Color.FromArgb(40, 0xE6, 0x51, 0x00);
            var strokeColor = isBom
                ? Color.FromRgb(0x2E, 0x7D, 0x32)
                : Color.FromRgb(0xE6, 0x51, 0x00);

            var rect = new Rectangle
            {
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(fillColor),
                Width = canvasW,
                Height = canvasH,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(rect, canvasX);
            Canvas.SetTop(rect, canvasY);
            drawCanvas.Children.Add(rect);
            region.Visual = rect;

            // Calculate label text based on mode and position in list
            string labelText;
            if (isBom)
            {
                int bomCount = _drawnRegions.Count(r => r.Mode == DrawMode.Bom);
                int myIndex = _drawnRegions.Where(r => r.Mode == DrawMode.Bom).ToList().IndexOf(region);
                labelText = bomCount == 1 ? "BOM" : $"BOM {myIndex + 1}";
            }
            else
            {
                int tbCount = _drawnRegions.Count(r => r.Mode == DrawMode.TitleBlock);
                int myIndex = _drawnRegions.Where(r => r.Mode == DrawMode.TitleBlock).ToList().IndexOf(region);
                labelText = tbCount == 1 ? "Title Block" : $"Title Block {myIndex + 1}";
            }

            var label = new TextBlock
            {
                Text = labelText,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(strokeColor),
                Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
                Padding = new Thickness(4, 2, 4, 2),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(label, canvasX + 4);
            Canvas.SetTop(label, canvasY + 4);
            drawCanvas.Children.Add(label);
            region.Label = label;
        }

        // Redraw all regions from stored percentage data (called on resize)
        private void RedrawAllRegions()
        {
            drawCanvas.Children.Clear();

            foreach (var region in _drawnRegions)
            {
                region.Visual = null;
                region.Label = null;
                AddRegionToCanvas(region);
            }

            UpdateRegionCount();
        }

        private void DrawCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_drawnRegions.Count > 0)
                RedrawAllRegions();
        }

        private void UpdateRegionCount()
        {
            int bomCount = _drawnRegions.Count(r => r.Mode == DrawMode.Bom);
            int tbCount = _drawnRegions.Count(r => r.Mode == DrawMode.TitleBlock);
            txtRegionCount.Text = $"Regions: {bomCount} BOM, {tbCount} Title Block";
        }

        // S3 path auto-update from name field
        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            string username = Sanitize(App.CurrentUser?.Username ?? "unknown");
            string configName = Sanitize(txtConfigName.Text);
            txtS3Path.Text = $"clients/{username}/{configName}.json";
        }

        // Lowercase, spaces to hyphens, strip non-alphanumeric
        private static string Sanitize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "...";
            string s = input.Trim().ToLowerInvariant();
            s = s.Replace(' ', '-');
            s = Regex.Replace(s, @"[^a-z0-9\-]", "");
            return string.IsNullOrEmpty(s) ? "..." : s;
        }

        // Delete config from S3
        private async void BtnDeleteConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_editConfigKey == null) return;

            string prefix = TakeoffService.GetDrawingPrefix(_editConfigKey);

            var result = MessageBox.Show(
                $"Delete this config?\n\n" +
                $"Config: {_editConfigKey}\n" +
                $"Drawings folder: {prefix}/\n\n" +
                "The config will be deleted. Drawings in S3 under this prefix will also be deleted.\n\n" +
                "This cannot be undone.",
                "Delete Config",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            btnDeleteConfig.IsEnabled = false;
            btnSaveConfig.IsEnabled = false;

            try
            {
                _service?.Dispose();
                _service = new TakeoffService();

                // Delete drawings under this config's prefix
                var drawings = await _service.ListDrawingsAsync(prefix);
                if (drawings.Count > 0)
                {
                    var keys = drawings.Select(d => d.Key).ToList();
                    await _service.DeleteDrawingsAsync(keys);
                }

                // Delete the config JSON
                await _service.DeleteConfigAsync(_editConfigKey);

                AppLogger.Info($"Config deleted: {_editConfigKey}",
                    "ConfigCreatorWindow.BtnDeleteConfig_Click", App.CurrentUser?.Username);

                MessageBox.Show("Config and associated drawings deleted.",
                    "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ConfigCreatorWindow.BtnDeleteConfig_Click");
                MessageBox.Show($"Error deleting config: {ex.Message}", "Delete Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                btnDeleteConfig.IsEnabled = true;
                btnSaveConfig.IsEnabled = true;
            }
        }

        // Save config to S3
        private async void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(txtConfigName.Text))
            {
                MessageBox.Show("Please enter a config name.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtConfigName.Focus();
                return;
            }

            int bomCount = _drawnRegions.Count(r => r.Mode == DrawMode.Bom);
            int tbCount = _drawnRegions.Count(r => r.Mode == DrawMode.TitleBlock);

            if (bomCount == 0)
            {
                MessageBox.Show("Draw at least one BOM region on the drawing.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (tbCount == 0)
            {
                MessageBox.Show("Draw a Title Block region on the drawing.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string username = Sanitize(App.CurrentUser?.Username ?? "unknown");
            string configName = Sanitize(txtConfigName.Text);

            // Build BOM regions with labels
            var bomRegions = new List<CropRegion>();
            var bomDrawn = _drawnRegions.Where(r => r.Mode == DrawMode.Bom).ToList();
            for (int i = 0; i < bomDrawn.Count; i++)
            {
                string label = i == 0 ? "primary" : $"secondary_{i + 1}";
                bomRegions.Add(new CropRegion
                {
                    Label = label,
                    XPct = bomDrawn[i].XPct,
                    YPct = bomDrawn[i].YPct,
                    WidthPct = bomDrawn[i].WidthPct,
                    HeightPct = bomDrawn[i].HeightPct
                });
            }

            // Title block regions (multiple sections like PIPE INFO + Project info)
            var tbRegions = new List<CropRegion>();
            var tbDrawn = _drawnRegions.Where(r => r.Mode == DrawMode.TitleBlock).ToList();
            for (int i = 0; i < tbDrawn.Count; i++)
            {
                string label = i == 0 ? "title_block" : $"title_block_{i + 1}";
                tbRegions.Add(new CropRegion
                {
                    Label = label,
                    XPct = tbDrawn[i].XPct,
                    YPct = tbDrawn[i].YPct,
                    WidthPct = tbDrawn[i].WidthPct,
                    HeightPct = tbDrawn[i].HeightPct
                });
            }

            var config = new CropRegionConfig
            {
                ClientId = username,
                ProjectId = configName,
                ClientName = App.CurrentUser?.Username ?? "Unknown",
                ProjectName = txtConfigName.Text.Trim(),
                BomRegions = bomRegions,
                TitleBlockRegions = tbRegions,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                CreatedBy = App.CurrentUser?.Username ?? "Unknown"
            };

            btnSaveConfig.IsEnabled = false;

            try
            {
                _service?.Dispose();
                _service = new TakeoffService();
                await _service.SaveConfigAsync(config);

                string s3Key = $"clients/{username}/{configName}.json";
                AppLogger.Info($"Config saved: {s3Key}",
                    "ConfigCreatorWindow.BtnSaveConfig_Click", App.CurrentUser?.Username);

                MessageBox.Show($"Config saved to S3:\n{s3Key}",
                    "Config Saved", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "ConfigCreatorWindow.BtnSaveConfig_Click");
                MessageBox.Show($"Error saving config: {ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSaveConfig.IsEnabled = true;
            }
        }
    }
}
