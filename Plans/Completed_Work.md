# VANTAGE: Milestone - Completed Work

This document tracks completed features and fixes. Items are moved here from Project_Status.md after user confirmation.

---

## Unreleased

### August 6, 2026 (MCAA Ratesheet — length-unit normalization, key-recipe finalization, flange status correction; producer-side data + docs, no app code)

**Producer-side rate-sheet work in the external SkySkraper `output/cdx_rates_review_FinalMerged-r3.xlsx` (not in this repo) plus VANTAGE planning docs. No app code shipped.**

- **`length` column unit-normalized.** Profiled r3: 2,670 bare-numeral `length` values (all `PIPE` components) had no unit; appended `FT` to every one (uppercase, matching the existing `20FT`/`6IN` convention — pipe joint lengths are priced in feet). Verify-first mechanic recomposed each row's key byte-for-byte before writing (2,670/2,670 matched); post-write 0 bare numerals remain and 0 new key mismatches sheet-wide. Backup `output/backups/FinalMerged-r3_BACKUP_before_lengthunits.xlsx`.
- **PIPE/TUBE connection-qty rule confirmed.** Exec rule set: all PIPE and TUBE rows carry no connection quantity. Profiling showed all 5,801 PIPE + 250 TUBE rows already have blank `connection_qty` — already satisfied, no change.
- **Lookup-key recipe finalized (two changes):** (1) `connection_type` moved to immediately **before the sizes** (was after `connection_qty`); (2) `connection_qty` **dropped from the key** entirely — redundant once connections are listed one token per end (verified lossless: qty == token count on all 96,584 keyed rows, zero rows where qty carried extra info). The `connection_qty` column is retained for validation / AI-reference use. Steve rebuilt `Merged_Props` + keys with the new formulas in Excel and pasted as values (kept static for performance at ~116k rows).
- **New canonical doc `Plans/MCAA_Key_Composition.md`** — single source of truth for the lookup-key recipe: segment order, the exact `TEXTJOIN` formulas, header→column-letter map (with a "resolve by name, not letter" caveat), the formula-vs-static operational note (C/M kept as pasted values by design), and the C# byte-match contract. Replaces the non-versioned `SkySkraper/output/new_key_formulas.txt` scratch note. Linked from the PRD and CLAUDE.md "See also".
- **Corrected a false "TIG-row" alarm.** An openpyxl recompose flagged 148 `TIG`/WAM rows as key mismatches; root cause was that `NewMaterial` is a live `=XLOOKUP` and openpyxl can't evaluate formulas, so it read blank and dropped the material token. Not bad data — the stored keys were correct. Documented the real lesson: rebuild keys in Excel (where XLOOKUP resolves), never via an openpyxl static recompose.
- **Flange status corrected — flagged as NOT complete.** Steve found many `FLG` rows with incorrect/missing properties; since properties feed the key, those flanges will mis-key. Downgraded the "flanges DONE 2026-07-10" claim to PARTIAL in the PRD and Project_Status, added a `⚠️` immediate to-do to finish flange properties, and noted the final key rebuild must not run over flanges until their properties are corrected. (Flange work continues at home.)

**Key files:** `Plans/MCAA_Key_Composition.md` (new), `Plans/MCAA_Ratesheet_Plan.md`, `Plans/Project_Status.md`, `Plans/Decisions.md`, `CLAUDE.md`. Data changes are in the external SkySkraper workbook (not git-tracked).

### August 4, 2026 (Procore Drawings — blocker cleared, install-ready; docs only)

**No app code — the Procore-side install blocker was worked through live in the browser and the plan docs were updated to a cold-start-ready state for the work PC.**

- **Blocker resolved:** custom-app installation is now enabled for Summit's Procore company (3480) and Steve has the required permissions, so the **"Install Custom App" button now renders** in Company Admin → App Management. Walked the UI to confirm — earlier the button was absent even with Directory-Admin and after ruling out viewport/width, which traced to an account-level setting only Procore could enable (requested via sysadmin on 2026-08-02).
- **Clarified a false lead:** the Developer-Portal "Data Connector Components" service-account permission grid does NOT gate the company-side Install button; it only scopes what the service account can do after install (keep it at Project→Drawings=Read-only, no Admin).
- **Docs updated** (`Plans/Procore_Plan.md`): status flipped to UNBLOCKED; Blocker section marked RESOLVED with history preserved; added a self-contained **"RESUME HERE (work PC)"** section (pull first, key values inline, Step 1 install + capture DMSA creds, Step 2 prove the read path + capture the still-unknown PDF-url field name, Step 3 code touches). `Plans/Project_Status.md` Procore row updated to match.

