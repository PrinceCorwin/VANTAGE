# Summit Takeoff Integration Guide

**Purpose:** Reference doc for the AWS-based automated drawing takeoff system integrated into VANTAGE (.NET 8 WPF).

**What this system does:** Extracts BOM (Bill of Materials) data from piping isometric drawings using Claude Vision AI on AWS Bedrock. Processes batches of drawings and outputs an Excel workbook with material lists and connection details.

**Status:** Core integration complete. Post-processing pipeline (fabrication items, rates, import) is the remaining work.

---

## Architecture Overview

Crop region configs are saved to S3 and reused across all users. Users select an existing config or create a new one via a maximized modal window with PDF preview and canvas rectangle drawing. Most drawings from the same client/project have identical BOM layouts — draw once, reuse forever.

---

## AWS Account & Resources

- **Account ID:** 430392373397
- **Region:** us-east-1
- **IAM User:** `vantage-takeoff-user` (dedicated — NOT shared Textract credentials)
  - Permissions: s3:GetObject, s3:PutObject, s3:DeleteObject, s3:ListBucket on all takeoff buckets; states:StartExecution, states:DescribeExecution on orchestrator
- **Access control:** Estimator role only (VMS_Estimators Azure table). Admins NOT auto-included.

| Resource | ARN / Name |
|----------|------------|
| Step Functions State Machine | `arn:aws:states:us-east-1:430392373397:stateMachine:summit-takeoff-orchestrator` |
| Config Bucket | `summit-takeoff-config` |
| Drawings Bucket | `summit-takeoff-drawings` |
| Processing Bucket | `summit-takeoff-processing` |

---

## NuGet Packages

AWS SDK v4 (nullable properties on S3Object — use `?? 0` / `== true`):

```xml
<PackageReference Include="AWSSDK.S3" Version="4.0.4" />
<PackageReference Include="AWSSDK.StepFunctions" Version="4.0.2.10" />
```

---

## Credential Storage

Credentials stored in `appsettings.json` (dev) / `appsettings.enc` (production) via `TakeoffConfig` class in `Models/AppConfig.cs`. Accessed through `CredentialService.Takeoff*` static properties. Same encrypted config pattern as Textract.

Config keys: `Takeoff:AccessKey`, `Takeoff:SecretKey`, `Takeoff:Region`, `Takeoff:StateMachineArn`, `Takeoff:DrawingsBucket`, `Takeoff:ProcessingBucket`, `Takeoff:ConfigBucket`.

---

## Key Implementation Files

| File | Purpose |
|------|---------|
| `Services/AI/TakeoffService.cs` | S3 operations, Step Functions, config CRUD |
| `Services/AI/TakeoffPostProcessor.cs` | Excel post-processing (Labor + Summary tabs) |
| `Views/TakeoffView.xaml/.cs` | Takeoff module UI |
| `Dialogs/ConfigCreatorWindow.xaml/.cs` | Config creation/edit modal with PDF preview + canvas |
| `Dialogs/ManageDrawingsDialog.xaml/.cs` | Drawing management dialog |
| `Models/AI/CropRegionConfig.cs` | Config data model |

---

## Config File Schema

**S3 Path:** `s3://summit-takeoff-config/clients/{username}/{config-name}.json`

**Example:** `clients/steve/lilly-lp1y-swp.json`

```json
{
  "client_id": "lilly",
  "project_id": "lp1y-swp",
  "client_name": "Eli Lilly",
  "project_name": "LP1Y SWP Piping",

  "bom_regions": [
    {
      "label": "primary",
      "x_pct": 0.0,
      "y_pct": 55.0,
      "width_pct": 65.0,
      "height_pct": 40.0
    }
  ],

  "title_block_region": {
    "x_pct": 65.0,
    "y_pct": 85.0,
    "width_pct": 35.0,
    "height_pct": 15.0
  },

  "created_at": "2026-02-22T00:00:00Z",
  "created_by": "steve"
}
```

**Field reference:**

