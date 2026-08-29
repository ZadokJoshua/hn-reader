using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HNReader.Core.Interfaces;
using HNReader.Core.Services.Logging;
using System.Threading;

namespace HNReader.Core.Viewmodels;

public partial class MainViewModel : BaseViewModel
{
    private readonly Lazy<IFavoritesService> _favoritesService;
    private readonly ILogger? _logger;

    [ObservableProperty]
    private int _favoriteCount;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    public MainViewModel(Lazy<IFavoritesService> favoritesService, ILogger? logger = null)
    {
        _favoritesService = favoritesService;
        _logger = logger;
    }

    private async void OnFavoritesChanged(object? sender, System.EventArgs e)
    {
        var all = await _favoritesService.Value.GetAllAsync();
        FavoriteCount = all.Count;
    }

    [RelayCommand]
    public async Task ExportFavoritesAsync(string filePath)
    {
        try
        {
            var count = await _favoritesService.Value.ExportToFileAsync(filePath);
            SetStatus($"Exported {count} favourite{(count == 1 ? "" : "s")}.");
        }
        catch (System.Exception ex)
        {
            _logger?.LogError("MainViewModel", "Export failed", ex);
            SetStatus($"Export failed: {ex.Message}", isError: true);
        }
    }

    [RelayCommand]
    public async Task ImportFavoritesAsync(string filePath)
    {
        try
        {
            var count = await _favoritesService.Value.ImportFromFileAsync(filePath);
            SetStatus($"Imported {count} favourite{(count == 1 ? "" : "s")}.");
        }
        catch (System.Exception ex)
        {
            _logger?.LogError("MainViewModel", "Import failed", ex);
            SetStatus($"Import failed: {ex.Message}", isError: true);
        }
    }

    private async void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        HasStatusMessage = true;
        await System.Threading.Tasks.Task.Delay(isError ? 5000 : 3000);
        HasStatusMessage = false;
        StatusMessage = string.Empty;
    }
}
