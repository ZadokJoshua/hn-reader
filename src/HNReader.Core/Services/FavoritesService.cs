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

    private void OnFavoritesChanged() => FavoritesChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _database.Dispose();
    }
}
