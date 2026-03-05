using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace HNReader.WinUI.Converters;

/// <summary>
/// Converts a boolean IsHighlighted state to a border thickness.
/// Highlighted comments get a thicker border (2px) for visual emphasis.
/// Non-highlighted comments use the default 1px border from the style.
/// Used for the AI insight scroll-to-comment feature.
/// </summary>
public partial class HighlightBorderThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isHighlighted && isHighlighted)
        {
            return new Thickness(2);
        }
        return new Thickness(1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
