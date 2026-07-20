using System.IO;
using ClosedXML.Excel;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    // Carries one VMS_ProgressSnapshots detail row together with the identifying
    // context of the group it belongs to (owner + week). The context columns let an
    // admin export that spans multiple users/weeks stay unambiguous in a single sheet.
    public class SnapshotExportRow
    {
        public string Username { get; set; } = string.Empty;
        public string WeekEndDate { get; set; } = string.Empty;
        public SnapshotData Data { get; set; } = new SnapshotData();
    }

    // Writes snapshot detail rows to an .xlsx. Shared by the user-facing Manage My
    // Snapshots dialog and the Admin Manage Snapshots dialog. One row per snapshot
    // record; column names match the SnapshotData/DB property names so the file reads
    // like a standard activity export, with AssignedTo + WeekEndDate context columns
    // prepended. Free-text string cells are neutralized against Excel formula
    // injection per Plans/Security_Guidelines.md.
    public static class SnapshotExcelExporter
    {
        // Explicit column order (SnapshotData property names, in declaration order).
        // Kept explicit rather than reflection-derived so the layout is deterministic.
        private static readonly string[] SnapshotColumnOrder = new[]
        {
            "UniqueID", "ProjectID", "Area", "SubArea", "Description",
            "CompType", "PhaseCategory", "PhaseCode", "ROCStep",
            "DwgNO", "RevNO", "SecondDwgNO", "ShtNO",
            "SchedActNO", "SecondActno", "ActStart", "ActFin", "PlanStart", "PlanFin",
            "PercentEntry", "Quantity", "BaseUnit", "UOM",
            "BudgetMHs", "BudgetHoursGroup", "BudgetHoursROC",
            "EarnedMHsRoc", "EarnQtyEntry", "EquivQTY", "EquivUOM",
            "PrevEarnMHs", "PrevEarnQTY",
            "ROCID", "ROCPercent", "ROCBudgetQTY",
            "PipeSize1", "PipeSize2", "PipeGrade", "MtrlSpec", "InsulType", "HtTrace", "PaintCode",
            "Service", "PjtSystem", "PjtSystemNo", "TagNO", "LineNumber",
            "EqmtNO", "HexNO", "ChgOrdNO", "RFINO", "XRay", "ShopField",
            "Estimator", "Aux1", "Aux2", "Aux3", "Notes", "DateTrigger",
            "ClientEquivQty", "ClientBudget", "ClientCustom3",
            "UDF1", "UDF2", "UDF3", "UDF4", "UDF5", "UDF6", "UDF7", "UDF8", "UDF9", "UDF10",
            "UDF11", "UDF12", "UDF13", "UDF14", "UDF15", "UDF16", "UDF17", "UDF20",
            "RespParty", "CreatedBy", "ProgDate", "AzureUploadUtcDate"
        };

        // Cached PropertyInfo lookup so we don't reflect per-cell on large exports.
        private static readonly System.Reflection.PropertyInfo?[] ColumnProps = BuildColumnProps();

        private static System.Reflection.PropertyInfo?[] BuildColumnProps()
        {
            var props = new System.Reflection.PropertyInfo?[SnapshotColumnOrder.Length];
            for (int i = 0; i < SnapshotColumnOrder.Length; i++)
                props[i] = typeof(SnapshotData).GetProperty(SnapshotColumnOrder[i]);
            return props;
        }

        // Writes the rows to filePath. Runs synchronously - callers should invoke on a
        // background thread (Task.Run) since the row set can reach 100k+ records.
        public static void Export(string filePath, List<SnapshotExportRow> rows)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Snapshots");

            // Header row: context columns first, then the snapshot data columns.
            ws.Cell(1, 1).Value = "AssignedTo";
            ws.Cell(1, 2).Value = "WeekEndDate";
            for (int i = 0; i < SnapshotColumnOrder.Length; i++)
                ws.Cell(1, i + 3).Value = SnapshotColumnOrder[i];

            var headerRange = ws.Range(1, 1, 1, SnapshotColumnOrder.Length + 2);
            headerRange.Style.Font.Bold = true;

            int rowIndex = 2;
            foreach (var row in rows)
            {
                ws.Cell(rowIndex, 1).Value = SanitizeForExcel(row.Username);
                ws.Cell(rowIndex, 2).Value = SanitizeForExcel(row.WeekEndDate);

                for (int i = 0; i < ColumnProps.Length; i++)
                {
                    var prop = ColumnProps[i];
                    if (prop == null) continue;
                    WriteCellValue(ws, rowIndex, i + 3, prop.GetValue(row.Data));
                }

                rowIndex++;
            }

            ws.Columns().AdjustToContents();

            try
            {
                workbook.SaveAs(filePath);
            }
            catch (IOException)
            {
                throw new IOException(
                    $"Cannot save file - it may be open in another application.\n\nPlease close the file and try again:\n{filePath}");
            }
        }

        // Writes a value with type-appropriate handling. Doubles are rounded to the
        // app-standard 3 places; strings are formula-injection guarded.
        private static void WriteCellValue(IXLWorksheet ws, int row, int col, object? value)
        {
            switch (value)
            {
                case null:
                    ws.Cell(row, col).Value = string.Empty;
                    break;
                case double d:
                    ws.Cell(row, col).Value = NumericHelper.RoundToPlaces(d);
                    break;
                case int n:
                    ws.Cell(row, col).Value = n;
                    break;
                default:
                    ws.Cell(row, col).Value = SanitizeForExcel(value.ToString());
                    break;
            }
        }

        // Prefixes a leading '=', '+', '-', or '@' with an apostrophe so Excel treats
        // the cell as literal text instead of a formula. See Security_Guidelines.md.
        private static string SanitizeForExcel(string? value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
            char first = value[0];
            return (first == '=' || first == '+' || first == '-' || first == '@')
                ? "'" + value
                : value;
        }
    }
}
