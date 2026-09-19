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

    [ObservableProperty]
    private bool isDigestEnabled;

    public string[] ThemeOptions { get; } = ["Auto", "Light", "Dark"];

    public bool CanReportBug => !string.IsNullOrWhiteSpace(AppFileNames.BUG_REPORT_URL);

    /// <summary>
    /// Whether a digest server address was compiled in at all. Gates the toggle
    /// the same way <see cref="CanReportBug"/> gates its button — offering a
    /// switch that cannot possibly work is worse than showing it disabled.
    /// </summary>
    public bool IsDigestConfigured => !string.IsNullOrWhiteSpace(AppFileNames.DIGEST_SERVER_BASE_URL);

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        SelectedThemeIndex = (int)_settingsService.Theme;
        IsDigestEnabled = _settingsService.IsDigestEnabled;
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        _settingsService.Theme = (AppTheme)value;
    }

    partial void OnIsDigestEnabledChanged(bool value)
    {
        _settingsService.IsDigestEnabled = value;
    }
}
