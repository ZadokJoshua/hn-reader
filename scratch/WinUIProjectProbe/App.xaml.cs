using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;
using HNReader.Core.Viewmodels;
using HNReader.WinUI.Factories;
using HNReader.WinUI.Services;
using HNReader.WinUI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace HNReader.WinUI;

public partial class App : Application
{
    private Window? _window;

    public IServiceProvider Services { get; }

    public static Window? CurrentWindow { get; private set; }

    public App()
    {
        Services = ConfigureServices();
        InitializeComponent();

        UnhandledException += OnUnhandledException;

        var settingsService = Services.GetRequiredService<ISettingsService>();
        settingsService.ThemeChanged += OnThemeChanged;
        ApplyTheme(settingsService.Theme);
    }

    private void OnThemeChanged(object? sender, AppTheme theme)
    {
        ApplyTheme(theme);
    }

    private static void ApplyTheme(AppTheme theme)
    {
        if (CurrentWindow?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    public void ApplyCurrentTheme()
    {
        var settingsService = Services.GetRequiredService<ISettingsService>();
        ApplyTheme(settingsService.Theme);
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        var errorMessage = e.Exception?.ToString() ?? "An unknown error occurred.";
        System.Diagnostics.Debug.WriteLine($"[UNHANDLED EXCEPTION] {errorMessage}");

        _window?.DispatcherQueue?.TryEnqueue(async () =>
        {
            await ErrorDialogService.ShowErrorAsync(
                "Unexpected Error",
                $"An unexpected error occurred:\n\n{e.Message}");
        });
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var localFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HNReader");
        Directory.CreateDirectory(localFolder);
        var favouritesDbPath = Path.Combine(localFolder, "favorites.db");

        // FavoritesService opens a LiteDB file synchronously in its constructor.
        // Wrap it in Lazy<T> so the I/O is deferred until the user actually navigates
        // to a page that needs favourites — keeps cold-start snappy.
        services.AddSingleton<Lazy<IFavoritesService>>(_ => new Lazy<IFavoritesService>(
            () => new FavoritesService(favouritesDbPath),
            LazyThreadSafetyMode.ExecutionAndPublication));
        services.AddSingleton<ISettingsService>(_ => new SettingsService(localFolder));

        services.AddTransient<TopPageViewModel>();
        services.AddTransient<NewPageViewModel>();
        services.AddTransient<FavouritesPageViewModel>();
        services.AddTransient<BestPageViewModel>();
        services.AddTransient<ShowPageViewModel>();
        services.AddTransient<AskPageViewModel>();
        services.AddSingleton<SettingsViewModel>();

        services.AddSingleton<MainViewModel>();

        services.AddSingleton<PageFactory>();
        services.AddSingleton<NavigationService>();

        services.AddTransient<TopPage>();
        services.AddTransient<NewPage>();
        services.AddTransient<FavouritesPage>();
        services.AddTransient<BestPage>();
        services.AddTransient<ShowPage>();
        services.AddTransient<AskPage>();
        services.AddTransient<SettingsPage>();

        services.AddHttpClient<HNClient>(client =>
        {
            client.BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/");
            // 15s is enough for HN's fast API. Anything longer usually means a
            // network problem we'd rather surface to the user than wait on.
            client.Timeout = TimeSpan.FromSeconds(15);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        });

        services.AddHttpClient<HNWebClient>(client =>
        {
            client.BaseAddress = new Uri("https://news.ycombinator.com/");
            // Comment scraping can be slower — bump this one.
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "HNReader/1.0");
        });

        services.AddSingleton<Func<ApplicationPages, BaseViewModel>>(x => name => name switch
        {
            ApplicationPages.Top => x.GetRequiredService<TopPageViewModel>(),
            ApplicationPages.New => x.GetRequiredService<NewPageViewModel>(),
            ApplicationPages.Favourites => x.GetRequiredService<FavouritesPageViewModel>(),
            ApplicationPages.Best => x.GetRequiredService<BestPageViewModel>(),
            ApplicationPages.Show => x.GetRequiredService<ShowPageViewModel>(),
            ApplicationPages.Ask => x.GetRequiredService<AskPageViewModel>(),
            ApplicationPages.Settings => x.GetRequiredService<SettingsViewModel>(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        });

        services.AddSingleton<Func<ApplicationPages, Page>>(x => name => name switch
        {
            ApplicationPages.Top => x.GetRequiredService<TopPage>(),
            ApplicationPages.New => x.GetRequiredService<NewPage>(),
            ApplicationPages.Favourites => x.GetRequiredService<FavouritesPage>(),
            ApplicationPages.Best => x.GetRequiredService<BestPage>(),
            ApplicationPages.Show => x.GetRequiredService<ShowPage>(),
            ApplicationPages.Ask => x.GetRequiredService<AskPage>(),
            ApplicationPages.Settings => x.GetRequiredService<SettingsPage>(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        });

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var mainViewModel = Services.GetRequiredService<MainViewModel>();
        _window = new MainWindow(mainViewModel);
        CurrentWindow = _window;
        _window.Activate();

        ApplyCurrentTheme();
    }
}
