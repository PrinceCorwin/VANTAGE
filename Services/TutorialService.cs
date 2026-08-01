using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using VANTAGE.Models;
using VANTAGE.Utilities;

namespace VANTAGE.Services
{
    // Access to tutorial videos in the private summit-vantage-tutorials S3 bucket.
    // Reuses the scoped AWS credential the AI Takeoff feature already ships
    // (vantage-takeoff-user). A tutorials.json manifest holds each video's filename
    // key, name, and description; the list shows the name and description, sorted by
    // filename key. Playback uses a short-lived pre-signed URL so a copied link can't
    // circulate.
    public static class TutorialService
    {
        private const string TutorialsBucket = "summit-vantage-tutorials";
        private const string ManifestKey = "tutorials.json";

        // Link lives just past a normal watch-through. If it lapses (user gets
        // called away), playback stops and they reopen the video to restart.
        private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(5);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static AmazonS3Client CreateClient()
        {
            var region = RegionEndpoint.GetBySystemName(CredentialService.TakeoffRegion);
            return new AmazonS3Client(
                CredentialService.TakeoffAccessKey,
                CredentialService.TakeoffSecretKey,
                region);
        }

        // Read the manifest and sort by filename key. Filenames are number-prefixed
        // ("1 - ...") so they self-sort into the intended order.
        public static async Task<List<TutorialItem>> GetTutorialsAsync(CancellationToken cancellationToken = default)
        {
            using var s3 = CreateClient();
            var request = new GetObjectRequest
            {
                BucketName = TutorialsBucket,
                Key = ManifestKey
            };

            using var response = await s3.GetObjectAsync(request, cancellationToken);
            using var reader = new StreamReader(response.ResponseStream);
            string json = await reader.ReadToEndAsync(cancellationToken);

            var items = JsonSerializer.Deserialize<List<TutorialItem>>(json, JsonOptions)
                        ?? new List<TutorialItem>();
            return items.OrderBy(i => i.Key, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Build a short-lived pre-signed GET URL for a tutorial video key.
        public static string GetTutorialUrl(string videoKey)
        {
            using var s3 = CreateClient();
            var request = new GetPreSignedUrlRequest
            {
                BucketName = TutorialsBucket,
                Key = videoKey,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(LinkLifetime)
            };

            return s3.GetPreSignedURL(request);
        }

        // ---- Admin management (Manage Tutorials dialog) ----

        // Lowercase-keyed shape written to tutorials.json. Kept separate from
        // TutorialItem so the runtime-only Watched flag never leaks into the manifest.
        private class ManifestEntry
        {
            [JsonPropertyName("key")] public string Key { get; set; } = "";
            [JsonPropertyName("name")] public string Name { get; set; } = "";
            [JsonPropertyName("description")] public string Description { get; set; } = "";
        }

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // Overwrite tutorials.json with the given items (sorted by key).
        public static async Task SaveManifestAsync(IEnumerable<TutorialItem> items)
        {
            var entries = items
                .OrderBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
                .Select(i => new ManifestEntry { Key = i.Key, Name = i.Name, Description = i.Description })
                .ToList();

            string json = JsonSerializer.Serialize(entries, WriteOptions);

            using var s3 = CreateClient();
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = TutorialsBucket,
                Key = ManifestKey,
                ContentBody = json,
                ContentType = "application/json"
            });
        }

        // Upload a local MP4 under the given key, reporting 0-100 progress.
        public static async Task UploadVideoAsync(string localPath, string key, IProgress<int>? progress = null)
        {
            using var s3 = CreateClient();
            var request = new PutObjectRequest
            {
                BucketName = TutorialsBucket,
                Key = key,
                FilePath = localPath,
                ContentType = "video/mp4"
            };

            if (progress != null)
                request.StreamTransferProgress += (s, e) => progress.Report(e.PercentDone);

            await s3.PutObjectAsync(request);
        }

        public static async Task DeleteVideoAsync(string key)
        {
            using var s3 = CreateClient();
            await s3.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = TutorialsBucket,
                Key = key
            });
        }

        // True if an object with this key already exists in the bucket.
        public static async Task<bool> VideoExistsAsync(string key)
        {
            using var s3 = CreateClient();
            try
            {
                await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = TutorialsBucket,
                    Key = key
                });
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        // All .mp4 object keys in the bucket (used to reconcile against the manifest).
        public static async Task<List<string>> ListVideoKeysAsync()
        {
            using var s3 = CreateClient();
            var keys = new List<string>();
            string? continuationToken = null;

            do
            {
                var response = await s3.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = TutorialsBucket,
                    ContinuationToken = continuationToken
                });

                keys.AddRange(response.S3Objects
                    .Select(o => o.Key)
                    .Where(k => k.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)));

                continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
            }
            while (continuationToken != null);

            return keys;
        }

        // Download an arbitrary bucket object to a local file, reporting 0-100 progress.
        // Used to fetch the ffmpeg tools zip on first admin use.
        public static async Task DownloadObjectAsync(string key, string destPath, IProgress<int>? progress = null)
        {
            using var s3 = CreateClient();
            using var response = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = TutorialsBucket,
                Key = key
            });

            if (progress != null)
                response.WriteObjectProgressEvent += (s, e) => progress.Report(e.PercentDone);

            await response.WriteResponseStreamToFileAsync(destPath, append: false, System.Threading.CancellationToken.None);
        }
    }
}
