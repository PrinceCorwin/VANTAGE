using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    public static class ScheduleExcelExporter
    {
        // Exports schedule data to P6 format
        // Returns the number of rows exported
        public static async Task<int> ExportToP6Async(
            List<ScheduleMasterRow> masterRows,
            string filePath,
            TimeSpan startTime,
            TimeSpan finishTime,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                progress?.Report("Creating P6 export file...");

                using var workbook = new XLWorkbook();

                // Create TASK sheet
                var taskSheet = workbook.Worksheets.Add("TASK");
                int rowCount = WriteTaskSheet(taskSheet, masterRows, startTime, finishTime, progress);

                // Create USERDATA sheet
                var userDataSheet = workbook.Worksheets.Add("USERDATA");
                WriteUserDataSheet(userDataSheet);

                progress?.Report("Saving file...");
                try
                {
                    workbook.SaveAs(filePath);
                }
                catch (IOException)
                {
                    throw new IOException($"Cannot save file - it may be open in another application.\n\nPlease close the file and try again:\n{filePath}");
                }

                return rowCount;
            });
        }
        private static int WriteTaskSheet(IXLWorksheet sheet, List<ScheduleMasterRow> masterRows, TimeSpan startTime, TimeSpan finishTime, IProgress<string>? progress)
        {
            // Row 1: Technical headers (P6 field names)
            sheet.Cell(1, 1).Value = "task_code";
            sheet.Cell(1, 2).Value = "status_code";
            sheet.Cell(1, 3).Value = "wbs_id";
            sheet.Cell(1, 4).Value = "task_name";
            sheet.Cell(1, 5).Value = "act_start_date";
            sheet.Cell(1, 6).Value = "act_end_date";
            sheet.Cell(1, 7).Value = "complete_pct";

            // Row 2: Friendly names (descriptions)
            sheet.Cell(2, 1).Value = "Activity ID";
            sheet.Cell(2, 2).Value = "Activity Status";
            sheet.Cell(2, 3).Value = "WBS Code";
            sheet.Cell(2, 4).Value = "Activity Name";
            sheet.Cell(2, 5).Value = "Actual Start";
            sheet.Cell(2, 6).Value = "Actual Finish";
            sheet.Cell(2, 7).Value = "Activity % Complete";

            // Style header rows
            var headerRange = sheet.Range(1, 1, 2, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2D2D30");
            headerRange.Style.Font.FontColor = XLColor.White;

            // Row 3+: Data
            int dataRow = 3;
            int count = 0;

            foreach (var row in masterRows)
            {
                progress?.Report($"Exporting row {count + 1} of {masterRows.Count}...");

                // task_code = SchedActNO
                sheet.Cell(dataRow, 1).Value = row.SchedActNO ?? string.Empty;

                // status_code = derived from MS_PercentComplete
                sheet.Cell(dataRow, 2).Value = DeriveStatusCode(row.MS_PercentComplete);

                // wbs_id = WbsId
                sheet.Cell(dataRow, 3).Value = row.WbsId ?? string.Empty;

                // task_name = Description
                sheet.Cell(dataRow, 4).Value = row.Description ?? string.Empty;

                // act_start_date = V_Start (only if activity has started)
                if (row.V_Start.HasValue && row.MS_PercentComplete > 0)
                {
                    var startDateTime = row.V_Start.Value.Date.Add(startTime);
                    sheet.Cell(dataRow, 5).Value = startDateTime;
                    sheet.Cell(dataRow, 5).Style.DateFormat.Format = "M/d/yyyy HH:mm";
                }

                // act_end_date = V_Finish (only if activity is complete)
                if (row.V_Finish.HasValue && row.MS_PercentComplete >= 100)
                {
                    var finishDateTime = row.V_Finish.Value.Date.Add(finishTime);
                    sheet.Cell(dataRow, 6).Value = finishDateTime;
                    sheet.Cell(dataRow, 6).Style.DateFormat.Format = "M/d/yyyy HH:mm";
                }

                // complete_pct = MS_PercentComplete (already 0-100 scale)
                sheet.Cell(dataRow, 7).Value = Math.Round(row.MS_PercentComplete, 0);

                dataRow++;
                count++;
            }

            // Auto-fit columns
            sheet.Columns().AdjustToContents();

            return count;
        }

        private static string DeriveStatusCode(double percentComplete)
        {
            if (percentComplete <= 0)
                return "Not Started";
            if (percentComplete >= 100)
                return "Complete";
            return "In Progress";
        }

        private static void WriteUserDataSheet(IXLWorksheet sheet)
        {
            // A1: user_data
            sheet.Cell(1, 1).Value = "user_data";

            // A2: UserSettings Do Not Edit
            sheet.Cell(2, 1).Value = "UserSettings Do Not Edit";

            // A3: Multi-line settings with soft returns (line feeds within cell)
            string settings = "DurationQtyType=QT_Day\n" +
                              "ShowAsPercentage=0\n" +
                              "SmallScaleQtyType=QT_Hour\n" +
                              "DateFormat=m/d/yyyy\n" +
                              "CurrencyFormat=US Dollar";

            sheet.Cell(3, 1).Value = settings;
            sheet.Cell(3, 1).Style.Alignment.WrapText = true;

            // Auto-fit column A
            sheet.Column(1).AdjustToContents();
        }
    }
}