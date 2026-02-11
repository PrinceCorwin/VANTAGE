using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using VANTAGE.Models;
using VANTAGE.Repositories;

namespace VANTAGE.Utilities
{
    // Exports Schedule Reports workbook
    public static class ScheduleReportExporter
    {
        public static async Task ExportAsync(
            List<ScheduleMasterRow> masterRows,
            DateTime weekEndDate,
            string filePath,
            IProgress<string>? progress = null)
        {
            // Get NotIn data before entering Task.Run (async calls)
            progress?.Report("Gathering discrepancy data...");
            var p6NotInMS = await ScheduleRepository.GetP6NotInMSAsync(weekEndDate);
            var msNotInP6 = await ScheduleRepository.GetMSNotInP6Async(weekEndDate);

            await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();

                // Single combined Schedule tab
                CreateScheduleSheet(workbook, masterRows, p6NotInMS, msNotInP6, progress);

                // Save workbook
                progress?.Report("Saving report...");
                workbook.SaveAs(filePath);
            });
        }

        private static void CreateScheduleSheet(
    XLWorkbook workbook,
    List<ScheduleMasterRow> masterRows,
    List<(string SchedActNO, string Description, string WbsId)> p6NotInMS,
    List<string> msNotInP6,
    IProgress<string>? progress)
        {
            progress?.Report("Creating Schedule report...");

            var sheet = workbook.Worksheets.Add("3WLA");
            var redFill = XLColor.FromHtml("#FFC7CE");

            // Headers (22 columns)
            var headers = new[]
            {
            "SchedActNO", "NotInP6", "NotInMS", "Description", "MS_%", "P6_%", "%_Mismatch",
            "V_Start", "V_Finish", "P6_ActualStart", "P6_ActualFinish", "Actual_Mismatch",
            "MS_BudgetMHs", "P6_BudgetMHs", "MH_Mismatch",
            "P6_Start", "P6_Finish", "ThreeWeekStart", "ThreeWeekFinish",
            "MissedStartReason", "MissedFinishReason", "Changed"
};

            for (int col = 0; col < headers.Length; col++)
            {
                sheet.Cell(1, col + 1).Value = headers[col];
                sheet.Cell(1, col + 1).Style.Font.Bold = true;
            }

            // Apply header colors by group
            sheet.Range(1, 1, 1, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");   // Identity/Flags
            sheet.Range(1, 4, 1, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#FCD5B4");   // Basic Info + Percent
            sheet.Range(1, 8, 1, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("#BDD7EE");  // Actuals
            sheet.Range(1, 13, 1, 15).Style.Fill.BackgroundColor = XLColor.FromHtml("#C6EFCE"); // MHs
            sheet.Range(1, 16, 1, 22).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFEB9C"); // 3WLA/Planning

            int row = 2;

            // 1. Write masterRows (InMS = 1)
            foreach (var masterRow in masterRows.OrderBy(r => r.SchedActNO))
            {
                bool pctMismatch = HasPercentMismatch(masterRow);
                bool actualMismatch = HasActualMismatch(masterRow);
                bool mhMismatch = HasMHMismatch(masterRow);
                bool changed = IsDateChanged(masterRow.ThreeWeekStart, masterRow.P6_Start) ||
                               IsDateChanged(masterRow.ThreeWeekFinish, masterRow.P6_Finish);

                sheet.Cell(row, 1).Value = masterRow.SchedActNO ?? string.Empty;
                sheet.Cell(row, 2).Value = "False";
                sheet.Cell(row, 3).Value = "False";
                sheet.Cell(row, 4).Value = masterRow.Description ?? string.Empty;
                sheet.Cell(row, 5).Value = masterRow.MS_PercentComplete;
                sheet.Cell(row, 6).Value = masterRow.P6_PercentComplete;
                sheet.Cell(row, 7).Value = pctMismatch ? "True" : "False";
                sheet.Cell(row, 8).Value = FormatDate(masterRow.V_Start);
                sheet.Cell(row, 9).Value = FormatDate(masterRow.V_Finish);
                sheet.Cell(row, 10).Value = FormatDate(masterRow.P6_ActualStart);
                sheet.Cell(row, 11).Value = FormatDate(masterRow.P6_ActualFinish);
                sheet.Cell(row, 12).Value = actualMismatch ? "True" : "False";
                sheet.Cell(row, 13).Value = Math.Round(masterRow.MS_BudgetMHs, 2);
                sheet.Cell(row, 14).Value = Math.Round(masterRow.P6_BudgetMHs, 2);
                sheet.Cell(row, 15).Value = mhMismatch ? "True" : "False";
                sheet.Cell(row, 16).Value = FormatDate(masterRow.P6_Start);
                sheet.Cell(row, 17).Value = FormatDate(masterRow.P6_Finish);
                sheet.Cell(row, 18).Value = FormatDate(masterRow.ThreeWeekStart);
                sheet.Cell(row, 19).Value = FormatDate(masterRow.ThreeWeekFinish);
                sheet.Cell(row, 20).Value = masterRow.MissedStartReason ?? string.Empty;
                sheet.Cell(row, 21).Value = masterRow.MissedFinishReason ?? string.Empty;
                sheet.Cell(row, 22).Value = changed ? "True" : "False";

                // Apply red fill only where True
                if (pctMismatch) sheet.Cell(row, 7).Style.Fill.BackgroundColor = redFill;
                if (actualMismatch) sheet.Cell(row, 12).Style.Fill.BackgroundColor = redFill;
                if (mhMismatch) sheet.Cell(row, 15).Style.Fill.BackgroundColor = redFill;
                if (changed) sheet.Cell(row, 22).Style.Fill.BackgroundColor = redFill;

                row++;
            }

            // 2. Write P6 Not In MS rows
            foreach (var p6Row in p6NotInMS.OrderBy(r => r.SchedActNO))
            {
                sheet.Cell(row, 1).Value = p6Row.SchedActNO;
                sheet.Cell(row, 2).Value = "False";
                sheet.Cell(row, 3).Value = "True";
                sheet.Cell(row, 3).Style.Fill.BackgroundColor = redFill;
                sheet.Cell(row, 4).Value = p6Row.Description;
                sheet.Cell(row, 7).Value = "False";
                sheet.Cell(row, 12).Value = "False";
                sheet.Cell(row, 15).Value = "False";
                sheet.Cell(row, 22).Value = "False";

                row++;
            }

            // 3. Write MS Not In P6 rows
            foreach (var actNo in msNotInP6.OrderBy(a => a))
            {
                sheet.Cell(row, 1).Value = actNo;
                sheet.Cell(row, 2).Value = "True";
                sheet.Cell(row, 2).Style.Fill.BackgroundColor = redFill;
                sheet.Cell(row, 3).Value = "False";
                sheet.Cell(row, 7).Value = "False";
                sheet.Cell(row, 12).Value = "False";
                sheet.Cell(row, 15).Value = "False";
                sheet.Cell(row, 22).Value = "False";

                row++;
            }

            sheet.Columns().AdjustToContents();
        }

        private static string FormatDate(DateTime? date)
        {
            return date?.ToString("M/d/yyyy") ?? string.Empty;
        }

        private static bool IsDateChanged(DateTime? threeWeekDate, DateTime? plannedDate)
        {
            if (threeWeekDate == null)
                return false;

            if (plannedDate == null)
                return true;

            return threeWeekDate.Value.Date != plannedDate.Value.Date;
        }

        private static bool HasActualMismatch(ScheduleMasterRow row)
        {
            // Start mismatch
            bool startMismatch = false;
            if (row.V_Start == null && row.P6_ActualStart != null)
                startMismatch = true;
            else if (row.V_Start != null && row.P6_ActualStart == null)
                startMismatch = true;
            else if (row.V_Start != null && row.P6_ActualStart != null &&
                     row.V_Start.Value.Date != row.P6_ActualStart.Value.Date)
                startMismatch = true;

            // Finish mismatch
            bool finishMismatch = false;
            if (row.V_Finish == null && row.P6_ActualFinish != null)
                finishMismatch = true;
            else if (row.V_Finish != null && row.P6_ActualFinish == null)
                finishMismatch = true;
            else if (row.V_Finish != null && row.P6_ActualFinish != null &&
                     row.V_Finish.Value.Date != row.P6_ActualFinish.Value.Date)
                finishMismatch = true;

            return startMismatch || finishMismatch;
        }
        private static bool HasPercentMismatch(ScheduleMasterRow row)
        {
            return Math.Abs(row.MS_PercentComplete - row.P6_PercentComplete) > 0.5;
        }

        private static bool HasMHMismatch(ScheduleMasterRow row)
        {
            return Math.Abs(row.MS_BudgetMHs - row.P6_BudgetMHs) > 0.01;
        }
    }
}