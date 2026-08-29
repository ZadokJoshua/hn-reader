using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HNReader.Avalonia.Converters;

/// <summary>
/// Converts a boolean IsHighlighted state to a border brush. Highlighted comments get the
/// brand accent border for visual emphasis.
/// </summary>
public class HighlightBorderBrushConverter : IValueConverter
{
    public static readonly HighlightBorderBrushConverter Instance = new();

    private static readonly SolidColorBrush HighlightBrush = new(Color.FromArgb(255, 255, 102, 0)); // #FF6600
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromArgb(40, 0, 0, 0));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isHighlighted && isHighlighted)
        {
            return HighlightBrush;
        }
        return DefaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
