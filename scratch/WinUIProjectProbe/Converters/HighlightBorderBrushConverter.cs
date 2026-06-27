using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace HNReader.WinUI.Converters;

/// <summary>
/// Converts a boolean IsHighlighted state to a border brush.
/// Highlighted comments get a thicker accent border for visual emphasis.
/// </summary>
public partial class HighlightBorderBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush HighlightBrush = new(ColorHelper.FromArgb(255, 255, 102, 0)); // #FF6600
    private static readonly SolidColorBrush DefaultBrush =  new(ColorHelper.FromArgb(40, 0, 0, 0));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isHighlighted && isHighlighted)
        {
            return HighlightBrush;
        }
        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
