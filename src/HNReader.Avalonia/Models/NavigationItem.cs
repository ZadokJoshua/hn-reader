using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using HNReader.Core.Enums;

namespace HNReader.Avalonia.Models;

/// <summary>
/// One entry in the navigation pane.
/// </summary>
public partial class NavigationItem : ObservableObject
{
    public ApplicationPages Page { get; }

    public string Label { get; }

    /// <summary>
    /// The Phosphor codepoint, already resolved from its resource key.
    /// </summary>
    /// <remarks>
    /// Resolved once here rather than left as a key for the item template to look
    /// up: a StaticResource cannot be used from a data-bound template, and a
    /// DynamicResource cannot take its key from a binding. The glyph set is
    /// static, so resolving at construction costs nothing.
    /// </remarks>
    public string Glyph { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isVisible = true;

    public NavigationItem(ApplicationPages page, string label, string glyphKey)
    {
        Page = page;
        Label = label;
        Glyph = ResolveGlyph(glyphKey);
    }

    private static string ResolveGlyph(string glyphKey)
    {
        var app = Application.Current;
        if (app is not null
            && app.TryGetResource(glyphKey, app.ActualThemeVariant, out var value)
            && value is string glyph)
        {
            return glyph;
        }

        // A missing glyph renders as an empty slot rather than throwing; the
        // label still identifies the item.
        return string.Empty;
    }
}
