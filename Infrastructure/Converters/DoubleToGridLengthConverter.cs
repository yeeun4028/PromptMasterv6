using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PromptMasterv6.Infrastructure.Converters
{
    public class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return new GridLength(d);
            }
            return new GridLength(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GridLength gl)
            {
                return gl.Value;
            }
            return 0.0;
        }
    }

    public class NavigationWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is bool isVisible && values[1] is double width)
            {
                return isVisible ? new GridLength(width) : new GridLength(0);
            }
            return new GridLength(0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            if (value is GridLength gl && gl.Value > 0)
            {
                return new object[] { true, gl.Value };
            }
            return new object[] { false, 0.0 };
        }
    }
}
