using System;
using System.Diagnostics;

namespace HNReader.Avalonia.Services;

/// <summary>
/// Cross-platform "open in default handler" helper, replacing WinUI's Launcher.LaunchUriAsync.
/// </summary>
public static class UrlOpener
{
    public static bool TryOpen(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UrlOpener failed to open '{url}': {ex.Message}");
            return false;
        }
    }

    public static bool TryOpenFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UrlOpener failed to open folder '{folder}': {ex.Message}");
            return false;
        }
    }
}
