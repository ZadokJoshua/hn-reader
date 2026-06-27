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

        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
        return exportable.Count;
    }

    public async Task<int> ImportFromFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Import file not found.", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return 0;
        }

        var stories = System.Text.Json.JsonSerializer.Deserialize<List<Story>>(json);
        if (stories == null || stories.Count == 0)
        {
            return 0;
        }

        var imported = 0;
        foreach (var story in stories)
        {
            if (story == null || story.Id == 0) continue;
            _collection.Upsert(story);
            imported++;
        }

        if (imported > 0) OnFavoritesChanged();
        return imported;
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
