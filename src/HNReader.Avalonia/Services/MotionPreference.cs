using System;
using System.Runtime.InteropServices;

namespace HNReader.Avalonia.Services;

/// <summary>
/// Whether the user has asked the OS to reduce animation. Avalonia 11.3 exposes no
/// such flag, so on Windows this reads "Show animations in Windows" directly; other
/// platforms report false and keep the (short, subtle) animations.
/// </summary>
public static class MotionPreference
{
    private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;

    private static readonly Lazy<bool> _reduced = new(Detect);

    public static bool ReducedMotion => _reduced.Value;

    private static bool Detect()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            return SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, out var enabled, 0) && !enabled;
        }
        catch
        {
            // Never let a preference lookup affect startup; default to animating.
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam,
        [MarshalAs(UnmanagedType.Bool)] out bool pvParam, uint fWinIni);
}
