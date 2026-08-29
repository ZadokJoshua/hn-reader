using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace HNReader.Avalonia.Converters;

/// <summary>
/// Converts a boolean IsHighlighted state to a border thickness. Highlighted comments get
/// a thicker border (2px) for visual emphasis.
/// </summary>
public class HighlightBorderThicknessConverter : IValueConverter
{
    public static readonly HighlightBorderThicknessConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isHighlighted && isHighlighted)
        {
            return new Thickness(2);
        }
        return new Thickness(1);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
