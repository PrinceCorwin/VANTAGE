using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Models;
using VANTAGE.Services;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Admin manager for the tutorials bucket: list, upload (with video checking +
    // streaming optimization), edit details, and delete. Opened from Admin menu →
    // Manage Tutorials (gated to admins).
    public partial class TutorialManagerDialog : Window
    {
        private readonly ObservableCollection<TutorialItem> _items = new();

        private static string ScratchDir => Path.Combine(Path.GetTempPath(), "VANTAGE_TutorialManager");

        private static string CurrentUser => App.CurrentUser?.Username ?? "Unknown";

        public TutorialManagerDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            grdTutorials.ItemsSource = _items;
            Loaded += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            SetBusy(true, "Loading tutorials...");
            try
            {
                await ReloadListAsync();
            }
            finally
            {
                SetBusy(false);
            }
        }

        // Load the manifest, bind it, and report counts + any bucket/manifest mismatches.
        // Swallows/logs its own errors so callers running mid-operation aren't disrupted.
        private async Task ReloadListAsync()
        {
            try
            {
                var manifest = await TutorialService.GetTutorialsAsync();

                _items.Clear();
                foreach (var item in manifest)
                    _items.Add(item);

                string status = $"{manifest.Count} tutorial(s).";

                try
                {
                    var bucketKeys = await TutorialService.ListVideoKeysAsync();
                    var manifestKeys = new HashSet<string>(manifest.Select(m => m.Key), StringComparer.OrdinalIgnoreCase);
                    var bucketSet = new HashSet<string>(bucketKeys, StringComparer.OrdinalIgnoreCase);

                    int orphans = bucketKeys.Count(k => !manifestKeys.Contains(k));
                    int missing = manifest.Count(m => !bucketSet.Contains(m.Key));

                    if (orphans > 0)
                        status += $"  ⚠ {orphans} video(s) in the bucket are not listed in the manifest.";
                    if (missing > 0)
                        status += $"  ⚠ {missing} listed video(s) have no file in the bucket.";
                }
                catch (Exception ex)
                {
                    // Reconciliation is best-effort; the list itself already loaded.
                    AppLogger.Error(ex, "TutorialManagerDialog.ReloadListAsync (reconcile)");
                }

                txtStatus.Text = status;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TutorialManagerDialog.ReloadListAsync");
                txtStatus.Text = "Failed to load the tutorial list.";
                AppMessageBox.Show("Could not load the tutorial list. See the log for details.", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            // Make sure ffmpeg/ffprobe are present (download once on first use).
            try
            {
                if (!FfmpegProvider.IsAvailable)
                {
                    var dlProgress = new Progress<int>(p => txtBusy.Text = $"Downloading video tools (one-time)... {p}%");
                    SetBusy(true, "Downloading video tools (one-time)...");
                    await FfmpegProvider.EnsureAvailableAsync(dlProgress);
                    SetBusy(false);
                }
            }
            catch (Exception ex)
            {
                SetBusy(false);
                AppLogger.Error(ex, "TutorialManagerDialog.BtnUpload_Click (tools)");
                AppMessageBox.Show("Could not prepare the video tools. See the log for details.", "Tools Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var existingKeys = new HashSet<string>(_items.Select(i => i.Key.ToLowerInvariant()));
            var existingNames = new HashSet<string>(_items.Select(i => i.Name.Trim().ToLowerInvariant()));
            var dlg = new TutorialEditDialog(editMode: false, existing: null, existingKeys, existingNames) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            SetBusy(true, "Checking storage...");
            string? remuxPath = null;
            try
            {
                // Authoritative bucket check — blocks silently overwriting an orphaned object
                // that isn't in the manifest (so the in-dialog key check wouldn't catch it).
                if (await TutorialService.VideoExistsAsync(dlg.ResultKey))
                {
                    AppMessageBox.Show(
                        $"A file named '{dlg.ResultKey}' already exists in storage but isn't in the list — it may be an orphan. Pick a different filename or reconcile it first.",
                        "File Already Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                txtBusy.Text = "Processing video...";
                remuxPath = await Mp4Tooling.RemuxFastStartAsync(dlg.ResultFilePath!, dlg.ResultInfo!, ScratchDir);

                var progress = new Progress<int>(p => txtBusy.Text = $"Uploading... {p}%");
                txtBusy.Text = "Uploading... 0%";
                await TutorialService.UploadVideoAsync(remuxPath, dlg.ResultKey, progress);

                txtBusy.Text = "Updating list...";
                var manifest = await TutorialService.GetTutorialsAsync();
                manifest.Add(new TutorialItem
                {
                    Key = dlg.ResultKey,
                    Name = dlg.ResultName,
                    Description = dlg.ResultDescription
                });
                await TutorialService.SaveManifestAsync(manifest);

                AppLogger.Info($"Uploaded tutorial '{dlg.ResultKey}' ({dlg.ResultName})",
                    "TutorialManagerDialog.BtnUpload_Click", CurrentUser);

                await ReloadListAsync();
                AppMessageBox.Show($"'{dlg.ResultName}' uploaded. It appears in the Tutorials list immediately.",
                    "Upload Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TutorialManagerDialog.BtnUpload_Click");
                AppMessageBox.Show("Upload failed. See the log for details.", "Upload Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (remuxPath != null) Mp4Tooling.TryDelete(remuxPath);
                SetBusy(false);
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (grdTutorials.SelectedItem is not TutorialItem sel)
            {
                AppMessageBox.Show("Select a tutorial to edit.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Existing titles excluding this item's own, so the user can keep its title
            // (or change only the description) without a false duplicate warning.
            var otherNames = new HashSet<string>(_items
                .Where(i => !string.Equals(i.Key, sel.Key, StringComparison.OrdinalIgnoreCase))
                .Select(i => i.Name.Trim().ToLowerInvariant()));
            var dlg = new TutorialEditDialog(editMode: true, existing: sel, new HashSet<string>(), otherNames) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            SetBusy(true, "Saving...");
            try
            {
                var manifest = await TutorialService.GetTutorialsAsync();
                var entry = manifest.FirstOrDefault(m => string.Equals(m.Key, sel.Key, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    AppMessageBox.Show("That tutorial no longer exists in the manifest. Refreshing.", "Not Found",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    await ReloadListAsync();
                    return;
                }

                entry.Name = dlg.ResultName;
                entry.Description = dlg.ResultDescription;
                await TutorialService.SaveManifestAsync(manifest);

                AppLogger.Info($"Edited tutorial details '{sel.Key}'",
                    "TutorialManagerDialog.BtnEdit_Click", CurrentUser);

                await ReloadListAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TutorialManagerDialog.BtnEdit_Click");
                AppMessageBox.Show("Could not save changes. See the log for details.", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (grdTutorials.SelectedItem is not TutorialItem sel)
            {
                AppMessageBox.Show("Select a tutorial to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = AppMessageBox.Show(
                $"Delete tutorial '{sel.Name}' ({sel.Key})?\n\nThis removes the video and its list entry and cannot be undone.",
                "Delete Tutorial", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            SetBusy(true, "Deleting...");
            try
            {
                // Destructive op: verify admin against Azure (authoritative), not just in-memory state.
                string user = CurrentUser;
                bool isAdmin = await Task.Run(() => AzureDbManager.IsUserAdmin(user));
                if (!isAdmin)
                {
                    AppMessageBox.Show("You must be an administrator to delete tutorials.", "Access Denied",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await TutorialService.DeleteVideoAsync(sel.Key);

                var manifest = await TutorialService.GetTutorialsAsync();
                manifest.RemoveAll(m => string.Equals(m.Key, sel.Key, StringComparison.OrdinalIgnoreCase));
                await TutorialService.SaveManifestAsync(manifest);

                AppLogger.Info($"Deleted tutorial '{sel.Key}' ({sel.Name})",
                    "TutorialManagerDialog.BtnDelete_Click", user);

                await ReloadListAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "TutorialManagerDialog.BtnDelete_Click");
                AppMessageBox.Show("Delete failed. See the log for details.", "Delete Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private void SetBusy(bool busy, string message = "")
        {
            pnlBusy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (busy) txtBusy.Text = message;

            btnUpload.IsEnabled = !busy;
            btnEdit.IsEnabled = !busy;
            btnDelete.IsEnabled = !busy;
            btnRefresh.IsEnabled = !busy;
        }
    }
}
