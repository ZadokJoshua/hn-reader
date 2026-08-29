using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;

namespace HNReader.Avalonia.Services;

/// <summary>
/// Thin wrapper around Avalonia's built-in <see cref="WindowNotificationManager"/>,
/// replacing the app's old hand-rolled copy-feedback banner with proper toast
/// notifications. Needs a TopLevel to attach to, so it lives here rather than
/// in HNReader.Core.
/// </summary>
public class NotificationService
{
    private WindowNotificationManager? _manager;

    public void Attach(Window window)
    {
        _manager = new WindowNotificationManager(window)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 3
        };
    }

    public void ShowInfo(string message, string title = "HN Reader")
        => Show(title, message, NotificationType.Information);

    public void ShowSuccess(string message, string title = "HN Reader")
        => Show(title, message, NotificationType.Success);

    public void ShowError(string message, string title = "HN Reader")
        => Show(title, message, NotificationType.Error);

    private void Show(string title, string message, NotificationType type)
    {
        _manager?.Show(new Notification(title, message, type, TimeSpan.FromSeconds(2.5)));
    }
}
