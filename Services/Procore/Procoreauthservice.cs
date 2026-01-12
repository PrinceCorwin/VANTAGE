using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VANTAGE.Services.Procore;
using VANTAGE.Utilities;

namespace MILESTONE.Services.Procore;

// Handles Procore OAuth authentication, token storage, and refresh
public class ProcoreAuthService
{
    private static readonly string TokenFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MILESTONE",
        "procore_tokens.json");

    private readonly HttpClient _httpClient;
    private ProcoreToken? _currentToken;

    public ProcoreAuthService()
    {
        _httpClient = new HttpClient();
    }

    // Returns true if we have valid (or refreshable) tokens
    public bool IsAuthenticated => _currentToken != null && !string.IsNullOrEmpty(_currentToken.RefreshToken);

    // Get a valid access token, refreshing if necessary
    // Returns null if not authenticated or refresh fails
    public async Task<string?> GetAccessTokenAsync()
    {
        if (_currentToken == null)
        {
            _currentToken = LoadTokenFromDisk();
        }

        if (_currentToken == null)
        {
            return null;
        }

        if (_currentToken.IsExpired)
        {
            var refreshed = await RefreshTokenAsync();
            if (!refreshed)
            {
                return null;
            }
        }

        return _currentToken.AccessToken;
    }

    // Opens browser for user to authenticate with Procore
    public void OpenBrowserForAuth()
    {
        var clientId = Credentials.ActiveProcoreClientId;
        var authUrl = Credentials.ActiveProcoreAuthUrl;
        var redirectUri = Uri.EscapeDataString(Credentials.ProcoreRedirectUri);

        var url = $"{authUrl}/authorize?response_type=code&client_id={clientId}&redirect_uri={redirectUri}";

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    // Exchange authorization code for tokens
    // Returns true if successful
    public async Task<bool> ExchangeCodeForTokenAsync(string authorizationCode)
    {
        try
        {
            var tokenUrl = $"{Credentials.ActiveProcoreAuthUrl}/token";

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = authorizationCode.Trim(),
                ["client_id"] = Credentials.ActiveProcoreClientId,
                ["client_secret"] = Credentials.ActiveProcoreClientSecret,
                ["redirect_uri"] = Credentials.ProcoreRedirectUri
            });

            var response = await _httpClient.PostAsync(tokenUrl, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                AppLogger.Error($"Token exchange failed: {response.StatusCode} - {json}", "ProcoreAuthService.ExchangeCodeForTokenAsync");
                return false;
            }

            _currentToken = JsonSerializer.Deserialize<ProcoreToken>(json);
            if (_currentToken == null)
            {
                AppLogger.Error("Failed to deserialize token response", "ProcoreAuthService.ExchangeCodeForTokenAsync");
                return false;
            }

            SaveTokenToDisk(_currentToken);
            AppLogger.Info("Procore authentication successful", "ProcoreAuthService.ExchangeCodeForTokenAsync");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ProcoreAuthService.ExchangeCodeForTokenAsync");
            return false;
        }
    }

    // Refresh the access token using the refresh token
    private async Task<bool> RefreshTokenAsync()
    {
        if (_currentToken == null || string.IsNullOrEmpty(_currentToken.RefreshToken))
        {
            return false;
        }

        try
        {
            var tokenUrl = $"{Credentials.ActiveProcoreAuthUrl}/token";

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = Credentials.ActiveProcoreClientId,
                ["client_secret"] = Credentials.ActiveProcoreClientSecret,
                ["redirect_uri"] = Credentials.ProcoreRedirectUri,
                ["refresh_token"] = _currentToken.RefreshToken
            });

            var response = await _httpClient.PostAsync(tokenUrl, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                AppLogger.Error($"Token refresh failed: {response.StatusCode} - {json}", "ProcoreAuthService.RefreshTokenAsync");
                ClearToken();
                return false;
            }

            _currentToken = JsonSerializer.Deserialize<ProcoreToken>(json);
            if (_currentToken == null)
            {
                AppLogger.Error("Failed to deserialize refreshed token", "ProcoreAuthService.RefreshTokenAsync");
                return false;
            }

            SaveTokenToDisk(_currentToken);
            AppLogger.Info("Procore token refreshed", "ProcoreAuthService.RefreshTokenAsync");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ProcoreAuthService.RefreshTokenAsync");
            ClearToken();
            return false;
        }
    }

    // Clear stored tokens (disconnect from Procore)
    public void ClearToken()
    {
        _currentToken = null;
        try
        {
            if (File.Exists(TokenFilePath))
            {
                File.Delete(TokenFilePath);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ProcoreAuthService.ClearToken");
        }
    }

    private ProcoreToken? LoadTokenFromDisk()
    {
        try
        {
            if (!File.Exists(TokenFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(TokenFilePath);
            return JsonSerializer.Deserialize<ProcoreToken>(json);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ProcoreAuthService.LoadTokenFromDisk");
            return null;
        }
    }

    private void SaveTokenToDisk(ProcoreToken token)
    {
        try
        {
            var directory = Path.GetDirectoryName(TokenFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(token, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(TokenFilePath, json);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ProcoreAuthService.SaveTokenToDisk");
        }
    }
}