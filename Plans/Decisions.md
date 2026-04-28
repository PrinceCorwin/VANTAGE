# VANTAGE: Milestone — Design Decisions

Permanent record of architectural choices, design rationale, and implementation decisions. Consult this when asking "why did we do X?" before changing existing behavior.

---

## Data Model

### Dates Stored as TEXT, Not DATETIME
**Decision:** All date columns use TEXT/VARCHAR in both SQLite and Azure SQL.
**Why:** P6 Primavera exports dates as text strings. Using TEXT avoids conversion issues and format mismatches during import/export cycles.

### ActStart/ActFin Are Required Metadata, Not Auto-Populated
**Decision:** Removed auto-set behavior. ActStart required when % > 0, ActFin required when % = 100. Red cell highlights and sync blocking enforce compliance.
**Why:** Auto-populating dates was silently overriding user intent. Explicit control ensures data completeness without surprises.
**Date:** February 2026

### AzureUploadUtcDate Is Pull-Only
**Decision:** Removed from SyncManager push columns. Admin sets value on Azure during upload; users receive on pull but cannot overwrite.
**Why:** Prevents users from accidentally corrupting the upload timestamp that only admins should set during Progress Log upload.
**Date:** January 2026

### V2 Data Model: ClientEarnedEquivQty Deferred
**Decision:** Column exists in OldVantage (`VAL_Client_Earned_EQ-QTY`) but is ignored during import. Will be added to Activities, Azure VMS_Activities, and ColumnMappings in V2.

---

## Sync

### Push Failure Blocks Pull to Protect Local Data
**Decision:** If push to Azure fails, the pull is skipped entirely. Local dirty records are preserved for retry.
**Why:** Previously, push failures were silent — the pull ran anyway, overwriting local changes with old Azure data and clearing LocalDirty flags. Users saw edits revert with no error shown.
**Date:** April 2026

### Ownership Check Removed from Sync, Moved to Tools Menu
**Decision:** Removed `CheckSplitOwnershipAsync` (JOIN on SchedActNO across 65K+ records) from the sync path. Created standalone "ActNO Split Ownership Check" tool under Tools menu.
**Why:** The query had no index and caused timeouts, blocking syncing entirely. The check is useful but doesn't need to run on every sync.
**Date:** April 2026

### Push Verifies Actual Rows Updated via OUTPUT INTO
**Decision:** The Azure UPDATE in push Step 3 uses `OUTPUT INSERTED.[UniqueID] INTO #UpdatedIds` to capture exactly which rows were updated. Only confirmed rows get LocalDirty cleared. Previously, all records were blindly marked as successfully pushed regardless of actual update count.
**Why:** If the UPDATE affected fewer rows than expected (transient Azure issue, staging table mismatch), unverified records had LocalDirty cleared and the pull overwrote local data with old Azure values. Users saw pasted values revert after sync with no error. Now missed rows keep LocalDirty=1 and retry on next sync.
**Date:** April 2026

### Unlimited SQL Timeouts in Sync Flow
**Decision:** All CommandTimeout and BulkCopyTimeout values in push/pull set to 0 (unlimited). Azure ConnectTimeout also 0.
**Why:** Users with slow internet were hitting 30-second default timeouts, causing "Execution Timeout Expired" errors. The sync operations must complete regardless of connection speed — there's no benefit to aborting partway through.
**Date:** April 2026

### LastPulledSyncVersion Tracks Pulled Records, Not Live MAX
**Decision:** After pulling, use the max SyncVersion from records actually pulled — not a separate query for current MAX.
**Why:** Race condition — if another user pushed between the pull query and the MAX query, those records would be permanently skipped on future pulls.
**Date:** April 2026

### Submit Week Snapshots from Local Data, Not Forced Sync
**Decision:** Removed forced sync and split ownership check from Submit Week. Snapshots capture local data directly.
**Why:** Enables historical snapshot scenarios (e.g., restore a backup and snapshot that point-in-time state). Faster submits with fewer network round-trips.
**Date:** February 2026

---

## Takeoff Pipeline

### CUT/BEV Folded into Connection Rows, Not Separate Labor
**Decision:** CUT and BEV no longer generate separate labor rows. BW connections include CUT+BEV rate additions, SW/THRD include CUT.
**Why:** Cut/bevel are always performed with the connection. Folding them in prevents row explosion in the Labor tab and matches how labor is actually performed.
**Date:** March 2026

### GSKT/BOLT Excluded from Labor Generation
**Decision:** GSKT and BOLT material items no longer create any labor records.
**Why:** These are material-only items. Their labor is accounted for in the BU (bolt-up) connection rows.
**Date:** March 2026

### Rate Lookup: Simplified 4-Step Fallback Chain
**Decision:** Removed OLW/SW class rating tier fallback system. New chain: try thickness as-is → toggle leading "S" → try class rating → try size-only.
**Why:** The tier system (40/S40/STD/2000 equivalence groups) was complex. The S-toggle approach handles the same edge cases more simply. Dual-size parsing for all components further reduced misses.
**Date:** March 2026

### Rate Sheet Keys Match Component Names Directly
**Decision:** Renamed all 56 EstGrp keys to short names. Components like INST, PIPE, SPL, BEV match rate keys directly. Only valve types (VBL/VGT→VLV) and fittings (90L/TEE→FTG) need the mapping dictionary.
**Why:** Eliminates indirection. The `DirectMatchComponents` set was redundant when `ResolveEstGrp` can just use the component name as the key.
**Date:** March 2026

### BudgetMHs Formula: RateSheet × RollupMult × MatlMult
**Decision:** Changed from `RateSheet × RollupMult × max(RollupMult, MatlMult)` to simple multiplication.
**Why:** The max() approach double-counted the rollup multiplier when it exceeded the material multiplier. Simple multiplication is more transparent and matches industry conventions.
**Date:** March 2026

### FS Material Group Auto-Corrected from Pipe Material
**Decision:** Field support (FS) `Matl_Grp` values are auto-corrected to match the pipe of the same size on the same drawing.
**Why:** AI often defaults FS to CS (carbon steel) because material isn't in the FS description. When multiple pipe materials exist, picks the non-CS value since CS is the assumed default.
**Date:** April 2026

### ShopField Post-Processing: Lambda Sets All to Shop, Post-Processor Corrects
**Decision:** Lambda sets all material rows to ShopField=1 (Shop). Post-processor corrects to Field (2) for: BU/SCRD-only connections, inherently field components (FS/BOLT/GSKT/WAS/INST/GAUGE), and items with zero connections.
**Why:** Simpler Lambda logic. Correction rules are data-driven and easier to maintain in the post-processor. PIPE always stays Shop. Mixed connection types stay Shop.
**Date:** April 2026

### Send Missed to Admin: Default Unchecked, No Persistence
**Decision:** "Send Missed Makeups and Rates to Admin" checkbox defaults to unchecked on every tab load. No saved preference.
**Why:** Users were unintentionally emailing admins on every batch because the checkbox persisted as checked. Default-off makes the email an intentional opt-in action each time.
**Date:** April 2026

### S3 Drawings Deleted After Processing
**Decision:** Uploaded drawings automatically deleted from S3 after takeoff processing completes.
**Why:** Drawings were being overwritten on each run anyway (to support new revisions with same filename), so persisting them served no purpose.
**Date:** March 2026

### Config Naming by Username, Not Client/Project
**Decision:** Config S3 path uses `clients/{username}/{config-name}.json` instead of separate client/project fields.
**Why:** Each user gets their own namespace. Simpler than requiring a client/project hierarchy for configs.
**Date:** March 2026

### In-App Results Panel Removed, Excel Is the Deliverable
**Decision:** Removed the in-app results summary panel from TakeoffView.
**Why:** The same information is available in the downloaded Excel file. The in-app panel was redundant.
**Date:** March 2026

### MakeupEquiv Two-Pass Lookup
**Decision:** `MakeupEquiv` dictionary (ADPT→FLG, FLGR→FLG) with two-pass lookup: direct match first, then equivalent component.
**Why:** Performance — most lookups succeed on direct match. Two-pass also makes the lookup hierarchy explicit and predictable.
**Date:** March 2026

### ROC Splits Read Raw Takeoff Data, Not Mapped Activity Properties
**Decision:** `ApplyROCSplitsAsync` reads ShopField and Component directly from the raw takeoff Excel row data instead of from the mapped Activity object properties (UDF1/UDF6).
**Why:** Users configure column mappings in their import profile — ShopField might map to `UDF1`, `ShopField`, or be unmapped entirely. Hardcoding to `activity.UDF1` made ROC splits silently fail if the mapping didn't match. Reading from raw data makes the feature work regardless of mapping configuration. Also added text value support ("Shop"/"Field") alongside numeric ("1"/"2").
**Date:** April 2026

### Failed DWGs Tab Owned by Aggregation Lambda, Not the App
**Decision:** The "Failed DWGs" tab in the batch Excel is written exclusively by the AWS Aggregation Lambda from S3 failure markers (one `failures/{drawing}.json` per failed drawing, emitted by the extraction Lambda's exception handler and by its zero-BOM-row guard). The WPF app does not create, overwrite, or delete this tab on any path — not on initial download, not on Recalc Excel, not on re-download from Previous Batches. `TakeoffPostProcessor.GenerateLaborAndSummary` was modified to drop its `failedDrawings` parameter and remove the write/delete branch entirely; the tab flows through untouched on every post-processing pass.
**Why:** The prior app-side `WriteFailedDrawingsTab` helper wrote a 1-column tab from an in-memory list (`_failedDrawings`) captured during SFN polling. The new Lambda schema carries 4 columns with diagnostic depth the app can't reconstruct: Drawing Name, Source Key (full S3 path), Error (the actual exception message or "Zero BOM items extracted"), and a UTC timestamp. Reading failures from S3 markers also means re-runs and Recalc Excel present a stable, full history rather than "whatever we captured in RAM this session." Keeping the write responsibility in one place (Aggregation Lambda) eliminates the class of bug where a null/empty `failedDrawings` parameter silently destroyed the existing tab.
**Date:** April 2026

### Failed Drawing Counts Come from the Excel, Not the Step Functions Output
**Decision:** The WPF polling loop no longer attempts to parse per-drawing counts from the Step Functions execution output. It only checks the top-level `status` field for aggregation-level failure. The actual success/failure counts are computed after download by counting the data rows in the batch Excel's "Failed DWGs" tab (`LastRowUsed.RowNumber() - 1` when populated, `0` when the tab is missing or carries the Aggregation Lambda's "No failed drawings…" sentinel in cell A1). `succeeded = totalSubmitted - failedDrawings`.
**Why:** The state machine's `BatchComplete` Pass state only forwards `status` / `batch_id` / `excel_path` — it strips the Aggregation Lambda's rich return payload (`total_drawings`, `total_failed_drawings`, etc.). Both naive fixes have downsides: modifying the Pass state requires AWS Console coordination and still leaves the app dependent on SFN output shape; reading the Excel leverages data the app already has after download and works no matter how the Pass state is later reconfigured. Tradeoff: the "N succeeded, M failed" status message now appears after the Excel save completes (not before), which added a small UX change — preliminary "Completed in {elapsed} — downloading results..." status bridges the gap.
**Date:** April 2026

