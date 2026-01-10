using System;

namespace VANTAGE.Utilities
{
    // Centralized numeric precision helper for consistent rounding across the application
    public static class NumericHelper
    {
        public const int DefaultDecimalPlaces = 3;

        // Rounds a value to the specified decimal places using AwayFromZero rounding
        public static double RoundToPlaces(double value, int decimals = DefaultDecimalPlaces)
        {
            return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        }
    }
}
