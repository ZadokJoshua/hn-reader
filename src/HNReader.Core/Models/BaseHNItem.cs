using System.Text.Json.Serialization;

namespace HNReader.Core.Models;

/// <summary>
/// Base class holds only common metadata fields shared by all item types.
/// </summary>
public class BaseHNItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("deleted")]
    public bool? Deleted { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("by")]
    public string? By { get; set; }

    [JsonPropertyName("time")]
    public long? Time { get; set; }

    [JsonPropertyName("dead")]
    public bool? Dead { get; set; }

    [JsonIgnore]
    public DateTime? CreatedAt =>
        Time.HasValue ? DateTimeOffset.FromUnixTimeSeconds(Time.Value).UtcDateTime : null;

    [JsonIgnore]
    public string? TimeAgo
    {
        get
        {
            if (CreatedAt == null) return null;

            var ts = DateTime.UtcNow - CreatedAt.Value;

            static string format(int value, string unit)
                => $"{value} {unit}{(value == 1 ? "" : "s")} ago";

            if (ts < TimeSpan.FromMinutes(1))
                return format(ts.Seconds, "second");
            if (ts < TimeSpan.FromHours(1))
                return format((int)ts.TotalMinutes, "minute");
            if (ts < TimeSpan.FromDays(1))
                return format((int)ts.TotalHours, "hour");
            if (ts < TimeSpan.FromDays(30))
                return format((int)ts.TotalDays, "day");
            if (ts < TimeSpan.FromDays(365))
                return format((int)(ts.TotalDays / 30), "month");

            return format((int)(ts.TotalDays / 365), "year");
        }
    }
}
