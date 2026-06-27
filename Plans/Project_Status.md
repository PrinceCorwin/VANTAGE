# MILESTONE - Project Status

**Last Updated:** June 27, 2026 (ProgressLog cleanup: archived 3 finished projects + rebuilt the table as a clustered, compressed index, 16 GB → 4.7 GB; Deleted Records grid sped up via covering filtered index + ordinal-cached reads; Archive-a-Project feature added to backlog; backlog cleanup)

## ⚠️ Cross-Machine Credential Notes
- **How creds propagate:** `/publisher` (`Scripts/publish-update.ps1`, step 2) regenerates `appsettings.enc` from the publishing machine's local `appsettings.json` on every publish, strips the plaintext from the shipped output, and the release commit pushes the new `.enc`. Other machines pick up the new `.enc` via `git pull`; production picks it up via app update. `appsettings.json` itself is gitignored and never copied between machines — the encrypted file is the propagation channel. `CredentialService` reads plaintext `appsettings.json` first (dev), then falls back to `appsettings.enc`.
- **Rule:** Whenever you edit a credential (rotation, new key, etc.), mirror the same edit on the other PC's `appsettings.json` BEFORE the next publish. The next publish will encrypt whatever is in the publishing machine's local plaintext, so a stale local plaintext silently regresses the rotation for every user. If the two PCs are in sync, publishing from either is fine — verified 2026-05-29 the work PC matched home 28/28.
- **Cleanup:** legacy `Credentials.cs` is unreferenced by any code (verified 2026-05-29 — everything reads `CredentialService`); delete it on all machines + tidy the `.gitignore` comment.

## Deferred to Post-V1
| Feature | Reason |
|---------|--------|
| Drawings in Work Packages | Per-WP location architecture needs design |
| AI Features (other than Progress Scan) | Lower priority for V1 |

## In Progress / Not Started
| Module | Status | Notes |
|--------|--------|-------|
| Analysis | IN PROGRESS | 3x1+3 grid layout, chart filters panel (session-only, lazy-populated) with Reset button, Excel-style (Select All) toggle and per-filter ALL/[N] count badges, dynamic chart sections with selectable visual type/X axis/Y axis, pie/doughnut labels and legends, summary grid with independent filters, Excel export |
| Procore | IN PROGRESS | OAuth + auth dialog + service layer scaffolded; targeted at WP DWG Log fetch |
| AI Features (other) | NOT STARTED | Error Assistant, Description Analysis, etc. |

## Ready to Publish
- _(none — most recent release is current)_

## Active Development

### Work Package Module
- Template editors testing
- PDF preview testing

