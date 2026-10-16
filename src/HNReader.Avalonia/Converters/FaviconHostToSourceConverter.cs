using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HNReader.Avalonia.Services;

namespace HNReader.Avalonia.Converters;

/// <summary>
/// Turns a story's favicon host into the sentinel value bound to
/// <c>ImageLoader.Source</c>, which <see cref="FaviconImageLoader"/> recognises.
///
/// This used to build a Google favicon URL directly from a truncated domain. Provider
/// choice, fallbacks and caching now live in the service; all this does is name the host,
/// which keeps network policy out of XAML.
/// </summary>
public class FaviconHostToSourceConverter : IValueConverter
{
    public static readonly FaviconHostToSourceConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string host ? FaviconImageLoader.ToSource(host) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
