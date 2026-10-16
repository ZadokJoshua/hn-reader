using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HNReader.Avalonia.Converters;

/// <summary>
/// Converts string URLs to Uri objects. Handles null and empty strings gracefully by
/// returning null.
/// </summary>
public class StringToUriConverter : IValueConverter
{
    public static readonly StringToUriConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string urlString && !string.IsNullOrWhiteSpace(urlString))
        {
            try
            {
                return new Uri(urlString);
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Uri uri)
        {
            return uri.ToString();
        }

        return string.Empty;
    }
}
