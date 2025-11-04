using System;
using System.Security.Cryptography;
using System.Text;

namespace VANTAGE.Utilities
{
    public static class AdminHelper
    {
        private const string SECRET_SALT = "V@nt@ge$ecr3t!2025#ProjectControls";

        
        /// Generate admin token hash for verification
        
        public static string GenerateAdminToken(int userId, string username)
        {
            string input = $"{userId}|{username}|{SECRET_SALT}";
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }

        
        /// Verify if user's admin token is valid
        
        public static bool VerifyAdminToken(int userId, string username, string storedToken)
        {
            if (string.IsNullOrEmpty(storedToken))
                return false;

            string expectedToken = GenerateAdminToken(userId, username);
            return storedToken == expectedToken;
        }

        
        /// Grant admin privileges to a user (with token)
        
        public static void GrantAdmin(int userId, string username)
        {
            try
            {
                string token = GenerateAdminToken(userId, username);

                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE Users 
                    SET IsAdmin = 1, AdminToken = @token 
                    WHERE UserID = @userId";
                command.Parameters.AddWithValue("@token", token);
                command.Parameters.AddWithValue("@userId", userId);

                command.ExecuteNonQuery();
                // TODO: log that user granted admin
            }
            catch (Exception ex)
            {
                // TODO: Add proper logging (e.g., to file or central log)
                throw;
            }
        }

        
        /// Revoke admin privileges from a user
        
        public static void RevokeAdmin(int userId)
        {
            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE Users 
                    SET IsAdmin = 0, AdminToken = NULL 
                    WHERE UserID = @userId";
                command.Parameters.AddWithValue("@userId", userId);

                command.ExecuteNonQuery();
                System.Diagnostics.Debug.WriteLine($"✓ Admin revoked from user {userId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error revoking admin: {ex.Message}");
            }
        }
    }
}