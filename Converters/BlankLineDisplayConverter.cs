using System;
using System.Globalization;
using System.Windows.Data;

namespace VANTAGE.Converters
{
    // Converts empty strings to "blank line" and "---" to "line separator" for display in TOC list editor
    public class BlankLineDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str))
                    return "blank line";
                if (str == "---")
                    return "line separator";
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not used - one-way binding
            return value;
        }
    }

    // Returns true if the string is blank or a line separator (for triggering italic/dimmed style)
    public class IsBlankLineConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string str && (string.IsNullOrWhiteSpace(str) || str == "---");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false;
        }
    }
}
