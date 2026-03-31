# PTP TFS MECH UPDATER Plugin — Design Plan

## Overview

Plugin for the Fluor T&M 25.005 project. Imports PTP (pipe support vendor) weekly Excel reports and creates/updates VANTAGE activities for the TFS Mechanical piping system.

## Architecture

- **Plugin type:** action (adds menu item to Tools menu)
- **References VANTAGE.exe directly** for database access (ActivityRepository, etc.)
- **Lives in:** `VANTAGE-Plugins/src/ptp-tfs-mech-updater/`

## PTP Report Structure

- **Source:** Excel file, single "Main" sheet, ~1,179 rows with varying quantities per row
- **Key columns:** Line No., Priority, CWA, Dike, CWP, Module Yard, Node, Size, Insulated, Quantity, PTP Tag, Client Tag, Job No., Item No., Status, Shipped QTY, Remaining QTY, dates, comments
- **Status values:** Shipped, Deleted, In PT&P Engineering
- **Important:** Each row has its own Quantity (not just 1) — must be aggregated per CWP

## Field Mappings

### Row Generation

For every unique **CWP** value in the PTP report, **1 VANTAGE activity row** is created:

| ROCStep | Description Pattern                    |
|---------|----------------------------------------|
| 4.SHP   | `FABRICATION - 4.SHP {CWP}`           |

Example for CWP `TFS00D001YS18`: `FABRICATION - 4.SHP TFS00D001YS18`

**Why single row:** The PTP report only provides shipping status. IWP, CUT, FAB phases are not tracked by this vendor report. ROCStep name may change later.

### Column Mappings

| VANTAGE Field   | Value / Source                          | Notes                                |
|-----------------|----------------------------------------|--------------------------------------|
| Description     | `FABRICATION - 4.SHP {CWP}`           | Default value, user can modify       |
| SchedActNO      | `x`                                    | Constant                             |
| UDF2            | PTP `CWP` column                       | **Match key** — one CWP = one row    |
| Area            | `TFS`                                  | Constant                             |
| CompType        | `P`                                    | Constant                             |
| PhaseCategory   | `PSF`                                  | Constant                             |
| PhaseCode       | `xx.xxx.xxx.`                          | Constant                             |
| ProjectID       | `25.005.`                              | Constant                             |
| ShopField       | `Shop`                                 | Constant                             |
| UDF1            | `1`                                    | Constant                             |
| UDF3            | `NEARSITE`                             | Constant                             |
| WorkPackage     | `x`                                    | Constant                             |
| RespParty       | `SUMMIT - PM`                          | Constant                             |
| ROCStep         | `4.SHP`                               | Constant                             |
| BudgetQty       | SUM of non-Deleted PTP `Quantity`       | Aggregated per CWP                   |
| BudgetMHs       | `0.001`                                | Constant — not tracking hours        |
| AssignedTo      | Current user's username                | Set on create only                   |

## Quantity Calculation

**Deleted rows excluded** from all calculations (case-insensitive Status check).

**BudgetQty** = SUM of PTP `Quantity` for non-Deleted rows per CWP. Updated on every import (items may be added or deleted between reports).

## PercentEntry Calculation

All calculations exclude rows where PTP Status = "Deleted" (case-insensitive).

**PercentEntry** = SUM(`Shipped QTY`) / SUM(`Quantity`) * 100 for non-Deleted rows per CWP

### All-Deleted Edge Case

If ALL rows for a CWP have Status = "Deleted":
1. Set PercentEntry to **100%**
2. Append `"DELETED"` to the Notes field
3. Show message to user: CWP is fully deleted, record set to 100% with DELETED in notes
4. User decides what to do from there

## Matching Logic

- **Match key:** UDF2 field (contains the CWP value directly)
- **Query criteria:** Area = 'TFS' AND ROCStep = '4.SHP' AND UDF2 is not empty
- **If found:** Update BudgetQty and PercentEntry
- **If not found:** Create new activity with all mapped fields

Note: Previously matched by Description pattern, but this prevented users from modifying descriptions. Changed to UDF2 in v1.0.2.

## Ownership Check

- **On create:** AssignedTo = current logged-in user
- **On update:** Before updating, check AssignedTo on existing records
  - If all records are owned by current user: proceed
  - If any records are owned by a different user: warn the importing user with a message showing which user owns the records, and **reject the update**
  - The original importer is responsible for keeping records updated

## Update Summary (per import run)

| Field | On Create | On Update |
|-------|-----------|-----------|
| All mapped fields | Set | Not changed |
| BudgetQty | Set | **Updated** |
| PercentEntry | Set | **Updated** |
| Notes | Empty (or "DELETED") | Append "DELETED" if all-deleted case |

## Rate Reference (not used by plugin)

For future use if hours tracking is added: 1.IWP=7, 2.CUT=20, 3.FAB=53, 4.SHP=20

## File Validation

On opening the selected Excel file, check that row 1 contains the expected column headers (at minimum: `CWP`, `Quantity`, `Status`, `Shipped QTY`). If headers are missing or not on row 1, reject the file and show message: "File is not formatted properly. Column headers must be on the first row."

## UI Flow

1. User clicks **PTP TFS MECH Updater** in Tools menu
2. File picker dialog opens — user selects PTP Excel report
3. **File validation** — check column headers on row 1; reject if invalid
4. Plugin parses report, aggregates per CWP, calculates quantities and percent
5. **Ownership check** — if existing records belong to another user, show warning and abort
6. Plugin creates new rows / updates existing rows
7. Show results summary: X created, Y updated, any all-deleted CWPs flagged
