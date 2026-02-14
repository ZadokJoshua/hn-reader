using Microsoft.UI.Xaml.Data;
using System;

namespace HNReader.WinUI.Converters;

public partial class BoolToCollapseTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isCollapsed)
        {
            return isCollapsed ? "[ + ]" : "[ - ]";
        }
        return "[ - ]";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
