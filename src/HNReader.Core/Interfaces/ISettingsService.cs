using HNReader.Core.Enums;

namespace HNReader.Core.Interfaces;

public interface ISettingsService
{
    AppTheme Theme { get; set; }

    /// <summary>Ask before opening a link found inside a comment. On by default.</summary>
    bool ConfirmCommentLinks { get; set; }

    event EventHandler<AppTheme>? ThemeChanged;

    void Save();
    void Load();
}
