using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PromptMasterv5.Infrastructure.Converters
{
    public class StringToGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string pathString && !string.IsNullOrWhiteSpace(pathString))
            {
                try
                {
                    return Geometry.Parse(pathString);
                }
                catch 
                {
                    return Geometry.Empty;
                }
            }
            return Geometry.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
