using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PromptMasterv6.Infrastructure.Converters
{
    public class StringToGeometryConverter : IValueConverter
    {
        private static readonly string[] ValidPrefixes = { "M", "F", "L", "H", "V", "C", "S", "Q", "T", "A", "Z", "E" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string pathString || string.IsNullOrWhiteSpace(pathString))
            {
                return DependencyProperty.UnsetValue;
            }

            pathString = pathString.Trim();
            
            if (!IsValidGeometryPrefix(pathString))
            {
                return DependencyProperty.UnsetValue;
            }

            try
            {
                return Geometry.Parse(pathString);
            }
            catch (FormatException)
            {
                return DependencyProperty.UnsetValue;
            }
            catch (InvalidOperationException)
            {
                return DependencyProperty.UnsetValue;
            }
        }

        private static bool IsValidGeometryPrefix(string pathString)
        {
            if (string.IsNullOrWhiteSpace(pathString) || pathString.Length < 1)
            {
                return false;
            }

            string upperPath = pathString.Trim().ToUpperInvariant();
            
            foreach (var prefix in ValidPrefixes)
            {
                if (upperPath.StartsWith(prefix))
                {
                    return true;
                }
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
