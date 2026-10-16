using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HNReader.Avalonia.Converters;

/// <summary>
/// Converts a boolean IsHighlighted state to a border brush. Highlighted comments get the
/// brand accent border for visual emphasis; non-highlighted comments fall back to the
/// theme's default card stroke brush set by the CommentCardBorderStyle style class.
/// </summary>
public class HighlightBorderBrushConverter : IValueConverter
{
    public static readonly HighlightBorderBrushConverter Instance = new();

    // Fallback only. The brand colour lives in Styles/Tokens.axaml; this literal
    // exists so the converter still returns something sane if it somehow runs
    // before Application.Resources is available (designer, unit test).
    private static readonly SolidColorBrush FallbackHighlightBrush = new(Color.FromArgb(255, 255, 102, 0)); // #FF6600

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isHighlighted && isHighlighted)
        {
            // Resolved per call rather than cached: the accent brush is a theme
            // resource, so a cached instance would keep whichever variant was
            // active when this converter first ran.
            return ResolveHighlightBrush();
        }
        return AvaloniaProperty.UnsetValue;
    }

    private static IBrush ResolveHighlightBrush()
    {
        var app = Application.Current;
        if (app is not null
            && app.TryGetResource("HnAccentBrush", app.ActualThemeVariant, out var resource)
            && resource is IBrush brush)
        {
            return brush;
        }

        return FallbackHighlightBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
