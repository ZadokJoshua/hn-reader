using HNReader.Core.Enums;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;
using HNReader.Core.Services.Logging;
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
using System.Threading.Tasks;

namespace HNReader.WinUI;

public partial class App : Application
{
    private Window? _window;
    private ILogger? _logger;

    public IServiceProvider Services { get; }

    public static Window? CurrentWindow { get; private set; }

    public App()
    {
        // Logger is constructed first so it can capture any later DI/startup
        // failures. It is owned by App and disposed on process exit.
        _logger = new Logger();
        _logger.LogInformation("App", "process starting",
            context: null);

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Services = ConfigureServices(_logger);
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

        // Capture the full exception details (type, message, stack, inner) into
        // the log before we do anything else. If the dispatcher is the thing
        // that's about to die, this still gives us a complete diagnostic record.
        var fullText = e.Exception?.ToString() ?? "An unknown error occurred.";
        _logger?.LogError("App.UnhandledXaml", e.Exception?.Message ?? "Unhandled XAML exception", e.Exception);

        // Flush synchronously via .Wait — the logger is thread-safe and the
        // background consumer can't drain its channel if the dispatcher dies.
        try { _logger?.FlushAsync().GetAwaiter().GetResult(); }
        catch { /* best effort */ }

        try
        {
            _window?.DispatcherQueue?.TryEnqueue(async () =>
            {
                try
                {
                    await ErrorDialogService.ShowErrorAsync(
                        "Unexpected Error",
                        $"An unexpected error occurred:\n\n{e.Message}\n\nDetails were written to the log file.",
                        logFilePath: _logger?.CurrentLogFilePath);
                }
                catch
                {
                    // Even the error dialog failed — don't let it recurse.
                }
            });
        }
        catch
        {
            // Dispatcher itself unavailable; log entry above is the only record.
        }
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        try
        {
            if (e.ExceptionObject is Exception ex)
            {
                _logger?.LogFatal("App.Domain", "unhandled AppDomain exception", ex);
            }
            else
            {
                _logger?.LogFatal("App.Domain", "unhandled non-Exception AppDomain error");
            }

            // Block briefly so the log entry reaches disk before the process
            // is torn down by the AppDomain unhandled-exception escalation.
            try { _logger?.FlushAsync().GetAwaiter().GetResult(); }
            catch { /* best effort */ }
        }
        catch
        {
            // Last resort — never throw from an unhandled-exception handler.
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            _logger?.LogError("App.TaskScheduler", "unobserved task exception", e.Exception);
            e.SetObserved();
        }
        catch
        {
            // Never throw from this handler.
        }
    }

    private static ServiceProvider ConfigureServices(ILogger logger)
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILogger>(logger);

        var localFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HNReader");
        Directory.CreateDirectory(localFolder);
        var favouritesDbPath = Path.Combine(localFolder, "favorites.db");

        // FavoritesService opens a LiteDB file synchronously in its constructor.
        // Wrap it in Lazy<T> so the I/O is deferred until the user actually navigates
        // to a page that needs favourites — keeps cold-start snappy.
        services.AddSingleton<Lazy<IFavoritesService>>(_ => new Lazy<IFavoritesService>(
            () => new FavoritesService(favouritesDbPath),
            LazyThreadSafetyMode.ExecutionAndPublication));
        services.AddSingleton<ISettingsService>(_ => new SettingsService(localFolder, logger));

        services.AddTransient<TopPageViewModel>(sp => new TopPageViewModel(
            sp.GetRequiredService<HNClient>(),
            sp.GetRequiredService<Lazy<IFavoritesService>>(),
            sp.GetRequiredService<HNWebClient>(),
            sp.GetService<ILogger>()));
        services.AddTransient<NewPageViewModel>(sp => new NewPageViewModel(
            sp.GetRequiredService<HNClient>(),
            sp.GetRequiredService<Lazy<IFavoritesService>>(),
            sp.GetRequiredService<HNWebClient>(),
            sp.GetService<ILogger>()));
        services.AddTransient<FavouritesPageViewModel>(sp => new FavouritesPageViewModel(
            sp.GetRequiredService<HNClient>(),
            sp.GetRequiredService<Lazy<IFavoritesService>>(),
            sp.GetRequiredService<HNWebClient>(),
            sp.GetService<ILogger>()));
        services.AddTransient<BestPageViewModel>(sp => new BestPageViewModel(
            sp.GetRequiredService<HNClient>(),
            sp.GetRequiredService<Lazy<IFavoritesService>>(),
            sp.GetRequiredService<HNWebClient>(),
            sp.GetService<ILogger>()));
        services.AddTransient<ShowPageViewModel>(sp => new ShowPageViewModel(
            sp.GetRequiredService<HNClient>(),
            sp.GetRequiredService<Lazy<IFavoritesService>>(),
            sp.GetRequiredService<HNWebClient>(),
            sp.GetService<ILogger>()));
        services.AddTransient<AskPageViewModel>(sp => new AskPageViewModel(
            sp.GetRequiredService<HNClient>(),
            sp.GetRequiredService<Lazy<IFavoritesService>>(),
            sp.GetRequiredService<HNWebClient>(),
            sp.GetService<ILogger>()));
        services.AddSingleton<SettingsViewModel>();

        services.AddSingleton<MainViewModel>(sp => new MainViewModel(
            sp.GetRequiredService<Lazy<IFavoritesService>>(),
            sp.GetService<ILogger>()));

        services.AddSingleton<PageFactory>();
        services.AddSingleton<NavigationService>();

        services.AddTransient<TopPage>();
        services.AddTransient<NewPage>();
        services.AddTransient<FavouritesPage>();
        services.AddTransient<BestPage>();
        services.AddTransient<ShowPage>();
        services.AddTransient<AskPage>();
        services.AddTransient<SettingsPage>();

        services.AddHttpClient<HNClient>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/");
            // 15s is enough for HN's fast API. Anything longer usually means a
            // network problem we'd rather surface to the user than wait on.
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        });
        // Decorate the registered HNClient so the resolved instance also gets
        // an ILogger injected via the secondary ctor.
        services.AddTransient<HNClient>(sp => new HNClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HNClient)),
            sp.GetService<ILogger>()));

        services.AddHttpClient<HNWebClient>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://news.ycombinator.com/");
            // Comment scraping can be slower — bump this one.
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "HNReader/1.0");
        });
        services.AddTransient<HNWebClient>(sp => new HNWebClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HNWebClient)),
            sp.GetService<ILogger>()));

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
        _logger?.LogInformation("App", "OnLaunched");

        var mainViewModel = Services.GetRequiredService<MainViewModel>();
        _window = new MainWindow(mainViewModel);
        CurrentWindow = _window;
        _window.Activate();

        ApplyCurrentTheme();

        _logger?.LogInformation("App", "main window activated",
            context: null);
    }
}
