using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Syncfusion.Pdf;
using VANTAGE.Data;
using VANTAGE.Models;
using VANTAGE.Models.ProgressBook;
using VANTAGE.Utilities;

namespace VANTAGE.Services.ProgressBook
{
    // Shared progress-book data gathering and PDF generation. This is the single source of truth
    // for how a layout's filter/exclude settings turn into activities + cover-page totals, used
    // by both the Progress Books module's Generate dialog and the Work Packages generator (which
    // embeds a progress book scoped to the work package being generated).
    public static class ProgressBookGenerationService
    {
        // Everything a caller needs after resolving a configuration's filter.
        public class ProgressBookData
        {
            public List<Activity> Activities { get; set; } = new();  // included rows (post exclude-completed)
            public CoverPageData CoverPageData { get; set; } = new();
            public int AllActivitiesCount { get; set; }               // before exclude-completed, after exclude-column
            public int UnsyncedCount { get; set; }                    // rows with ActivityID == 0
        }

        // Load activities and build the cover-page totals for a configuration. Mirrors exactly
        // what the Generate dialog used to do inline.
        public static async Task<ProgressBookData> LoadDataAsync(ProgressBookConfiguration config, string username)
        {
            var result = new ProgressBookData();

            // Query ALL matching activities first (cover-page totals need completed rows too).
            var (allActivities, _) = await ActivityRepository.GetAllActivitiesAsync(BuildWhereClause(config, username));

            // Drop excluded column values if configured.
            if (!string.IsNullOrEmpty(config.ExcludeColumn) && config.ExcludeValues.Count > 0)
            {
                allActivities = allActivities
                    .Where(a => !config.ExcludeValues.Contains(GetActivityFieldValue(a, config.ExcludeColumn) ?? ""))
                    .ToList();
            }

            var coverPageData = new CoverPageData
            {
                TotalBudgetMHs = allActivities.Sum(a => a.BudgetMHs),
                TotalEarnedMHs = allActivities.Sum(a => a.EarnMHsCalc),
                LastSyncDisplay = GetLastSyncDisplay()
            };

            var completedActivities = allActivities.Where(a => a.PercentEntry >= 100).ToList();
            var activities = config.ExcludeCompleted
                ? allActivities.Where(a => a.PercentEntry < 100).ToList()
                : allActivities;

            coverPageData.IncludedCount = activities.Count;
            if (config.ExcludeCompleted)
            {
                coverPageData.ExcludedCompletedCount = completedActivities.Count;
                coverPageData.ExcludedCompletedBudgetMHs = completedActivities.Sum(a => a.BudgetMHs);
                coverPageData.ExcludedCompletedEarnedMHs = completedActivities.Sum(a => a.EarnMHsCalc);
            }
            coverPageData.LastUpdatedDisplay = GetLastUpdatedDisplay(activities);

            result.Activities = activities;
            result.CoverPageData = coverPageData;
            result.AllActivitiesCount = allActivities.Count;
            result.UnsyncedCount = allActivities.Count(a => a.ActivityID == 0);
            return result;
        }

        // Generate a progress book PDF for a configuration. Returns null when nothing matches the
        // filter (so a caller merging into a work package can simply omit it). bookName is the
        // label shown in the PDF header.
        public static async Task<PdfDocument?> GenerateAsync(
            ProgressBookConfiguration config, string projectId, string bookName, string username)
        {
            var data = await LoadDataAsync(config, username);
            if (data.Activities.Count == 0)
                return null;

            var generator = new ProgressBookPdfGenerator();
            var projectDescription = ProjectCache.GetProjectDescription(projectId);
            return generator.Generate(config, data.Activities, bookName, projectId, projectDescription, data.CoverPageData);
        }

        // Build the activity WHERE clause from a configuration's filter + assignee scope.
        public static string BuildWhereClause(ProgressBookConfiguration config, string username)
        {
            if (!string.IsNullOrEmpty(config.FilterField) && !string.IsNullOrEmpty(config.FilterValue))
            {
                var safeValue = config.FilterValue.Replace("'", "''");
                return config.IncludeAllUsers
                    ? $"{config.FilterField} = '{safeValue}'"
                    : $"AssignedTo = '{username}' AND {config.FilterField} = '{safeValue}'";
            }

            // No filter - all records, optionally scoped to the current user.
            return config.IncludeAllUsers ? "1=1" : $"AssignedTo = '{username}'";
        }

        // Read an Activity field by name via reflection.
        public static string? GetActivityFieldValue(Activity activity, string fieldName)
        {
            try
            {
                var prop = typeof(Activity).GetProperty(fieldName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                return prop?.GetValue(activity)?.ToString();
            }
            catch
            {
                return null;
            }
        }

        // Last successful sync (local time) from user settings, or "Never".
        public static string GetLastSyncDisplay()
        {
            var lastSyncString = SettingsManager.GetUserSetting("LastSyncUtcDate");
            if (string.IsNullOrEmpty(lastSyncString))
                return "Never";
            if (DateTime.TryParse(lastSyncString, out DateTime lastSyncUtc))
                return lastSyncUtc.ToLocalTime().ToString("M/d/yyyy HH:mm");
            return "Never";
        }

        // Max UpdatedUtcDate across the given activities (local time), or "N/A".
        public static string GetLastUpdatedDisplay(List<Activity> activities)
        {
            DateTime? maxDate = null;
            foreach (var activity in activities)
            {
                if (activity.UpdatedUtcDate.HasValue &&
                    (!maxDate.HasValue || activity.UpdatedUtcDate.Value > maxDate.Value))
                {
                    maxDate = activity.UpdatedUtcDate.Value;
                }
            }
            return maxDate.HasValue ? maxDate.Value.ToLocalTime().ToString("M/d/yyyy HH:mm") : "N/A";
        }
    }
}
