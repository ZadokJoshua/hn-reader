using System;
using Avalonia.Controls;
using Avalonia.Threading;
using HNReader.Avalonia.Factories;
using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Viewmodels;

namespace HNReader.Avalonia.Services;

/// <summary>
/// Avalonia has no Frame/Page navigation stack, so this manually swaps the
/// Content of a ContentControl instead — mirroring the WinUI NavigationService's
/// Frame.Content assignment pattern.
/// </summary>
public class NavigationService(PageFactory pageFactory)
{
    /// <summary>
    /// How long the outgoing page stays alive after being replaced.
    /// </summary>
    /// <remarks>
    /// Must be at least the host's page-transition duration. The host is a
    /// TransitioningContentControl, which keeps the previous visual in the tree
    /// and rendering for the whole animation — so tearing its ViewModel down
    /// immediately (as this used to) makes it fade out as a blank rectangle.
    /// This and the transition duration in MainWindow.axaml are one setting
    /// expressed in two files; change them together.
    /// </remarks>
    public TimeSpan TeardownDelay { get; set; } = TimeSpan.FromMilliseconds(260);

    private ContentControl? _host;

    public event Action<ApplicationPages>? Navigated;

    public void Initialize(ContentControl host) => _host = host;

    public void NavigateToPage(ApplicationPages page)
    {
        if (_host == null) return;

        var previousPage = _host.Content as UserControl;

        var pageInstance = pageFactory.GetPage(page);
        _host.Content = pageInstance;

        Navigated?.Invoke(page);

        var viewModel = pageFactory.GetPageViewModel(page);
        pageInstance.DataContext = viewModel;

        // else-if, never a second if: a ViewModel must not be able to load twice
        // by satisfying both branches.
        if (viewModel is PageViewModel pageViewModel) _ = pageViewModel.PopulateListAsync();
        else if (viewModel is IInitializableViewModel initializable) _ = initializable.InitializeAsync();

        if (previousPage != null) ScheduleTeardown(previousPage);
    }

    private void ScheduleTeardown(UserControl previousPage) =>
        DispatcherTimer.RunOnce(() =>
        {
            // Navigating back inside the delay window makes this page current
            // again before the timer fires. Views are transient so the instances
            // differ, but SettingsViewModel is a singleton shared across
            // navigations — disposing it out from
            // under the live page would be the worst kind of intermittent bug,
            // so check the ViewModel as well as the view.
            if (ReferenceEquals(previousPage, _host?.Content)) return;

            var viewModel = previousPage.DataContext;
            if (viewModel is null) return;
            if (ReferenceEquals(viewModel, (_host?.Content as UserControl)?.DataContext)) return;

            if (viewModel is IDisposable disposable) disposable.Dispose();
            previousPage.DataContext = null;
        }, TeardownDelay);
}
