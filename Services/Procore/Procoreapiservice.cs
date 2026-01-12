using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using VANTAGE.Utilities;

namespace MILESTONE.Services.Procore;

// Makes API calls to Procore REST API
public class ProcoreApiService
{
    private readonly ProcoreAuthService _authService;
    private readonly HttpClient _httpClient;

    public ProcoreApiService(ProcoreAuthService authService)
    {
        _authService = authService;
        _httpClient = new HttpClient();
    }

    // Get list of companies the user has access to
    public async Task<List<ProcoreCompany>> GetCompaniesAsync()
    {
        var json = await GetAsync("/rest/v1.0/companies");
        if (string.IsNullOrEmpty(json))
        {
            return new List<ProcoreCompany>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProcoreCompany>>(json) ?? new List<ProcoreCompany>();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ProcoreApiService.GetCompaniesAsync");
            return new List<ProcoreCompany>();
        }
    }

    // Get list of projects for a company
    public async Task<List<ProcoreProject>> GetProjectsAsync(long companyId)
    {
        var json = await GetAsync($"/rest/v1.0/projects?company_id={companyId}");
        if (string.IsNullOrEmpty(json))
        {
            return new List<ProcoreProject>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProcoreProject>>(json) ?? new List<ProcoreProject>();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ProcoreApiService.GetProjectsAsync");
            return new List<ProcoreProject>();
        }
    }

    // Get drawing revisions for a project (current drawings)
    public async Task<List<ProcoreDrawing>> GetDrawingsAsync(long projectId)
    {
        var json = await GetAsync($"/rest/v1.0/projects/{projectId}/drawing_revisions");
        if (string.IsNullOrEmpty(json))
        {
            return new List<ProcoreDrawing>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProcoreDrawing>>(json) ?? new List<ProcoreDrawing>();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ProcoreApiService.GetDrawingsAsync");
            return new List<ProcoreDrawing>();
        }
    }

    // Generic GET request to Procore API
    private async Task<string?> GetAsync(string endpoint)
    {
        try
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                AppLogger.Error("No access token available", "ProcoreApiService.GetAsync");
                return null;
            }

            var baseUrl = Credentials.ActiveProcoreApiUrl;
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}{endpoint}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                AppLogger.Error($"API call failed: {response.StatusCode} - {endpoint} - {json}", "ProcoreApiService.GetAsync");
                return null;
            }

            return json;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ProcoreApiService.GetAsync");
            return null;
        }
    }
}

// Models for Procore API responses
public class ProcoreCompany
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public long Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}

public class ProcoreProject
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public long Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("project_number")]
    public string? ProjectNumber { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("active")]
    public bool Active { get; set; }
}

public class ProcoreDrawing
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public long Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("revision_number")]
    public string RevisionNumber { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("current")]
    public bool Current { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("pdf_url")]
    public string? PdfUrl { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("discipline")]
    public ProcoreDiscipline? Discipline { get; set; }
}

public class ProcoreDiscipline
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public long Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}