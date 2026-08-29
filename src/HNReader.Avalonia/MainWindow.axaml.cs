using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using HNReader.Avalonia.Controls;
using HNReader.Avalonia.Services;
using HNReader.Core.Enums;
using HNReader.Core.Viewmodels;
using Microsoft.Extensions.DependencyInjection;

namespace HNReader.Avalonia;

public partial class MainWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private NavigationService? _navigationService;
    private bool _suppressNavSelection;

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

        Width = 1400;
        Height = 900;
        Title = "HN Reader";

        if (global::Avalonia.Application.Current is App app && app.Services != null)
        {
            _navigationService = app.Services.GetService<NavigationService>();
            if (_navigationService != null)
            {
                _navigationService.Initialize(ContentHost);
                _navigationService.Navigated += OnPageNavigated;
            }

            app.Services.GetService<NotificationService>()?.Attach(this);
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
        SelectNavItemForPage(ApplicationPages.New);
        _navigationService?.NavigateToPage(ApplicationPages.New);
        _initialNavigationDone = true;
    }

    private void OnPageNavigated(ApplicationPages page) => SelectNavItemForPage(page);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control) return;

        var page = e.Key switch
        {
            Key.D1 or Key.NumPad1 => ApplicationPages.New,
            Key.D2 or Key.NumPad2 => ApplicationPages.Top,
            Key.D3 or Key.NumPad3 => ApplicationPages.Best,
            Key.D4 or Key.NumPad4 => ApplicationPages.Show,
            Key.D5 or Key.NumPad5 => ApplicationPages.Ask,
            Key.D6 or Key.NumPad6 => ApplicationPages.Favourites,
            _ => (ApplicationPages?)null
        };

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
        SettingsPageNavItem.Tag = ApplicationPages.Settings.ToString();
    }

    private void SelectNavItemForPage(ApplicationPages page)
    {
        _suppressNavSelection = true;
        NavView.SelectedItem = page switch
        {
            ApplicationPages.New => NewPageNavItem,
            ApplicationPages.Top => TopPageNavItem,
            ApplicationPages.Best => BestPageNavItem,
            ApplicationPages.Show => ShowPageNavItem,
            ApplicationPages.Ask => AskPageNavItem,
            ApplicationPages.Favourites => FavouritesPageNavItem,
            ApplicationPages.Settings => SettingsPageNavItem,
            _ => NewPageNavItem
        };
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