| Field | Type | Description |
|-------|------|-------------|
| `client_id` | string | URL-safe identifier (lowercase, hyphens, no spaces) |
| `project_id` | string | URL-safe identifier |
| `client_name` | string | Display name for UI |
| `project_name` | string | Display name for UI |
| `bom_regions` | array | One or more crop regions for BOM table(s) |
| `bom_regions[].label` | string | "primary", "secondary", etc. |
| `bom_regions[].x_pct` | float | Left edge, 0-100% of drawing width |
| `bom_regions[].y_pct` | float | Top edge, 0-100% of drawing height |
| `bom_regions[].width_pct` | float | Width, 0-100% |
| `bom_regions[].height_pct` | float | Height, 0-100% |
| `title_block_region` | object | Single crop region for title block |
| `created_at` | string | ISO timestamp |
| `created_by` | string | Username who created config |

---

## Step Functions Input Format

```json
{
    "config_path": "clients/steve/lilly-lp1y-swp.json",
    "bucket": "summit-takeoff-drawings",
    "drawing_keys": [
        "steve/lilly-lp1y-swp/drawing001.pdf",
        "steve/lilly-lp1y-swp/drawing002.pdf"
    ]
}
```

No batch_id in the input. Drawing keys use config-based prefixes (`{username}/{config-name}/filename.pdf`). Files overwrite on re-upload (latest rev wins).

---

## Excel Output Structure

The downloaded Excel from AWS has 4 tabs. The post-processor (`TakeoffPostProcessor`) then adds Labor and Summary tabs.

### Summary Tab (AWS-generated)
- Total drawings, BOM items, connections
- Connections by type (BW, SW, THRD, BU, etc.)
- Connections by size
- Components by type

### Detail Tab
One row per **connection** (not per BOM item). Columns:
- drawing_number, item_id, instance (1-4 for items with multiple connections)
- size, description, component, connection_type, connection_size
- thickness, class_rating, material, commodity_code
- pipe_spec (from title block), shop_field (SHOP or FIELD based on component type)
- concat_desc (concatenated description for rate matching)

**Business rules already applied by AWS:**
- VLV (valve) THRD and BU connections excluded — only welded connections (BW, SW, GRV, OLW) counted
- NIP (nipple) connections included
- Zero-connection items excluded from Detail tab

### Material Tab
One row per **BOM item** (not exploded). All original fields plus length, class_rating, shop_field.

### Flagged Tab
Items with extraction issues (low confidence, missing data, etc.)

### Labor Tab (post-processor)
Added by `TakeoffPostProcessor.GenerateLaborAndSummary()` after download.

### Summary Tab (post-processor)
Added by post-processor with aggregated labor metrics.

---

## Error Handling

| Error | Cause | Solution |
|-------|-------|----------|
| `ThrottlingException` | Bedrock token quota exceeded | Wait and retry, or reduce batch size |
| `AccessDenied` | IAM permissions | Verify credentials are correct |
| `ExecutionAlreadyExists` | Duplicate batch ID | Ensure unique batch IDs (timestamp-based) |
| `States.TaskFailed` | Lambda error | Check CloudWatch logs |

**Partial failures:** The orchestrator continues on individual drawing failures. Failed drawings won't appear in results. Check `Summary.FlaggedCount` in output.

**App-level failures:** Step Functions execution can return SUCCEEDED but app-level output has `"status": "failed"`. Code checks both — hides Download Excel when app status is failed.

**AWS SDK v4 null guard:** `ListConfigsAsync` must null-guard `response.S3Objects` — returns null when no objects match prefix.

---

## Timing Reference

- Single drawing: ~30 seconds
- 27 drawings: ~5 minutes
- Throughput limited by Bedrock token quota (currently 200k tokens/min)

---

## Testing

**Existing config for testing:** `clients/lilly/lp1y-swp.json`

**Existing drawings:** 27 Lilly drawings already in `summit-takeoff-drawings` bucket.

---

## Remaining Work: Post-Processing Pipeline

These will be added as post-processing in the C# app after download — no AWS changes needed. See also `Project_Status.md` backlog.

1. **Fabrication item generation** — Generate cut, bevel, handling records from Material tab
2. **Rate sheet upload** — User provides Excel with unit rates
3. **Rate application** — Match rates to items, calculate manhours
4. **ROC splits** — Divide handling records by Rules of Credit
5. **VANTAGE tab** — Column rename for direct import into Activities
6. **Fitting makeup table** — For center-to-center length calculation
