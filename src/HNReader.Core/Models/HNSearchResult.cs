using System.Net;
using System.Text.Json.Serialization;
using System;

namespace HNReader.Core.Models;

/// <summary>
/// Represents the root response from the Algolia Hacker News Search API.
/// </summary>
public class HNSearchResult
{
    [JsonPropertyName("hits")]
    public List<HNHit> Hits { get; set; } = [];

    [JsonPropertyName("nbHits")]
    public int TotalHits { get; set; }
}


public class HNHit
{
    [JsonPropertyName("objectID")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// The main body of the post. 
    /// Note: This contains HTML and Escaped characters.
    /// </summary>
    [JsonPropertyName("story_text")]
    public string? RawStoryText { get; set; }

    [JsonPropertyName("points")]
    public int? Points { get; set; }

    [JsonPropertyName("num_comments")]
    public int? CommentCount { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// AI-generated summary for digest stories. Only populated when the story is part of a digest.
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// Image URL extracted from the article. Only populated for digest stories.
    /// </summary>
    [JsonIgnore]
    public string? ImageUrl { get; set; }

    [JsonIgnore]
    public bool HasImage => TryCreateSafeUri(ImageUrl, out _);

    [JsonIgnore]
    public Uri? ImageUri => TryCreateSafeUri(ImageUrl, out var uri) ? uri : null;

    // --- HELPER PROPERTIES ---

    /// <summary>
    /// Returns the StoryText converted from HTML-encoded strings to plain text.
    /// </summary>
    public string? DecodedStoryText =>
        string.IsNullOrEmpty(RawStoryText) ? null : WebUtility.HtmlDecode(RawStoryText);

    public bool IsShowHN => Title?.StartsWith("Show HN:") ?? false;
    public bool IsAskHN => Title?.StartsWith("Ask HN:") ?? false;

    private static bool TryCreateSafeUri(string? input, out Uri? uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}