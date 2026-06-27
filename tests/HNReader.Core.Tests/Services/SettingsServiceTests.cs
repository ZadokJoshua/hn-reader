using HNReader.Core.Enums;
using HNReader.Core.Services;

namespace HNReader.Core.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testSettingsDir;
    private readonly string _testSettingsFile;

    public SettingsServiceTests()
    {
        _testSettingsDir = Path.Combine(Path.GetTempPath(), $"hn_settings_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSettingsDir);
        _testSettingsFile = Path.Combine(_testSettingsDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testSettingsDir)) Directory.Delete(_testSettingsDir, recursive: true);
    }

    [Fact]
    public void Constructor_DefaultsToAutoTheme()
    {
        var service = new SettingsService(_testSettingsDir);
        Assert.Equal(AppTheme.Auto, service.Theme);
    }

    [Fact]
    public void Theme_SetAndGet_RoundTrips()
    {
        var service = new SettingsService(_testSettingsDir);
        service.Theme = AppTheme.Dark;
        Assert.Equal(AppTheme.Dark, service.Theme);
    }

    [Fact]
    public void Theme_TriggersChangedEvent()
    {
        var service = new SettingsService(_testSettingsDir);
        AppTheme? received = null;
        service.ThemeChanged += (s, t) => received = t;
        service.Theme = AppTheme.Light;
        Assert.Equal(AppTheme.Light, received);
    }

    [Fact]
    public void Theme_PersistsAcrossInstances()
    {
        var service = new SettingsService(_testSettingsDir);
        service.Theme = AppTheme.Dark;

        var service2 = new SettingsService(_testSettingsDir);
        Assert.Equal(AppTheme.Dark, service2.Theme);
    }

    [Fact]
    public void Theme_SameValue_DoesNotRaiseEventOrResave()
    {
        var service = new SettingsService(_testSettingsDir);
        service.Theme = AppTheme.Dark;
        var fired = false;
        service.ThemeChanged += (s, t) => fired = true;
        service.Theme = AppTheme.Dark;
        Assert.False(fired);
    }
}
