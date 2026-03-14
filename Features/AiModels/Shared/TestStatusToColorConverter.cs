using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PromptMasterv6.Features.AiModels.Shared;

public class ConnectionTestStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            ConnectionTestStatus.Success => System.Windows.Media.Brushes.Green,
            ConnectionTestStatus.Failed => System.Windows.Media.Brushes.Red,
            ConnectionTestStatus.Testing => System.Windows.Media.Brushes.Orange,
            _ => System.Windows.Media.Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TranslationTestStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TranslationTestStatus.FullSuccess => System.Windows.Media.Brushes.Green,
            TranslationTestStatus.PartialSuccess => System.Windows.Media.Brushes.Orange,
            TranslationTestStatus.Failed => System.Windows.Media.Brushes.Red,
            TranslationTestStatus.Testing => System.Windows.Media.Brushes.Orange,
            _ => System.Windows.Media.Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
