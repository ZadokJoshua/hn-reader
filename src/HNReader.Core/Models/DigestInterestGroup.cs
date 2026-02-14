using System.Collections.ObjectModel;

namespace HNReader.Core.Models;

/// <summary>
/// Groups trending stories under a single user interest for digest generation.
/// Enforces the constraint that a story can belong to exactly one interest group.
/// </summary>
public class DigestInterestGroup
{
    public Interest Interest { get; }

    public ObservableCollection<HNHit> Stories { get; } = [];

    public string Summary { get; set; } = string.Empty;

    public int StoryCount => Stories.Count;

    public double TrendingScore => Math.Round((double)Stories.Average(s => s.Points), 2);

    public DigestInterestGroup(Interest interest)
    {
        ArgumentNullException.ThrowIfNull(interest);
        Interest = interest;
    }

    /// <summary>
    /// Checks whether a story can be added to this group without violating the
    /// one-story-per-interest constraint across all groups.
    /// </summary>
    /// <param name="story">The story to check.</param>
    /// <param name="allGroups">All interest groups in the current digest.</param>
    /// <returns>True if the story is not already assigned to any group.</returns>
    public static bool CanAddStory(HNHit story, IEnumerable<DigestInterestGroup> allGroups)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(allGroups);

        return !allGroups.Any(g => g.Stories.Any(s => s.Id == story.Id));
    }

    /// <summary>
    /// Adds a story to this group if it passes the uniqueness constraint.
    /// </summary>
    /// <param name="story">The story to add.</param>
    /// <param name="allGroups">All interest groups in the current digest.</param>
    /// <returns>True if the story was added; false if it already exists in another group.</returns>
    public bool TryAddStory(HNHit story, IEnumerable<DigestInterestGroup> allGroups)
    {
        if (!CanAddStory(story, allGroups))
        {
            return false;
        }

        Stories.Add(story);
        return true;
    }
}
