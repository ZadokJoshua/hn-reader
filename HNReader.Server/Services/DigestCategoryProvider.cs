using HNReader.Server.Configuration;
using HNReader.Server.Models;
using LiteDB;

namespace HNReader.Server.Services;

/// <summary>
/// An immutable view of the taxonomy at a point in time. Handed out whole so a
/// reader can never observe a partially-updated list, and so a digest run can
/// hold one consistent view for its entire duration.
/// </summary>
/// <param name="All">Every category in display order, enabled and disabled alike.</param>
/// <param name="EnabledNames">
/// The names new digest runs classify into, in display order.
/// </param>
/// <param name="KnownNames">
/// Every name the taxonomy has ever been told about, enabled or not, compared
/// case-insensitively. This — not <paramref name="EnabledNames"/> — is what the
/// public /digest endpoint whitelists against, so a stored digest that
/// references a since-disabled category can still be filtered for.
/// </param>
public sealed record CategorySnapshot(
    IReadOnlyList<DigestCategory> All,
    IReadOnlyList<string> EnabledNames,
    IReadOnlySet<string> KnownNames)
{
    public static readonly CategorySnapshot Empty =
        new([], [], new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}

public enum CategoryWriteOutcome
{
    Ok,
    Invalid,
    Duplicate,
    NotFound,
    /// <summary>The write was refused because it would leave the taxonomy empty.</summary>
    WouldEmptyTaxonomy
}

public sealed record CategoryWriteResult(
    CategoryWriteOutcome Outcome,
    DigestCategory? Category = null,
    string? Error = null)
{
    public static CategoryWriteResult Ok(DigestCategory category) => new(CategoryWriteOutcome.Ok, category);
    public static CategoryWriteResult Invalid(string error) => new(CategoryWriteOutcome.Invalid, Error: error);
    public static CategoryWriteResult Duplicate(string error) => new(CategoryWriteOutcome.Duplicate, Error: error);
    public static CategoryWriteResult NotFound(string error) => new(CategoryWriteOutcome.NotFound, Error: error);
    public static CategoryWriteResult WouldEmpty(string error) => new(CategoryWriteOutcome.WouldEmptyTaxonomy, Error: error);
}

/// <summary>
/// Owns the digest taxonomy: seeding, validation, mutation, and the in-memory
/// snapshot every reader uses.
///
/// <para>
/// Registered as a singleton and read on the public /digest hot path as well as
/// once per digest run, so the list is cached rather than queried each time. The
/// cache is a single immutable <see cref="CategorySnapshot"/> that is
/// <b>replaced, never mutated</b>: readers take no lock and cannot observe a
/// half-built list, while writers serialize on <see cref="_writeLock"/> because
/// every mutation is a read-modify-write (duplicate check, max-sort-order,
/// permutation check).
/// </para>
///
/// <para>
/// Invalidation is trivial because the only writer rebuilds the snapshot inline
/// — there is no TTL and no cross-process invalidation. That is correct <i>here</i>
/// because the deployment is single-instance by construction (an embedded LiteDB
/// file plus SQLite-backed Hangfire). <b>If this ever runs multi-instance, this
/// cache goes stale silently</b> and would need a real invalidation channel.
/// </para>
/// </summary>
public class DigestCategoryProvider
{
    /// <summary>
    /// Upper bound on a category name's length. Not decoration: every name is
    /// interpolated into the classification prompt for every batch of every run,
    /// and long category names are exactly what previously pushed a
    /// classification response past the model's output limit — silently
    /// returning a quarter of the stories. The longest seeded name is 42 chars.
    /// </summary>
    public const int MaxNameLength = 80;

    private readonly DigestStorageService _storage;
    private readonly ILogger<DigestCategoryProvider> _logger;
    private readonly object _writeLock = new();

    private CategorySnapshot _snapshot = CategorySnapshot.Empty;

    public DigestCategoryProvider(DigestStorageService storage, ILogger<DigestCategoryProvider> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// The current taxonomy. Lock-free: the returned snapshot is immutable and
    /// stays valid for as long as the caller holds it, even across a concurrent
    /// write.
    /// </summary>
    public CategorySnapshot GetSnapshot() => Volatile.Read(ref _snapshot);

    /// <summary>
    /// Populates an empty taxonomy from <see cref="DigestCategorySeed"/> and
    /// loads the snapshot. Called explicitly at startup rather than from a
    /// constructor: the singleton is resolved lazily, and bootstrapping schema
    /// off first-resolution makes ordering implicit and hard to grep for.
    ///
    /// <para>
    /// Seeds only when the collection is empty. Deliberately <b>not</b> a "merge
    /// any missing seeds" reconciliation — that would resurrect a category an
    /// operator had deleted, and would keep the code constant as the de-facto
    /// source of truth, defeating the point of moving the taxonomy into the
    /// database. To re-seed, delete the <c>digest_categories</c> collection.
    /// </para>
    /// </summary>
    public async Task EnsureSeededAsync()
    {
        var existing = await _storage.GetCategoryCountAsync().ConfigureAwait(false);

        if (existing == 0)
        {
            var now = DateTime.UtcNow;
            var seeds = DigestCategorySeed.Names.Select((name, index) => new DigestCategory
            {
                Name = name,
                NameLower = name.ToLowerInvariant(),
                SortOrder = (index + 1) * DigestCategorySeed.SortOrderStep,
                IsEnabled = true,
                CreatedAtUtc = now
            });

            await _storage.SeedCategoriesAsync(seeds).ConfigureAwait(false);
            _logger.LogInformation(
                "Digest: seeded {Count} categories into an empty taxonomy", DigestCategorySeed.Names.Length);
        }

        await RefreshAsync().ConfigureAwait(false);
    }

    /// <summary>Re-reads the taxonomy from storage and swaps in a new snapshot.</summary>
    public async Task RefreshAsync()
    {
        var categories = await _storage.GetCategoriesAsync().ConfigureAwait(false);
        Volatile.Write(ref _snapshot, BuildSnapshot(categories));
    }

    private static CategorySnapshot BuildSnapshot(List<DigestCategory> categories) =>
        new(
            categories,
            categories.Where(c => c.IsEnabled).Select(c => c.Name).ToList(),
            categories.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Adds a category to the end of the taxonomy. Appending rather than
    /// inserting is deliberate: an add can then never reorder the sections an
    /// existing client is already rendering.
    /// </summary>
    public async Task<CategoryWriteResult> AddAsync(string? name)
    {
        // Trim before validating and store the trimmed form: surrounding
        // whitespace is never meaningful in a category name, and leaving it in
        // would defeat duplicate detection.
        var trimmed = name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return CategoryWriteResult.Invalid("A category name is required.");
        }

        if (trimmed.Length > MaxNameLength)
        {
            return CategoryWriteResult.Invalid(
                $"A category name must be {MaxNameLength} characters or fewer.");
        }

        // A newline would corrupt the classification prompt's line-per-story
        // format, and a double quote would break out of the quoted name the
        // prompt wraps it in.
        if (trimmed.Any(char.IsControl) || trimmed.Contains('"'))
        {
            return CategoryWriteResult.Invalid(
                "A category name cannot contain control characters or double quotes.");
        }

        var nameLower = trimmed.ToLowerInvariant();

        // Read-modify-write: the duplicate check and the max-sort-order read
        // must not interleave with another add.
        lock (_writeLock)
        {
            var existing = _storage.FindCategoryByNameAsync(nameLower).GetAwaiter().GetResult();
            if (existing is not null)
            {
                return CategoryWriteResult.Duplicate(
                    $"The category \"{existing.Name}\" already exists.");
            }

            var current = GetSnapshot().All;
            var nextSortOrder = (current.Count == 0 ? 0 : current.Max(c => c.SortOrder))
                                + DigestCategorySeed.SortOrderStep;

            var category = new DigestCategory
            {
                Name = trimmed,
                NameLower = nameLower,
                SortOrder = nextSortOrder,
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            try
            {
                _storage.InsertCategoryAsync(category).GetAwaiter().GetResult();
            }
            catch (LiteException ex) when (ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
            {
                // The unique index is the backstop for the check above. Map it to
                // the duplicate result rather than letting a 500 escape.
                _logger.LogWarning(ex, "Digest: unique-index violation adding category {Name}", trimmed);
                return CategoryWriteResult.Duplicate($"The category \"{trimmed}\" already exists.");
            }

            RefreshSnapshotUnderLock();
            _logger.LogInformation(
                "Digest: category {Name} added to the taxonomy (sort order {SortOrder})",
                trimmed, nextSortOrder);

            return CategoryWriteResult.Ok(category);
        }
    }

    /// <summary>
    /// Enables or disables a category. A disabled category stops being offered
    /// to the classifier on future runs but stays in the taxonomy, so digests
    /// that already reference its name remain filterable and coherent.
    /// </summary>
    public Task<CategoryWriteResult> SetEnabledAsync(string name, bool isEnabled)
    {
        lock (_writeLock)
        {
            var category = FindUnderLock(name);
            if (category is null)
            {
                return Task.FromResult(CategoryWriteResult.NotFound($"No category named \"{name}\" exists."));
            }

            // Refusing to disable the last enabled category for the same reason
            // delete refuses to empty the taxonomy: a run with nothing to
            // classify into aborts, so this would silently break every digest.
            if (!isEnabled && GetSnapshot().EnabledNames.Count == 1 && category.IsEnabled)
            {
                return Task.FromResult(CategoryWriteResult.WouldEmpty(
                    "Cannot disable the last enabled category — digest runs would have nothing to classify into."));
            }

            if (category.IsEnabled != isEnabled)
            {
                category.IsEnabled = isEnabled;
                _storage.UpdateCategoryAsync(category).GetAwaiter().GetResult();
                RefreshSnapshotUnderLock();
                _logger.LogInformation(
                    "Digest: category {Name} {State}", category.Name, isEnabled ? "enabled" : "disabled");
            }

            return Task.FromResult(CategoryWriteResult.Ok(category));
        }
    }

    /// <summary>
    /// Removes a category outright. Stored digests embed category names as
    /// plain strings, so past digests stay readable — they simply reference a
    /// name the taxonomy no longer lists. Prefer disabling when the intent is
    /// "stop using this", and delete only to undo a mistake.
    /// </summary>
    public Task<CategoryWriteResult> DeleteAsync(string name)
    {
        lock (_writeLock)
        {
            var category = FindUnderLock(name);
            if (category is null)
            {
                return Task.FromResult(CategoryWriteResult.NotFound($"No category named \"{name}\" exists."));
            }

            if (GetSnapshot().All.Count <= 1)
            {
                return Task.FromResult(CategoryWriteResult.WouldEmpty(
                    "Cannot delete the last category — digest runs would have nothing to classify into."));
            }

            _storage.DeleteCategoryAsync(category).GetAwaiter().GetResult();
            RefreshSnapshotUnderLock();
            _logger.LogInformation("Digest: category {Name} deleted from the taxonomy", category.Name);

            return Task.FromResult(CategoryWriteResult.Ok(category));
        }
    }

    /// <summary>
    /// Reorders the taxonomy from a full ordered list of names. Taking the whole
    /// list rather than a move-this-to-index operation makes the call idempotent
    /// and removes all index arithmetic; anything that isn't a permutation of the
    /// current names is rejected outright instead of half-applied.
    /// </summary>
    public Task<CategoryWriteResult> ReorderAsync(IReadOnlyList<string>? names)
    {
        if (names is null || names.Count == 0)
        {
            return Task.FromResult(CategoryWriteResult.Invalid("An ordered list of category names is required."));
        }

        lock (_writeLock)
        {
            var current = GetSnapshot().All;

            var requested = names.Select(n => n?.Trim() ?? string.Empty).ToList();
            if (requested.Any(string.IsNullOrWhiteSpace))
            {
                return Task.FromResult(CategoryWriteResult.Invalid("Category names cannot be blank."));
            }

            var requestedSet = requested.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (requestedSet.Count != requested.Count)
            {
                return Task.FromResult(CategoryWriteResult.Invalid("The list contains duplicate category names."));
            }

            if (requested.Count != current.Count || !current.All(c => requestedSet.Contains(c.Name)))
            {
                return Task.FromResult(CategoryWriteResult.Invalid(
                    $"The list must be a permutation of all {current.Count} existing category names."));
            }

            var byName = current.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < requested.Count; index++)
            {
                var category = byName[requested[index]];
                var sortOrder = (index + 1) * DigestCategorySeed.SortOrderStep;
                if (category.SortOrder == sortOrder) continue;

                category.SortOrder = sortOrder;
                _storage.UpdateCategoryAsync(category).GetAwaiter().GetResult();
            }

            RefreshSnapshotUnderLock();
            _logger.LogInformation("Digest: taxonomy reordered ({Count} categories)", requested.Count);

            return Task.FromResult(new CategoryWriteResult(CategoryWriteOutcome.Ok));
        }
    }

    private DigestCategory? FindUnderLock(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : _storage.FindCategoryByNameAsync(name.Trim().ToLowerInvariant()).GetAwaiter().GetResult();

    // The storage calls are Task-returning but synchronous throughout this file's
    // house style, so blocking on them inside the lock introduces no scheduling
    // risk — and a lock cannot span an await.
    private void RefreshSnapshotUnderLock()
    {
        var categories = _storage.GetCategoriesAsync().GetAwaiter().GetResult();
        Volatile.Write(ref _snapshot, BuildSnapshot(categories));
    }
}
