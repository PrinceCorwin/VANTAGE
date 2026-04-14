using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using VANTAGE.Models.AI;
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // Metadata for a previously processed takeoff batch
    public class BatchInfo
    {
        public string BatchId { get; set; } = string.Empty;
        public string? BatchName { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public int? DrawingCount { get; set; }
        public string? Username { get; set; }
        public string? ConfigName { get; set; }
        public bool IsComplete { get; set; }
    }

    // AWS Step Functions + S3 wrapper for the Summit Takeoff pipeline
    public class TakeoffService : IDisposable
    {
        private readonly AmazonStepFunctionsClient _sfnClient;
        private readonly AmazonS3Client _s3Client;
        private bool _disposed;

        public TakeoffService()
        {
            var region = RegionEndpoint.GetBySystemName(CredentialService.TakeoffRegion);

            _sfnClient = new AmazonStepFunctionsClient(
                CredentialService.TakeoffAccessKey,
                CredentialService.TakeoffSecretKey,
                region);

            _s3Client = new AmazonS3Client(
                CredentialService.TakeoffAccessKey,
                CredentialService.TakeoffSecretKey,
                region);
        }

        // Start a takeoff batch execution on the Step Functions state machine.
        // Returns the execution ARN for polling.
        public async Task<string> StartBatchAsync(
            string batchId,
            string configPath,
            List<string> drawingKeys,
            bool revBubbleOnly = false,
            CancellationToken cancellationToken = default)
        {
            var input = new
            {
                config_path = configPath,
                bucket = CredentialService.TakeoffDrawingsBucket,
                drawing_keys = drawingKeys,
                rev_bubble_only = revBubbleOnly
            };

            string inputJson = JsonSerializer.Serialize(input);

            AppLogger.Info($"Starting takeoff batch '{batchId}' with {drawingKeys.Count} drawing(s)",
                "TakeoffService.StartBatchAsync");

            var request = new StartExecutionRequest
            {
                StateMachineArn = CredentialService.TakeoffStateMachineArn,
                Name = batchId,
                Input = inputJson
            };

            var response = await _sfnClient.StartExecutionAsync(request, cancellationToken);

            AppLogger.Info($"Execution started: {response.ExecutionArn}",
                "TakeoffService.StartBatchAsync");

            return response.ExecutionArn;
        }

        // Poll the execution status. Returns (status, outputJson).
        // Status is RUNNING, SUCCEEDED, FAILED, TIMED_OUT, or ABORTED.
        public async Task<(string Status, string? Output)> PollExecutionAsync(
            string executionArn,
            CancellationToken cancellationToken = default)
        {
            var request = new DescribeExecutionRequest
            {
                ExecutionArn = executionArn
            };

            var response = await _sfnClient.DescribeExecutionAsync(request, cancellationToken);

            string status = response.Status.Value;
            string? output = status == "RUNNING" ? null : response.Output;

            return (status, output);
        }

        // Stop a running Step Functions execution
        public async Task StopExecutionAsync(
            string executionArn,
            string cause = "User cancelled batch",
            CancellationToken cancellationToken = default)
        {
            var request = new StopExecutionRequest
            {
                ExecutionArn = executionArn,
                Cause = cause
            };

            await _sfnClient.StopExecutionAsync(request, cancellationToken);
            AppLogger.Info($"Stopped execution: {executionArn}", "TakeoffService.StopExecutionAsync");
        }

        // Download the output Excel file from S3 to a local path
        public async Task DownloadExcelAsync(
            string batchId,
            string localPath,
            CancellationToken cancellationToken = default)
        {
            string key = $"batches/{batchId}/output/takeoff_{batchId}.xlsx";

            AppLogger.Info($"Downloading s3://{CredentialService.TakeoffProcessingBucket}/{key}",
                "TakeoffService.DownloadExcelAsync");

            var request = new GetObjectRequest
            {
                BucketName = CredentialService.TakeoffProcessingBucket,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request, cancellationToken);
            using var fileStream = File.Create(localPath);
            await response.ResponseStream.CopyToAsync(fileStream, cancellationToken);

            AppLogger.Info($"Downloaded to {localPath}", "TakeoffService.DownloadExcelAsync");
        }

        // List available configs from the config bucket (clients/ prefix).
        // Returns list of (s3Key, displayName) tuples.
        public async Task<List<(string Key, string DisplayName)>> ListConfigsAsync(
            CancellationToken cancellationToken = default)
        {
            var configs = new List<(string Key, string DisplayName)>();

            var request = new ListObjectsV2Request
            {
                BucketName = CredentialService.TakeoffConfigBucket,
                Prefix = "clients/"
            };

            var response = await _s3Client.ListObjectsV2Async(request, cancellationToken);

            foreach (var obj in (response.S3Objects ?? new List<Amazon.S3.Model.S3Object>()).Where(o => o.Key.EndsWith(".json")))
            {
                // Build display name from path: "clients/lilly/lp1y-swp.json" -> "lilly / lp1y-swp"
                var parts = obj.Key.Replace("clients/", "").Replace(".json", "").Split('/');
                string displayName = parts.Length >= 2
                    ? $"{parts[0]} / {parts[1]}"
                    : obj.Key;
                configs.Add((obj.Key, displayName));
            }

            AppLogger.Info($"Found {configs.Count} config(s) in S3",
                "TakeoffService.ListConfigsAsync");

            return configs;
        }

        // Derive the drawing prefix from a config key.
        // "clients/lilly/lp1y-swp.json" -> "lilly/lp1y-swp"
        public static string GetDrawingPrefix(string configKey)
        {
            return configKey.Replace("clients/", "").Replace(".json", "");
        }

        // Upload local drawing files to S3 under the config-based prefix.
        // Overwrites existing files (latest rev wins).
        // Returns the S3 keys for use in StartBatchAsync.
        public async Task<List<string>> UploadDrawingsAsync(
            string drawingPrefix,
            List<string> localPaths,
            IProgress<(int current, int total)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var uploadedKeys = new List<string>();

            AppLogger.Info($"Uploading {localPaths.Count} drawing(s) to prefix '{drawingPrefix}'",
                "TakeoffService.UploadDrawingsAsync");

            for (int i = 0; i < localPaths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string fileName = Path.GetFileName(localPaths[i]);
                string key = $"{drawingPrefix}/{fileName}";

                var request = new PutObjectRequest
                {
                    BucketName = CredentialService.TakeoffDrawingsBucket,
                    Key = key,
                    FilePath = localPaths[i]
                };

                await _s3Client.PutObjectAsync(request, cancellationToken);
                uploadedKeys.Add(key);

                progress?.Report((i + 1, localPaths.Count));
            }

            AppLogger.Info($"Uploaded {uploadedKeys.Count} drawing(s) to S3",
                "TakeoffService.UploadDrawingsAsync");

            return uploadedKeys;
        }

        // List drawings in S3 under a config-based prefix.
        // Returns list of (s3Key, fileName, sizeBytes, lastModified).
        public async Task<List<(string Key, string FileName, long Size, DateTime LastModified)>> ListDrawingsAsync(
            string drawingPrefix,
            CancellationToken cancellationToken = default)
        {
            var drawings = new List<(string Key, string FileName, long Size, DateTime LastModified)>();

            var request = new ListObjectsV2Request
            {
                BucketName = CredentialService.TakeoffDrawingsBucket,
                Prefix = $"{drawingPrefix}/"
            };

            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(request, cancellationToken);

                foreach (var obj in response.S3Objects ?? new List<Amazon.S3.Model.S3Object>())
                {
                    string fileName = Path.GetFileName(obj.Key);
                    if (!string.IsNullOrEmpty(fileName))
                        drawings.Add((obj.Key, fileName, obj.Size ?? 0, obj.LastModified ?? DateTime.MinValue));
                }

                request.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated == true);

            return drawings;
        }

        // Delete a single drawing from S3
        public async Task DeleteDrawingAsync(
            string s3Key,
            CancellationToken cancellationToken = default)
        {
            AppLogger.Info($"Deleting s3://{CredentialService.TakeoffDrawingsBucket}/{s3Key}",
                "TakeoffService.DeleteDrawingAsync");

            var request = new DeleteObjectRequest
            {
                BucketName = CredentialService.TakeoffDrawingsBucket,
                Key = s3Key
            };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);
        }

        // Delete multiple drawings from S3
        public async Task DeleteDrawingsAsync(
            List<string> s3Keys,
            CancellationToken cancellationToken = default)
        {
            if (s3Keys.Count == 0) return;

            AppLogger.Info($"Deleting {s3Keys.Count} drawing(s) from S3",
                "TakeoffService.DeleteDrawingsAsync");

            var request = new DeleteObjectsRequest
            {
                BucketName = CredentialService.TakeoffDrawingsBucket,
                Objects = s3Keys.Select(k => new KeyVersion { Key = k }).ToList()
            };

            await _s3Client.DeleteObjectsAsync(request, cancellationToken);
        }

        // Save a crop region config to the config bucket
        public async Task SaveConfigAsync(
            CropRegionConfig config,
            CancellationToken cancellationToken = default)
        {
            string key = $"clients/{config.ClientId}/{config.ProjectId}.json";

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            AppLogger.Info($"Saving config to s3://{CredentialService.TakeoffConfigBucket}/{key}",
                "TakeoffService.SaveConfigAsync");

            var request = new PutObjectRequest
            {
                BucketName = CredentialService.TakeoffConfigBucket,
                Key = key,
                ContentBody = json,
                ContentType = "application/json"
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);

            AppLogger.Info($"Config saved: {key}", "TakeoffService.SaveConfigAsync");
        }

        // Delete a config JSON from the config bucket
        public async Task DeleteConfigAsync(
            string configKey,
            CancellationToken cancellationToken = default)
        {
            AppLogger.Info($"Deleting config: s3://{CredentialService.TakeoffConfigBucket}/{configKey}",
                "TakeoffService.DeleteConfigAsync");

            var request = new DeleteObjectRequest
            {
                BucketName = CredentialService.TakeoffConfigBucket,
                Key = configKey
            };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);
        }

        // Load a crop region config from the config bucket
        public async Task<CropRegionConfig?> GetConfigAsync(
            string configKey,
            CancellationToken cancellationToken = default)
        {
            AppLogger.Info($"Loading config: {configKey}", "TakeoffService.GetConfigAsync");

            var request = new GetObjectRequest
            {
                BucketName = CredentialService.TakeoffConfigBucket,
                Key = configKey
            };

            using var response = await _s3Client.GetObjectAsync(request, cancellationToken);
            using var reader = new StreamReader(response.ResponseStream);
            string json = await reader.ReadToEndAsync(cancellationToken);

            return JsonSerializer.Deserialize<CropRegionConfig>(json);
        }

        // Write batch metadata to S3 for later retrieval by Previous Batches
        public async Task WriteMetadataAsync(
            string batchId,
            int drawingCount,
            string username,
            string configName,
            string? batchName = null,
            CancellationToken cancellationToken = default)
        {
            var metadata = new
            {
                drawingCount,
                username,
                configName,
                batchName,
                submittedAt = DateTime.UtcNow.ToString("o")
            };

            string json = JsonSerializer.Serialize(metadata);
            string key = $"batches/{batchId}/metadata.json";

            AppLogger.Info($"Writing metadata for batch '{batchId}': {drawingCount} drawing(s), config={configName}",
                "TakeoffService.WriteMetadataAsync");

            var request = new PutObjectRequest
            {
                BucketName = CredentialService.TakeoffProcessingBucket,
                Key = key,
                ContentBody = json,
                ContentType = "application/json"
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);
        }

        // List all previous batches from S3 for the Previous Batches dropdown
        public async Task<List<BatchInfo>> ListBatchesAsync(
            CancellationToken cancellationToken = default)
        {
            var batches = new List<BatchInfo>();

            // List batch folders using delimiter to get CommonPrefixes
            var listRequest = new ListObjectsV2Request
            {
                BucketName = CredentialService.TakeoffProcessingBucket,
                Prefix = "batches/",
                Delimiter = "/"
            };

            var listResponse = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);

            // CommonPrefixes are the batch folders (e.g., "batches/vantage-20260304-023504/")
            var batchFolders = listResponse.CommonPrefixes ?? new List<string>();

            AppLogger.Info($"Found {batchFolders.Count} batch folder(s) in S3",
                "TakeoffService.ListBatchesAsync");

            // Fetch metadata and check completion status in parallel
            var tasks = batchFolders.Select(async folder =>
            {
                // Extract batch ID from folder path (remove "batches/" prefix and trailing "/")
                string batchId = folder.Replace("batches/", "").TrimEnd('/');
                var info = new BatchInfo { BatchId = batchId };

                // Try to read metadata.json
                try
                {
                    var metaRequest = new GetObjectRequest
                    {
                        BucketName = CredentialService.TakeoffProcessingBucket,
                        Key = $"batches/{batchId}/metadata.json"
                    };

                    using var metaResponse = await _s3Client.GetObjectAsync(metaRequest, cancellationToken);
                    using var reader = new StreamReader(metaResponse.ResponseStream);
                    string json = await reader.ReadToEndAsync(cancellationToken);

                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("drawingCount", out var dcElem))
                        info.DrawingCount = dcElem.GetInt32();
                    if (doc.RootElement.TryGetProperty("submittedAt", out var saElem))
                        info.SubmittedAt = DateTime.Parse(saElem.GetString() ?? "");
                    if (doc.RootElement.TryGetProperty("username", out var userElem))
                        info.Username = userElem.GetString();
                    if (doc.RootElement.TryGetProperty("configName", out var cfgElem))
                        info.ConfigName = cfgElem.GetString();
                    if (doc.RootElement.TryGetProperty("batchName", out var bnElem))
                        info.BatchName = bnElem.GetString();
                }
                catch (AmazonS3Exception)
                {
                    // No metadata.json - try to parse timestamp from end of batch ID
                    // Formats: vantage-yyyyMMdd-HHmmss, AwsDwgTakeoff-yyyyMMdd-HHmmss, or custom-yyyyMMdd-HHmmss
                    if (batchId.Length >= 15)
                    {
                        string tail = batchId.Substring(batchId.Length - 15); // yyyyMMdd-HHmmss
                        string dateTime = tail.Replace("-", ""); // yyyyMMddHHmmss
                        if (dateTime.Length == 14 && DateTime.TryParseExact(dateTime, "yyyyMMddHHmmss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
                        {
                            info.SubmittedAt = parsed.ToLocalTime();
                        }
                    }
                }

                // Check if output Excel exists
                try
                {
                    var headRequest = new GetObjectMetadataRequest
                    {
                        BucketName = CredentialService.TakeoffProcessingBucket,
                        Key = $"batches/{batchId}/output/takeoff_{batchId}.xlsx"
                    };
                    await _s3Client.GetObjectMetadataAsync(headRequest, cancellationToken);
                    info.IsComplete = true;
                }
                catch (AmazonS3Exception)
                {
                    info.IsComplete = false;
                }

                return info;
            });

            var results = await Task.WhenAll(tasks);

            // Sort by date descending (newest first), nulls last
            batches = results
                .OrderByDescending(b => b.SubmittedAt ?? DateTime.MinValue)
                .ToList();

            return batches;
        }

        // Delete a batch folder and all its contents from S3
        public async Task DeleteBatchAsync(string batchId, CancellationToken cancellationToken = default)
        {
            string prefix = $"batches/{batchId}/";

            // List all objects under this batch prefix
            var listRequest = new ListObjectsV2Request
            {
                BucketName = CredentialService.TakeoffProcessingBucket,
                Prefix = prefix
            };

            var response = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);
            var objects = response.S3Objects;
            if (objects == null || objects.Count == 0) return;

            // Delete all objects in the batch folder
            foreach (var obj in objects)
            {
                await _s3Client.DeleteObjectAsync(
                    CredentialService.TakeoffProcessingBucket, obj.Key, cancellationToken);
            }

            AppLogger.Info($"Deleted batch {batchId} ({objects.Count} objects)",
                "TakeoffService.DeleteBatchAsync", App.CurrentUser?.Username);
        }

        // Rename a batch by updating its metadata.json
        public async Task RenameBatchAsync(string batchId, string newName, CancellationToken cancellationToken = default)
        {
            string metadataKey = $"batches/{batchId}/metadata.json";
            Dictionary<string, object> metadata;

            // Read existing metadata
            try
            {
                var getRequest = new GetObjectRequest
                {
                    BucketName = CredentialService.TakeoffProcessingBucket,
                    Key = metadataKey
                };

                using var response = await _s3Client.GetObjectAsync(getRequest, cancellationToken);
                using var reader = new StreamReader(response.ResponseStream);
                string json = await reader.ReadToEndAsync(cancellationToken);
                metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
            }
            catch (AmazonS3Exception)
            {
                // No existing metadata - create new
                metadata = new Dictionary<string, object>();
            }

            // Update batch name
            metadata["batchName"] = newName;

            // Write back to S3
            string updatedJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            var putRequest = new PutObjectRequest
            {
                BucketName = CredentialService.TakeoffProcessingBucket,
                Key = metadataKey,
                ContentBody = updatedJson,
                ContentType = "application/json"
            };

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);

            AppLogger.Info($"Renamed batch {batchId} to '{newName}'",
                "TakeoffService.RenameBatchAsync", App.CurrentUser?.Username);
        }

        // Download a drawing from S3 to a temp file, returns local path
        public async Task<string> DownloadDrawingToTempAsync(
            string s3Key,
            CancellationToken cancellationToken = default)
        {
            string ext = Path.GetExtension(s3Key);
            string tempPath = Path.Combine(Path.GetTempPath(), $"vantage_preview_{Guid.NewGuid():N}{ext}");

            AppLogger.Info($"Downloading drawing to temp: {s3Key}",
                "TakeoffService.DownloadDrawingToTempAsync");

            var request = new GetObjectRequest
            {
                BucketName = CredentialService.TakeoffDrawingsBucket,
                Key = s3Key
            };

            using var response = await _s3Client.GetObjectAsync(request, cancellationToken);
            using var fileStream = File.Create(tempPath);
            await response.ResponseStream.CopyToAsync(fileStream, cancellationToken);

            return tempPath;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _sfnClient?.Dispose();
                _s3Client?.Dispose();
                _disposed = true;
            }
        }
    }
}