**Key files:** `Plans/Procore_Plan.md`, `Plans/Project_Status.md`.
### August 4, 2026 (Email service migration — AWS SES infrastructure; no app code shipped)

**Stood up AWS SES to replace the Azure Communication Services email service, which was discovered to be running on Steve's personal Azure account. No app code shipped — the live `Utilities/EmailService.cs` (ACS) is untouched; this session is the company-side AWS setup plus a pre-written rewrite ready to drop in.**

- **Registered a company-owned sending domain, `summitapps.net`,** via Route 53 (Summit Industrial company asset, 1yr + auto-renew, WHOIS privacy; hosted zone `Z01697882XFI70LHLYFEE`). Neutral name so other in-house apps (REQit, etc.) can share the one SES sender.
- **Created the SES v2 domain identity** for `summitapps.net` (us-east-1) with Easy DKIM (RSA 2048) and a custom MAIL FROM (`mail.summitapps.net`); wrote all sending records into the Route 53 zone — 3 DKIM CNAMEs, root SPF (`-all`), DMARC (`p=none`), and the MAIL-FROM MX + SPF.
- **Parked a second SES identity for `summit.us`** for the eventual switch-over. The internal IT admin approved sending from `summit.us`, but final Comfort Systems blessing is ~2 months out (Comfort acquired Summit; `summit.us` DNS is in Comfort-managed Azure DNS). Plan: run on `summitapps.net` now, flip the sender to `@summit.us` later — a config change, not a rebuild.
- **Chose AWS SES over repairing the Azure path:** it gets a live all-users feature off personal infrastructure and onto the company AWS account (`430392373397`, already used for S3/tutorials/AI-Takeoff), and dodges the pending Summit→Comfort Azure-tenant migration entirely.
- **Pre-wrote the SES v2 rewrite of `EmailService.cs`** (`Plans/EmailService_SES_draft.txt`, kept as `.txt` so the build ignores it): same four public method signatures (zero call-site changes), identical email HTML/text, attachments via MimeKit raw-MIME. Drop-in at swap time.
- **Remaining before go-live:** Chris Aberly clicks the ICANN domain-verification email (check Mimecast quarantine), request SES production access, create the send-only `vantage-email-user` IAM key, add `AWSSDK.SimpleEmailV2` + `MimeKit` NuGets, wire config, swap the file, build, test.

- **Update (same day) — code swap applied, tested, then REVERTED; parked.** The SES rewrite was dropped in and **sending was proven end-to-end** as `vantage-email-user` from `DoNotReply@summitapps.net` (MessageIds returned; IAM policy had to be broadened to `identity/*` because SES sandbox also authorizes against the recipient identity). Then **reverted to the personal-Azure ACS**. **Blocker (delivery, not sending):** `summit.us` receives through Mimecast, which quarantines the brand-new `summitapps.net` domain until an org-wide allowlist entry is added — and that needs Comfort IT (~months). AWS infra left fully intact; flip is a ~10-min pre-written swap once Comfort allowlists it.

**Key files:** `Plans/Email_SES_Migration_Plan.md` (full state + remaining steps), `Plans/EmailService_SES_draft.txt` (pre-written rewrite), `Plans/Project_Status.md`.

### August 1, 2026 (Procore Drawings — design, auth validation, and hand-off docs; not shipped)

**Groundwork for the "Fetch Drawings from Procore" Work Packages feature. No app code shipped — planning + a live read-only auth test + hand-off docs.**

- **Design settled:** service-account (DMSA / client_credentials) auth for all-users, no per-user login; fetch-as-a-sync-step that fills the Drawings form's per-WP folders (generation unchanged, reuses `DrawingsRenderer`); current-revision-only default with an optional all-revisions toggle; match on `DwgNO`; per-Vantage-ProjectID → Procore-project mapping to handle name mismatches. Chosen over the old plan's generation-time fetch.
- **Auth validated live (read-only):** proved token exchange, `/rest/v1.0/me` and `/companies` (Summit Industrial 3480, Hoffman 41665), and confirmed the mapping **Vantage `25.005` = Procore project `3199727` ("Fluor Lilly Pipe Racks")**. Corrected host facts (dev sandbox uses `sandbox.procore.com` for both OAuth+API, not `login-sandbox`; redirect is `urn:ietf:wg:oauth:2.0:oob`).
- **Procore app configured:** "Vantage: Milestone" Version 0.1.0 promoted to Production with a service account granted Project→Drawings=Read-only.
- **Blocked (Procore-side, may sit for days/indefinitely):** the app must be installed on Summit's company (3480) via Company Admin → App Management, which requires "Admin" on the Company Directory tool — Steve lacks it. A permission-request + full admin-install instructions are drafted.
- **Docs:** rewrote `Plans/Procore_Plan.md` (current design, findings, blocker, build plan); added gitignored `Plans/Procore_Admin_Install_Instructions.md` (hand-off with the production App Version Key). `.gitignore` now excludes `Plans/ProcoreInfo.txt` + the install-instructions doc (both hold keys/secrets).

