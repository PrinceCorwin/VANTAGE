using System;
using System.Globalization;
using System.Windows.Data;
using VANTAGE.Models;

namespace VANTAGE.Converters
{
    // Converter to display TemplateColumn as "Name (Width%)"
    public class ColumnDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TemplateColumn col)
                return $"{col.Name} ({col.WidthPercent}%)";
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
