using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using LiteDB;

namespace HNReader.Core.Services;

public class FavoritesService : IFavoritesService, IDisposable
{
    private const string CollectionName = "favorites";
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<Story> _collection;

    public FavoritesService(string databasePath)
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

        _collection = _database.GetCollection<Story>(CollectionName);
        _collection.EnsureIndex(x => x.Id, true);
    }

    public event EventHandler? FavoritesChanged;

    public Task AddOrUpdateAsync(Story story)
    {
        if (story == null) throw new ArgumentNullException(nameof(story));

        _collection.Upsert(story);
        OnFavoritesChanged();
        return Task.CompletedTask;
    }

    public Task RemoveAsync(int storyId)
    {
        _collection.DeleteMany(s => s.Id == storyId);
        OnFavoritesChanged();
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(int storyId)
    {
        var exists = _collection.Exists(s => s.Id == storyId);
        return Task.FromResult(exists);
    }

    public Task<List<Story>> GetAllAsync()
    {
        var items = _collection.FindAll()
            .OrderByDescending(s => s.Time ?? 0)
            .ToList();
        return Task.FromResult(items);
    }

    public Task<List<int>> GetAllIdsAsync()
    {
        var ids = _collection.FindAll()
            .OrderByDescending(s => s.Time ?? 0)
            .Select(s => s.Id)
            .ToList();
        return Task.FromResult(ids);
    }

    public async Task<int> ExportToFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        var stories = await GetAllAsync().ConfigureAwait(false);

        // Strip runtime-only fields so the file is portable
        var exportable = stories.Select(CloneForExport).ToList();
        var json = System.Text.Json.JsonSerializer.Serialize(exportable, CoreHelper.JsonSerializerOptions);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Temp file + move: a failure part-way never leaves a truncated export in place of a good one.
        var tempPath = filePath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { /* best effort */ }
            throw;
        }
        return exportable.Count;
    }

    /// <summary>Largest file we will try to parse; a real export is well under a few MB.</summary>
    private const long MaxImportBytes = 20L * 1024 * 1024;

    public async Task<int> ImportFromFileAsync(string filePath)
        => (await ImportAsync(filePath).ConfigureAwait(false)).Total;

    public async Task<FavoritesImportResult> ImportAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Import file not found.", filePath);
        }

        if (new FileInfo(filePath).Length > MaxImportBytes)
        {
            throw new FavoritesImportException(FavoritesImportFailure.FileTooLarge);
        }

        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new FavoritesImportException(FavoritesImportFailure.EmptyFile);
        }

        System.Text.Json.JsonDocument document;
        try
        {
            document = System.Text.Json.JsonDocument.Parse(json);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new FavoritesImportException(FavoritesImportFailure.NotFavouritesFile, ex);
        }

        var stories = new List<Story>();
        var skipped = 0;
        using (document)
        {
            // An export is a top-level array. Anything else is some other JSON file.
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                throw new FavoritesImportException(FavoritesImportFailure.NotFavouritesFile);
            }

            // Parse entry by entry so one malformed entry skips itself, not the whole file.
            foreach (var element in document.RootElement.EnumerateArray())
            {
                try
                {
                    var story = System.Text.Json.JsonSerializer.Deserialize<Story>(element);
                    if (story is null || story.Id == 0) { skipped++; continue; }
                    stories.Add(story);
                }
                catch (System.Text.Json.JsonException)
                {
                    skipped++;
                }
            }
        }

        if (stories.Count == 0)
        {
            throw new FavoritesImportException(FavoritesImportFailure.NothingValid);
        }

        var added = 0;
        var updated = 0;

        // One transaction, so a failure part-way leaves the existing favourites untouched.
        _database.BeginTrans();
        try
        {
            foreach (var story in stories)
            {
                if (_collection.Upsert(story)) added++; else updated++;
            }
            _database.Commit();
        }
        catch
        {
            _database.Rollback();
            throw;
        }

        OnFavoritesChanged();
        return new FavoritesImportResult(added, updated, skipped);
    }

    private static Story CloneForExport(Story story)
    {
        // Don't carry transient state like IsFavorite into the exported file.
        return new Story
        {
            Id = story.Id,
            Deleted = story.Deleted,
            Type = story.Type,
            By = story.By,
            Time = story.Time,
            Dead = story.Dead,
            Title = story.Title,
            Url = story.Url,
            Text = story.Text,
            Score = story.Score,
            Kids = story.Kids,
            Descendants = story.Descendants
        };
    }

    private void OnFavoritesChanged() => FavoritesChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _database.Dispose();
    }
}
