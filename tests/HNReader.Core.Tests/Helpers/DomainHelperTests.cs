using HNReader.Core.Helpers;

namespace HNReader.Core.Tests.Helpers;

public class DomainHelperTests
{
    [Theory]
    // The regression this whole change exists for: the old "last two labels" rule
    // turned these into co.uk / github.io / pages.dev and asked for the wrong icon.
    [InlineData("https://www.bbc.co.uk/news/articles/x", "bbc.co.uk")]
    [InlineData("https://simonw.github.io/datasette/", "simonw.github.io")]
    [InlineData("https://astralcodexten.substack.com/p/x", "astralcodexten.substack.com")]
    [InlineData("https://myapp.pages.dev/", "myapp.pages.dev")]
    [InlineData("https://www.news.com.au/story", "news.com.au")]
    [InlineData("https://blog.cloudflare.com/post", "blog.cloudflare.com")]
    // www. is stripped so it does not split the cache from the bare host.
    [InlineData("https://www.example.com/a", "example.com")]
    [InlineData("https://example.com", "example.com")]
    [InlineData("http://LOCALHOST:3000/x", "localhost")]
    [InlineData("https://192.168.1.1/admin", "192.168.1.1")]
    // Uppercase hosts normalise, so they hit one cache entry rather than two.
    [InlineData("https://EXAMPLE.COM/Path", "example.com")]
    public void GetFaviconHost_ReturnsFullHost(string url, string expected)
        => Assert.Equal(expected, DomainHelper.GetFaviconHost(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    // Non-web schemes must never reach the fetcher.
    [InlineData("mailto:someone@example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://files.example.com/x")]
    public void GetFaviconHost_ReturnsNull_ForUnusableUrls(string? url)
        => Assert.Null(DomainHelper.GetFaviconHost(url));

    [Theory]
    [InlineData("https://www.bbc.co.uk/news", "bbc.co.uk")]
    [InlineData("https://blog.cloudflare.com/post", "cloudflare.com")]
    [InlineData("https://example.com/a", "example.com")]
    [InlineData("https://www.news.com.au/story", "news.com.au")]
    [InlineData("https://sub.deep.example.co.jp/x", "example.co.jp")]
    [InlineData("http://localhost:3000/x", "localhost")]
    [InlineData("https://192.168.1.1/admin", "192.168.1.1")]
    public void GetRegistrableDomain_WalksPastMultiLabelSuffixes(string url, string expected)
        => Assert.Equal(expected, DomainHelper.GetRegistrableDomain(url));

    [Fact]
    public void GetRegistrableDomain_ReturnsNull_WhenHostIsItselfAPublicSuffix()
        => Assert.Null(DomainHelper.GetRegistrableDomain("https://co.uk/"));

    [Theory]
    // Sub-domain sites: try the exact host, then the apex, which for these platforms
    // does carry a real icon worth falling back to.
    [InlineData("https://astralcodexten.substack.com/p/x", "astralcodexten.substack.com", "substack.com")]
    [InlineData("https://blog.cloudflare.com/post", "blog.cloudflare.com", "cloudflare.com")]
    public void GetFaviconHostCandidates_FallsBackToApex(string url, string first, string second)
    {
        var candidates = DomainHelper.GetFaviconHostCandidates(url);
        Assert.Equal(new[] { first, second }, candidates);
    }

    [Theory]
    // No apex rung: it would either duplicate the host or hit a bare hosting suffix
    // that has no icon, so the request is not worth making.
    [InlineData("https://example.com/a", "example.com")]
    [InlineData("https://www.bbc.co.uk/news", "bbc.co.uk")]
    [InlineData("https://simonw.github.io/x", "simonw.github.io")]
    [InlineData("https://myapp.pages.dev/", "myapp.pages.dev")]
    [InlineData("https://thing.vercel.app/", "thing.vercel.app")]
    public void GetFaviconHostCandidates_ReturnsSingleCandidate(string url, string only)
        => Assert.Equal(new[] { only }, DomainHelper.GetFaviconHostCandidates(url));

    [Fact]
    public void GetFaviconHostCandidates_IsEmpty_ForSelfPosts()
        => Assert.Empty(DomainHelper.GetFaviconHostCandidates(null));

    [Fact]
    public void GetDisplayDomain_MatchesFaviconHost()
    {
        const string url = "https://www.bbc.co.uk/news";
        Assert.Equal(DomainHelper.GetFaviconHost(url), DomainHelper.GetDisplayDomain(url));
    }
}
