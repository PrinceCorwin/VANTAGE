# PRD — Project Dashboard Customization (Saved Report Layouts)

**Status:** Planned — not started. Build AFTER the Default layout content is finalized.
**Owner:** Steve
**Last updated:** July 24, 2026

> Mockups exist (shown in-session: "Customize report layout manager" + "Visual editor dialog"). Steve has additional feedback on the mockups still to be incorporated — treat the mockups as directional, not final.

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

### Visual types (fixed palette, configurable data)
- Progress ring (single % with center value)
- Composition donut (slices by group, colored per group)
- Bar list (horizontal % bars per group)
- Table — group | % | earned | budget, with Total row
- Heatmap — group-by-phase matrix
- Week-over-week trend (cumulative)
- Stat tiles (count / value pairs, e.g. Connections)

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
      "placement": { "row": 1, "col": 2, "size": "2slots", "order": 0 }
    }
  ]
}
```

## 4. Layout structure & placement rules

- **Row 1** — four columns, fixed height (2× the Overall Progress card). Each column holds a **maximum of two visuals**.
  - **Hard rule:** when a column already contains two visuals, the "Add visual" affordance for that column is **hidden/disabled** — the user must delete one before adding another. Never surface a way to add a third.
- **Row 2+** — full-width visuals (heatmap, trend, and any future rows), stacked.
- A visual's `size` decides whether it fills a column (2 slots) or takes one slot (1 slot). Two 1-slot visuals or one 2-slot visual per row-1 column.
- Reordering within/between columns — start with move up/down (and move-to-column); drag-and-drop is a later enhancement. *(Decision — see §9.)*

## 5. Storage & sharing (Azure-centric, no auto-sync)

- **Local (SQLite):** the user's own saved-layout list, mirroring the existing `ManageLayoutsDialog` (grid layouts) pattern. This is the list the report picks from.
- **Azure shared library:** a new `ReportLayouts` table on `projectcontrols`.
  - **Publish to Azure** — pushes a *copy* of a local layout to the shared library.
  - **Import from Azure** — pulls a published layout down into the local list as a new local copy. **One-time copy — the local layout does NOT auto-link or sync to Azure.**
  - Workflow: one person designs a per-project report, publishes it; teammates import it. Replaces passing settings files around.
  - ⚠️ `projectcontrols` is shared with **REQit** — coordinate the schema addition per CLAUDE.md.
- **File export/import** — retained as a secondary path (`.json`), but Azure is the primary sharing channel.

## 6. PDF export

- Button on the main report toolbar (not just customize mode).
- Uses `CoreWebView2.PrintToPdfAsync` (already used by the help sidebar), landscape, fit-to-width, filename patterned per project.

## 7. Customization UI

- **CUSTOMIZE** button on the report's top toolbar (WPF) toggles **edit mode inside the WebView2** (HTML/JS), so add/remove/relabel/edit render with **live preview** against real data.
- **Layout manager** (edit mode): the saved-layouts list (Default locked + user layouts), and the report's column/row structure with per-visual ✎ edit / ✕ delete, and per-column "+ Add visual" (respecting the row-1 max-two rule). Toolbar: Save, Save as, Revert to default, Publish to Azure, Import from Azure, Export PDF.
- **Visual editor** (opens on ✎ / Add): Label, Visual type, Group by (source column), Measure, Center/headline value, Exclude-rows filter builder, Column, Size. Live preview updates the report behind the dialog.
- **C# owns:** local layout persistence, Azure publish/import, PDF, and the default-locked logic. Page ↔ C# exchange the layout JSON over `postMessage` (same seam used for data injection).

## 8. Security

- Treat imported layouts (Azure or file) as **untrusted data**: whitelist `type`, `groupBy` field names, and `measure` values against known enums; drop/ignore anything unrecognized. Never `eval` layout content. (See `Plans/Security_Guidelines.md`.)

## 9. Open decisions

1. **Reordering UX** — move-buttons first vs drag-and-drop. Lean: move-buttons first.
2. **Layout scope** — reusable template (applied to any project) vs bound to a ProjectID. Lean: reusable template; report remembers last-used layout per ProjectID.
3. **Publish permissions** — any user vs admins/author only. Lean: admins or the layout's author can publish/overwrite; anyone can import.
4. Mockup feedback from Steve — pending.

## 10. Sequencing

1. **Finish Default layout content** (remaining visuals / rows) — in progress.
2. Refactor render engine to data-driven visual definitions; express Default as a layout.
3. Local layout store + manager UI + visual editor (edit mode in WebView2).
4. PDF export.
5. Azure `ReportLayouts` table + Publish/Import (coordinate with REQit).
6. File export/import (secondary).

> Given the engine refactor + WPF dialogs + Azure work, this is a good candidate to build via a multi-agent workflow when the time comes.
