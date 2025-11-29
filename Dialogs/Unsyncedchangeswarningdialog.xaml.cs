using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace VANTAGE.Dialogs
{
    /// <summary>
    /// Warning dialog shown when user is about to lose unsaved local edits
    /// by excluding projects from sync.
    /// </summary>
    public partial class UnsyncedChangesWarningDialog : Window
    {
        private readonly List<ProjectDirtyInfo> _projectsWithDirtyRecords;

        /// <summary>
        /// Initialize the warning dialog with project dirty record counts.
        /// </summary>
        /// <param name="dirtyCountsByProject">Dictionary of ProjectID to count of LocalDirty=1 records</param>
        /// <param name="projectNames">Dictionary of ProjectID to ProjectName for display</param>
        public UnsyncedChangesWarningDialog(
            Dictionary<string, int> dirtyCountsByProject,
            Dictionary<string, string> projectNames)
        {
            InitializeComponent();

            // Build display list
            _projectsWithDirtyRecords = dirtyCountsByProject
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new ProjectDirtyInfo
                {
                    ProjectID = kvp.Key,
                    ProjectName = projectNames.TryGetValue(kvp.Key, out var name) ? name : "Unknown Project",
                    DirtyCount = kvp.Value
                })
                .ToList();

            projectList.ItemsSource = _projectsWithDirtyRecords;

            // Update summary text with totals
            int totalDirty = _projectsWithDirtyRecords.Sum(p => p.DirtyCount);
            int projectCount = _projectsWithDirtyRecords.Count;

            txtSummary.Text = $"Total: {totalDirty:N0} unsaved change{(totalDirty != 1 ? "s" : "")} " +
                              $"across {projectCount} project{(projectCount != 1 ? "s" : "")}. " +
                              $"Click 'No, Go Back' to keep these changes.";
        }

        private void BtnGoBack_Click(object sender, RoutedEventArgs e)
        {
            // User wants to go back and include these projects
            DialogResult = false;
            Close();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            // User confirms they want to lose these changes
            DialogResult = true;
            Close();
        }
    }

    /// <summary>
    /// Display model for projects with unsaved changes
    /// </summary>
    public class ProjectDirtyInfo
    {
        public string? ProjectID { get; set; }
        public string? ProjectName { get; set; }
        public int DirtyCount { get; set; }
    }
}