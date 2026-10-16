using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace HNReader.Avalonia.Controls;

/// <summary>
/// The app mark: a newspaper page carrying the HN wordmark. Doubles as the busy
/// indicator, its body lines filling in one after another while
/// <see cref="IsAnimating"/> is set.
/// </summary>
/// <remarks>
/// Deliberately a newspaper rather than the Hacker News mark: that one is Y
/// Combinator's, and this is an unofficial reader. The page, the lines and the two
/// letters are all generic shapes.
///
/// Used for the page-scale loaders. The 12-14px indicators that sit inline beside
/// text keep <see cref="BusyRing"/> instead - a newspaper at that size is a smudge,
/// which is why the ring stays in the codebase rather than being replaced outright.
/// </remarks>
public partial class HnLogo : UserControl
{
    private const string FillingClass = "filling";

    public static readonly StyledProperty<bool> IsAnimatingProperty =
        AvaloniaProperty.Register<HnLogo, bool>(nameof(IsAnimating), defaultValue: false);

    /// <summary>
    /// Runs the line-filling animation. Left off by default so the mark can be used
    /// as a plain logo without opting out of motion.
    /// </summary>
    public bool IsAnimating
    {
        get => GetValue(IsAnimatingProperty);
        set => SetValue(IsAnimatingProperty, value);
    }

    public HnLogo()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateAnimation();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateAnimation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsAnimatingProperty || change.Property == IsVisibleProperty)
        {
            UpdateAnimation();
        }
    }

    /// <summary>
    /// Adds the class carrying the animations only while the logo is actually on
    /// screen and animating, so hidden instances cost nothing.
    /// </summary>
    /// <remarks>
    /// Same gate as <see cref="BusyRing"/>, and for the same reason: these sit in
    /// list footers and page overlays that are collapsed most of the time, and
    /// Avalonia keeps running an infinite animation on a control that is merely
    /// invisible.
    /// </remarks>
    private void UpdateAnimation()
    {
        var shouldAnimate = IsAnimating && IsVisible && this.GetVisualRoot() is not null;

        if (shouldAnimate)
        {
            if (!PART_Page.Classes.Contains(FillingClass)) PART_Page.Classes.Add(FillingClass);
        }
        else
        {
            PART_Page.Classes.Remove(FillingClass);
        }
    }
}
