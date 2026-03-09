using System;
using System.Globalization;
using System.Windows.Data;

namespace PromptMasterv6.Infrastructure.Converters
{
    public class ApiKeyMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string s || string.IsNullOrEmpty(s))
                return string.Empty;

            if (s.Length <= 4)
                return s;

            var visiblePart = s.Substring(s.Length - 4);
            var maskPart = new string('*', s.Length - 4);
            
            return maskPart + visiblePart;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("ApiKeyMaskConverter is one-way only.");
        }
    }
}
