using System.Windows;
using System.Windows.Controls;
using Syncfusion.SfSkinManager;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Dialogs
{
    public partial class ScheduleUDFMappingDialog : Window
    {
        private readonly TextBox[] _primaryTextboxes;
        private readonly TextBox[] _secondaryTextboxes;
        private readonly TextBox[] _displayTextboxes;

        public ScheduleUDFMappingDialog()
        {
            InitializeComponent();
            SfSkinManager.SetTheme(this, new Theme(ThemeManager.GetSyncfusionThemeName()));

            // Store control references in arrays for easy iteration
            _primaryTextboxes = new[] { txtPrimary1, txtPrimary2, txtPrimary3, txtPrimary4, txtPrimary5 };
            _secondaryTextboxes = new[] { txtSecondary1, txtSecondary2, txtSecondary3, txtSecondary4, txtSecondary5 };
            _displayTextboxes = new[] { txtDisplay1, txtDisplay2, txtDisplay3, txtDisplay4, txtDisplay5 };

            LoadMappings();
        }

        private void LoadMappings()
        {
            var config = SettingsManager.GetScheduleUDFMappings();

            for (int i = 0; i < 5; i++)
            {
                var mapping = config.Mappings.Count > i ? config.Mappings[i] : null;
                if (mapping != null)
                {
                    _primaryTextboxes[i].Text = mapping.PrimaryHeader;
                    _secondaryTextboxes[i].Text = mapping.SecondaryHeader;
                    _displayTextboxes[i].Text = mapping.DisplayName;
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var config = new ScheduleUDFMappingConfig();

            for (int i = 0; i < 5; i++)
            {
                var primary = _primaryTextboxes[i].Text?.Trim() ?? string.Empty;
                var secondary = _secondaryTextboxes[i].Text?.Trim() ?? string.Empty;

                config.Mappings.Add(new ScheduleUDFMapping
                {
                    TargetColumn = $"SchedUDF{i + 1}",
                    // A mapping is enabled if it has any header configured
                    IsEnabled = !string.IsNullOrEmpty(primary) || !string.IsNullOrEmpty(secondary),
                    PrimaryHeader = primary,
                    SecondaryHeader = secondary,
                    DisplayName = _displayTextboxes[i].Text?.Trim() ?? string.Empty
                });
            }

            SettingsManager.SetScheduleUDFMappings(config);
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
