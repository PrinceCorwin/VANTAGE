using System;

namespace VANTAGE.Utilities
{
    public static class UserHelper
    {
        public static string GetCurrentWindowsUsername()
        {
            var u = Environment.UserName;
            return string.IsNullOrWhiteSpace(u) ? "UnknownUser" : u;
        }
    }
}
