using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

// 确保命名空间是 PromptMasterv5，这样 XAML 中的 local: 才能找到它
namespace PromptMasterv5
{
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}