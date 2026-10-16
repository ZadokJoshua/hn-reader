using System.Net;

namespace HNReader.Core.Helpers;

/// <summary>
/// Decides which URLs the app is willing to hand to the OS shell. Comments are written by
/// strangers, and launching with UseShellExecute will happily run a file: path or a custom
/// protocol handler, so only plain web links are allowed through.
/// </summary>
public static class LinkHelper
{
    /// <summary>
    /// Normalises <paramref name="raw"/> to an absolute http/https URI.
    /// A bare host such as "example.com/page" is treated as https, as HN users often write them.
    /// Returns false for any other scheme (file:, javascript:, mailto:, custom handlers), relative
    /// paths and empty input.
    /// </summary>
    public static bool TryGetSafeWebUri(string? raw, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var candidate = WebUtility.HtmlDecode(raw).Trim();

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed))
        {
            // No scheme at all: accept only something that looks like host[/path].
            if (candidate.Contains(' ') || candidate.StartsWith('/') || candidate.StartsWith("\\", StringComparison.Ordinal)
                || !Uri.TryCreate($"https://{candidate}", UriKind.Absolute, out parsed))
            {
                return false;
            }
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return false;
        if (string.IsNullOrEmpty(parsed.Host)) return false;

        uri = parsed;
        return true;
    }
}
