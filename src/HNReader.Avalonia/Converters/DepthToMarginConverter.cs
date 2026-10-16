using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace HNReader.Avalonia.Converters;

/// <summary>
/// Converts comment depth to appropriate margin to create visual gaps between root-level
/// comment threads. Root comments (Depth == 0) get bottom margin so the first thread does
/// not create a top gap.
/// </summary>
public class DepthToMarginConverter : IValueConverter
{
    public static readonly DepthToMarginConverter Instance = new();

    // Shared: this runs once per comment row, and a Thickness is a value type
    // that never changes, so there is nothing to allocate per call.
    private static readonly Thickness RootMargin = new(0, 0, 0, 10);
    private static readonly Thickness NestedMargin = new(0);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int depth)
        {
            return depth == 0 ? RootMargin : NestedMargin;
        }
        return NestedMargin;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
