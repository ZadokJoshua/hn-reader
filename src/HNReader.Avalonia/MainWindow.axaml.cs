using Avalonia.Controls;
using Avalonia.Input;
using HNReader.Avalonia.Controls;
using HNReader.Avalonia.Services;
using HNReader.Avalonia.Viewmodels;
using HNReader.Core.Viewmodels;
using Microsoft.Extensions.DependencyInjection;

namespace HNReader.Avalonia;

public partial class MainWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private NavigationService? _navigationService;
    private NavigationViewModel? _navigationViewModel;

    // Parameterless constructor kept for the Avalonia XAML previewer only.
    public MainWindow() : this(null!)
    {
    }

    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;

        if (global::Avalonia.Application.Current is App app && app.Services != null)
        {
            _navigationService = app.Services.GetService<NavigationService>();
            _navigationService?.Initialize(ContentHost);

            // Assigned before the window's own DataContext, not after. The pane
            // needs its own view model - MainViewModel is shared with the WinUI
            // shell and knows nothing about navigation - and setting the window
            // DataContext first would propagate MainViewModel into the pane for
            // one layout pass, failing every binding in it on the way through.
            _navigationViewModel = app.Services.GetService<NavigationViewModel>();
            NavPane.DataContext = _navigationViewModel;

            app.Services.GetService<NotificationService>()?.Attach(this);

            Closed += (_, _) => _navigationViewModel?.Dispose();
        }

        DataContext = _mainViewModel;

        // Windows "Show animations" is off: drop the page cross-fade and let the
        // reduceMotion style (Styles/Resources.axaml) strip button transitions.
        if (MotionPreference.ReducedMotion)
        {
            Classes.Add("reduceMotion");
            ContentHost.PageTransition = null;
        }

        KeyDown += OnKeyDown;

        // Navigated directly, with no dispatcher post and no "has the initial
        // navigation happened yet" flag. Both used to be needed because
        // NavigationView applied its own default selection asynchronously and
        // raced this call; the pane is a plain ItemsControl with no selection of
        // its own, so there is nothing left to race.
        _navigationService?.NavigateToPage(HNReader.Core.Enums.ApplicationPages.Top);
    }

    // The window has no system title bar to drag, so the strip above the content
    // column provides one.
    private void OnTitleStripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        BeginMoveDrag(e);
    }

    private void OnTitleStripDoubleTapped(object? sender, TappedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control) return;

        var ordinal = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 1,
            Key.D2 or Key.NumPad2 => 2,
            Key.D3 or Key.NumPad3 => 3,
            Key.D4 or Key.NumPad4 => 4,
            Key.D5 or Key.NumPad5 => 5,
            Key.D6 or Key.NumPad6 => 6,
            Key.D7 or Key.NumPad7 => 7,
            _ => 0
        };

        // The number the user counts down the sidebar is the number they press,
        // because the view model indexes the visible items in render order rather
        // than mapping keys to pages by hand.
        if (ordinal > 0)
        {
            if (_navigationViewModel?.TryNavigateByOrdinal(ordinal) == true) e.Handled = true;
            return;
        }

        if (e.Key == Key.F)
        {
            // Reached through Content rather than by searching the visual tree:
            // during a page transition the outgoing page is still in the tree, and
            // a tree search would find its search box instead of the new one.
            var storiesControl = (ContentHost.Content as ContentControl)?.Content as StoriesPageControl;
            storiesControl?.FocusSearchBox();
            e.Handled = true;
        }
    }
}
