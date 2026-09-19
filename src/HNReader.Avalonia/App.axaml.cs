using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using HNReader.Avalonia.Factories;
using HNReader.Avalonia.Services;
using HNReader.Avalonia.Views;
using HNReader.Core.Enums;
using HNReader.Core.Constants;
using HNReader.Core.Interfaces;
using HNReader.Core.Services;
using HNReader.Core.Services.Logging;
using HNReader.Core.Viewmodels;
using Microsoft.Extensions.DependencyInjection;

namespace HNReader.Avalonia;

public partial class App : Application
{
    private ILogger? _logger;

    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        // Logger is constructed first so it can capture any later DI/startup failures.
        _logger = new Logger(AppPaths.LogDirectory);
        _logger.LogInformation("App", "process starting", context: null);

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Services = ConfigureServices(_logger);

        // Must happen before the XAML loads, so no binding can run against the
        // library default. Favicon requests are dispatched by sentinel; every other
        // ImageLoader.Source (the digest preview images) still loads normally.
        ImageLoader.AsyncImageLoader = Services.GetRequiredService<FaviconImageLoader>();

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = global::Avalonia.Controls.ShutdownMode.OnMainWindowClose;

            var mainViewModel = Services.GetRequiredService<MainViewModel>();
            var window = new MainWindow(mainViewModel);
            desktop.MainWindow = window;

            desktop.Exit += async (_, __) =>
            {
                if (_logger is not null)
                {
                    try { await _logger.FlushAsync(); }
                    catch { /* best effort during shutdown */ }
                }
            };

            var settingsService = Services.GetRequiredService<ISettingsService>();
            settingsService.ThemeChanged += (_, theme) => ApplyTheme(theme);
            ApplyTheme(settingsService.Theme);

            _logger?.LogInformation("App", "main window activated", context: null);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyTheme(AppTheme theme)
    {
        RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
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

        services.AddSingleton<Lazy<IFavoritesService>>(_ => new Lazy<IFavoritesService>(
            () => new FavoritesService(AppPaths.FavouritesDbPath),
            LazyThreadSafetyMode.ExecutionAndPublication));
        services.AddSingleton<ISettingsService>(_ => new SettingsService(AppPaths.SettingsDirectory, logger));

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

        // Singleton, unlike the story pages: the digest is fetched once and
        // reused across navigations (see DigestPageViewModel's staleness
        // window). Safe because it does not implement IDisposable, which
        // NavigationService would otherwise call on navigate-away.
        services.AddSingleton<DigestPageViewModel>(sp => new DigestPageViewModel(
            sp.GetRequiredService<DigestClient>(),
            sp.GetRequiredService<ISettingsService>(),
            sp.GetService<ILogger>()));

        services.AddSingleton<MainViewModel>(sp => new MainViewModel(
            sp.GetRequiredService<Lazy<IFavoritesService>>(),
            sp.GetService<ILogger>()));

        services.AddSingleton<PageFactory>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<NotificationService>();

        services.AddTransient<TopView>();
        services.AddTransient<NewView>();
        services.AddTransient<FavouritesView>();
        services.AddTransient<BestView>();
        services.AddTransient<ShowView>();
        services.AddTransient<AskView>();
        services.AddTransient<SettingsView>();
        services.AddTransient<DigestView>();

        services.AddHttpClient<HNClient>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/");
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        });
        services.AddTransient<HNClient>(sp => new HNClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HNClient)),
            sp.GetService<ILogger>()));

        services.AddHttpClient<HNWebClient>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://news.ycombinator.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "HNReader/1.0");
        });
        services.AddTransient<HNWebClient>(sp => new HNWebClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HNWebClient)),
            sp.GetService<ILogger>()));

        // Our own digest server — the only non-Hacker-News host this app talks
        // to, and only when the digest feature is switched on.
        services.AddHttpClient<DigestClient>((sp, client) =>
        {
            client.BaseAddress = new Uri(AppFileNames.DIGEST_SERVER_BASE_URL);
            client.Timeout = TimeSpan.FromSeconds(AppFileNames.DIGEST_HTTP_TIMEOUT_SECONDS);
            client.DefaultRequestHeaders.Add("User-Agent", "HNReader/1.0");
        });
        services.AddTransient<DigestClient>(sp => new DigestClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(DigestClient)),
            sp.GetService<ILogger>()));

        // Favicons. Singleton, unlike the clients above: this service IS its caches, and a
        // transient would hand every caller an empty one.
        services.AddHttpClient<FaviconService>((sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(AppFileNames.FAVICON_CHAIN_TIMEOUT_SECONDS + 2);
            client.DefaultRequestHeaders.Add("User-Agent", "HNReader/1.0");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = AppFileNames.FAVICON_MAX_REDIRECTS
        });
        services.AddSingleton<IFaviconService>(sp => new FaviconService(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(FaviconService)),
            AppPaths.FaviconCacheDirectory,
            sp.GetService<ILogger>()));

        services.AddSingleton<FaviconImageLoader>(sp => new FaviconImageLoader(
            sp.GetRequiredService<IFaviconService>(),
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
            ApplicationPages.Digest => x.GetRequiredService<DigestPageViewModel>(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        });

        services.AddSingleton<Func<ApplicationPages, global::Avalonia.Controls.UserControl>>(x => name => name switch
        {
            ApplicationPages.Top => x.GetRequiredService<TopView>(),
            ApplicationPages.New => x.GetRequiredService<NewView>(),
            ApplicationPages.Favourites => x.GetRequiredService<FavouritesView>(),
            ApplicationPages.Best => x.GetRequiredService<BestView>(),
            ApplicationPages.Show => x.GetRequiredService<ShowView>(),
            ApplicationPages.Ask => x.GetRequiredService<AskView>(),
            ApplicationPages.Settings => x.GetRequiredService<SettingsView>(),
            ApplicationPages.Digest => x.GetRequiredService<DigestView>(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        });

        return services.BuildServiceProvider();
    }
}
