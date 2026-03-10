using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PromptMasterv6.Infrastructure.Converters;

public class LocalImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();

            bitmap.CacheOption = BitmapCacheOption.OnLoad;

            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

            bitmap.UriSource = new Uri(path, UriKind.Absolute);

            bitmap.DecodePixelWidth = 80;

            bitmap.EndInit();

            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
