using HNReader.Core.Enums;
using HNReader.Core.Viewmodels;
using HNReader.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using System;
using System.IO;
using WinRT.Interop;
using HNReader.Core.Interfaces;

namespace HNReader.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private readonly NavigationService? _navigationService;
    private readonly ISettingsService? _settingsService;
    private AppWindow? _appWindow;
    private bool _suppressNavSelection;

    public MainWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;

        SetupTitleBar();
        SetPagesTags();

        if (Application.Current is App currentApp && currentApp.Services != null)
        {
            _navigationService = currentApp.Services.GetService<NavigationService>();
            if (_navigationService != null)
            {
                _navigationService.Initialize(ContentFrame);
                _navigationService.Navigated += OnPageNavigated;
            }
            _settingsService = currentApp.Services.GetService<ISettingsService>();

            if (_settingsService != null)
            {
                _settingsService.ThemeChanged += OnThemeChanged;
            }
        }

        SelectNavItemForPage(ApplicationPages.New);
        _navigationService?.NavigateToPage(ApplicationPages.New);

        UpdateTitleBarColors();
    }

    private void OnPageNavigated(ApplicationPages page)
    {
        SelectNavItemForPage(page);
    }

    private void OnThemeChanged(object? sender, AppTheme theme)
    {
        UpdateTitleBarColors();
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

    private void SetupTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow != null)
        {
            _appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
            _appWindow.Title = "HN Reader";

            var iconPath = Path.Combine(AppContext.BaseDirectory, "HnReaderIcon.ico");
            if (File.Exists(iconPath))
            {
                _appWindow.SetIcon(iconPath);
            }
        }
    }

    private void UpdateTitleBarColors()
    {
        if (_appWindow?.TitleBar == null) return;

        var titleBar = _appWindow.TitleBar;
        var isDarkMode = IsDarkTheme();

        if (isDarkMode)
        {
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonPressedForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = Colors.Gray;
        }
        else
        {
            titleBar.ButtonForegroundColor = Colors.Black;
            titleBar.ButtonHoverForegroundColor = Colors.Black;
            titleBar.ButtonPressedForegroundColor = Colors.Black;
            titleBar.ButtonInactiveForegroundColor = Colors.Gray;
        }

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonHoverBackgroundColor = isDarkMode
            ? Windows.UI.Color.FromArgb(25, 255, 255, 255)
            : Windows.UI.Color.FromArgb(25, 0, 0, 0);
        titleBar.ButtonPressedBackgroundColor = isDarkMode
            ? Windows.UI.Color.FromArgb(40, 255, 255, 255)
            : Windows.UI.Color.FromArgb(40, 0, 0, 0);
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
    }

    private bool IsDarkTheme()
    {
        if (_settingsService != null)
        {
            return _settingsService.Theme switch
            {
                AppTheme.Dark => true,
                AppTheme.Light => false,
                _ => IsSystemDarkTheme()
            };
        }

        return IsSystemDarkTheme();
    }

    private bool IsSystemDarkTheme()
    {
        if (Content is FrameworkElement rootElement)
        {
            var actualTheme = rootElement.ActualTheme;
            return actualTheme == ElementTheme.Dark;
        }

        return Application.Current.RequestedTheme == ApplicationTheme.Dark;
    }

    private void SetPagesTags()
    {
        NewPageNavItem.Tag = ApplicationPages.New.ToString();
        TopPageNavItem.Tag = ApplicationPages.Top.ToString();
        FavouritesPageNavItem.Tag = ApplicationPages.Favourites.ToString();
        BestPageNavItem.Tag = ApplicationPages.Best.ToString();
        ShowPageNavItem.Tag = ApplicationPages.Show.ToString();
        AskPageNavItem.Tag = ApplicationPages.Ask.ToString();
        SettingsPageNavItem.Tag = ApplicationPages.Settings.ToString();
    }

    private void NavView_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressNavSelection) return;

        var selectedItem = args.SelectedItem as Microsoft.UI.Xaml.Controls.NavigationViewItem;
        if (selectedItem?.Tag == null) return;

        SetContentFramePage(selectedItem);
    }

    private void SetContentFramePage(Microsoft.UI.Xaml.Controls.NavigationViewItem selectedItem)
    {
        var page = Enum.Parse<ApplicationPages>(selectedItem.Tag.ToString()!);
        _navigationService?.NavigateToPage(page);
    }
}
