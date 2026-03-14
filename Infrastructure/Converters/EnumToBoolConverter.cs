using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

using Binding = System.Windows.Data.Binding;

namespace PromptMasterv6.Infrastructure.Converters
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
                if (string.IsNullOrWhiteSpace(targetValue)) return Binding.DoNothing;
                
                try
                {
                    if (!targetType.IsEnum) return Binding.DoNothing;
                    
                    if (Enum.TryParse(targetType, targetValue, true, out var result))
                    {
                        return result;
                    }
                    return Binding.DoNothing;
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
