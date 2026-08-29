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

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int depth)
        {
            return depth == 0 ? new Thickness(0, 0, 0, 10) : new Thickness(0);
        }
        return new Thickness(0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
