# PRD: Previous Batches Feature

## Overview
Allow users to view and re-download results from previously processed takeoff batches.

## User Story
As an estimator, I want to retrieve previous takeoff results in case of file loss or corruption, so I don't have to reprocess drawings.

---

## UI Requirements

### Location
Add "Previous Batches" button/tab in the Takeoff section of Vantage, alongside existing batch processing UI.

### Batch List View
Display a list of previous batches with columns:
| Column | Source | Format |
|--------|--------|--------|
| Batch ID | Folder name | `vantage-20260304-023504` |
| Date | Parsed from batch ID | `Mar 4, 2026 2:35 AM` |
| Drawing Count | Count files in extractions folder | `27` |
| Status | Check if output Excel exists | `Complete` or `Failed` |

Sort by date descending (newest first).

### Actions per Batch
- **Download Excel** — Download `takeoff_{batch_id}.xlsx` to user-selected location
- **Delete Batch** (optional, phase 2) — Remove batch folder from S3

---

## AWS Integration

### List Batches
```csharp
var request = new ListObjectsV2Request
{
    BucketName = "summit-takeoff-processing",
    Prefix = "batches/",
    Delimiter = "/"
};
var response = await s3Client.ListObjectsV2Async(request);
// response.CommonPrefixes contains batch folders
```

### Get Drawing Count
```csharp
var request = new ListObjectsV2Request
{
    BucketName = "summit-takeoff-processing",
    Prefix = $"batches/{batchId}/extractions/"
};
var response = await s3Client.ListObjectsV2Async(request);
int drawingCount = response.S3Objects.Count(o => o.Key.EndsWith(".json"));
```

### Check if Complete
```csharp
try
{
    await s3Client.GetObjectMetadataAsync(
        "summit-takeoff-processing",
        $"batches/{batchId}/output/takeoff_{batchId}.xlsx"
    );
    return "Complete";
}
catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    return "Failed";
}
```

### Download Excel
```csharp
var response = await s3Client.GetObjectAsync(new GetObjectRequest
{
    BucketName = "summit-takeoff-processing",
    Key = $"batches/{batchId}/output/takeoff_{batchId}.xlsx"
});

using var fileStream = File.Create(localPath);
await response.ResponseStream.CopyToAsync(fileStream);
```

---

## IAM Permissions
Already granted to `vantage-takeoff-user`:
- `s3:ListBucket` on `summit-takeoff-processing`
- `s3:GetObject` on `summit-takeoff-processing/*`

No changes needed.

---

## Performance Notes
- Listing batches is fast (single S3 call)
- Getting drawing count requires one call per batch — consider lazy loading or pagination if list grows large
- Consider caching batch list with refresh button

---

## Out of Scope (Phase 1)
- Delete batch functionality
- Filter by date range
- Search by drawing name
- Batch details view (list of drawings in batch)

---

## Acceptance Criteria
1. User can view list of all previous batches
2. List shows batch ID, date, drawing count, status
3. User can download Excel for any completed batch
4. List updates when new batches are processed
5. Empty state shown when no previous batches exist
