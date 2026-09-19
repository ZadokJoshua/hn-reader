using LiteDB;

namespace HNReader.Server.Models;

/// <summary>
/// One story from a digest run's candidate pool, already classified, staged in
/// LiteDB so the summarization pass can read stories back grouped by category
/// instead of relying on an in-memory dictionary that a crashed run would lose.
/// Plain mutable class (not a record) so LiteDB's BSON mapper has no ambiguity
/// about which constructor to use.
/// </summary>
public class StagedCandidate
{
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public int StoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public int Score { get; set; }
    public int CommentCount { get; set; }
    public string? RootDomain { get; set; }
    public string? Author { get; set; }
    public string Category { get; set; } = string.Empty;
}
