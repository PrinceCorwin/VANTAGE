# Summit Takeoff Integration Guide for Vantage WPF App

**Purpose:** This document provides everything needed to integrate the AWS-based automated drawing takeoff system into the existing Vantage WPF application (.NET 8).

**What this system does:** Extracts BOM (Bill of Materials) data from piping isometric drawings using Claude Vision AI on AWS Bedrock. Processes batches of drawings and outputs an Excel workbook with material lists and connection details.

### Implementation Notes (Updated March 2, 2026)
The VANTAGE integration is built and working. Key differences from the original guide below:
- **NuGet packages:** Using AWS SDK v4 (`AWSSDK.S3 4.0.4`, `AWSSDK.StepFunctions 4.0.2.10`), not v3. SDK v4 has nullable properties on S3Object.
- **Drawing upload prefixes:** Changed from batch-based (`batchId/filename.pdf`) to config-based (`{client_id}/{project_id}/filename.pdf`). Files overwrite on re-upload (latest rev wins).
- **Credentials:** Stored in `appsettings.json` (dev) / `appsettings.enc` (production) via `TakeoffConfig` class, accessed through `CredentialService.Takeoff*` properties. Same encrypted config pattern as Textract.
- **Access control:** Estimator role (VMS_Estimators Azure table), not a column on Users table.
- **Config creation UI:** Not yet built. Next step — user wants detached full-width window for drawing preview.

---

## IMPORTANT: Architecture Change

**Original plan:** User draws crop boxes every batch, regions sent inline with batch request.

**Current implementation:** Crop region configs are saved to S3 and reused. Users select an existing config or create a new one. All configs are shared across all users.

**Rationale:** Most drawings from the same client/project have identical BOM layouts. Draw once, reuse forever. Configs stored in S3 are automatically available to everyone with app access.

---

## AWS Account & Region

- **Account ID:** 430392373397
- **Region:** us-east-1
- **All resources use prefix:** `summit-takeoff-`

---

## IAM User (Already Created)

**User:** `vantage-takeoff-user`

**Access Key ID and Secret:** Ask Steve for credentials. Store encrypted in app settings.

This user has permissions to:
- Start and poll Step Functions executions
- Upload drawings to S3
- Download results from S3
- List, read, and write config files in S3

---

## Required NuGet Packages

```xml
<PackageReference Include="AWSSDK.S3" Version="3.*" />
<PackageReference Include="AWSSDK.StepFunctions" Version="3.*" />
<PackageReference Include="AWSSDK.Core" Version="3.*" />
```

---

## AWS Resource ARNs

| Resource | ARN / Name |
|----------|------------|
| Step Functions State Machine | `arn:aws:states:us-east-1:430392373397:stateMachine:summit-takeoff-orchestrator` |
| Config Bucket | `summit-takeoff-config` |
| Drawings Bucket | `summit-takeoff-drawings` |
| Processing Bucket | `summit-takeoff-processing` |

---

## User Interface Requirements

The app needs two main workflows:

### Workflow 1: Config Management

Users must be able to:

1. **List existing configs** — Show all available configs from `s3://summit-takeoff-config/clients/`
2. **Select a config** — For use in batch processing
3. **Create new config:**
   - Upload a sample drawing
   - Display drawing on canvas
   - Draw BOM region(s) — can be multiple if BOM wraps to second area
   - Draw title block region
   - Enter client name and project name
   - Save to S3 as `clients/{client_id}/{project_id}.json`

**All configs are shared.** When one user creates a config, all other users see it immediately.

### Workflow 2: Batch Processing

1. Select config from dropdown (populated from S3)
2. Select drawing files (multi-select PDFs/images)
3. Click "Process"
4. Show progress (polling)
5. Download Excel when complete

---

## Config File Schema

**S3 Path:** `s3://summit-takeoff-config/clients/{client_id}/{project_id}.json`

**Example:** `clients/lilly/lp1y-swp.json`

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

## End-to-End Workflow Code

### Initialize AWS Clients

```csharp
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

// Load credentials from app settings (stored encrypted)
var credentials = new Amazon.Runtime.BasicAWSCredentials(
    settings.AwsAccessKeyId,
    settings.AwsSecretAccessKey
);

var s3Client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
var sfnClient = new AmazonStepFunctionsClient(credentials, RegionEndpoint.USEast1);
```

### List Available Configs

```csharp
public async Task<List<ConfigInfo>> ListConfigsAsync()
{
    var configs = new List<ConfigInfo>();
    
    var request = new ListObjectsV2Request
    {
        BucketName = "summit-takeoff-config",
        Prefix = "clients/"
    };
    
    var response = await s3Client.ListObjectsV2Async(request);
    
    foreach (var obj in response.S3Objects)
    {
        if (obj.Key.EndsWith(".json"))
        {
            // Load each config to get display names
            var configJson = await GetConfigAsync(obj.Key);
            configs.Add(new ConfigInfo
            {
                S3Key = obj.Key,
                ClientName = configJson.ClientName,
                ProjectName = configJson.ProjectName,
                DisplayName = $"{configJson.ClientName} - {configJson.ProjectName}"
            });
        }
    }
    
    return configs;
}
```

