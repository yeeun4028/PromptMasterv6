using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

// ★★★ 核心修复：强制指定 Binding 为 WPF 版本，彻底解决歧义 ★★★
using Binding = System.Windows.Data.Binding;

#pragma warning disable IDE0130
namespace PromptMasterv5.Models
#pragma warning restore IDE0130
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
            // 因为上面定义了别名，这里的 Binding 明确指向 System.Windows.Data.Binding
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