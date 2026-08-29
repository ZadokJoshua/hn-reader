using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HNReader.Avalonia.Converters;

/// <summary>
/// Maps a bool to 1.0/0.0 so Opacity can be driven by the same condition an
/// IsVisible binding uses elsewhere, letting a paired Transitions block fade
/// the element in/out instead of popping via IsVisible's hard collapse. Pass
/// ConverterParameter=Inverse to negate.
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isOpaque = value is bool b && b;

        var isInverse = parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
        if (isInverse) isOpaque = !isOpaque;

        return isOpaque ? 1.0 : 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
