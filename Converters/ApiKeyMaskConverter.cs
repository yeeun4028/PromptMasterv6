using System;
using System.Globalization;
using System.Windows.Data;

namespace PromptMasterv6.Converters
{
    public class ApiKeyMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string s || string.IsNullOrEmpty(s))
                return string.Empty;

            if (s.Length <= 4)
                return s; // 如果长度小于等于4，直接显示（或者也可以全遮罩，这里按显示处理）

            // 按照需求：只显示后四位，前面全部显示*号
            // 例如：sk-1234567890abcdef -> ************cdef
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
