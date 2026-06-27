using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
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
    /// Returns the dialog result so callers can check if the user copied details.
    /// </summary>
    public static async Task ShowErrorAsync(string title, string message, XamlRoot? xamlRoot = null)
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
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = root
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText($"{title}\n\n{message}");
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
        }
        catch (Exception ex)
        {
            // Last resort — if even the error dialog fails, write to debug output
            System.Diagnostics.Debug.WriteLine($"ErrorDialogService failed to show dialog: {ex}");
        }
    }

    private static XamlRoot? GetXamlRoot()
    {
        return App.CurrentWindow?.Content?.XamlRoot;
    }
}
