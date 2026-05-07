using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Syncfusion.UI.Xaml.Grid;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    // Exports the Schedule Module master grid to Excel, honoring current column
    // visibility and order (including UDF header renames from the Schedule UDF Mapping).
    public static class ScheduleMasterExporter
    {
        // Snapshot of one visible grid column, captured on the UI thread before
        // the export runs so the worker can build the file without touching XAML state.
        private sealed class ColumnDef
        {
            public string MappingName { get; init; } = string.Empty;
            public string HeaderText { get; init; } = string.Empty;
            public ColumnKind Kind { get; init; } = ColumnKind.Text;
            public int DecimalDigits { get; init; }
        }

        private enum ColumnKind { Text, Date, Numeric }

        // Build the column snapshot from the live SfDataGrid. Skips hidden columns and
        // anything we can't resolve on ScheduleMasterRow via reflection.
        public static List<object> CaptureColumns(SfDataGrid grid)
        {
            var defs = new List<object>();
            if (grid?.Columns == null) return defs;

            foreach (var col in grid.Columns)
            {
                if (col.IsHidden) continue;
                if (string.IsNullOrWhiteSpace(col.MappingName)) continue;

                var prop = typeof(ScheduleMasterRow).GetProperty(col.MappingName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop == null) continue;

                ColumnKind kind = ColumnKind.Text;
                int digits = 0;

                switch (col)
                {
                    case GridDateTimeColumn:
                        kind = ColumnKind.Date;
                        break;
                    case GridNumericColumn num:
                        kind = ColumnKind.Numeric;
                        digits = num.NumberDecimalDigits;
                        break;
                    default:
                        var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                        if (t == typeof(DateTime)) kind = ColumnKind.Date;
                        else if (t == typeof(double) || t == typeof(decimal) || t == typeof(float))
                        {
                            kind = ColumnKind.Numeric;
                            digits = 1;
                        }
                        break;
                }

                defs.Add(new ColumnDef
                {
                    MappingName = col.MappingName,
                    HeaderText = string.IsNullOrEmpty(col.HeaderText) ? col.MappingName : col.HeaderText,
                    Kind = kind,
                    DecimalDigits = digits
                });
            }
            return defs;
        }

        // Writes the rows to an .xlsx file using the captured columns. Returns row count.
        public static async Task<int> ExportAsync(
            List<ScheduleMasterRow> rows,
            List<object> columnDefs,
            string filePath,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                progress?.Report("Creating Excel file...");

                using var workbook = new XLWorkbook();
                var sheet = workbook.Worksheets.Add("Schedule");

                var defs = new List<ColumnDef>();
                foreach (var d in columnDefs)
                    if (d is ColumnDef cd) defs.Add(cd);

                // Header row
                for (int c = 0; c < defs.Count; c++)
                    sheet.Cell(1, c + 1).Value = defs[c].HeaderText;

                var headerRange = sheet.Range(1, 1, 1, Math.Max(defs.Count, 1));
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2D2D30");
                headerRange.Style.Font.FontColor = XLColor.White;

                // Cache PropertyInfos once
                var props = new PropertyInfo[defs.Count];
                for (int c = 0; c < defs.Count; c++)
                {
                    props[c] = typeof(ScheduleMasterRow).GetProperty(defs[c].MappingName,
                        BindingFlags.Public | BindingFlags.Instance)!;
                }

                int dataRow = 2;
                int count = 0;
                int total = rows.Count;
                int reportEvery = Math.Max(1, total / 100);

                foreach (var row in rows)
                {
                    if (count % reportEvery == 0)
                        progress?.Report($"Exporting row {count + 1:N0} of {total:N0}...");

                    for (int c = 0; c < defs.Count; c++)
                    {
                        var def = defs[c];
                        var value = props[c].GetValue(row);
                        var cell = sheet.Cell(dataRow, c + 1);

                        if (value == null) continue;

                        switch (def.Kind)
                        {
                            case ColumnKind.Date:
                                if (value is DateTime dt)
                                {
                                    cell.Value = dt;
                                    cell.Style.DateFormat.Format = "yyyy-MM-dd";
                                }
                                break;
                            case ColumnKind.Numeric:
                                cell.Value = Convert.ToDouble(value);
                                cell.Style.NumberFormat.Format = def.DecimalDigits > 0
                                    ? "0." + new string('0', def.DecimalDigits)
                                    : "0";
                                break;
                            default:
                                cell.Value = value.ToString() ?? string.Empty;
                                break;
                        }
                    }

                    dataRow++;
                    count++;
                }

                progress?.Report("Auto-fitting columns...");
                sheet.Columns().AdjustToContents();

                progress?.Report("Saving file...");
                try
                {
                    workbook.SaveAs(filePath);
                }
                catch (IOException)
                {
                    throw new IOException(
                        $"Cannot save file - it may be open in another application.\n\n" +
                        $"Please close the file and try again:\n{filePath}");
                }

                return count;
            });
        }
    }
}
