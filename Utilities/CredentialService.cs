using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VANTAGE.Models;

namespace VANTAGE.Utilities
{
    // Loads app credentials from appsettings.json (dev) or appsettings.enc (production).
    // Replaces the old compiled Credentials.cs with a runtime-loaded config.
    public static class CredentialService
    {
        private static AppConfig? _config;
        private static readonly object _lock = new();
        private const string PlaintextFileName = "appsettings.json";
        private const string EncryptedFileName = "appsettings.enc";
        private const int Pbkdf2Iterations = 100_000;
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int KeySize = 32; // AES-256

        // Encryption passphrase — compiled into the app.
        // Not a vault, but raises the bar vs plaintext credentials in the DLL.
        private const string Passphrase = "VANTAGE-Summit-2026-Cr3d3nt1al-Encrypt10n-K3y-X9mPqR7vL2w";

        private static AppConfig Config
        {
            get
            {
                if (_config == null)
                {
                    lock (_lock)
                    {
                        _config ??= LoadConfig();
                    }
                }
                return _config;
            }
        }

        // Azure SQL
        public static string AzureServer => Config.Azure.Server;
        public static string AzureDatabase => Config.Azure.Database;
        public static string AzureUserId => Config.Azure.UserId;
        public static string AzurePassword => Config.Azure.Password;

        // Email
        public static string AzureEmailConnectionString => Config.Email.ConnectionString;
        public static string AzureEmailSenderAddress => Config.Email.SenderAddress;

        // AWS Textract
        public static string AwsAccessKey => Config.Aws.AccessKey;
        public static string AwsSecretKey => Config.Aws.SecretKey;
        public static string AwsRegion => Config.Aws.Region;

        // Procore (active = sandbox or production based on toggle)
        public static string ActiveProcoreClientId => Config.Procore.UseSandbox
            ? Config.Procore.SandboxClientId : Config.Procore.ClientId;
        public static string ActiveProcoreClientSecret => Config.Procore.UseSandbox
            ? Config.Procore.SandboxClientSecret : Config.Procore.ClientSecret;
        public static string ActiveProcoreAuthUrl => Config.Procore.UseSandbox
            ? Config.Procore.SandboxAuthUrl : Config.Procore.AuthUrl;
        public static string ActiveProcoreApiUrl => Config.Procore.UseSandbox
            ? Config.Procore.SandboxApiUrl : Config.Procore.ApiUrl;
        public static string ProcoreRedirectUri => Config.Procore.RedirectUri;

        // Update
        public static string UpdateBaseUrl => Config.Update.BaseUrl;

        // Load config: try plaintext first (dev), then encrypted (production)
        private static AppConfig LoadConfig()
        {
            string baseDir = AppContext.BaseDirectory;

            // Dev mode: plaintext appsettings.json
            string plaintextPath = Path.Combine(baseDir, PlaintextFileName);
            if (File.Exists(plaintextPath))
            {
                string json = File.ReadAllText(plaintextPath);
                return JsonSerializer.Deserialize<AppConfig>(json)
                    ?? throw new InvalidOperationException($"Failed to deserialize {PlaintextFileName}");
            }

            // Production mode: encrypted appsettings.enc
            string encryptedPath = Path.Combine(baseDir, EncryptedFileName);
            if (File.Exists(encryptedPath))
            {
                return LoadEncryptedConfig(encryptedPath);
            }

            throw new FileNotFoundException(
                $"No configuration file found. Expected '{PlaintextFileName}' or '{EncryptedFileName}' in {baseDir}. " +
                $"Copy appsettings.json to the output directory for development, or run the encryption tool for production.");
        }

        // Decrypt appsettings.enc and deserialize
        private static AppConfig LoadEncryptedConfig(string encryptedPath)
        {
            byte[] encryptedData = File.ReadAllBytes(encryptedPath);

            if (encryptedData.Length < SaltSize + IvSize + 1)
                throw new InvalidOperationException($"{EncryptedFileName} is too small to contain valid encrypted data.");

            // Extract salt, IV, and ciphertext
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            Array.Copy(encryptedData, 0, salt, 0, SaltSize);
            Array.Copy(encryptedData, SaltSize, iv, 0, IvSize);

            int ciphertextLength = encryptedData.Length - SaltSize - IvSize;
            byte[] ciphertext = new byte[ciphertextLength];
            Array.Copy(encryptedData, SaltSize + IvSize, ciphertext, 0, ciphertextLength);

            // Derive key from passphrase + salt
            using var keyDerivation = new Rfc2898DeriveBytes(
                Passphrase, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
            byte[] key = keyDerivation.GetBytes(KeySize);

            // Decrypt
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] decryptedBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            string json = Encoding.UTF8.GetString(decryptedBytes);

            return JsonSerializer.Deserialize<AppConfig>(json)
                ?? throw new InvalidOperationException($"Failed to deserialize decrypted {EncryptedFileName}");
        }

        // Encrypt a plaintext JSON file to the encrypted format.
        // Called by the publish script (via PowerShell) or can be invoked for testing.
        public static void EncryptConfigFile(string inputPath, string outputPath)
        {
            string json = File.ReadAllText(inputPath);
            byte[] plaintext = Encoding.UTF8.GetBytes(json);

            // Generate random salt and IV
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] iv = RandomNumberGenerator.GetBytes(IvSize);

            // Derive key from passphrase + salt
            using var keyDerivation = new Rfc2898DeriveBytes(
                Passphrase, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
            byte[] key = keyDerivation.GetBytes(KeySize);

            // Encrypt
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

            // Write [salt][iv][ciphertext]
            using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            output.Write(salt);
            output.Write(iv);
            output.Write(ciphertext);
        }
    }
}
