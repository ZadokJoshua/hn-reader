using HNReader.Core.Enums;

namespace HNReader.Core.Interfaces;

public interface ISettingsService
{
    AppTheme Theme { get; set; }

    event EventHandler<AppTheme>? ThemeChanged;

    void Save();
    void Load();
}
