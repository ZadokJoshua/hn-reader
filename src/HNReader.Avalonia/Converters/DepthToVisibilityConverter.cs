using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HNReader.Avalonia.Converters;

public class DepthToVisibilityConverter : IValueConverter
{
    public static readonly DepthToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int depth)
        {
            return depth > 0;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
