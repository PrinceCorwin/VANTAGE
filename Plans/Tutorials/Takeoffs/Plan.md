# Takeoffs Video — Plan

**Audience:** New users (assumed they've watched the Intro)
**Format:** OBS screencast with voiceover
**Length:** Determined by content (this will likely be the longest video — lots of moving parts)
**Demo project:** 99.999 (Sandbox)

---

## Goal

Walk a user through the entire AI Takeoff workflow: from understanding what a config is, to processing a batch of drawings, to getting the resulting Excel file, to (briefly) importing it into Activities. By the end they should be able to run a takeoff on their own drawings.

---

## Section Outline

### 1. What AI Takeoff Is
- Purpose: feed PDF drawings (isometrics) into the app, AWS extracts the BOM table and title block, returns an Excel file with material, labor, and summary tabs
- Why it matters: replaces manual takeoff entry — hours of work become a few clicks
- High-level flow: **Config → Drawings → Process → Excel → Import to Activities**
- This video covers the Takeoffs module end-to-end. Importing the Excel into Activities lives in the **Progress** video, but we'll touch on it briefly so you see the full pickup.

### 2. Prerequisites
- You need a **crop region config** for the type of drawing you're processing. A config tells the AI where on the drawing to look for the BOM table and the title block.
- Configs are reusable — once you have one for a particular drawing format/template, you don't need to make a new one for every drawing of that type.
- You need PDF drawing files.

### 3. Tour of the Takeoffs Page
Open the **TAKEOFFS** nav button. Walk through every control on the page top to bottom:
- **Batch Name** — optional label for the batch; auto-generated if blank
- **Config dropdown** — list of crop region configs from S3; **Edit** button opens the Config Creator; **Refresh** reloads from S3
- **Drawings: Select Files...** — pick the PDFs you want to process
- **Unit Rates dropdown** — project-specific overrides or the default embedded rate sheet
- **PROCESS BATCH** button (green) — kicks off the upload and extraction
- **CANCEL** button (only visible during processing)
- **PREVIOUS BATCHES** button — download results from earlier batches
- **RECALC EXCEL** button — re-run the labor/summary tabs from an existing Material tab
- **Send Missed Makeups and Rates to Admin** checkbox — emails admin if the takeoff hits unmatched data
- **Status panel** at the bottom — running log of what's happening

### 4. Crop Region Configs (Config Creator)
This is the part most users won't touch often, but they need to understand it.
- Click **Edit** next to the config dropdown to open the **Config Creator** window
- Click **Load Drawing...** to load a sample PDF that matches the format you're configuring for
- **Draw mode buttons:**
  - **BOM Region** (green) — draw a box around the bill of materials table. Multiple regions allowed; they get stitched into one tall image before Claude reads them.
  - **Title Block** — draw boxes around title block sections (project info, drawing info, etc.). Multiple regions allowed and they're sent as separate images that Claude combines into one title block object.
- Why multiple regions? Some drawings have title blocks split across the page or have noise (logos, revision history) you want to exclude.
- Save the config with a name. It's stored in S3 so anyone on the team can use it.
- **Important:** if your drawings come from a new vendor or in a new format, you'll need a new config. If you don't see one that fits, contact me — I can create one with you.

### 5. Setting Up a Batch
Back on the main Takeoffs page. Walk through configuring a real batch:
- Type a batch name (e.g., "Sandbox Demo - ISO Set 1")
- Pick a config from the dropdown
- Click **Select Files...**, pick the PDF drawings
- Pick the right unit rates (default or a project-specific set)
- Leave the "Send Missed Makeups and Rates to Admin" checkbox checked

### 6. Processing the Batch
- Click **PROCESS BATCH**
- Watch the status panel update: uploading drawings to S3, Lambda processing, extraction in progress
- Show the **CANCEL** button appearing — explain when you'd use it
- Note: processing time depends on drawing count
- **Production tip:** for the recording I'll either let it run (if short enough) or pre-process a batch ahead of time and "cut to" the completed state to keep the video tight

### 7. The Result — Excel Output
When processing finishes, the Excel file auto-downloads and opens. Walk through the tabs:
- **Material** — every component extracted from the BOM, with size, class, schedule, quantity, rate-driven BudgetMHs, ShopField (Shop or Field), and audit columns (RateSheet, RollupMult, MatlMult, CutAdd, BevelAdd)
- **Labor** — generated labor rows (welds, threading, etc.) tied to the material rows via connection types
- **Summary** — totals
- **Missed Makeups** — components where the fitting makeup lookup failed; includes a Reason column (No Makeup Found / Unclaimed)
- **Missed Rates** — components where the rate lookup failed; shows what thickness/class keys were attempted
- **No Conns** — material items that have no connections
- **VANTAGE** — column-mapped data, ready for import
- Quick mention: this Excel is a working file — you can review, tweak the Material tab, and then re-run RECALC EXCEL to refresh Labor/Summary if you change anything

### 8. Previous Batches
- Click **PREVIOUS BATCHES**
- Show the dialog: list of past batches with metadata (username, config used, drawing count)
- Re-download an existing batch's Excel
- Admins can delete or rename batches
- When you'd use this: someone else processed a batch, you need the file; or you misplaced your local copy

### 9. Recalc Excel
- Click **RECALC EXCEL**
- Pick an existing takeoff Excel file
- Explain: this regenerates Labor, Summary, Missed tabs, etc. from the Material tab — useful if you manually corrected a row in Material and need the downstream calculations to catch up
- Show the resulting refreshed file

### 10. Send to Admin Checkbox
- Re-highlight the "Send Missed Makeups and Rates to Admin" checkbox
- When checked and the takeoff has missed makeups or rates, the Excel is auto-emailed to admins so they can fix the rate sheet or makeup tables for next time
- Recommended: leave it on

### 11. Importing into Activities (Brief)
- Switch to the **PROGRESS** module
- File → **Import from AI Takeoff** → pick the Excel from the takeoff
- Show the dialog briefly (column mapping, profile, BU rollup, fab-per-DWG rollup, ROC split, Create Excel output)
- Do not deep-dive — say "this is fully covered in the Progress video"
- Run the import, show the rows appearing in the Activities grid

### 12. Wrap-Up
- Recap the workflow: Config → Drawings → Process → Excel → Import
- Reminders:
  - One config per drawing format — reusable across batches
  - Send-to-admin keeps the rate sheet improving over time
  - Previous Batches has your history if you need to re-download
  - For new drawing formats or rate issues, call Steve
- Next: watch the **Progress** video for the full Import from AI Takeoff dialog walkthrough

---

## Open Questions for User
1. **Sample drawings** — what PDF set should I use for the demo? Need real drawings (or anonymized) that match an existing config.
2. **Live processing vs. pre-recorded** — let processing run live, or pre-process and cut to the finished state?
3. **Config Creator depth** — full create-from-scratch demo, or just open an existing config in Edit mode and explain what's already there?
4. **Show the actual emailed-to-admin flow** (e.g., screenshot of an admin email) or just describe it?
5. **Recalc Excel demo** — manufacture a manual edit on the Material tab and run Recalc to show the effect, or just describe the feature?
6. **Should "Import from AI Takeoff" dialog get its own short video** instead of being split between this and Progress? It's a substantial dialog with profiles, ROC splits, column mapping, etc.
