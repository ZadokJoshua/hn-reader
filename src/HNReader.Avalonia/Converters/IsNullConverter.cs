using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HNReader.Avalonia.Converters;

/// <summary>
/// True when the bound value is null. Used to show the globe placeholder while an
/// <c>Image</c> has no bitmap - that is, both while the favicon is still being fetched and
/// permanently when the site has none.
/// </summary>
public class IsNullConverter : IValueConverter
{
    public static readonly IsNullConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
