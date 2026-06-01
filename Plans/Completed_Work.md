# VANTAGE: Milestone - Completed Work

This document tracks completed features and fixes. Items are moved here from Project_Status.md after user confirmation.

---

## Unreleased

### June 1, 2026 (P6 Export — complete_pct Now Two Decimals)

**Schedule → Export To P6 File no longer rounds `complete_pct` to a whole number.** Users reported activities at 99.5–99.9% landing in the exported P6 workbook as `100` while the same row's `act_end_date` was blank — an inconsistency P6 flags because the exporter only writes `act_end_date` when `MS_PercentComplete >= 100`. Root cause: `Utilities/ScheduleExcelExporter.cs:111` called `Math.Round(row.MS_PercentComplete, 0)`, which rounded `99.7` to `100`. Fix: round to 2 decimals and apply `NumberFormat = "0.00"` so Excel/P6 display both decimals even when the trailing one is zero (`25.50`, not `25.5`). Two-line edit in `WriteTaskSheet`.

**3WLA companion file already correct.** Verified `Utilities/ScheduleReportExporter.cs` writes `MS_PercentComplete` and `P6_PercentComplete` (columns 5 and 6) as raw doubles with no rounding, which matches the user feedback that the Schedule Reports file already shows the right decimal precision.

**Key files:** `Utilities/ScheduleExcelExporter.cs`.

---

**Archives:** See Plans/Archives/ for previous months.
