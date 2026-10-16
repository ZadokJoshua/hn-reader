using System;
using Avalonia;
using Avalonia.Controls;

namespace HNReader.Avalonia.Controls;

/// <summary>
/// Renders one Phosphor glyph. Replaces FluentAvalonia's FontIcon.
/// </summary>
/// <remarks>
/// A TextBlock subclass rather than a TemplatedControl, deliberately: every call
/// site already sets FontSize, Foreground, Classes, Margin, alignment, ToolTip.Tip
/// and Grid.Column directly on the icon element, and inheriting TextBlock keeps all
/// of those working unchanged. Glyph, rather than reusing Text, keeps the call
/// sites reading as icons and leaves Text free as the implementation detail.
///
/// StyleKeyOverride is required: without it this control would be styled as a
/// TextBlock, so the app's own TextBlock class styles (MetaText and friends) and
/// the theme's TextBlock defaults would both leak in.
/// </remarks>
public class IconGlyph : TextBlock
{
    public static readonly StyledProperty<string?> GlyphProperty =
        AvaloniaProperty.Register<IconGlyph, string?>(nameof(Glyph));

    public string? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(IconGlyph);

    static IconGlyph() =>
        GlyphProperty.Changed.AddClassHandler<IconGlyph>(
            (icon, args) => icon.Text = args.NewValue as string);
}
