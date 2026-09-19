using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using HNReader.Avalonia.Controls;
using HNReader.Avalonia.Services;
using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Viewmodels;
using Microsoft.Extensions.DependencyInjection;

namespace HNReader.Avalonia;

public partial class MainWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private NavigationService? _navigationService;
    private ISettingsService? _settingsService;
    private bool _suppressNavSelection;

    // Tracked so turning the digest off while its page is open can navigate away
    // from it. OnPageNavigated already fires for every navigation.
    private ApplicationPages _currentPage = ApplicationPages.Top;

    // FluentAvalonia's NavigationView can apply its own default item
    // selection asynchronously while it finishes loading, which races with
    // (and can silently override) the app's initial navigation. Until our
    // deliberate initial navigation has run, any SelectionChanged is treated
    // as internal noise and ignored rather than acted on.
    private bool _initialNavigationDone;

    // Parameterless constructor kept for the Avalonia XAML previewer only.
    public MainWindow() : this(null!)
    {
    }

    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        DataContext = _mainViewModel;

        if (global::Avalonia.Application.Current is App app && app.Services != null)
        {
            _navigationService = app.Services.GetService<NavigationService>();
            if (_navigationService != null)
            {
                _navigationService.Initialize(ContentHost);
                _navigationService.Navigated += OnPageNavigated;
            }

            app.Services.GetService<NotificationService>()?.Attach(this);

            // The digest nav item is the feature's first off-switch (the second
            // is in DigestPageViewModel, which makes no request when disabled).
            // Handled here rather than on MainViewModel, which is shared with
            // the WinUI shell and constructed by an explicit factory there — a
            // new required dependency would break that build.
            _settingsService = app.Services.GetService<ISettingsService>();
            if (_settingsService != null)
            {
                ApplyDigestVisibility(_settingsService.IsDigestEnabled);
                _settingsService.DigestEnabledChanged += OnDigestEnabledChanged;

                // The settings service is a singleton that outlives this window.
                Closed += (_, _) => _settingsService.DigestEnabledChanged -= OnDigestEnabledChanged;
            }
        }

        NavView.SelectionChanged += NavView_SelectionChanged;
        KeyDown += OnKeyDown;

        SetPagesTags();

        // Run after the current layout/loaded pass so it lands after any
        // internal default-selection NavigationView performs on its own.
        Dispatcher.UIThread.Post(RunInitialNavigation, DispatcherPriority.Background);
    }

    private void RunInitialNavigation()
    {
        SelectNavItemForPage(ApplicationPages.Top);
        _navigationService?.NavigateToPage(ApplicationPages.Top);
        _initialNavigationDone = true;
    }

    private void OnPageNavigated(ApplicationPages page)
    {
        _currentPage = page;
        SelectNavItemForPage(page);
    }

    private void OnDigestEnabledChanged(object? sender, bool enabled) =>
        Dispatcher.UIThread.Post(() => ApplyDigestVisibility(enabled));

    private void ApplyDigestVisibility(bool enabled)
    {
        DigestPageNavItem.IsVisible = enabled;

        // Don't leave the user stranded on a page they just switched off.
        if (!enabled && _currentPage == ApplicationPages.Digest)
        {
            _navigationService?.NavigateToPage(ApplicationPages.New);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control) return;

        // Ordered to match the nav pane top to bottom, so the number a user counts
        // down the sidebar is the number they press. (These previously ran New-first
        // and had Digest and Favourites transposed against the visible order.)
        var page = e.Key switch
        {
            Key.D1 or Key.NumPad1 => ApplicationPages.Top,
            Key.D2 or Key.NumPad2 => ApplicationPages.New,
            Key.D3 or Key.NumPad3 => ApplicationPages.Best,
            Key.D4 or Key.NumPad4 => ApplicationPages.Show,
            Key.D5 or Key.NumPad5 => ApplicationPages.Ask,
            Key.D6 or Key.NumPad6 => ApplicationPages.Digest,
            Key.D7 or Key.NumPad7 => ApplicationPages.Favourites,
            _ => (ApplicationPages?)null
        };

        // The shortcut is gated on the setting too, so it can't reach a page
        // whose nav item is hidden.
        if (page == ApplicationPages.Digest && _settingsService?.IsDigestEnabled != true)
        {
            return;
        }

        if (page != null)
        {
            _navigationService?.NavigateToPage(page.Value);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F)
        {
            var storiesControl = (ContentHost.Content as ContentControl)?.Content as StoriesPageControl;
            storiesControl?.FocusSearchBox();
            e.Handled = true;
        }
    }

    private void SetPagesTags()
    {
        NewPageNavItem.Tag = ApplicationPages.New.ToString();
        TopPageNavItem.Tag = ApplicationPages.Top.ToString();
        BestPageNavItem.Tag = ApplicationPages.Best.ToString();
        ShowPageNavItem.Tag = ApplicationPages.Show.ToString();
        AskPageNavItem.Tag = ApplicationPages.Ask.ToString();
        FavouritesPageNavItem.Tag = ApplicationPages.Favourites.ToString();
        DigestPageNavItem.Tag = ApplicationPages.Digest.ToString();
        SettingsPageNavItem.Tag = ApplicationPages.Settings.ToString();
    }

    private void SelectNavItemForPage(ApplicationPages page)
    {
        var target = page switch
        {
            ApplicationPages.New => NewPageNavItem,
            ApplicationPages.Top => TopPageNavItem,
            ApplicationPages.Best => BestPageNavItem,
            ApplicationPages.Show => ShowPageNavItem,
            ApplicationPages.Ask => AskPageNavItem,
            ApplicationPages.Favourites => FavouritesPageNavItem,
            ApplicationPages.Digest => DigestPageNavItem,
            ApplicationPages.Settings => SettingsPageNavItem,
            _ => NewPageNavItem
        };

        if (ReferenceEquals(NavView.SelectedItem, target)) return;

        _suppressNavSelection = true;
        NavView.SelectedItem = target;
        _suppressNavSelection = false;
    }

    private void NavView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressNavSelection) return;
        if (!_initialNavigationDone) return;

        if (args.SelectedItem is not NavigationViewItem selectedItem || selectedItem.Tag is null) return;

        var page = Enum.Parse<ApplicationPages>(selectedItem.Tag.ToString()!);
        _navigationService?.NavigateToPage(page);
    }
}
