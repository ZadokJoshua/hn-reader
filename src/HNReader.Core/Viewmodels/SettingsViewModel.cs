using CommunityToolkit.Mvvm.ComponentModel;
using HNReader.Core.Enums;
using HNReader.Core.Interfaces;

namespace HNReader.Core.Viewmodels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private int selectedThemeIndex;

    [ObservableProperty]
    private bool confirmCommentLinks;

    public string[] ThemeOptions { get; } = ["Auto", "Light", "Dark"];

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        SelectedThemeIndex = (int)_settingsService.Theme;
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        // A ComboBox reports -1 while its items are being rebuilt or cleared; that is
        // not a choice, and casting it would persist an undefined theme.
        if (!Enum.IsDefined(typeof(AppTheme), value)) return;

        _settingsService.Theme = (AppTheme)value;
    }

    partial void OnConfirmCommentLinksChanged(bool value)
    {
        _settingsService.ConfirmCommentLinks = value;
    }
}
