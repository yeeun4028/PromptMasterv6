using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PromptMasterv6.Features.Settings.Sync;

public class RestoreStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            RestoreStatusType.Success => System.Windows.Media.Brushes.Green,
            RestoreStatusType.Failed => System.Windows.Media.Brushes.Red,
            RestoreStatusType.InProgress => System.Windows.Media.Brushes.Orange,
            _ => System.Windows.Media.Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
