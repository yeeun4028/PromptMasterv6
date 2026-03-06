using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PromptMasterv6.Converters
{
    public class HexToSolidColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hexColor && !string.IsNullOrWhiteSpace(hexColor))
            {
                try
                {
                    return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor));
                }
                catch
                {
                    return System.Windows.Media.Brushes.Transparent;
                }
            }
            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Media.SolidColorBrush brush)
            {
                return brush.Color.ToString();
            }
            return "#00000000";
        }
    }
}
