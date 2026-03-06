using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PromptMasterv6.Infrastructure.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public bool Inverted { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool hasValue = !string.IsNullOrWhiteSpace(value?.ToString());

            if (Inverted)
            {
                return hasValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return hasValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
