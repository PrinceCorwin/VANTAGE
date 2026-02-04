using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VANTAGE.Converters
{
    // Converts percentage values to theme-appropriate background colors for Analysis grid
    // Ranges: 0-25% Red, >25-50% Orange, >50-75% Yellow, >75-100% Green
    public class PercentToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = 0;

            if (value is double d)
                percent = d;
            else if (value is int i)
                percent = i;
            else if (value is decimal dec)
                percent = (double)dec;

            // Lookup theme brushes from Application resources
            string resourceKey;
            if (percent <= 25)
                resourceKey = "AnalysisRedBg";
            else if (percent <= 50)
                resourceKey = "AnalysisOrangeBg";
            else if (percent <= 75)
                resourceKey = "AnalysisYellowBg";
            else
                resourceKey = "AnalysisGreenBg";

            var brush = Application.Current.TryFindResource(resourceKey) as Brush;
            return brush ?? Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
