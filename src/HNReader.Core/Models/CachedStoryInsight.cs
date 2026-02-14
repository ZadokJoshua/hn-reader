namespace HNReader.Core.Models;

/// <summary>
/// Cached AI insight for a story, including the panel open state.
/// Stored in-memory per session.
/// </summary>
public sealed record CachedStoryInsight(string InsightText, bool IsPanelOpen);