### Save New Config

```csharp
public async Task SaveConfigAsync(TakeoffConfig config)
{
    var key = $"clients/{config.ClientId}/{config.ProjectId}.json";
    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
    { 
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    });
    
    await s3Client.PutObjectAsync(new PutObjectRequest
    {
        BucketName = "summit-takeoff-config",
        Key = key,
        ContentBody = json,
        ContentType = "application/json"
    });
}
```

### Upload Drawings to S3

```csharp
public async Task<List<string>> UploadDrawingsAsync(string batchId, List<string> filePaths)
{
    var uploadedKeys = new List<string>();
    
    foreach (var filePath in filePaths)
    {
        var fileName = Path.GetFileName(filePath);
        var key = $"{batchId}/{fileName}";
        
        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = "summit-takeoff-drawings",
            Key = key,
            FilePath = filePath
        });
        
        uploadedKeys.Add(key);
    }
    
    return uploadedKeys;
}
```

### Start Step Functions Execution

**CRITICAL: Use this exact input format.**

```csharp
public async Task<string> StartBatchAsync(string batchId, string configPath, List<string> drawingKeys)
{
    // configPath example: "clients/lilly/lp1y-swp.json"
    // drawingKeys example: ["batch-20260302/drawing1.pdf", "batch-20260302/drawing2.pdf"]
    
    var input = new
    {
        config_path = configPath,
        bucket = "summit-takeoff-drawings",
        drawing_keys = drawingKeys
    };
    
    var response = await sfnClient.StartExecutionAsync(new StartExecutionRequest
    {
        StateMachineArn = "arn:aws:states:us-east-1:430392373397:stateMachine:summit-takeoff-orchestrator",
        Name = batchId,
        Input = JsonSerializer.Serialize(input)
    });
    
    return response.ExecutionArn;
}
```

**Input JSON structure:**
```json
{
    "config_path": "clients/lilly/lp1y-swp.json",
    "bucket": "summit-takeoff-drawings",
    "drawing_keys": [
        "batch-20260302-143052/drawing001.pdf",
        "batch-20260302-143052/drawing002.pdf"
    ]
}
```

### Poll for Completion

```csharp
public async Task<ExecutionResult> WaitForCompletionAsync(string executionArn, IProgress<string> progress)
{
    while (true)
    {
        var response = await sfnClient.DescribeExecutionAsync(new DescribeExecutionRequest
        {
            ExecutionArn = executionArn
        });
        
        if (response.Status == ExecutionStatus.SUCCEEDED)
        {
            var output = JsonSerializer.Deserialize<ExecutionOutput>(response.Output);
            return new ExecutionResult
            {
                Success = true,
                ExcelPath = output.ExcelPath,
                Summary = output.Summary
            };
        }
        
        if (response.Status == ExecutionStatus.FAILED ||
            response.Status == ExecutionStatus.TIMED_OUT ||
            response.Status == ExecutionStatus.ABORTED)
        {
            return new ExecutionResult
            {
                Success = false,
                Error = response.Cause ?? response.Error
            };
        }
        
        progress?.Report($"Processing... Status: {response.Status}");
        await Task.Delay(5000);
    }
}
```

### Download Excel Result

```csharp
public async Task DownloadExcelAsync(string batchId, string localPath)
{
    var key = $"batches/{batchId}/output/takeoff_{batchId}.xlsx";
    
    var response = await s3Client.GetObjectAsync(new GetObjectRequest
    {
        BucketName = "summit-takeoff-processing",
        Key = key
    });
    
    using var fileStream = File.Create(localPath);
    await response.ResponseStream.CopyToAsync(fileStream);
}
```

---

## Data Models

```csharp
public class TakeoffConfig
{
    public string ClientId { get; set; }
    public string ProjectId { get; set; }
    public string ClientName { get; set; }
    public string ProjectName { get; set; }
    public List<CropRegion> BomRegions { get; set; } = new();
    public CropRegion TitleBlockRegion { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
}

public class CropRegion
{
    public string Label { get; set; }
    public double XPct { get; set; }
    public double YPct { get; set; }
    public double WidthPct { get; set; }
    public double HeightPct { get; set; }
}

public class ConfigInfo
{
    public string S3Key { get; set; }
    public string ClientName { get; set; }
    public string ProjectName { get; set; }
    public string DisplayName { get; set; }
}

public class ExecutionOutput
{
    public string Status { get; set; }
    public string BatchId { get; set; }
    public string ExcelPath { get; set; }
    public ExecutionSummary Summary { get; set; }
}

public class ExecutionSummary
{
    public int TotalDrawings { get; set; }
    public int TotalBomItems { get; set; }
    public int TotalConnections { get; set; }
    public int FlaggedCount { get; set; }
}

public class ExecutionResult
{
    public bool Success { get; set; }
    public string ExcelPath { get; set; }
    public ExecutionSummary Summary { get; set; }
    public string Error { get; set; }
}
```

