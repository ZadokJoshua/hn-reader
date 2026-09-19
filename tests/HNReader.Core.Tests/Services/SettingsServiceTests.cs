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

    // ── Digest feature toggle ────────────────────────────────────────────

    [Fact]
    public void IsDigestEnabled_DefaultsToOff()
    {
        var service = new SettingsService(_testSettingsDir);

        // Opt-in on purpose: the digest depends on a server the user may not be
        // running, so a fresh install makes no requests to it.
        Assert.False(service.IsDigestEnabled);
    }

    [Fact]
    public void IsDigestEnabled_SetAndGet_RoundTrips()
    {
        var service = new SettingsService(_testSettingsDir);
        service.IsDigestEnabled = true;
        Assert.True(service.IsDigestEnabled);
    }

    [Fact]
    public void IsDigestEnabled_PersistsAcrossInstances()
    {
        new SettingsService(_testSettingsDir).IsDigestEnabled = true;

        var reloaded = new SettingsService(_testSettingsDir);

        Assert.True(reloaded.IsDigestEnabled);
    }

    [Fact]
    public void IsDigestEnabled_TriggersChangedEvent()
    {
        var service = new SettingsService(_testSettingsDir);
        bool? raised = null;
        service.DigestEnabledChanged += (_, value) => raised = value;

        service.IsDigestEnabled = true;

        Assert.True(raised);
    }

    [Fact]
    public void IsDigestEnabled_SettingSameValue_DoesNotRaise()
    {
        var service = new SettingsService(_testSettingsDir);
        var raisedCount = 0;
        service.DigestEnabledChanged += (_, _) => raisedCount++;

        service.IsDigestEnabled = false;

        Assert.Equal(0, raisedCount);
    }

    /// <summary>
    /// The real upgrade path: a settings file written before the digest existed
    /// has no IsDigestEnabled key at all, and must load with the feature off
    /// rather than throwing.
    /// </summary>
    [Fact]
    public void Load_SettingsFileFromBeforeTheDigestExisted_LeavesFeatureOff()
    {
        File.WriteAllText(_testSettingsFile, "{\"Theme\":2}");

        var service = new SettingsService(_testSettingsDir);

        Assert.Equal(AppTheme.Dark, service.Theme);
        Assert.False(service.IsDigestEnabled);
    }
}
