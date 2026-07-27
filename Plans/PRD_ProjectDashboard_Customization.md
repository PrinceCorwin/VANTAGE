# PRD — Project Dashboard Customization (Saved Report Layouts)

**Status:** In progress. §10 step 1 (Default content) done; **§10 step 2 (data-driven render engine) DONE 2026-07-24** — all 7 visual types ported to definition-driven renderers, verified byte-identical to the legacy output, legacy functions deleted. Next: step 3 (layout store + manager UI + visual editor).
**Owner:** Steve
**Last updated:** July 24, 2026

> Mockups reviewed and refined 2026-07-24 (layout manager v3 + visual editor). Steve's feedback is incorporated into §3/§4/§7/§9. Note: mockup code is not saved to disk — it is regenerated in-session from this PRD when needed. Remaining editor detail is being worked type-by-type (§7).

---

## 1. Goal

Let users tailor the Project Dashboard per project: add / remove / relabel visuals, change each visual's source column, measure, and exclusions, arrange them across the report's columns/rows, and save the result as a **named layout**. Layouts are shareable across the team through Azure. A built-in **Default** layout is always present, locked, and revertible.

## 2. Core concepts

- **Layout** — an ordered set of *visual definitions* plus placement, saved under a user-chosen name. This is what the report renders.
- **Default layout** — the shipped reference report. Always first in the list, **locked (read-only)**, never editable. "Revert to default" re-selects it. Attempting to edit it prompts "Save as new layout."
- **Visual definition** — one card on the report. Carries: `type`, `title` (label), `groupBy` (source column), `measure`, `excludeFilters`, `placement` (column + slot + size).

## 3. The architectural shift (the real work)

Today each visual is a hardcoded JS function (`progressHTML`, `phasePanel`, `evbTable`, ROC bars, heatmap, trend). Customization requires making rendering **data-driven**: one generic renderer that draws any visual from its definition. The current report is re-expressed as the Default layout in this schema. **This engine refactor is the bulk of the effort and is why the Default content must be finalized first** — we lock the visual vocabulary, then generalize it.

### Rendering approach — hand-rolled SVG (decided)
All visuals are hand-drawn SVG / HTML / CSS — **no charting library** (Chart.js, ECharts, D3, etc.). Confirmed 2026-07-24 after weighing the options: keeps the file lean and fully offline, stays PDF-crisp (vector SVG), and gives total control of the Summit look. The engine renders each visual *type* with a small dedicated renderer. Adding a new visual to the Default later is cheap: a same-type visual is just another entry in the Default's JSON (no engine change); a genuinely new *type* is one new renderer, additive and localized — never a rewrite. So the Default being "locked for now" does not paint us into a corner.

### Visual types (fixed palette, configurable data) — 7 types, confirmed 2026-07-24
Each type is one dedicated renderer; label, source column, measure, exclusions, and placement are config on top. These 7 are exactly what the current report renders — the Default is expressed entirely with them (the two week trends are two instances of the single Trend type). No net-new types in v1.
1. **Progress ring** — single % with center value (Overall Progress)
2. **Composition donut** — slices per group, colored per group, optional center %, **and an optional summary table beneath** (driven by one def). Progress by Phase stays a single card (donut + table). *(Decided 2026-07-24.)*
3. **Bar list** — horizontal % bar per group (ROC Step, Shop/Field)
4. **Summary table** — group | % | earned | budget, with a Total row (Component Type, Area)
5. **Heatmap** — group × phase matrix (Area / Module Progress by Phase). *(Steve refers to this one as "the data table" — same visual.)*
6. **Trend** — earned MH by week; per-type mode toggle **cumulative / per-week** (Default carries one of each)
7. **Stat tiles** — a fully-generic "X of Y" pair + % ring. User picks the aggregated column (e.g. Quantity), a **numerator** where-clause and a **denominator** where-clause (e.g. ROCStep = 5.CON), and edits all three labels (title + the two stat labels). "Summit Connections" is just one configuration of this.

