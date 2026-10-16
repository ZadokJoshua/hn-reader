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

    public bool ConfirmCommentLinks
    {
        get => _settings.ConfirmCommentLinks;
        set
        {
            if (_settings.ConfirmCommentLinks != value)
            {
                _settings.ConfirmCommentLinks = value;
                Save();
            }
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, CoreHelper.JsonSerializerOptions);

            // Write to a sibling temp file and swap it in, so a crash or power loss
            // mid-write cannot leave a truncated settings.json (which Load would
            // then discard, silently resetting the user's preferences).
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
            var tempPath = _settingsFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _settingsFilePath, overwrite: true);
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

                // A hand-edited or newer-version file can hold a number that is not
                // a member of the enum; fall back rather than carry it into the UI.
                if (!Enum.IsDefined(_settings.Theme))
                {
                    _logger?.LogWarning("Settings", "unknown theme value; using default", context: null);
                    _settings.Theme = AppTheme.Auto;
                }
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

        // Defaults to true so a settings file written before this existed still prompts.
        public bool ConfirmCommentLinks { get; set; } = true;
    }
}
