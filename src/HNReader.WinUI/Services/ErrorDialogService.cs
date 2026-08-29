using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HNReader.WinUI.Services;

/// <summary>
/// Provides a centralized way to show error dialogs to the user.
/// Used by the global exception handler and can be reused throughout the app.
/// </summary>
public static class ErrorDialogService
{
    /// <summary>
    /// Shows an error dialog with the given title and message.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Dialog body text.</param>
    /// <param name="xamlRoot">Optional XamlRoot; falls back to the current window's root.</param>
    /// <param name="logFilePath">Optional path to the active log file. When supplied, the
    /// dialog gains an "Open log folder" secondary button and the "Copy details" payload
    /// includes the log path so the user can attach it to a bug report.</param>
    public static async Task ShowErrorAsync(
        string title,
        string message,
        XamlRoot? xamlRoot = null,
        string? logFilePath = null)
    {
        try
        {
            var root = xamlRoot ?? GetXamlRoot();
            if (root is null) return;

            var dialog = new ContentDialog
            {
                Title = title,
                Content = new ScrollViewer
                {
                    MaxHeight = 300,
                    Content = new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    }
                },
                PrimaryButtonText = "Copy Details",
                SecondaryButtonText = logFilePath is null ? null : "Open log folder",
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = root
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var payload = logFilePath is null
                    ? $"{title}\n\n{message}"
                    : $"{title}\n\n{message}\n\nLog file: {logFilePath}";
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(payload);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
            else if (result == ContentDialogResult.Secondary && logFilePath is not null)
            {
                TryOpenInExplorer(System.IO.Path.GetDirectoryName(logFilePath));
            }
        }
        catch (Exception ex)
        {
            // Last resort — if even the error dialog fails, write to debug output
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

    private static XamlRoot? GetXamlRoot()
    {
        return App.CurrentWindow?.Content?.XamlRoot;
    }
}
