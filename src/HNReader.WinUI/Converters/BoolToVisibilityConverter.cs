using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace HNReader.WinUI.Converters;

public partial class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isVisible;

        if (value is bool boolValue)
        {
            isVisible = boolValue;
        }
        else if (value is string str)
        {
            isVisible = !string.IsNullOrWhiteSpace(str);
        }
        else if (value is int intValue)
        {
            isVisible = intValue != 0;
        }
        else if (value is long longValue)
        {
            isVisible = longValue != 0L;
        }
        else if (value is double doubleValue)
        {
            isVisible = Math.Abs(doubleValue) > double.Epsilon;
        }
        else if (value is decimal decimalValue)
        {
            isVisible = decimalValue != 0m;
        }
        else
        {
            // For other reference types, consider non-null as visible
            isVisible = value != null;
        }

        var isInverse = parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase);

        if (isInverse)
        {
            isVisible = !isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
