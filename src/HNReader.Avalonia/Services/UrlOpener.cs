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
        // Only plain web links reach the shell: launching with UseShellExecute would
        // otherwise run file: paths and custom protocol handlers found in comments.
        if (!HNReader.Core.Helpers.LinkHelper.TryGetSafeWebUri(url, out var uri)) return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
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
}
