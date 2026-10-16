using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HNReader.Avalonia.Converters;

/// <summary>
/// Avalonia controls use IsVisible:bool directly (no Visibility enum), so this is a
/// bool-to-bool passthrough kept for XAML/behavioral parity with the WinUI converter of
/// the same name. Truthiness rules mirror the WinUI version: non-empty strings, non-zero
/// numbers, and non-null references count as "visible". Pass ConverterParameter=Inverse
/// to negate.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isVisible;

        switch (value)
        {
            case bool boolValue:
                isVisible = boolValue;
                break;
            case string str:
                isVisible = !string.IsNullOrWhiteSpace(str);
                break;
            case int intValue:
                isVisible = intValue != 0;
                break;
            case long longValue:
                isVisible = longValue != 0L;
                break;
            case double doubleValue:
                isVisible = Math.Abs(doubleValue) > double.Epsilon;
                break;
            case decimal decimalValue:
                isVisible = decimalValue != 0m;
                break;
            default:
                isVisible = value != null;
                break;
        }

        var isInverse = parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
        if (isInverse) isVisible = !isVisible;

        return isVisible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
