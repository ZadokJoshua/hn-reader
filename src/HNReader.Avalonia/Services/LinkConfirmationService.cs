using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using HNReader.Avalonia.Controls;
using HNReader.Core.Helpers;
using HNReader.Core.Interfaces;

namespace HNReader.Avalonia.Services;

/// <summary>
/// Shows where a comment link really goes before the browser opens it. Comment text is
/// written by strangers and a link's visible text can differ from its target, so the
/// destination host is spelled out. Story buttons do not use this: the user pressed a
/// button that already says what it does.
/// </summary>
public sealed class LinkConfirmationService
{
    private readonly ISettingsService _settings;
    private readonly NotificationService _notifications;
    private bool _dialogOpen;

    public LinkConfirmationService(ISettingsService settings, NotificationService notifications)
    {
        _settings = settings;
        _notifications = notifications;
    }

    public async Task OpenAsync(string? url)
    {
        if (!LinkHelper.TryGetSafeWebUri(url, out var uri))
        {
            _notifications.ShowError("That link can't be opened from here.");
            return;
        }

        if (!_settings.ConfirmCommentLinks)
        {
            Launch(uri);
            return;
        }

        // A double-click on a link must not stack two dialogs.
        if (_dialogOpen) return;

        var owner = (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null)
        {
            // Nothing to attach a dialog to; the safest fallback is not to open silently.
            return;
        }

        var dontAsk = new CheckBox
        {
            Content = "Don't ask me again",
            Margin = new global::Avalonia.Thickness(0, 16, 0, 0)
        };

        var content = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "This link will open in your web browser:", TextWrapping = TextWrapping.Wrap },
                new SelectableTextBlock
                {
                    Text = Shorten(uri.AbsoluteUri),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    MaxWidth = 460,
                    HorizontalAlignment = HorizontalAlignment.Left
                },
                dontAsk
            }
        };

        _dialogOpen = true;
        try
        {
            // Cancel is the dialog's default button, so pressing Enter by reflex does not open the link.
            var result = await AppDialogWindow.ShowAsync(
                owner, "Open link in your browser?", content,
                primaryText: "Open link", closeText: "Cancel", showHeading: false);

            if (result != DialogResult.Primary) return;

            if (dontAsk.IsChecked == true)
            {
                _settings.ConfirmCommentLinks = false;
            }

            Launch(uri);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private void Launch(Uri uri)
    {
        if (!UrlOpener.TryOpen(uri.AbsoluteUri))
        {
            _notifications.ShowError("Couldn't open the link in your browser.");
        }
    }

    private static string Shorten(string url) => url.Length <= 200 ? url : url[..200] + "...";
}
