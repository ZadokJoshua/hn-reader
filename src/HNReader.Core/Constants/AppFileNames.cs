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

    // GitHub "new issue" URL for the "Report a bug" link. Empty disables the button.
    public const string BUG_REPORT_URL = "https://github.com/ZadokJoshua/hn-reader/issues/new";

    // ── Daily digest ─────────────────────────────────────────────────────
    //
    // The digest is served by our own HNReader.Server, not by Hacker News. It is
    // the only part of the app that talks to a server we run, and it is an
    // opt-in feature (see ISettingsService.IsDigestEnabled) precisely because
    // that server may not be running.

    // Trailing slash is load-bearing: HttpClient.BaseAddress drops everything
    // after the last '/' when resolving a relative request URI.
    // Empty disables the feature, the same way an empty BUG_REPORT_URL disables
    // its button.
    public const string DIGEST_SERVER_BASE_URL = "http://localhost:5282/";

    public const string DIGEST_ENDPOINT = "digest";
    public const string DIGEST_CATEGORIES_ENDPOINT = "digest/categories";
    public const string DIGEST_GENERATE_ENDPOINT = "digest/generate";

    // How often to re-check for a digest while the server is generating one, and
    // how long to keep waiting before giving up and letting the user retry. A run
    // was measured at 4-10 minutes, so the ceiling is generous.
    public static readonly TimeSpan DIGEST_GENERATION_POLL_INTERVAL = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan DIGEST_GENERATION_TIMEOUT = TimeSpan.FromMinutes(15);

    public const int DIGEST_HTTP_TIMEOUT_SECONDS = 20;

    // How long a loaded digest stays good enough to reuse. The digest is
    // regenerated nightly and the server already output-caches it for an hour,
    // so re-fetching on every navigation would be pure waste.
    public static readonly TimeSpan DIGEST_STALE_AFTER = TimeSpan.FromMinutes(30);

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
