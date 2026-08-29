using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using FluentAvalonia.UI.Controls;

namespace HNReader.Avalonia.Services;

/// <summary>
/// Centralized error dialog, mirroring HNReader.WinUI.Services.ErrorDialogService but
/// built on FluentAvalonia's ContentDialog.
/// </summary>
public static class ErrorDialogService
{
    public static async Task ShowErrorAsync(
        string title,
        string message,
        Window? owner = null,
        string? logFilePath = null)
    {
        try
        {
            var window = owner ?? GetMainWindow();
            if (window is null) return;

            var scroll = new ScrollViewer
            {
                MaxHeight = 300,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                }
            };

            var dialog = new ContentDialog
            {
                Title = title,
                Content = scroll,
                PrimaryButtonText = "Copy Details",
                SecondaryButtonText = logFilePath is null ? null : "Open log folder",
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
            };

            var result = await dialog.ShowAsync(window);

            if (result == ContentDialogResult.Primary)
            {
                var payload = logFilePath is null
                    ? $"{title}\n\n{message}"
                    : $"{title}\n\n{message}\n\nLog file: {logFilePath}";

                var clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(payload);
                }
            }
            else if (result == ContentDialogResult.Secondary && logFilePath is not null)
            {
                TryOpenInExplorer(System.IO.Path.GetDirectoryName(logFilePath));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ErrorDialogService failed to show dialog: {ex}");
        }
    }

    private static void TryOpenInExplorer(string? folder)
    {
        if (string.IsNullOrEmpty(folder)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ErrorDialogService could not open log folder '{folder}': {ex.Message}");
        }
    }

    private static Window? GetMainWindow()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }
}