---

## Excel Output Structure

The downloaded Excel has 4 tabs:

### Summary Tab
- Total drawings, BOM items, connections
- Connections by type (BW, SW, THRD, BU, etc.)
- Connections by size
- Components by type

### Detail Tab
One row per **connection** (not per BOM item). Columns:
- drawing_number
- item_id
- instance (1-4 for items with multiple connections)
- size
- description
- component
- connection_type
- connection_size
- thickness
- class_rating
- material
- commodity_code
- pipe_spec (from title block)
- shop_field (SHOP or FIELD based on component type)
- concat_desc (concatenated description for rate matching)

**Business rules already applied:**
- VLV (valve) THRD and BU connections excluded — only welded connections (BW, SW, GRV, OLW) counted
- NIP (nipple) connections included
- Zero-connection items excluded from Detail tab

### Material Tab
One row per **BOM item** (not exploded). All original fields plus length, class_rating, shop_field.

### Flagged Tab
Items with extraction issues (low confidence, missing data, etc.)

---

## Canvas Drawing for Config Creation

When user creates a new config, they need to draw boxes on a sample drawing.

**WPF Implementation Notes:**

1. Load PDF first page as image (use PdfiumViewer or similar)
2. Display in Canvas or Image control
3. Track mouse events for drawing rectangles
4. Store coordinates as percentages: `x_pct = (x_pixels / image_width) * 100`
5. Allow multiple BOM regions (user might need 2 if BOM wraps)
6. Allow exactly one title block region
7. Visual feedback: green boxes for BOM, orange for title block

**Coordinate conversion:**
```csharp
var region = new CropRegion
{
    Label = "primary",
    XPct = (rect.X / imageWidth) * 100,
    YPct = (rect.Y / imageHeight) * 100,
    WidthPct = (rect.Width / imageWidth) * 100,
    HeightPct = (rect.Height / imageHeight) * 100
};
```

---

## Timing Reference

- Single drawing: ~30 seconds
- 27 drawings: ~5 minutes
- Throughput limited by Bedrock token quota (currently 200k tokens/min)

---

## Error Handling

### Common Errors

| Error | Cause | Solution |
|-------|-------|----------|
| `ThrottlingException` | Bedrock token quota exceeded | Wait and retry, or reduce batch size |
| `AccessDenied` | IAM permissions | Verify credentials are correct |
| `ExecutionAlreadyExists` | Duplicate batch ID | Ensure unique batch IDs (timestamp-based) |
| `States.TaskFailed` | Lambda error | Check CloudWatch logs |

### Partial Failures
The orchestrator continues on individual drawing failures. Failed drawings won't appear in results. Check `Summary.FlaggedCount` in output.

---

## Future Features (Not Yet Implemented)

These will be added as post-processing in the C# app after download:

1. **Fabrication item generation** — Generate cut, bevel, handling records from Material tab
2. **Rate sheet upload** — User provides Excel with unit rates
3. **Rate application** — Match rates to items, calculate manhours
4. **ROC splits** — Divide handling records by Rules of Credit
5. **VANTAGE tab** — Column rename for direct import
6. **Fitting makeup table** — For center-to-center length calculation

All of these operate on the downloaded Excel — no AWS changes needed.

---

## Credential Storage

Store AWS credentials encrypted in user's local app data. Prompt on first use of takeoff feature.

```csharp
public class TakeoffSettings
{
    public string AwsAccessKeyId { get; set; }
    public string AwsSecretAccessKey { get; set; }
}
```

Validate credentials by attempting `ListObjectsV2Async` on config bucket.

---

## Testing

**Existing config for testing:** `clients/lilly/lp1y-swp.json`

**Existing drawings:** 27 Lilly drawings already in `summit-takeoff-drawings` bucket.

**Test single drawing:**
```csharp
var batchId = $"test-{DateTime.Now:yyyyMMdd-HHmmss}";
var executionArn = await StartBatchAsync(
    batchId,
    "clients/lilly/lp1y-swp.json",
    new List<string> { "LP1Y-CHWR-033002-02-Piping-Isometric-CHWR-Rev.0.pdf" }
);
```

Note: Drawing keys for existing drawings don't include batch prefix — they're in root of bucket.

---

## Summary Checklist

- [ ] Add AWS SDK NuGet packages
- [ ] Create settings storage for AWS credentials
- [ ] Implement credential entry UI (first-time setup)
- [ ] Implement config list/select dropdown
- [ ] Implement config creation:
  - [ ] Sample drawing upload
  - [ ] Canvas display
  - [ ] Mouse drag to draw rectangles
  - [ ] Save to S3
- [ ] Implement batch processing:
  - [ ] File picker for drawings
  - [ ] Upload to S3 with batch ID
  - [ ] Start Step Functions
  - [ ] Poll for completion with progress
  - [ ] Download Excel
- [ ] (Future) Post-processing for fab items, rates, VANTAGE tab
