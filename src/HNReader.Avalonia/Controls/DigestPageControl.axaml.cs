using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using HNReader.Avalonia.Services;
using HNReader.Core.Enums;
using HNReader.Core.Models;
using HNReader.Core.Viewmodels;
using Microsoft.Extensions.DependencyInjection;

namespace HNReader.Avalonia.Controls;

/// <summary>
/// The digest page's interactions. Handlers follow the same idioms as
/// <see cref="StoriesPageControl"/>: the bound item arrives via the control's
/// Tag, links open through <see cref="UrlOpener"/>, and feedback goes through
/// the shared <see cref="NotificationService"/>.
/// </summary>
public partial class DigestPageControl : UserControl
{
    public DigestPageControl()
    {
        InitializeComponent();
    }

    private static NotificationService? ResolveNotifications() =>
        (global::Avalonia.Application.Current as App)?.Services.GetService<NotificationService>();

    /// <summary>
    /// A checkbox in the category picker changed. The binding already updated the
    /// option; this only tells the ViewModel to re-evaluate the picker's label and
    /// whether Apply should be enabled, which are computed across the whole list.
    /// </summary>
    private void OnCategoryOptionToggled(object? sender, RoutedEventArgs e)
    {
        (DataContext as DigestPageViewModel)?.NotifyFilterChanged();
    }

    /// <summary>
    /// Closes the picker after Apply. The flyout has no idea its button did
    /// anything, so it would otherwise stay open over the reloading page.
    /// </summary>
    private void OnCloseCategoryFlyout(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            FlyoutBase.GetAttachedFlyout(control)?.Hide();

            // The Apply button lives inside a Flyout opened from another button,
            // so the attached-flyout lookup above finds nothing — walk up to the
            // popup host and close that instead.
            var popup = control.FindAncestorOfType<Popup>();
            if (popup is not null) popup.IsOpen = false;
        }
    }

    private void OnToggleNoteClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.Tag is DigestItem item)
        {
            item.IsNoteExpanded = !item.IsNoteExpanded;
        }
    }

    private void OnOpenArticleClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.Tag is DigestItem item)
        {
            UrlOpener.TryOpen(item.PrimaryUrl);
        }
    }

    private void OnOpenHnClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.Tag is DigestItem item)
        {
            UrlOpener.TryOpen(item.HackerNewsUrl);
        }
    }

    private async void OnCopyLinkClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Control control || control.Tag is not DigestItem item) return;

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;

            await clipboard.SetTextAsync(item.PrimaryUrl);
            ResolveNotifications()?.ShowSuccess("Link copied to clipboard");
        }
        catch (Exception)
        {
            // An async void handler must never let an exception escape — it would
            // reach the dispatcher's unhandled path and take the app down over a
            // failed clipboard write.
            ResolveNotifications()?.ShowError("Could not copy the link");
        }
    }

    /// <summary>
    /// Shown only by the disabled state, which the user reaches by turning the
    /// feature off while this page is open.
    /// </summary>
    private void OnOpenSettingsClicked(object? sender, RoutedEventArgs e)
    {
        (global::Avalonia.Application.Current as App)?.Services
            .GetService<NavigationService>()?
            .NavigateToPage(ApplicationPages.Settings);
    }
}
