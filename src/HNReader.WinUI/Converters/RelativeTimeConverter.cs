using Microsoft.UI.Xaml.Data;
using System;

namespace HNReader.WinUI.Converters;

/// <summary>
/// Converts a DateTime value to a human-readable relative time string (e.g., "2h ago", "3d ago").
/// </summary>
public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DateTime dateTime || dateTime == DateTime.MinValue)
        {
            return string.Empty;
        }

        // Ensure we're comparing in the same timezone
        var now = DateTime.UtcNow;
        var dateTimeUtc = dateTime.Kind == DateTimeKind.Utc ? dateTime : dateTime.ToUniversalTime();
        var elapsed = now - dateTimeUtc;

        if (elapsed.TotalSeconds < 0)
        {
            return "just now";
        }

        if (elapsed.TotalMinutes < 1)
        {
            return "just now";
        }

        if (elapsed.TotalMinutes < 60)
        {
            var minutes = (int)elapsed.TotalMinutes;
            return $"{minutes}m ago";
        }

        if (elapsed.TotalHours < 24)
        {
            var hours = (int)elapsed.TotalHours;
            return $"{hours}h ago";
        }

        if (elapsed.TotalDays < 7)
        {
            var days = (int)elapsed.TotalDays;
            return $"{days}d ago";
        }

        if (elapsed.TotalDays < 30)
        {
            var weeks = (int)(elapsed.TotalDays / 7);
            return weeks == 1 ? "1 week ago" : $"{weeks} weeks ago";
        }

        if (elapsed.TotalDays < 365)
        {
            var months = (int)(elapsed.TotalDays / 30);
            return months == 1 ? "1 month ago" : $"{months} months ago";
        }

        var years = (int)(elapsed.TotalDays / 365);
        return years == 1 ? "1 year ago" : $"{years} years ago";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