### MCAA Ratesheet Integration (Phase 2 of the rate-mode toggle)
- **PRD:** `Plans/MCAA_Ratesheet_Plan.md` — 8-step high-level plan and the deferred-details parking lot. Phase 1 (toggle infrastructure + behavior gating) shipped 2026-05-05; Phase 2 is the AI Takeoff fork + key-based MCAA rate lookup.
- **Producer-side status (SkySkraper):** Review pivoted to section-by-section on 2026-05-08 — rebuild each PIPING SYSTEMS slice from cached raw HTML, normalize in Excel, merge into a `FinalMerged` workbook. Done/user-edited: Joints (167 leaves, 5,075 rows), Branch Connections (141 leaves, 9,652 rows; merged into `output/cdx_rates_review_FinalMerged.xlsx` as of 2026-05-14). Active: Fittings (305 leaves, 62,333 rows). Generated and queued for review: Cut Tables, Flanges, Flanges Orifice, TIG Root, Pipe. Remaining MCAA-producer blockers before VANTAGE-side fork work starts: finish Fittings + the five queued section workbooks, decide final lookup-key composition, then build the `xlsx → SQLite` exporter into `output/cdx_weblem_rates.db`.
- **TODO — Import from AI Takeoff: MCAA option on ImportTakeoffDialog.** When the source workbook was generated in MCAA mode, add an import option (radio or checkbox) that rolls the companion CUT labor rows into the per-ISO handling row (PIPE fab/handling per drawing) instead of carrying CUT as separate rows. Driver: exec policy keeps welding as ShopField=1 and pushes everything else to field, but the standalone CUT rows currently land at ShopField=2 and are awkward to reconcile per ISO. Today the user works around this via a pivot table of field welds per ISO + manual ShopField flips. See `project_mcaa_shopfield_policy.md` (auto-memory) and `Services/AI/TakeoffPostProcessor.cs:1271-1277` for the current CUT-row generation site. When this option lands, standalone CUT row generation under MCAA can be made conditional on the option being off.
- **TODO — MCAA AI Takeoff: relabel CUT labor row material to "alloy" for alloy materials.** In MCAA mode only, the material on every CUT labor row should be normalized to `alloy` when the source material is any alloy (CS, SS, HAST, Chrome Moly, etc.). Driver: MCAA cut rates don't differentiate between alloy variants, so collapsing them simplifies the rate lookup. Open question: enumerate the alloy list, OR enumerate the (smaller) exception list of materials that should NOT be relabeled (likely non-metallics — copper, PVC, etc.) and treat everything else as alloy. Lean toward the exception list if it's shorter. Summit pipeline must remain unaffected — gate strictly on MCAA mode.
- **TODO — Route AI Takeoff to MCAA Lambdas + MCAA extraction prompt.** When the producer side finishes (SkySkraper SQLite + final lookup-key composition), VANTAGE's `TakeoffService` / `TakeoffView` still needs the plumbing to actually call MCAA-specific Lambdas (not the Summit ones) and load an MCAA extraction prompt from S3. Likely scope: (1) deploy `mcaa-takeoff-poc` and `mcaa-takeoff-aggregate` Lambdas (currently local-only — no AWS targets exist yet); (2) decide S3 bucket strategy (separate `mcaa-takeoff-*` buckets vs shared `summit-takeoff-config` with `mcaa-` prefixed keys); (3) wire RateMode toggle to select the correct Lambda + prompt + ref tables at submit time; (4) Step Functions orchestrator for MCAA (or extend the Summit one with mode-aware routing). Note: the MCAA Lambda source files (`mcaa-takeoff-poc/`, `mcaa-aggregate-deploy/`) are scratch placeholders — likely to be substantially rewritten once the MCAA data aggregation finalizes, so further pre-rewrite edits to them are theater.
- **TODO — MCAA prompt engineering: defeat the "lazy null" failure mode on material grade (and similar optional fields).** When we work on the MCAA extraction prompt, the persistent risk is the model taking the easy way out and emitting `null` instead of doing the work to extract a value that's actually present. Mitigations to bake into the prompt: (1) require an `evidence` field per optional extraction (the verbatim substring the value came from) OR an explicit `<field>_absent_reason` string ("no ASTM callout in description", "title block grade column blank", etc.) — null with no justification is invalid output; (2) include contrastive few-shot examples — one description where the field IS extractable and one where it genuinely isn't — so the model learns the distinction rather than defaulting to the safer of the two. Description-language variability across clients makes regex impractical (settled 2026-05-14); prompt engineering is the lever.

