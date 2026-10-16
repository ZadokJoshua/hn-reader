using HNReader.Core.Constants;

namespace HNReader.Core.Services.Logging;

/// <summary>
/// Resolves the on-disk locations used by the file logger.
/// </summary>
public static class LoggingPaths
{
    /// <summary>
    /// Primary log directory. Resolves to <c>%TEMP%\HNReader\logs\</c>.
    /// Falls back to <c>%LOCALAPPDATA%\HNReader\logs\</c> if <c>%TEMP%</c>
    /// is read-only or otherwise unusable on this machine.
    /// </summary>
    public static string ResolveLogDirectory()
    {
        var primary = Path.Combine(Path.GetTempPath(), "HNReader", AppFileNames.LOG_FOLDER_NAME);
        if (TryEnsureDirectory(primary)) return primary;

        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HNReader",
            AppFileNames.LOG_FOLDER_NAME);
        TryEnsureDirectory(local);
        return local;
    }

    public static string BuildLogFileName(DateTimeOffset utcNow)
        => $"{AppFileNames.LOG_FILE_PREFIX}{utcNow:yyyyMMdd-HHmmss}{AppFileNames.LOG_FILE_EXTENSION}";

    public static string BuildLogFilePath(DateTimeOffset utcNow)
        => Path.Combine(ResolveLogDirectory(), BuildLogFileName(utcNow));

    private static bool TryEnsureDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
