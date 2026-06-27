using HNReader.Core.Constants;
using HNReader.Core.Enums;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using System.Text.Json;

namespace HNReader.Core.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private SettingsData _settings;

    public event EventHandler<AppTheme>? ThemeChanged;

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

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, CoreHelper.JsonSerializerOptions);
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

    private class SettingsData
    {
        public AppTheme Theme { get; set; } = AppTheme.Auto;
    }
}
