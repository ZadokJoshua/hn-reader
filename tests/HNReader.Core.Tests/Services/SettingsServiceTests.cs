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

    /// <summary>Older settings can contain fields the current settings model no longer uses.</summary>
    [Fact]
    public void Load_SettingsWithObsoleteProperties_PreservesTheme()
    {
        File.WriteAllText(_testSettingsFile,
            "{\"Theme\":2,\"IsDigestEnabled\":true,\"SelectedDigestCategories\":[\"Science\"]}");

        var service = new SettingsService(_testSettingsDir);

        Assert.Equal(AppTheme.Dark, service.Theme);
        service.Theme = AppTheme.Light;

        var reloaded = new SettingsService(_testSettingsDir);
        Assert.Equal(AppTheme.Light, reloaded.Theme);
    }

    [Fact]
    public void Load_UndefinedThemeValue_FallsBackToAuto()
    {
        File.WriteAllText(_testSettingsFile, "{\"Theme\":42}");

        var service = new SettingsService(_testSettingsDir);

        Assert.Equal(AppTheme.Auto, service.Theme);
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaults()
    {
        File.WriteAllText(_testSettingsFile, "{ not json");

        var service = new SettingsService(_testSettingsDir);

        Assert.Equal(AppTheme.Auto, service.Theme);
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        var service = new SettingsService(_testSettingsDir);
        service.Theme = AppTheme.Light;

        Assert.True(File.Exists(_testSettingsFile));
        Assert.False(File.Exists(_testSettingsFile + ".tmp"));
    }

    [Fact]
    public void Save_MissingDirectory_IsRecreated()
    {
        var nested = Path.Combine(_testSettingsDir, "nested");
        var service = new SettingsService(nested);
        service.Theme = AppTheme.Dark;

        Assert.Equal(AppTheme.Dark, new SettingsService(nested).Theme);
    }

    [Fact]
    public void ConfirmCommentLinks_DefaultsToTrue()
    {
        Assert.True(new SettingsService(_testSettingsDir).ConfirmCommentLinks);
    }

    [Fact]
    public void ConfirmCommentLinks_PersistsAcrossInstances()
    {
        new SettingsService(_testSettingsDir).ConfirmCommentLinks = false;

        Assert.False(new SettingsService(_testSettingsDir).ConfirmCommentLinks);
    }

    [Fact]
    public void Load_FileWithoutConfirmField_KeepsPromptOn()
    {
        File.WriteAllText(_testSettingsFile, "{\"Theme\":1}");

        var service = new SettingsService(_testSettingsDir);

        Assert.Equal(AppTheme.Light, service.Theme);
        Assert.True(service.ConfirmCommentLinks);
    }
}
