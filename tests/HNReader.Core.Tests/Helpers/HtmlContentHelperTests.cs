using HNReader.Core.Helpers;

namespace HNReader.Core.Tests.Helpers;

public class HtmlContentHelperTests
{
    [Fact]
    public void HandDrawnOBullets_BecomeAList()
    {
        Assert.Equal("* a\n\n* b", HtmlContentHelper.ToMarkdown("o a<p>o b"));
    }

    [Fact]
    public void DashBullets_UseTheDiscMarker()
    {
        // "-" renders as a hollow circle that reads as the letter O.
        Assert.Equal("* a\n\n* b", HtmlContentHelper.ToMarkdown("- a<p>- b"));
    }

    [Fact]
    public void LoneDashItem_IsStillRewritten()
    {
        Assert.Equal("* only", HtmlContentHelper.ToMarkdown("- only"));
    }

    [Fact]
    public void LoneOLine_IsLeftAsProse()
    {
        Assert.Equal("o brother where art thou", HtmlContentHelper.ToMarkdown("o brother where art thou"));
    }

    [Fact]
    public void BulletCharacters_BecomeAList()
    {
        Assert.Equal("* a\n\n* b", HtmlContentHelper.ToMarkdown("• a<p>• b"));
    }

    [Fact]
    public void BreakSeparatedBullets_BecomeAList()
    {
        Assert.Equal("* a\n* b", HtmlContentHelper.ToMarkdown("o a<br>o b"));
    }

    [Fact]
    public void ProseBetweenBullets_BreaksTheRun()
    {
        // The single "o" lines are separated by prose, so neither is a list.
        var result = HtmlContentHelper.ToMarkdown("o first<p>some prose here<p>o second");
        Assert.Equal("o first\n\nsome prose here\n\no second", result);
    }

    [Fact]
    public void NumberedLists_AreUnchanged()
    {
        Assert.Equal("1. a\n\n2. b", HtmlContentHelper.ToMarkdown("1. a<p>2. b"));
    }

    [Fact]
    public void FencedCode_IsNotRewritten()
    {
        var result = HtmlContentHelper.ToMarkdown("<pre><code>- a\n- b</code></pre>");
        Assert.Contains("- a", result);
        Assert.Contains("- b", result);
    }

    [Fact]
    public void ExistingConversions_StillWork()
    {
        var result = HtmlContentHelper.ToMarkdown("<i>hi</i> <b>there</b> <a href=\"https://x.com\">link</a> &amp; more");
        Assert.Equal("*hi* **there** [link](https://x.com) & more", result);
    }
}
