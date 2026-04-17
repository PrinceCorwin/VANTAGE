using ClosedXML.Excel;

namespace VANTAGE.Utilities
{
    // Augments a JC Labor Productivity report with two new columns (Vtg Earned, Vtg Budget)
    // sourced from Azure VMS_Activities, aggregated by (ProjectID, PhaseCode).
    // Highlights pair-wise mismatches > 1% against Est Hours / JTD ERN.
    public static class VPvsVtgReportAugmenter
    {
        private const string SummarySheetName = "Summary";
        private const string JobHeader = "Job";
        private const string PhaseHeader = "Phase";
        private const string EstHoursHeader = "Est Hours";
        private const string JtdErnHeader = "JTD ERN";
        private const string VtgBudgetHeader = "Vtg Budget";
        private const string VtgEarnedHeader = "Vtg Earned";
        private const string NotFound = "Not Found";
        private const double MismatchThreshold = 0.01; // 1%

        public class Result
        {
            public int DataRows { get; set; }
            public int MatchedRows { get; set; }
            public int NotFoundRows { get; set; }
            public int BudgetMismatchRows { get; set; }
            public int EarnedMismatchRows { get; set; }
            public string OutputPath { get; set; } = string.Empty;
        }

        // Reads Azure VMS_Activities, aggregates by normalized (ProjectID, PhaseCode),
        // writes two new columns to the Summary sheet, and saves to outputPath.
        public static Result Augment(string inputPath, string outputPath)
        {
            var aggregates = LoadAggregatesFromAzure();

            var result = new Result { OutputPath = outputPath };

            using var workbook = new XLWorkbook(inputPath);
            if (!workbook.Worksheets.TryGetWorksheet(SummarySheetName, out var sheet))
                throw new InvalidOperationException($"'{SummarySheetName}' sheet not found in the workbook.");

            int headerRow = 1;
            int jobCol = FindHeaderColumn(sheet, headerRow, JobHeader);
            int phaseCol = FindHeaderColumn(sheet, headerRow, PhaseHeader);
            int estHoursCol = FindHeaderColumn(sheet, headerRow, EstHoursHeader);
            int jtdErnCol = FindHeaderColumn(sheet, headerRow, JtdErnHeader);

            if (jobCol == 0) throw new InvalidOperationException($"Required header '{JobHeader}' not found on row {headerRow}.");
            if (phaseCol == 0) throw new InvalidOperationException($"Required header '{PhaseHeader}' not found on row {headerRow}.");
            if (estHoursCol == 0) throw new InvalidOperationException($"Required header '{EstHoursHeader}' not found on row {headerRow}.");
            if (jtdErnCol == 0) throw new InvalidOperationException($"Required header '{JtdErnHeader}' not found on row {headerRow}.");

            int lastUsedCol = sheet.LastColumnUsed()?.ColumnNumber() ?? jtdErnCol;
            int vtgBudgetCol = lastUsedCol + 1;
            int vtgEarnedCol = lastUsedCol + 2;

            var headerTemplate = sheet.Cell(headerRow, jtdErnCol);
            var budgetHeaderCell = sheet.Cell(headerRow, vtgBudgetCol);
            budgetHeaderCell.Value = VtgBudgetHeader;
            budgetHeaderCell.Style = headerTemplate.Style;

            var earnedHeaderCell = sheet.Cell(headerRow, vtgEarnedCol);
            earnedHeaderCell.Value = VtgEarnedHeader;
            earnedHeaderCell.Style = headerTemplate.Style;

            sheet.Column(vtgBudgetCol).Width = 14;
            sheet.Column(vtgEarnedCol).Width = 14;

            var redFill = XLColor.FromHtml("#FF8989");
            var orangeFill = XLColor.FromHtml("#FFC000");
            var greenFill = XLColor.FromHtml("#63BE7B");

            int lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;
            for (int row = headerRow + 1; row <= lastRow; row++)
            {
                string projectRaw = sheet.Cell(row, jobCol).GetString();
                string phaseRaw = sheet.Cell(row, phaseCol).GetString();

                if (string.IsNullOrWhiteSpace(projectRaw) && string.IsNullOrWhiteSpace(phaseRaw))
                    continue;

                result.DataRows++;

                string projectKey = NormalizeKey(projectRaw);
                string phaseKey = NormalizeKey(phaseRaw);
                var key = (projectKey, phaseKey);

                var vtgBudgetCell = sheet.Cell(row, vtgBudgetCol);
                var vtgEarnedCell = sheet.Cell(row, vtgEarnedCol);
                var estHoursCell = sheet.Cell(row, estHoursCol);
                var jtdErnCell = sheet.Cell(row, jtdErnCol);

                if (aggregates.TryGetValue(key, out var totals))
                {
                    double vtgBudget = NumericHelper.RoundToPlaces(totals.Budget);
                    double vtgEarned = NumericHelper.RoundToPlaces(totals.Earned);

                    vtgBudgetCell.Value = vtgBudget;
                    vtgBudgetCell.Style.NumberFormat.Format = "#,##0.000";

                    vtgEarnedCell.Value = vtgEarned;
                    vtgEarnedCell.Style.NumberFormat.Format = "#,##0.000";

                    result.MatchedRows++;

                    double estHours = TryGetNumber(estHoursCell);
                    double jtdErn = TryGetNumber(jtdErnCell);

                    if (IsMismatch(vtgBudget, estHours))
                    {
                        vtgBudgetCell.Style.Fill.BackgroundColor = redFill;
                        result.BudgetMismatchRows++;
                    }
                    else
                    {
                        vtgBudgetCell.Style.Fill.BackgroundColor = greenFill;
                    }

                    if (IsMismatch(vtgEarned, jtdErn))
                    {
                        vtgEarnedCell.Style.Fill.BackgroundColor = redFill;
                        result.EarnedMismatchRows++;
                    }
                    else
                    {
                        vtgEarnedCell.Style.Fill.BackgroundColor = greenFill;
                    }
                }
                else
                {
                    vtgBudgetCell.Value = NotFound;
                    vtgEarnedCell.Value = NotFound;
                    vtgBudgetCell.Style.Fill.BackgroundColor = orangeFill;
                    vtgEarnedCell.Style.Fill.BackgroundColor = orangeFill;
                    result.NotFoundRows++;
                }
            }

            workbook.SaveAs(outputPath);
            return result;
        }

