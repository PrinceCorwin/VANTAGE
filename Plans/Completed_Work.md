# VANTAGE: Milestone - Completed Work

This document tracks completed features and fixes. Items are moved here from Project_Status.md after user confirmation.

---

## Unreleased

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
