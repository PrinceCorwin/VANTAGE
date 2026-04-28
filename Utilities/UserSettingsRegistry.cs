using System.Collections.Generic;

namespace VANTAGE.Utilities
{
    // Central catalog of every user-facing UserSettings key.
    // Used by the Reset User Settings dialog to show groups with natural-language labels.
    // Callers may also consult GetDefault / GetDataType to consolidate inline fallbacks.
    //
    // Rule: if a setting already has its own manager UI (Theme submenu, Grid Layouts dialog,
    // Analysis chart filter Reset button, etc.), it is NOT registered here. See the deny-list
    // commented at the bottom of this file for the full exclusion list and reasons.
    //
    // When a session adds/removes/renames a UserSetting key, the finisher skill (Step 3.7)
    // reminds the agent to keep this file in sync.
    public static class UserSettingsRegistry
    {
        public class SettingEntry
        {
            public string Key { get; }
            public string DefaultValue { get; }
            public string DataType { get; }
            public string Description { get; }

            public SettingEntry(string key, string defaultValue, string dataType, string description)
            {
                Key = key;
                DefaultValue = defaultValue;
                DataType = dataType;
                Description = description;
            }
        }

        public class SettingGroup
        {
            public string Label { get; }
            public string Tooltip { get; }
            public IReadOnlyList<SettingEntry> Entries { get; }

            public SettingGroup(string label, string tooltip, IReadOnlyList<SettingEntry> entries)
            {
                Label = label;
                Tooltip = tooltip;
                Entries = entries;
            }
        }

        // Individual entries indexed by key for lookup.
        private static readonly Dictionary<string, SettingEntry> _byKey = new();

        // Ordered list of groups shown in the Reset dialog.
        private static readonly List<SettingGroup> _groups = BuildGroups();

        private static List<SettingGroup> BuildGroups()
        {
            var groups = new List<SettingGroup>
            {
                new SettingGroup(
                    "Help sidebar width",
                    "Restores the help sidebar to its default width (400 px).",
                    Register(
                        new SettingEntry("SidePanel.Width", "400", "int",
                            "Width of the help sidebar in pixels; adjusted by dragging the splitter.")
                    )),

                new SettingGroup(
                    "Progress grid preferences",
                    "Column layout, widths, freeze count, custom percent buttons, and summary column.",
                    Register(
                        new SettingEntry("ProgressGrid.PreferencesJson", "", "json",
                            "Saved column order, widths, and sort order for the Progress grid."),
                        new SettingEntry("ProgressGrid.FrozenColumnCount", "0", "int",
                            "Number of frozen (pinned) columns at the left of the Progress grid."),
                        new SettingEntry("CustomPercentButton1", "", "string",
                            "Custom percent value for the first quick-set button in the Progress grid."),
                        new SettingEntry("CustomPercentButton2", "", "string",
                            "Custom percent value for the second quick-set button in the Progress grid."),
                        new SettingEntry("SummaryStats.BudgetColumn", "BudgetMHs", "string",
                            "Which budget column drives the summary stats strip above the Progress grid.")
                    )),

                new SettingGroup(
                    "Schedule grid preferences",
                    "Master and detail grid column layouts plus the splitter positions.",
                    Register(
                        new SettingEntry("ScheduleGrid.PreferencesJson", "", "json",
                            "Saved column order, widths, and sort order for the Schedule master grid."),
                        new SettingEntry("ScheduleDetailGrid.PreferencesJson", "", "json",
                            "Saved column order, widths, and sort order for the Schedule detail grid."),
                        new SettingEntry("ScheduleView_MasterGridHeight", "", "string",
                            "Height of the Schedule master grid area (splitter position)."),
                        new SettingEntry("ScheduleView_DetailGridHeight", "", "string",
                            "Height of the Schedule detail grid area (splitter position).")
                    )),

                new SettingGroup(
                    "Analysis grid layout",
                    "The Analysis tab's section splitter positions.",
                    Register(
                        new SettingEntry("AnalysisGridLayout", "", "json",
                            "Row and column sizes of the Analysis tab's section grid.")
                    )),

                new SettingGroup(
                    "Progress Books splitter",
                    "Splitter position between the Progress Books list and preview.",
                    Register(
                        new SettingEntry("ProgressBook.SplitterRatio", "", "string",
                            "Left panel width ratio (0.0 – 1.0) in the Progress Books view.")
                    )),

                new SettingGroup(
                    "Work Package splitter",
                    "Splitter position between the Work Package list and detail panels.",
                    Register(
                        new SettingEntry("WorkPackage.SplitterRatio", "", "string",
                            "Left panel width ratio (0.0 – 1.0) in the Work Package view.")
                    )),

                new SettingGroup(
                    "Progress Scan dialog size",
                    "Window size and column widths of the AI Progress Scan dialog.",
                    Register(
                        new SettingEntry("ProgressScanDialog.Width", "", "string",
                            "Saved width (px) of the AI Progress Scan dialog."),
                        new SettingEntry("ProgressScanDialog.Height", "", "string",
                            "Saved height (px) of the AI Progress Scan dialog."),
                        new SettingEntry("ProgressScanDialog.ColumnWidths", "", "string",
                            "Comma-separated column widths inside the AI Progress Scan dialog.")
                    )),

                new SettingGroup(
                    "Re-show \"don't show again\" dialogs",
                    "Restores reminder/instruction dialogs that you previously ticked \"do not show again\" on (currently: VP vs Vtg Report prep dialog).",
                    Register(
                        new SettingEntry("SkipVPvsVtgPrepDialog", "false", "bool",
                            "Skips the VP vs Vtg Report prep/instructions dialog when true.")
                    )),
            };

            return groups;
        }

