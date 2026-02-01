namespace VANTAGE.Models
{
    // Root configuration model matching appsettings.json structure
    public class AppConfig
    {
        public AzureConfig Azure { get; set; } = new();
        public EmailConfig Email { get; set; } = new();
        public AwsConfig Aws { get; set; } = new();
        public ProcoreConfig Procore { get; set; } = new();
        public UpdateConfig Update { get; set; } = new();
    }

    public class AzureConfig
    {
        public string Server { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class EmailConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string SenderAddress { get; set; } = string.Empty;
    }

    public class AwsConfig
    {
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
    }

    public class ProcoreConfig
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string AuthUrl { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = string.Empty;
        public string SandboxClientId { get; set; } = string.Empty;
        public string SandboxClientSecret { get; set; } = string.Empty;
        public string SandboxAuthUrl { get; set; } = string.Empty;
        public string SandboxApiUrl { get; set; } = string.Empty;
        public bool UseSandbox { get; set; }
    }

    public class UpdateConfig
    {
        public string BaseUrl { get; set; } = string.Empty;
    }
}
