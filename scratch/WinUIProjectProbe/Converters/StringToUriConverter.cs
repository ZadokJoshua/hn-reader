using Microsoft.UI.Xaml.Data;
using System;

namespace HNReader.WinUI.Converters;

/// <summary>
/// Converts string URLs to Uri objects for use with HyperlinkButton and other Uri-dependent controls.
/// Handles null and empty strings gracefully by returning null.
/// </summary>
public class StringToUriConverter : IValueConverter
{
    /// <summary>
    /// Converts a string URL to a Uri object.
    /// Returns null if the string is null, empty, or whitespace.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string urlString && !string.IsNullOrWhiteSpace(urlString))
        {
            try
            {
                return new Uri(urlString);
            }
            catch (UriFormatException)
            {
                // Invalid URI format - return null to disable the hyperlink
                return null!;
            }
        }

        // Return null for empty/null strings - HyperlinkButton handles this gracefully
        return null!;
    }

    /// <summary>
    /// Converts a Uri object back to a string URL.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Uri uri)
        {
            return uri.ToString();
        }

        return string.Empty;
    }
}