### MCAA Proxy Mappings — Discovered During Parity-Test Scoping (2026-04-29, updated 2026-04-30)
- **Reference for the future `MCAALaborService` and parity-test work.** Notes from a session-long discussion mapping a real Summit takeoff against the MCAA scrape to identify which Summit components have direct MCAA equivalents and which need proxy lookups.
- **Coverage update (2026-04-30):** Full WebLEM corpus now cached on the producer side — all 1,926 leaves across 10 top-level sections. Items previously flagged as out-of-scope (valve actuators under `Instrumentation > Field Mounted Instruments & Devices`, HVAC specialties, plumbing equipment, etc.) are now reachable. The "no MCAA-piping equivalent" caveat from the original 2026-04-29 note is mostly retired; specific proxy decisions still need to be made per-component during the rate-application pass.
- **Items with no MCAA-piping match (proxy needed).** Figure-8 Blank (`F8B`) → no spectacle / fig-8 / "Blind Flange" as distinct component; MCAA bundles all flange faces (slip-on/weld-neck/blind/threaded/socket-weld) into the single `Flange` rate at `Flanges > <material> > Flange > <pressure>`. Use same-size Flange install rate at the matching material/class as the proxy. Hastelloy camlock fittings (e.g. `HOSE CONNECTION HASTELLOY BW ADAPTER CAMLOCK WITH DUST CAP`) → MCAA has Camlock Fittings under `HVAC Specialties > Camlock Fittings` in Aluminum/Brass-Bronze/Polypropylene/Stainless Steel only; no Hastelloy and no `BW × Camlock` combination. Proxy: SS `MPT × Male Camlock` (or matching gender) install rate plus a `Joints > Nickel Alloy > Butt Weld` for the pipe-side weld.
- **MCAA pricing structure (what to wire into the labor service).** Total MH per Summit takeoff row is ADDITIVE: install rate from item section (`Fittings/Flanges/Valves/Pipe/Nipples`) + joint rate per connection from `Joints` (164 leaves keyed by material × connection method) + prep ops per connection per saved memory rule (`BW = cut + bevel`, `SW/OLW/SCRD = cut`) + applicable post-fab from `Hydrotesting/Stress Relieving/Preheat`. Items section rates the per-fitting handling/rigging/fit-up regardless of joint type — MCAA does NOT bundle the bolt-up or weld into the item rate.
- **Existing-takeoff parity-test catch.** The reference workbook `Plans/All_Labor_Combined2-ColumnsAdjusted-AiTakeoff.xlsx` was generated with the pre-fix `TakeoffPostProcessor` and is missing FLGB/FLGLJ install rows. Two options when running the rating pass: re-run AI Takeoff post-processing on the source drawings to regenerate, or synthesize the missing fab rows in-place from the Material sheet during the rating step.

## Tutorial Videos

Series of short tutorial videos for end users. Each item below needs a plan and script developed in its corresponding folder under `Plans/Tutorials/`.

- [ ] **Intro Video** (`Plans/Tutorials/Intro/`) — Installation, updates, plugins, and the other MainWindow menu items (Tools, Settings, Help, status bar, etc.)
- [ ] **Progress Module Video** (`Plans/Tutorials/Progress/`) — Grid basics, sorting/grouping/filtering, editing, copy/paste, find/replace, sync, snapshots
- [ ] **Schedule Module Video** (`Plans/Tutorials/Schedule/`) — Master/detail layout, P6 import/export, editing, quick filters, UDF mapping, change log
- [ ] **Work Packages Video** (`Plans/Tutorials/WorkPackages/`) — Templates (WP and Form), tokens, generate tab, PDF output
- [ ] **Progress Books Video** (`Plans/Tutorials/ProgressBooks/`) — Layout configuration, column selection, generation, AI Progress Scan
- [ ] **Analyse Module Video** (`Plans/Tutorials/Analyse/`) — Summary grid, group by, user/project filters, metrics, conditional coloring
- [ ] **Takeoffs Module Video** (`Plans/Tutorials/Takeoffs/`) — AI-powered piping takeoff extraction, configs, regions, batches, downloads
- [ ] **Admin Video** (`Plans/Tutorials/Admin/`) — Edit Users, Edit Projects, Manage Snapshots, Progress Log, Project Rates, S3 Drawings (admin-only features)

## Feature Backlog

