using HNReader.Core.Constants;
using HNReader.Core.Enums;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Models;
using System.Text.Json;

namespace HNReader.Core.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private SettingsData _settings;

    public event EventHandler<AppTheme>? ThemeChanged;
    public event EventHandler<string?>? VaultChanged;
    public event EventHandler<List<Interest>>? UserInterestsChanged;

    public SettingsService(string settingsDirectory)
    {
        _settingsFilePath = Path.Combine(settingsDirectory, AppFileNames.SETTINGS_FILE_NAME);
        _settings = new SettingsData();
        Load();
    }

    public AppTheme Theme
    {
        get => _settings.Theme;
        set
        {
            if (_settings.Theme != value)
            {
                _settings.Theme = value;
                Save();
                ThemeChanged?.Invoke(this, value);
            }
        }
    }

    public int StoryLimit
    {
        get => _settings.StoryLimit;
        set
        {
            var clampedValue = Math.Clamp(value, 10, 100);
            if (_settings.StoryLimit != clampedValue)
            {
                _settings.StoryLimit = clampedValue;
                Save();
            }
        }
    }

    public List<Interest> UserInterests
    {
        get => _settings.UserInterests;
        set
        {
            _settings.UserInterests = value;
            Save();
            UserInterestsChanged?.Invoke(this, value);
        }
    }

    public int MaxStoriesPerDigestGroup
    {
        get => _settings.MaxStoriesPerDigestGroup;
        set
        {
            var clampedValue = Math.Clamp(value, 1, 30);
            if (_settings.MaxStoriesPerDigestGroup != clampedValue)
            {
                _settings.MaxStoriesPerDigestGroup = clampedValue;
                Save();
            }
        }
    }

    public string CopilotModel
    {
        get => _settings.CopilotModel;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!string.Equals(_settings.CopilotModel, value, StringComparison.Ordinal))
            {
                _settings.CopilotModel = value;
                Save();
            }
        }
    }

    public string? VaultPath
    {
        get => _settings.VaultPath;
        set
        {
            if (_settings.VaultPath != value)
            {
                _settings.VaultPath = value;
                Save();
                VaultChanged?.Invoke(this, value);
            }
        }
    }

    public string? VaultName => string.IsNullOrEmpty(VaultPath) 
        ? null 
        : Path.GetFileName(VaultPath);

    public bool HasVault => !string.IsNullOrEmpty(VaultPath) && Directory.Exists(VaultPath);

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                _settings = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            _settings = new SettingsData();
        }
    }

    public string GetKnowledgeBasePath(KnowledgeBaseFolders folder)
    {
        if (!HasVault) throw new InvalidOperationException("Vault is not set.");

        var folderName = folder.GetDescription();
        var knowledgeBasePath = Path.Combine(VaultPath!, AppFolderNames.KNOWLEDGE_BASE_FOLDER, folderName);
        Directory.CreateDirectory(knowledgeBasePath);
        return knowledgeBasePath;
    }

    private class SettingsData
    {
        public AppTheme Theme { get; set; } = AppTheme.Auto;
        public int StoryLimit { get; set; } = 20;
        public List<Interest> UserInterests { get; set; } = [];
        public string? VaultPath { get; set; }
        public int MaxStoriesPerDigestGroup { get; set; } = 5;
        public string CopilotModel { get; set; } = "claude-sonnet-4.5";
    }
}
