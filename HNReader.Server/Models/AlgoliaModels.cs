using System.Text.Json.Serialization;

namespace HNReader.Server.Models;

/// <summary>
/// Deserialization shape for HN's Algolia search API
/// (https://hn.algolia.com/api/v1/search), confirmed against the live response.
/// </summary>
internal record HNSearchResult(
    [property: JsonPropertyName("hits")] List<HNSearchHit> Hits);

public record HNSearchHit(
    [property: JsonPropertyName("objectID")] string ObjectId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("story_text")] string? StoryText,
    [property: JsonPropertyName("points")] int? Points,
    [property: JsonPropertyName("num_comments")] int? NumComments,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("created_at_i")] long CreatedAtI);
