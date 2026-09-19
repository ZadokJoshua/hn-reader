using HNReader.Server.Models;
using HNReader.Shared.Models;
using LiteDB;

namespace HNReader.Server.Services;

/// <summary>
/// Persists generated digests to a LiteDB file — the same embedded, serverless
/// storage approach HNReader.Core's FavoritesService already uses, rather than
/// pulling in EF Core/SQL Server for what's a small document-shaped need.
/// The digest itself is stored as a JSON string (not mapped field-by-field)
/// so LiteDB's BSON mapper never has to reason about nested record collections.
/// </summary>
public class DigestStorageService : IDisposable
{
    private const string CollectionName = "digests";
    private const string StagingCollectionName = "digest_run_candidates";
    private const string SummaryCacheCollectionName = "story_summaries";
    private const string CategoriesCollectionName = "digest_categories";
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<DigestRecord> _collection;
    private readonly ILiteCollection<StagedCandidate> _stagingCollection;
    private readonly ILiteCollection<CachedStorySummary> _summaryCacheCollection;
    private readonly ILiteCollection<DigestCategory> _categoriesCollection;

    public DigestStorageService(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path cannot be null or empty", nameof(databasePath));
        }

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _database = new LiteDatabase(new ConnectionString
        {
            Filename = databasePath,
            Connection = ConnectionType.Shared
        });

        _collection = _database.GetCollection<DigestRecord>(CollectionName);
        _collection.EnsureIndex(x => x.GeneratedAtUtc);

        _stagingCollection = _database.GetCollection<StagedCandidate>(StagingCollectionName);
        _stagingCollection.EnsureIndex(x => x.Category);

        _summaryCacheCollection = _database.GetCollection<CachedStorySummary>(SummaryCacheCollectionName);
        _summaryCacheCollection.EnsureIndex(x => x.StoryId, unique: true);
        _summaryCacheCollection.EnsureIndex(x => x.CachedAtUtc);

