using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

// ★★★ 核心修复：显式指定 Binding 为 WPF 的 Binding，解决与 WinForms 的冲突 ★★★
using Binding = System.Windows.Data.Binding;

namespace PromptMasterv6.Converters
{
    public class EnumToBoolConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            string checkValue = value.ToString() ?? "";
            string targetValue = parameter.ToString() ?? "";
            return checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Binding.DoNothing;
            if (value is bool useValue && useValue)
            {
                string targetValue = parameter.ToString() ?? "";
                try
                {
                    return Enum.Parse(targetType, targetValue);
                }
                catch
                {
                    return Binding.DoNothing;
                }
            }
            return Binding.DoNothing;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}