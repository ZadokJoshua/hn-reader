using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace HNReader.WinUI.Converters;

public partial class DepthToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int depth)
        {
            return depth > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
