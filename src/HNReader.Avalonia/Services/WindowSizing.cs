using System;
using Avalonia.Controls;

namespace HNReader.Avalonia.Services;

/// <summary>
/// Keeps a window's requested size inside the monitor it will open on.
/// </summary>
public static class WindowSizing
{
    /// <summary>
    /// Leaves a margin around the clamped size so the window never sits flush
    /// against the working-area edges on a display that only just fits it.
    /// </summary>
    private const double FitFactor = 0.94;

    /// <summary>
    /// Shrinks <paramref name="window"/> to fit the working area of its screen,
    /// leaving a smaller request untouched.
    /// </summary>
    /// <remarks>
    /// Window.Width/Height are device-independent pixels; Screen.WorkingArea is
    /// physical pixels (and already excludes the taskbar). Those two are only the
    /// same number at 100% scaling, which is what made the previous fixed
    /// 1400x900 overflow this machine: at 125% it asks for 1750x1125 physical on
    /// a 1536x864 display, putting roughly 200 logical rows below the monitor.
    ///
    /// Call this before the window is first shown. WindowStartupLocation then
    /// centres the clamped size rather than the requested one.
    /// </remarks>
    public static void ClampToWorkingArea(Window window)
    {
        // ScreenFromWindow needs a positioned window, which it is not yet, so
        // Primary is the realistic answer on first show.
        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        if (screen is null) return;

        var scaling = screen.Scaling;
        if (scaling <= 0) return;

        var maxWidth = screen.WorkingArea.Width / scaling * FitFactor;
        var maxHeight = screen.WorkingArea.Height / scaling * FitFactor;

        // Never clamp below the minimum the window declares, or it would open at
        // a size it immediately refuses to keep.
        window.Width = Math.Max(window.MinWidth, Math.Min(window.Width, maxWidth));
        window.Height = Math.Max(window.MinHeight, Math.Min(window.Height, maxHeight));
    }
}
