using System.Text.Json.Serialization;

namespace HNReader.Core.Models;

/// <summary>
/// Root DTO for the single digest.json file that the agent writes to the news_digest folder.
/// Only one digest exists at a time — each new generation overwrites the previous one.
/// </summary>
public class DigestOutputDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("groups")]
    public List<DigestGroupDto> Groups { get; set; } = [];

    /// <summary>
    /// Timestamp when the digest was generated. Set client-side after agent completion.
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public DateTime? GeneratedAt { get; set; }
}

/// <summary>
/// A single interest group within the digest output.
/// </summary>
public class DigestGroupDto
{
    [JsonPropertyName("interest")]
    public string Interest { get; set; } = string.Empty;

    [JsonPropertyName("interestDescription")]
    public string InterestDescription { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("stories")]
    public List<DigestStoryDto> Stories { get; set; } = [];
}

/// <summary>
/// A single story entry within a digest interest group.
/// </summary>
public class DigestStoryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>
    /// Image URL extracted from the article, if available.
    /// Populated client-side from the content scraper cache after agent completion.
    /// </summary>
    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }
}