        // Finds the 1-based column number whose row-`headerRow` cell matches `headerText`
        // (case- and whitespace-insensitive). Returns 0 if not found.
        private static int FindHeaderColumn(IXLWorksheet sheet, int headerRow, string headerText)
        {
            var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            for (int c = 1; c <= lastCol; c++)
            {
                string cellText = sheet.Cell(headerRow, c).GetString();
                if (string.IsNullOrWhiteSpace(cellText)) continue;
                if (string.Equals(cellText.Trim(), headerText, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            return 0;
        }

        private static double TryGetNumber(IXLCell cell)
        {
            try
            {
                if (cell.DataType == XLDataType.Number)
                    return cell.GetDouble();
                var s = cell.GetString();
                if (string.IsNullOrWhiteSpace(s)) return 0;
                return double.TryParse(s, out double v) ? v : 0;
            }
            catch
            {
                return 0;
            }
        }

        // Mismatch: |a - b| > 1% of |b|. If both are zero -> match. If reference is zero and other is not -> mismatch.
        private static bool IsMismatch(double vtgValue, double excelValue)
        {
            double diff = Math.Abs(vtgValue - excelValue);
            if (Math.Abs(excelValue) < 1e-9)
                return diff > 1e-9;
            return diff > MismatchThreshold * Math.Abs(excelValue);
        }

        // Aggregates Azure VMS_Activities rows (IsDeleted = 0) by normalized (ProjectID, PhaseCode).
        // Budget = sum(BudgetMHs); Earned = sum((PercentEntry / 100) * BudgetMHs).
        private static Dictionary<(string, string), (double Budget, double Earned)> LoadAggregatesFromAzure()
        {
            var map = new Dictionary<(string, string), (double Budget, double Earned)>();

            using var conn = AzureDbManager.GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 0;
            cmd.CommandText = @"
                SELECT ProjectID, PhaseCode, BudgetMHs, PercentEntry
                FROM VMS_Activities
                WHERE ISNULL(IsDeleted, 0) = 0";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string projectRaw = reader.IsDBNull(0) ? "" : reader.GetString(0);
                string phaseRaw = reader.IsDBNull(1) ? "" : reader.GetString(1);
                double budget = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2));
                double percent = reader.IsDBNull(3) ? 0 : Convert.ToDouble(reader.GetValue(3));

                string projectKey = NormalizeKey(projectRaw);
                string phaseKey = NormalizeKey(phaseRaw);
                var key = (projectKey, phaseKey);
                double earned = (percent / 100.0) * budget;

                if (map.TryGetValue(key, out var cur))
                    map[key] = (cur.Budget + budget, cur.Earned + earned);
                else
                    map[key] = (budget, earned);
            }

            return map;
        }

        // Normalizes a key segment so that "26.001.001", "26.001.  1.", and "26.001.1" all match.
        // Rules: strip all whitespace; split on '.'; per segment, strip leading zeros (keep "0" if all zeros
        // or empty-after-strip-but-was-all-zeros); drop trailing empty segments; rejoin with '.'.
        public static string NormalizeKey(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var compact = new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
            var parts = compact.Split('.');
            var normalized = new List<string>(parts.Length);
            foreach (var raw in parts)
            {
                if (raw.Length == 0)
                {
                    normalized.Add("");
                    continue;
                }
                if (raw.All(ch => ch == '0'))
                {
                    normalized.Add("0");
                    continue;
                }
                string trimmed = raw.TrimStart('0');
                normalized.Add(trimmed.Length == 0 ? "0" : trimmed);
            }
            while (normalized.Count > 0 && normalized[^1] == "") normalized.RemoveAt(normalized.Count - 1);
            return string.Join(".", normalized);
        }
    }
}
