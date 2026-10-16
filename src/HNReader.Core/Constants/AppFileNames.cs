namespace HNReader.Core.Constants;

public static class AppFileNames
{
    public const string SETTINGS_FILE_NAME = "settings.json";

    public const string LOG_FOLDER_NAME = "logs";
    public const string LOG_FILE_PREFIX = "hnreader-";
    public const string LOG_FILE_EXTENSION = ".log";

    // 7 days is generous for diagnosing a problem after the fact and short enough to
    // keep the app-data folder tidy. Not user-configurable in v1.
    public static readonly TimeSpan LOG_TTL = TimeSpan.FromDays(7);

    public const long LOG_MAX_FILE_BYTES = 2L * 1024L * 1024L; // 2 MB
    public const int LOG_MAX_FILES = 10;

    // ── Favicons ─────────────────────────────────────────────────────────
    //
    // Icons are fetched per host through a fallback chain. Google's s2 endpoint is
    // deliberately LAST: it answers 200 with a generic globe when it has no entry,
    // which is indistinguishable from a hit, so putting it first would stop the
    // chain before the site's own /favicon.ico is ever tried.

    public const string FAVICON_CACHE_FOLDER_NAME = "favicons";

    public const string FAVICON_DUCKDUCKGO_URL_FORMAT = "https://icons.duckduckgo.com/ip3/{0}.ico";
    public const string FAVICON_DIRECT_URL_FORMAT = "https://{0}/favicon.ico";
    public const string FAVICON_GOOGLE_URL_FORMAT = "https://www.google.com/s2/favicons?sz=32&domain={0}";

    // A favicon is a handful of KB. Anything larger is a mislabelled page or an
    // asset we have no business decoding into a 14px slot.
    public const long FAVICON_MAX_BYTES = 256L * 1024L;

    public const int FAVICON_RUNG_TIMEOUT_SECONDS = 3;
    public const int FAVICON_CHAIN_TIMEOUT_SECONDS = 8;
    public const int FAVICON_MAX_REDIRECTS = 3;

    // Positive results are effectively permanent; sites change icons rarely.
    // Negative results expire far sooner so a site that adds an icon, or a lookup
    // that failed only because the network was down, recovers without a reinstall.
    public static readonly TimeSpan FAVICON_POSITIVE_TTL = TimeSpan.FromDays(7);
    public static readonly TimeSpan FAVICON_NEGATIVE_TTL = TimeSpan.FromHours(6);
    public static readonly TimeSpan FAVICON_DISK_TTL = TimeSpan.FromDays(30);

    public const int FAVICON_MEMORY_CACHE_SIZE = 512;
    public const int FAVICON_BITMAP_CACHE_SIZE = 256;
}
