using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;

namespace HNReader.Core.Viewmodels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private int selectedThemeIndex;

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
        _settingsService.Theme = (AppTheme)value;
    }

    [RelayCommand]
    private void ResetTheme()
    {
        SelectedThemeIndex = (int)AppTheme.Auto;
    }
}
