namespace HNReader.Core.Constants;

public static class AppFileNames
{
    public const string SETTINGS_FILE_NAME = "settings.json";

    public const string LOG_FOLDER_NAME = "logs";
    public const string LOG_FILE_PREFIX = "hnreader-";
    public const string LOG_FILE_EXTENSION = ".log";

    // 7 days is generous for a bug-report window and short enough to keep
    // %TEMP% tidy. Not user-configurable in v1.
    public static readonly TimeSpan LOG_TTL = TimeSpan.FromDays(7);

    public const long LOG_MAX_FILE_BYTES = 2L * 1024L * 1024L; // 2 MB
    public const int LOG_MAX_FILES = 10;

    // Microsoft Forms URL for the "Report a bug" link. Empty disables the button.
    public const string BUG_REPORT_URL = "";
}
