using HNReader.Core.Helpers;

namespace HNReader.Core.Tests.Helpers;

public class LinkHelperTests
{
    [Theory]
    [InlineData("https://example.com/page?x=1", "https://example.com/page?x=1")]
    [InlineData("http://example.com", "http://example.com/")]
    [InlineData("example.com/path", "https://example.com/path")]
    [InlineData("  https://example.com  ", "https://example.com/")]
    [InlineData("https://example.com/?a=1&amp;b=2", "https://example.com/?a=1&b=2")]
    public void WebLinks_AreAccepted(string raw, string expected)
    {
        Assert.True(LinkHelper.TryGetSafeWebUri(raw, out var uri));
        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Theory]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-settings:privacy")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("ftp://example.com/file")]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    [InlineData(@"\\server\share")]
    [InlineData("/relative/path")]
    [InlineData("two words")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NonWebOrMalformedLinks_AreRejected(string? raw)
    {
        Assert.False(LinkHelper.TryGetSafeWebUri(raw, out _));
    }
}
