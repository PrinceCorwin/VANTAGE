# Procore Drawings Integration Plan

## Overview
Integrate Procore API to fetch project drawings for Work Package generation. Drawings are added to WPs via a new "Drawings" form template type with Procore or Local source options.

## Requirements Summary
- OAuth 2.0 authentication (sandbox + production)
- New "Drawings" form template type (like Cover, List, Form, Grid)
- Source options: Procore (fetch via API) or Local (browse folder)
- Default source in template, override at generation time
- Fetch ALL revisions for each drawing from Procore
- Match drawings by DwgNO field (user-configurable column)
- Alert user if Procore source selected but not connected
- Embed drawings in WP PDF; save separately if checkbox selected

---

## Phase 1: Procore Service Infrastructure

### New Files
| File | Purpose |
|------|---------|
| `Services/Procore/ProcoreAuthService.cs` | OAuth flow, token storage/refresh |
| `Services/Procore/ProcoreApiService.cs` | API calls: companies, projects, drawings |
| `Models/Procore/ProcoreCompany.cs` | Company model |
| `Models/Procore/ProcoreProject.cs` | Project model |
| `Models/Procore/ProcoreDrawing.cs` | Drawing + revisions model |

### OAuth Flow (OOB for Desktop)
1. Build auth URL → open in browser
2. User logs in, authorizes app
3. User copies auth code back to app
4. Exchange code for access_token + refresh_token
5. Store tokens via SettingsManager
6. Auto-refresh before expiry

### Settings Keys (via SettingsManager)
```
Procore.AccessToken
Procore.RefreshToken
Procore.TokenExpiry
Procore.CompanyId / CompanyName
Procore.ProjectId / ProjectName
Procore.DrawingColumn (default: "DwgNO")
```

---

## Phase 2: Procore Settings UI

### New Files
| File | Purpose |
|------|---------|
| `Dialogs/ProcoreSettingsDialog.xaml` | Connection, company/project selection |
| `Dialogs/ProcoreSettingsDialog.xaml.cs` | Dialog logic |

### Features
- Connect/Disconnect button (opens browser for OAuth)
- Paste auth code textbox
- Company dropdown (fetched after auth)
- Project dropdown (fetched when company selected)
- Drawing column mapping dropdown (DwgNO, SecondDwgNO, etc.)
- Connection status indicator
- Test Connection button

### MainWindow Integration
- Add "Procore Settings" to Tools menu

---

## Phase 3: Drawings Form Template Type

### Database Changes
- Add "Drawings" as valid TemplateType in FormTemplates table
- Built-in "Drawings" template seeded in DatabaseSetup.cs

### New Renderer
| File | Purpose |
|------|---------|
| `Services/PdfRenderers/DrawingsRenderer.cs` | Handles drawing PDF assembly |

### Form Template Structure (JSON)
```json
{
  "source": "Procore",        // "Procore" or "Local"
  "localFolderPath": null,    // For Local source
  "includeAllRevisions": true
}
```

### WorkPackageView Changes
- When WP template contains Drawings form and source is Procore:
  - Check if connected → if not, show alert with link to settings
- At generation time:
  - If Procore: collect DwgNO values from activities, fetch from API
  - If Local: prompt for folder, include all PDFs from folder

---

## Phase 4: Generation Integration

### WorkPackageGenerator.cs Modifications
1. Detect "Drawings" form in WP template
2. If source = "Procore":
   - Query activities for unique DwgNO values (using configured column)
   - Call `ProcoreApiService.SearchDrawingByNumberAsync()` for each
   - Fetch all revisions for each drawing
   - Download PDFs, add to document list
3. If source = "Local":
   - Get folder path (from template or user prompt)
   - Load all PDFs from folder
4. Merge drawings into WP PDF at the Drawings form's position
5. If "individual PDFs" checked, save drawings separately

### Error Handling
| Error | Action |
|-------|--------|
| Procore not connected | Alert user, offer to open settings |
| Drawing not found | Log warning, skip, continue with others |
| 401 Unauthorized | Attempt refresh, then re-auth prompt |
| 429 Rate Limited | Exponential backoff, retry 3x |
| Download failed | Log error, skip revision, continue |

---

## Phase 5: Polish & Testing

- Progress indicator: "Fetching drawing 3 of 7..."
- Generation result includes drawing count and any failures
- Sandbox testing with test drawings
- Production testing

---

## Implementation Order

1. **Phase 1** - ProcoreAuthService + ProcoreApiService (foundation)
2. **Phase 2** - ProcoreSettingsDialog + Tools menu item
3. **Phase 3** - Drawings form template type + DrawingsRenderer
4. **Phase 4** - WorkPackageGenerator integration
5. **Phase 5** - Error handling, progress, polish

---

## Critical Files to Modify

| File | Changes |
|------|---------|
| `Credentials.cs` | Already has Procore creds (no changes) |
| `DatabaseSetup.cs` | Seed built-in Drawings template |
| `MainWindow.xaml.cs` | Add Procore Settings to Tools menu |
| `WorkPackageGenerator.cs` | Handle Drawings form type |
| `WorkPackageView.xaml.cs` | Alert if Procore needed but not connected |
| `TokenResolver.cs` | Possibly add drawing-related tokens |

---

## API Endpoints

| Endpoint | Purpose |
|----------|---------|
| `POST /oauth/token` | Exchange code / refresh token |
| `GET /rest/v1.0/companies` | List companies |
| `GET /rest/v1.0/projects?company_id=X` | List projects |
| `GET /rest/v1.0/projects/{id}/drawings` | List drawings |
| `GET /rest/v1.0/drawings/{id}/revisions` | Get all revisions |
| Drawing PDF URL from revision data | Download PDF |

All API calls require header: `Authorization: Bearer {token}`, `Procore-Company-Id: {id}`