### Size and Quantity Returned Raw-as-Printed; Conversion Centralized
**Decision:** Claude returns both `size` and `quantity` exactly as printed on the drawing — fractions like `"3/4"` and `"1-1/2"`, reducing-size strings like `"6X4"`, and length strings like `"27'10\""`, `"10 FT 2 IN"`, `"5.5 ft"`, `"41.3'"` all flow through the extraction JSON unchanged. The aggregation Lambda's `normalize_size()` converts fractional sizes to decimals at the Excel write boundary; the C# app's `NormalizeMaterialQuantities()` (called once from `GenerateLaborAndSummary` right after `WriteMaterialShopField`) converts quantity strings to decimals and writes them back to the Excel Material tab. Downstream consumers (connection explosion, SPL generation, PIPE fab, `ApplyRates`, `ParsePipeLengthFeet`) read the already-normalized double.
**Why:** Vision models are substantially more accurate when echoing printed text than when doing arithmetic on what they read. The original prompt asked Claude to convert feet+inches to decimal feet and fractions to decimals; in practice that produced silent wrong values when drawings used unusual unit forms. Centralizing conversion in one place per value type (Lambda for size, C# app for quantity) makes parse failures visible — both layers log a structured warning (`SIZE_NORMALIZATION_FAILED`, `QUANTITY_PARSE_FAILED`) and leave the raw text in place so a reviewer can correct the cell and rerun. Replaces a prior multi-layer silent-fallback design where Claude could return raw text, the Lambda could pass it through unconverted, and the C# side could default to `1` or `0` with no user-visible indicator that any step had failed.
**Date:** April 2026

### Unmatched Material Returns Null, Not Default-CS
**Decision:** When Claude can't match a BOM description's material to the Material Reference Table, `matl_grp` and `matl_grp_desc` are returned as `null` instead of defaulting to `"CS"` / `"CARBON STL"`. The C# app's `CorrectFsMaterial` step still inherits pipe material for FS (field support) rows on the same drawing, so FS behavior is unchanged. Non-FS rows with unmatched materials now surface as empty cells on the Material tab for manual review.
**Why:** The old CS default masked two distinct problems. FS items whose descriptions don't mention material are always overwritten app-side by the pipe lookup regardless of what the Lambda returned — the CS guess did no work there. Non-FS items with exotic or non-standard materials (Monel, Inconel, chrome-moly variants outside the reference table) used to silently flow into rate calculations with the wrong material multiplier, with no indicator on the Material tab that anything was guessed. Returning null surfaces these cases as empty cells; reviewers spot them and fill in the correct value.
**Date:** April 2026

### Labor Tab Description Does Not Include Pipe Spec
**Decision:** The `Description` column on the Labor tab for connection rows (BW/SW/BU/SCRD/OLW/THRD) is built from `"{size} IN - {thickness} - {class} - {material} - {connType}"`. The pipe spec token was removed.
**Why:** The prior `FindPipeSpec` heuristic picked the first title-block field whose key contained the substring "spec" — fragile on drawings with multiple spec fields (Pipe Spec, Coating Spec, Insulation Spec — it would sometimes latch onto Coating Spec) and silently broken on drawings that labeled the field something else entirely (e.g., "Pipe Schedule"). Every title-block field already flows to the Labor tab as a dedicated trailing column, so injecting a best-guess spec into the Description added no information — only confusion when the guess was wrong. Reviewers read the spec from its own column.
**Date:** April 2026

### Legacy Direct-Image Extraction Path Removed from Lambda
**Decision:** The extraction Lambda's pre-config-based calling convention was deleted — the entire `else` branch of `lambda_handler` (accepting `s3_path` or `bucket`+`key` event shapes for pre-cropped images), the `parse_input` / `load_drawing_as_image` / `convert_pdf_to_png` / `convert_tiff_to_png` / `call_bedrock` single-image helpers, and the "Legacy helper functions" section header (~140 lines). `lambda_handler` now requires `config_path` in the event and raises `ValueError` if missing.
**Why:** The legacy path predated the config-based cropping system. It accepted pre-cropped images (an operator manually isolated the BOM outside AWS) and ran Bedrock on them directly. Since `TakeoffService.StartBatchAsync` was wired up, every Step Functions execution sends the new-format payload (`{config_path, bucket, drawing_keys, rev_bubble_only}`) and no other caller remains — full repo audit confirmed no `AmazonLambdaClient` in the C# app, no `s3_path` references outside the Lambda itself, and no test harnesses using the legacy shape. Deleting removed ~140 lines of unexercised code and also closed a non-batch zero-BOM silent-success edge case for free (it only existed because the legacy path set `batch_id = None`).
**Date:** April 2026

### Size Normalization Moved Lambda → C# (Mirrors Quantity Pattern)
**Decision:** Removed `normalize_size`, `normalize_single_size`, `format_decimal`, and the `import re` statement from the aggregation Lambda. Both `build_material_rows` and `build_flagged_rows` now pass `item.get("size")` through unchanged. Size normalization (fractions → decimals, mixed numbers, unicode fractions, en/em dashes, reducer "X" handling) is performed app-side in `TakeoffPostProcessor` as Step A1 — `NormalizeMaterialSizes` runs immediately after `ReadMaterialTab`, before `NormalizeTeeSizes`, and writes the normalized strings back to the Material worksheet via `WriteMaterialSizes` (mirror of `WriteMaterialQuantities`). Five compiled regex statics (`SizeMixedNumberRegex`, `SizeFractionRegex`, `SizeReducerSplitRegex`, `SizeWhitespaceAroundHyphenRegex`, `SizeWhitespaceCollapseRegex`) plus a `UnicodeFractions` dictionary back the new `TryNormalizeSize` / `TryNormalizeSingleSize` / `FormatSizeDecimal` helpers. Behavior is intentionally identical to the deleted Python — same regex patterns, same vulgar-fraction map, same 4-decimal-place truncation — verified via a 16-case Python parity harness during implementation.
**Why:** Quantity already moved app-side in the 2026-04-22 review for exactly the same reasons; sizes had been left behind only because they were already "working." User raised the inconsistency: tweaking a Lambda regex requires an AWS deploy and only helps new takeoffs, while a C# change ships in the next app release and applies to every Recalc Excel pass on existing takeoffs. Centralizing both Lambda-side and app-side conversion eliminates the two-implementation maintenance burden (Python regex set + C# regex set, formerly required to agree). Defense-in-depth gain: when the C# normalizer fails on a malformed input, the row falls through to the new Malformed Sizes guard in `ExplodeMaterialRow` rather than getting silently passed through with a CloudWatch warning that nobody reads.
**Date:** April 2026

### Malformed-Size Rows Skipped from Labor and Surfaced to a Dedicated Tab (Loud Over Silent)
**Decision:** When a material row's `Size` contains an unbalanced "X" — i.e., contains `x`/`X` AND `FittingMakeupService.ParseDualSize` returns null — `ExplodeMaterialRow` adds the row to a new `_malformedSizes` collection and returns immediately, skipping the entire labor-explosion path (no fab row, no connection rows, no THRD companions). A new "Malformed Sizes" worksheet is written from that collection with columns Drawing Number, Item ID, Component, Size, Quantity, Connection Type, Raw Description, and a fixed Reason string ("Size contains unbalanced 'X' — labor generation skipped. Verify against drawing."). The tab is added to `ReorderTabs` between "Failed DWGs" and "Missed Makeups", and the Summary tab now carries a "Malformed Sizes" count line under Low/Medium Confidence. The same trigger condition exists for "5x5x5" (three-part split → ParseDualSize null) and any non-numeric token on either side of the X.
**Why:** Real bug observed in the 2026-04-23 takeoff: model emitted `Size = "0.5x"` for item 12 (a wrap-line miss on a TEE). `ParseDualSize` returned null, the fallback `GetDouble(mat, "Size")` parsed `"0.5x"` as 0 (the `x` made it non-numeric), and six connection labor rows (3 SCRD + 3 THRD companions) were silently emitted at size 0 with no rate, no `BudgetMHs`, and no signal beyond an obscure size-0 entry on Missed Rates. Two design options were considered: silent recovery (`ParseDualSize` returns the present side, e.g., `(0.5, 0.5)` for `"0.5x"`) or loud flag. Loud flag was chosen because the C# regex genuinely cannot infer the missing portion — only the drawing knows whether the outlet was 1/2 or 1 1/2 — and silently picking either is worse than skipping. The new prompt's size-validity contract should prevent the model from ever emitting unbalanced X again, but defense-in-depth catches future slips at the boundary instead of letting them silently degrade the takeoff.
**Date:** April 2026

### `connection_size` Field Removed End-to-End
**Decision:** The `connection_size` field has been stripped from the extraction prompt JSON schema, the aggregation Lambda's row-build code (both Material and Flagged tab paths), the Lambda's column mapping list, and the Labor tab's `explicitColumns`. The Summary tab's "CONNECTIONS BY SIZE" section now groups by `Size` (the per-row connection size) instead of `Connection Size` (which carried the parent fitting's full dual-size string for reducers).
**Why:** `connection_size` was a phantom field — the model emitted it, the Lambda passed it through, the Material and Labor tabs displayed it, but no C# code computed labor or makeup from it. The single remaining consumer was the "CONNECTIONS BY SIZE" Summary grouping, which was arguably buggy for reducers: a SCRD row at the run side (Size=2) and a SCRD row at the outlet side (Size=0.5) of a `2x0.5` REDT both grouped into bucket "2x0.5" because they shared the parent's `Connection Size`, instead of into the correct buckets "2" and "0.5". Switching the grouping to `Size` fixes that bucketing for free. Stripping the field saves prompt tokens, JSON bytes, and one more column the user has to scroll past on the Material tab. The Olet Rule and the connection_size-related portions of the SIZE VALIDITY CONTRACT were rewritten in the proposed new prompt; the historical `extraction_prompt.txt` (the version that produced the 2026-04-23 sample takeoff under investigation) was deliberately left untouched as evidence.
**Date:** April 2026

### Flagged Tab Kept Minimal; Material Tab Is the Review Target
**Decision:** The Flagged tab is a minimal "rows to look at" view. It includes 13 columns (drawing number, item ID, size, description, component, connection qty/type, material group + desc, thickness, class rating, confidence, flag reason). It does NOT mirror Material — `connection_size`, `quantity`, `commodity_code`, `length`, `shop_field`, and all `tb_*` title-block columns are intentionally omitted. The four override columns (`Override Component`, `Override Conn Qty`, `Override Conn Type`, `Override Notes`) that used to sit on the right edge with yellow highlighting were deleted. Reviewers match by `(Drawing Number, Item ID)` back to the Material tab to see full context or to apply a correction.
**Why:** The Flagged tab's purpose is to surface the rows that need human review — it's a worklist, not an editor. The override columns were decorative only (full repo grep found zero C# consumers; if a reviewer filled them in, nothing happened). Wiring them up would duplicate editing functionality that already exists on the Material tab — users correct values there directly, the Material tab is the single source of truth for BOM data, and any "Recalc Excel" pass regenerates the Labor and Summary tabs from whatever is on Material. Keeping Flagged minimal prevents drift between the two tabs and communicates clearly which tab is authoritative.
**Date:** April 2026

### Takeoff Lifecycle Lives in App-Level `TakeoffSession`, Not the View
**Decision:** The upload → start → poll lifecycle of an AI Takeoff batch is owned by `Services/AI/TakeoffSession.cs` held at `App.CurrentTakeoff` (mirroring the `App.CurrentUser` pattern), not by `TakeoffView.xaml.cs`. The view becomes a thin subscriber that rebuilds itself from session state on `Loaded` and unsubscribes on `Unloaded`. The session also owns its own `TakeoffService`, `CancellationTokenSource`, and any future timer.
**Why:** `MainWindow.BtnTakeoff_Click` destroys and recreates the `TakeoffView` instance on every navigation. Before this lift, switching to Schedule mid-batch orphaned the view but left its async state machine running invisibly — the polling loop kept writing to a `txtStatus` the user could no longer see, and a deferred `SaveFileDialog` could pop from a detached view. Hoisting state above the view lifecycle lets the user navigate freely while a takeoff is in flight; the next view instance restores the in-progress UI and picks up event subscriptions where the previous one left off.
**Date:** April 2026

### Sticky "Takeoff: Complete" Bottom-Bar Indicator; Cancellation Does Not Set It
**Decision:** `App.HasCompletedTakeoffSinceStartup` is set the first time a takeoff session raises `Completed` with `CompletedSuccessfully == true`. It stays set until the next batch starts (flips to Running) or the app closes. Cancelled or failed batches do not set the flag — they leave the bottom bar at "Not Running."
**Why:** The bottom-bar indicator is a quiet acknowledgment that a takeoff actually happened in this app session. After the user has saved the Excel and moved on to other work, glancing at the bar should confirm "yes, you ran a batch." A cancelled batch is not a completion in any meaningful sense; reusing the Complete state for it would dilute the signal and could mask user mistakes (cancel-by-accident on a long batch, then walk away thinking it succeeded). Per-app-session lifetime (not persisted to disk) keeps the indicator scoped to "what you did right now."
**Date:** April 2026

### Auto-Open SaveFileDialog on Return; Cancelled Dialog Does Not Reopen
**Decision:** When a takeoff completes successfully while the user is on another tab, `TakeoffSession.PendingDownloadBatchId` is set. On returning to the Takeoffs tab, `RestoreFromSessionIfActive` calls `ClearPendingDownload()` first and then opens the SaveFileDialog. Whether the user saves or cancels, the flag is already cleared — a subsequent nav-and-return will not re-pop the dialog. Recovery from a cancelled save is via the Previous Batches button.
**Why:** The user explicitly asked for a frictionless on-return experience: dialog opens directly, no inline confirm. Re-popping the dialog on every navigation would punish the user for cancelling once. Clearing before the await also defends against a SaveFileDialog crash leaving the flag set indefinitely. Previous Batches is already the documented recovery path for downloading a completed batch you didn't save the first time, so leaning on it for the cancel-on-return case avoids a parallel flow.
**Date:** April 2026

### Persist Last Config by Key, Not Index; Don't Persist Rates / Bubble / Send-Missed
**Decision:** `Takeoff.LastConfigKey` UserSetting persists the last-selected config across tab navigations and app sessions. Lookup is by `_configs[i].Key` so dropdown order changes don't break the restore; if the saved key no longer exists (config deleted), fall back to the first available config. The Unit Rates dropdown, Rev Bubble Items Only checkbox, and Send Missed Makeups to Admin checkbox are intentionally NOT persisted — they always default to "Default (Embedded)" / unchecked / unchecked.
**Why:** Config selection is high-friction to re-pick from a dropdown each session and almost always the same one. Index-based persistence breaks the moment a user adds, deletes, or renames a config; key-based persistence survives. The other three controls are deliberately friction by design — Unit Rates and the two checkboxes change the meaning of the resulting Excel (project-specific rates, scope of extraction, who gets emailed), and the user wants to make a fresh decision each session rather than have a stale opt-in survive a restart.
**Date:** April 2026

---

## VP vs Vtg Report

### JC Cost Code: Exact Match After Outer-Whitespace Trim Only
**Decision:** ProjectID and PhaseCode matching between the JC Labor Productivity report and Vantage `VMS_Activities` uses `String.Trim()` on both sides (stripping only leading and trailing whitespace), then compares as an exact string. No leading-zero stripping, no internal whitespace collapsing, no trailing-separator trimming. The previous `NormalizeKey` helper was removed.
**Why:** Reversed an earlier decision that normalized both sides to match cosmetic variants like `26.001.001` ↔ `26.1.1`. The normalization was hiding a real data-quality problem: Vantage phase codes should match VP's canonical format exactly. "Not Found" is now a useful signal — it tells the user which Vantage records have drift from VP and need their codes corrected to match VP. Outer-whitespace trim is the only concession, because Excel cell formatting and SQL CHAR padding can introduce leading/trailing spaces that aren't user-meaningful differences; everything else (internal spaces, digits, punctuation, zero-padding) must match character-for-character or the row is reported as `Not Found`.
**Date:** April 2026 (reversed same month it was introduced)

### Color Coding Scoped to Added Columns Only
**Decision:** Only the two generated columns (`Vtg Budget`, `Vtg Earned`) receive conditional fill (green within 1%, red over 1%, orange `Not Found`). The companion Excel columns (`Est Hours`, `JTD ERN`) are left untouched.
**Why:** Initial implementation paired the red fill across both cells of a mismatch. User preferred minimizing modifications to the source report and keeping the visual signal scoped to the Vantage-sourced values. Every data row gets a color on the two new columns so mismatches are never ambiguous with "not yet checked".
**Date:** April 2026

### Prep Dialog + Native File Picker, Not a Custom Combined Picker
**Decision:** Before the `OpenFileDialog`, show a custom WPF verification dialog (`VPvsVtgPrepDialog`) with instructions and an annotated screenshot. On OK, the standard Windows file picker opens. Two separate dialogs, not one custom combined picker.
**Why:** Considered building a custom file browser to bundle the prep instructions into one screen. Rejected — the native `OpenFileDialog` is a Windows OS control users already understand, and replicating it would be significant work with no payoff. The only thing the OS picker can't do is show an image or rich text; a small pre-dialog is the right tool for that single gap.
**Date:** April 2026

### Prep Dialog Skip Flag Persists to UserSettings, Not AppSettings
**Decision:** The "Do not show this dialog again" checkbox writes `SkipVPvsVtgPrepDialog=true` to the `UserSettings` table (per-user), not `AppSettings` (app-wide).
**Why:** The prep step is a user-learning concern — once a user has read the instructions, they don't need to see them again. Other users on the same machine haven't necessarily learned yet. User-scoped persistence mirrors how other dismissible UI state is stored (grid layouts, filter selections, analysis group field, etc.).
**Date:** April 2026

### Trust the Admin on Snapshot Re-Upload (Not Implemented, Decision Logged)
**Decision:** Deferred adding a WeekEndDate override to the Admin Snapshots upload dialog. If users want to re-upload an older unchanged snapshot under a new week, they instead take a fresh snapshot.
**Why:** Considered adding an override so admins could reuse old snapshots for weeks where nothing changed, avoiding re-snapshotting closed-out activities. User ultimately judged the complexity not worth the bug surface for the small workflow gain, and preferred to keep the current simple invariant (WeekEndDate = when the snapshot was taken). Filed here so the conversation doesn't get re-litigated if the idea comes up again.
**Date:** April 2026

---

## Schedule Module

### P6 Current Schedule Dates, Not Baseline Dates
**Decision:** P6 import maps `start_date`/`end_date` (current schedule) instead of `target_start_date`/`target_end_date` (baseline).
**Why:** 3WLA requirement logic and missed start/finish reasons need current schedule dates, not stale baselines.
**Date:** February 2026

### 3WLA Dates Stored in Activities Table, Not Separate Table
**Decision:** Simplified from separate `ThreeWeekLookahead` table to `Activities.PlanStart/PlanFin` columns.
**Why:** Pre-populated from MIN/MAX of plan dates per SchedActNO. Persists across P6 imports. Eliminates separate table management.
**Date:** February 2026

### MissedReasons Are Session-Only, Not Persisted
**Decision:** MissedReasons stored in Schedule table, cleared on P6 import.
**Why:** Only required for P6 dates within the current week. Persisting required complex stale-detection logic for when underlying dates changed.
**Date:** February 2026

### Local SQLite Mirror for Snapshot Data
**Decision:** Schedule module reads from a local 12-column mirror instead of Azure's 89-column table. Azure stays authoritative; edits write through to both.
**Why:** Eliminated long lag on master/detail grid interactions. Local mirror self-heals on P6 import. Trimmed to 12 columns because that's all the Schedule module reads.
**Date:** April 2026

### MS Not In P6 Report: Per-User Only
**Decision:** After conversion to local mirror, report shows only current user's data (was previously all users).
**Why:** Matches the rest of the Schedule module's per-user filtering and is more intuitive for a per-user export.
**Date:** April 2026

### Snapshot Retention: 21 Days
**Decision:** Submit-time purge uses 21-day retention, not 28.
**Why:** Reduced data volume while still covering 3 full weekly cycles.
**Date:** April 2026

### Schedule Module: Dynamic Per-Cell Save, No SAVE Button
**Decision:** Removed the explicit SAVE button from the Schedule module. Every master-grid cell commit (Missed Reasons, lookahead Start/Finish, and cell-clear via Delete/Backspace) now saves immediately via a new `ScheduleRepository.SaveScheduleRowAsync(row, username)` — per-row equivalent of the old `SaveAllScheduleRowsAsync`, wrapping the single Schedule-row update plus the PlanStart/PlanFin bounds update scoped to that one SchedActNO. The `NotifyActivitiesModifiedAsync` callback (which reloads the Progress grid) is debounced with a 1-second trailing `DispatcherTimer` so rapid editing doesn't hammer the 100k+ row reload path. `HasUnsavedChanges`, the exit prompt, the "save first" export gate, and `SaveAllScheduleRowsAsync` are all deleted. Detail-grid edits were already auto-saving — the refactor brings the master grid up to that same pattern.
**Why:** The SAVE button introduced a bug class: clicking Refresh while edits were pending silently discarded them. Eliminating the unsaved state eliminates that bug by design and matches the Progress module's long-standing pattern.
**Date:** April 2026

### Lookahead Window Is User-Configurable (3 / 6 / 9 Weeks)
**Decision:** The Schedule lookahead window is selectable per-user via a ComboBox in the Schedule toolbar (3WLA / 6WLA / 9WLA; default 3WLA). Stored in UserSettings as `Schedule.LookaheadWeeks`. A single static `ScheduleMasterRow.LookaheadDays` is the source of truth for the 21-/42-/63-day threshold consumed by `IsThreeWeekStartRequired` / `IsThreeWeekFinishRequired`. Property and DB column names like `ThreeWeekStart` / `ThreeWeekFinish` are retained — the "3" is historical, not structural. Excel import/export file formats are unchanged; only in-app UI strings, the `AddDays(…)` highlighting threshold, and the Schedule Reports worksheet name + filename + dialog title adapt.
**Why:** Different trades and project phases need different forecast horizons. Hardcoding 21 days forced everyone into the same window. Per-user persistence lets each user set their own preference without affecting teammates.
**Date:** April 2026

### Schedule Reports Export: AssignedTo as Last Column, Same Value on Every Row, Reports File Only
**Decision:** The Schedule Reports export (3WLA / 6WLA / 9WLA workbook produced by `ScheduleReportExporter`) gained a 23rd `AssignedTo` column at the far right, populated with `App.CurrentUser!.Username` of the user who ran the export. The same username is written on every row across all three sections (master rows, P6-not-in-MS rows, MS-not-in-P6 rows). The header gets the grey `#D9D9D9` band that the Identity/Flags group (cols 1-3) uses, deliberately distinct from the yellow `#FFEB9C` 3WLA/Planning band (cols 16-22), to read visually as file-origin metadata rather than another planning field. The P6 export (the file headed back into Primavera) was deliberately NOT touched — only the Reports file. The `?? "Unknown"` defensive fallback was dropped because login is a hard gate at app startup (unrecognized users never reach the export menu); `App.CurrentUser!` is guaranteed non-null at the call site.
**Why:** Schedulers receive Schedule Reports exports from multiple users for the same week and lose track of which file came from whom — especially after the file is renamed, merged, or sorted. Originally proposed filename stamp as the safer option (zero risk to file format), but schedulers explicitly asked for an in-file column because filenames get renamed during their workflow and they wanted the origin marker to survive sort, filter, paste, and merge operations. Same-value-on-every-row (instead of value-on-first-row-only or a metadata header row) was chosen for exactly this durability reason: any single row carries the origin marker even after the file is sliced. Schedulers also explicitly scoped this to the Reports file only — the P6 export stays clean because Primavera's column schema is strict (the recent `status_code` "Complete" → "Completed" fix is fresh evidence of how unforgiving P6 import can be), and an unknown column risks import warnings or rejection. Grey-not-yellow because the AssignedTo column is metadata about the file (origin), not metadata about an activity (planning data) — visual grouping should match semantic role.
**Date:** April 2026

---

## AI / Progress Scan

### Textract over Claude Vision for OCR
**Decision:** Switched from Claude Vision API to AWS Textract for table extraction.
**Why:** Textract provides proper table structure with row/column indices, yielding 100% accuracy. Claude Vision had inconsistent accuracy between PDF and JPEG. Tool Use (function calling) was tried first but Textract's native table detection was superior.
**Date:** January 2026

### ActivityID over UniqueID for OCR Identifier
**Decision:** Progress Book uses ActivityID (integer) instead of UniqueID (long string) as the record identifier.
**Why:** Shorter values are more reliable for OCR from scanned handwritten pages.
**Date:** January 2026

### Eliminated Checkboxes, "Write 100" Means Done
**Decision:** Removed Done checkbox concept entirely. Writing "100" in the % ENTRY box means done.
**Why:** Simplified the scan form and improved AI accuracy by reducing distinct columns to parse. Color-coded entry fields were also removed — AI relies on text labels, not colors.
**Date:** January 2026

### Sidebar AI Assistant Tab Shelved
**Decision:** Removed the two-tab (Help / AI Assistant) layout from the Help sidebar and deleted the AI tab scaffolding (button, placeholder content grid, tab-switching code in view and view-model). Kept `Plans/Sidebar_AI_Assistant_Plan.md` in the repo.
**Why:** The AI chat feature was never wired past a "Coming soon" placeholder and had no roadmap date. Carrying the empty tab in the UI misled users into thinking the feature was imminent, and the tab-switching code was dead weight. Shelving is preferred over leaving stubs; plan doc retained in case the feature is revived later.
**Date:** April 2026

---

## Edit Rules & Bulk Operations

### Hard Rules vs. Required Metadata
**Decision:** Activity date/percent rules are split into two tiers. Hard rules (future dates, ActFin before ActStart, ActStart set with %=0, ActFin set with %&lt;100) block the edit and revert. Required metadata (ActStart needed when %&gt;0, ActFin needed when %=100) does NOT block — the cell is flagged red and sync is gated.
**Why:** Treating "required metadata" as a hard block creates deadlocks when raising a record's progress before dates are known, or when bulk-setting % to 100 on records that still need ActFin. Users expressed frustration with being unable to move records forward. Red highlighting + sync gate still enforces data completeness, without blocking the in-progress edit.
**Date:** April 2026

### Single Validator as Source of Truth (`ActivityValidator.Validate`)
**Decision:** All edit paths — single-cell CurrentCellEndEdit, Find &amp; Replace, and both multi-row paste flows — call `ActivityValidator.Validate(percent, actStart, actFin)` to check the prospective state. The function returns the first violation message or null.
**Why:** Previously the same rules were re-implemented in three places and drifted. Centralising prevents new bulk paths from forgetting a rule.
**Date:** April 2026

### Bulk Operations Abort on Any Violation, Not Partial Apply
**Decision:** When Find &amp; Replace or a multi-row paste encounters a hard-rule violation on <em>any</em> affected row, the entire operation is rolled back in memory, nothing is written to the DB, and a dialog lists up to 10 offending ActivityIDs plus a "…and N more" footer.
**Why:** Silent partial apply (paste some rows, skip others) was reported as confusing — users couldn't tell what actually changed. Abort-on-any forces the user to correct the source data and re-run, which is more predictable than picking which rows to commit.
**Date:** April 2026

### Entering ActFin Auto-Bumps % Complete to 100
**Decision:** Single-cell edit, Find &amp; Replace, and paste all auto-set `PercentEntry = 100` on any row where a non-null Finish date was entered and current % is below 100.
**Why:** Users always have to follow a Finish-date entry with a %=100 update (ActFin requires %=100). The auto-bump saves a step. If ActStart is null when ActFin is entered, the record is still written — ActStart turns red as required metadata, consistent with the hard-rule / required-metadata split.
**Date:** April 2026

### Filter Does Not Auto-Refresh After Edits
**Decision:** Removed every `View.Refresh()` call that fired after a data mutation (paste success and rollback, Find &amp; Replace success, Prorate, single-cell rollback, Undo/Redo, ClearCurrentCell). Filters only re-evaluate when the user clicks a filter toggle or the Refresh button. Grid set to `LiveDataUpdateMode="Default"` so Syncfusion also doesn't re-shape data on property changes.
**Why:** Users reported frustration at rows silently disappearing from a filtered view the moment their edit made the row no longer match (e.g., raising % to 100 while viewing "In Progress"). They want to see the value they just changed before deciding to re-apply the filter. `INotifyPropertyChanged` on `Activity` still propagates value changes to grid cells; only the filter predicate stays stale until an explicit refresh.
**Date:** April 2026

### Single Source of Truth for 9 Required-Metadata Field Names (`ActivityRequiredMetadata`)
**Decision:** The 9 required-metadata field names (`ProjectID`, `WorkPackage`, `PhaseCode`, `CompType`, `PhaseCategory`, `SchedActNO`, `Description`, `ROCStep`, `RespParty`) live in one place: `ActivityRequiredMetadata.Fields` in `Utilities/ActivityValidator.cs`. Three helpers are exposed — `Fields` (the array, ProjectID-first), `FieldsDisplay` (comma-joined for user-facing messages), and `BuildMissingFieldSql(tableAlias)` (generates `"X IS NULL OR X = '' OR Y IS NULL OR Y = '' ..."` for either unqualified or aliased columns). Six call sites consume the canonical list: the sync-gate filter SQL, `CalculateMetadataErrorCount`, the new `CountMetadataErrorsForProject`, the sync-block MessageBox, the reassign-check in-memory filter (reflection-based, skipping ProjectID so `HasInvalidProjectID` covers that slot exactly as before), the reassign-block MessageBox, and `ImportTakeoffDialog.RequiredMetadataFields`. Conditional rules (ActStart required at % > 0, ActFin required at % = 100) and the ProjectID existence check remain hand-coded alongside the generated fragments — they are not part of the simple 9-field list.
**Why:** Before this refactor, the list was duplicated across 5 locations — two SQL filter strings, two MessageBox display strings, one C# in-memory `string.IsNullOrWhiteSpace` chain, and one array in the Import Takeoff dialog — plus an additional ordering variant (ProjectID-first in the dialog vs. ProjectID-in-position-5 in SQL/messages). Adding or removing a required field would have required finding and editing every copy in lockstep; silent drift was a matter of time. One centralized array with a SQL generator makes drift impossible and keeps the wording of user-facing messages consistent with the actual enforced list. The ProjectID-first canonical order was chosen to preserve the interactive Import Takeoff dialog row order (the only interactive UI touching this list); the two MessageBox strings now display ProjectID first instead of in position 5 — purely cosmetic, no runtime impact (SQL `OR` is commutative). `ActivityValidator.cs` was explicitly chosen as the home because its existing file comment already pointed at "required-metadata highlighting / sync gate" as the owner of these rules.
**Date:** 2026-04-24

### Submit Week Gated by Per-Project Metadata Error Count, Not Per-User
**Decision:** `BtnSubmit_Click` in `ProgressView.xaml.cs` runs a new `CountMetadataErrorsForProject(selectedProject)` check between Step 3 (project selection) and Step 4 (week-end date picker). If the selected project has any records with missing required fields (9-field list) or conditional rule violations (ProjectID-exists, ActStart-when-%>0, ActFin-when-%=100), the submit is blocked with a MessageBox reusing `ActivityRequiredMetadata.FieldsDisplay`. The helper runs the same SQL as `CalculateMetadataErrorCount` but with an added `AND a.ProjectID = @projectId` clause and does NOT mutate the viewmodel's `MetadataErrorCount` counter.
**Why:** The existing sync gate uses `CalculateMetadataErrorCount` which counts errors across all the user's records (all projects), because sync pushes every dirty record the user has. Submit Week, by contrast, is always scoped to one project (user picks it at Step 3). Using the sync-wide count would wrongly block submitting project A when project B has unrelated errors. Two scoping options were considered: (1) per-user (matches sync, simpler) vs. (2) per-project (matches submit semantics). Per-project chosen because it matches the user's mental model — "fix the project I'm submitting" — and avoids cross-project surprises. Insertion point (after project selection, before date picker) chosen so single-project users still hit the check immediately, and multi-project users aren't forced through a date picker only to then learn they have errors. The project-scoped helper is intentionally separate from `CalculateMetadataErrorCount` so Submit Week's check doesn't pollute the viewmodel counter that the footer "Metadata Errors: X" button displays (that counter continues to reflect the user-wide total).
**Date:** 2026-04-24

---

### Paste Into ActStart/ActFin Validates Instead of Silently Skipping Rows
**Decision:** Removed the pre-filter that skipped rows failing the date-percent rules during multi-cell paste (both "single value to multiple rows" and "multi-value paste" flows). Paste is now abort-all-on-any-violation with a detailed error dialog.
**Why:** The silent-skip behavior (with a post-paste "N rows skipped" message) let users think a paste succeeded broadly when significant portions were dropped. Consistent abort-all semantics match Find &amp; Replace and single-cell edits.
**Date:** April 2026

---

## UI / UX

### ProgressView Cached for Instant Navigation
**Decision:** Cache the ProgressView instance in MainWindow and reuse on subsequent navigations.
**Why:** First load unchanged, but every subsequent navigation is instant. Force-reloads only on Excel import and Reset Grid Layouts.
**Date:** February 2026

### DIY Summary Panel Instead of Syncfusion TableSummaryRow
**Decision:** Replaced Syncfusion's `TableSummaryRow` with custom toolbar summary panel.
**Why:** TableSummaryRow was too slow on large datasets. DIY panel uses cached `PropertyInfo` lookups and 200ms debounce.
**Date:** February 2026

### DynamicResource + Role-Based Theme Token Names
**Decision:** Converted ~1,119 StaticResource refs to DynamicResource. Renamed resources to role-based names (e.g., `ToolbarForeground`, `GridHeaderForeground`).
**Why:** Enables live theme switching without app restart. Role-based names support future themes beyond Dark/Light.
**Date:** February 2026

### Custom Grid Filter Icons
**Decision:** Replaced Syncfusion's built-in FilterToggleButton with custom stroke-based funnel icons.
**Why:** Syncfusion's internal filter icon colors are resolved from compiled BAML and cannot be overridden via resource dictionaries. Custom template was the only option for theme-aware icons.
**Date:** February 2026

### Double-Click to Sort Grid Columns
**Decision:** All grids require double-click on column headers to sort.
**Why:** Prevents accidental sorting when clicking headers — users were inadvertently resorting data.
**Date:** February 2026

### Column Settings Graceful Schema Migration
**Decision:** Column preferences apply to matching columns when schema changes, rather than being fully rejected on hash mismatch.
**Why:** Previously, adding or removing any column discarded all user column preferences. Now new columns appear at end with defaults, removed columns are ignored.
**Date:** February 2026

### PercentEntry: Custom GridTemplateColumn with Progress Bar
**Decision:** Uses a custom `GridTemplateColumn` with progress-bar overlay instead of native `GridNumericColumn`.
**Why:** Enables the thin colored progress bar in each cell. Trade-off: decimal handling, arrow key navigation, and auto-edit-on-type all had to be hand-coded.

### PercentEntry Edit: LostFocus Trigger, Not PropertyChanged
**Decision:** EditTemplate TextBox binding uses `UpdateSourceTrigger=LostFocus` instead of `PropertyChanged`.
**Why:** PropertyChanged triggered the setter on every keystroke, running clamp/round/multi-PropertyChanged chains. This raced against input, causing `0.5` to become `5`. LostFocus commits only when editing ends.
**Date:** April 2026

### Notification Sounds Removed from Informational Dialogs
**Decision:** Changed ~90 `MessageBoxImage.Information` instances to `MessageBoxImage.None`.
**Why:** Windows notification sounds were disruptive and unnecessary for informational confirmations.
**Date:** February 2026

### Clone Buttons Removed, Save-As Pattern Instead
**Decision:** Removed Clone from WP Templates, Form Templates, and Prog Books layouts. To copy, change the name and save.
**Why:** Simplified template management. Clone required a naming dialog. Save-with-new-name is simpler and matches common patterns.
**Date:** February 2026

### Custom Scrollbar Templates over Syncfusion Defaults
**Decision:** Always-visible 14px custom scrollbar templates with theme-aware colors, replacing auto-hiding scrollbars.
**Why:** Auto-hiding scrollbars were hard to find and interact with. Custom `ScrollViewer` template avoids implicit style leaking into ComboBoxAdv dropdown internals.
**Date:** March-April 2026

### SfSkinManager.SetTheme in Constructor, Not Loaded Event
**Decision:** Theme application must happen in the constructor, before the control is in the visual tree.
**Why:** When applied in Loaded on a second instance, Syncfusion's theme engine interfered with SfDataGrid rendering, causing the grid not to display data.
**Date:** April 2026

### Progress Row Actions: Sidebar Dropdown, Not Grid Right-Click
**Decision:** Row-action commands for the Progress grid (Select All, Delete, Copy [submenu], Duplicate, Add Blank, Export Selected) live under an "Actions" button in the left filter sidebar, not on the grid's right-click context menu. The grid's `RecordContextMenu` was removed entirely. The column-header `HeaderContextMenu` (Find & Replace, Copy Column, Freeze) is unaffected.
**Why:** Syncfusion `SfDataGrid`'s default right-click behavior clears the existing multi-row selection and reduces it to the single row under the cursor (unless Shift is held). Users routinely selected 50+ rows, right-clicked Delete, and got only one row deleted. Sidebar buttons don't touch grid selection at all, so multi-row operations behave the way users expect by construction. Trade-off: standard right-click doesn't open a row menu anymore, requiring a brief retraining; the discoverability cost is small because the Actions button is always visible and labeled.
**Date:** April 2026

### Menu Item Icons Inline in Header, Not `<MenuItem.Icon>`
**Decision:** Menu items that need icons (Actions menu items in the Progress sidebar) render the icon as a TextBlock inside the `MenuItem.Header` StackPanel, with a fixed 22px-wide icon column so labels align. The `<MenuItem.Icon>` slot is not used.
**Why:** WPF MenuItem's default template renders an "icon column gutter" rail above and below items even when the visual is overridden via a custom `ItemContainerStyle` template. The chrome leaked through every workaround attempted: keyed `MenuItem.SeparatorStyleKey` overrides, custom `ControlTemplate` with explicit Grid columns, etc. Switching to inline-icon-in-Header bypasses the gutter rendering path entirely — the template only sees a Header content presenter and a submenu arrow, no icon-related chrome to suppress. Same approach is what the existing toolbar `DropDownMenuItem` controls effectively do internally.
**Date:** April 2026

### Hover-Out Auto-Close: Custom Polling, Two Distinct Hit-Test Strategies
**Decision:** All app dropdowns (Progress sidebar Actions and USER, MainWindow toolbar File/Tools/Admin) auto-close when the cursor leaves them. WPF doesn't support this natively for top-level menus (only submenus), so `Utilities/MenuAutoClose.cs` implements it via a 150ms polling DispatcherTimer with two timing constants: `InitialOpenGraceMs = 1500` (long delay before close if cursor never enters the menu) and `CursorLeftDelayMs = 400` (faster close once cursor has been in and then left). A `hasBeenOver` flag chooses which delay applies.
**Why:** Different control types need different hit-test approaches. ContextMenu (Actions, USER) is a popup-hosted single-rect element — `Mouse.GetPosition(menu)` against `menu.ActualWidth/ActualHeight` is reliable. DropDownButtonAdv (File, Tools, Admin) is harder: dropdown popup lives in a separate visual tree from the button, so `button.IsMouseOver` doesn't propagate from items, walking up from `Mouse.DirectlyOver` doesn't reliably find the button or the popup, and the logical-tree walk catches a stuck `IsMouseOver=true` flag on the button while the dropdown is open. Final approach for DropDownButtonAdv: `Mouse.GetPosition(button)` plus a generous-rect bounds check covering the button rect AND a 240×600 area below it where the dropdown is rendered. Toolbar dropdowns always open downward, so the bounds-below approach is reliable without trying to enumerate Syncfusion internals. Open submenus (Copy Row(s)) suppress the close countdown so navigating into a submenu doesn't kill the parent.
**Date:** April 2026

### Menu Item Stays Open on Click: Dispatcher.BeginInvoke Reopen, Not Preview-Event Trick
**Decision:** Select All in the Actions menu uses a regular `Click` handler that performs the work, then schedules `ContextMenu.IsOpen = true` via `Dispatcher.BeginInvoke` at `Background` priority to reopen the menu after WPF's `MenuItem.OnClick` closes it. Brief flicker is the cost.
**Why:** WPF `MenuItem.OnClick` closes the parent menu unconditionally for non-`SubmenuHeader` role items, and there is no clean way to suppress it from XAML or a derived class without subclassing `MenuItem`. Tried `IsCheckable="True"` (toggles `IsChecked` but doesn't suppress the close in a `ContextMenu` context); tried `PreviewMouseLeftButtonUp` with `Handled=true` (ignored because `ButtonBase.OnMouseLeftButtonUp` is registered with `HandledEventsToo=true`). Dispatcher reopen is the canonical workaround across the WPF community.
**Date:** April 2026

### `AppMessageBox.Show` Wrapper Mandated for All User-Facing Dialogs
**Decision:** Created `Utilities/AppMessageBox.cs` static helper that wraps `MessageBox.Show` and added a CLAUDE.md "User-Facing Dialogs" rule requiring its use everywhere instead of `MessageBox.Show` directly. Migrated all 452 production call sites across 41 files (`VANTAGE.Installer/` excluded — separate project that doesn't reference VANTAGE.Utilities).
**Why:** After long-running awaits or focus loss, a parameterless `MessageBox.Show` can render behind the owning window — a 5,783-record restore appeared to hang because the success dialog was hidden under the Progress grid. The wrapper finds the active window, calls `Activate()`, and toggles `Topmost` true→false to force the z-order before parenting the dialog. Considered `MessageBoxOptions.DefaultDesktopOnly` (loses owner-modality, can render on the wrong desktop in multi-monitor setups) and per-callsite `this.Activate()` calls (480 sites, easy to forget for new code) — a centralized wrapper with mechanical migration is the maintainable answer.
**Date:** April 2026

### Bulk Restore/Purge: Temp Table + SqlBulkCopy + INNER JOIN, Not `WHERE IN (large param list)`
**Decision:** `BtnRestore_Click` and `BtnPurge_Click` in `Views/DeletedRecordsView.xaml.cs` create a session-scoped `#RestoreBatch` / `#PurgeBatch` temp table, bulk-copy the UniqueIDs in via `SqlBulkCopy`, and run a single `UPDATE ... INNER JOIN` (or `DELETE ... INNER JOIN`) — same pattern as `Utilities/SyncManager.cs`. No more chunked loops, no transaction wrapping multiple statements.
**Why:** First attempt used `WHERE UniqueID IN (@uid0, @uid1, ..., @uid999)` chunked into 1000-ID batches under a single transaction. This (a) hit SQL Server's 2100-parameter ceiling on the original unchunked version, (b) produced terrible query plans even after chunking — the optimizer expands large IN lists into OR-trees, parameter sniffing varies per batch, and plan reuse is unreliable, and (c) held write locks on every restored row across all batches in one transaction, blocking any concurrent user sync. SqlBulkCopy + INNER JOIN is one round-trip, gets a clean index-seek plan, and the implicit per-statement transaction is small. A 5,783-record restore that previously took several minutes now runs in seconds.
**Date:** April 2026

### Loading Overlay for Slow Bulk Operations: Fullscreen DualRing, Not Inline Bar
**Decision:** Slow bulk operations on the Progress grid (Select All, Delete after confirmation) use the fullscreen `LoadingOverlay` Grid with `SfBusyIndicator` (`AnimationType="DualRing"`), not the inline `SfLinearProgressBar` bound to `viewModel.IsLoading`. The inline bar is reserved for background data loads where the operation isn't user-initiated.
**Why:** Both the DualRing animation and the linear progress bar's animation tick on the UI thread (Syncfusion's animations are dispatcher-driven, not composition-thread). For long-running synchronous operations like populating `SelectedItems` with 100k+ rows, both animations freeze unless the UI thread gets regular yield windows. The DualRing is more visually prominent and signals "wait, work is happening" more clearly than a thin bar at the bottom of the grid that's easy to miss. Chunked work with `Task.Delay(1)` between chunks of 100 keeps either animation moving, but the overlay's larger visual real estate is the better fit for user-initiated bulk actions. Pre-confirmation overlays for fast operations (e.g., Delete's ownership pre-check on a small selection) were removed because they flickered before the confirmation dialog and added no signal.
**Date:** April 2026

---

## Architecture

### Auto-Update: Host-Agnostic Manifest-Based System
**Decision:** Custom auto-updater checking `manifest.json` with SHA-256 verified ZIP downloads and a separate Updater console app.
**Why:** Works with GitHub raw URLs now, can switch to Azure Blob by changing one URL. Graceful failure if offline. Self-contained publish means users don't need .NET runtime.
**Date:** January 2026

### Credentials: Encrypted Config over Compiled Constants
**Decision:** Replaced `Credentials.cs` with `CredentialService.cs` reading `appsettings.json` (dev) or AES-256 encrypted `appsettings.enc` (production).
**Why:** Published builds carry credentials without embedding them in source code. Publish script handles encryption automatically.
**Date:** February 2026

### Schema Migrations: Formal Versioned System
**Decision:** `SchemaMigrator.cs` with numbered sequential migrations, replacing ad-hoc column checks.
**Why:** Local DB contains user data that can't be deleted. Migrations must be idempotent and backward-compatible. Failed migrations offer to delete local DB and re-sync. Formal versioning prevents missed or double-applied changes.
**Date:** February 2026

### Plugin Architecture: Dynamic Menu Injection
**Decision:** Plugins inject their own Tools menu items via `host.AddToolsMenuItem()`. Each plugin has its own assembly loaded at startup with `IVantagePlugin` interface.
**Why:** Plugins create their own UI dynamically. Replaced the static `ProjectSpecificFunctionsDialog`. Auto-update checks installed plugins against a feed index on startup.
**Date:** March 2026

### Import Format Auto-Detection by Column Headers
**Decision:** Single import detects Legacy vs NewVantage format by column headers (`UDFNineteen` = Legacy, `UniqueID` = NewVantage).
**Why:** Previous threshold-based percent detection (1.5 threshold to guess 0-1 vs 0-100 format) caused edge cases. Column headers are definitive.
**Date:** January 2026

### Drawings Module Deferred to Post-V1
**Decision:** Disabled with `Visibility="Collapsed"` and code filters. Re-enable instructions documented in Project_Status.md.
**Why:** Per-WP drawing location architecture needs design (token paths, per-WP config, or Drawings Manager). The feature works but the architecture isn't settled.

### Asset Folder Structure: `Assets/Images/{System, Sidebar, Dialogs}`
**Decision:** All image resources live under a single top-level `Assets/Images/` tree with purpose-named subfolders — `System/` (app icons, logos, cover art), `Sidebar/` (help sidebar screenshots), `Dialogs/` (one-off dialog imagery). Applies to source tree AND build output — no `Link` indirection. Previously split across `Images/` and `Help/*.png`.
**Why:** One discoverable home for every image file, with semantic grouping that tells a developer where a new image should land without having to ask. `Help/` retains only `manual.html`, not the 30 screenshots it consumes. Flat sibling folders under `Assets/` (without the `Images/` parent) were rejected because future one-off dialog images wouldn't have an obvious home and the top level would grow noisily. An earlier iteration used MSBuild's `Link` attribute to keep output at `Help/*.png` while source lived at `Assets/Images/Sidebar/` — reversed because it defeated the point of organizing: the user saw images "back in the Help folder" in the build output.
**Date:** April 2026

### Reset User Settings: Registry-Based Whitelist, Not Nuclear Clear
**Decision:** A "Reset User Settings" dialog in the Settings popup lets users selectively clear groups of preferences from the `UserSettings` table. Exposed groups and their member keys are declared in `Utilities/UserSettingsRegistry.cs`; anything not in the registry is excluded by policy. Rule: if a setting already has a manager UI where the user can modify/add/delete it (Theme submenu, Grid Layouts dialog, Manage Filters dialog, Analysis chart filter Reset button, Schedule UDF dialog, in-view dropdowns/checkboxes/paths), it does NOT appear in the Reset dialog. Only settings that have no other way to clear — grid column prefs, splitter ratios, dialog window dimensions, one-time skip flags — are included. System bookkeeping keys (`LastSyncUtcDate`, `LastSeenVersion`) are also excluded.
**Why:** A naive "reset all" would delete `LastSyncUtcDate` (forcing a costly full re-sync), wipe named grid layouts that the user deliberately created, and duplicate functionality that's already accessible via dedicated dialogs. A curated registry also gives us a single source of truth for natural-language labels and defaults, which the finisher skill (Step 3.7) keeps in sync as new settings are added.
**Date:** April 2026

### Grid Reset Must Recreate the Current View, Not Just Delete the Row
**Decision:** When the Reset dialog clears `ProgressGrid.PreferencesJson` / `ScheduleGrid.PreferencesJson`, `MenuResetUserSettings_Click` detects that case, calls `SkipSaveOnClose()` on the currently-loaded view, and force-recreates the view (`LoadProgressModule(forceReload: true)` or nulling `ContentArea.Content` + new `ScheduleView()`). Mirrors the existing `ResetGridLayoutsToDefault` pattern.
**Why:** The Progress and Schedule views save their in-memory column state to `UserSettings` on unload. If we only deleted the row and left the view in place, closing the app would trigger the Unload handler and re-save the in-memory widths back over our reset — a silent no-op. The view must be recreated so its Unload handler fires while the row is empty (and the new instance loads default columns).
**Date:** April 2026

### App-Close Guard via Single Counter, Not Per-Op Flags
**Decision:** One static counter in `Utilities/LongRunningOps.cs` tracks any critical operation via `using (LongRunningOps.Begin()) { ... }`. `MainWindow_Closing` reads `LongRunningOps.IsRunning` and warns the user before exiting if true. Six call sites currently wrap the scope: Submit Week, user snapshot delete, user snapshot revert, admin delete selected, admin delete all, admin upload-to-ProgressLog.
**Why:** Submit Week's Step 10 (local UPDATEs to WeekEndDate/ProgDate/PrevEarnMHs/PrevEarnQTY) and similar post-commit housekeeping can leave local SQLite in a partially updated state if the app process dies mid-sequence. A single counter with `Interlocked` increment/decrement is ~40ns per op (imperceptible overhead on second-scale operations), works across threads, and doesn't require per-op plumbing. Alternatives considered: (1) per-op boolean flags on MainWindow — rejected as six flags is worse than one counter, (2) service registry pattern with event notifications — rejected as overkill for a boolean "anything running?" check, (3) relocking the grid during Step 10 — rejected because the user explicitly wanted the grid usable after the snapshot was captured.
**Date:** April 2026

### WebView2 Virtual Host Maps to App Base Dir, Not Help/
**Decision:** The help sidebar's WebView2 virtual host `help.local` is mapped to the app base directory (`{baseDir}`), not the `Help` subfolder. Navigation URL is `https://help.local/Help/manual.html`, and `manual.html` references images via `<img src="../Assets/Images/Sidebar/xxx.png">`.
**Why:** When images moved to `Assets/Images/Sidebar/`, manual.html still lives at `Help/manual.html` — the sibling-relative `<img src>` pattern no longer works because the images are no longer siblings. Rooting the virtual host at the app base dir lets a single host cover both the HTML and the separately-located image folder. Alternative considered: duplicate the host mapping (one for `Help/`, one for `Assets/Images/Sidebar/`). Rejected because one mapping at the base dir is simpler and won't need further changes if the folder tree grows.
**Date:** April 2026

### Flat-File-Only Logging; SQLite Logs Table Dropped
**Decision:** `AppLogger` writes exclusively to `%LocalAppData%\VANTAGE\Logs\app-yyyyMMdd.log` (one file per UTC day, daily rotation). The parallel SQLite `Logs` table was dropped via schema migration v12. Export Logs dialog reads the flat files directly via `AppLogger.ReadLogFilesAsText(fromDate, toDate, minLevel)`, which parses the `[timestamp] Level ...` line prefix to apply the level filter and correctly groups multi-line exception stack traces with their owning log line.
**Why:** The DB path added I/O on every log call (`CREATE TABLE IF NOT EXISTS` + `INSERT` per entry) and duplicated the flat file's content for one reader — the Export Logs dialog. Alternatives considered: (1) keep the table but stop querying it — rejected, leaves dead writes on every log call, (2) keep the table as primary and drop the flat file — rejected, flat files are easier for users to grep/tail externally and survive SQLite corruption, (3) keep both but make DB write async/batched — rejected, adds complexity for a path we're eliminating anyway. Dropping the table is a one-way migration (v12); legacy rows are lost but the flat files for the same period are still on disk up to the 15-day retention window. Multi-line exception parsing in `ReadLogFilesAsText` uses a simple rule: any line not starting with `[timestamp] Level` is a continuation of the previous log line, so it inherits the previous line's filter decision. This correctly keeps stack traces attached to their Error/Warning headers without needing a structured parser.
**Date:** April 2026

### Permission Allowlist: Project `.claude/settings.json`, Not `settings.local.json`
**Decision:** Broadly-shareable permissions (git, dotnet, gh, PowerShell, file utilities, Skills, WebSearch, WebFetch domains) live in `.claude/settings.json` — versioned, committed, syncs across the user's machines via git. `.claude/settings.local.json` (gitignored) holds only genuinely machine-specific entries: absolute user-path `Read(...)`, one-off sed regexes, and personal overrides against team policy (e.g., `Bash(dotnet run)` when CLAUDE.md says never run the app from Claude Code). `.gitignore` narrowed from `.claude/` to `.claude/settings.local.json` so only the local file is ignored.
**Why:** Claude Code's approve-and-remember dialog writes to `settings.local.json` by default. Every approval made on machine A was invisible to machine B, causing the user to repeatedly re-approve the same commands across machines. Alternatives considered: (1) global `~/.claude/settings.json` — rejected because these permissions are VANTAGE-specific; other projects don't need dotnet/gh/Syncfusion fetches, (2) keep everything in local and accept the re-approval tax — rejected, it was the problem, (3) sync `settings.local.json` via git — rejected because by convention local files belong to a single machine, and forcing them to sync would fight Claude Code's expectations and bleed personal overrides into the team set. The project settings.json is the path Claude Code's design actually intends.
**Date:** April 2026

### Claude Instruction Split: Inline Vigilance Rules in CLAUDE.md, Triggered Detail in `Plans/Security_Guidelines.md`
**Decision:** Security and defensive-coding guidance for Claude Code is split into two locations. Vigilance rules with no clear trigger or that fire constantly (SQL parameterization, ownership/admin authority, secrets-must-not-surface-in-UI) live INLINE in CLAUDE.md as 1–3 line guards in their topical section. Detail rules with clear, infrequent triggers (CSV/Excel formula injection, filename / Windows-reserved-name guard, AI/Bedrock input bounds + response validation, logging hygiene specifics, FeedbackDialog secret-pattern check) live in `Plans/Security_Guidelines.md` with a one-line pointer in CLAUDE.md's Workflow Skills section. The pointer line itself names every trigger condition explicitly — the trigger list MUST live in CLAUDE.md (always loaded), never inside the referenced file.
**Why:** CLAUDE.md is loaded into every conversation; pointer files are only loaded if Claude opens them. The cost of inline content is recurring context load on every turn; the cost of pointer files is silent-skip when the trigger is too quiet to notice (a 1-line SQL change feels too small to "go read the security file"). The split aligns each rule with its actual failure mode: vigilance rules can't tolerate silent-skip so they go inline; clear-trigger rules don't pay their inline rent on the 99% of conversations that don't touch their surface area. Pattern matches the existing AWS deployment guide pointer (`Plans/claude-code-aws-deployment-guide.md`), which uses the same trigger-list-in-CLAUDE.md / detail-in-file structure. Alternatives considered: (1) put everything inline — rejected, ~100 extra lines on every conversation for content most turns don't need, (2) put everything in a pointer file with a generic "read for security work" instruction — rejected, the trigger surface is too broad and too easy to skip without an explicit named-condition list, (3) put the trigger list inside the pointer file — rejected, it's circular (you'd have to open the file to learn that you should open it). Threat model framing in `Security_Guidelines.md` explicitly notes VANTAGE is internal Summit Industrial software so defenses are calibrated to accidental misuse / paste-bombs / Excel formula injection at the customer's desk — not a determined external attacker — to prevent over-engineering when applying the rules.
**Date:** April 2026

---

## Analysis Module

### Chart Filters Independent from Summary Grid
**Decision:** Chart filter panel applies to charts only. Summary table has its own independent filters (Group By, My Records/All Users, Projects).
**Why:** Different analytical needs — charts for visual exploration across many dimensions, summary table for its own grouping context.
**Date:** April 2026

### Project Selection Not Persisted
**Decision:** Auto-selects first project from current local data instead of saving/restoring selections.
**Why:** Stale saved selections pointed to projects no longer in local DB after clear/re-sync, causing the table to appear empty.
**Date:** April 2026

---

## Plugin System

### PTP Plugin: Match on UDF2, Not Description
**Decision:** Changed matching from Description pattern (`FABRICATION - 4.SHP {CWP}`) to UDF2 field containing CWP value directly.
**Why:** Users were editing the Description field after import, breaking update-vs-create detection. UDF2 is a stable identifier not typically modified.
**Date:** March 2026

---

## Admin / Snapshots

### ManageProgressLog: Rolled Up from Per-RespParty to Per-Batch
**Decision:** Upload batches grouped by (Username, ProjectID, WeekEndDate, UploadUtcDate) instead of per-RespParty rows.
**Why:** Snapshots are always created as a unit across all RespParty values. Per-RespParty tracking added complexity with no practical benefit.
**Date:** February 2026

### Snapshot Modify Is Sync-Inert; Never Touches Activities
**Decision:** The new `ModifySnapshotDialog` writes edits only to Azure `VMS_ProgressSnapshots` (via `ScheduleRepository.UpdateSnapshotFullAsync`) and best-effort to the local 12-column `ProgressSnapshots` mirror. It explicitly excludes `SyncVersion`, `LocalDirty`, `AzureUploadUtcDate`, and `ActivityID` from the UPDATE, and never touches `Activities` / `VMS_Activities`. A subsequent sync push reports zero records for a Modify-only session.
**Why:** Revert-to-Snapshot exists for "restore my world to that week's state" — it overwrites local Activities and sets `LocalDirty = 1`, which pushes the snapshot values to Azure on next sync, destroying current live work. Users expressed a different need: "I submitted 50% last week but should have said 60% — let me fix the snapshot without disturbing my current progress at 75%." Conflating the two flows would make Modify destructive by default; keeping them separate (with the same data-loading pattern but opposite write invariants) preserves Revert's semantics AND gives users a non-destructive correction path. Bonus: because the change is invisible to the sync system, multi-user concurrency isn't a concern — the sync push isn't pushing anything.
**Date:** 2026-04-24

### Modify Save Detects Snapshot-Regenerated-Externally via 0-Rows-Affected
**Decision:** `UpdateSnapshotFullAsync` returns `int` (rows affected) instead of `bool`. If every dirty row UPDATE returns 0, the dialog surfaces "Snapshot was regenerated externally (most likely by Submit Week)" and leaves the dialog open so the user can close and reopen to see the current version. Partial cases (some rows affected, others zero) list the missing UniqueIDs.
**Why:** There's no row versioning on `VMS_ProgressSnapshots` today, so two concurrent flows (Modify open + Submit Week for the same week) can produce lost updates in Modify's favor on the old rows. Submit Week's path is a `DELETE` scoped to `(AssignedTo, ProjectID, WeekEndDate)` followed by bulk `INSERT` from live Activities — the UniqueIDs that come back may match, but values will have shifted. The affected-rows count is the cheapest signal we have that the snapshot has been regenerated under us. Alternatives considered: (1) add a `SyncVersion`-style column to `VMS_ProgressSnapshots` and version-check on UPDATE — rejected as a schema change that affects a shared table with millions of rows; (2) take a cross-dialog lock via `LongRunningOps` to block Submit Week while Modify is open — rejected because `LongRunningOps` today is an app-close guard, not a mutex between dialogs, and users can modeless-multitask by design. The 0-rows-affected check is the minimum-complexity defense that doesn't constrain the happy path.
**Date:** 2026-04-24

### Canonical SnapshotEditableColumns Mirrors Progress-View Editable Baseline With Dates Added
**Decision:** `Utilities/SnapshotEditableColumns.NonEditable` lists: `UniqueID`, `AzureUploadUtcDate`, `UpdatedBy`, `UpdatedUtcDate`, `CreatedBy`, `ProgDate`, `PrevEarnMHs`, `EarnedMHsRoc`, `PlanStart`, `PlanFin`. Every other `SnapshotData` property is editable — including the 9 required-metadata fields, which pass the non-empty check at Save. Mirrors `ImportTakeoffDialog.ExcludedColumns` (Progress-view editable baseline) with one intentional difference: `ActStart` and `ActFin` are editable here.
**Why:** User specification was "everything editable in Progress view." ActStart/ActFin are excluded from ImportTakeoffDialog's mapping because takeoff imports shouldn't set them, not because they're inherently read-only — the Schedule module's detail grid edits those same fields today. Modify-snapshot's use case (correcting a historical submission) often requires correcting the associated dates, so re-admitting them to the editable set is right. Keeping PlanStart/PlanFin non-editable because they're P6-driven and users have no "correct the plan date in the snapshot" workflow today. Required-metadata columns stay editable (users need to correct them after the snapshot is already submitted) but must not be blank at Save — the check reuses `ActivityRequiredMetadata.Fields` and the existing bulk-abort pattern.
**Date:** 2026-04-24

### SfDataGrid ColumnSizer="SizeToHeader" on 77-Column Editable Grids
**Decision:** `ModifySnapshotDialog`'s `SfDataGrid` uses `ColumnSizer="SizeToHeader"` rather than `ColumnSizer="Auto"` for its ~77 generated columns.
**Why:** User-tested initial version with `ColumnSizer="Auto"` took 30+ seconds to render and initially looked frozen. Syncfusion's `Auto` sizer measures every visible cell across every row for each column on first render; at 77 columns × hundreds of rows that's O(rows × columns) of UI-thread measurement work. `SizeToHeader` sizes by header text only, near-instant. Columns are still user-resizable from there. Adding column-width persistence (`SfDataGrid` column preferences in `UserSettings`) is a possible future improvement if users frequently resize — not done yet because this dialog's value comes from correctness, not layout polish. Pattern worth copying to any future generated-column grid with wide schemas.
**Date:** 2026-04-24

---

### Snapshot Dialogs Kept Open + Modeless, Not Detached Background Work
**Decision:** Both `ManageSnapshotsDialog` (user) and `AdminSnapshotsDialog` were converted from modal (`ShowDialog`) to modeless (`Show`). The dialog stays open with its own spinner during long deletes/uploads; the user drags it aside and keeps working in the main window. `DialogResult = true/false` patterns replaced with a public `NeedsRefresh` property the caller reads in the `Closed` event. Re-entrancy guards on both menu items focus the existing instance instead of opening a second.
**Why:** Initial plan was to extract delete logic into services, close the dialog immediately on confirm, and show a non-modal status toast pinned to MainWindow. User picked the simpler approach: keep the dialog intact with its existing spinner, just remove the modal-ness so MainWindow stays interactive. Four lines per dialog instead of a service refactor.
**Date:** April 2026

### Submit Week Snapshot Is Frozen at SELECT, Not at Click
**Decision:** The Progress grid is locked (`sfActivities.IsEnabled = false`) the instant the busy dialog appears, then unlocked via `Dispatcher.InvokeAsync` the instant the local SELECT into the in-memory DataTable completes inside the background task. The SELECT was also moved ahead of the Azure DELETE step so the grid re-enables faster on the overwrite path.
**Why:** Before this change, the grid stayed live throughout an async submit, so edits made after clicking Submit could or could not end up in the snapshot depending on micro-timing of Azure pre-checks. Now the snapshot boundary is explicit: everything up to the SELECT is captured; everything after the SELECT is intentionally the NEXT week's progress. Users stay productive during the slow Azure writes (DELETE/bulk-copy/purge) that happen after the snapshot is already frozen in memory. No data loss either way — post-SELECT edits still set `LocalDirty=1` and push on next sync.
**Date:** April 2026

### ProgressLog UserID: Concat Uploader + AssignedTo Instead of Adding a Column
**Decision:** The admin upload to `VANTAGE_global_ProgressLog` writes `UserID` as `"uploader|assignedto"` (pipe-separated), e.g. `"steve|Grant.Gilbert"`. Concatenation happens in the SQL expression `@userId + '|' + ISNULL([AssignedTo], '')` inside the existing `INSERT ... SELECT FROM VMS_ProgressSnapshots`. Wrapped in `LEFT(CAST(... AS NVARCHAR(MAX)), maxLen)` using the column's max length from `INFORMATION_SCHEMA`. Pipe chosen because it cannot appear in a Windows username.
**Why:** Before this, `UserID` was hardcoded to the admin who ran the upload, and the two rows in `VMS_ColumnMappings` that were supposed to route `AssignedTo` through were both broken (each had one field empty, so the mapping loader silently skipped them). Result: when two users' snapshots were uploaded at the same Timestamp + ProjectID, their ProgressLog rows were indistinguishable. Alternatives considered: (1) `ALTER TABLE` to add a dedicated `AssignedTo` column — rejected for now because the ProgressLog table has 14.9M rows and schema changes require DBA approval the admin doesn't currently have; backlogged for later, (2) fix the `VMS_ColumnMappings` rows so `AssignedTo` writes to its own column — but there's no suitable empty column and would need the ALTER anyway, (3) leave `UserID` alone and pull original owner from the separate `VMS_ProgressLogUploads` tracking table — rejected because the tracking table records per-batch aggregates, not per-row identity. The concat packs both pieces of info into a single existing column at zero I/O cost. Legacy rows keep their old `"steve"`-style UserID and are distinguishable from new rows by the absence of `|`.
**Date:** April 2026

### Dedup Check After DELETE, Not Before
**Decision:** In Submit Week, the order inside the background Task is now: SELECT local → unlock grid → DELETE old Azure snapshots (if overwriting) → dedup check (existing UniqueIDs for the week) → bulk copy.
**Why:** The dedup check only needs to skip records already submitted by *other* users for the same week. If we check before the DELETE, our own prior snapshots would match and incorrectly flag every row as a duplicate. Running the dedup after the DELETE leaves only other-user entries — which is exactly the conflict set we want to report. The DELETE scope is narrow (AssignedTo + ProjectID + WeekEndDate), so no risk of removing other users' data.
**Date:** April 2026

### ProgressLog Upload Timeouts: Tiered Ceilings, Not Unlimited
**Decision:** Every `CommandTimeout = 0` in `AdminSnapshotsDialog.xaml.cs` replaced with a tiered ceiling sized per operation: 3600s on large INSERT/UPDATE/DELETE paths (main `INSERT ... SELECT`, `UPDATE VMS_Activities` that fires `TR_VMS_Activities_SyncVersion`, per-group `DELETE`, full-table `DELETE ALL`), 120s on the snapshot-groups aggregate query, 60s on the single-row tracking INSERT.
**Why:** `CommandTimeout = 0` (infinite) was wedging the UI forever when a TCP socket dropped mid-operation — the ADO.NET client has no way to know the response isn't coming. On 2026-04-18 a 178,193-row upload committed all data on Azure, but the UI hung on the final `UPDATE VMS_Activities` because the trigger's response packet was lost. The timeout is not there to abort legitimate work; it's a dead-socket guard so the UI recovers. 3600s is well above the legitimate max (150k+ row uploads routinely exceed 10 minutes, so a 30-min ceiling would be too aggressive). Short ceilings make sense only on operations that can't legitimately take long. Note: this is the OPPOSITE choice from the Sync flow, which uses unlimited timeouts — see "Unlimited SQL Timeouts in Sync Flow". The difference: sync flow is a foreground operation blocking the user; this is an admin-triggered background operation where the UI sits on `SfBusyIndicator` — a dead socket is less tolerable here because the user has no signal anything is wrong. Consider applying the same tiered approach to the sync flow in the future.
**Date:** April 2026

### RespParty Write Path Removed — Aligning Writer with Feb 2026 Decision
**Decision:** `AdminSnapshotsDialog.UploadSnapshotsToProgressLog` no longer writes per-RespParty tracking rows to `VMS_ProgressLogUploads`. Writes one row per upload group (Username + ProjectID + WeekEndDate), with `RespParty = ""`. The column is retained in the schema, not dropped.
**Why:** The February 2026 decision "ManageProgressLog: Rolled Up from Per-RespParty to Per-Batch" aligned the READER side — grid XAML, REFRESH path, and DELETE path in `ManageProgressLogDialog` all ignore `RespParty`. But the WRITER in `AdminSnapshotsDialog` continued fanning uploads into N per-RespParty rows per batch, producing data nothing could read. Every upload cost one extra `SELECT ... GROUP BY RespParty` round-trip per group and N extra tracking INSERTs for zero information gain. Column retention (vs `DROP COLUMN`) is intentional: the column contains legacy data and dropping it requires DBA coordination on a 14.9M-row adjacent table; an empty-string column on new rows is harmless and keeps this a pure code change.
**Date:** April 2026
