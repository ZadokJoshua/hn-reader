using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HNReader.Avalonia.Controls;

/// <summary>
/// Severity of a <see cref="StatusBanner"/>. Replaces FluentAvalonia's InfoBarSeverity.
/// </summary>
public enum StatusSeverity
{
    Informational,
    Success,
    Warning,
    Error
}

/// <summary>
/// Inline status message with a severity colour and an optional dismiss button.
/// Replaces FluentAvalonia's InfoBar.
/// </summary>
/// <remarks>
/// The property surface (Severity/Title/Message/IsOpen/IsClosable) mirrors InfoBar
/// exactly so the settings page's ShowStatus helpers did not have to change shape.
/// </remarks>
public partial class StatusBanner : UserControl
{
    public static readonly StyledProperty<StatusSeverity> SeverityProperty =
        AvaloniaProperty.Register<StatusBanner, StatusSeverity>(nameof(Severity));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<StatusBanner, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<StatusBanner, string?>(nameof(Message));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<StatusBanner, bool>(nameof(IsOpen));

    public static readonly StyledProperty<bool> IsClosableProperty =
        AvaloniaProperty.Register<StatusBanner, bool>(nameof(IsClosable), defaultValue: true);

    public StatusSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public StatusBanner()
    {
        InitializeComponent();
        ApplySeverity();
        PART_Close.Click += OnCloseClick;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SeverityProperty)
        {
            ApplySeverity();
        }
        else if (change.Property == IsOpenProperty && change.NewValue is bool open)
        {
            // Removed on close so the next open starts the entrance animation afresh.
            PART_Root.Classes.Set("opened", open && !HNReader.Avalonia.Services.MotionPreference.ReducedMotion);
        }
    }

    private void ApplySeverity()
    {
        PseudoClasses.Set(":success", Severity == StatusSeverity.Success);
        PseudoClasses.Set(":warning", Severity == StatusSeverity.Warning);
        PseudoClasses.Set(":error", Severity == StatusSeverity.Error);

        PART_Icon.Glyph = Severity switch
        {
            StatusSeverity.Success => FindGlyph("IconSuccess"),
            StatusSeverity.Warning or StatusSeverity.Error => FindGlyph("IconWarning"),
            _ => FindGlyph("IconInfo")
        };
    }

    private string? FindGlyph(string key) =>
        Application.Current is { } app
        && app.TryGetResource(key, app.ActualThemeVariant, out var value)
            ? value as string
            : null;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => IsOpen = false;
}
