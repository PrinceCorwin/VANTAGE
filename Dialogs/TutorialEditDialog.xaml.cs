using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Add-or-edit form for a tutorial. In New mode it collects a file, filename (key),
    // title and description and inspects the video. In Edit mode only title/description
    // are shown.
    public partial class TutorialEditDialog : Window
    {
        private readonly bool _editMode;
        private readonly HashSet<string> _existingKeys;
        private readonly HashSet<string> _existingNames;

        // Results (read by the caller when DialogResult == true)
        public string ResultKey { get; private set; } = "";
        public string ResultName { get; private set; } = "";
        public string ResultDescription { get; private set; } = "";
        public string? ResultFilePath { get; private set; }
        public Mp4Info? ResultInfo { get; private set; }

        private bool _fileInspected;

        public TutorialEditDialog(bool editMode, TutorialItem? existing, HashSet<string> existingKeys, HashSet<string> existingNames)
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            _editMode = editMode;
            _existingKeys = existingKeys;
            _existingNames = existingNames;

            if (_editMode)
            {
                Title = "Edit Tutorial Details";
                txtHeader.Text = "Edit Tutorial Details";

                lblFile.Visibility = Visibility.Collapsed;
                pnlFile.Visibility = Visibility.Collapsed;
                pnlInspect.Visibility = Visibility.Collapsed;
                lblKey.Visibility = Visibility.Collapsed;
                txtKey.Visibility = Visibility.Collapsed;

                if (existing != null)
                {
                    txtName.Text = existing.Name;
                    txtDesc.Text = existing.Description;
                    ResultKey = existing.Key;
                }
            }
            else
            {
                Title = "Upload Tutorial";
                txtHeader.Text = "Upload New Tutorial";
            }
        }

        private async void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Tutorial Video",
                Filter = "MP4 Video (*.mp4)|*.mp4",
                DefaultExt = ".mp4"
            };
            if (dlg.ShowDialog(this) != true) return;

            if (!FfmpegProvider.IsAvailable)
            {
                AppMessageBox.Show("The video tools are not available. Close and reopen Manage Tutorials to download them.",
                    "Missing Tools", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            txtFile.Text = dlg.FileName;
            _fileInspected = false;
            ResultFilePath = null;
            ResultInfo = null;
            btnBrowse.IsEnabled = false;

            try
            {
                var info = await Mp4Tooling.InspectAsync(dlg.FileName);

                string audio = info.HasAudio ? info.AudioCodec! : "no audio";
                string extra = info.ExtraTrackCount > 0
                    ? $" · {info.ExtraTrackCount} extra track(s) will be removed"
                    : "";

                if (info.CodecsAreStreamable)
                {
                    txtInspect.Text =
                        $"{info.VideoCodec.ToUpperInvariant()} / {audio.ToUpperInvariant()} · " +
                        $"{info.Width}×{info.Height} · {info.DurationDisplay} · {info.FileSizeDisplay}\n" +
                        $"Will be optimized for streaming (faststart){extra}.";
                    _fileInspected = true;
                    ResultFilePath = dlg.FileName;
                    ResultInfo = info;

                    if (string.IsNullOrWhiteSpace(txtKey.Text))
                        txtKey.Text = TutorialKeyValidator.Sanitize(Path.GetFileName(dlg.FileName));
                }
                else
                {
                    txtInspect.Text =
                        $"Unsupported codecs: {info.VideoCodec.ToUpperInvariant()} / {audio.ToUpperInvariant()}.\n" +
                        $"VANTAGE tutorials must be H.264 video + AAC audio. Re-export from your editor and try again.";
                }

                pnlInspect.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TutorialEditDialog.BtnBrowse_Click");
                AppMessageBox.Show("Could not read that video file. See the log for details.", "File Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtInspect.Text = "";
                pnlInspect.Visibility = Visibility.Collapsed;
            }
            finally
            {
                btnBrowse.IsEnabled = true;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text.Trim();
            string desc = txtDesc.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                AppMessageBox.Show("Title is required.", "Missing Title", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Titles are what the Tutorials list shows; keep them unique (case-insensitive).
            // In edit mode the caller excludes this item's own title from the set.
            if (_existingNames.Contains(name.ToLowerInvariant()))
            {
                AppMessageBox.Show($"A tutorial titled '{name}' already exists. Choose a different title.",
                    "Duplicate Title", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_editMode)
            {
                ResultName = name;
                ResultDescription = desc;
                DialogResult = true;
                return;
            }

            // New mode
            if (ResultFilePath == null || !_fileInspected)
            {
                AppMessageBox.Show("Choose a valid H.264/AAC video file first.", "No Video Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string key = TutorialKeyValidator.Sanitize(txtKey.Text);
            if (!TutorialKeyValidator.IsValid(key, out string keyError))
            {
                AppMessageBox.Show(keyError, "Invalid Filename", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Reflect the sanitized key back so the user sees exactly what will be stored.
            txtKey.Text = key;

            if (_existingKeys.Contains(key.ToLowerInvariant()))
            {
                AppMessageBox.Show($"A tutorial with filename '{key}' already exists. Choose a different filename.",
                    "Duplicate Filename", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ResultKey = key;
            ResultName = name;
            ResultDescription = desc;
            DialogResult = true;
        }
    }
}