        _categoriesCollection = _database.GetCollection<DigestCategory>(CategoriesCollectionName);
        _categoriesCollection.EnsureIndex(x => x.NameLower, unique: true);
        _categoriesCollection.EnsureIndex(x => x.SortOrder);
    }

    /// <summary>
    /// Persists this run's classified candidate pool — "arranging" the stories
    /// by category — before the (potentially slower, tool-using) summarization
    /// pass reads them back grouped. Gives a crashed/retried run somewhere to
    /// resume from instead of re-fetching and re-classifying from scratch.
    /// </summary>
    public Task SaveStagedCandidatesAsync(IEnumerable<StagedCandidate> candidates)
    {
        _stagingCollection.DeleteAll();
        _stagingCollection.InsertBulk(candidates);
        return Task.CompletedTask;
    }

    public Task<List<StagedCandidate>> GetStagedCandidatesAsync(string category)
    {
        var items = _stagingCollection.Query()
            .Where(c => c.Category == category)
            .ToList();
        return Task.FromResult(items);
    }

    /// <summary>Called once the whole run finishes successfully.</summary>
    public Task ClearStagedCandidatesAsync()
    {
        _stagingCollection.DeleteAll();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Looks up already-written summaries for the given story ids so a run can
    /// skip the LLM (and its slow tool fetches) for stories it has summarized
    /// before. Queried in one round trip rather than per story.
    /// </summary>
    public Task<Dictionary<int, CachedStorySummary>> GetCachedSummariesAsync(IReadOnlyCollection<int> storyIds)
    {
        if (storyIds.Count == 0)
        {
            return Task.FromResult(new Dictionary<int, CachedStorySummary>());
        }

        // Looked up one id at a time rather than with a single Contains()-over-a-
        // closure query: LiteDB translates only a subset of LINQ to BSON queries,
        // and a set-membership closure is exactly the kind of expression it can
        // silently fail to translate. StoryId is a unique index and this is an
        // embedded database, so a handful of point lookups per category costs
        // nothing and can't be quietly wrong.
        var cached = new Dictionary<int, CachedStorySummary>();
        foreach (var storyId in storyIds.Distinct())
        {
            var entry = _summaryCacheCollection.FindOne(c => c.StoryId == storyId);
            if (entry is not null) cached[storyId] = entry;
        }

        return Task.FromResult(cached);
    }

    /// <summary>
    /// Upserts summaries for stories this run actually summarized. Matches on
    /// StoryId rather than the LiteDB ObjectId so a re-summarized story replaces
    /// its old entry instead of accumulating duplicates that
    /// <see cref="GetCachedSummariesAsync"/> would then have to disambiguate.
    /// </summary>
    public Task SaveCachedSummariesAsync(IEnumerable<CachedStorySummary> summaries)
    {
        foreach (var summary in summaries)
        {
            var existing = _summaryCacheCollection.FindOne(c => c.StoryId == summary.StoryId);
            if (existing is not null)
            {
                summary.Id = existing.Id;
            }

            _summaryCacheCollection.Upsert(summary);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Drops cache entries older than <paramref name="maxAge"/>. Without this the
    /// cache is unbounded and a story summarized once is never revisited, so a
    /// weak summary would outlive every future run. Returns how many were pruned.
    /// </summary>
    public Task<int> PruneCachedSummariesAsync(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var deleted = _summaryCacheCollection.DeleteMany(c => c.CachedAtUtc < cutoff);
        return Task.FromResult(deleted);
    }

    public Task SaveAsync(DigestDto digest)
    {
        var record = new DigestRecord
        {
            GeneratedAtUtc = digest.GeneratedAtUtc,
            DigestJson = System.Text.Json.JsonSerializer.Serialize(digest)
        };
        _collection.Insert(record);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the most recently generated digest, or null if none exists yet.
    /// </summary>
    public Task<DigestDto?> GetLatestAsync()
    {
        var record = _collection.Query()
            .OrderByDescending(r => r.GeneratedAtUtc)
            .FirstOrDefault();

        var digest = record is null ? null : System.Text.Json.JsonSerializer.Deserialize<DigestDto>(record.DigestJson);
        return Task.FromResult(digest);
    }

    // ── Digest taxonomy ──────────────────────────────────────────────────
    //
    // The category list is small (tens of rows) and read by every digest run and
    // every /digest request, so DigestCategoryProvider keeps it in memory and
    // this type stays the thin persistence layer it is for everything else.

    /// <summary>
    /// Returns the whole taxonomy in display order, enabled and disabled alike.
    /// </summary>
    public Task<List<DigestCategory>> GetCategoriesAsync()
    {
        // Ordered in the query by SortOrder, then tie-broken in memory: LiteDB's
        // fluent query has no ThenBy, and a deterministic order matters even in
        // the unlikely case that two rows share a SortOrder. The row count is in
        // the tens, so sorting again locally costs nothing.
        var categories = _categoriesCollection.Query()
            .OrderBy(c => c.SortOrder)
            .ToList()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.NameLower, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(categories);
    }

    /// <summary>
    /// Point lookup on the unique NameLower index. <paramref name="nameLower"/>
    /// must already be invariant-lowercased by the caller.
    /// </summary>
    public Task<DigestCategory?> FindCategoryByNameAsync(string nameLower)
    {
        var category = _categoriesCollection.FindOne(c => c.NameLower == nameLower);
        return Task.FromResult<DigestCategory?>(category);
    }

    public Task<int> GetCategoryCountAsync() => Task.FromResult(_categoriesCollection.Count());

    public Task InsertCategoryAsync(DigestCategory category)
    {
        _categoriesCollection.Insert(category);
        return Task.CompletedTask;
    }

    public Task UpdateCategoryAsync(DigestCategory category)
    {
        _categoriesCollection.Update(category);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteCategoryAsync(DigestCategory category)
    {
        var deleted = _categoriesCollection.Delete(category.Id);
        return Task.FromResult(deleted);
    }

    /// <summary>
    /// Bulk-inserts the initial taxonomy. Only ever called when the collection
    /// is empty — see DigestCategoryProvider.EnsureSeededAsync.
    /// </summary>
    public Task SeedCategoriesAsync(IEnumerable<DigestCategory> categories)
    {
        _categoriesCollection.InsertBulk(categories);
        return Task.CompletedTask;
    }

    public void Dispose() => _database.Dispose();

    private class DigestRecord
    {
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        public DateTime GeneratedAtUtc { get; set; }
        public string DigestJson { get; set; } = string.Empty;
    }
}
