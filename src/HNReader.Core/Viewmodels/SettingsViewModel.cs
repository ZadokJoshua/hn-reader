using CommunityToolkit.Mvvm.ComponentModel;
using HNReader.Core.Constants;
using HNReader.Core.Enums;
using HNReader.Core.Interfaces;

namespace HNReader.Core.Viewmodels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private int selectedThemeIndex;

    public string[] ThemeOptions { get; } = ["Auto", "Light", "Dark"];

    public bool CanReportBug => !string.IsNullOrWhiteSpace(AppFileNames.BUG_REPORT_URL);

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
        _settingsService.Theme = (AppTheme)value;
    }
}
