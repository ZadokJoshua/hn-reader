using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HNReader.Avalonia.Converters;

/// <summary>
/// Builds a favicon URL for a story's root domain via Google's public favicon
/// service, so the story list can show a per-site icon — something the WinUI
/// original doesn't have. Returns null for an empty domain so the bound image
/// control just shows nothing rather than a broken-image icon.
/// </summary>
public class RootDomainToFaviconUrlConverter : IValueConverter
{
    public static readonly RootDomainToFaviconUrlConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string domain || string.IsNullOrWhiteSpace(domain)) return null;

        return $"https://www.google.com/s2/favicons?sz=32&domain={Uri.EscapeDataString(domain)}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
