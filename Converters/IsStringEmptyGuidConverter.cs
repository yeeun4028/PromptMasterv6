using System;
using System.Globalization;
using System.Windows.Data;

namespace PromptMasterv5.Converters
{
    public class IsStringEmptyGuidConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return true; // Treat null as empty
            
            if (value is string str)
            {
                if (str == Guid.Empty.ToString()) return true;
                if (Guid.TryParse(str, out Guid g))
                {
                    return g == Guid.Empty;
                }
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
