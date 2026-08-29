using HNReader.Core.Constants;
using HNReader.Core.Enums;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;
using HNReader.Core.Services.Logging;
using System.Text.Json;

namespace HNReader.Core.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly ILogger? _logger;
    private SettingsData _settings;

    public event EventHandler<AppTheme>? ThemeChanged;

    public SettingsService(string settingsDirectory, ILogger? logger = null)
    {
        _settingsFilePath = Path.Combine(settingsDirectory, AppFileNames.SETTINGS_FILE_NAME);
        _logger = logger;
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
            _logger?.LogDebug("Settings", "settings saved", context: null);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Settings", "failed to save settings", ex);
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
            _logger?.LogWarning("Settings", "failed to load settings; using defaults", ex);
            _settings = new SettingsData();
        }
    }

    private class SettingsData
    {
        public AppTheme Theme { get; set; } = AppTheme.Auto;
    }
}
