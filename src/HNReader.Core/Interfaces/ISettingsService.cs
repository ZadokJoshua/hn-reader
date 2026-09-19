using HNReader.Core.Enums;

namespace HNReader.Core.Interfaces;

public interface ISettingsService
{
    AppTheme Theme { get; set; }

    /// <summary>
    /// Whether the daily digest feature is on. Opt-in and off by default: the
    /// digest is served by HNReader.Server, which the user may not be running,
    /// so leaving it off means the app makes no requests to it at all.
    /// </summary>
    bool IsDigestEnabled { get; set; }

    /// <summary>
    /// The digest categories the user wants to see, by name.
    /// <para>
    /// An <b>empty list means "all of them"</b>, not "none". That keeps a fresh
    /// install showing the whole digest without needing a migration to
    /// pre-populate every category name, and it means a category added to the
    /// server taxonomy later shows up automatically rather than being invisible
    /// because it wasn't in a list written before it existed.
    /// </para>
    /// </summary>
    IReadOnlyList<string> SelectedDigestCategories { get; set; }

    event EventHandler<AppTheme>? ThemeChanged;

    event EventHandler<bool>? DigestEnabledChanged;

    event EventHandler? SelectedDigestCategoriesChanged;

    void Save();
    void Load();
}
