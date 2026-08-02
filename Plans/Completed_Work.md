# VANTAGE: Milestone - Completed Work

This document tracks completed features and fixes. Items are moved here from Project_Status.md after user confirmation.

---

## Unreleased

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
