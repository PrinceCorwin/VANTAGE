# Procore Drawings Integration — Plan & Status

**Goal:** Add "Fetch Drawings from Procore" to the Work Packages module. For each Work Package, pull the matching drawing PDFs from Procore (matched by `DwgNO`) into the Drawings form's per-WP folders; PDF generation then runs unchanged.

**Status (2026-08-01): BLOCKED on a Procore company-admin action** — the app must be connected to Summit's Procore company before any drawing data is readable. Everything else is designed and the auth/mapping is proven. See "Blocker" + "Next steps".

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

## BLOCKER (2026-08-01)
Every company-scoped call returns **403 `{"errors":["App is not connected to this company."]}`**. The "Vantage: Milestone" app has never been installed/connected to Summit's Procore company (3480). This blocks BOTH the service-account and the user-login paths. Connecting the app needs **Company Admin / App Management** rights, which Steve does NOT have (his Company Tools show only Portfolio + Planroom). This is an organizational/permissions step, not a code problem.

## Next steps to unblock (see `Plans/Procore_Admin_Install_Instructions.md` for the hand-off)
1. **App owner (Steve, Developer Portal): ✅ DONE 2026-08-01.** App is a Data Connection App with a service-account (client_credentials) component, **Project → Drawings = Read-only**; **Version 0.1.0 promoted to Production**. Production App Version Key is in `Plans/ProcoreInfo.txt` + the (gitignored) `Procore_Admin_Install_Instructions.md`. Gotcha found: Procore's **Save Component** only enables when BOTH Component Type boxes (User Level + Service Account) are checked — both are enabled (we use only the service account).
2. **Company Admin (Summit Procore):** install the custom app on company 3480 (Admin → App Management → Install Custom App → App Version ID). This auto-creates the **DMSA** (a service-account user in the Company Directory) and its **client_id/secret**; then set **Permitted Projects** (include `25.005`). Return the **DMSA client_id + client_secret** → add to `Plans/ProcoreInfo.txt` (gitignored) labeled "DMSA production".
3. Re-run the read test (DMSA token → project 3199727 `drawing_revisions` → confirm `LP1Y-APL(100)-034001-02` + capture the PDF-url field name).

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
