using System;
using System.Collections.Generic;

namespace HNReader.Core.Helpers;

/// <summary>
/// A deliberately small, hand-maintained substitute for the full Public Suffix List.
///
/// Favicons are looked up per host, and the full host is always tried first, so these
/// rules only ever affect the *last* rung of the fallback chain. An entry missing here
/// costs at most one wasted request against a domain that has no icon of its own - never
/// a wrong icon. That payoff does not justify taking on a ~250 KB PSL data file plus a
/// refresh story in a project with a handful of dependencies, so this stays curated.
/// </summary>
public static class PublicSuffixRules
{
    /// <summary>
    /// Multi-label ICANN suffixes, i.e. the ones where the naive "last two labels" rule
    /// produces a suffix rather than a real domain (<c>bbc.co.uk</c> -> <c>co.uk</c>).
    /// Used to walk one label further left when finding the registrable domain.
    /// </summary>
    private static readonly HashSet<string> MultiLabelSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        // UK
        "co.uk", "org.uk", "ac.uk", "gov.uk", "me.uk", "net.uk", "sch.uk", "ltd.uk", "plc.uk",
        // Australia / NZ
        "com.au", "net.au", "org.au", "edu.au", "gov.au", "id.au", "asn.au",
        "co.nz", "net.nz", "org.nz", "ac.nz", "govt.nz", "geek.nz", "school.nz",
        // Japan / Korea / China / HK / TW / SG
        "co.jp", "ne.jp", "or.jp", "ac.jp", "go.jp", "ad.jp", "ed.jp", "gr.jp", "lg.jp",
        "co.kr", "or.kr", "ne.kr", "re.kr", "pe.kr", "go.kr", "ac.kr",
        "com.cn", "net.cn", "org.cn", "gov.cn", "edu.cn", "ac.cn",
        "com.hk", "org.hk", "net.hk", "edu.hk", "gov.hk", "idv.hk",
        "com.tw", "org.tw", "net.tw", "edu.tw", "gov.tw", "idv.tw",
        "com.sg", "net.sg", "org.sg", "edu.sg", "gov.sg", "per.sg",
        // India / Israel / Turkey / South Africa
        "co.in", "net.in", "org.in", "firm.in", "gen.in", "ind.in", "ac.in", "edu.in", "gov.in", "res.in",
        "co.il", "org.il", "net.il", "ac.il", "gov.il", "muni.il", "k12.il",
        "com.tr", "net.tr", "org.tr", "edu.tr", "gov.tr", "bel.tr", "web.tr",
        "co.za", "org.za", "net.za", "web.za", "gov.za", "ac.za",
        // Latin America
        "com.br", "net.br", "org.br", "gov.br", "edu.br", "art.br", "blog.br",
        "com.ar", "net.ar", "org.ar", "gob.ar", "edu.ar",
        "com.mx", "net.mx", "org.mx", "gob.mx", "edu.mx",
        "com.co", "net.co", "org.co", "gov.co", "edu.co",
        // Europe / other
        "com.ua", "net.ua", "org.ua", "gov.ua", "in.ua", "kiev.ua",
        "com.pl", "net.pl", "org.pl", "gov.pl", "edu.pl",
        "com.ru", "net.ru", "org.ru", "spb.ru", "msk.ru",
        "com.es", "org.es", "nom.es", "gob.es", "edu.es",
        "co.id", "web.id", "or.id", "ac.id", "go.id", "sch.id",
        "com.ph", "net.ph", "org.ph", "gov.ph", "edu.ph",
        "com.my", "net.my", "org.my", "gov.my", "edu.my",
        "com.vn", "net.vn", "org.vn", "gov.vn", "edu.vn",
    };

    /// <summary>
    /// Platform suffixes that hand every customer their own sub-domain. For these, the
    /// apex is a bare hosting suffix with no icon worth showing, so the apex rung is
    /// suppressed entirely rather than attempted.
    ///
    /// Note what is deliberately absent: <c>substack.com</c>, <c>medium.com</c>,
    /// <c>tumblr.com</c> and friends. Those apexes serve a real, recognisable platform
    /// icon, so falling back to it is a reasonable answer when a publication has set
    /// no icon of its own.
    /// </summary>
    private static readonly HashSet<string> BarePlatformSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.io", "gitlab.io", "githubusercontent.com",
        "pages.dev", "workers.dev", "r2.dev",
        "netlify.app", "vercel.app", "now.sh",
        "herokuapp.com", "azurewebsites.net", "firebaseapp.com", "web.app",
        "appspot.com", "cloudfront.net", "amazonaws.com",
        "readthedocs.io", "surge.sh", "neocities.org",
        "bearblog.dev", "ghost.io", "notion.site", "gitbook.io",
        "wixsite.com", "weebly.com", "squarespace.com",
        "sourceforge.net", "bitbucket.io",
    };

    /// <summary>
    /// True when <paramref name="candidate"/> is a known multi-label ICANN suffix such as
    /// <c>co.uk</c> - i.e. a string that looks like a domain but cannot be registered.
    /// </summary>
    public static bool IsMultiLabelSuffix(string candidate) =>
        !string.IsNullOrEmpty(candidate) && MultiLabelSuffixes.Contains(candidate);

    /// <summary>
    /// True when <paramref name="candidate"/> is a bare hosting platform suffix whose apex
    /// has no icon worth falling back to.
    /// </summary>
    public static bool IsBarePlatformSuffix(string candidate) =>
        !string.IsNullOrEmpty(candidate) && BarePlatformSuffixes.Contains(candidate);
}
