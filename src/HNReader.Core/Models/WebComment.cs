namespace HNReader.Core.Models;

/// <summary>
/// Represents a comment parsed from the HN website HTML.
/// Includes depth information for proper nesting display.
/// </summary>
public class WebComment
{
    public int Id { get; set; }
    public string By { get; set; } = string.Empty;
    public string? Text { get; set; }
    public int Depth { get; set; }
    public long Time { get; set; }
    public string? TimeString { get; set; }

    public string? TimeAgo
    {
        get
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(Time);
            var diff = DateTimeOffset.UtcNow - dt;

            return diff.TotalMinutes switch
            {
                < 1 => "just now",
                < 60 => $"{(int)diff.TotalMinutes}m ago",
                < 1440 => $"{(int)diff.TotalHours}h ago",
                < 43200 => $"{(int)diff.TotalDays}d ago",
                _ => $"{(int)(diff.TotalDays / 30)}mo ago"
            };
        }
    }
}
