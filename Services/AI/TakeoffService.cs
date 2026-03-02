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
using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
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
            CancellationToken cancellationToken = default)
        {
            var input = new
            {
                config_path = configPath,
                bucket = CredentialService.TakeoffDrawingsBucket,
                drawing_keys = drawingKeys
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

            foreach (var obj in response.S3Objects.Where(o => o.Key.EndsWith(".json")))
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
