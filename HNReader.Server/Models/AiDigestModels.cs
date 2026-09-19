namespace HNReader.Server.Models;

/// <summary>
/// Structured output shape for the classification pass. The model sees each
/// candidate story's id, title, points/comments, domain, and story_text when
/// available (self-posts) and returns a category per id. It never has to
/// reproduce a URL, so there's no risk of it hallucinating/mangling one; the
/// real Title/Url/Author get joined back in locally from the source candidate
/// data using <see cref="StoryClassification.Id"/>.
/// </summary>
internal record ClassificationResponse(List<StoryClassification> Classifications);

internal record StoryClassification(int Id, string Category);

/// <summary>
/// Structured output shape for the per-category summarization pass. The model
/// writes prose only (an overview + takeaways keyed by story id), never a URL.
/// <see cref="ItemTakeaway.CommentNote"/> is populated only when the model
/// actually used the read_comments tool for that story — null otherwise, not an
/// error.
/// </summary>
internal record CategorySummaryResponse(string Summary, List<ItemTakeaway> Items);

internal record ItemTakeaway(int Id, string Takeaway, string? CommentNote = null);
