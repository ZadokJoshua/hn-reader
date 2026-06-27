using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace HNReader.WinUI.Converters;

public sealed class UriToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Uri uri)
        {
            return new BitmapImage(uri);
        }

        if (value is string str && Uri.TryCreate(str, UriKind.Absolute, out var parsedUri))
        {
            return new BitmapImage(parsedUri);
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