### Critical
*(None currently.)*
### High Priority
- **Migrate Email Service to company Azure account (2026-06-18).** `EmailService.cs` currently sends via an Azure Communication Services resource on Steve's personal Azure subscription. With the director's company-Azure access restored, stand up an equivalent ACS resource (sender domain + connection string) under the company account, swap the connection string in `appsettings.json` on both PCs, regenerate `appsettings.enc` via `/publisher`, and decommission the personal-account ACS resource after one release cycle of overlap. Coordinate sender-domain change with anyone who has email filters keyed on the current sender.
- **Mobile/iOS Version (iPad)** — Execs want iPad app for field supes to submit progress. Needs architecture discussion: native iOS, cross-platform framework, web app, API design, offline sync, etc.
- **AI Takeoff Lambda + prompt — known issues backlog (none currently broken).** Surface area for defensive improvements and design refinements identified during the 2026-04-22 prompt/Lambda review and reaudited 2026-05-09 against the current deployed code. Items the original review flagged that have already been fixed are not listed here (legacy direct-image path removed, `clean_quantity` made pass-through, `MODEL_ID` updated to Sonnet 4.6, `extraction_notes` truncation made consistent, `load_batch_extraction_keys` defensive try/except added, dead locals in `build_material_rows` cleaned up, output schema aligned with body rules, etc.).

  **Minor cleanups (next time you touch each Lambda):**
  3. **Flagged tab missing columns vs Material** — Flagged omits `quantity`, `length`, `commodity_code`, and all `tb_*` fields. A reviewer doesn't see full context; C# matches back to Material by `(drawing_number, item_id)` — an implicit data contract. Decide between full mirror or document the contract explicitly.
  4. **Unused `count` in `consensus_backfill_class_rating`** — `winner, count = counter.most_common(1)[0]`; `count` is never read. Rename to `_`. Lint noise.
  5. **Inconsistent None handling in `build_material_rows`** — some fields use `item.get("x") or ""`, others use bare `item.get("x")`. Both render as empty Excel cells but the inconsistency is confusing for readers. Pick one (recommend bare `.get()` — None is the honest signal) and apply consistently.
  6. **`from botocore.config import Config` placement** in the extraction Lambda — currently mid-module after the `s3` client creation. Move to the top with other imports. PEP 8 cosmetic.

  **Awareness only (defer unless triggered by real symptoms):**
  7. **`class_rating` consensus backfill is exact-string, unweighted** — groups by raw description (case + whitespace sensitive), counts low-confidence votes the same as high-confidence. OCR variants never merge. Consider normalizing descriptions before grouping and weighting votes by confidence.
  8. **`bom_row_count` vs `len(bom_items)` no cross-check** — both come from the model and can silently disagree. Zero-BOM guard uses `bom_row_count` only. Trust `len(bom_items)` as ground truth and log a warning on mismatch.
  9. **Per-image `"Image N: {label}"` text block** — small token tax on every multi-image Bedrock call. Consider A/B-testing with the labels removed; strip if no accuracy drop.
  10. **No app-level retry around `bedrock.converse`** — only botocore adaptive retry (max_attempts=10) handles throttling/5xx. Add application-level retry with backoff if batch reliability ever becomes an issue at higher concurrency.
  11. **`render_pdf_page` memory guard assumes RGB** — `width * height * 3` under-counts RGBA. Currently safe because `alpha=False` is set. If alpha is ever enabled, switch to `width * height * 4` or derive from `pix.n`.
  12. **Excel column width sampled from first 100 rows only** — wide values in rows 101+ don't widen their column. Not worth changing unless users complain.
  13. **`batch_id` not sanitized before S3 key construction** — flows directly into S3 prefixes. Internal tool, controlled caller (Vantage app), low risk.
  14. **Hardcoded color constants inside `generate_excel`** — header fill `DAEEF3`, border style, etc. Hoist to module-level constants if more tabs/styles get added.


