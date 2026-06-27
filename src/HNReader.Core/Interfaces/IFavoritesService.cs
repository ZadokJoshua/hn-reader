using HNReader.Core.Models;

namespace HNReader.Core.Interfaces;

public interface IFavoritesService
{
    Task AddOrUpdateAsync(Story story);
    Task RemoveAsync(int storyId);
    Task<bool> ExistsAsync(int storyId);
    Task<List<Story>> GetAllAsync();
    Task<List<int>> GetAllIdsAsync();

    /// <summary>
    /// Exports all favorites to a JSON file at the given path.
    /// </summary>
    /// <returns>Number of stories exported.</returns>
    Task<int> ExportToFileAsync(string filePath);

    /// <summary>
    /// Imports favorites from a JSON file at the given path.
    /// Existing entries with the same ID are overwritten.
    /// </summary>
    /// <returns>Number of stories imported.</returns>
    Task<int> ImportFromFileAsync(string filePath);

    event EventHandler? FavoritesChanged;
}
