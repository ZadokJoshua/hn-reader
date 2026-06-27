using HNReader.Core.Models;
using HNReader.Core.Services;

namespace HNReader.Core.Tests.Services;

[Collection("FavoritesService")]
public class FavoritesServiceExportImportTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _testExportPath;
    private readonly FavoritesService _service;

    public FavoritesServiceExportImportTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"fav_test_{Guid.NewGuid()}.db");
        _testExportPath = Path.Combine(Path.GetTempPath(), $"fav_export_{Guid.NewGuid()}.json");
        _service = new FavoritesService(_testDbPath);
    }

    public void Dispose()
    {
        _service?.Dispose();
        if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
        if (File.Exists(_testExportPath)) File.Delete(_testExportPath);
    }

    [Fact]
    public async Task ExportToFileAsync_WithEmptyFavorites_CreatesEmptyFile()
    {
        var count = await _service.ExportToFileAsync(_testExportPath);
        Assert.Equal(0, count);
        Assert.True(File.Exists(_testExportPath));
    }

    [Fact]
    public async Task ExportToFileAsync_WithFavorites_WritesAllToFile()
    {
        await _service.AddOrUpdateAsync(new Story { Id = 1, Title = "A", Url = "https://a" });
        await _service.AddOrUpdateAsync(new Story { Id = 2, Title = "B", Url = "https://b" });
        await _service.AddOrUpdateAsync(new Story { Id = 3, Title = "C", Url = "https://c" });

        var count = await _service.ExportToFileAsync(_testExportPath);
        Assert.Equal(3, count);
        Assert.True(File.Exists(_testExportPath));

        var content = await File.ReadAllTextAsync(_testExportPath);
        Assert.Contains("\"id\": 1", content);
        Assert.Contains("\"id\": 2", content);
        Assert.Contains("\"id\": 3", content);
    }

    [Fact]
    public async Task ImportFromFileAsync_RestoresFavorites()
    {
        // Seed the source file
        var sourceService = new FavoritesService(Path.Combine(Path.GetTempPath(), $"fav_src_{Guid.NewGuid()}.db"));
        await sourceService.AddOrUpdateAsync(new Story { Id = 100, Title = "Imported 1" });
        await sourceService.AddOrUpdateAsync(new Story { Id = 200, Title = "Imported 2" });
        await sourceService.ExportToFileAsync(_testExportPath);
        sourceService.Dispose();

        var count = await _service.ImportFromFileAsync(_testExportPath);
        Assert.Equal(2, count);
        Assert.True(await _service.ExistsAsync(100));
        Assert.True(await _service.ExistsAsync(200));
    }

    [Fact]
    public async Task ImportFromFileAsync_OverwritesExistingStories()
    {
        await _service.AddOrUpdateAsync(new Story { Id = 1, Title = "Old" });
        var sourceService = new FavoritesService(Path.Combine(Path.GetTempPath(), $"fav_src2_{Guid.NewGuid()}.db"));
        await sourceService.AddOrUpdateAsync(new Story { Id = 1, Title = "New" });
        await sourceService.ExportToFileAsync(_testExportPath);
        sourceService.Dispose();

        await _service.ImportFromFileAsync(_testExportPath);

        var all = await _service.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("New", all[0].Title);
    }

    [Fact]
    public async Task ImportFromFileAsync_WithNonexistentFile_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.ImportFromFileAsync(Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid()}.json")));
    }

    [Fact]
    public async Task ExportToFileAsync_StripsTransientIsFavoriteFlag()
    {
        await _service.AddOrUpdateAsync(new Story { Id = 1, Title = "A", IsFavorite = true });

        await _service.ExportToFileAsync(_testExportPath);

        // Read raw JSON and check IsFavorite is not present (since it has [JsonIgnore])
        var content = await File.ReadAllTextAsync(_testExportPath);
        Assert.DoesNotContain("IsFavorite", content);
    }
}
