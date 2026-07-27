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
    }
}
