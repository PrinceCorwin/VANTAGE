using System;
using System.Globalization;
using System.Windows.Data;

namespace VANTAGE.Converters
{
    // Converts 0-100 percentage to 0.0-1.0 decimal for ScaleTransform bindings
    public class PercentToDecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = 0;
            if (value is double d)
                percent = d;
            else if (value is int i)
                percent = i;

            return Math.Max(0, Math.Min(1.0, percent / 100.0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
