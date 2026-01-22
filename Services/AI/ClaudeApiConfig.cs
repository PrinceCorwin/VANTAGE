using VANTAGE.Utilities;

namespace VANTAGE.Services.AI
{
    // Configuration for Claude Vision API access
    public static class ClaudeApiConfig
    {
        // API key for authentication (from Credentials.cs)
        public static string ApiKey => Credentials.ClaudeApiKey;

        // API endpoint URL
        public static string Endpoint => Credentials.ClaudeApiEndpoint;

        // Model to use for vision requests
        public static string Model => Credentials.ClaudeModel;

        // Anthropic API version header
        public const string ApiVersion = "2023-06-01";

        // Maximum tokens for response
        public const int MaxTokens = 4096;

        // Retry configuration
        public const int MaxRetries = 3;
        public const int InitialRetryDelayMs = 1000;  // 1 second
        public const int MaxRetryDelayMs = 10000;     // 10 seconds

        // Rate limit handling
        public const int RateLimitDelayMs = 5000;     // 5 seconds wait on 429
    }
}
