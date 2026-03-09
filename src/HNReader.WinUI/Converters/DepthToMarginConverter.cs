using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace HNReader.WinUI.Converters;

/// <summary>
/// Converts comment depth to appropriate margin to create visual gaps between root-level comment threads.
/// Root comments (Depth == 0) get bottom margin so the first thread does not create a top gap.
/// </summary>
public partial class DepthToMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int depth)
        {
            // Keep the first root comment flush to the top while still separating root threads.
            if (depth == 0)
            {
                return new Thickness(0, 0, 0, 10);
            }
            // Nested comments have minimal spacing
            return new Thickness(0, 0, 0, 0);
        }
        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
