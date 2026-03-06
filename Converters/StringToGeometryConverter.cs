using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PromptMasterv6.Converters
{
    /// <summary>
    /// 将 SVG Path Data 字符串转换为 Geometry。
    /// 空字符串或 null 返回 DependencyProperty.UnsetValue，
    /// 从而让绑定的 FallbackValue（默认图标）生效。
    /// </summary>
    public class StringToGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string s || string.IsNullOrWhiteSpace(s))
                return DependencyProperty.UnsetValue; // 触发 FallbackValue

            try
            {
                return Geometry.Parse(s);
            }
            catch
            {
                return DependencyProperty.UnsetValue; // 解析失败也触发 FallbackValue
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Geometry geometry)
                return geometry.ToString();
            return string.Empty;
        }
    }
}
