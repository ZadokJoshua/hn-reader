using HNReader.Core.Models;
using HNReader.Core.Services;

namespace HNReader.Core.Tests.Services;

[Collection("FavoritesService")]
public class FavoritesImportValidationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fav_val_{Guid.NewGuid()}.db");
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"fav_val_{Guid.NewGuid()}.json");
    private readonly FavoritesService _service;

    public FavoritesImportValidationTests()
    {
        _service = new FavoritesService(_dbPath);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (File.Exists(_file)) File.Delete(_file);
    }

    private async Task<FavoritesImportFailure> FailureOf(string content)
    {
        await File.WriteAllTextAsync(_file, content);
        var ex = await Assert.ThrowsAsync<FavoritesImportException>(() => _service.ImportAsync(_file));
        return ex.Reason;
    }

    [Fact]
    public async Task EmptyFile_IsReportedAsEmpty()
        => Assert.Equal(FavoritesImportFailure.EmptyFile, await FailureOf("   \n"));

    [Fact]
    public async Task PlainText_IsNotAFavouritesFile()
        => Assert.Equal(FavoritesImportFailure.NotFavouritesFile, await FailureOf("hello there"));

    [Fact]
    public async Task JsonObject_IsNotAFavouritesFile()
        => Assert.Equal(FavoritesImportFailure.NotFavouritesFile, await FailureOf("{\"name\":\"x\"}"));

    [Fact]
    public async Task EmptyArray_HasNothingToImport()
        => Assert.Equal(FavoritesImportFailure.NothingValid, await FailureOf("[]"));

    [Fact]
    public async Task OnlyInvalidEntries_HasNothingToImport()
        => Assert.Equal(FavoritesImportFailure.NothingValid, await FailureOf("[1, \"a\", {\"id\":\"abc\"}, {\"id\":0}]"));

    [Fact]
    public async Task OneBadEntry_IsSkipped_OthersImported()
    {
        await File.WriteAllTextAsync(_file, "[{\"id\":5,\"title\":\"ok\"}, {\"id\":\"oops\"}, 7, {\"id\":0}]");

        var result = await _service.ImportAsync(_file);

        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Updated);
        Assert.Equal(3, result.Skipped);
        Assert.True(await _service.ExistsAsync(5));
    }

    [Fact]
    public async Task ExistingEntries_AreCountedAsUpdated()
    {
        await _service.AddOrUpdateAsync(new Story { Id = 5, Title = "old" });
        await File.WriteAllTextAsync(_file, "[{\"id\":5,\"title\":\"new\"},{\"id\":6,\"title\":\"fresh\"}]");

        var result = await _service.ImportAsync(_file);

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Updated);
    }

    [Fact]
    public async Task FailedImport_LeavesExistingFavouritesUntouched()
    {
        await _service.AddOrUpdateAsync(new Story { Id = 1, Title = "keep" });

        await FailureOf("{\"not\":\"an array\"}");

        Assert.Equal(new[] { 1 }, await _service.GetAllIdsAsync());
    }

    [Fact]
    public async Task Export_LeavesNoTempFile()
    {
        await _service.AddOrUpdateAsync(new Story { Id = 1, Title = "a" });

        await _service.ExportToFileAsync(_file);

        Assert.True(File.Exists(_file));
        Assert.False(File.Exists(_file + ".tmp"));
    }
}
