using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using Syncfusion.SfSkinManager;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    // Dialog letting the user selectively clear groups of UserSettings rows.
    // Groups and their member keys come from UserSettingsRegistry; anything not
    // in the registry is considered managed elsewhere or system-critical and is
    // never touched by this dialog.
    public partial class ResetUserSettingsDialog : Window
    {
        public class GroupRow : INotifyPropertyChanged
        {
            private bool _isChecked;
            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked == value) return;
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
            public string Label { get; set; } = string.Empty;
            public string Tooltip { get; set; } = string.Empty;
            public string MemberSummary { get; set; } = string.Empty;
            public IReadOnlyList<UserSettingsRegistry.SettingEntry> Entries { get; set; } = new List<UserSettingsRegistry.SettingEntry>();

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private readonly List<GroupRow> _rows = new();

        // Names of keys that were actually removed from UserSettings, exposed for the caller to log.
        public IReadOnlyList<string> ResetKeys { get; private set; } = new List<string>();

        public ResetUserSettingsDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            foreach (var group in UserSettingsRegistry.GetResetableGroups())
            {
                _rows.Add(new GroupRow
                {
                    Label = group.Label,
                    Tooltip = group.Tooltip,
                    MemberSummary = BuildMemberSummary(group.Entries),
                    Entries = group.Entries,
                });
            }
            groupsList.ItemsSource = _rows;
        }

        private static string BuildMemberSummary(IReadOnlyList<UserSettingsRegistry.SettingEntry> entries)
        {
            if (entries.Count == 1) return "1 setting";
            return $"{entries.Count} settings";
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var selected = new List<GroupRow>();
            foreach (var r in _rows) if (r.IsChecked) selected.Add(r);

            if (selected.Count == 0)
            {
                AppMessageBox.Show("No groups selected.", "Reset User Settings",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int totalKeys = 0;
            foreach (var g in selected) totalKeys += g.Entries.Count;

            string message = selected.Count == 1
                ? $"Reset \"{selected[0].Label}\"?\n\nThis will clear {totalKeys} setting{(totalKeys == 1 ? "" : "s")} and cannot be undone."
                : $"Reset {selected.Count} groups?\n\nThis will clear {totalKeys} settings total and cannot be undone.";

            var result = AppMessageBox.Show(this, message, "Reset User Settings",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes) return;

            var removed = new List<string>();
            foreach (var g in selected)
            {
                foreach (var entry in g.Entries)
                {
                    if (SettingsManager.RemoveUserSetting(entry.Key))
                        removed.Add(entry.Key);
                }
            }

            ResetKeys = removed;

            AppMessageBox.Show(this,
                "Settings reset. Grid layouts reload right away; other settings (splitters, dialog sizes, dismissed dialogs) apply the next time you open each affected view or restart Vantage.",
                "Reset User Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
