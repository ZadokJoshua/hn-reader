namespace HNReader.Core.Enums;

public record StoryTypeMetadata(string DisplayName, string ApiEndpoint, ApplicationPages? Page = null);

public static class StoryTypeExtensions
{
    private static readonly Dictionary<StoryType, StoryTypeMetadata> Metadata =
        new()
        {
            [StoryType.Top] = new("Top Stories", "topstories.json", ApplicationPages.Top),
            [StoryType.New] = new("New Stories", "newstories.json", ApplicationPages.New),
            [StoryType.Best] = new("Best Stories", "beststories.json", ApplicationPages.Best),
            [StoryType.Ask] = new("Ask HN", "askstories.json", ApplicationPages.Ask),
            [StoryType.Show] = new("Show HN", "showstories.json", ApplicationPages.Show),
            [StoryType.Job] = new("Job Stories", "jobstories.json", null)
        };

    public static StoryTypeMetadata GetMetadata(this StoryType type)
    {
        if (!Metadata.TryGetValue(type, out var metadata))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown story type");
        }

        return metadata;
    }

    public static string GetDisplayName(this StoryType type) => type.GetMetadata().DisplayName;

    public static string GetFeedEndpoint(this StoryType type)
    {
        var endpoint = type.GetMetadata().ApiEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException($"Story type {type} does not expose a feed endpoint.");
        }

        return endpoint;
    }

    public static ApplicationPages? GetAssociatedPage(this StoryType type) => type.GetMetadata().Page;
}
