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

    public DigestInterestGroup(Interest interest)
    {
        ArgumentNullException.ThrowIfNull(interest);
        Interest = interest;
    }
}
