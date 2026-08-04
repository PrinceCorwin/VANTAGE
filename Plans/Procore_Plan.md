# Procore Drawings Integration — Plan & Status

**Goal:** Add "Fetch Drawings from Procore" to the Work Packages module. For each Work Package, pull the matching drawing PDFs from Procore (matched by `DwgNO`) into the Drawings form's per-WP folders; PDF generation then runs unchanged.

**Status (2026-08-02): BLOCKED on Procore Support — custom-app installation is disabled at the company-account level.** Steve now HAS Procore company-admin rights (including Directory-Admin), but the **"Install App" button never renders** in Company Admin → App Management. Verified it's not a permission gap (can edit Directory fieldsets/users) and not a viewport/width issue (zoomed out + maximized, still absent). Per Procore's own docs the button should be there — its absence means custom-app installs are not enabled for company 3480, which only Procore can turn on. Emailed sysadmin (also named Steve) on 2026-08-02 to flip the account setting or open a Procore ticket. Everything else is designed and the auth/mapping is proven. See "Blocker" + "Next steps".

---

## Design decisions (settled 2026-08-01)
- **Auth = service account (DMSA / `client_credentials`).** One credential, all users, no per-user browser login. (All users generate WPs, so per-user OAuth is the wrong model.)
- **Integration = fetch-to-folder sync step, NOT generation-time.** A "Fetch from Procore" action downloads matched PDFs into the Drawings form's `{ParentFolderPath}\{WorkPackage}\` folders; the existing `DrawingsRenderer` merges them unchanged. Decoupled from generation → offline-safe at generate time, reviewable before generating, reuses all existing code. (This supersedes the old plan's generation-time fetch.)
- **Revisions = current revision only**, with an optional "include all revisions" toggle (default OFF).
- **Match on `DwgNO` only** for now, normalized (trim/case).
- **Project name mismatch** solved by a persisted **per-Vantage-ProjectID → Procore-project** mapping (a dictionary, not one global project — WP generation spans many projects). Store shared in Azure (pattern of `AzureReportLayoutRepository`) so any user benefits once a project is linked; the service-account credential is central.

## Existing code (scaffolding — present, NOT wired to any UI)
- `Services/Procore/ProcoreAuthService.cs`, `ProcoreApiService.cs`, `ProcoreToken.cs`, `Dialogs/ProcoreAuthDialog.xaml(.cs)`.
  - Namespace inconsistency to fix: `MILESTONE.Services.Procore` vs `VANTAGE.Services.Procore`.
  - Current auth = authorization-code OOB (per-user). Will be replaced/augmented by `client_credentials` (DMSA) for production.
  - `ProcoreApiService` has `GetCompaniesAsync`, `GetProjectsAsync`, `GetDrawingsAsync` (→ `/projects/{id}/drawing_revisions`). No PDF-download method yet. Not sending `Procore-Company-Id` header yet (required).