### Layout schema (illustrative)
```json
{
  "id": "guid",
  "name": "TFS field report",
  "schemaVersion": 1,
  "basedOn": "default",
  "visuals": [
    {
      "id": "guid",
      "type": "donut",
      "title": "Progress by Phase",
      "groupBy": "PhaseCategory",
      "measure": "budgetMHs",
      "centerValue": "percentComplete",
      "excludeFilters": [ { "field": "ROCStep", "op": "notIn", "values": ["X", "0.FAB"] } ],
      "placement": { "row": 2, "slot": 0, "stretch": false }
    }
  ]
}
```
(No `size`/slots field — sizing is content-driven, see §4. `stretch:true` makes a below-row-1 visual own its whole row.)

## 4. Layout structure & placement rules  *(refined 2026-07-24 — mockup v3)*

- **Row 1 — fixed four columns, exclusive to row 1.** Fixed height (2× the Overall Progress card). Each column holds a **maximum of two visuals**.
  - **Hard rule:** when a column already contains two visuals, the "Add visual" affordance for that column is **hidden/disabled** — the user must delete one before adding another. Never surface a way to add a third.
  - There is no way to create a second four-column band. The 4-column structure belongs to row 1 only.
  - **Adjustable column widths (drag, 2026-07-25):** in Customize, drag the divider between two row-1 columns to resize them; the per-column fr weights save with the layout (`layout.row1Widths`). The shipped Default keeps its tuned proportions (`minmax(200px,1fr) …`); once a width is customized the columns switch to `minmax(0,Nfr)` so a content-heavy column (e.g. a table with large numbers, which otherwise min-content-forces itself wide and squeezes its neighbours) can be dragged narrower and scrolls internally. Below-row rows stay equal-split.
- **Rows 2+ — user-added rows.** The user can add rows below row 1 (never another 4-column row). Each added row holds **1 to 5 visuals**, whose widths are **split equally** by how many are present (5 is the current cap).
  - **Stretch to fit:** a visual can be set (in its editor) to stretch to fill its whole row. A row containing a stretched visual shows **no "Add visual"** affordance — the user must edit that visual to un-stretch it before the row can hold more. The UI states this inline ("Stretched to fit — edit the visual to add more").
  - The current report's below-row-1 visuals are all stretched: Row 2 Heatmap, Row 3 Trend (cumulative), Row 4 Trend (per-week) — matching `matrixHTML + trendHTML + weeklyTrendHTML` in the render order.
- **No slot/size picker.** Size is content-driven: each visual sizes to its content and scrolls if it overflows (per the existing panel code). The only sizing control is the per-visual **stretch-to-fit** toggle.
- **Footer — its own entity.** Independent left- and right-justified text boxes, **empty by default**; users can add text to either side. Already stubbed as `data-footer="left"/"right"` in `footerHTML`.
- Reordering rows — start with move up/down (drag handles shown in the mockup); drag-and-drop is a later enhancement. *(Decision — see §9.)*

## 5. Storage & sharing (Azure-centric, no auto-sync)

