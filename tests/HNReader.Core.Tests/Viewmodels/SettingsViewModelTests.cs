using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Viewmodels;

namespace HNReader.Core.Tests.Viewmodels;

public class SettingsViewModelTests
{
    private sealed class FakeSettings : ISettingsService
    {
        public AppTheme Theme { get; set; } = AppTheme.Dark;
        public bool ConfirmCommentLinks { get; set; } = true;
        public event EventHandler<AppTheme>? ThemeChanged { add { } remove { } }
        public void Save() { }
        public void Load() { }
    }

    [Fact]
    public void Constructor_ReflectsStoredTheme()
    {
        var vm = new SettingsViewModel(new FakeSettings { Theme = AppTheme.Light });
        Assert.Equal((int)AppTheme.Light, vm.SelectedThemeIndex);
    }

    [Fact]
    public void SelectingTheme_WritesThroughToService()
    {
        var settings = new FakeSettings();
        var vm = new SettingsViewModel(settings);
        vm.SelectedThemeIndex = (int)AppTheme.Light;
        Assert.Equal(AppTheme.Light, settings.Theme);
    }

    [Fact]
    public void NoSelection_DoesNotOverwriteStoredTheme()
    {
        var settings = new FakeSettings();
        var vm = new SettingsViewModel(settings);
        vm.SelectedThemeIndex = -1;
        Assert.Equal(AppTheme.Dark, settings.Theme);
    }

    [Fact]
    public void ConfirmCommentLinks_ReflectsStoredValue_AndWritesThrough()
    {
        var settings = new FakeSettings { ConfirmCommentLinks = false };
        var vm = new SettingsViewModel(settings);
        Assert.False(vm.ConfirmCommentLinks);

        vm.ConfirmCommentLinks = true;
        Assert.True(settings.ConfirmCommentLinks);
    }
}
