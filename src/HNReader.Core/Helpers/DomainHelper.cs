using System;
using System.Collections.Generic;

namespace HNReader.Core.Helpers;

/// <summary>
/// Host parsing for story URLs.
///
/// Deliberately separate from <see cref="CoreHelper"/>, which is scoped to JSON
/// serialization defaults.
///
/// The distinction that matters here: a favicon belongs to a *host*, not to a
/// registrable domain. Truncating <c>simonw.github.io</c> to <c>github.io</c> or
/// <c>www.bbc.co.uk</c> to <c>co.uk</c> asks for an icon that either does not exist or
/// belongs to somebody else. So lookups key on the full host and only fall back to the
/// apex afterwards, and the string shown to the user is tracked separately from the
/// string used to fetch.
/// </summary>
public static class DomainHelper
{
    /// <summary>
    /// The host of <paramref name="url"/>, lowercased, or <c>null</c> when the URL is
    /// absent or unparseable. Self-posts (Ask HN and friends) carry no URL and land here
    /// as <c>null</c>, which is what suppresses their favicon slot entirely.
    /// </summary>
    public static string? GetHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;

            // Reject non-web schemes outright - mailto:, javascript: and friends have no
            // host worth showing and must never reach the fetcher.
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

            var host = uri.Host;
            return string.IsNullOrWhiteSpace(host) ? null : host.ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// The host to ask for a favicon: the full host minus a leading <c>www.</c>, which is
    /// near-universally an alias for the same site and only splits the cache.
    /// </summary>
    public static string? GetFaviconHost(string? url)
    {
        var host = GetHost(url);
        if (host is null) return null;

        const string www = "www.";
        if (host.StartsWith(www, StringComparison.Ordinal) && host.Length > www.Length)
            host = host.Substring(www.Length);

        return host;
    }

    /// <summary>
    /// The domain shown under a story title. Currently identical to
    /// <see cref="GetFaviconHost"/>, but named separately on purpose: what is displayed
    /// and what is fetched are different contracts, and conflating them is what produced
    /// "co.uk" captions in the first place.
    /// </summary>
    public static string? GetDisplayDomain(string? url) => GetFaviconHost(url);

    /// <summary>
    /// The registrable domain (apex) - <c>bbc.co.uk</c> for <c>www.bbc.co.uk/news</c>,
    /// <c>cloudflare.com</c> for <c>blog.cloudflare.com</c>. Returns <c>null</c> when the
    /// host *is* a public suffix, or has too few labels to reduce.
    /// </summary>
    public static string? GetRegistrableDomain(string? url)
    {
        var host = GetFaviconHost(url);
        if (host is null) return null;

        // An IP literal has no registrable domain; it is its own host.
        if (Uri.CheckHostName(host) is UriHostNameType.IPv4 or UriHostNameType.IPv6) return host;

        var parts = host.Split('.');
        if (parts.Length < 2) return host; // "localhost" and other single-label hosts

        var lastTwo = parts[^2] + "." + parts[^1];

        // "bbc.co.uk" -> last two labels are the suffix itself, so take three.
        if (PublicSuffixRules.IsMultiLabelSuffix(lastTwo))
            return parts.Length >= 3 ? parts[^3] + "." + lastTwo : null;

        return lastTwo;
    }

    /// <summary>
    /// Hosts to try for a favicon, most specific first and de-duplicated. Never null.
    ///
    /// The apex rung is skipped when it is a bare hosting suffix (<c>github.io</c>,
    /// <c>pages.dev</c>) - there is no icon there worth a round trip.
    /// </summary>
    public static IReadOnlyList<string> GetFaviconHostCandidates(string? url)
    {
        var host = GetFaviconHost(url);
        if (host is null) return Array.Empty<string>();

        var candidates = new List<string>(2) { host };

        var apex = GetRegistrableDomain(url);
        if (!string.IsNullOrEmpty(apex) &&
            !string.Equals(apex, host, StringComparison.OrdinalIgnoreCase) &&
            !PublicSuffixRules.IsBarePlatformSuffix(apex))
        {
            candidates.Add(apex);
        }

        return candidates;
    }
}
