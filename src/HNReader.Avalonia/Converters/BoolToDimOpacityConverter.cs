using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HNReader.Avalonia.Converters;

/// <summary>
/// Maps a bool to full or reduced opacity, for an element that should read as
/// de-emphasised rather than disappear.
///
/// <para>
/// Distinct from <see cref="BoolToOpacityConverter"/>, which returns 0.0 for
/// false — that one exists to fade an element fully out in place of IsVisible's
/// hard collapse. Reusing it to "dim" something makes it invisible while still
/// occupying space and taking clicks, which is worse than either intent.
/// </para>
///
/// <para>Pass ConverterParameter=Inverse to negate.</para>
/// </summary>
public class BoolToDimOpacityConverter : IValueConverter
{
    public static readonly BoolToDimOpacityConverter Instance = new();

    /// <summary>Low enough to read as inactive, high enough to stay legible.</summary>
    private const double DimOpacity = 0.45;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isFull = value is bool b && b;

        var isInverse = parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
        if (isInverse) isFull = !isFull;

        return isFull ? 1.0 : DimOpacity;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
