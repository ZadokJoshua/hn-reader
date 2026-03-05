using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace HNReader.WinUI.Converters;

/// <summary>
/// Converts a boolean IsHighlighted state to a border brush.
/// When highlighted, returns the HN accent orange (#FF6600) for a prominent indicator.
/// When not highlighted, returns the default divider brush.
/// Used for the AI insight scroll-to-comment feature.
/// </summary>
public partial class HighlightBorderBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush HighlightBrush = new(ColorHelper.FromArgb(255, 0, 255, 159)); // #00FF9F
    private static readonly SolidColorBrush DefaultBrush =  new(ColorHelper.FromArgb(255, 255, 102, 0)); // #FF6600

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
