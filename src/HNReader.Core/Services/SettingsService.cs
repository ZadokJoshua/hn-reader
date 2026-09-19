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
    public event EventHandler<bool>? DigestEnabledChanged;
    public event EventHandler? SelectedDigestCategoriesChanged;

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

    public bool IsDigestEnabled
    {
        get => _settings.IsDigestEnabled;
        set
        {
            if (_settings.IsDigestEnabled != value)
            {
                _settings.IsDigestEnabled = value;
                Save();
                DigestEnabledChanged?.Invoke(this, value);
            }
        }
    }

    public IReadOnlyList<string> SelectedDigestCategories
    {
        get => _settings.SelectedDigestCategories;
        set
        {
            var incoming = value?.ToList() ?? [];

            // Order-insensitive comparison: this is a set of choices, and
            // re-saving (and re-fetching the digest) because a checkbox list was
            // enumerated in a different order would be pure churn.
            if (_settings.SelectedDigestCategories.Count == incoming.Count &&
                !_settings.SelectedDigestCategories.Except(incoming, StringComparer.OrdinalIgnoreCase).Any())
            {
                return;
            }

            _settings.SelectedDigestCategories = incoming;
            Save();
            SelectedDigestCategoriesChanged?.Invoke(this, EventArgs.Empty);
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

        // Absent from a settings.json written before the digest existed, which
        // deserializes to this initializer rather than throwing — so upgrading
        // an existing install just leaves the feature off.
        public bool IsDigestEnabled { get; set; } = false;

        // Empty means "all categories" — see ISettingsService for why.
        public List<string> SelectedDigestCategories { get; set; } = [];
    }
}