        // Adds entries to the by-key lookup and returns them as an array for the group constructor.
        private static SettingEntry[] Register(params SettingEntry[] entries)
        {
            foreach (var e in entries) _byKey[e.Key] = e;
            return entries;
        }

        // Groups shown in the Reset User Settings dialog, in display order.
        public static IReadOnlyList<SettingGroup> GetResetableGroups() => _groups;

        // Default value for a registered key, or null if not registered (i.e. on the deny-list).
        public static string? GetDefault(string key)
            => _byKey.TryGetValue(key, out var e) ? e.DefaultValue : null;

        // Data type string for a registered key, or null if not registered.
        public static string? GetDataType(string key)
            => _byKey.TryGetValue(key, out var e) ? e.DataType : null;

        // True if the key appears in a reset group (i.e. the user can safely reset it via the dialog).
        public static bool IsUserFacing(string key) => _byKey.ContainsKey(key);

        // ================================================================
        // DENY-LIST (informational — not stored because absence from _byKey is the check).
        // Do NOT add these to groups. Each has a manager UI elsewhere OR is system-critical.
        // ----------------------------------------------------------------
        //   Theme                          — Theme submenu in Settings popup
        //   LastView                       — auto-updated whenever you navigate to a view; no reset needed
        //   LastSyncUtcDate                — system bookkeeping (deleting triggers full re-sync)
        //   LastSeenVersion                — internal release-notes gating
        //   GridLayouts.Index              — Grid Layouts dialog
        //   GridLayout.{name}.Data         — Grid Layouts dialog
        //   GridLayouts.ActiveLayout       — Grid Layouts dialog
        //   ImportProfiles.Index           — Import from AI Takeoff dialog
        //   ImportProfile.{name}           — Import from AI Takeoff dialog
        //   Takeoff.LastConfigKey          — config dropdown in the AI Takeoff view
        //   UserFilters.Progress           — Manage Filters dialog
        //   AnalysisFilter_*  (12 keys)    — Reset button on Analysis chart filters panel
        //   Schedule.UDFMappings           — Schedule UDF Column Mappings dialog
        //   Schedule.LookaheadWeeks        — Lookahead ComboBox in the Schedule toolbar
        //   AnalysisGroupField             — dropdown in the Analysis tab
        //   AnalysisCurrentUserOnly        — checkbox in the Analysis tab
        //   AnalysisSelectedProjects       — project picker in the Analysis tab
        //   AnalysisVisual_1_1             — chart type dropdown in the Analysis tab
        //   AnalysisXAxis_1_1              — X-axis dropdown in the Analysis tab
        //   AnalysisField_1_1              — Y-axis dropdown in the Analysis tab
        //   ProgressBook.ExcludeCompleted  — checkbox in the Progress Books view
        //   ProgressBook.IncludeAllUsers   — checkbox in the Progress Books view
        //   WorkPackage.LastProjectID      — dropdown in the Work Package view
        //   WorkPackage.LastWPTemplateID   — dropdown in the Work Package view
        //   WorkPackage.LastPKGManager     — dropdown in the Work Package view
        //   WorkPackage.LastScheduler      — dropdown in the Work Package view
        //   WorkPackage.LastOutputPath     — path picker in the Work Package view
        //   WorkPackage.LastLogoPath       — path picker in the Work Package view
        //   WorkPackage.WPNamePattern      — text box in the Work Package view
        //   WorkPackage.DrawingsLocalPath  — path picker in the Work Package view
        //   MyRecordsOnlySync              — checkbox in the Sync dialog
        // ================================================================
    }
}