- Creds via `CredentialService` (sandbox/prod toggle) + `AppConfig.ProcoreConfig`, loaded from `appsettings.json`/`appsettings.enc`.
- Drawings form: `Models/FormTemplate.DrawingsStructure {Title, ParentFolderPath}`; `Services/PdfRenderers/DrawingsRenderer` merges `{ParentFolderPath}\{WorkPackage}\*.pdf`; fetch decoupled from generation (`WorkPackageGenerator` just needs PDFs on disk).
- Activity fields: `DwgNO`, `SecondDwgNO`, `RevNO`, `ShtNO`, `WorkPackage`. A `SELECT DISTINCT DwgNO WHERE WorkPackage=@wp` query is new work (doesn't exist yet).

## What we proved — live read-only test, 2026-08-01 (via Steve's user login)
- Client-credentials token mint works at the OAuth layer (sandbox app).
- Production authorization-code login works: `/rest/v1.0/me` → `samalfitano@summit.us` (id 8067289); `/rest/v1.0/companies` → **Summit Industrial Construction LLC (company 3480)** and Hoffman Corporation (41665).
- **Project mapping confirmed:** Vantage `25.005` = Procore project **3199727** ("Fluor Lilly Pipe Racks"). (Drawing area 3119466; sample drawing/revision 386068887; test `DwgNO = LP1Y-APL(100)-034001-02`.)
- Drawings endpoint to use: `GET /rest/v1.0/projects/{project_id}/drawing_revisions` with `Procore-Company-Id` header. Each revision carries `number` (= DwgNO), `revision`, `current`, plus a PDF file/url field (exact field name TBD — confirm once a project is readable).

## Environment / host facts (corrected — `Credentials.cs` is partly wrong)
- **Production:** OAuth `https://login.procore.com/oauth`, API `https://api.procore.com`.
- **Developer sandbox:** OAuth AND API are BOTH `https://sandbox.procore.com` (NOT `login-sandbox.procore.com`, which is what `Credentials.cs` currently hardcodes — fix this). Monthly sandbox OAuth is `https://login-sandbox-monthly.procore.com/oauth`.
- Tokens are NOT shared across environments; the token issuer host must match the API host.
- App's registered OAuth **redirect URI = `urn:ietf:wg:oauth:2.0:oob`** (redirectless), not `http://localhost`.
- App id (dev portal): `feddff3a-4692-43b2-9fe4-e72dbcfb3dcf`. Production client_id `BQBoIYM6cE_...` (secret in gitignored `Plans/ProcoreInfo.txt`).

## BLOCKER

### Current (2026-08-02) — custom-app installs disabled at the account level
Every company-scoped call still returns **403 `{"errors":["App is not connected to this company."]}`** because the app is not yet installed on company 3480 — and we **cannot install it** because the **"Install App" button does not appear** in Company Admin → App Management.

What we ruled out on 2026-08-02 (walked the UI live):
- **Not a user-permission gap.** Steve now has company-admin rights incl. Directory-Admin — confirmed by being able to open Directory → Fieldsets and Create/Edit fieldsets and users. Procore's stated requirement (Admin on the Company Directory tool) is met.
- **Not a viewport/rendering issue.** The orange "Install App" button lives at the top-right of the App Management header; it stayed absent after zooming out to ~67% and maximizing the window.
- **Not the app's service-account permission grid.** The Developer-Portal "Data Connector Components" permission radios (None/Read-only/Standard/Admin per tool) only define what the service account can do *after* install; they have zero effect on whether the company-side Install button renders. (Note: do NOT grant the service account Admin on App Management/Directory — over-privileged; we need only Project → Drawings = Read-only.)

Conclusion: the generic "Install Custom App" entry point is gated by a **company-account setting that must be enabled by Procore** (or a Procore account rep). This is a support/account action, not something Steve can self-serve. **Emailed sysadmin Steve on 2026-08-02** to enable custom-app installs for company 3480 or open a Procore ticket.

### Prior framing (2026-08-01) — superseded
Originally believed the blocker was that Steve lacked Company-Admin / App-Management rights (his Company Tools showed only Portfolio + Planroom). He has since been granted company-admin, which advanced the blocker to the account-level setting described above rather than resolving it.

## Next steps to unblock (see `Plans/Procore_Admin_Install_Instructions.md` for the hand-off)
1. **App owner (Steve, Developer Portal): ✅ DONE 2026-08-01.** App is a Data Connection App with a service-account (client_credentials) component, **Project → Drawings = Read-only**; **Version 0.1.0 promoted to Production**. Production App Version Key is in `Plans/ProcoreInfo.txt` + the (gitignored) `Procore_Admin_Install_Instructions.md`. Gotcha found: Procore's **Save Component** only enables when BOTH Component Type boxes (User Level + Service Account) are checked — both are enabled (we use only the service account).
2. **Procore Support / account rep (via sysadmin Steve — emailed 2026-08-02):** enable **custom-app installation** for company 3480 so the "Install App → Install Custom App" button appears in Company Admin → App Management. This is the current gating action.
3. **Then (Steve, now has company-admin):** Admin → App Management → Install App → Install Custom App → paste Production App Version Key `3af7e34e-b8d9-485b-8522-8546b16b8955`. This auto-creates the **DMSA** (service-account user in the Company Directory) and its **client_id/secret**; then set **Permitted Projects** (include `25.005`). Save the **DMSA client_id + client_secret** → add to `Plans/ProcoreInfo.txt` (gitignored) labeled "DMSA production". (Secret is shown once at creation — copy immediately.)
4. Re-run the read test (DMSA token → project 3199727 `drawing_revisions` → confirm `LP1Y-APL(100)-034001-02` + capture the PDF-url field name).

## Build plan (after unblock) — folder-sync model
- `ProcoreApiService`: add `Procore-Company-Id` header; add `client_credentials` token path; add `GetDrawingRevisionsAsync(projectId)` + `DownloadDrawingPdfAsync(url, destPath)` (HttpClient stream-to-file, `PluginInstallService` pattern).
- New `SELECT DISTINCT DwgNO ... WHERE WorkPackage=@wp` (respecting current ProjectID).
- Project-mapping store (Azure table `Vantage ProjectID → {company_id, procore_project_id}`), with a one-time "Link Procore Project" picker (companies → projects).
- WP module: a **"Fetch Drawings from Procore"** action for the selected WPs → for each WP: distinct DwgNOs → match against the project's current drawing revisions → download PDFs into `{ParentFolderPath}\{WorkPackage}\` → report misses. Config toggle for current-only vs all-revisions.
- Fix `Credentials.cs`/config sandbox host; unify the `MILESTONE`/`VANTAGE` namespace.

## Security notes
- `Plans/ProcoreInfo.txt` holds Procore client secrets — **gitignored** (added 2026-08-01). Never commit.
- Fetch is **read-only** against Procore (GET only). The live test touched no data.

## References (Procore docs)
- Developer Managed Service Accounts: https://procore.github.io/documentation/developer-managed-service-accounts
- Install a Custom App: https://support.procore.com/products/online/user-guide/company-level/admin/tutorials/install-a-custom-app
- Install a Data Connection App: https://support.procore.com/products/online/user-guide/company-level/admin/tutorials/Install-data-connection-app
- Configure Service Account Permissions: https://support.procore.com/products/online/user-guide/company-level/admin/tutorials/configure-service-account-permissions