### Medium Priority
- **Archive a Project (admin) — move a finished project's ProgressLog records to the archive table from the UI.** Today archiving means an admin hand-runs copy→verify→delete SQL against the backend (done manually 2026-06-27 for `23.006.`/`23.007.`/`24.001.` → `VANTAGE_global_ProgressLog_archive`). A menu item would remove the backend round-trip when a job closes out. Lower priority for now — Steve is comfortable doing it from the backend, especially since the ProgressLog table was just cleaned up and reorganized (2026-06-27) and won't need attention for the foreseeable future. **Design (v1):** admin-only dialog (gate on `AzureDbManager.IsUserAdmin`; sibling to `AdminSnapshotsDialog`/`ManageProgressLogDialog`, reuse the `SfBusyIndicator` spinner + type-to-confirm pattern). Grid lists projects from `VANTAGE_global_ProgressLog` with row count + last-upload date (`GROUP BY Tag_ProjectID`). **Single-select — deliberately NO multiselect** (safer for a heavy, rarely-reversed bulk move; keeps the confirm step unambiguous). The move runs entirely server-side (no data round-trip, same principle as the existing `INSERT...SELECT` upload): schema-compatibility pre-flight (live vs archive column compare) and abort on drift; build the column list at runtime from `sys.columns` excluding the identity column (never hardcode the ~90-column list); then copy → verify-count → **batched delete** (e.g. 50k rows/loop to protect the transaction log) following the proven manual pattern, with an idempotency guard (clear any stale archive rows for the project before copying so a re-run can't duplicate). `CommandTimeout` tiered like the upload code. Audit-log each archive with username. **Cross-app caveat:** REQit reads `dbo_VANTAGE_global_ProgressLog`, so archiving removes the project from the live table for REQit too — the confirm dialog must state "archived = removed from the live table for ALL apps." **v2:** Restore-from-archive button (rows are still in the archive table, so un-archiving is cheap).
- **Grid layout selection not persisting across sessions (Progress view).** User reports 2026-06-25 that selecting a saved layout from the layouts menu doesn't survive an app restart — the grid comes back up on the default schema instead of the last-selected named layout. Need to verify which side is broken: is the active-layout key (UserSettings or per-user setting) not being written on layout-apply, not being read on grid init, or being read with the wrong key name. Likely files: `Dialogs/ManageLayoutsDialog.xaml.cs` for apply/save, `Views/ProgressView.xaml.cs` for grid init / column-order restore on load. Confirm whether width/order/visibility of the default-schema columns ARE being persisted (column-persistence design from `UserSettings`) separately from the named-layout selection — the two are different settings and either could be the broken one.
- **Progress search bar — broaden to all data columns.** Tooltip on `txtGlobalSearch` says "Search all columns" but `PassesGlobalSearch` in `Views/ProgressView.xaml.cs:4779-4799` only checks 14 hand-picked fields. ProjectID (visible in the grid) is a notable miss, plus most UDFs, material specs, drawing rev/sheet, and numeric columns. Plan: replace the hand-rolled OR chain with a static reflection-built accessor cache over `Activity` — include all public writable properties (auto-excludes calculated/read-only since they have no public setter), skip `DateTime`/`bool`, and deny-list sync internals (`LocalDirty`, `SyncVersion`, `IsBulkSelected`, `DateTrigger`). Use strongly-typed delegates via `Delegate.CreateDelegate` per type (string/int/double/long) so there's no per-call reflection cost at 100k-row scale. Net result matches the tooltip: every real data column searchable, dates and calculated columns excluded.
- **Help manual screenshots audit** — Review all sections of `Help/manual.html`, update outdated screenshots to reflect current UI, and add missing screenshots for features that have none (e.g., ActNO Split Ownership Check dialog, Sync Incomplete warning, any other recently added dialogs or UI changes).
- **MSI/MSIX installer** — Replace custom installer with MSI (WiX Toolset) or MSIX packaging to get genuine Windows install integration. Current custom installer registers via registry but Windows Search won't execute `UninstallString` directly — only MSI and UWP/MSIX apps get direct uninstall from search context menu. Current setup works via Settings > Apps.
- **User-editable header template for WP** — Allow customizing header layout
- **Complete RateEquiv mappings** — Finish adding all component-to-EstGrp mappings in `RateSheetService.cs` (ComponentToEstGrp dictionary). Currently has valve types, fittings, GGLASS, METER, HOSE, HEAT→INST, etc.
- **Unify component reference tables** — Ensure all components are represented across CompRefTable, RateSheet.json, and FittingMakeup.json. Audit for missing entries and add equivalence mappings (MakeupEquiv in FittingMakeupService.cs) where components share identical values. SCRD/FLG (all sizes incl. 3"), wildcard SCRD/CPLG, classless FLG wildcards (BW/SW/SCRD), wildcard SCRD/TEE and SCRD/90L added. GRV→SW→BW makeup fallback chain. GAUGE excluded from makeup lookup.

### Low Priority
- **Push re-claim of reassigned rows — narrow race, low real-world risk.** Demoted from Critical 2026-06-27: in practice admins coordinate reassignments with users, so an admin reassign landing in the same 1–3s window as that user's push (10–30s on a 50K-row sync) is near-impossible, and the worst case is one record silently reverting to its old owner — the admin just reassigns again. Kept for reference, not urgent. Detail: Push DOES gate on ownership — `SyncManager.cs:281-296` reads Azure's `AssignedTo` into `ownershipMap`, then the filter drops any row where local's `AssignedTo` doesn't match, returning "No longer assigned to you". The common-case sequence (user edits 9am, admin reassigns 10am, user syncs 11am) is safely rejected. The bug is a narrow race within a single push call: between the Step 3 ownership SELECT (`:267-279`) and the Step 3 staging UPDATE (`:354-363`), an admin reassign that lands in the gap gets silently overwritten because the staging UPDATE writes local's `AssignedTo`. Fix shapes if ever needed: (a) wrap Steps 2/3 in a SERIALIZABLE transaction so range locks on the joined UniqueIDs block concurrent admin UPDATEs until commit; or (b) add `OriginalAssignedTo` to `#UpdateStaging` plus `AND a.AssignedTo = s.OriginalAssignedTo` on the staging UPDATE JOIN (compare-and-set, no broader locking).

### V2 Data Model
- Add ClientEarnedEquivQty column to Activities table, Azure VMS_Activities, and ColumnMappings (maps to OldVantage `VAL_Client_Earned_EQ-QTY`) - currently ignored during import

### AI Features (see InCode_AI_Plan.md)
| Feature | Status |
|---------|--------|
| AI Scan pre-filter | Not Started - Local image analysis to detect marks before calling API (skip blank pages, reduce cost) |
| AI Error Assistant | Not Started |
| AI Description Analysis | Not Started |
| Metadata Consistency Analysis | Not Started |
| AI MissedReason Assistant | Not Started |
| AI Schedule Analysis | Deferred |

### Post-V1: Drawings Architecture
- Design per-WP drawing location system (options: token paths, per-WP config, Drawings Manager)
- Fix preview display
- Fix layout/orientation for 11x17 drawings
- Implement Procore fetch
- Consider AI-assisted drawing matching (DwgNO formats, revisions, sheet numbers)

**Code disabled for v1 (re-enable when drawings architecture is ready):**
| File | What to re-enable |
|------|-------------------|
| `WorkPackageView.xaml` | Remove `Visibility="Collapsed"` from Drawings section Border (~line 238) |
| `WorkPackageView.xaml.cs` | Remove `.Where(t => t.TemplateType != TemplateTypes.Drawings)` from `PopulateAddFormMenu()` |
| `WorkPackageView.xaml.cs` | Remove filter in `ApplyWPFormsListFilter()` method |
| `WorkPackageView.xaml.cs` | Remove early return in `BuildDrawingsEditor()` and `#pragma warning` directives |
| `DrawingsRenderer.cs` | Remove early return in `Render()` method and `#pragma warning` directives |

### Syncfusion Features to Evaluate

**Dashboard Module (Post-V1):**
| Feature | Description |
|---------|-------------|
| Column/Stacked Chart | Daily/weekly activity completion counts, productivity trends |
| S-Curve (Line/Area Chart) | Planned vs actual progress over time |
| Pie/Doughnut Chart | Distribution by WorkPackage, PhaseCode, or RespParty |
| Gantt Chart | Visual schedule with dependencies (complements P6 import) |
| Radial Gauge | Dashboard widget for overall project % complete |
| Bullet Graph | Performance vs target KPIs per work package |

**V2:**
| Feature | Description |
|---------|-------------|
| Docking Manager | Visual Studio-like docking for flexible panel/toolbar layouts |

**Schedule Module:**
| Feature | Description |
|---------|-------------|
| TreeGrid (SfTreeGrid) | Hierarchical WBS display with parent/child relationships |
| Critical Path Highlighting | Auto-highlight critical path activities (P6 provides float data) |

### Deferred / Considered, Not Built
- **Admin Snapshots: WeekEndDate override on re-upload** — considered an admin override so old snapshots could be re-uploaded under a new week (avoiding re-snapshotting closed-out activities). Judged the complexity not worth the bug surface for a small workflow gain. Current invariant retained: `WeekEndDate` = when the snapshot was taken. Filed here so the conversation doesn't get re-litigated; if it comes up again, this is the prior call. (Was previously in `Plans/Decisions.md`.)

### Documentation Backlog
- **Discuss workflow / dev-setup doc format and content.** Decisions.md is for VANTAGE runtime facts only; dev workflow content (Claude Code settings split, CLAUDE.md / Security_Guidelines.md routing rationale, repo conventions, build/test/release procedures used by humans) should live somewhere else. Considering a new `Plans/Workflow.md` (or similar) that another developer could use to set up the VANTAGE dev environment from scratch. Need to discuss the right scope, format, and which existing scattered notes to consolidate.

### Shelved
- **AI Takeoff — multi-drawing PDFs** — supporting PDFs that contain multiple drawings (one per page) was on the AI Takeoff backlog. Bluebeam already specializes in splitting multi-page PDFs into per-drawing files, and the workflow there is faster and lower-risk than rewriting the AI Takeoff intake to detect and split pages. Users do the split in Bluebeam before upload; AI Takeoff continues to treat one uploaded PDF as one drawing.
- **Mouse Shift+Click first→last row on Progress grid at 100k-row scale** — still freezes the UI. Ctrl+A and `Actions → Select All` were refactored 2026-05-07 to bypass Syncfusion's per-cell selection model entirely (transient `IsBulkSelected` flag + `RecordOwnershipRowStyleSelector` data trigger), but Shift+Click is Syncfusion's own range-extend gesture and we can't intercept it from outside the grid without replacing the selection controller. User said "deal with it another time, if ever." Ctrl+A is the supported scale-safe path.
- Find-Replace in Schedule Detail Grid - deferred to V2; may need redesign of main/detail grid interaction
- Offline Indicator in status bar - clickable to retry connection
- Disable Tooltips setting (see DisableTooltips_Plan.md)
- Interactive Help Mode - click UI controls to navigate to documentation (see Sidebar_Help_Plan.md)
- AI Sidebar Chat (see Sidebar_AI_Assistant_Plan.md) - conversational AI tab in the sidebar; UI scaffolding removed April 2026, plan doc retained for possible future revival

## Test Scenarios Validated

- Import -> Edit -> Sync -> Pull cycle
- Multi-user ownership conflicts
- Deletion propagation
- Metadata validation blocking
- Offline mode with retry dialog
- P6 Import/Export cycle
- Schedule filters and conditional formatting
- Detail grid editing with rollup recalculation
- Email notifications
- Admin dialogs (Users, Projects, Snapshots)
- UserSettings export/import with immediate reload
- Log export to file and email with attachment
- User-defined filters create/edit/delete and apply
- Grid layouts save/apply/rename/delete and reset to default
- Prorate MHs with various operation/preserve combinations
- Discrepancy dropdown filter
- My Records Only sync (toggle on/off, full re-pull on disable)
- Work Package PDF generation and preview
- Manage Snapshots: delete multiple weeks, revert to single week with/without backup
- Manage Snapshots: grouping by ProjectID + WeekEndDate + ProgDate, delete spinner
- Schedule Change Log: log detail grid edits, view/apply to Activities, duplicate handling
- Activity Import: auto-detects Legacy/Milestone format, date/percent cleanup, strict percent conversion
- Snapshot save: immediate spinner, date auto-fix, conditional NOT EXISTS, sync optimization, elapsed timer
- GitHub 2FA enabled — verified push access still works
