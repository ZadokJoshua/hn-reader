using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace HNReader.Avalonia.Controls;

/// <summary>
/// Indeterminate circular busy indicator. Replaces FluentAvalonia's ProgressRing.
/// </summary>
/// <remarks>
/// Deliberately a ring rather than an indeterminate ProgressBar: half the call
/// sites are 12-20px and sit inline in a horizontal StackPanel beside a line of
/// text, where a horizontal bar at that size is an illegible smear.
/// </remarks>
public partial class BusyRing : UserControl
{
    private const string SpinningClass = "spinning";

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<BusyRing, bool>(nameof(IsActive), defaultValue: true);

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public BusyRing()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSpin();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateSpin();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsActiveProperty || change.Property == IsVisibleProperty)
        {
            UpdateSpin();
        }
        else if (change.Property == BoundsProperty)
        {
            // Scaled from the control size rather than exposed as a property: the
            // call sites range from 12px to 48px and all of them already say how
            // big they are, so a separate thickness at each would be one more
            // number to keep in step for no gain.
            PART_Arc.StrokeThickness = Math.Max(1.5, Bounds.Width / 11);
        }
    }

    /// <summary>
    /// Adds the class that carries the spin animation only while the ring is
    /// actually on screen and active, so hidden rings cost nothing.
    /// </summary>
    private void UpdateSpin()
    {
        var shouldSpin = IsActive && IsVisible && this.GetVisualRoot() is not null;

        if (shouldSpin)
        {
            if (!PART_Arc.Classes.Contains(SpinningClass)) PART_Arc.Classes.Add(SpinningClass);
        }
        else
        {
            PART_Arc.Classes.Remove(SpinningClass);
        }
    }
}