**Key files:** `Plans/Procore_Plan.md`, `.gitignore` (Procore secret/key exclusions).

### August 1, 2026 (Tutorials — in-app Admin "Manage Tutorials" tool)

**New Admin → Manage Tutorials — admins upload, edit, and delete tutorial videos from inside the app** (previously videos were managed by hand via the AWS CLI). Changes are live immediately; the Tutorials library reads the manifest each time it opens, so no app release is needed to add or change a video.

- **Manage Tutorials dialog** (`Dialogs/TutorialManagerDialog`) — lists every tutorial (File / Title / Description) in a Syncfusion `SfDataGrid` with the same column resize, sort, and funnel-filter behavior as the Progress grid; shows a count plus a warning when the bucket and manifest disagree (orphaned video / missing file). Buttons: Upload New, Edit Details, Delete, Refresh, with an `SfBusyIndicator` overlay during work.
- **Add/edit form** (`Dialogs/TutorialEditDialog`) — Upload mode browses to an `.mp4`, auto-inspects it (codecs, resolution, duration, size) and shows a readout; requires H.264 + AAC and blocks otherwise (the app can't transcode). Edit mode changes just the title/description. Enforces a unique filename and a unique title (case-insensitive; edit mode excludes the item's own title).
- **Video processing** — `Utilities/Mp4Tooling` shells out to ffprobe (inspect) and ffmpeg (`-map 0:v:0 [-map 0:a:0] -c copy -movflags +faststart` — faststart + drops any timecode/data track, no re-encode) before upload, matching the manual DaVinci-export workflow.
- **ffmpeg is NOT bundled in the installer.** `Utilities/FfmpegProvider` lazy-downloads `ffmpeg-tools.zip` (~73 MB) from the tutorials S3 bucket into `%LocalAppData%\VANTAGE\tools\` on the first upload, SHA-256 verified, extracted once — the main installer stays lean and only admins who upload ever fetch it.
- **S3 write methods** added to `Services/TutorialService`: `SaveManifestAsync` (writes via a lowercase-keyed DTO so the runtime-only `Watched` flag never lands in tutorials.json), `UploadVideoAsync` (progress), `DeleteVideoAsync`, `VideoExistsAsync` (authoritative pre-upload orphan-overwrite guard), `ListVideoKeysAsync` (manifest/bucket reconcile), `DownloadObjectAsync`.
- **IAM** — the `vantage-takeoff-user` `TakeoffAppAccess` policy's tutorials statement expanded from GetObject-only to Get/Put/Delete/List (`S3TutorialsReadWrite`). Admin gating: the menu is hidden for non-admins, the handler re-checks `IsAdmin` + Azure connection, and Delete re-verifies `IsUserAdmin` against Azure.
- **Watched-badge cleanup** — `SettingsManager.PruneWatchedTutorials` runs on every Tutorials-library open, dropping watched keys not in the manifest, so a deleted (or later same-name re-uploaded) video never shows a stale "Watched" badge. `Utilities/TutorialKeyValidator` sanitizes the user filename into a safe flat `.mp4` key.
- **Docs** — `Help/manual.html` gained an Administration → Manage Tutorials subsection (+ TOC entry and Admin-menu bullet).

**Key files:** `Dialogs/TutorialManagerDialog.xaml(.cs)`, `Dialogs/TutorialEditDialog.xaml(.cs)`, `Utilities/Mp4Tooling.cs`, `Utilities/FfmpegProvider.cs`, `Utilities/TutorialKeyValidator.cs`, `Services/TutorialService.cs`, `Utilities/SettingsManager.cs` (PruneWatchedTutorials), `Dialogs/TutorialsDialog.xaml.cs` (prune on load), `MainWindow.xaml(.cs)` (Admin menu item + handler), `Help/manual.html`.

---

**Archives:** See Plans/Archives/ for previous months.