- **Local (UserSettings JSON — confirmed 2026-07-24):** mirror the existing grid-layouts pattern in `SettingsManager` ([SettingsManager.cs:320-443](../Utilities/SettingsManager.cs)): an index key `ReportLayouts.Index` (JSON list of names), one `ReportLayout.<name>.Data` key per layout (serialized layout JSON), and `ReportLayouts.LastUsed.<ProjectID>` for the per-project last-used pointer (§9 decision 2). No dedicated local table, no migration; register the keys in `UserSettingsRegistry`. (Grid layouts cap at 5 — decide whether report layouts share that cap.)
- **Azure shared library:** a new **dedicated** `dbo.VMS_ReportLayouts` table on `projectcontrols` (decided 2026-07-24 over a generic `GlobalSettings` key-value table — the import picker needs queryable per-layout metadata: name, author, updated-date; and a narrow table is safer on the REQit-shared DB). The `VMS_` prefix is the Vantage Milestone table convention. A `GlobalSettings` table can be added later for scalar cloud settings if needed. Schema: `LayoutId` (uniqueidentifier PK), `Name` (nvarchar(120)), `Author` (nvarchar(120)), `SchemaVersion` (int), `LayoutJson` (nvarchar(max)), `CreatedUtc`, `UpdatedUtc` (datetime2). **Created server-side manually (2026-07-25); the app runs no DDL** — additive-only against the REQit-shared DB.
  - **Publish to Cloud** — pushes a *copy* of a local layout to the shared library. **Built 2026-07-25:** `Data/AzureReportLayoutRepository.cs` (`PublishAsync` upsert by Name+Author, `GetListAsync`, `GetJsonAsync`, `DeleteAsync`). The page posts `publishLayout` with the layout; `ProjectDashboardWindow.PublishToCloudAsync` checks the connection, stamps `Author = App.CurrentUser`, and publishes. Publishing the Default routes to Save as first.
  - **Import from Cloud** — **Built 2026-07-25:** `Dialogs/ReportLayoutImportDialog` lists the cloud library (Name / Author / Updated). Import downloads the JSON (`GetJsonAsync`), re-homes it as a fresh local layout (new local id, `locked=false`, keeps the name), saves via `SettingsManager.SaveReportLayout`, sets it active + last-used. **One-time copy — the local layout does NOT auto-link or sync to Azure.**
  - Workflow: one person designs a per-project report, publishes it; teammates import it. Replaces passing settings files around.
  - **Deletion of shared/cloud layouts is admin-only** (decided 2026-07-25) — **Built 2026-07-25:** the Import dialog shows a **Delete from Cloud** button, visible only when `AzureDbManager.IsUserAdmin(current user)` (confirm + `DeleteAsync`, then refresh the list). Local layouts remain freely deletable by their owner (toolbar Delete). Import copies a cloud layout into the local list, so a user can always delete their local copy.
  - ⚠️ `projectcontrols` is shared with **REQit** — coordinate the schema addition per CLAUDE.md.
- **File export/import** — retained as a secondary path (`.json`), but Azure is the primary sharing channel.

## 6. PDF export  *(DONE 2026-07-25)*

Implemented: an **Export PDF** button on the main toolbar (`ProjectDashboardWindow`) — and the manager-toolbar "Export PDF" routes to the same handler. `ExportPdfAsync` posts `setPrintMode:true` to the page (which re-renders report-only: no filter rail, no edit chrome, full width), waits for a `printReady` handshake (3s timeout fallback), shows a SaveFileDialog (default `ProjectDashboard_<project>_<yyyyMMdd>.pdf`), calls `CoreWebView2.PrintToPdfAsync` with **landscape + print-backgrounds**, then posts `setPrintMode:false` to restore. Offers to open the saved file.

- Button on the main report toolbar (not just customize mode).
- Uses `CoreWebView2.PrintToPdfAsync` (already used by the help sidebar), landscape, fit-to-width, filename patterned per project.
- **The PDF must NOT include the filter panel** (the left rail) — export the report content only. Hide/detach the rail for the print render, then restore.
- **No mid-row page splits (2026-07-25):** the print-mode DOM is wrapped in `.printwrap`; `break-inside:avoid` on `.dashstack` (each below-row-1 row) and `.dashrow1` keeps a row whole — a row that would straddle a page boundary moves entirely to the next page. Print also removes the on-screen 450px `.dashstack` cap (`max-height:none;overflow:visible`) so tall visuals print full content instead of being clipped. A single visual taller than one page still splits (unavoidable).
- **Scale-to-fit (2026-07-27):** `ExportPdfAsync` sets `CoreWebView2PrintSettings.ScaleFactor = 0.68`. The ~1400px-wide report on a ~975px landscape-Letter page was squeezing/clipping the row-1 columns (tables, bars) at scale 1.0; because `.printwrap` is fluid `width:100%`, a sub-1 scale factor gives Chromium a wider logical canvas (~1435px) then shrinks the page to fit — proportional, vector-crisp, no font hacks. Value is tunable (lower = wider canvas).

