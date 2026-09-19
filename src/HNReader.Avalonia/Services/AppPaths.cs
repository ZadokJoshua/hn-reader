using System;
using System.IO;
using HNReader.Core.Constants;

namespace HNReader.Avalonia.Services;

/// <summary>
/// Cross-platform replacement for WinUI's ApplicationData.Current.LocalFolder.Path.
/// Resolves an "HNReader" folder under the OS-appropriate application-data directory
/// (e.g. %AppData%\HNReader on Windows, ~/.config/HNReader on Linux, ~/Library/Application Support/HNReader on macOS)
/// and ensures it exists.
/// </summary>
public static class AppPaths
{
    public static string LocalFolder { get; } = ResolveLocalFolder();

    public static string SettingsDirectory => LocalFolder;

    public static string FavouritesDbPath => Path.Combine(LocalFolder, "favorites.db");

    public static string LogDirectory => Path.Combine(LocalFolder, "logs");

    /// <summary>Cached favicon bytes, keyed by a hash of the host.</summary>
    public static string FaviconCacheDirectory =>
        Path.Combine(LocalFolder, AppFileNames.FAVICON_CACHE_FOLDER_NAME);

    private static string ResolveLocalFolder()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(baseDir, "HNReader");
        Directory.CreateDirectory(folder);
        return folder;
    }
}
