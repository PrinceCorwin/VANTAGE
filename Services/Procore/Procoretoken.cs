using System.Text.Json.Serialization;

namespace VANTAGE.Services.Procore;

public class ProcoreToken
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    // Calculated property - not from JSON
    [JsonIgnore]
    public DateTime ExpiresAt => DateTimeOffset.FromUnixTimeSeconds(CreatedAt).DateTime.AddSeconds(ExpiresIn);

    [JsonIgnore]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5); // 5 minute buffer
}