## 7. Customization UI

- **CUSTOMIZE** button on the report's top toolbar (WPF) toggles **edit mode inside the WebView2** (HTML/JS), so add/remove/relabel/edit render with **live preview** against real data.
- **Layout manager** (edit mode) — **built 2026-07-25:** per-visual ✎ edit / ✕ delete; **"+ Add visual"** per row-1 column (respecting the max-two rule — the slot hides at 2) and per added row (hidden at 5); **"+ Add row"** appends an empty row (each added row also has a **Remove row** control); a **type picker** (7 types) opens on Add, creates a sensible default def + new id, and opens its editor; the **editable footer** (left/right dashed inputs → `layout.footer`); drag-to-resize row-1 columns. **Per added-row move up/down arrows** (reorder any row below the fixed row-1 band — existing or newly added; row 1 is never movable). **Per-row "Stretch to fit" toggle** (2026-07-25): rows below row 1 size each visual to its content width by default (a few-column heatmap no longer stretches across the row); toggle on to fill the full row width (`row.stretch`; the Default's two trend rows ship stretched). Empty added-rows are skipped in view and stripped on Save. Toolbar: Save, Save as, Revert to default, **Publish to Cloud** (done), **Import from Cloud** (done), **Export PDF** (done), Done. *Note: the saved-layouts list lives in the WPF toolbar Layout combo rather than an in-page panel.* (UI says "Cloud"; backing store is the Azure `dbo.VMS_ReportLayouts` table.)
- **Visual editor** (opens on ✎ / Add) — **an in-page HTML panel docked in the dashboard window, NOT a native WPF dialog** (decided 2026-07-25). This keeps live preview trivial (same JS context — edits mutate the working layout and `render()` repaints the report beside the panel) and reuses the approved mockups; it also works inside the "Open in Browser" export. Color controls are an **HTML swatch→popover**, not the Syncfusion `ColorPicker`. Fields: Label, Visual type, Group by (source column), Measure, Center/headline value, Exclude-rows filter builder, and per-type extras (e.g. Trend cumulative/per-week toggle). **No slot/size picker** — sizing is content-driven. Built **type-by-type**. Live-preview surface is settled: the report updates live *beside* the docked panel (§9.4 resolved).
- **C# owns:** local layout persistence, Azure publish/import, PDF, and the default-locked logic. Page ↔ C# exchange the layout JSON over `postMessage` (same seam used for data injection).

### Editor fields by type (locked as approved — designed one type at a time)
**Editors built: all 7 (2026-07-25).** The **filter builder** (a reusable where-clause UI: rows of `AND/OR · field · operator · value` with add/remove) powers both the common **Exclude rows** section and the **Stat tiles** Complete/Total criteria. It **mirrors the Progress module's saved filters** (2026-07-25): the same 12 operators from `Models/UserFilter.FilterCriteria.AllCriteria` (Equals / Not Equals / Contains / Does Not Contain / Starts With / Ends With / Is Empty / Is Not Empty / Greater Than / … / Less Than or Equal, case-insensitive), the **full column list** (all COLMAP + NUMMAP + DATEMAP keys, not just group-able ones), and a per-condition **AND/OR** joiner. **The report is now fed all UDF fields (UDF1, UDF3–UDF17, UDF20; UDF2 is "Area / Module") + `EarnQtyEntry`** (added to `ActivityDto`/projection in `ProjectDashboardWindow`, mapped in the page's COLMAP/NUMMAP, 2026-07-25). These extra columns are available in **every editor column picker** — group-by, filter/exclude fields, measure, heatmap axes, stat-tile aggregate — but deliberately **NOT added to the left filter-panel rail**. Is Empty / Is Not Empty take no value. Conditions store as `{field,op,value,logic}` arrays (`excludeFilters`, `complete.where`, `total.where`); `rowMatches` folds them left-to-right honouring each `logic`.

