using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using System.Collections.ObjectModel;

namespace HNReader.Core.Viewmodels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private int selectedThemeIndex;

    [ObservableProperty]
    private int storyLimit;

    [ObservableProperty]
    private int maxStoriesPerDigestGroup;

    [ObservableProperty]
    private int maxStoriesPerDigestGroupIndex;

    [ObservableProperty]
    private int selectedCopilotModelIndex;

    [ObservableProperty]
    private string newInterestName = string.Empty;

    [ObservableProperty]
    private string newInterestDescription = string.Empty;

    // Character counters
    public const int MaxInterestNameLength = 30;
    public const int MaxInterestDescriptionLength = 100;

    public int InterestNameCharactersRemaining => MaxInterestNameLength - (NewInterestName?.Length ?? 0);
    public int InterestDescriptionCharactersRemaining => MaxInterestDescriptionLength - (NewInterestDescription?.Length ?? 0);

    [ObservableProperty]
    private string? vaultPath;

    [ObservableProperty]
    private string vaultDisplayName = "No vault selected";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoInterests))]
    private bool hasInterests;
    
    public bool HasNoInterests => !HasInterests;

    public ObservableCollection<Interest> UserInterests { get; } = [];

    public bool CanAddInterest => !string.IsNullOrWhiteSpace(NewInterestName) && UserInterests.Count < 5;

    public bool HasVault => !string.IsNullOrEmpty(VaultPath);

    public string[] ThemeOptions { get; } = ["Auto", "Light", "Dark"];

    public string[] MaxStoriesPerDigestGroupOptions { get; } = ["5", "10", "20", "30"];

    public string[] CopilotModelOptions { get; } =
    [
        "Claude Sonnet 4.5 (x1)",
        "Claude Opus 4.5 (x3)",
        "GPT 5.2 (x1)"
    ];

    private static readonly string[] CopilotModelValues =
    [
        "claude-sonnet-4.5",
        "claude-opus-4-5",
        "gpt-5.2"
    ];

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        SelectedThemeIndex = (int)_settingsService.Theme;
        StoryLimit = _settingsService.StoryLimit;
        MaxStoriesPerDigestGroup = _settingsService.MaxStoriesPerDigestGroup;
        MaxStoriesPerDigestGroupIndex = (_settingsService.MaxStoriesPerDigestGroup) switch
        {
            5 => 0,
            10 => 1,
            20 => 2,
            30 => 3,
            _ => 0
        };
        SelectedCopilotModelIndex = ResolveModelIndex(_settingsService.CopilotModel);
        VaultPath = _settingsService.VaultPath;
        UpdateVaultDisplay();

        UserInterests.Clear();
        foreach (var interest in _settingsService.UserInterests)
        {
            UserInterests.Add(interest);
        }
        
        UpdateInterestState();
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        _settingsService.Theme = (AppTheme)value;
    }

    partial void OnStoryLimitChanged(int value)
    {
        _settingsService.StoryLimit = value;
    }

    partial void OnMaxStoriesPerDigestGroupChanged(int value)
    {
        _settingsService.MaxStoriesPerDigestGroup = value;
    }

    partial void OnMaxStoriesPerDigestGroupIndexChanged(int value)
    {
        MaxStoriesPerDigestGroup = value switch
        {
            0 => 5,
            1 => 10,
            2 => 20,
            3 => 30,
            _ => 5
        };
    }

    partial void OnSelectedCopilotModelIndexChanged(int value)
    {
        if (value < 0 || value >= CopilotModelValues.Length)
        {
            return;
        }

        _settingsService.CopilotModel = CopilotModelValues[value];
    }

    partial void OnVaultPathChanged(string? value)
    {
        _settingsService.VaultPath = value;
        UpdateVaultDisplay();
    }

    partial void OnNewInterestNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanAddInterest));
        OnPropertyChanged(nameof(InterestNameCharactersRemaining));
    }

    partial void OnNewInterestDescriptionChanged(string value)
    {
        OnPropertyChanged(nameof(InterestDescriptionCharactersRemaining));
    }

    private void UpdateVaultDisplay()
    {
        if (string.IsNullOrEmpty(VaultPath))
        {
            VaultDisplayName = "No vault selected";
        }
        else
        {
            var name = Path.GetFileName(VaultPath);
            VaultDisplayName = string.IsNullOrEmpty(name) ? VaultPath : name;
        }
        OnPropertyChanged(nameof(HasVault));
    }

    private void UpdateInterestState()
    {
        HasInterests = UserInterests.Count > 0;
    }

    [RelayCommand]
    public void AddInterest()
    {
        if (!CanAddInterest)
            return;

        var newInterest = new Interest
        {
            Name = NewInterestName.Trim(),
            Description = NewInterestDescription.Trim()
        };

        UserInterests.Add(newInterest);
        SaveInterests();

        NewInterestName = string.Empty;
        NewInterestDescription = string.Empty;
        OnPropertyChanged(nameof(CanAddInterest));
        UpdateInterestState();
    }

    [RelayCommand]
    public void RemoveInterest(Interest interest)
    {
        UserInterests.Remove(interest);
        SaveInterests();
        OnPropertyChanged(nameof(CanAddInterest));
        UpdateInterestState();
    }

    [RelayCommand]
    public void ClearVault()
    {
        VaultPath = null;
    }

    public void SetVaultPath(string path)
    {
        VaultPath = path;
    }

    private void SaveInterests()
    {
        _settingsService.UserInterests = [.. UserInterests];
    }

    private static int ResolveModelIndex(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return 0;
        }

        var index = Array.IndexOf(CopilotModelValues, model);
        return index >= 0 ? index : 0;
    }
}
