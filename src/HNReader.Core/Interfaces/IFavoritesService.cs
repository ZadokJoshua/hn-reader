using HNReader.Core.Models;

namespace HNReader.Core.Interfaces;

public interface IFavoritesService
{
    Task AddOrUpdateAsync(Story story);
    Task RemoveAsync(int storyId);
    Task<bool> ExistsAsync(int storyId);
    Task<List<Story>> GetAllAsync();
    Task<List<int>> GetAllIdsAsync();
    event EventHandler? FavoritesChanged;
}