Common to every editor: a **Label** field, an **Exclude rows where** filter builder (field / op / values, add-condition), a **Preview** button, a **Revert to default** button (resets that visual to its shipped config; only shown when the visual exists in the Default), and Cancel / Save. **Preview is on-demand, not live** — field edits update a working copy silently and the report repaints only on Preview / Save (decided 2026-07-25; per-keystroke re-rendering made text inputs janky and would not scale to 100k rows). Cancel discards all edits made since the panel opened (including a Revert). Placement + stretch are set in the layout manager, not the editor. Measure options everywhere: **Inherit report basis** (default — follows the filter-rail Progress-basis selector) then the full numeric set — BudgetMHs, ClientBudget, ClientCustom3, ClientEquivQty, EarnMHsCalc, EarnedMHsRoc, EquivQTY, PercentEntry, PrevEarnMHs, PrevEarnQTY, Quantity, EarnQtyEntry, ROCBudgetQTY (added 2026-07-25; the same set is offered by the filter-rail Progress-basis selector). **All field/measure pickers label fields with the native Activity column names** (`CompType`, `PhaseCategory`, `ROCStep`, `ShopField`, `WorkPackage`, `DwgNO`, `BudgetMHs`, `EarnMHsCalc`, `EarnQtyEntry`, …) — matching the Progress grid, not friendly relabels (decided 2026-07-25). **All color controls default to the current Summit colors** so an unedited layout matches today exactly. A color control is a **swatch** paired with a hex field — a native HTML `<input type="color">` (opens the OS spectrum picker) beside a hex text box that stays in sync, plus Summit preset swatches. (Not the Syncfusion `ColorPicker` — the editor is in-page HTML, see the Visual-editor bullet.)
- **1. Progress ring** — Measure; **Center value** (% complete / Total earned / Total budget / Row count); **Show Earned & Total** toggle (default off) — when on, two tiles beneath the ring show earned + total, labeled just **"Earned" / "Total"** with no unit suffix (the old `unitLabel` suffix reflected the global basis, not the visual's own measure, so it was dropped 2026-07-25); **Progress color** (filled arc, default navy `#1e1b6b`) and **Remaining color** (track, default light grey `#e6e6ee`). *(Locked 2026-07-24.)*
- **2. Composition donut** — Group by (slices); Measure; **Show center value** toggle + value (% / earned / budget / count); **Show summary table beneath** toggle + Sort (by size / by name / by %) + **per-column show/hide** for %, Earned, Budget (hide hours for client-facing reports; 2026-07-25); **Colors** — default the discrete Summit palette (editable ordered swatch list: navy, blue, light-blue, violet, lilac, amber, green, grey; cycles past 8), plus a **"spread between two colors"** mode (start/end pickers, engine interpolates N evenly-spaced colors). *(Locked 2026-07-24.)*
- **3. Bar list** — **Orientation** (Horizontal / Vertical, default Horizontal; vertical = columns on a shared baseline, value on top, category below, sideways scroll past ~12 bars; 2026-07-25); the group-by / measure pickers are labeled **X-Axis / Y-Axis** and **swap by orientation** (horizontal → category is Y-Axis, value is X-Axis; vertical → reversed); **Sort** (by name / by size / by %); **Label width** (px, default 52 — horizontal only, hidden when vertical); **Colors** — **Bar** (default navy `#1e1b6b`), **Remaining** (bar track, default light grey `#eef0f4`), and a **Highlight complete (100%)** toggle (default on) gating the **Complete** color (default green `#2bb24c`; off = bars stay the bar color at 100%). **Row-1 sizing:** content-based — alone in a column it grows to 2H, sharing it flexes with a `max-content` cap (each guaranteed ~1H, a shorter neighbour frees surplus to a taller one, overflow scrolls). *(Locked 2026-07-24; orientation + axis labels + sizing 2026-07-25.)*
- **4. Summary table** — Group by (rows); Measure; **Sort** (by size / by name / by %); **Highlight complete (100%)** toggle (default on) + Complete-% color (default green `#2bb24c`; off = 100% rows use the normal text color). Columns: the **group** column is always shown; **%, Earned, and Budget are each show/hide checkboxes** (`showPct`/`showEarn`/`showBudget`, default on — hide hours for client-facing reports, 2026-07-25) + Total row; clicking a row filters the whole report to that group, click again to clear. *(Locked 2026-07-24.)*
- **5. Heatmap** — **Y-Axis (rows)** + **X-Axis (columns)** groups; Measure; **all distinct column values shown, ordered alphabetically** (the old Max-columns cap was removed 2026-07-25 — drop any column via Exclude rows); **%/Earned/Budget show-hide** toggles (like the summary table + donut, 2026-07-25); **Color scale** — Low 0% (default red), High 100% (default green `#2bb24c`), No data (default grey `#666`); engine blends Low→High by %. Rows sortable by any column. *(Locked 2026-07-24; axis labels / all-columns / toggles 2026-07-25.)*
- **6. Trend** — **Mode** (Cumulative / Per-week); measure picker labeled **Y-Axis** (X is always time); **Week start** (Monday default / Sunday). Uses **actual dates only** (`actFin || actStart`) — no planned-date option. **Bar color** stored per-instance (Default: cumulative navy `#1e1b6b`, per-week blue `#3c5c9e`) + **Highlight complete** toggle (cumulative only, default green `#2bb24c`). *(Locked 2026-07-24.)*
- **7. Stat tiles** — fully-generic "X of Y". **Title**; **Aggregate** (Count of rows / Sum of a chosen column, e.g. Quantity); **Complete** section (editable tile label + where-clause builder) and **Total** section (editable tile label + where-clause builder); **Show ring** toggle (default on) with **Ring color** (default navy) + **Remaining color** (default light grey); ring % = Complete ÷ Total. "Summit Connections" is just this configured (ROCStep = 4.CON). *(Locked 2026-07-24.)*

## 8. Security

- Treat imported layouts (Azure or file) as **untrusted data**: whitelist `type`, `groupBy` field names, and `measure` values against known enums; drop/ignore anything unrecognized. Never `eval` layout content. (See `Plans/Security_Guidelines.md`.)

## 9. Decisions & open items

**Resolved 2026-07-24 (mockup review, v3):**
- **Visual palette = 7 types** (see §3) — exactly what renders today; no net-new types. (An earlier "8th detail Data table" was a misread: "data table" is Steve's name for the existing Heatmap.)
- **Rows 2+ model** — user-added rows of 1–5 equal-width visuals; per-visual stretch-to-fit; no second 4-column band (§4).
- **Footer** — its own entity with independent left/right text boxes, empty by default (§4).
- **No slot/size picker** — content-driven sizing + stretch toggle only (§4, §7).
- **UI wording** — "Publish to Cloud" / "Import from Cloud" instead of "…Azure" (§5, §7).

- **Default is read-only in the manager (2026-07-25):** the edit-mode toolbar shows a standing warning ("changes aren't saved until you click Save"), and clicking **Save** while the active layout is the Default routes to **Save as** — an in-page name prompt that converts the working copy into a new unlocked user layout (`id` reissued, `locked:false`, `basedOn:'default'`) and posts it to C# via a `saveLayout` message.
- **Persistence + layout picker + last-used built (2026-07-25):** C# stores user layouts in UserSettings via `SettingsManager` (`ReportLayouts.Index` / `ReportLayout.<id>.Data` / `ReportLayouts.LastUsed`). A native **Layout** combo on the toolbar lists Default + saved layouts; picking one posts `setLayout` to the page. Page `saveLayout` / `deleteLayout` messages upsert/remove. **Local delete:** a **Delete** button beside the toolbar Layout combo removes the selected saved layout (enabled only for user layouts, with an `AppMessageBox` confirm); the in-Customize "Delete layout" button remains as a second path. Any user can delete their own **local** layouts. **Last-used is global** (whatever layout you were viewing loads next open — the report isn't project-scoped, so per-ProjectID from §9.2 was dropped).
- **Click-to-filter extended (2026-07-24):** in addition to the tables (which always filtered), **bar lists** now filter by the clicked group and **trend charts** filter by the clicked week (a single week, by actual date, via a reserved `__week` filter in `filtered()`, Monday week-start). **Heatmap** intentionally left non-filtering for now.

**Still open:**
1. ~~**Reordering UX**~~ — RESOLVED 2026-07-25: **row up/down arrows shipped** (any row below the fixed row-1 band). **Deferred for now** (Steve's call): drag-and-drop, and reordering visuals *within* a column/row (no buttons or drag for intra-row/column order yet). Revisit drag-and-drop later.
2. ~~**Layout scope**~~ — RESOLVED 2026-07-25: layouts are reusable templates; the report remembers a single **global** last-used layout (not per-ProjectID — the dashboard shows all local data, not one project).
3. **Publish permissions** — any user vs admins/author only. Lean: admins or the layout's author can publish/overwrite; anyone can import.
4. ~~**Live-preview surface**~~ — RESOLVED 2026-07-25: an on-demand **Preview** button repaints the report beside the docked in-page editor panel (no live/per-keystroke render).

## 10. Sequencing

1. **Finish Default layout content** (remaining visuals / rows) — in progress.
2. ~~Refactor render engine to data-driven visual definitions; express Default as a layout.~~ **DONE 2026-07-24.** Engine in `Dashboards/vantage-dashboard.html`: `DEFAULT_LAYOUT` constant + `renderVisual(def,rows)` dispatcher + per-type renderers (`RENDERERS.{ring,statTiles,donut,table,barList,heatmap,trend}`) + `renderLayout(layout,rows)`; `render()` swapped to `renderLayout(activeLayout,rows)`. `activeLayout` defaults to `DEFAULT_LAYOUT` — ready to accept a custom layout via `postMessage` (wire that intake in step 3). Two deliberate 1px/label cleanups: ROC bar-list gap 5→6px; connections tile labels use a space, not `<br>` (both render identically).
3. Local layout store + manager UI + visual editor (edit mode in WebView2).
4. PDF export.
5. ~~Azure `VMS_ReportLayouts` table + Publish/Import (coordinate with REQit).~~ **DONE 2026-07-25.** `AzureReportLayoutRepository` (Publish upsert, List/Get/Delete against server-created `VMS_ReportLayouts` — no app-side DDL); Publish to Cloud + Import from Cloud (`ReportLayoutImportDialog`, admin-only Delete from Cloud) wired into `ProjectDashboardWindow`.
6. File export/import (secondary).

## 11. Future / deferred visual ideas
- **General in-visual column resize (drag inside a visual during edit).** Preferred long-term for any visual with internal columns (tables, donut table, heatmap, bar list). Sizable — HTML tables are auto-layout, so it means converting to `table-layout:fixed` with explicit per-column widths + header drag handles, per visual type. For now the bar list has a **Label width (px)** editor field (2026-07-25) as a targeted fix for its fixed label column; revisit the general drag approach later.
- **3-week look-ahead (3WLA)** — a planned-date (`PlanStart/PlanFin`) forward view of scheduled workload. Considered as a Trend date-source option and rejected (kept Trend on actuals only); revisit later as its own additive visual type.

> Given the engine refactor + WPF dialogs + Azure work, this is a good candidate to build via a multi-agent workflow when the time comes.
