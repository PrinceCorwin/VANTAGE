using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;

namespace VANTAGE.Utilities
{
    // Manages Azure SQL database connections for the Central database
    // Replaces the SQLite Central database on Google Drive
    public static class AzureDbManager
    {
        // Connection string built from Credentials.cs (gitignored)
        private static readonly string _connectionString = BuildConnectionString();

        private static string BuildConnectionString()
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = Credentials.AzureServer,
                InitialCatalog = Credentials.AzureDatabase,
                UserID = Credentials.AzureUserId,
                Password = Credentials.AzurePassword,
                Encrypt = true,
                TrustServerCertificate = false,
                ConnectTimeout = 30,
                MultipleActiveResultSets = false,
                PersistSecurityInfo = false
            };

            return builder.ConnectionString;
        }
        // Check if user is admin by querying Azure Admins table
        public static bool IsUserAdmin(string username)
        {
            try
            {
                if (string.IsNullOrEmpty(username))
                    return false;

                if (!IsNetworkAvailable())
                    return false;

                using var connection = GetConnection();
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Admins WHERE Username = @username";
                cmd.Parameters.AddWithValue("@username", username);

                int count = Convert.ToInt32(cmd.ExecuteScalar());
                return count > 0;
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "AzureDbManager.IsUserAdmin");
                return false;
            }
        }
        // Get the Azure connection string
        public static string ConnectionString => _connectionString;

        // Create and return a new SqlConnection (caller must open and dispose)
        public static SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        // Check if Azure database is accessible
        // Returns true if connection succeeds, false otherwise
        // errorMessage contains details on failure
        public static bool CheckConnection(out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                // First check network availability
                if (!IsNetworkAvailable())
                {
                    errorMessage = "No network connection detected.\n\nPlease check your internet connection and try again.";
                    return false;
                }

                using var connection = new SqlConnection(_connectionString);
                connection.Open();

                // Verify we can query the database
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT 1";
                cmd.ExecuteScalar();

                // Verify Activities table exists
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Activities'";
                var tableExists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;

                if (!tableExists)
                {
                    errorMessage = "Azure database is missing the Activities table.\n\nPlease contact your administrator.";
                    return false;
                }

                return true;
            }
            catch (SqlException sqlEx)
            {
                // Provide user-friendly messages for common SQL errors
                switch (sqlEx.Number)
                {
                    case -2:
                        errorMessage = "Connection to Azure timed out.\n\nThe server may be busy or your network connection is slow.";
                        break;
                    case 53:
                    case 40:
                        errorMessage = "Cannot reach Azure server.\n\nPlease check your internet connection.";
                        break;
                    case 18456:
                        errorMessage = "Azure authentication failed.\n\nPlease contact your administrator.";
                        break;
                    case 4060:
                        errorMessage = "Cannot access the MILESTONE database.\n\nPlease contact your administrator.";
                        break;
                    default:
                        errorMessage = $"Azure database error ({sqlEx.Number}):\n\n{sqlEx.Message}";
                        break;
                }

                AppLogger.Error(sqlEx, "AzureDbManager.CheckConnection");
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Cannot connect to Azure database:\n\n{ex.Message}";
                AppLogger.Error(ex, "AzureDbManager.CheckConnection");
                return false;
            }
        }

        // Async version of CheckConnection for UI responsiveness
        public static async Task<(bool Success, string ErrorMessage)> CheckConnectionAsync()
        {
            return await Task.Run(() =>
            {
                bool success = CheckConnection(out string errorMessage);
                return (success, errorMessage);
            });
        }

        // Check if network connectivity is available
        private static bool IsNetworkAvailable()
        {
            try
            {
                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                {
                    return false;
                }

                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

                foreach (var ni in interfaces)
                {
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        var ipProps = ni.GetIPProperties();

                        foreach (var addr in ipProps.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                byte[] bytes = addr.Address.GetAddressBytes();
                                if (!(bytes[0] == 169 && bytes[1] == 254))
                                {
                                    return true;
                                }
                            }
                            else if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                            {
                                if (!addr.Address.IsIPv6LinkLocal)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // Get the next SyncVersion from GlobalSyncVersion table (atomic increment)
        // This replaces the SQLite trigger approach for thread-safe version assignment
        public static long GetNextSyncVersion()
        {
            using var connection = GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    UPDATE GlobalSyncVersion SET CurrentVersion = CurrentVersion + 1;
                    SELECT CurrentVersion FROM GlobalSyncVersion;";

                long newVersion = Convert.ToInt64(cmd.ExecuteScalar());

                transaction.Commit();

                return newVersion;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // Get the current max SyncVersion without incrementing (for pull operations)
        public static long GetCurrentSyncVersion()
        {
            using var connection = GetConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT CurrentVersion FROM GlobalSyncVersion";

            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt64(result) : 0;
        }
    }
}